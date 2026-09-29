// File: GyroRepairMod.cs
// Gyrocopter Repair Mod
// Author: Frilioth
// Version: 1.4.0
//
// v1.4.0 CHANGES (from v1.3.1)
//   - The coolant slot now accepts MURKY OR CLEAN water: drinkJarRiverWater or
//     drinkJarBoiledWater. Either one alone fills the slot.
//     drinkJarPureMineralWater is deliberately NOT accepted.
//   - This needed a structural change, not a name added to a list. The old
//     RequiredParts was a flat HashSet of item names and the completion test was
//     "found.Count < RequiredParts.Count". Adding a second water name to that set
//     would have raised the count to 9 and demanded BOTH waters. Parts are now
//     SLOTS, each accepting one or more item names, and completion counts
//     SATISFIED SLOTS rather than distinct item names.
//   - PartLabels and RequiredParts are replaced by Parts and AcceptedItems.
//     Nothing outside this file referenced either, and both uses inside it
//     (UpdateStatusLabels and the completion check) are updated below.
//
// v1.3.1 CHANGES (from v1.3.0)
//   - Fixed the container clear on completion. te is ALREADY a TEFeatureStorage
//     in 3.2, so (te as TileEntityComposite)?.GetFeature<TEFeatureStorage>()
//     always returned null and the code silently fell through to clearing the
//     item array by hand. SetEmpty() never ran. Caught by its own warning line
//     in the 7 Sep play log, not by the compiler: both casts are legal C#.
//
// v1.3.0 CHANGES (from v1.2.1) - 7 DAYS TO DIE 3.2 PORT
//   Every item confirmed against the 3.2 Assembly-CSharp, not assumed.
//   - XUiC_LootWindowGroup.OnOpen removed. Retargeted to openContainer, which is
//     where BOTH the immediate and the open-delay paths converge.
//   - GUIWindowManager: 4-arg Open removed, CloseIfOpen removed.
//   - XUiV_Label.IsDirty removed. SetTextImmediately / ForceTextUpdate replace it.
//   - XUiController.RefreshBindings(bool) lost its parameter;
//     RefreshBindingsSelfAndChildren covers the "true" case.
//   - WorldBase.SetBlockRPC now takes a BlockValueRef instead of a cluster index
//     plus Vector3i.
//   NOTE: TEFeatureStorage was already used here, but see v1.3.1 - the
//   TileEntityComposite cast in the completion path was in fact broken.
//
// Standalone mod — no dependency on HospitalChallengeMod or any other mod.
//
// v1.2.1 CHANGES (from v1.2.0):
// - Restored dedicated gyroRepairStatus window group — status panel was
//   showing on all loot containers because xui.xml was appending it to
//   the looting group. Now opened/closed explicitly from C#.
//
// v1.2.0 CHANGES (from v1.1.0):
// - hospitalGyroEngine replaced with vanilla smallEngine
// - hospitalGyroBattery replaced with vanilla carBattery
//
// HOW IT WORKS:
// The block "gyroRepairBlock" is a CompositeTileEntity container with 8 slots.
// When the player opens it, a companion status panel opens on the right side
// showing which of the 8 required parts are present. Labels update live every
// 0.25s as items are placed or removed.
// When the player closes the loot window with all 8 parts present, the block
// is removed and a working vehicleGyrocopter entity spawns at the block position.
//
// REQUIRED PARTS (one of each, any slot order):
//   resourceRadiator
//   drinkJarRiverWater  OR  drinkJarBoiledWater   (coolant, either is fine)
//   smallEngine
//   carBattery
//   hospitalGyroSparkPlugs
//   hospitalGyroControlCables
//   hospitalGyroTailAssembly
//   hospitalGyroBlades

using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

public class GyroRepairMod : IModApi
{
    public const string ModVersion = "1.4.0";

    public void InitMod(Mod _modInstance)
    {
        Debug.Log("[GyroRepair] Mod initializing v" + ModVersion);
        var harmony = new Harmony("com.frilioth.gyrorepair");
        harmony.PatchAll();
        Debug.Log("[GyroRepair] Harmony patches applied.");
    }
}

// ---------------------------------------------------------------
// SHARED DATA
//
// A part is a SLOT, not an item name. Most slots accept exactly one item, but
// the coolant slot accepts either murky or clean water, so the structure has to
// carry a set of acceptable names per slot rather than a single name.
//
// This is why completion counts SATISFIED SLOTS and not distinct item names:
// with a flat name list, adding a second accepted water would have raised the
// required count and demanded both.
// ---------------------------------------------------------------
public sealed class GyroPart
{
    public readonly string Label;
    public readonly string[] Accepts;

    public GyroPart(string label, params string[] accepts)
    {
        Label = label;
        Accepts = accepts;
    }

    // A slot is filled if ANY of its accepted items is present.
    public bool IsSatisfiedBy(HashSet<string> present)
    {
        for (int i = 0; i < Accepts.Length; i++)
            if (present.Contains(Accepts[i])) return true;
        return false;
    }
}

public static class GyroRepairData
{
    // Order here must match the order of labels in windows.xml exactly.
    public static readonly List<GyroPart> Parts = new List<GyroPart>
    {
        new GyroPart("Engine",         "smallEngine"               ),
        new GyroPart("Battery",        "carBattery"                ),
        new GyroPart("Spark Plugs",    "hospitalGyroSparkPlugs"    ),
        new GyroPart("Control Cables", "hospitalGyroControlCables" ),
        new GyroPart("Tail Assembly",  "hospitalGyroTailAssembly"  ),
        new GyroPart("Rotor Blades",   "hospitalGyroBlades"        ),
        new GyroPart("Radiator",       "resourceRadiator"          ),

        // Murky or clean. Boiled water counts because a player who has been
        // purifying drinking water already has usable coolant, and the basement
        // standing water stops being the only route to this part.
        // drinkJarPureMineralWater is NOT accepted, on purpose.
        new GyroPart("Coolant Water",  "drinkJarRiverWater", "drinkJarBoiledWater")
    };

    // Flat set of every name any slot will take. Used only to decide whether an
    // item in the container is worth looking at; it is NOT the completion count.
    public static readonly HashSet<string> AcceptedItems = BuildAcceptedItems();

    private static HashSet<string> BuildAcceptedItems()
    {
        HashSet<string> set = new HashSet<string>();
        foreach (GyroPart p in Parts)
            foreach (string name in p.Accepts)
                set.Add(name);
        return set;
    }
}

// ---------------------------------------------------------------
// OPEN PATCH
// Fires when any loot window group opens.
// Detects gyroRepairBlock and opens the companion status window,
// then starts a coroutine to keep it updated.
// ---------------------------------------------------------------
// 3.2 PORT: XUiC_LootWindowGroup.OnOpen no longer exists. The single point that
// both the immediate path (OpenLooting) and the delayed path (openTimerFinished)
// go through is openContainer, confirmed by reading the 3.2 IL: it assigns te,
// calls lootWindow.SetTileEntityChest, then windowManager.Open. Patching
// OpenLooting instead would fire too early on any container with an open delay,
// because OpenLooting returns before that timer elapses.
// _te is taken as a parameter rather than read from __instance.te so it cannot
// be null even if the assignment order ever changes.
[HarmonyPatch(typeof(XUiC_LootWindowGroup), "openContainer")]
public class GyroRepairWindowOpenPatch
{
    // Set to false by the close patch to stop the coroutine.
    public static bool statusWindowOpen = false;

    static void Postfix(XUiC_LootWindowGroup __instance, ITileEntityLootable _te)
    {
        try
        {
            ITileEntityLootable te = _te;
            if (te == null) return;

            Block block = null;
            try { block = te.blockValue.Block; } catch { return; }
            if (block == null || block.GetBlockName() != "gyroRepairBlock") return;

            EntityPlayerLocal player = GameManager.Instance?.World?.GetPrimaryPlayer();
            if (player == null) return;

            LocalPlayerUI ui = LocalPlayerUI.GetUIForPlayer(player);
            if (ui == null || ui.xui == null) return;

            // Open the companion status window and start live update coroutine
            // 3.2: the 4-arg Open overload is gone; only (name,bool) and
            // (name,bool,bIsNotEscClosable) remain. Non-modal, as before.
            ui.windowManager.Open("gyroRepairStatus", false);
            Debug.Log("[GyroRepair] Status window opened.");

            statusWindowOpen = true;
            GameManager.Instance.StartCoroutine(LiveUpdateLoop(te, ui.xui));
            GameManager.Instance.StartCoroutine(DelayedHeaderFix(ui.xui, te));
        }
        catch (Exception e)
        {
            Debug.LogError("[GyroRepair] WindowOpenPatch error: " + e.Message);
        }
    }

    static IEnumerator DelayedHeaderFix(XUi xui, ITileEntityLootable te)
    {
        yield return new WaitForSeconds(0.1f);
        try
        {
            // Fix the top header and left panel name
            XUiC_LootWindowGroup lootGroup = (XUiC_LootWindowGroup)xui.FindWindowGroupByName("looting");
            if (lootGroup != null)
            {
                string title = Localization.Get("gyroRepairBlockTitle", false);
                if (lootGroup.nonPagingHeaderWindow != null)
                    lootGroup.nonPagingHeaderWindow.SetHeader(title);
                if (lootGroup.lootWindow != null)
                {
                    lootGroup.lootWindow.lootContainerName = title;
                    // 3.2: RefreshBindings lost its bool. The "with children"
                    // behaviour is now a separate method.
                    lootGroup.lootWindow.RefreshBindingsSelfAndChildren();
                }
            }

            // Replace the Inspect panel with repair instructions
            XUiC_EmptyInfoWindow emptyInfo = xui
                .FindWindowGroupByName("backpack")
                ?.GetChildByType<XUiC_EmptyInfoWindow>();

            if (emptyInfo != null)
            {
                // 3.2: XUiV_Label.IsDirty is gone. SetTextImmediately does the
                // assign-and-refresh that Text + IsDirty used to do together.
                emptyInfo.descriptionText.SetTextImmediately(Localization.Get("gyroRepairInspectBody", false));
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[GyroRepair] DelayedHeaderFix failed: " + ex.Message);
        }
    }

    static IEnumerator LiveUpdateLoop(ITileEntityLootable te, XUi xui)
    {
        // Wait one frame for the window to fully initialise before grabbing labels
        yield return new WaitForSeconds(0.1f);

        // Get labels from our dedicated status window group, filtering out the header label
        // by checking for our known prefixes — header label text is "Repair Status"
        XUiV_Label[] allLabels = xui.FindWindowGroupByName("gyroRepairStatus")?.GetChildrenByViewType<XUiV_Label>();
        XUiV_Label[] statusLabels = null;
        if (allLabels != null)
        {
            var ourLabels = new System.Collections.Generic.List<XUiV_Label>();
            foreach (XUiV_Label lbl in allLabels)
                if (lbl.Text != null && (
                    lbl.Text.StartsWith("[ ]") ||
                    lbl.Text.StartsWith("[ff0000]") ||
                    lbl.Text.StartsWith("[00ff00]")))
                    ourLabels.Add(lbl);
            statusLabels = ourLabels.ToArray();
            Debug.Log("[GyroRepair] Found " + statusLabels.Length + " status labels.");
        }

        while (statusWindowOpen)
        {
            UpdateStatusLabels(te, statusLabels);
            yield return new WaitForSeconds(0.25f);
        }
        Debug.Log("[GyroRepair] Live update loop stopped.");
    }

    static void UpdateStatusLabels(ITileEntityLootable te, XUiV_Label[] labels)
    {
        try
        {
            if (labels == null) return;

            // Collect which accepted items are currently in the container
            HashSet<string> foundParts = new HashSet<string>();
            if (te.items != null)
            {
                foreach (ItemStack stack in te.items)
                {
                    if (stack == null || stack.IsEmpty()) continue;
                    string itemName = stack.itemValue?.ItemClass?.GetItemName();
                    if (itemName != null && GyroRepairData.AcceptedItems.Contains(itemName))
                        foundParts.Add(itemName);
                }
            }

            // Update labels by index — array contains exactly our 8 part labels, no header
            for (int i = 0; i < GyroRepairData.Parts.Count && i < labels.Length; i++)
            {
                GyroPart entry = GyroRepairData.Parts[i];
                bool found = entry.IsSatisfiedBy(foundParts);
                // 3.2: XUiV_Label.IsDirty is gone; SetTextImmediately replaces
                // the old Text + IsDirty pair.
                labels[i].SetTextImmediately((found ? "[00ff00][+][-] " : "[ff0000][x][-] ") + entry.Label);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[GyroRepair] UpdateStatusLabels error: " + e.Message);
        }
    }
}

// ---------------------------------------------------------------
// CLOSE PATCH
// Fires when any loot window group closes.
// Stops the live update coroutine, closes the companion status
// window, then checks for gyroRepairBlock completion.
// ---------------------------------------------------------------
[HarmonyPatch(typeof(XUiC_LootWindowGroup), "OnClose")]
public class GyroRepairContainerPatch
{
    static void Postfix(XUiC_LootWindowGroup __instance)
    {
        try
        {
            ITileEntityLootable te = __instance.te;
            if (te == null) return;

            Block block = null;
            try { block = te.blockValue.Block; } catch { return; }
            if (block == null) return;

            // Always stop the coroutine and close the status window on any loot close,
            // in case our block was the one that was open.
            GyroRepairWindowOpenPatch.statusWindowOpen = false;

            EntityPlayerLocal playerClose = GameManager.Instance?.World?.GetPrimaryPlayer();
            if (playerClose != null)
            {
                LocalPlayerUI uiClose = LocalPlayerUI.GetUIForPlayer(playerClose);
                // 3.2: CloseIfOpen is gone. IsWindowOpen + Close is the equivalent.
                if (uiClose != null && uiClose.windowManager.IsWindowOpen("gyroRepairStatus"))
                    uiClose.windowManager.Close("gyroRepairStatus");
            }

            if (block.GetBlockName() != "gyroRepairBlock") return;

            // Restore the Inspect panel to default
            EntityPlayerLocal player = GameManager.Instance?.World?.GetPrimaryPlayer();
            if (player != null)
            {
                try
                {
                    LocalPlayerUI ui = LocalPlayerUI.GetUIForPlayer(player);
                    XUiC_EmptyInfoWindow emptyInfo = ui?.xui
                        .FindWindowGroupByName("backpack")
                        ?.GetChildByType<XUiC_EmptyInfoWindow>();
                    if (emptyInfo != null)
                    {
                        emptyInfo.UpdateDescriptionText();
                        // 3.2: IsDirty removed; ForceTextUpdate is the equivalent
                        // when the text has already been set by the call above.
                        emptyInfo.descriptionText.ForceTextUpdate();
                    }
                }
                catch { }
            }

            // --- Completion check ---

            ItemStack[] items = te.items;
            if (items == null) return;

            HashSet<string> found = new HashSet<string>();
            foreach (ItemStack stack in items)
            {
                if (stack == null || stack.IsEmpty()) continue;
                string itemName = stack.itemValue?.ItemClass?.GetItemName();
                if (itemName != null && GyroRepairData.AcceptedItems.Contains(itemName))
                    found.Add(itemName);
            }

            // Count SATISFIED SLOTS, not distinct item names. A slot with two
            // acceptable items must not count as two, and must not need both.
            int satisfied = 0;
            string missing = "";
            foreach (GyroPart part in GyroRepairData.Parts)
            {
                if (part.IsSatisfiedBy(found)) satisfied++;
                else missing += (missing.Length > 0 ? ", " : "") + part.Label;
            }

            Debug.Log("[GyroRepair] Parts present on close: " + satisfied + "/" + GyroRepairData.Parts.Count
                + (missing.Length > 0 ? " — missing: " + missing : ""));

            if (satisfied < GyroRepairData.Parts.Count) return;

            Debug.Log("[GyroRepair] All parts placed — spawning gyrocopter.");

            World world = GameManager.Instance?.World;
            if (world == null) return;

            Vector3i blockPos = te.ToWorldPos();

            // Clear the container
            // 3.2 PORT FIX (v1.3.1): te is ALREADY the TEFeatureStorage. Confirmed
            // against the 3.2 assembly: TEFeatureStorage is the only implementor of
            // ITileEntityLootable, so "te as TileEntityComposite" could never succeed
            // and this always fell through to the manual clear below. The fallback
            // masked it, but SetEmpty() never ran. Found in the 7 Sep play log:
            //   [GyroRepair] TEFeatureStorage cast failed - clearing items directly.
            TEFeatureStorage storage = te as TEFeatureStorage;
            if (storage != null)
            {
                storage.SetEmpty();
            }
            else
            {
                Debug.LogWarning("[GyroRepair] TEFeatureStorage cast failed — clearing items directly.");
                if (te.items != null)
                    for (int i = 0; i < te.items.Length; i++)
                        te.items[i] = ItemStack.Empty.Clone();
            }

            // Remove the block
            // 3.2: SetBlockRPC no longer takes a cluster index and a Vector3i.
            // Position is carried by a BlockValueRef.
            world.SetBlockRPC(new BlockValueRef(blockPos), BlockValue.Air);

            // Spawn the gyrocopter entity at the block position
            Vector3 spawnPos = new Vector3(blockPos.x + 0.5f, blockPos.y + 0.5f, blockPos.z + 0.5f);
            int gyroClassId = EntityClass.FromString("vehicleGyrocopter");
            if (gyroClassId == -1)
            {
                Debug.LogError("[GyroRepair] vehicleGyrocopter entity class not found!");
                return;
            }

            Entity gyroEntity = EntityFactory.CreateEntity(gyroClassId, spawnPos);
            world.SpawnEntityInWorld(gyroEntity);

            Debug.Log("[GyroRepair] Gyrocopter spawned at " + spawnPos);

            // Drain fuel after one frame — vehicle must be fully initialised first
            EntityVehicle gyroVehicle = gyroEntity as EntityVehicle;
            if (gyroVehicle != null)
                GameManager.Instance.StartCoroutine(DrainFuelNextFrame(gyroVehicle));

            // Notify the player
            if (player != null)
            {
                GameManager.ShowTooltip(player, "Gyrocopter repaired — ready to fly!", (string)null, null, null, false, true, 0f);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[GyroRepair] GyroRepairContainerPatch error: " + e.Message);
        }
    }

    static System.Collections.IEnumerator DrainFuelNextFrame(EntityVehicle vehicle)
    {
        yield return null;
        float fuelLevel = vehicle.vehicle.GetFuelLevel();
        vehicle.vehicle.AddFuel(-fuelLevel);
        Debug.Log("[GyroRepair] Fuel drained (was " + fuelLevel + ").");
    }
}
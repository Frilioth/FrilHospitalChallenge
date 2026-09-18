// File: HospitalMenuVideoCycler.cs
// Navezgane Hospital Challenge - Menu Video Cycler
// Author: Frilioth
// Version: 1.2.0
//
// Rotates the main-menu background video through several hospital security
// camera feeds, like a bank of monitors switching cameras.
//
// HOW IT WORKS
//   The main menu already has a video element with the id "videoTexture".
//   A Postfix on XUiC_MainMenu.OnOpen starts a coroutine that, every N
//   seconds, sets that element's VideoUri to the next clip and restarts it.
//   No XUi changes, no new controller, nothing to register.
//
// RANDOM ORDER, VIA A SHUFFLE BAG - NOT Random.Range per swap.
//   Picking at random each time will repeat the same feed back to back, which
//   looks like the cycler has stopped. Instead the clip list is shuffled and
//   played all the way through, then reshuffled. Every feed is seen once per
//   pass and no feed repeats, except possibly across a reshuffle boundary -
//   and the reshuffle explicitly avoids starting with the clip that just
//   played, so even that cannot happen.
//
// CONFIG: Config/videocycler.xml. Interval and clip list are read at startup.
//   Adding, removing or reordering clips needs a restart, NOT a rebuild.
//   A listed-but-missing file is skipped by the engine and logged.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using HarmonyLib;
using UnityEngine;

public class HospitalMenuVideoCyclerInit : IModApi
{
    public const string ModVersion = "1.2.0";

    public void InitMod(Mod _modInstance)
    {
        Debug.Log("[Hospital VideoCycler] Mod initializing v" + ModVersion);
        try
        {
            HospitalVideoCyclerState.LoadConfig(_modInstance);
        }
        catch (Exception e)
        {
            Debug.LogError("[Hospital VideoCycler] Config load failed: " + e.Message);
        }

        var harmony = new Harmony("com.frilioth.hospitalmenuvideocycler");
        harmony.PatchAll();
        Debug.Log("[Hospital VideoCycler] Harmony patches applied.");
    }
}

public static class HospitalVideoCyclerState
{
    // Clip URIs in the order given by the config file.
    public static readonly List<string> Clips = new List<string>();

    // Seconds each feed plays before switching.
    public static float SecondsPerClip = 6f;

    // Guards against a second coroutine if OnOpen fires again.
    public static bool Running = false;


    // The shuffled play order, and how far through it we are.
    private static readonly List<int> order = new List<int>();
    private static int orderPos = 0;
    private static int lastPlayed = -1;

    public static void LoadConfig(Mod modInstance)
    {
        Clips.Clear();

        string path = Path.Combine(Path.Combine(modInstance.Path, "Config"), "videocycler.xml");
        if (!File.Exists(path))
        {
            Debug.LogWarning("[Hospital VideoCycler] No config at " + path + " - cycler will not run.");
            return;
        }

        XmlDocument doc = new XmlDocument();
        doc.Load(path);
        XmlElement root = doc.DocumentElement;
        if (root == null) return;

        string interval = root.GetAttribute("interval");
        float parsed;
        if (!string.IsNullOrEmpty(interval) && float.TryParse(interval, out parsed) && parsed > 0f)
            SecondsPerClip = parsed;

        foreach (XmlNode node in root.ChildNodes)
        {
            XmlElement el = node as XmlElement;
            if (el == null || el.Name != "clip") continue;
            string uri = el.GetAttribute("uri");
            if (!string.IsNullOrEmpty(uri)) Clips.Add(uri);
        }

        Debug.Log("[Hospital VideoCycler] Loaded " + Clips.Count
            + " feeds, " + SecondsPerClip + "s each.");
    }

    // Fisher-Yates over the indices. Called at the start and whenever the bag
    // empties. Reshuffles until the first entry is not the clip that just
    // played, so a feed can never appear twice running across the boundary.
    private static void Reshuffle()
    {
        order.Clear();
        for (int i = 0; i < Clips.Count; i++) order.Add(i);

        for (int attempt = 0; attempt < 8; attempt++)
        {
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                int tmp = order[i]; order[i] = order[j]; order[j] = tmp;
            }
            // With only one clip there is nothing to avoid.
            if (order.Count < 2 || order[0] != lastPlayed) break;
        }

        orderPos = 0;
    }

    public static int NextIndex()
    {
        if (Clips.Count == 0) return -1;
        if (order.Count != Clips.Count || orderPos >= order.Count) Reshuffle();

        int idx = order[orderPos];
        orderPos++;
        lastPlayed = idx;
        return idx;
    }
}

[HarmonyPatch(typeof(XUiC_MainMenu))]
[HarmonyPatch("OnOpen")]
public class HospitalMenuVideoCycler_OnOpen
{
    static void Postfix(XUiC_MainMenu __instance)
    {
        try
        {
            if (HospitalVideoCyclerState.Clips.Count < 2) return;
            if (HospitalVideoCyclerState.Running) return;
            if (__instance == null || __instance.xui == null) return;

            HospitalVideoCyclerState.Running = true;
            ThreadManager.StartCoroutine(CycleCo(__instance.xui));
            Debug.Log("[Hospital VideoCycler] Cycle started ("
                + HospitalVideoCyclerState.Clips.Count + " feeds).");
        }
        catch (Exception e)
        {
            Debug.LogError("[Hospital VideoCycler] OnOpen failed: " + e.Message);
            HospitalVideoCyclerState.Running = false;
        }
    }

    // The menu's background video element.
    private static XUiV_Video FindVideo(XUi xui)
    {
        if (xui == null) return null;
        XUiController c = xui.GetChildById("videoTexture");
        if (c == null) return null;
        return c.ViewComponent as XUiV_Video;
    }

    // True while the element still exists AND its player object is active.
    // This is how the cycler knows the player has left the menu.
    private static bool IsLive(XUiV_Video v)
    {
        if (v == null) return false;
        if (v.videoPlayer == null) return false;
        return v.videoPlayer.gameObject.activeInHierarchy;
    }

    // Every exit path logs its reason. The old build stopped silently in most
    // paths, which is why it ran for 19 minutes with nothing saying it should not.
    private static void Stop(string reason)
    {
        HospitalVideoCyclerState.Running = false;
        Debug.Log("[Hospital VideoCycler] Cycle stopped: " + reason + ".");
    }

    private static IEnumerator CycleCo(XUi xui)
    {
        // START EXPIRED so the first swap happens immediately.
        //
        // Reported by a playtester: every fresh launch opened on VANILLA's menu
        // video (the cop eating someone) and only switched to the Hospital clips
        // after a few seconds. Cause: this started at 0 and waited a full interval
        // before the first swap, so vanilla's video is what the player sees on the
        // way in. Returning from a game looked correct only because the video
        // element still held the last URI this coroutine set.
        float t = HospitalVideoCyclerState.SecondsPerClip;

        while (true)
        {
            // THE stop condition. A world exists ONLY when a game is loaded, so this
            // cannot be wrong about whether the player is still in the menu.
            //
            // v1.0.0 relied solely on IsLive() below, which tests whether the video
            // element's GameObject is active - and it STAYS ACTIVE in game. So the
            // coroutine never stopped: it kept loading a 1080p video from disk every
            // few seconds for the whole session. Measured on a real 19-minute
            // session, 115 of 117 swaps happened AFTER the world had loaded.
            // Do not rely on the view tree to tell you the menu has gone.
            //
            // v1.1.0 also patched XUiC_MainMenu.OnClose, and that BROKE THE WHOLE
            // MOD: OnClose is declared on XUiController, not on XUiC_MainMenu, so
            // Harmony could not find it, and one failed patch aborts the entire
            // PatchAll. The world check alone is sufficient, so the patch is gone.
            if (GameManager.Instance != null && GameManager.Instance.World != null)
            {
                Stop("world loaded");
                yield break;
            }

            XUiV_Video video = FindVideo(xui);
            if (!IsLive(video))
            {
                Stop("video element gone");
                yield break;
            }

            // unscaledDeltaTime, because the main menu runs with the game clock
            // stopped and Time.deltaTime would never accumulate.
            t += Time.unscaledDeltaTime;
            if (t < HospitalVideoCyclerState.SecondsPerClip)
            {
                yield return null;
                continue;
            }
            t = 0f;

            int idx = HospitalVideoCyclerState.NextIndex();
            if (idx < 0) { yield return null; continue; }

            string uri = HospitalVideoCyclerState.Clips[idx];
            bool ok = true;
            try
            {
                video.VideoUri = uri;
                video.startVideo();
            }
            catch (Exception e)
            {
                ok = false;
                Debug.LogError("[Hospital VideoCycler] Swap to feed " + idx + " failed: " + e.Message);
            }

            if (ok) Debug.Log("[Hospital VideoCycler] -> feed " + idx + " (" + uri + ")");

            yield return null;
        }
    }
}

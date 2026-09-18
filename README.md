# Navezgane Hospital Challenge

### A 7 Days to Die challenge mod

**Author:** Frilioth · **Game version:** 3.2 · **Mod version:** 0.22.0

---

## What is this?

You wake up in Navezgane Hospital, infected with **Voss-7** — a mutated strain of the Septdiurnal Virus, resistant to honey and antibiotics. It is killing you. The only cure is at Trader Jen's clinic, and the only way to reach her is the wrecked gyrocopter on the hospital roof.

One life. Eight parts. About eight days before the virus finishes the job.

---

## Key features

- **Permadeath.** Die and the run is over.
- **A clock that never stops.** Voss-7 climbs from 5% at a flat rate and kills you at 100%. Suppressants push it back down; only The Cure ends it.
- **Eight gyro parts**, each from a different source, each pushing you into a different part of the building.
- **Six difficulty presets**, from learning the layout to not expected to be survivable.
- **A debrief page** written at the end of every run, with a verifiable completion code.
- **Eleven languages.**

---

## Installing

Copy each folder into your `Mods` directory.

**Required**

| Folder | |
|---|---|
| `Hospital` | the mod, including its five maps in `Worlds` |
| `0_HospitalItems` | items |
| `zzzz_FrilBlocks` | adds blocks needed for the challenge |
| `GyroRepairMod` | builds and launches the gyrocopter |
| `FrilHospitalESCGuide` | the in-game survival guide |

**Recommended**

`Frils-HUD` · `FrilRagdollFloorFix` · `FrilHospitalMenuVideoCycler` · `FrilHospitalRotateLoadingscreens`

Folder names are only what the download ships as. The game identifies a mod by the `Name` in its `ModInfo.xml`, so you can rename them.

This mod contains code, so **EAC must be off**.

### Single player only

**Do not run this on a dedicated server yet.** The challenge itself is per-player and works, but the rest of the mod assumes one player, so the host gets a working game and nobody else does. A co-op version is planned.

---

## Difficulty

| Preset | |
|---|---|
| Walk-In | ill, but not dying yet. For learning the hospital. |
| Under Observation | stable for now. Forgiving, still expects attention. |
| Critical | **the challenge as intended. Recommended.** |
| Code Blue | deteriorating. Faster infected, heavier hits. |
| Terminal | everything sprints. The roof is a long way up. |
| Flatline | not expected to be survivable. Prove otherwise. |

There is also an **Increasing Infection** toggle on the Basic tab. Leave it on for the real challenge. Turned off, the virus is disabled and your first suppressant ends it for the whole run — for players who want the hospital without the clock. Runs with it off are marked as such and are not ranked.

---

## Voss-7

Starts at **5%** and climbs about **1% every five minutes**, flat, nothing accelerates it. A zombie hit adds 0.2%. Untouched, that is roughly day 8.

| Infection | Effect |
|---|---|
| 25% | stamina regen −10% |
| 50% | movement speed −15% |
| 75% | stamina a further −15%, movement a further −5% |
| 90% | weapon handling −30% |
| 100% | death |

**The effects stack, and they lift again.** Each threshold costs you something only while you are above it — push the level back down and you get it back. From 75% the screen begins to sway; at 99% it turns properly bad and you have about a minute left.

### Suppressants

Crafted at a Chemistry Station. They push the infection **down**, never to zero, and the climb resumes when they run out.

| Tier | Cost | Reduces |
|---|---|---|
| Weak | 1 painkiller + 1 reagent | 1% |
| Standard | 2 painkillers + 2 reagents | 2.5% |
| Strong | 3 painkillers + 3 reagents | 6.25% |

**Voss-7 Reagent** is crafted from 1 acid + 1 blood bag + 10 rotting flesh, and **yields 3**. Acid is the bottleneck of the whole chain: it comes from medical containers, and from the bags dropped by cops, soldiers and mutated zombies.

---

## The eight parts

Install them into the wrecked gyro on the roof. The order is fixed.

| Stage | Part | Where |
|---|---|---|
| 1 | Radiator | wrench the hospital's air conditioning units, 25% each |
| 2 | Water | standing water in the basement |
| 3 | Engine | heavy loot bags, or craft at a workbench |
| 4 | Battery | vehicles, janitor trolleys, or craft at a chemistry station |
| 5 | Spark Plugs | carried by bikers |
| 6 | Control Cables | craft at a workbench from electrical parts and duct tape |
| 7 | Tail Assembly | carried by cop zombies |
| 8 | Rotor Blades | wrench air conditioning units, 10% each, or craft from forged steel |

Fuel is not a part. Fuel the gyro normally once it spawns.

### Two things that catch people out

**The engine is not in the cars.** Wrecked vehicles give engines in the base game. They do not here, no matter how long you wrench. It comes from a heavy loot bag or the workbench, and the recipe needs **forged steel** — so you need a forge and a crucible.

**You cannot build a workbench or a chemistry station.** Several of each are fixed around the hospital; find them and learn where the nearest are. **The forge is the exception** — you can and should build one.

---

## Starting kit

Every run starts the same: Hospital Survival Guide, Stone Axe, 2× First Aid Bandage, 2× Boiled Water, 2× Chilli, 1× Strong Suppressant, Hunting Knife.

The starting-weapon choice from earlier versions has been removed.

---

## The survival guide

Press **ESC** in game for a four-tab guide: what the virus is and how it behaves, what each stage costs you, where all eight parts come from, and practical advice on staying alive. Translated into every supported language.

---

## If you escape

The run writes a full debrief to `stats/Hospital_Debrief.html` inside the mod folder — the in-game message tells you the exact path. It has your timeline, kills, suppressant use, peak infection, and when you found each part.

It also contains a **leaderboard code** beginning `HC-1-`, which encodes your run so it can be verified. You can print it at any time with the console command `hospitalcode`.

*The leaderboard is not live yet. Hold on to your code until it is.*

---

## Languages

English, French, German, Spanish, Italian, Polish, Brazilian Portuguese, Russian, Turkish, Japanese and Simplified Chinese.

Everything the mod adds is translated. Place and character names are left in English in the Latin-script languages on purpose; Japanese and Chinese follow the game's own conventions for them.

Corrections are welcome — several of these have been checked by native speakers and several have not.

---

## Known issues

- Multiplayer is not supported. See above.
- The vanilla **Infection Chance** and **Infection Rate** sandbox options do nothing here. Voss-7 replaces the vanilla infection entirely, but they still appear in the options list because they are vanilla settings.
- Loot respawns every 5 days, so the hospital feels picked clean before then. That is intended.
- Rain is disabled in all biomes, which was a performance decision rather than a design one.

If you hit something else, a copy of your `Player.log` is far more useful than a description.

---

## Credits

Developed by **Frilioth** — [twitch.tv/Frilioth](https://twitch.tv/Frilioth)

Thanks to the playtesters who died a great many times so this could be balanced, and to **AuroraGiggleFairy**, whose AGF-ESCWindow showed how to hang a window off the ESC menu without a line of code.

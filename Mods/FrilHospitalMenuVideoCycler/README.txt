NAVEZGANE HOSPITAL MENU VIDEO CYCLER  v1.2.0
============================================

Rotates the main-menu background video through several hospital
"security camera" feeds, like a bank of monitors switching cameras.

Feeds play in RANDOM order. The list is shuffled and played all the
way through before reshuffling, so every feed is seen once per pass
and none repeats back to back.

CONFIG - Config/videocycler.xml
  interval = seconds per feed before switching (currently 6).
  <clip>   = one feed. uri is a mod path with NO extension
             (the game appends .mp4 on Windows).

ADDING / CHANGING CAMERAS (no rebuild needed)
  1. Make the clip: MP4, H.264, 1920x1080, 30fps, MUTED.
     It should loop cleanly, since a feed can be cut off mid-play
     when the interval expires.
  2. Put it in this mod's Video/ folder, e.g. hospital_menu_cam9.mp4
  3. Add a line to Config/videocycler.xml:
        <clip uri="@modfolder(FrilHospitalMenuVideoCycler):Video/hospital_menu_cam9"/>
  4. Restart.

  Names are up to you; the uri just has to match the real filename
  minus the extension. A listed-but-missing file is skipped and
  logged, and the cycle carries on.

  Order in the file does not matter - it is randomised at runtime.

  Fewer than 2 clips and the cycler does not start, since there is
  nothing to cycle between. The menu keeps whatever video it had.

REQUIREMENTS
  DLL mod - needs EAC OFF, same as the other Hospital DLL mods.
  The clips live in this mod's own Video/ folder, so the whole
  cycler is self-contained: one folder to install, one to update.

WHAT TO LOOK FOR IN THE LOG
  [Hospital VideoCycler] Loaded 8 feeds, 6s each.
  [Hospital VideoCycler] Cycle started (8 feeds).
  [Hospital VideoCycler] -> feed 3 (@modfolder...)
  [Hospital VideoCycler] Menu no longer active - cycler stopped.

CHANGELOG
  1.2.0 - the first clip now plays immediately. Every fresh launch
          used to open on the VANILLA menu video for a few seconds
          before switching, because the timer started at zero and
          waited a full interval before the first swap.
  1.1.1 - v1.1.0 DID NOT LOAD AT ALL. It patched
          XUiC_MainMenu.OnClose, but OnClose is declared on
          XUiController, not on XUiC_MainMenu, so Harmony could
          not find it - and one failed patch aborts the whole
          PatchAll, taking the entire mod with it.
          That patch is removed. The world check alone is enough:
          a world exists ONLY when a game is loaded.
          If you ran 1.1.0, the log said "Failed initializing
          ModAPI instance" and there were no videos at all.
  1.1.0 - CRITICAL FIX. The cycler did not stop when you entered a
          game. It kept loading a 1080p video from disk every few
          seconds for the whole session - measured at 115 of 117
          swaps AFTER the world had loaded on a 19 minute session.
          That is enough to freeze the game.
          Cause: the only stop condition tested whether the video
          element's GameObject was still active, and it stays
          active in game. There are now THREE independent stop
          conditions - main menu closed, a world exists, or the
          video element is gone - and every exit logs its reason.
          If you see "Cycle stopped:" in the log it is working.
  1.0.0 - first release. Config-driven clip list and interval,
          random order via a shuffle bag.

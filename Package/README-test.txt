Ore to Auto Gps - internal test build v1.1.0
=============================================

This archive is password-protected (AES-256). Extract it with 7-Zip or
WinRAR using the password you received separately - Windows Explorer
cannot open AES-encrypted zips.

A plugin that turns ore your ORE DETECTOR has already detected into GPS
waypoints - but ONLY when you press the mark key (default Alt+K). It never
scans for or reveals ore the detector did not find; the background voxel
read only measures the size of already-detected deposits.

This build adds (vs. the previous test round):
  - default mark key Alt+K (rebindable in plugin settings),
  - a rolling memory between keypresses (only the N most recent
    detections are remembered, default 5; 0 = remember everything),
  - yield figures computed from a BASELINE yield per ore (all ice is
    sized as regular ice, all stone as regular stone) - the detector
    cannot tell dense/snow variants apart, so the numbers must not either.

1. Install
----------
1. Make sure Space Engineers is CLOSED.
2. Run Install-TestPlugin.bat.
   - It finds Pulsar by itself (%AppData%\Pulsar).
   - If Pulsar is elsewhere, run it from a command line:
       Install-TestPlugin.bat "C:\path\to\Pulsar"
3. Start the game through Pulsar as usual.
4. To remove the plugin later, run Uninstall-TestPlugin.bat.

IMPORTANT: if you still have the old fully-automatic build (or any other
ore-marker plugin) installed, disable it first - you would get doubled
markers and the tests below would be confusing.

2. How to test
--------------
Enter any world with an ore detector (ship or hand). The mark key is
Alt+K by default; the plugin settings are in Pulsar's plugin list
(Player interaction section: "Mark detected ore" + "Remembered detections").

a) The gate - markers ONLY on keypress:
   Fly over ore deposits and do NOT press the key. Watch the GPS list:
   nothing may appear, no matter how long you wait or how much ore the
   detector beeps at. Then press Alt+K: markers appear, and the HUD
   shows "Ore to Auto Gps: N new, M updated marker(s).".

b) Chat safety:
   Open chat (or any text field), press Alt+K there: NOTHING may happen
   (no markers, no toast spam). The key only works in normal gameplay.

c) Rolling memory (the new setting):
   In the plugin settings set "Remembered detections" to 2. Fly past
   several DIFFERENT deposits one after another without pressing, then
   press Alt+K: only roughly the last 2 detections should get markers -
   the older ones were forgotten (a HUD toast still reports the count).
   Then set it to 0, fly past several again and press: everything found
   since the last press gets marked (0 = remember everything).

d) Incremental presses:
   Detect some ore, press Alt+K (markers appear). Detect MORE ore, press
   again: only the new stuff is added/updated - no duplicates of the
   first batch.

e) Baseline yields (new sizing rule):
   Find ICE - ideally both a snow-covered area and a regular ice lake.
   Press Alt+K and compare the kg figures in the GPS descriptions:
   deposits of similar visual size must show similar kg even if one is
   snow and the other is ice (all sized as regular ice, ratio 5).
   Same for stone/soil areas on planets (all sized as regular stone).

f) Old features still working:
   - Size bands in the name (Trace/Small/Medium/Large/Huge) and
     "~X kg ore -> ~Y kg ingots @100%" in the description.
   - Field markers: scattered tiny deposits (uranium boulders on the
     Moon are perfect) share one "Uranium xN" marker.
   - Per-ore toggles, colors, "Clear all" button (Maintenance section)
     removes every marker created this session.

3. Settings (defaults after install)
-----------------------------------
Mark key: Alt+K. Remembered detections: 5. Dedup radius: 100 m.
Show quantity: on. Show on HUD: on. Minor threshold: 1000 m^3.
Field radius: 500 m. All ores on except Stone. Modded ores: on.

4. If something does not work
-----------------------------
Send back the newest file:
    %AppData%\SpaceEngineers\SpaceEngineers.log
Everything the plugin logs starts with [OreToAutoGps]. Also note what
you did (which test letter above) and what happened instead.

Thank you for testing!

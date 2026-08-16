Ore to Auto Gps - internal test build v1.2.0
=============================================

This archive is password-protected (AES-256). Extract it with 7-Zip or
WinRAR using the password you received separately - Windows Explorer
cannot open AES-encrypted zips.

A plugin that turns ore your ORE DETECTOR has already detected into GPS
waypoints - but ONLY when you press the mark key (default Alt+K). It never
scans for or reveals ore the detector did not find; the background voxel
read only measures the size of already-detected deposits.

This build adds (vs. the previous test round):
  - the mark key now ships bound to Alt+K (rebindable in plugin settings),
  - a marker cap per press: at most N NEW markers per keypress, the most
    recent deposits (default 5, slider 0-5; 0 = no limit). A wide deposit
    or a whole field of small deposits counts as ONE marker; updating
    existing markers is never limited,
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

c) Marker cap (the new setting):
   In the plugin settings set "Max markers per press" to 2. Fly past
   several DIFFERENT deposits one after another without pressing, then
   press Alt+K: AT MOST 2 new markers appear - the 2 most recent
   deposits; the older ones were forgotten (the HUD toast reports how
   many were actually added). Then set it to 0, find more ore and press
   again: every deposit found since the last press gets marked
   (0 = no limit).
   Also check the cap counts MARKERS, not detections: fly slowly along
   one LARGE deposit (the detector beeps/queues many times along it)
   with the cap at 1, then press: you get ONE marker for that deposit,
   not a flood.

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
Mark key: Alt+K. Max markers per press: 5. Dedup radius: 100 m.
Show quantity: on. Show on HUD: on. Minor threshold: 1000 m^3.
Field radius: 500 m. All ores on except Stone. Modded ores: on.

4. If something does not work
-----------------------------
Send back the newest file:
    %AppData%\SpaceEngineers\SpaceEngineers.log
Everything the plugin logs starts with [OreToAutoGps]. Also note what
you did (which test letter above) and what happened instead.

Thank you for testing!

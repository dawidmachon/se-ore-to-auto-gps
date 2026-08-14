# Ore to Auto Gps

A Space Engineers 1 **client plugin** for [Pulsar](https://github.com/SpaceGT/Pulsar) that
**automatically marks ore your ore detector detects** as GPS waypoints — with an approximate
deposit size measured in the background. No keybind, no effort: fly around with an ore detector
and ore deposits get pinned on your GPS list as you find them.

## It's legit

This plugin **only ever marks ore your ore detector has already detected.** It never scans for
or reveals ore you have not found. The only "extra" it does is *measure* the size of those
already-detected deposits (using a background voxel read) so each waypoint can show an
approximate size and a proper center. Anything the detector did not find is discarded.

## Features

- **Automatic** — waypoints appear as your detector scans; works while remote-controlling drones
  (the drone's detector does the detecting).
- **Correct sizing & centering** — each deposit is measured at its own world position, so it works
  at any ship speed. One deposit = one waypoint at its mass-weighted center, with the true total
  size — no matter how wide the deposit is.
- **Size bands** in the GPS name: `Trace / Small / Medium / Large / Huge` (Huge ≥ 5,000,000 kg of ore),
  and the description shows the potential yield in kilograms: `~6.5k kg ore -> ~4.5k kg ingots @100%`
  (ingot potential assumes a default refinery with no yield upgrades).
- **Field markers** — scattered tiny deposits (e.g. uranium boulders) within a radius share one
  marker (`Uranium ×N`) so the GPS list stays clean.
- **Per-ore toggles**, a distinct color per ore, and smart de-duplication across scans.
- **Performant** — sizing runs on a background thread; only newly detected ore is measured, one
  area at a time.
- Works on **dedicated servers** (client-side only).

## Requirements

- [Space Engineers](https://store.steampowered.com/app/244850/Space_Engineers/) (v1)
- [Pulsar](https://github.com/SpaceGT/Pulsar) plugin loader

## Install

### From Pulsar's plugin browser (once published)
Search for **"Ore to Auto Gps"** in Pulsar's plugin list and enable it. *(Not published yet.)*

### Manual install (for testing / sharing a build)
1. Take the two files for your Pulsar edition:
   - **Legacy** (`.NET Framework 4.8`, the common one): `OreToAutoGps.dll` + `OreToAutoGps.dll.xml` from the `net48` build.
   - **Interim** (`.NET 10`): the same two files from the `net10.0` build.
2. Copy them into `%AppData%\Pulsar\Legacy\Local\` (or `...\Pulsar\Interim\Local\` for Interim).
3. Start the game via Pulsar and enable **Ore to Auto Gps** in the plugin list (save it to a profile).

## Configuration

Open the plugin's config dialog from Pulsar's plugin list. Options include marker spacing, size
toggle, HUD visibility, coordinates in the name, small-deposit clustering (clutter control), and
per-ore toggles (Iron, Nickel, Cobalt, Magnesium, Silicon, Silver, Gold, Platinum, Uranium, Ice,
Stone). A **Clear all** button removes every GPS created this session.

## Build from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (also builds
`net48` for the Legacy edition on Windows).

```cmd
dotnet build OreToAutoGps.sln
```

The build auto-detects your Space Engineers `Bin64` folder from Steam. If that fails, copy
`Directory.Build.props.template` to `Directory.Build.props` and set `<Bin64>` to your
`SpaceEngineers\Bin64` path. The built DLL (+ descriptor) auto-deploys to Pulsar's `Local` folder.

## How it works

A Harmony postfix on the game's internal ore-detector callback captures only what your detector
detects. Each newly detected position is queued and measured by a small background voxel read at
that position; results are filtered to the detected ore, so undetected ore is never marked.

## Reporting bugs

Open a [GitHub issue](https://github.com/dawidmachon/se-ore-to-auto-gps/issues). Include the
Pulsar loader log at `%AppData%\Pulsar\Legacy\info.log` and steps to reproduce.

## License

MIT — see [LICENSE](LICENSE). Space Engineers is a trademark of Keen Software House s.r.o.

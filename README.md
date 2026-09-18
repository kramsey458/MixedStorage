# MixedStorage 0.5.1

Percentage allocations for Timberborn 1.1.2.4 warehouses and piles.

## Downloads

**[Download v0.5.1 visual prototype from Releases](https://github.com/kramsey458/MixedStorage/releases/tag/v0.5.1).**

**MixedStorage-v0.5.1.zip** is the only package for single-player and multiplayer. BeaverBuddies support is bundled and activates automatically when BeaverBuddies is enabled.

Download this compiled ZIP rather than GitHub's automatically generated source-code archives.

Supported buildings:
- Both factions: small, medium and large warehouses.
- Folktails: small, large and underground piles.
- Iron Teeth: small and large industrial piles.

Each building lists only its own inventory's accepted goods. Piles retain the game's Pileable category; warehouses retain their warehouse goods. Tanks and map-editor reserve storage are not changed.

Select a building, set percentages and press Apply at exactly 100%. Values allow two decimal places. A row's × button resets its draft percentage to zero. Search and the allocated-only filter help navigate the list. Gameplay shortcuts are blocked while a text field in the allocation editor has focus.

Whole-item limits use largest-remainder rounding, with stable good-ID ordering for ties. Very small allocations can round to zero, especially in small piles. The UI previews the final item limits. Incoming deliveries count toward limits; conflicting reductions are rejected until deliveries finish. Existing excess goods are preserved and can be hauled out. Accept, Obtain, Supply and Empty use the native inventory/hauling system, with mixed-good support for Obtain and Supply.

Mixed 3D contents use allocation-sized sections of native good meshes, filled from actual per-good inventory. Empty allocations leave their section empty. Banners still show a representative good; the panel gives exact counts. This rendering prototype has not been tested in game. Allocation boundaries select complete native mesh cells by center, so visual proportions round to whole cells and very small shares may have no visible cell. Continuous bulk surfaces and unrecognized mesh layouts fit their whole mesh into the section, which can narrow their appearance. Excess stock is visually capped at the allocated section; goods with no allocated section remain listed in the panel. Unsupported or unreadable native meshes fall back to the original visualizer.

## Install / upgrade

Extract MixedStorage into Documents/Timberborn/Mods, replacing the existing MixedStorage folder's files. Enable it and Harmony, then restart Timberborn. Uses MixedStorage mod IDs and allocation save keys.

When upgrading from 0.4.x or earlier, remove or disable the old MixedStorage-BeaverBuddies addon before restarting. Keeping it enabled causes a clear startup error to prevent conflicting integrations. Existing MixedStorage allocations are preserved.

For multiplayer, enable BeaverBuddies separately and install the same MixedStorage version on every computer. No separate MixedStorage addon is needed. The bundled bridge sends allocation commands through BeaverBuddies replay events. Start a fresh session after upgrading; old replay recordings may refer to the former addon assembly.

## Validation

Compiled against Timberborn 1.1.2.4 assemblies. 10,043 allocation assertions cover capacities 20, 30, 180, 200, 1000 and 1200, invalid percentages, persistence, delivery guards, and 2,000 randomized splits with up to 100 goods. All 11 supported template names and storage categories were checked against the game's Blueprints.zip. Native rendering API signature checks guard against the ambiguous method lookup that broke v0.4.0. The new mesh clipper passes 15,689 offline assertions, including 1,000 randomized section partitions. Earlier versions were used successfully in game; v0.5.1 mixed visuals have not been tested in game or multiplayer. Appearance, lifecycle behavior and performance still need live verification.

## Build

Use .NET SDK 8 and the build.ps1 script below. It builds the base API, compiles the bridge, then rebuilds the main DLL with the bridge embedded. A direct bootstrap build is not a distributable release. Run the allocation checks with `dotnet run --project tests/AllocationTests.csproj -c Release`. Game and third-party DLLs are referenced locally, not distributed in these packages.

On Windows, `build.ps1` builds, tests and packages the unified mod:

```powershell
.\build.ps1 -GameDir 'C:\Games\Timberborn' -HarmonyPath 'C:\Mods\Harmony\0Harmony.dll' -BeaverBuddiesPath 'C:\Mods\BeaverBuddies\version-1.1\BeaverBuddies.dll'
```

The package is written to `dist/`. Use dependency paths from your own installation. The bundled multiplayer integration was developed against BeaverBuddies Stability Preview for Timberborn 1.1.

## Quick allocation controls

Each good has a Max button: set it to 100% and all other goods to zero, then Apply. Copy allocations copies a valid draft; select another storage building, Paste allocations, then Apply. Percentages remain exact and limits scale to the destination capacity. Incompatible goods reject the entire paste without changing the draft. The clipboard is local to the game process; Apply synchronizes through BeaverBuddies. Hauling mode and hauler priority are not copied.

The top contents summary shows each allocated or stocked good's applied percentage and stored quantity / limit, plus incoming deliveries and excess stock. It updates live and is independent of search, filters and unapplied draft edits.

## Optional DLL loading

Timberborn 1.1.2.4 loads all enabled mods' DLLs recursively before starting mods, then enumerates their types. The main DLL has no BeaverBuddies assembly reference. The bridge is an embedded resource, so it is loaded only when the BeaverBuddies assembly is present. Loading checks run in separate processes with and without BeaverBuddies, including eager main-type enumeration, bridge event discovery, delegate installation, repeated initialization and legacy-addon rejection. These are offline checks, not a live multiplayer session test.

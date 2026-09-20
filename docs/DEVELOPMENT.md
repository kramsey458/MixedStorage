# Developer notes

## Build

Use .NET SDK 8 and local Timberborn 1.1.2.4, Harmony, and BeaverBuddies dependencies. From the repository root:

```powershell
.\build.ps1 -GameDir 'C:\Games\Timberborn' -HarmonyPath 'C:\Mods\Harmony\0Harmony.dll' -BeaverBuddiesPath 'C:\Mods\BeaverBuddies\version-1.1\BeaverBuddies.dll'
```

Replace example paths with your installation paths. BeaverBuddies is required to build the bridge, although optional for players. The script builds the base API, compiles the bridge, then rebuilds the main DLL with the bridge embedded. A bootstrap build alone is not a distributable release. Checks run before the unified ZIP is written to `dist/`. Game and third-party DLLs are referenced locally, not distributed.

Run allocation checks separately with `dotnet run --project tests/AllocationTests.csproj -c Release`. Use `build.ps1` for the full dependency-aware checks.

## Allocation and persistence

Percentages use integer units totaling 10,000. Whole-item limits use largest-remainder rounding with ordinal good-ID ordering for ties. Incoming reservations guard against conflicting limit reductions; existing excess stock is preserved. Copy/paste transfers percentages atomically into a draft and rejects incompatible goods. Hauling mode and priority are not copied.

Persistence uses the MixedStorage mod ID and `MixedStorage.Allocation` save key. Applying settings uses the multiplayer command path when available.

## Optional DLL loading

Inspection of Timberborn 1.1.2.4's ModCodeStarter showed recursive DLL loading for all enabled mods before startup, followed by Assembly.GetTypes() to discover starters. A loose bridge DLL referencing BeaverBuddies would expose missing dependencies when BeaverBuddies is absent.

The main DLL has no BeaverBuddies assembly reference. The bridge is an embedded resource named `MixedStorage.OptionalMultiplayer`, loaded only when BeaverBuddies is present. A scoped assembly resolver finds the already-loaded main mod, bridge, and BeaverBuddies assemblies. Initialization is idempotent. The obsolete separate addon is rejected to prevent conflicting integrations.

The bridge sends allocation changes through BeaverBuddies replay events. Its assembly identity differs from the former addon, so historical replay recordings may require the old mod versions. Allocation save keys remain unchanged.

## Rendering and UI

Native goods meshes and materials are reused. Complete primary/secondary mesh cells are selected by center after validating index topology. Continuous or unrecognized layouts are fitted whole into their sections. Visual proportions are approximate, banners remain single-good, and the UI provides exact quantities. Unsupported or unreadable meshes fall back to native rendering.

Updates are coalesced to at most five rebuilds per building per second. Generated meshes/materials have lifecycle cleanup. Rendering does not intentionally consume simulation randomness or add multiplayer commands.

The editor has one scrolling body and a fixed footer containing Copy/Paste allocations, the allocation total, and Clear/Revert/Apply. The shared native `EntityPanel` targets 440 UI units, bounded by the viewport, so every fragment stretches to the same width. Its original inline width is restored when the storage view closes, detaches, or switches to an unsupported selection. Height is based on available space below the allocation editor's current position, less the height of any visible fragments the game stacks beneath it in the same column (for example the Construction site panel while a building is unfinished), so those stay on screen.

The mod reuses the installed game's styles and assets: `NineSliceVisualElement` panels, `bg-sub-box--green` and `bg-sub-box--blue` frames, `button-game` buttons from the native DebugButton template, native TextFields cloned from the InputBox template, and the game's checkbox/scrollbar styles. Game art is not bundled in the mod. Existing summary sizes and allocation controls are retained. Appearance at different resolutions and UI scales still needs verification.

## Validation

- 10,043 allocation assertions: 2,000 randomized splits, lists up to 100 goods, capacities 20/30/180/200/1000/1200, validation, rounding, persistence, and delivery guards.
- All 11 supported template names and storage categories checked against the game's Blueprints.zip.
- 15,689 legacy clipping assertions and 201 whole-cell partition cases covering boundary ownership, complete topology, and unknown-topology fallback.
- Nine native rendering API signatures checked against installed game assemblies; the old ambiguous Initialize lookup is reproduced as a regression check.
- Isolated loading processes with and without BeaverBuddies check eager main-type enumeration, bridge event discovery, delegate installation, repeated initialization, and obsolete-addon rejection.

Loading checks run under .NET, not inside Unity/Mono. They do not establish live multiplayer compatibility. Builds pass without compiler warnings; v0.5.6's layout has been visually verified in game.

## Website

The project website (features, install guide, troubleshooting, FAQ) is plain static HTML, CSS and JavaScript in `site/`. There is no build step; edit the files directly. GitHub Pages serves the `gh-pages` branch, so after committing changes to `site/`, publish them with `.\deploy-site.ps1` (add `-DryRun` to preview what would change). The download buttons ask GitHub's public releases API for the newest release and fall back to the releases page if that request fails. `site/assets/split.js` mirrors `AllocationPlan.Capacities`, and `site/assets/demo.js` mirrors the panel behavior and messages in `StorageView`, for the interactive demo, so update them if the rounding rules or messages ever change. The demo's styling is in `site/assets/game-panel.css`. It uses self-hosted Noto Sans (SIL Open Font License, included in `site/assets/fonts/`). `site/assets/goods.js` lists every good a Folktails warehouse accepts: the `Box`-type goods in the game's Common and Folktails good collections (27, matching the in-game list), with ids and names from the game's `Blueprints.zip` and English localization. The warehouse capacities (30, 200, 1,200) and descriptions come from the same blueprints. The icons in `site/assets/goods/` are the game's own goods textures, color-adjusted to match how the game draws them and exported at 40 and 60 px. Regenerate them if the game adds or changes goods.

# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

Timberborn players who run out of warehouse and pile slots, or are tired of one-good-per-building storage, mostly in
single player; some play co-op through the BeaverBuddies Stability Fork. Mostly non-technical: they find the mod
through the org site, a friend or a forum post, want to know in one look what it does, install it without breaking
their game, and set up their first mixed building. Returning players come back to update or to troubleshoot.

## Product Purpose

The website for **MixedStorage** (https://github.com/timbermods/MixedStorage), a Timberborn mod that lets one
warehouse or pile hold several kinds of goods, each with a share of the building's capacity set by percentage (for
example 50% carrots, 50% gears in a 1,200-capacity warehouse: 600 each).

Success, in order:
1. The visitor understands the mechanism (percentages reserve space; they don't create goods or fill the building)
   and downloads the right file.
2. They install it correctly (the MixedStorage folder in `Documents\Timberborn\Mods`, Harmony enabled) and set up
   their first mixed building (total exactly 100%, then Apply).
3. When something's wrong, they find the fix or send a useful report (Player.log, versions, other mods).

## Positioning

The game allows one good per storage building. MixedStorage divides a building's capacity good by good, inside the
building's own panel, drawn with the game's own UI pieces, and shows the mix in the world with the game's own goods
models. One download covers single player and co-op.

## Operating Context

- Current release: **v1.2.0** (a stable Latest release; `release.js` fills the version and zip link). Built for
  Timberborn **1.1.2.4**. Requires **Harmony 2.4.1** or newer. Optional co-op through the **BeaverBuddies Stability
  Fork** (support is bundled; every player needs the same MixedStorage and game version).
- Supported: Folktails small/medium/large warehouses and small/large/underground piles; Iron Teeth small/medium/large
  warehouses and small/large industrial piles. Not tanks or map-editor reserve storage.
- The panel: Storage Allocation in the building's window; summary cards; Search; Allocated goods only; Max; x
  (reset); Clear all; Revert; Copy / Paste allocations; Apply 100% (Apply: store nothing at 0%). Edits are drafts
  until Apply; Apply gets an orange ring while the draft differs. The game's Copy settings tool copies allocations.
- Reporting: GitHub issues with Player.log (`%USERPROFILE%\AppData\LocalLow\Mechanistry\Timberborn`), versions, other
  mods, multiplayer or not.

## Capabilities and Constraints

- **Stack and hosting:** plain static HTML/CSS/JS in `site/` on `main`, no build step. GitHub Pages serves the
  `gh-pages` branch; publish with `.\deploy-site.ps1` after the change is merged to `main`.
- **Contracts the site test enforces** (`node tests/test-site.mjs`, run in CI): the home and install pages keep a
  download button (`a.btn` whose text says Download) with `data-release-href="download"` and a fallback link to
  `/releases/latest`; elements marked `hidden` stay hidden under the stylesheets; `404.html` loads assets by absolute
  `/MixedStorage/` paths; every page loads `assets/release.js` with the same `data-repo` and `data-asset`.
  `assets/release.js` is a byte-for-byte copy shared across timbermods sites: replace, never edit.
- `assets/split.js`, `goods.js` and `demo.js` mirror the mod's rounding, goods list and panel behavior for the
  interactive demo; keep them working and in step with the mod.
- Terminology as in game and README: Storage Allocation, Apply 100%, Apply: store nothing, Copy allocations, Paste
  allocations, Clear all, Revert, Max, Allocated goods only, excess, Accept / Obtain / Supply / Empty.
- **Honest status:** the v0.5.8 panel layout and v0.5.7 storage visuals were verified in game; later changes (Copy
  settings, error messages, Supply order, store nothing, the Apply ring, and more) haven't been played in game yet.
  Say so plainly. Describe the mod as it is now; version history belongs in the changelog. Keep the upgrade facts
  players need (replace the files with the game closed; allocations are kept; remove the old MixedStorage-BeaverBuddies
  addon if present).

## Brand Commitments

- Voice: a fellow player explaining a handy tool. Clear, exact, friendly, never hype.
- Native feel is the product's identity: the mod's own panel is drawn with the game's frames, buttons and fields, and
  the site's replica does the same.
- No official Timberborn logos or key art. The game's goods icons are allowed (they're already used, credited as
  Timberborn's).
- License: MIT for the project's own code, docs and site; goods icons belong to Mechanistry; Noto Sans under OFL.
- Unofficial community mod, not affiliated with or endorsed by Mechanistry. Maintained by Timbermods.

## Evidence on Hand

- Real in-game screenshots: `site/assets/panel.webp` (the Storage Allocation panel on a large warehouse),
  `site/assets/world.webp` (a warehouse showing several goods), `site/assets/og.png`.
- The interactive replica of the panel (`demo.js`, `game-panel.css`) with all 27 goods a Folktails warehouse accepts.
- Player reports of successful use of earlier builds (no quotes on hand; don't invent any).

## Product Principles

1. **Show the mechanism.** Percentages reserve space; the demo and the numbers prove it.
2. **The game's own look is the proof of fit.** The panel belongs in the building's window; the site shows that.
3. **Install right the first time.** Folder, Harmony, restart: impossible to miss.
4. **Exact, not approximate.** Counts, rules and limits stated precisely, including what rounds and what doesn't.
5. **Honest about what's been played.**

# CLAUDE.md

MixedStorage (timbermods/MixedStorage) is a Timberborn mod: one warehouse or pile holds several goods, each with a share
of capacity set by percentage. C# in `source/` (mod), `multiplayer/` (bundled BeaverBuddies Stability Fork bridge),
`tests/` (allocation tests, site test, version check), `loading-tests/`; the website is `docs/`. Changes land on `main`
through a PR → merge.

- CI (`.github/workflows/tests.yml`, every PR) runs, and you can run locally without the game:
  `dotnet run --project tests/AllocationTests.csproj -c Release` (prints `PASS: 19,138 assertions…`),
  `node tests/test-site.mjs` (76/76), and `powershell -NoProfile -Command ".\tests\check-version.ps1"` (prints `1.2.0`).
- The full mod build needs the game and DLLs: `.\build.ps1 -HarmonyPath <0Harmony.dll> -BeaverBuddiesPath <BeaverBuddies.dll>`
  (see `DEVELOPMENT.md`). The version lives only in `Directory.Build.props` plus
  `packaging/MixedStorage/version-1.1/manifest.json`; a release bump also touches README, CHANGELOG.md and the site's
  status note (`docs/index.html#status`).

## Standing rules

- Never launch or drive Timberborn, and never touch installed mods or saves. The maintainer (Kyler) playtests himself.
- Commit on a branch and open a PR. Kyler has said to merge PRs automatically: merge, then check the page live.
- Upgrade facts players need stay (replace files with the game closed; allocations are kept; remove the old
  MixedStorage-BeaverBuddies add-on). Other version history goes only in CHANGELOG.md and release notes.

## Writing README and website text

Kyler, 2026-09-24: "simplicity and elegance is effective and desirable." Every change to the README, the website
text and the player docs follows these rules.

- **Write for a Timberborn player** who wants to download, install and use the mod. Developer detail goes in
  `DEVELOPMENT.md` or CHANGELOG.md; link to it rather than repeating it.
- **Short.** One idea per sentence, most under about 20 words. A paragraph or FAQ answer is one to three sentences,
  a troubleshooting answer a few numbered steps.
- **Lead with the action.** Menu paths as arrow chains; on-screen labels in bold, exactly as in game.
- **Say each thing once**, where a player would look for it; link to it elsewhere.
- **Plain words.** No internals (class names, ids, formats) unless the player needs them to act.
- **Cut** filler, repeated caveats, edge cases a player won't meet, and history ("since …", "no longer", older
  builds). Describe the mod as it is now.
- **Check every fact against the code** before writing it; changelogs lag.
- **Keep, briefly:** credits, the unofficial line, the status, and safety facts.
- **Reread as a new player before publishing.** Every step works as written, and nothing is said twice.

## Website

- **Where:** `docs/`: `index.html` (features, hero panel, demo), `install.html`, `troubleshooting.html`, `faq.html`,
  `404.html`, plus `robots.txt`, `sitemap.xml`, `.nojekyll`. Live at https://timbermods.github.io/MixedStorage/.
- **Published:** GitHub Pages serves `main:/docs`, like every other timbermods site, so merging to main publishes;
  a build takes about a minute. (The old `gh-pages` branch and `deploy-site.ps1` are retired.)
- **Latest releases update themselves:** when a release becomes GitHub's Latest, `.github/workflows/latest-release.yml`
  (the shared timbermods workflow) appends the standard footer to its notes, sets the site's
  `data-release="version|tag|asset-name"` fallback text and the README lines ending in `<!-- latest -->` to the new
  version, runs the site checks and commits to main. Pre-releases change nothing. Descriptions, status lists and FAQs
  stay manual (the checklist below). Dry run: Actions → Latest release → Run workflow.
- **Look:** "The Apothecary Drawer Cabinet". The site is a walnut cabinet on a painted wall, with brass plates, kraft
  cards and brass cup pulls. The look is fixed: updates extend it and never restyle it.
- **The mod is never drawn in wood.** Any picture of the mod itself (the hero, the demo) is the in-game panel replica,
  so readers see how the mod really looks (Kyler, 2026-09-24).
- **Design records (read these before any site change):**
  - `PRODUCT.md`: the facts, voice, and every site contract.
  - `DESIGN.md`: the visual system and its named rules, the source of truth for the look.
  - `.impeccable/surfaces/site-index-html.md`: the direction contract.
  - `.impeccable/design.json`: tokens and component snippets.
  - `.impeccable/critique/`: the pre-redesign critique. `.impeccable/config.json`: detector ignores.

### Design rules (from DESIGN.md; keep them)

- **Fixed Materials**: only the wall changes between day and lamplight (ground, paper, ink, rule, link, orange).
  Walnut, brass and kraft keep one value in both modes.
- **One Metal**: brass is the only accent that fills. The primary action (Download) is the one brass plate per view;
  everything else is walnut, kraft or ink. Orange (`--orange`) is only the warning note's tab and the replica's Apply ring.
- **Card Ink**: text on kraft or brass is kraft-ink `#231a12`. Text on walnut is kraft `#d8c29b`, pale kraft `#d9ccb3`
  or polished brass `#e0c27e`, and turns white on hover.
- **Names-Not-Sentences**: Alegreya SC is for names, headings, labels, buttons and figures. Questions, captions, notes
  and prose use the system face.
- **Counter**: every count, capacity and percentage uses tabular figures; proportions are drawn at true share.
- **Mounted-or-Set-In**: paper panels are set in (1.5px rule border, no shadow); frames, plates and
  hardware are mounted (one soft shadow downward). Nothing floats or glows.
- **Rail**: header and footer meet the page with a 3px tarnished-brass (`#8a6a2c`) rail.
- Tokens live in `docs/assets/style.css` `:root`; night values in `@media (prefers-color-scheme: dark)`. Day / night:
  ground `--bg` #dfe4da / #18221f, tint `--bg-deep` #d0d8cc / #111916, `--paper` #eef1ea / #1f2b27, `--ink` #1d2320 /
  #ece6d8, `--muted` #4c5751 / #b8b09d, `--line` #aab5ab / #34423c, `--link` #1f5a50 / #8fd0c2, `--orange` #b35c10 /
  #ffa60f. Fixed: walnut #4a2f1f, #5d3c28, #6e4a32, edge #2e1c12; brass #c19a4b, hi #e0c27e,
  deep #8a6a2c; kraft #d8c29b. Two literals with no custom property: pale kraft `#d9ccb3` (footer text) and every
  page's `<meta name="theme-color" content="#16231c">` (one value for both modes). The legacy classes
  `panel--blue` / `panel--maroon` in install and troubleshooting just map to paper; leave them.
- Fonts: Alegreya SC 500 and 700, self-hosted in `docs/assets/fonts/` (`OFL-Alegreya.txt`); body is the system-ui
  stack; mono is ui-monospace. Noto Sans 400/700 (`OFL.txt`) belongs to the panel replica only. No other webfonts,
  nothing from a CDN at runtime.
- Textures: `kraft.webp` (tile 200px), `brass.webp` (fit to height), `holder.png`
  (nine-slice, `border-image: url(textures/holder.png) 16`), `pull.png` (the one "this opens" sign, on drawers and
  accordions), made by `docs/assets/textures/make_textures.py` (numpy + Pillow, seed 1200; run it from that folder).
  Change the script and re-run it rather than editing images. Every shipping raster carries provenance (a
  `<file>.json` sidecar): run the Impeccable `embed-prompt` command on each new or changed image.
- Themes: light and dark follow the OS (`prefers-color-scheme`) only. There is no toggle and no storage key. Check both.
- Phones: no horizontal scroll at 390px, and tap targets ≥ 44px (buttons 48, small 44, toc 46, accordions 52).
  Breakpoints 900 / 760 (brass Menu button) / 600px.
- Motion: plates lift 1px; an accordion's pull drops 3px. The hero panel's split buttons redraw its cards at once, as
  the game does (`hero-panel.js` with `split.js`, the mod's rounding). Everything is off under
  `prefers-reduced-motion`.
- The in-game panel replica (`game-panel.css`, `demo.js`, `hero-panel.js`, `goods.js`, `split.js`) must keep matching
  the mod's real Storage Allocation panel (the 0.5.7-era native look; `docs/assets/panel.webp` is the in-game
  screenshot). It reads only `--ink`, `--muted`, `--edge`, `--orange`. Don't restyle it toward the cabinet or use its
  parts outside the replica and the hero panel. Update `split.js`, `demo.js` and `hero-panel.js` when the mod's
  rounding (`AllocationPlan.Capacities`) or messages (`StorageView`) change.
- Don't: imitate wood, brass or paper with CSS gradients or bevels; put kickers or small labels above headings; build
  stat rows or icon-tile grids; use hard zero-blur offset shadows; set sentences in Alegreya SC; add a second accent
  fill; use official Timberborn logos or key art (the goods icons in `docs/assets/goods/` are allowed and credited).
- New components: build them from the tokens and components above, match the neighbouring sections, and add them to
  DESIGN.md.

### Content rules

- Every word follows *Writing README and website text* above.
- Describe the mod as it is now. Don't write "New in <version>", "added in …" or version history on player pages;
  that belongs in CHANGELOG.md and the GitHub release notes.
- The played and not-played status matches the README's "Compatibility and testing" exactly. Never invent numbers,
  reviews or screenshots.
- Keep the credits and lines: goods icons are Timberborn's; "An unofficial community mod … Not affiliated with or
  endorsed by Mechanistry"; maintained by Timbermods; MIT for the project, OFL for the fonts.
- Terminology as in game and README: Storage Allocation, Apply 100%, Apply: store nothing, Copy allocations, Paste
  allocations, Clear all, Revert, Max, Allocated goods only, excess, Accept / Obtain / Supply / Empty, Copy settings
  (the game's tool), BeaverBuddies Stability Fork.
- `docs/assets/release.js` is shared across timbermods sites and byte-identical: replace it, never edit it.

### Update the website for a new release

When asked to "update the website for the latest release, consistent with the design":
1. Read the release and the docs: `gh release list -R timbermods/MixedStorage -L 5`,
   `gh release view <tag> -R timbermods/MixedStorage`, README, CHANGELOG.md, `DEVELOPMENT.md`. List every
   player-facing change.
2. Update every place the site states a changed fact:
   - `release.js` fills `data-release="tag"` (fallback text `v1.2.0`), `data-release="asset-name"` (fallback
     `MixedStorage-v1.2.0.zip`), `data-release-href="download"|"notes"` (fallback `…/releases/latest` or
     `…/releases`) and the `data-release-show` pills. The tag and zip-name fallbacks are updated automatically when a
     release is marked Latest (`.github/workflows/latest-release.yml`); the site test checks that the install guide
     keeps the zip name it was written with. Pre-releases change nothing.
   - Game version (`grep -rn "1\.1\.2\.4" docs`): install meta/og description, hero meta row and `#buildings` ledger
     (index), `#requirements` ledger and the `#verify` log line (install), `#faq-version`, and `footer__fine` on all
     five pages including 404. The `version-1.1` folder appears in install `#steps` and troubleshooting `#t-not-listed`.
   - Harmony version (`grep -rn "2\.4\.1" docs`): hero meta, `#buildings` ledger, install requirements and step 4,
     troubleshooting `#t-harmony`.
   - Status: the `#status` note ("What's been played") in index.html, plus PRODUCT.md's Operating Context and
     Honest status.
   - Features and controls: index `#features` rules list (three rules) and the `#in-the-game` ledger; install `#first`
     (steps, the store-nothing paragraph, the drafts note, the controls table), `#upgrading` warn note, `#multiplayer`
     list; the FAQ groups; troubleshooting `#problems`. Supported buildings: index `#buildings` table and `#faq-buildings`.
   - Panel changes: the replica (`demo.js`, `game-panel.css`) and its tips list; ask Kyler for a new in-game
     screenshot rather than faking one.
3. Write it as *Writing README and website text* says, and put new content into the existing components: a new
   question goes in the right FAQ group as a `details.qa` with a stable id; a new problem is a `details.qa` in
   troubleshooting with "Why it happens" / "What to do" h4s; a new control is a row in install's controls table; a
   feature is a `.ledger` row; a caution is a `.panel.note.note--warn`. Don't restyle anything.
4. Test: `node tests/test-site.mjs` (must pass 76/76). It enforces: download buttons (`a.btn` "Download") with
   `data-release-href="download"` and a `/releases/latest` fallback on index and install; elements marked `hidden` stay
   hidden under the stylesheets; `404.html` loads assets by absolute `/MixedStorage/` paths; every page loads
   `assets/release.js` with the same `data-repo` and `data-asset`; the offered-release scenarios. CI also runs the
   allocation tests and `tests/check-version.ps1`.
5. Preview: `python -m http.server 8788 -d docs` (in the background), then open http://localhost:8788/. Capture light,
   dark and a 390px phone. If the personal `impeccable-site-flow` skill is available, use
   `python <skill>/scripts/capsite.py http://localhost:8788/ <out> "" install.html troubleshooting.html faq.html 404.html`;
   otherwise use the Browser pane in both colour schemes at desktop and mobile sizes. Check the changed sections and
   that there's no horizontal scroll. Stop the server afterwards.
6. Optional but recommended: run the detector,
   `"$(ls -d ~/.claude/plugins/cache/impeccable/impeccable/*/skills/impeccable | tail -1)/scripts/impeccable" detect --json docs`
   (parse from the first `[`). Known false positives: cramped-padding on the four main pages and flat-type-hierarchy on
   404 (ignored in config); `side-tab` on the 3px walnut rules (`.page-head`, `.rules li`); `layout-transition` on the replica's fill bar; Noto Sans,
   off-ramp sizes and colours in the replica; `flat-type-hierarchy` from footer headings;
   `gpt-thin-border-wide-shadow` on mounted frames and plates; the "could not read /MixedStorage/assets/style.css" note.
7. If the look changed (a new component or layout), update DESIGN.md and `.impeccable/design.json`.
8. Update the README if it repeats the facts.
9. Ship: branch → commit → push → `gh pr create`. Kyler has said to merge PRs automatically: `gh pr merge <n> --merge`
   (that publishes), then verify:
   - `gh api repos/timbermods/MixedStorage/pages/builds/latest -q .status` is `built`;
   - `curl -s https://timbermods.github.io/MixedStorage/ | grep -c "<a changed string>"` finds the change.

### Full redesign

A new look goes through the whole Impeccable flow (init → critique → audit → direction → build → finish review →
DESIGN.md). With the personal skill: "use the impeccable-site-flow skill to redesign this site".

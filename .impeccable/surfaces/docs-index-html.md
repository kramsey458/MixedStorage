---
version: 1
slug: "site-index-html"
primary_target: "docs/index.html"
related_targets: ["docs/install.html","docs/troubleshooting.html","docs/faq.html","docs/404.html"]
---

# Surface brief: MixedStorage site (docs/index.html with install, troubleshooting, faq, 404)

Scope: docs/. Home: Persuade. Guides: Read. Audience: Timberborn players short on storage; mostly single player, some Stability Fork co-op. Action: understand the percentage mechanism, download the right zip, install right, set up a first mixed building. Proof: the working panel replica, real in-game screenshots (panel.webp, world.webp), exact rounding. Constraints: tests/test-site.mjs contracts (download buttons, /releases/latest fallbacks, hidden stays hidden, 404 absolute paths, same release.js config); release.js byte-identical; describe the mod as it is now; decided by the agent under the maintainer's delegation (no question rounds).

## Direction contract

THESIS: MixedStorage is an apothecary drawer cabinet: one wooden cabinet, many labelled drawers, each drawer's height set by its share. It refuses the generic mod page (eyebrow labels, stat row, icon-tile cards) and the incumbent's copy of the game's dark panels.

OWN-WORLD: Painted-cabinet ground (deep verdigris #18221f at night, sage-limewash #dfe4da by day); walnut drawer fronts (#4a2f1f to #6e4a32); brass label holders and pulls (#c19a4b); kraft label cards (#d8c29b) with dark ink; goods keep the game's icons. Display lettering in Alegreya SC (engraved brass-label small caps, self-hosted, OFL); body in system-ui; figures tabular. The game-panel replica stays as a contained exception.

STORY: One look: one building, several goods, each a share by percentage; percentages reserve space, haulers still bring the goods. Then how the rules work (100%, whole items, stock is never deleted), the panel in the game, try it, install right, co-op, status, help.

FIRST VIEWPORT: Left, the name in brass-label lettering, the one-line pitch, the mechanism line, a brass Download plate and an Install guide link, a one-line meta row (Timberborn 1.1.2.4 · Harmony 2.4.1+ · co-op optional). Right, the top of the mod's own Storage Allocation panel in the game's look (title, "1200 / 1200 items · 2 goods allocated", a card per good at 50/50 with its stock bar); split buttons under it redraw the cards. Any picture of the mod itself (the hero, the demo) is the in-game panel replica. Walnut, brass and kraft never stand in for the mod's UI: readers must not think the mod looks like wood (Kyler, 2026-09-24).

FORM: Apothecary Drawer Cabinet, candidate 7 of 7 (seed 4fa92011). Raises: exact counts in fixed-width figures (nixie counter); the same split drawn at the same scale for 30, 200 and 1,200 capacity (botanical folio); rules annotated as measured callouts, "exactly 100%" (uniform-code annotations). Signature interaction: the split buttons redraw the hero panel with counts that follow the mod's rounding (split.js).

FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance

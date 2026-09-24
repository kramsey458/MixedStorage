---
name: MixedStorage
description: The MixedStorage mod site as an apothecary drawer cabinet around the mod's own in-game panel.
colors:
  sage-limewash: "#dfe4da"
  sage-deep: "#d0d8cc"
  paper-day: "#eef1ea"
  ink-day: "#1d2320"
  muted-day: "#4c5751"
  line-day: "#aab5ab"
  link-day: "#1f5a50"
  orange-day: "#b35c10"
  verdigris: "#18221f"
  verdigris-deep: "#111916"
  paper-night: "#1f2b27"
  ink-night: "#ece6d8"
  muted-night: "#b8b09d"
  line-night: "#34423c"
  link-night: "#8fd0c2"
  orange-night: "#ffa60f"
  walnut: "#4a2f1f"
  walnut-2: "#5d3c28"
  walnut-3: "#6e4a32"
  walnut-edge: "#2e1c12"
  brass: "#c19a4b"
  brass-hi: "#e0c27e"
  brass-deep: "#8a6a2c"
  kraft: "#d8c29b"
  kraft-pale: "#d9ccb3"
  kraft-ink: "#231a12"
  kraft-soft: "#5a4632"
typography:
  display:
    fontFamily: "Alegreya SC, Palatino Linotype, Palatino, Georgia, serif"
    fontSize: "clamp(2.6rem, 6vw, 4.4rem)"
    fontWeight: 700
    lineHeight: 1.08
    letterSpacing: "0.005em"
  headline:
    fontFamily: "Alegreya SC, Palatino Linotype, Palatino, Georgia, serif"
    fontSize: "clamp(1.9rem, 3.8vw, 2.7rem)"
    fontWeight: 700
    lineHeight: 1.08
    letterSpacing: "0.005em"
  title:
    fontFamily: "Alegreya SC, Palatino Linotype, Palatino, Georgia, serif"
    fontSize: "1.4rem"
    fontWeight: 700
    lineHeight: 1.15
  label:
    fontFamily: "Alegreya SC, Palatino Linotype, Palatino, Georgia, serif"
    fontSize: "1.05rem"
    fontWeight: 700
    lineHeight: 1
    letterSpacing: "0.02em"
    fontFeature: "\"tnum\" 1"
  lead:
    fontFamily: "system-ui, -apple-system, Segoe UI, Roboto, Helvetica Neue, Arial, sans-serif"
    fontSize: "clamp(1.1rem, 1.8vw, 1.28rem)"
    fontWeight: 400
    lineHeight: 1.62
  body:
    fontFamily: "system-ui, -apple-system, Segoe UI, Roboto, Helvetica Neue, Arial, sans-serif"
    fontSize: "17px"
    fontWeight: 400
    lineHeight: 1.62
  body-strong:
    fontFamily: "system-ui, -apple-system, Segoe UI, Roboto, Helvetica Neue, Arial, sans-serif"
    fontSize: "1.05rem"
    fontWeight: 650
    lineHeight: 1.4
  mono:
    fontFamily: "ui-monospace, Cascadia Code, SF Mono, Consolas, Liberation Mono, monospace"
    fontSize: "0.9rem"
    fontWeight: 400
    lineHeight: 1.55
rounded:
  hairline: "2px"
  sm: "3px"
  md: "4px"
spacing:
  gutter: "clamp(16px, 4vw, 40px)"
  wrap: "1160px"
  section: "clamp(56px, 8vw, 104px)"
  row: "18px"
  panel: "22px 24px"
  gap-sm: "8px"
  gap-md: "12px"
  gap-lg: "28px"
components:
  button-brass:
    backgroundColor: "{colors.brass}"
    textColor: "{colors.kraft-ink}"
    typography: "{typography.label}"
    rounded: "{rounded.md}"
    padding: "0 22px"
    height: "48px"
  button-brass-hover:
    backgroundColor: "{colors.brass-hi}"
    textColor: "{colors.kraft-ink}"
  button-walnut:
    backgroundColor: "{colors.walnut-2}"
    textColor: "{colors.kraft}"
    typography: "{typography.label}"
    rounded: "{rounded.md}"
    padding: "0 22px"
    height: "48px"
  button-walnut-hover:
    backgroundColor: "{colors.walnut-3}"
    textColor: "#ffffff"
  button-small:
    rounded: "{rounded.md}"
    padding: "0 14px"
    height: "44px"
  button-split-pressed:
    backgroundColor: "{colors.kraft}"
    textColor: "{colors.kraft-ink}"
  pill-kraft:
    backgroundColor: "{colors.kraft}"
    textColor: "{colors.kraft-ink}"
    rounded: "{rounded.sm}"
    padding: "0 10px"
    height: "26px"
  label-holder:
    backgroundColor: "{colors.kraft}"
    textColor: "{colors.kraft-ink}"
    typography: "{typography.label}"
    padding: "3px 10px 3px 4px"
  toc-card:
    backgroundColor: "{colors.kraft}"
    textColor: "{colors.kraft-ink}"
    typography: "{typography.label}"
    padding: "0 12px"
    height: "46px"
  hero-panel:
    backgroundColor: "#436452"
    textColor: "#fbfffc"
    rounded: "4px"
    padding: "10px 11px 9px"
  panel-note:
    backgroundColor: "{colors.paper-day}"
    textColor: "{colors.ink-day}"
    rounded: "{rounded.md}"
    padding: "{spacing.panel}"
  accordion-drawer:
    backgroundColor: "{colors.paper-day}"
    textColor: "{colors.ink-day}"
    typography: "{typography.body-strong}"
    rounded: "{rounded.md}"
    padding: "14px 64px 14px 18px"
  nav-bar:
    backgroundColor: "{colors.walnut}"
    textColor: "{colors.kraft}"
    typography: "{typography.label}"
    height: "64px"
  nav-link:
    textColor: "{colors.kraft}"
    rounded: "{rounded.sm}"
    padding: "0 14px"
    height: "44px"
  nav-link-hover:
    backgroundColor: "{colors.walnut-2}"
    textColor: "#ffffff"
---

# Design System: MixedStorage

## Overview

**Creative North Star: "The Apothecary Drawer Cabinet"**

The site is a walnut cabinet standing against a painted wall: walnut header and footer, brass plates, kraft cards, brass cup pulls on every accordion, and the goods keep the game's own icons. The rules section shows the same share in three sizes of warehouse, as three small copies of the mod's own panel. The mod itself is never drawn in wood: the hero shows the top of the mod's real Storage Allocation panel, in the game's look, redrawn with the mod's own rounding.

Materials are real and made, not faked. Walnut, brass and kraft are procedural textures (`docs/assets/textures/make_textures.py`, fixed seed, no source images, no generative model). Kraft tiles, the brass label holder is a nine-slice border image, and the pull is one image used on every accordion. CSS supplies only edge lines, recesses and cast shadows over those textures. The wall behind the cabinet is plain paint: sage limewash by day, deep verdigris by lamplight. Walnut, brass and kraft keep their values in both modes, because they are materials, not themes.

Density is calm and reading-first. Type is split by job. Alegreya SC, engraved brass-label small caps, names things: the wordmark, headings, labels, buttons, counts. The system face carries every sentence. The in-game panel replica (`game-panel.css`, `demo.js`, and its top in the hero, `hero-panel.js`) is a contained exception. It reproduces the mod's panel in the game's own look and is not part of this system.

**Key Characteristics:**
- One setting for the site: an apothecary cabinet's materials (walnut, brass, kraft). The mod's own UI is always shown as it looks in game.
- Produced material textures (walnut, brass, kraft), tiled or nine-sliced, never imitated with gradients.
- Small-caps display face for names and figures; system sans for prose.
- Exact numbers in tabular figures.
- Two grounds (sage limewash, verdigris) under one set of fixed materials.

## Colors

A painted-wall ground with warm cabinet materials on it. Walnut is the structure, brass is the one metal accent, kraft is the writing surface.

### Primary
- **Label Brass** (brass): the Download plate (over the brass texture), label-holder frames, pulls, the focus ring, text selection, and the note tab. It is the only metal in the world and the only fill that says "act here".
- **Polished Brass** (brass-hi): hover on the brass plate, and the brass-coloured figures on walnut (footer headings).
- **Tarnished Brass** (brass-deep): plate borders, the header's bottom rail and the footer's top rail.

### Secondary
- **Walnut** (walnut, walnut-2, walnut-3, walnut-edge): the header, the footer, walnut plates (secondary buttons), the rules' top rules (walnut-3) and the frame around in-game screenshots.

### Tertiary
- **Kraft Card** (kraft) with **Kraft Ink** (kraft-ink) and **Faded Ink** (kraft-soft): label cards in holders, step numbers, table-of-contents cards, pills, the pressed split button. Text on kraft is always kraft-ink; kraft-soft is only for the percentage under a count. **Pale Kraft** (kraft-pale) is the footer's body text on walnut. It ships as a literal `#d9ccb3` in `style.css` (`.site-footer p`, `.footer__fine`); there is no `--kraft-pale` custom property.
- **Warning Orange** (orange-day / orange-night): the warning note's tab only. The panel replica also reads it for its Apply ring.

### Neutral
- **Sage Limewash** (sage-limewash) / **Deep Verdigris** (verdigris): the page ground, day and night.
- **Sage Shadow** (sage-deep) / **Verdigris Shadow** (verdigris-deep): alternating tinted sections.
- **Wall Paper** (paper-day / paper-night): panels, notes, tables, code, accordions.
- **Ink** (ink-day / ink-night), **Muted Ink** (muted-day / muted-night): text and secondary text.
- **Rule** (line-day / line-night): 1px row rules and 1.5px panel borders.
- **Verdigris Link** (link-day / link-night): inline links, underlined with a 3px offset that thickens to 2px on hover.
- **Browser chrome:** every page sets `<meta name="theme-color" content="#16231c">`, one value for both modes (a green-black near the night ground). It is not a palette token.

### Named Rules
**The Fixed Materials Rule.** Only the wall changes between day and lamplight: ground, paper, ink, rule, link and orange. Walnut, brass and kraft keep one value in both modes.

**The One Metal Rule.** Brass is the only accent that fills. The primary action is a brass plate. Everything else is walnut, kraft or ink.

**The Card Ink Rule.** Anything written on kraft or brass is kraft-ink. Anything written on walnut is kraft, pale kraft or polished brass, and turns white on hover.

## Typography

**Display Font:** Alegreya SC 500/700, self-hosted woff2, SIL OFL (fallback Palatino Linotype, Palatino, Georgia, serif)
**Body Font:** system-ui stack
**Label/Mono Font:** ui-monospace stack for paths, code and keys

**Character:** engraved small caps on brass labels, set against a plain, fast system sans. The display face names things and states figures. It never carries a sentence.

### Hierarchy
- **Display** (700, clamp(2.6rem, 6vw, 4.4rem), 1.08): the wordmark h1 on the home page. Guide page heads use clamp(2.3rem, 5vw, 3.5rem).
- **Headline** (700, clamp(1.9rem, 3.8vw, 2.7rem), 1.08, balanced wrap): section h2. Group titles on the guide pages use clamp(1.6rem, 3vw, 2.1rem).
- **Title** (700, 1.4rem, 1.15): h3. Rules and steps use 1.3rem, notes 1.2rem.
- **Label** (700, 1.0–1.12rem, 0.02em tracking, tabular figures): buttons, nav, pills, toc cards, footer headings.
- **Lead** (400, clamp(1.1rem, 1.8vw, 1.28rem), muted, 60ch max): the sentence under a section heading. The hero pitch is clamp(1.25rem, 2.3vw, 1.55rem) at 1.4, 30ch.
- **Body** (400, 17px, 1.62; 16.5px under 600px): all running text. Measures: 68–72ch for prose, accordion bodies and step text.
- **Body strong** (650): accordion questions, ledger names, table heads and first cells. These are sentences or phrases, so they stay in the body face.

### Named Rules
**The Names-Not-Sentences Rule.** Alegreya SC is for names, headings, labels and figures. Questions, captions, notes and anything longer than a label use the body face.

**The Counter Rule.** Every count, capacity and percentage uses tabular figures, so numbers hold still when the split buttons redraw them.

## Layout

A single centred column, `min(1160px, 100% - 2 × gutter)`, with a fluid gutter (16–40px). Full-width sections pad clamp(56px, 8vw, 104px) top and bottom and alternate between the plain ground and the deeper tint. Headers and footers are full-bleed walnut.

- **Hero:** two columns (1fr : 1.05fr) with name, pitch, mechanism line, a Download + Install guide button row, a dot-separated meta line and a status line on the left, and the hero panel on the right. They stack at 900px. The panel is at most 460px wide, scaled 1.2x (CSS `zoom`, like the game's UI scale) at 1200px and wider.
- **Rules:** three columns, each item under a 3px walnut rule. The three sizes sit under it as three columns (one under 600px).
- **Screenshots:** 0.7fr : 1.3fr, stacked at 900px.
- **Ledger:** two columns of rows (name 11rem, text), one column at 900px, name above text at 600px.
- **Guide pages:** a page head with a 3px walnut rule under it, a row of kraft toc cards, then sections 56px apart. Accordions are capped at 860px.
- **Breakpoints:** 900px (grids collapse), 760px (nav becomes a brass Menu button over a walnut drop-down), 600px (single columns, full-width buttons, tables become stacked rows with a visually hidden head).
- **Targets:** every interactive control is at least 44px tall (buttons 48, small buttons 44, toc cards 46, nav 44 and 50 on mobile, footer links 44, accordion summaries 52).

## Elevation & Depth

Depth is physical. Surfaces are either set into the wall (paper panels: flat, bordered, no shadow) or objects mounted on it (screenshots, plates, holders, pulls). Mounted objects cast one soft shadow from above. Recesses come from inset shadows over the texture. There is no ambient glow and no floating card.

### Shadow Vocabulary
- **Mount** (`0 1px 0 rgba(20,14,8,.35), 0 12px 22px -14px rgba(20,14,8,.7)`, deeper at night): screenshots in their walnut and brass frame.
- **Plate lift** (`0 8px 16px -10px rgba(20,14,8,.75)`): buttons.
- **Hardware drop** (`drop-shadow(0 2px 2px rgba(0,0,0,.45))` for pulls): follows the alpha of the brass images.

### Named Rules
**The Mounted-or-Set-In Rule.** A surface is either mounted (it casts a shadow downward) or set into the wall (a border, no shadow). Nothing floats and nothing glows.

**The Rail Rule.** The header and footer meet the page with a 3px tarnished-brass rail. It is a full-width line, not an offset shadow on a card.

## Shapes

Crisp, lightly eased cabinetry. Corners are 4px on plates, panels, frames and accordions, 3px on nav links, pills and the focus ring, and 2px on note tabs. Borders are 1.5px on paper surfaces and plates, and 1px for row rules. Structural rules (under page heads and over rules items) are 3px walnut. The holder's rivets and rounded corners come from its image, not from CSS radius.

## Components

### Buttons
Engraved plates you press.
- **Shape:** gently eased (4px), 48px tall, label type.
- **Brass plate (primary):** brass over the brushed-brass texture, sized to the plate's height, with a tarnished-brass border and kraft-ink lettering. Used once per view for the main action (Download).
- **Walnut plate (secondary):** walnut-2 with a tarnished-brass border and kraft lettering (Install guide, split buttons, tools).
- **Hover / Active:** lifts 1px (0.15s, cubic-bezier(.2,.7,.2,1)). Brass brightens to polished brass. Walnut warms to walnut-3 with white lettering. On press it settles back.
- **Small:** 44px tall, 14px side padding, 1rem.
- **Pressed toggle:** a pressed split button turns into a kraft card (kraft texture, kraft-ink).

### Chips
- **Pill:** a 26px kraft tag with kraft-ink label type, for release state (Latest release / Pre-release).
- **Toc card:** a kraft card in a brass nine-slice holder (7px × 9px border), 46px tall, used for the section links on guide pages.

### Cards / Containers
- **Panel:** wall paper with a 1.5px rule border, 4px, padded 22px 24px, no shadow.
- **Note:** a panel with a brass tab (58 × 12px, brass texture) pinned across its top-left edge. The warning note's tab is orange.
- **Ledger row:** a 1px rule on top, name in body-strong, text in muted. There is no card or icon.
- **Screenshot frame:** 4px corners inside a 3px walnut ring and a 2px tarnished-brass ring, with the mount shadow.

### Inputs / Fields
The site system has no form fields. The only fields belong to the panel replica (see the exception below).

### Navigation
A sticky walnut bar, 64px, with a 3px brass rail. The wordmark is the brand mark plus Alegreya SC at 1.45rem in kraft. Links are kraft label type, 44px tall. Hover gives a walnut-2 ground with white lettering. The current page gets a 3px brass underline inset and white lettering. GitHub sits in a 1.5px brass outline. Under 760px a brass Menu button opens a full-width walnut drop-down with 50px rows.

### Steps
A numbered list. Each step is a row between 1px rules, and its number sits on a 52px kraft card in a brass nine-slice holder (8px × 9px border, 46px on phones), set in Alegreya SC 1.5rem.

### Accordions (questions and fixes)
Drawers that pull open. Each is a paper panel with a 52px body-strong question and a 38 × 16px brass pull at the right edge. When it opens, the pull drops 3px (0.2s) and the border darkens to walnut-3. Hover turns the question to link colour. The body is capped at 72ch.

### The Hero Panel
The top of the mod's Storage Allocation panel for a full Large Warehouse: the "Storage Allocation" title, the summary line ("1200 / 1200 items · 2 goods allocated") and one card per good (icon, name, "50% allocated", "600 / 600" over "stored / limit", and the orange stock bar). It is drawn with the replica's own classes from `game-panel.css`, so it looks as the mod does in game. Split buttons under it (walnut plates; the pressed one is a kraft card) redraw it using the mod's rounding (`hero-panel.js` with `split.js`), and a visually hidden aria-live line states the new split in words. The caption says it is the mod as it shows in game and links to the full panel in Try it. No motion: the game redraws its cards at once.

### The Three Sizes
The same shares, 1% carrots and 99% gears, in a Small (30), Medium (200) and Large (1200) Warehouse, each drawn as the top of the mod's panel with the replica's classes (`.ig.ig-mini`): the title, a full building's summary, and the two cards with their counts and bars. The small warehouse's panel also shows the mod's own rounding warning in its error colour ("1 allocated good(s) round to 0 items. …"). A caption above each names the building in Alegreya SC and says what 1% comes to. Three columns, one under 600px.

### Exception: the in-game panel replica
`game-panel.css` and `demo.js` reproduce the mod's Storage Allocation panel in the game's own colours, frames and Noto Sans (OFL). The replica is proof of fit, not site style. It reads only `--ink`, `--muted`, `--edge` and `--orange` from this system. Its parts are used only for the replica and the hero panel. Don't restyle either toward the cabinet.

## Do's and Don'ts

### Do:
- **Do** take walnut, brass and kraft from the produced textures in `docs/assets/textures/` (regenerate with `make_textures.py`). Tile kraft at 200px, and fit brass to the element's height.
- **Do** frame kraft cards with the brass holder nine-slice (`border-image: url(textures/holder.png) 16`) and `background-clip: padding-box`. Scale the border width to the element (7–11px vertical).
- **Do** use the brass cup pull as the one "this opens" sign on accordions.
- **Do** set every count, capacity and percentage in tabular figures, and draw proportions at true share.
- **Do** keep controls at least 44px tall, show focus as a 3px brass outline 3px out, and turn off every transition and smooth scrolling under prefers-reduced-motion.
- **Do** keep materials identical in both modes. Only the wall, paper, ink, rules, link and orange change for lamplight.

### Don't:
- **Don't** draw the mod in wood. Any picture of the mod itself (the hero, the demo) is the in-game panel replica. Walnut, brass and kraft never stand in for the mod's UI: readers must not think the mod looks like wood (Kyler, 2026-09-24).
- **Don't** imitate wood, brass or paper with CSS gradients or bevels. If a material needs a new form, produce it in `make_textures.py`.
- **Don't** put small labels or kickers above headings. The heading is the label.
- **Don't** build stat rows of big numbers, or grids of icon tiles. Use ledger rows, measured drawings and the goods' own icons.
- **Don't** use hard, zero-blur offset shadows on cards or buttons. Mounted objects cast soft shadows downward.
- **Don't** set sentences, questions or captions in Alegreya SC.
- **Don't** add a second accent fill beside brass. Orange is only for warnings.
- **Don't** use official Timberborn logos or key art. Goods icons are the game's and stay as they are.

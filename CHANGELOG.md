# Changelog

## v1.0.0

First official release. Everything from v0.5.8, plus fixes from a code review before release.

- The game's Duplicate settings tool now works both ways. Copying from a mixed building still copies its percentages, and a target that cannot take them is left as it was. Copying from a normal building, or one set to store nothing, now turns a mixed building back into a normal one; before, the mixed allocation silently stayed. This is also the way to leave mixed storage.
- Package the download so it extracts correctly on macOS and Linux. Earlier zips used Windows-only `\` folder separators.
- If the installed BeaverBuddies is a build MixedStorage cannot work with, say what is missing in Player.log at startup and refuse multiplayer Apply with that reason, instead of Apply silently doing nothing. The game still starts. Any other Apply error is shown in the panel.
- A damaged saved allocation no longer stops the whole save from loading; that building keeps its normal single good and the log says what was skipped.
- The building list says "Mixed (1 good)". Remove the unused v0.4 triangle clipper. README covers removing the mod and notes that the mod's own text is English-only.

## v0.5.8

- Keep the dev-mode (cheats) panel fully visible below the allocation editor, so its buttons, such as "Finish now" on an unfinished building, are no longer pushed off the bottom of the screen. The editor already left room for the Construction site panel; it now leaves room for the dev-mode panel as well. Layout is unchanged when dev mode is off.

## v0.5.7

- Cheaper mixed-storage visuals. A delivery now redraws only the goods whose stock changed instead of every good in the building, reuses lookups and geometry buffers instead of reallocating them each time, and holds off redrawing buildings the camera cannot see until they come back into view. How storage looks is otherwise unchanged.

## v0.5.6

- Tighten the search row: the search box now starts right after the Search label and fills the rest of the row, removing the empty gap. Layout is otherwise unchanged.

## v0.5.5

- Keep the native Construction site panel fully visible below the allocation editor while a warehouse or pile is being built. The editor now leaves room for any fragments Timberborn stacks beneath it and grows back when construction finishes. Layout is otherwise unchanged.

## v0.5.4

- Keep Copy allocations and Paste allocations fixed above the allocation total so both remain visible at every scroll position. Preserve the existing native styling and allocation behavior.

## v0.5.3

- Align the entire selected storage window to one width, including the native title, description, hauling controls, and allocation editor. Restore the original width when selecting another type of building or closing the window.
- Reuse Timberborn's native textured panel frames, buttons, input fields, checkbox, scrollbar, text colors, and stock-bar colors.
- Preserve the summary cards, compact goods rows, screen-height cap, and fixed allocation total / Apply / Revert footer. Allocation and multiplayer behavior are unchanged.

## v0.5.2

- Use a single scrolling body, removing stacked scrollbar gutters from the summary and goods list.
- Align percentage controls and limits with their headers; wrap the capacity summary and reduce horizontal padding.
- Widen the editor to 440 UI units, bounded by the viewport, expanding left from the sidebar edge. Preserve the fixed Apply/Revert/total footer and screen-height cap.


## v0.5.1

- Bound the allocation panel to available screen height and scroll its content independently.
- Keep allocation totals and Apply/Clear/Revert controls outside the scrolling body.


## v0.5.0

- One MixedStorage download and mod entry for single-player and multiplayer.
- Embed the optional BeaverBuddies bridge and activate it only when BeaverBuddies is loaded.
- Reject the obsolete separate addon with clear upgrade instructions. Preserve allocation save keys.
- Add isolated loading tests with and without BeaverBuddies and legacy-addon detection.


## v0.4.3

- Replace triangle clipping with complete native cell selection at allocation boundaries.
- Validate primary/secondary mesh topology before selecting cells. Fit continuous or unrecognized meshes into their sections without cutting faces.
- Keep exact gameplay limits; visual proportions approximate whole cells. Add partition and topology regression checks.


## v0.4.2

- Replace the small inline contents summary with readable cards: 30px goods icons, bold names, 19px stored/limit counts, allocation percentages and stock fill bars.
- Keep incoming and excess counts visible, and bound the summary height for large goods lists.


## v0.4.1 (visual prototype fix)

- Fix ambiguous native method lookup that caused mixed visuals to fall back to one good.
- Resolve rendering methods by their argument types and validate nine native API signatures against the installed game assemblies.
- Include full exception details in rendering fallback warnings.


## v0.4.0 (visual prototype)

- Divide native goods meshes into deterministic allocation-sized sections, filling each from its actual stock.
- Preserve native materials and icons, with lifecycle cleanup and native visual fallback.
- Coalesce visual updates to at most five rebuilds per building per second.
- Add offline mesh-clipping tests. In-game appearance and performance remain unverified.


## v0.3.1

- Add a live top summary of applied percentages, stored quantities and limits for all allocated or stocked goods.
- Include incoming deliveries and excess stock, independently of search and filters.


## v0.3.0

- Add per-good Max buttons to assign 100% with one click.
- Add Copy allocations and Paste allocations between compatible storage buildings.
- Preserve exact percentages and recalculate limits for each destination capacity.
- Reject incompatible pastes atomically; apply changes through the existing multiplayer event path.


## v0.2.0

- Use MixedStorage naming throughout the mod, multiplayer addon, assemblies, IDs, UI identifiers and allocation save data.

- Add Folktails small, large and underground piles.
- Add Iron Teeth small and large industrial piles.
- Preserve each storage building's native accepted goods and capacity.
- Extend existing allocation persistence, delivery reservations and multiplayer replay events to piles.
- Rename the editor heading to Storage Allocation.

## v0.1.1

- Add a reset button beside each percentage and a clear-search button.
- Compact rows without reducing goods-name or stock-count text sizes.
- Block gameplay shortcuts while editing allocation or search text.
- Add visible styling to inputs and buttons.

## v0.1.0

- Add configurable mixed storage for small, medium and large warehouses.
- Require allocations to sum to exactly 100%, with 0.01% precision.
- Add search, allocated-only filtering and whole-item capacity previews.
- Preserve allocations in saves and support mixed-good Obtain and Supply modes.
- Add the optional BeaverBuddies synchronization addon.

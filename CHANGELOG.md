# Changelog

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

# MixedStorage

**Store several kinds of goods in one warehouse or pile—and choose how much space each gets.**

Divide a building's capacity by percentage. For example, a 1,200-capacity warehouse can reserve **50% for carrots (600)** and **50% for gears (600)**. Percentages reserve space; they do not create goods or instantly fill the building.

**[Download MixedStorage v0.5.4](https://github.com/kramsey458/MixedStorage/releases/download/v0.5.4/MixedStorage-v0.5.4.zip)** · [Release notes](https://github.com/kramsey458/MixedStorage/releases/tag/v0.5.4) · [Report a problem](https://github.com/kramsey458/MixedStorage/issues)

> **v0.5.4 is a prerelease**, built for Timberborn **1.1.2.4**. One download covers single-player and BeaverBuddies multiplayer. Recent layout and visual changes still need more in-game testing.

<img width="625" height="1112" alt="image" src="https://github.com/user-attachments/assets/862e5c13-33a0-47ed-9515-9e2dffefe3a9" />

## What you can do

- **Mix goods:** assign percentages to any goods the building normally accepts.
- **See your stock at a glance:** icons, large counts, percentages, and fill bars summarize the contents.
- **Set up storage quickly:** use **Max** for one good or copy allocations between compatible buildings.
- **Keep controls within reach:** contents scroll while **Copy allocations**, **Paste allocations**, **Apply**, **Revert**, and the allocation total stay in a fixed footer.
- **See mixed contents in the world:** native goods models represent the stored items. The visual split is approximate; the panel shows exact counts.

## Install

1. Download **MixedStorage-v0.5.4.zip** above. On the release page, choose that file under **Assets**, not **Source code**.
2. Close Timberborn. Extract the ZIP's **MixedStorage** folder into your Timberborn **Mods** folder—normally `Documents\Timberborn\Mods` on Windows.
3. Check that `Documents\Timberborn\Mods\MixedStorage\version-1.1\manifest.json` exists. Avoid an extra nested MixedStorage folder.
4. Install **Harmony 2.4.1 or newer** if needed. Enable **Harmony** and **MixedStorage** in the game's mod manager, then restart when prompted.

**Upgrading?** Replace the existing MixedStorage files while the game is closed. Existing MixedStorage allocations are preserved.

**Coming from v0.4.x or earlier?** Remove or disable **MixedStorage-BeaverBuddies**, the old separate addon. Its functionality is now included; leaving it enabled causes a startup error. Keep BeaverBuddies itself if you play multiplayer.

## Set up your first mixed storage building

1. Select a supported warehouse or pile.
2. Find the goods you want in **Storage Allocation**. Use **Search** if needed.
3. Enter a percentage for each good. Leave unwanted goods at **0%**.
4. Make the total **exactly 100%**, then click **Apply 100%**.

For the example above, enter `50` for carrots and `50` for gears, with everything else at `0`. A 1,200-capacity warehouse will allow 600 of each.

**Edits are drafts until you click Apply.** The top summary continues to show the applied settings and actual stock while you edit, search, or filter. Normal hauling rules determine when goods arrive.

| Control | What it does |
| --- | --- |
| **Max** | Sets that good to 100% and all others to 0%. Click Apply to confirm. |
| **× beside a percentage** | Resets that good's draft percentage to 0%. |
| **Clear all** | Clears draft percentages. Current allocations stay active until you apply a valid replacement. |
| **Revert** | Discards edits and restores the current settings. |
| **Allocated goods only** | Shows goods with a nonzero draft percentage. |
| **Copy allocations** | Copies a valid draft totaling 100%. |
| **Paste allocations** | Pastes copied percentages into another building's draft. Click Apply there to confirm. |

Percentages allow **two decimal places**. Copy/paste adjusts item limits to the destination's capacity. The destination must accept every copied good with a nonzero percentage; an incompatible paste leaves the draft unchanged. Stock, hauling mode, and hauler priority are not copied. Copied settings last only for the current game process.

## Supported buildings

| Faction | Warehouses | Piles |
| --- | --- | --- |
| **Folktails** | Small, medium, large | Small, large, underground |
| **Iron Teeth** | Small, medium, large | Small industrial, large industrial |

Buildings keep their normal accepted goods. Warehouses do not gain pile-only goods, or vice versa. **Tanks and map-editor reserve storage are not included.**

## Multiplayer with BeaverBuddies

Install the **same MixedStorage version on every computer**, alongside matching BeaverBuddies versions and Harmony. Enable BeaverBuddies as usual; MixedStorage automatically activates its bundled integration.

There is **no separate MixedStorage multiplayer download**. Single-player users do not need BeaverBuddies.

Applied allocations synchronize through BeaverBuddies. Apply may briefly show a queued message until the next simulation tick. After upgrading, restart every computer's game and start a fresh multiplayer session. Old replay recordings may reference the former addon and may need the original mod versions.

## Useful things to know

- **Items use whole slots.** Percentages are rounded into whole-item limits while keeping the full capacity allocated. Tiny shares can round to zero, especially in small storage buildings. Check the displayed limit.
- **Lowering a limit does not delete stock.** Extra goods remain stored and can be hauled out. The summary marks them as excess.
- **Incoming deliveries count toward limits.** If a reduction conflicts with goods already on their way, wait for delivery and try again.
- **Storage modes still matter.** Accept, Obtain, Supply, Empty, and hauler priority continue to use the game's hauling system.
- **World visuals are approximate.** Whole visible cells cannot always match a percentage exactly. Tiny shares may have no visible cell, some models may look narrower, and banners still show one representative good. Excess goods may not be fully represented. Use the summary for exact amounts; unsupported meshes fall back to the original visuals.

## Having trouble?

| Problem | Check this |
| --- | --- |
| Mod does not appear | Check the folder structure, enable Harmony and MixedStorage, and restart. |
| Apply is unavailable | Every percentage must be valid and the total must be exactly 100%. Wait if a multiplayer change is queued. |
| Paste is rejected | The destination must accept every good with a nonzero copied percentage. |
| Lower limit is rejected | Wait for incoming deliveries to finish. |
| Startup mentions the old addon | Remove or disable MixedStorage-BeaverBuddies, then fully restart. |
| Panel or visuals look wrong | Include a screenshot, resolution/UI scale, building type, and allocations in a report. |

[Open an issue](https://github.com/kramsey458/MixedStorage/issues) with your game/mod versions, other enabled mods, whether you were playing multiplayer, and steps to reproduce. For crashes, include the relevant error and stack trace; review logs for personal information before sharing them. Windows logs are normally in `%USERPROFILE%\AppData\LocalLow\Mechanistry\Timberborn`. After restarting, the previous session is usually in `Player-prev.log`.

## Compatibility and testing

Built against **Timberborn 1.1.2.4**. Multiplayer integration was developed against **BeaverBuddies Stability Preview for Timberborn 1.1**; other builds are not guaranteed compatible.

Players have reported successful use of earlier builds. Automated allocation, geometry, native API, and optional multiplayer loading checks pass, but these do not replace live gameplay testing. **The v0.5.4 layout has not yet been visually verified in game.**

See the [changelog](CHANGELOG.md) for version history and [developer notes](docs/DEVELOPMENT.md) for build instructions and technical details.

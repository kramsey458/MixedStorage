# MixedStorage

**Store several kinds of goods in one warehouse or pile—and choose how much space each gets.**

Divide a building's capacity by percentage. For example, a 1,200-capacity warehouse can reserve **50% for carrots (600)** and **50% for gears (600)**. Percentages reserve space; they do not create goods or instantly fill the building.

**[Download MixedStorage v1.2.0](https://github.com/timbermods/MixedStorage/releases/download/v1.2.0/MixedStorage-v1.2.0.zip)** · [Release notes](https://github.com/timbermods/MixedStorage/releases/tag/v1.2.0) · [Report a problem](https://github.com/timbermods/MixedStorage/issues) · [Website](https://timbermods.github.io/MixedStorage/)

> **v1.2.0** is the current release, built for Timberborn **1.1.2.4**. One download covers single-player and BeaverBuddies multiplayer.

<img width="625" height="1112" alt="image" src="https://github.com/user-attachments/assets/862e5c13-33a0-47ed-9515-9e2dffefe3a9" />

<img width="752" height="364" alt="image" src="https://github.com/user-attachments/assets/f097ffda-3656-4ec6-9e67-7a7a979d60fe" />

## What you can do

- **Mix goods:** assign percentages to any goods the building normally accepts.
- **See your stock at a glance:** icons, large counts, percentages, and fill bars summarize the contents.
- **Set up storage quickly:** use **Max** for one good or copy allocations between compatible buildings.
- **Keep controls within reach:** contents scroll while **Copy allocations**, **Paste allocations**, **Apply**, **Revert**, and the allocation total stay in a fixed footer.
- **See mixed contents in the world:** native goods models represent the stored items. The visual split is approximate; the panel shows exact counts.

## Install

1. Download **MixedStorage-v1.2.0.zip** above. On the release page, choose that file under **Assets**, not **Source code**.
2. Close Timberborn. Extract the ZIP's **MixedStorage** folder into your Timberborn **Mods** folder—normally `Documents\Timberborn\Mods` on Windows.
3. Check that `Documents\Timberborn\Mods\MixedStorage\version-1.1\manifest.json` exists. Avoid an extra nested MixedStorage folder.
4. Install **Harmony** 2.4.1 or newer from the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3284904751) if you don't have it. Enable **Harmony** and **MixedStorage** in the game's mod manager, then restart when prompted.

**Upgrading?** Replace the existing MixedStorage files while the game is closed. Existing MixedStorage allocations are preserved.

**Coming from v0.4.x or earlier?** Remove or disable **MixedStorage-BeaverBuddies**, the old separate addon. Its functionality is now included; leaving it enabled causes a startup error. Keep BeaverBuddies itself if you play multiplayer.

## Set up your first mixed storage building

1. Select a supported warehouse or pile.
2. Find the goods you want in **Storage Allocation**. Use **Search** if needed.
3. Enter a percentage for each good. Leave unwanted goods at **0%**.
4. Make the total **exactly 100%**, then click **Apply 100%**.

For the example above, enter `50` for carrots and `50` for gears, with everything else at `0`. A 1,200-capacity warehouse will allow 600 of each.

To store nothing in a building, set every good to 0% (for example with **Clear all**) and click **Apply: store nothing**. It then stores nothing, like a newly built one. Stock already there is kept and can be hauled out.

**Edits are drafts until you click Apply.** While the draft differs from the building's current settings, the Apply button gets an orange border and bold text. The top summary continues to show the applied settings and actual stock while you edit, search, or filter. Normal hauling rules determine when goods arrive.

| Control | What it does |
| --- | --- |
| **Max** | Sets that good to 100% and all others to 0%. Click Apply to confirm. |
| **× beside a percentage** | Resets that good's draft percentage to 0%. |
| **Clear all** | Sets every draft percentage to 0%. Apply it to store nothing, or enter new percentages. Current allocations stay active until you click Apply. |
| **Revert** | Discards edits and restores the current settings. |
| **Allocated goods only** | Shows goods with a nonzero draft percentage. |
| **Copy allocations** | Copies a valid draft totaling 100%. |
| **Paste allocations** | Pastes copied percentages into another building's draft. Click Apply there to confirm. |

Percentages allow **two decimal places**. Copy/paste adjusts item limits to the destination's capacity. The destination must accept every copied good with a nonzero percentage; an incompatible paste leaves the draft unchanged. Stock, hauling mode, and hauler priority are not copied. Copied settings are kept until you close the game.

## Supported buildings

| Faction | Warehouses | Piles |
| --- | --- | --- |
| **Folktails** | Small, medium, large | Small, large, underground |
| **Iron Teeth** | Small, medium, large | Small industrial, large industrial |

Buildings keep their normal accepted goods. Warehouses do not gain pile-only goods, or vice versa. **Tanks and map-editor reserve storage are not included.**

## Multiplayer with BeaverBuddies

Co-op needs the [BeaverBuddies Stability Fork](https://timbermods.github.io/BeaverBuddies-Stability-Fork/). Every player installs the **same version of MixedStorage** (and the same Stability Fork build) and runs the **same game version**. Enable the Stability Fork as usual; MixedStorage turns on its bundled integration automatically. See [Compatibility and testing](#compatibility-and-testing) for which BeaverBuddies builds work.

There is **no separate MixedStorage multiplayer download**. Single-player users do not need BeaverBuddies.

Applied allocations synchronize through BeaverBuddies. Apply may briefly show a queued message until the next simulation tick. After upgrading, restart every computer's game and start a fresh multiplayer session. Old replay recordings may reference the former addon and may need the original mod versions.

## Useful things to know

- **Items use whole slots.** Percentages are rounded into whole-item limits while keeping the full capacity allocated. Tiny shares can round to zero, especially in small storage buildings. Check the displayed limit.
- **Lowering a limit does not delete stock.** Extra goods remain stored and can be hauled out. The summary marks them as excess.
- **Incoming deliveries count toward limits.** If a reduction conflicts with goods already on their way, wait for delivery and try again.
- **Storage modes still matter.** Accept, Obtain, Supply, Empty, and hauler priority continue to use the game's hauling system.
- **World visuals are approximate.** Whole visible cells cannot always match a percentage exactly. Tiny shares may have no visible cell, some models may look narrower, and banners still show one representative good. Excess goods may not be fully represented. Use the summary for exact amounts; unsupported meshes fall back to the original visuals.
- **The game's Copy settings tool works with mixed storage.**
  - Copying from a mixed building gives the target the same percentages. If the target does not accept all of those goods, or an incoming delivery conflicts, the target keeps its current goods and percentages and Player.log says why. The tool's other settings, such as the storage mode, are still copied.
  - Copying from a building set to store nothing, such as one just built, leaves a mixed building's allocation as it is. So copying its storage mode (Accept, Obtain, Supply or Empty) does not wipe the allocation.
  - Copying from a normal building that stores a good this one accepts turns a mixed building back into a normal one. If a delivery already on its way would no longer fit, the building stays mixed and Player.log says why. To make such a normal building, pick a good for a building without an allocation in the goods dropdown of the game's building list.
- **The mod's own text is in English.** Good names follow your game language.

## Having trouble?

| Problem | Check this |
| --- | --- |
| Mod does not appear | Check the folder structure, enable Harmony and MixedStorage, and restart. |
| Apply is unavailable | Every percentage must be valid and the total must be exactly 100%, or 0% to store nothing. Goods marked "(not accepted here)" or "(unavailable)", which a saved allocation can still hold after a goods mod is removed, must be set to 0%. Wait if a multiplayer change is queued. |
| Paste is rejected | The destination must accept every good with a nonzero copied percentage. |
| Lower limit is rejected | Wait for incoming deliveries to finish. |
| Startup mentions the old addon | Remove or disable MixedStorage-BeaverBuddies, then fully restart. |
| Apply says multiplayer support could not start with the installed BeaverBuddies | Install the BeaverBuddies build named under [Compatibility and testing](#compatibility-and-testing) on every computer. Player.log names what is missing. Single-player usually still works; if even that is refused, disable BeaverBuddies for single-player games. |
| Apply says MixedStorage cannot change allocations with this game version | A game update changed something MixedStorage relies on. Existing allocations still apply, but none can change (by Apply or Copy settings) until you install the MixedStorage version made for your game version. In co-op, every player needs the same game version. |
| Apply shows "Apply failed" | Report it with your Player.log; the message and log say what went wrong. |
| Panel or visuals look wrong | Include a screenshot, resolution/UI scale, building type, and allocations in a report. |

[Open an issue](https://github.com/timbermods/MixedStorage/issues) with your game/mod versions, other enabled mods, whether you were playing multiplayer, and steps to reproduce. For crashes, include the relevant error and stack trace; review logs for personal information before sharing them. Windows logs are normally in `%USERPROFILE%\AppData\LocalLow\Mechanistry\Timberborn`. After restarting, the previous session is usually in `Player-prev.log`.

## Removing MixedStorage

Close Timberborn, then disable MixedStorage in the mod manager or delete its folder. In saves that used it, each mixed building goes back to storing only its largest allocated good. As when you switch a warehouse to a different good in the base game, haulers empty out the other goods, and may move some of the kept good too, before the building fills with it again.

## Compatibility and testing

Built against **Timberborn 1.1.2.4**.

The bundled multiplayer integration is built against the **[BeaverBuddies Stability Fork](https://github.com/timbermods/BeaverBuddies-Stability-Fork)** (formerly called Stability Preview), which is the build to use. Every Stability Fork release so far, 1.0.0 through 1.1.12, has everything the integration needs; install the latest. At startup, MixedStorage checks the installed BeaverBuddies for every part it uses. If anything is missing, it turns co-op Apply off rather than risk a desync, and Player.log names what is missing. The original [BeaverBuddies](https://github.com/thomaswp/BeaverBuddies) by thomaswp and contributors, and BeaverBuddies MultiColony (Beta), have not been tested with MixedStorage.

Players have reported successful use of earlier builds, and the **v0.5.8 layout, including the dev-mode panel, and the v0.5.7 storage visuals have been verified in game**; v1.0.0 through v1.2.0 leave both unchanged apart from v1.2.0's orange ring around Apply. The v1.0.0 to v1.2.0 changes (Copy settings, error messages, loading damaged saves, the Supply mode order, the panel's reason for an unavailable Apply, patch order, leaving mixed storage with a removed good or through the map editor's undo, and in v1.2.0 storing nothing and the Apply ring) have not been played in game yet. Automated allocation, geometry, native API, and optional multiplayer loading checks also pass, but these do not replace live gameplay testing.

See the [changelog](CHANGELOG.md) for version history and [developer notes](docs/DEVELOPMENT.md) for build instructions and technical details.

## License

MIT. See [LICENSE](LICENSE).

The license covers this project's own source code, documentation and website. It does not cover Timberborn, its name or its artwork, which belong to Mechanistry, including the goods icons in `site/assets/goods/` (the game's own textures). The Noto Sans fonts in `site/assets/fonts/` keep their own SIL Open Font License (see `OFL.txt` there).

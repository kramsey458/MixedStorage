# MixedStorage

**Store several kinds of goods in one warehouse or pile, and choose how much space each gets.**

Give each good a percentage of the building's capacity. At **50% carrots** and **50% gears**, a 1,200-capacity warehouse holds 600 of each. Percentages reserve space; they don't create goods or fill the building.

**[Download MixedStorage v1.2.0](https://github.com/timbermods/MixedStorage/releases/download/v1.2.0/MixedStorage-v1.2.0.zip)** · [Release notes](https://github.com/timbermods/MixedStorage/releases/tag/v1.2.0) · [Report a problem](https://github.com/timbermods/MixedStorage/issues) · [Website](https://timbermods.github.io/MixedStorage/) <!-- latest -->

> **v1.2.0** is the current release, built for Timberborn **1.1.2.4**. One download covers single-player and BeaverBuddies multiplayer. <!-- latest -->

<img width="625" height="1112" alt="image" src="https://github.com/user-attachments/assets/862e5c13-33a0-47ed-9515-9e2dffefe3a9" />

<img width="752" height="364" alt="image" src="https://github.com/user-attachments/assets/f097ffda-3656-4ec6-9e67-7a7a979d60fe" />

## What you can do

- **Mix goods:** give a percentage to any good the building normally accepts.
- **See your stock at a glance:** summary cards show each good's stock, limit, share and fill bar.
- **Set up quickly:** search the list, give one good everything with **Max**, or copy a split to another building.
- **See the mix in the world:** the building shows its goods with the game's own models.

## Install

1. Download **MixedStorage-v1.2.0.zip** above. On the release page, choose that file under **Assets**, not **Source code**. <!-- latest -->
2. Close Timberborn. Extract the ZIP's **MixedStorage** folder into `Documents\Timberborn\Mods`.
3. Check that `Documents\Timberborn\Mods\MixedStorage\version-1.1\manifest.json` exists, with no extra folder in between.
4. Install **Harmony** 2.4.1 or newer from the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3284904751) if you don't have it.
5. Start the game, enable **Harmony** and **MixedStorage** in the mod manager, and restart when prompted.

The [install guide](https://timbermods.github.io/MixedStorage/install.html) shows the folder layout and how to check that the mod loaded.

**Upgrading:** with the game closed, replace the MixedStorage files. Your allocations are kept. If you still have the old **MixedStorage-BeaverBuddies** add-on, remove or disable it: it causes a startup error.

## Set up a mixed building

1. Select a supported warehouse or pile.
2. In **Storage Allocation**, type a percentage for each good you want. Leave the rest at **0%**.
3. When the total is exactly 100%, click **Apply 100%**.

Edits are drafts until you click Apply. While Apply would change the building, it has an orange ring.

To make a building store nothing, set every good to 0% and click **Apply: store nothing**. Stock already there stays and can be hauled out.

| Control | What it does |
| --- | --- |
| **Search** | Finds a good in the list. |
| **Max** | Sets that good to 100% and every other good to 0%. |
| **×** beside a percentage | Sets that good to 0%. |
| **Clear all** | Sets every good to 0%. |
| **Revert** | Discards your edits. |
| **Allocated goods only** | Shows only goods above 0%. |
| **Copy allocations** | Copies a valid 100% split. |
| **Paste allocations** | Pastes it into another building. Limits follow that building's capacity. |

Percentages allow two decimal places. Paste works only if the building accepts every copied good. Stock, hauling mode and hauler priority aren't copied, and the copy is kept until you close the game.

## Supported buildings

| Faction | Warehouses | Piles |
| --- | --- | --- |
| **Folktails** | Small, medium, large | Small, large, underground |
| **Iron Teeth** | Small, medium, large | Small industrial, large industrial |

Buildings keep their normal accepted goods. Tanks and map-editor reserve storage aren't included.

## Good to know

- **Limits are whole items.** They always add up to the full capacity. A tiny share in a small building can round to 0; the panel warns you.
- **Lowering a limit never deletes stock.** Extra goods show as excess and can be hauled out.
- **Deliveries on their way count.** If a lower limit clashes with them, wait for them to arrive and apply again.
- **Hauling works as usual.** Accept, Obtain, Supply, Empty and hauler priority follow the game's rules.
- **The mix in the world is approximate.** Banners show one good. The panel has the exact counts.
- **The game's Copy settings tool copies allocations.**
  - From a mixed building, it copies the percentages.
  - From a normal building, it turns a mixed building back into a normal one.
  - From a building that stores nothing, it leaves the allocation alone.
  - If the target can't take the change, for example while a delivery is on its way, it stays as it was and Player.log says why.
- **The mod's own text is in English.** Good names follow your game language.

## Multiplayer

Co-op needs the latest [BeaverBuddies Stability Fork](https://timbermods.github.io/BeaverBuddies-Stability-Fork/) on every computer. Single player doesn't need BeaverBuddies.

- Co-op support is built in and turns on by itself.
- Every player needs the same MixedStorage version, Stability Fork build and game version.
- After upgrading, everyone restarts the game and starts a fresh session.
- Apply may briefly say **Queued for multiplayer** until the next simulation tick.

If the installed BeaverBuddies lacks something MixedStorage needs, co-op Apply is turned off and Player.log says what's missing. The original [BeaverBuddies](https://github.com/thomaswp/BeaverBuddies) by thomaswp and contributors, and Timber Together, haven't been tested with MixedStorage.

## Having trouble?

| Problem | What to do |
| --- | --- |
| The mod doesn't appear | Check the folder layout, enable Harmony and MixedStorage, and restart. |
| Apply is grayed out | Make every box valid and the total exactly 100%, or 0% to store nothing. Set goods marked "(not accepted here)" or "(unavailable)" to 0%. |
| Paste is rejected | The building must accept every copied good. |
| A lower limit is rejected | Wait for incoming deliveries to finish. |
| A startup error names the old add-on | Remove or disable MixedStorage-BeaverBuddies, then restart. |
| Apply says multiplayer support could not start | Install the latest Stability Fork on every computer. For single player, you can disable BeaverBuddies. |
| Apply says MixedStorage cannot change allocations with this game version | Install the MixedStorage version made for your game version. Existing allocations still work. |
| Apply says "Apply failed" | Report it with your Player.log. |

The [troubleshooting guide](https://timbermods.github.io/MixedStorage/troubleshooting.html) has more. To report a bug, [open an issue](https://github.com/timbermods/MixedStorage/issues) with your game and mod versions, other mods, whether you played co-op, and steps to reproduce.

Player.log is in `%USERPROFILE%\AppData\LocalLow\Mechanistry\Timberborn`; after a restart, the previous session is in `Player-prev.log`. Check logs for personal details before sharing them.

## Removing MixedStorage

Close Timberborn, then disable MixedStorage or delete its folder. In saves that used it, each mixed building goes back to one good: the one with the largest share. Haulers move the other goods out, as when you change a warehouse's good in the base game.

## Compatibility and testing

The panel's layout and the storage visuals have been checked in game, and players have reported using the mod. The newer behavior passes automated checks but hasn't been played yet, alone or in co-op. That includes the game's Copy settings tool, storing nothing, the orange ring on Apply, Supply mode, loading damaged saves and the panel's messages.

See the [changelog](CHANGELOG.md) for version history and the [developer notes](DEVELOPMENT.md) for building and how the mod works.

## License

MIT. See [LICENSE](LICENSE). MixedStorage is an unofficial community mod, not affiliated with or endorsed by Mechanistry.

The license covers this project's own code, documentation and website. It doesn't cover Timberborn, its name or its artwork, which belong to Mechanistry, including the goods icons in `docs/assets/goods/`. The fonts in `docs/assets/fonts/` (Noto Sans and Alegreya SC) keep their SIL Open Font License (`OFL.txt` and `OFL-Alegreya.txt` there).

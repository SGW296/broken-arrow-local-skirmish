# Broken Arrow: Local Skirmish

A MelonLoader mod that turns Broken Arrow's **Skirmish** button into a real, **fully-local battle against
the AI**, with a proper setup panel for map, deck, difficulty and the map's own game options. No online
lobby, no anti-cheat, no network required.

> **Single-player / offline only.** This is for offline modded play (EasyAntiCheat off). It does not enable
> online multiplayer with a modded client. It's a fan-made mod, not affiliated with or endorsed by the
> developers or publisher of Broken Arrow.

---

## Install (the easy way)

1. Click the green **Code** button above and choose **Download ZIP**, then unzip it anywhere.
2. Double-click **`install.bat`**.

The installer finds your Broken Arrow install, **downloads and installs MelonLoader v0.7.3 for you** if it
isn't already there, copies the mod into `Mods\`, writes `steam_appid.txt` + a Steam-aware launcher, and adds
a **"Broken Arrow (Modded)"** desktop shortcut.

**Make sure Steam is running and signed in**, then launch the game with that desktop shortcut (not the Steam
Play button, which runs EasyAntiCheat and blocks mods), then click **Skirmish**.

> **Steam must be running.** The shortcut starts the game with anti-cheat off but still with a Steam context.
> Without it (e.g. Steam closed, or launching the raw exe on a fresh install) the game hangs at the
> "Loading Hangar" screen. The launcher makes sure Steam is up first.
>
> The first modded launch is slower because MelonLoader sets itself up once. A black MelonLoader console
> window opening alongside the game is normal.

## How to use

1. Launch modded (the "Broken Arrow (Modded)" shortcut).
2. In the main menu, click **Skirmish**. A **Local AI Skirmish** setup panel opens.
3. Choose your **map**, **deck**, **difficulty**, and any map-specific options (income, time limit,
   day/night, map size), then **Start Battle**.

On dedicated skirmish maps, choose an **AI player**, then choose that player's **AI deck**. Repeat for
each AI ally or opponent you want to customize. Switching between AI players keeps their individual
choices; the status line shows how many have custom decks. Your **Player deck** stays separate.

Each AI defaults to **Same as player**. Use the deck arrows to choose from the available decks, including
your saved custom decks. Create or edit custom decks in the game's deck builder before opening setup.
The human slot is excluded from the AI player list. The inspected built-in maps have nine AI slots.

Changing the map or reopening setup resets every AI choice to **Same as player**. These controls are only
shown for dedicated skirmish maps; PvE scenarios and campaign missions keep their existing deck setup.

v2.6.3 fixes duplicate hook registration in the mod. MelonLoader's automatic registration and the
mod's manual registration were both running. The second load callback cleared the selected AI decks
before any units spawned. Hooks are now registered only once.

v2.6.2 replaces the scripted AI spawn lists with units from each AI's chosen deck. The built-in maps
spawn their AI directly, bypassing the normal player deck system. Changing deck assignments alone did
not change the AI's units in earlier versions.

The AI still follows the map's scripted wave timing and orders. Units are selected from the matching
category in its chosen deck, with a preference for the same unit type and role. Selected upgrades,
skins, and card transports are carried across. Categories missing from a deck are skipped. Deck card
counts weight unit selection; they do not impose a finite inventory on the scripted waves.

You spawn in with your deck against the AI opponents (and any AI allies the scenario defines). Everything
runs on your machine.

## Reporting deck or spawn problems

There is a report that individually selected AI decks still produce the human player's units in v2.6.3. This is under investigation. The managed regression checks pass, but they do not reproduce a live match and do not establish that this report is fixed.

If you see this problem:

1. Use the latest repository download, select clearly different decks for yourself and at least two AI players, and start a battle.
2. After observing the problem, close the game and double-click **`collect-diagnostics.bat`** in the extracted download. Do this before launching the game again, because the next launch replaces the log.
3. Open the new folder under `diagnostics`. Review and share `summary.txt`, `Latest.log`, and `deck-and-spawn-trace.txt` with your report. Include the map, AI player names and selected decks, and what units actually appeared. An unknown map does not prevent collecting the report.

The collector records installed mod file hashes and copies the current MelonLoader log. It does not upload anything or change the game. Logs can include local folder paths and player/deck names. If the log is missing, the summary explains how to collect it after a modded launch.

Infantry cards with a valid selected transport and a positive transport count are intended to spawn loaded into that transport. On-foot infantry is not automatically a game limitation. Include whether the affected cards have transports selected. Also report unusually slow vehicles, such as the M8 Thunderbolt; the cause is unconfirmed. The map controls orders and wave timing, while other installed mods may change unit data.

## Pairs with the Balance Mod

If you also install the [Balance Mod](https://github.com/BovineOverlord/broken-arrow-balance-mod), your
rebalanced stats apply in these local battles. (The Balance Mod needs Local Skirmish to give it an offline
battle to play in, so if you want custom stats, install both.)

## Limits

- **Offline only.** It does not create real online lobbies with a modded client.
- **Which maps.** The panel lists dedicated **skirmish** maps first, followed by **PvE / co-op** scenarios
  and **campaign** missions (including installed Workshop scenarios of these types). Skirmish maps are
  marked `(skirmish)` and use the game's separate `ScenarioType.Skirmish` type; the built-in `*_Skirmish`
  scenarios already define bot slots. Plain multiplayer **PvP** (`*_network`) scenarios remain excluded.
  Map availability depends on your installed game version and content.
- Difficulty and the map's options are honoured. Your first valid deck for the map is preselected, and you
  can change it in the panel.

## Uninstall

Delete `Mods\BALocalSkirmish.dll`. To remove the loader entirely, delete `version.dll` and the
`MelonLoader\` folder from the game directory.

## Build it yourself / extend it

See **[docs/MODDING.md](docs/MODDING.md)** for how to compile from source.

## License

[MIT](LICENSE), 2026 BovineOverlord. Provided as-is, with no warranty. Use at your own risk; you are
responsible for complying with the game's own terms of service.

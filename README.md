# Broken Arrow: Local Skirmish Lobby

A community fork of [BovineOverlord's Local Skirmish](https://github.com/BovineOverlord/broken-arrow-local-skirmish), with a clearer offline lobby, native map previews and configurable teams.

**Public alpha: 3.1.0-alpha.1. Offline only. Tested against Broken Arrow v1.2.0 on Windows x64 with an existing MelonLoader 0.7.3 setup.** Newer game versions are untested. This is an unofficial mod and is not endorsed by the game developers or publisher.

[Download the public alpha](https://github.com/SGW296/broken-arrow-local-skirmish/releases/tag/v3.1.0-alpha.1). The initial user-run lobby/control checklist passed; broader match and compatibility coverage remains open. See [the validation record](docs/ALPHA-VALIDATION.md).

## Download and install

1. Download the **release ZIP** from [Releases](https://github.com/SGW296/broken-arrow-local-skirmish/releases). GitHub's **Code → Download ZIP** contains source, not an installable build.
2. Close the game and extract the release ZIP to its own folder outside the game directory.
3. Run `install.bat`, or use the [manual installation instructions](docs/INSTALL-LOBBY.md).
4. Launch using your existing working offline modded launcher and choose **Skirmish**.

The installer verifies the release DLL, preserves your loader and launcher, and backs up the previous mod and lobby settings outside `Mods`. It requires an already working MelonLoader installation; it does not install a loader or configure online play.

## What the lobby adds

- A battlefield preview using the game's own loaded map-card artwork, with a fallback when unavailable.
- Separate friendly and opposing force rosters, direct army selection and clear active/closed slots.
- Match formats from 1v1 to 5v5 and manual uneven teams on supported dedicated skirmish maps.
- Searchable map and army choices, team nation filters and named saved setups.
- Native scripted AI army presets on the 22 audited v1.2.0 skirmish maps, alongside the original custom-deck option.
- A native-inspired graphical layout, readable match summary and Normal as the balance-reference difficulty label.

Closing a bot changes its runtime activation; it does more than hide a card. Human and required scenario slots remain protected. Native scripted presets use the game's unit pools and orders. Custom-deck substitution follows the upstream extension's existing limitations, including behavior-dependent aircraft requests.

**This alpha improves setup and presentation. It does not implement a new AI commander or rebalance the native AI.** AI behavior experiments are developed separately, offline first; online support remains an investigation.

## Compatibility and alpha limits

| Area | Status |
|---|---|
| Broken Arrow v1.2.0 / Windows x64 / MelonLoader 0.7.3 | Build and local test target |
| v1.2.1, v1.2.2 and other game builds | Untested; no compatibility claim |
| Existing offline loader and launcher | Required; preserved by this installer |
| Online matchmaking, online skirmish and co-op | Unsupported by this release |
| Campaign/PvE and Workshop scenarios | Outside alpha acceptance; authored setup and script limitations apply |
| Resource parity, custom-deck air behavior and a new AI commander | Not provided |

Official updates have changed the engine and game systems since v1.2.0. Do not assume that a build targeting the old APIs works with newer versions. See the [official update announcements](https://steamcommunity.com/app/1604270/announcements/).

This injected offline mod is not an official supported modding route. The developer's published guidance directs custom content toward the scenario editor; this project makes no online or anti-cheat safety guarantee. Review the [developer guidance](https://steamcommunity.com/app/1604270/discussions/0/599657527486041224/) before using mods.

Read [alpha validation and known limits](docs/ALPHA-VALIDATION.md). Automated stand-ins do not run Unity or prove every map, army, resolution or full-match outcome.

## Feedback and development

Use [Issues](https://github.com/SGW296/broken-arrow-local-skirmish/issues) and include the mod/game/loader versions, map, army modes, match format, resolution and reproduction steps. Review logs for personal paths and player/deck names before sharing them. The included diagnostic collector saves reports locally and uploads nothing.

- [Installation, settings and rollback](docs/INSTALL-LOBBY.md)
- [Build, checks and release packaging](docs/MODDING.md)
- [Changelog](CHANGELOG.md)
- [Lobby roadmap](PROJECT-ROADMAP.md)

The full build requires your locally generated game and MelonLoader assemblies. They are not distributed here. Public CI runs managed fixtures; local release validation also compiles against the game and checks Harmony registration.

## Credits and licence

Original Local Skirmish mod: **BovineOverlord**. Lobby fork and public-alpha work: **SGW296**.
The original MIT notice is preserved in [LICENSE](LICENSE). No extracted game maps, textures, fonts or game assemblies are included in release packages; previews and styling borrow assets already loaded by the user's game.

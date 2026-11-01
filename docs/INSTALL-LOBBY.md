# Install Local Skirmish Lobby 3.1.0-alpha.1

This experimental alpha targets **Broken Arrow v1.2.0, Windows x64 and an existing working MelonLoader 0.7.3 offline setup**. It does not install MelonLoader, change a launcher or enable online play. Other game versions are untested.

## Release ZIP installation

1. Download the ZIP attached to the [alpha release](https://github.com/SGW296/broken-arrow-local-skirmish/releases), not the automatic source-code archive.
2. Extract it to a separate folder outside the game directory and close Broken Arrow.
3. Run `install.bat`. If asked, select the game folder containing `BrokenArrow.exe`.
4. Keep the backup location printed by the installer. Launch using your existing working offline launcher.
5. Choose **Skirmish**. The lobby header and log should identify **3.1.0-alpha.1**; MelonLoader lists assembly version 3.1.0.

The package has one top-level mod DLL. The installer verifies it against `manifest.json` and replaces only `Mods/BALocalSkirmish.dll`. It preserves the loader, launcher, original assets and current `UserData/BALocalSkirmish/lobby.json`. An existing mod and settings are backed up outside `Mods` before replacement. The legacy upstream binary is not an installation candidate.

The install manifest verifies file integrity; it does not prove publisher approval or compatibility with untested game versions. An unsupported-version or running-game check must be resolved before installation.

## Manual installation

1. Close the game. Back up `Mods/BALocalSkirmish.dll` and `UserData/BALocalSkirmish/lobby.json`, if present, outside `Mods`.
2. Verify the package DLL's SHA-256 against `manifest.json`.
3. Copy only the package's top-level `BALocalSkirmish.dll` into `Mods`, replacing the previous file.
4. Keep the working loader and offline launcher. Do not leave a renamed second DLL in `Mods`.

## Lobby controls

Choose a dedicated skirmish map, then use the friendly and opposing cards to choose armies.
**Match format** sets 1v1–5v5 using existing scenario slots. **Closed — no player** disables an optional bot while retaining its saved army choice; human and required slots are protected. At least one enemy remains active.

**Native AI preset** selects an authored scripted army on supported built-in maps. **Custom deck** and **Same as your deck** use the upstream deck-substitution extension. These modes have different capabilities: adding aircraft to a custom deck does not make a ground-only behavior request aircraft. Team nation filters constrain available army choices and may replace incompatible selections.

**Normal** maps to the native Medium value 2. Existing saved Hard/Easy choices remain valid. **Reset setup** restores the mod's defaults, including Normal for dedicated skirmish; it also resets slot and army choices. This release does not change AI timing, production or combat rules.

Map previews use loaded game-owned artwork where available. Missing previews have a fallback. Extra scenario teams/rows and options are paged.

## Settings, diagnostics and rollback

Settings live under MelonLoader's `UserData/BALocalSkirmish/lobby.json`. Named setups and the last per-map choices persist. Missing armies or invalid values produce a notice or block launch. Unreadable/newer settings are preserved, with saving disabled for that session.

To roll back, close the game and restore the backed-up DLL. Restore the matching settings backup if needed: an older mod may discard fields it does not understand. To uninstall, remove this mod DLL from `Mods`; keep the loader if other mods use it.

If setup or launch fails, collect `MelonLoader/Latest.log` before relaunch replaces it. `collect-diagnostics.bat` saves a local report and uploads nothing. Review logs for personal paths and names before attaching them to an issue. Include the map, slot/army modes, resolution and steps that reproduce the problem.

See [alpha validation](ALPHA-VALIDATION.md) for actual checks and remaining live acceptance. Builds on your machine, successful hook registration and a correct file hash are separate from successful in-game behavior.

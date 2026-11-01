# Local Skirmish UI — 2.8.0 local fork

> Historical development notes. For the public alpha use [INSTALL-LOBBY.md](INSTALL-LOBBY.md).

Team roster and map-preview iteration for the existing Broken Arrow v1.2.0 + MelonLoader setup.
This is a local fork, not an official upstream release.

## Install manually

1. Close Broken Arrow.
2. Keep a backup of your current `C:\Games\Broken Arrow\Mods\BALocalSkirmish.dll` outside `Mods`.
   The package includes the previous 2.7.0 UI DLL in `rollback`, and the original upstream 2.6.3
   DLL separately in `upstream-original`.
3. Copy **only the package's top-level `BALocalSkirmish.dll`** into
   `C:\Games\Broken Arrow\Mods`, replacing the existing file of that name.
4. Keep your existing MelonLoader and working offline launcher. Do not run the upstream
   installer, copy the rollback directory into Mods, or install a second renamed copy of the DLL.
5. Launch as usual and click Skirmish. The MelonLoader log should identify version `2.8.0`.

The package is prepared for manual installation; it has not changed the installed mod or game files.
No original game assets, launcher settings or MelonLoader files need replacement.

## What changed

- Your team and opposing team appear side by side, including your human row. Click a deck
  directly on any configurable row; there is no separate AI slot chooser.
- Built-in map previews use the game's own map-card sprites. Scenario PNG/JPG previews take
  precedence. Missing previews remain a clear placeholder; no game assets need to be copied.
- Searchable lists for maps, decks and enum settings, with paging.
- Scenario category filters, readable default deck/map names and random map within the category.
- “Time of day / Random” fixes the misleading “Daytime / Random nation” text while retaining
  the exact integer value the scenario expects. Map size uses “Automatic”.
- Standard skirmish rosters show you plus four allied AI, against five AI. Unusual scenarios
  retain their actual rows and teams, with paging as needed. Read-only rows stay visible.
  Counts describe scenario metadata, not guaranteed active spawns; team/AI-count editing is deferred.
- Every public map option is accessible through pages. Numeric values enforce scenario bounds/steps.
  Income/timer units are not guessed from their names.
- Settings are remembered per scenario on map change, close and launch. Save/load named presets
  for the current scenario, or restore its own defaults. Saving the same name replaces that preset.

## Settings and rollback

Settings normally live at `C:\Games\Broken Arrow\UserData\BALocalSkirmish\lobby.json`, using
MelonLoader's configured UserData directory. Missing decks/slots and invalid option values fall back
to scenario defaults with a notice. Moving the game/scenario folder may invalidate its saved identity.

Unreadable or newer-format settings are preserved and saving is disabled for that session. To reset,
close the game, move `lobby.json` aside and restart; retain that file if you want to recover presets.

To roll back, close the game and copy `rollback\BALocalSkirmish.dll` over
`C:\Games\Broken Arrow\Mods\BALocalSkirmish.dll`. The new settings file is harmless to the old version.
The rollback DLL is the preserved 2.7.0 build from the previous package. `upstream-original` contains
the unchanged upstream 2.6.3 DLL if you need to return to the original mod instead.

## Validation status

Built against the local generated game/MelonLoader assemblies: net6.0/x64, zero warnings/errors.
55 lobby/row-binding checks, 24 roster checks, 23 preview checks and 317 upstream deck/spawn checks
pass, plus the managed Harmony registration test.

**In-game testing of this fork is still required.** Automated checks do not verify live UI layout,
native IL2CPP dispatch or scripted AI behaviour. The upstream AI deck-selection problem remains
unresolved; this UI iteration does not claim a gameplay fix. See `UI-ITERATION.md` for the smoke
checklist, API evidence and limitations. Stay with your existing offline modded launch procedure.

Upstream: https://github.com/BovineOverlord/broken-arrow-local-skirmish (MIT; license included).

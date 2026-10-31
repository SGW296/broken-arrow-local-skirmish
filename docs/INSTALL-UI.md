# Local Skirmish UI — 2.7.0 local fork

First UI/configuration iteration for the existing Broken Arrow v1.2.0 + MelonLoader setup.
This is a local fork, not an official upstream release.

## Install manually

1. Close Broken Arrow.
2. Keep a backup of your current `C:\Games\Broken Arrow\Mods\BALocalSkirmish.dll` outside `Mods`.
   The package also includes the original, unchanged upstream DLL in `rollback`.
3. Copy **only the package's top-level `BALocalSkirmish.dll`** into
   `C:\Games\Broken Arrow\Mods`, replacing the existing file of that name.
4. Keep your existing MelonLoader and working offline launcher. Do not run the upstream
   installer, copy the rollback directory into Mods, or install a second renamed copy of the DLL.
5. Launch as usual and click Skirmish. The MelonLoader log should identify version `2.7.0`.

The package is prepared for manual installation; it has not changed the installed mod or game files.
No original game assets, launcher settings or MelonLoader files need replacement.

## What changed

- Searchable lists for maps, decks, AI slots and enum settings, with paging.
- Scenario category filters, readable default deck/map names and random map within the category.
- “Time of day / Random” fixes the misleading “Daytime / Random nation” text while retaining
  the exact integer value the scenario expects. Map size uses “Automatic”.
- AI slot team, relationship and UID are visible. Selecting a slot configures its deck;
  it does not create a new opponent. Counts describe scenario metadata, not guaranteed active spawns.
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
The rollback DLL was verified byte-for-byte against the installed mod before this package was built.

## Validation status

Built against the local generated game/MelonLoader assemblies: net6.0/x64, zero warnings/errors.
46 lobby checks and 317 upstream deck/spawn checks pass, plus the managed Harmony registration test.

**In-game testing of this fork is still required.** Automated checks do not verify live UI layout,
native IL2CPP dispatch or scripted AI behaviour. The upstream AI deck-selection problem remains
unresolved; this UI iteration does not claim a gameplay fix. See `UI-ITERATION.md` for the smoke
checklist, API evidence and limitations. Stay with your existing offline modded launch procedure.

Upstream: https://github.com/BovineOverlord/broken-arrow-local-skirmish (MIT; license included).

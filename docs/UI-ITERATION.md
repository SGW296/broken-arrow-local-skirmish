# Local lobby UI iteration — 2.7.0 local fork

Base: BovineOverlord/broken-arrow-local-skirmish at `77ba5ae30bf4705b1119ae39a0a829708ede6b6b`.
Branch: `ui/clear-local-lobby`. The version number identifies this fork, not an upstream release.

## Findings and priorities

The original lobby is implemented in `src/LocalSkirmish.cs` using Unity UI. It cycles through all
maps/decks, displays at most six public options, resets AI choices when changing maps/reopening,
and has no persistence. It already loads map previews.

Implemented priorities:

1. Direct selection: searchable, paged lists for maps, decks, AI slots and enum options; filters
   for dedicated skirmish, PvE/co-op, campaign/missions and all local scenarios. PvP stays excluded.
2. Clear data: readable names, category, team relationship, stable slot UID, customized-deck count,
   and the difference between metadata slots and actual active spawns.
3. Correct known text keys without changing scenario values. Expose every public option with
   paging; validate numeric input against the scenario's min/max/step. Unknown units stay explicit.
4. Use scenario defaults as the safe baseline preset. Remember per-scenario settings and allow
   named presets per scenario. Generic income/duration presets would require verified semantics.

Deferred: adding/removing AI, team editing, per-slot difficulty, weather and new victory rules.
The existing launcher does not safely expose these as generic controls. Scripts still own waves/orders.

## API evidence from the installed generated assemblies

- `ScenariosService._scenarios`, `ScenarioSource.MetaFile`, `Folder`, `Name`, `PreviewPath`:
  scenario enumeration, identity and existing previews.
- `ScenarioMetaFile.Teams`: team side/name and slot bot ID/name/flag. The upstream loader assumes
  the first Alpha slot is human. The UI describes this existing assumption, without a team editor.
- `DeckService.GetAvailableDecksForScenario(scenario, 0)`: original deck-list API preserved.
  `IDeckDataModel.FileName`, `Name`, `CountryID`, `Spec1ID`, `Spec2ID`: persistence identifiers.
  Per-AI legality beyond the original returned deck list is not newly established.
- `ScenarioPublicOption`: UID, order, name, default, dropdown keys and min/max/step. Original
  UID/value pairs still feed `ISceneLoadManager.ScenarioPublicOptions`; difficulty still uses
  `CampaignService`. `ISceneTransition.ChangeScene` and the original AI hooks remain the battle path.
- `MelonEnvironment.UserDataDirectory`: settings go under `BALocalSkirmish/lobby.json`.

Installed Airbase metadata declares `gameVersion: 1.2.0`, five Alpha and five Bravo slots. With the
existing human-slot assumption, that gives four allied and five opposing AI metadata slots. Scripts
decide which units spawn; this does not establish an arbitrary working 5v5 setup.

Daytime option UID 221 maps value 3 to `ui_lobby_random_nation` in that metadata. It now displays
**Random** only in this context. `Size Auto` becomes **Automatic**. Known default deck identifiers
receive readable country/default names; custom deck names stay intact. Unknown text keys keep the
original readable fallback. This is English presentation, not complete game localization support.

## Persistence

- Save on map change, close, launch and explicit preset save. Individual edits remain in memory
  until one of these actions; a crash can lose unsaved edits.
- Scenario identity includes type, folder and name. Moving the install/scenario can invalidate
  previous settings. Same-name Workshop scenarios in different folders do not share setups.
- Deck identity uses country, specializations and file name (name fallback if no file name).
  Missing/renamed/ambiguous decks fall back instead of selecting another list position.
- AI assignments use UID. Missing slots/options and invalid values are skipped with a notice;
  current scenario defaults fill those choices.
- A same-name preset replaces the preset for that scenario, case insensitive. Other scenarios
  keep their own presets. Restoring defaults does not delete presets.
- JSON writes use a temporary sibling file followed by replacement. Read/format failures preserve
  the original settings and disable saving that session. To recover, close the game, move the
  settings file aside and restart. Write failures are shown and logged without blocking play.
- No new companion DLL is required; JSON support comes from .NET 6.

## Validation and limits

Release targets net6.0/x64 against the generated assemblies in `C:\Games\Broken Arrow`.
The build passes without warnings/errors. Lobby regressions cover labels, filtering, stable identities,
map switching, presets, bounds, JSON roundtrips and failure preservation. Upstream managed deck/spawn
and real managed Harmony-dispatch regressions also pass.

**This fork has not been tested inside the running game.** Compilation and managed tests do not
prove native IL2CPP dispatch, live UI layout or scenario behaviour. Upstream reports an unresolved
AI deck-selection issue in 2.6.3; this UI iteration does not claim to fix it. Game/loader/installed-mod
files were not changed to build or package this work.

## In-game acceptance checklist

- Open Skirmish at 1280×720 and 1920×1080: check scaling, text fit, input focus, clicks and reopening.
- Browse/search every category, empty results and pages. Random map must stay inside its category.
- On Airbase, verify readable names, allied/opposing slot UIDs, “Time of day / Random” and
  “Map size / Automatic”. Assign clearly different decks to two AI slots.
- Try valid/invalid timer and income steps, and cancel without applying. On a scenario with more
  than four public options, verify every page and retain edits between pages.
- Save/alter/load a preset; switch maps and return; close/restart and verify settings. Restore
  defaults and check that named presets remain available.
- Launch a previously working skirmish, observe decks/units, finish and return to menu. Check
  another scenario category separately because script assumptions differ.
- If AI decks misbehave, collect the original diagnostics before relaunching. Record map, slot
  UIDs, selected decks and observed units. Keep the gameplay report separate from UI bugs.

Source and known limitations: https://github.com/BovineOverlord/broken-arrow-local-skirmish

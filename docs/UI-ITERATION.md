# Local lobby iteration — 3.0.0 local fork

Base: BovineOverlord/broken-arrow-local-skirmish at `77ba5ae30bf4705b1119ae39a0a829708ede6b6b`.
Branch: `feature/native-skirmish-parity`, based on the previous UI branches.
The version number identifies this fork, not an upstream release.

## 3.0.0: native feature investigation and full lobby overhaul

Implemented runtime active/closed optional bots, 1v1–5v5 shortcuts, asymmetric setups, fill-all,
protected human/required slots and launch validation. Closed states, native AI profile IDs and team
nation filters persist without breaking older setup files. Nation filters constrain actual armies.

Native army presets connect through `NodeGetSlotData.InitSolo` and `AiPresetId` on the 22 verified
shipped v1.2.0 maps. Profile slots retain native scripted UnitInfo; custom-deck slots retain existing
wave replacement. Both modes can be mixed. Required plan-binding failures stop initialization
instead of silently loading a different army.

The full lobby uses a 1680×920 composition: battlefield/settings sidebar, two large native-style force
panels, player cards, closed-state controls, borrowed native fonts/artwork and larger matching pickers.
Asset availability determines exact visual fidelity. No network prefab is instantiated or shared
sprite/font destroyed. Live match/visual validation remains necessary.

Detailed evidence and limits:

- [Native feature audit and offline parity matrix](NATIVE-SKIRMISH-AUDIT.md)
- [Closed-slot native-code and graph traces](CLOSED-SLOT-ENGINE.md)
- [Native AI profile execution path](NATIVE-AI-PROFILE-NOTES.md)
- [Native UI design and asset borrowing](NATIVE-UI-DESIGN.md)
- [Current installation and live checklist](INSTALL-LOBBY.md)

This is the implemented offline subset. Network synchronization, invitations, matchmaking, client
readiness, extra humans/team switching and spectators are not copied through decorative controls.
The notes below describe the earlier 2.8.0 iteration.

## 2.8.0: visible teams and working map images

- Replaced the separate AI-slot chooser with two visible team columns. Your human row and every
  configurable AI row have their own deck button. Callbacks bind to that row's AI object rather
  than a global selected-slot index. Non-configurable metadata rows remain visible/read-only.
- Built-in maps show five rows on each side (you + four allies, five opponents). Larger rosters
  page rows; additional sides remain distinct and page teams instead of being relabelled enemies.
- Fixed the actual preview source: built-in skirmish folders such as Airbase contain no loose
  preview image. The native menu uses `GameConfig.Instance.MapCardsConfig.Data`, whose
  `MapCardData` exposes `SceneName`, `MapPreview` and `Image`. Exact scene matching selects
  `MapPreview`, with `Image` as fallback; `ScenarioDataManager._mapCardsConfig` is a second source.
- Explicit/scenario PNG/JPG/JPEG files take precedence. Native fallback is labelled **Base map
  preview** because a scenario may use only part of that map. No assets are redistributed.
- Shared native sprites are borrowed and never destroyed. Only images created from loose files
  are released by this mod. Missing native assets retry briefly as the menu finishes loading.
- Existing saved setups/presets keep the same schema and identifiers.

55 lobby/projection/row-binding checks, 24 roster checks and 23 preview checks pass, alongside
317 upstream deck/spawn checks and the Harmony registration fixture. Native APIs compile against
the local assemblies; live game visual testing is still required.

### Native lobby comparison

The [official manual, page 15](https://ftp.matrixgames.com/pub/BrokenArrow/BrokenArrowManualEBOOK.pdf#page=15)
shows two force columns, five rows each, per-AI preset selection, human deck, portraits/flags and
add/lock controls. It is a launch-era screenshot; its old requirement for human teammates is obsolete.
The [developer's Allied Command update](https://www6.slitherine.com/news/broken-arrow-allied-command-update-out-now)
(16 December 2025) confirms allied bots, any-side bot slots, difficulty/specialization controls,
private/autofilled skirmish lobbies and time-of-day selection.

Local v1.2.0 metadata confirms `TeamPanelView` has two team containers and `LobbySlotView` exposes
add/kick/lock handlers. Native `TeamPanelBotSlot` selects an `AiProfilePreset` (country + specializations),
not an arbitrary custom `IDeckDataModel`. Those UI actions reference network lobby/room services;
opening the online menu offline is not automatically supported. This iteration mirrors its
organization while retaining the existing local launch path, without fake add/kick/lock controls.

## Findings and priorities

The original lobby is implemented in `src/LocalSkirmish.cs` using Unity UI. It cycles through all
maps/decks, displays at most six public options, resets AI choices when changing maps/reopening,
and has no persistence. It already loads map previews.

Implemented priorities:

1. Direct selection: searchable, paged lists for maps, decks and enum options; visible AI rows; filters
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
  scenario enumeration, identity and file previews; native map-card sprites cover built-in skirmish.
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
- On Airbase, verify your human row and four allies appear opposite five AI, each with its own deck
  button. Assign clearly different decks across both columns and confirm other rows stay unchanged.
  Check “Time of day / Random”, “Map size / Automatic” and the native map preview image.
- Switch maps repeatedly, including scenarios with their own preview files. Images must match the
  scene and persist after reopening. Missing images must not prevent launching. Check a scenario
  with additional/read-only rows or sides if installed; every actual row must remain accessible.
- Try valid/invalid timer and income steps, and cancel without applying. On a scenario with more
  than four public options, verify every page and retain edits between pages.
- Save/alter/load a preset; switch maps and return; close/restart and verify settings. Restore
  defaults and check that named presets remain available.
- Launch a previously working skirmish, observe decks/units, finish and return to menu. Check
  another scenario category separately because script assumptions differ.
- If AI decks misbehave, collect the original diagnostics before relaunching. Record map, slot
  UIDs, selected decks and observed units. Keep the gameplay report separate from UI bugs.

Source and known limitations: https://github.com/BovineOverlord/broken-arrow-local-skirmish

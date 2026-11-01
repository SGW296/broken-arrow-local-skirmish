# Native skirmish lobby audit and offline parity plan

Audit date: 31 October 2026. Target: the installed **Broken Arrow v1.2.0** and its generated
MelonLoader/IL2CPP assemblies. This is a feature audit, not a claim that the mod already implements
every item below. No original game files were edited to perform it.

## Evidence and confidence

Three different kinds of evidence are kept separate:

- **Publisher-confirmed behaviour:** the official [Allied Command announcement, 16 December 2025](https://www6.slitherine.com/news/broken-arrow-allied-command-update-out-now)
  confirms allied bots, bots in available slots on either side, difficulty/specialization options,
  private skirmish lobbies filled with bots on creation, and time-of-day selection.
- **Historical visual reference:** the [official manual, page 15](https://ftp.matrixgames.com/pub/BrokenArrow/BrokenArrowManualEBOOK.pdf#page=15)
  shows separate friendly/enemy force columns, five rows per side, portraits/flags, the human's
  deck name, AI preset dropdowns and slot controls. Its prose reflects launch restrictions and
  must not be used as a current minimum-player rule. Allied bots were added later.
- **Installed v1.2.0 API/data evidence:** read-only inspection of `Il2CppBrokenArrow.dll`,
  `Il2CppNetworkCommon.dll` and the installed scenario metadata verifies the types, members,
  enum values and data below. These generated wrappers expose native method signatures; their
  existence alone does not reveal all native/server validation rules or prove offline operation.

No live online lobby was created during this audit. Exact native v1.2.0 hover behaviour, animations,
start-button validation and post-kick readiness transitions remain live-test items.

## What the native lobby presents

The native organization is a lobby with **both teams visible together**, individual slot rows,
map selection/preview and match settings. The local UI should copy that organization directly.
The installed `TeamPanelView` has `_team1Container`, `_team2Container`, a per-side `Slots`
dictionary, `_team1DeckPreview` and `_team2DeckPreview`.

A row is a `LobbySlotView` with a side/index, status and player/bot identity. It owns separate
`TeamPanelPlayerInfo`, `TeamPanelBotSlot` and `TeamPanelEmptySlot` presentations. Consequently,
an empty/closed row is a meaningful lobby state, not simply an AI row with its deck hidden.

`TeamPanelBotSlot` contains country/specification images, avatar/frame/patch/epaulet images,
`_botPresetDropdown` and `_kickButton`. `TeamPanelEmptySlot` exposes `LockButton`, `UnlockButton`,
`BotButton`, `IsLocked`, plus/invite/switch visuals. `LobbySlotView` has
`OnSetBotSlotButtonClick`, `OnKickBotSlotButtonClick`, `OnLockSlotButtonClick`,
`OnChangeSlotButtonClick` and `SetBotSlot(bool useRandomPreset)`.

These members substantiate row-level add/kick/lock/switch controls. They do not establish that
calling those native handlers without a network lobby safely changes an offline mission.

## Slot states, sides and identities

Exact installed `Il2CppNetworkCommon.Enums.LobbySlotStatus` values:

| State | Value | Consequence for an offline equivalent |
|---|---:|---|
| `None` | 0 | Preserve as unknown/uninitialized, rather than displaying a playable AI. |
| `Free` | 1 | A genuinely open slot in the online model. Offline has no second human joining it. |
| `Occupied` | 10 | Human/player occupancy is distinct from bot occupancy. |
| `Locked` | 20 | Closed/locked presentation is distinct from an open empty row. |
| `Inaccessible` | 30 | Do not offer ordinary slot edits without determining why it is inaccessible. |
| `OccupiedByBot` | 40 | Bot presentation and configuration. |

The enum names, lock/unlock buttons and `IsLocked` property are verified. The exact sequence of
native actions required after kicking an AI, including whether every free slot blocks launch in
every mode, was not established from wrapper signatures. Do not hard-code an unverified online
rule merely because it appears in older community guidance.

`TeamSide` values are `Alpha=0`, `Bravo=1`, `Spectators=100`, `Neutral=101`,
`AllPlayableTeams=253`, `AllTeams=254`, `NotDefined=255`. The latter values are not extra teams
to create as ordinary opponent columns. For unusual scenarios retain source identity and treat
unknown/nonplayable sides conservatively.

`Messages.Lobby.LobbySlot` carries `ClientId`, `Status`, `Country`, `SpectatorMode`, `Mandatory`,
`HasScenarioDeck`, `AllowedCountries`, `IsBot`, `BotPresetId`, `BotId`, `BotNameIdx` and
`ClientShortDeck`. Human client ID, slot index, bot UID and preset ID are different identifiers.
Saved offline choices must not interchange them.

The mission's `ScenarioSlotMeta` instead provides `PlayerName`, `IsBot`, `BotId`, `Mandatory`,
`Locked`, `DeckRestrictions` and `DeckList`. Preserve the original metadata and runtime UID links.
Closing an optional bot in the local UI must eventually prevent that bot's native/scripted spawning;
changing a caption or omitting its deck assignment is not enough.

## Native capacity, required slots and readiness

All **22 installed dedicated skirmish scenarios** declare `gameVersion: 1.2.0` and have five Alpha
plus five Bravo metadata slots. All ten slots have `bot=true`, `mandatory=false`, `lock=false`,
and each scenario exposes four public options. The existing offline launcher treats the first Alpha
slot as the human; this yields the familiar human plus four allies versus five opponents.

This survey supports a five-row-per-side default layout. It does not establish the limits of all
Workshop scenarios, campaign missions or custom PvP modes. Do not invent additional player UIDs.
`RoomTeamData` exposes `Slots`, `MaxClients` and named default capacity properties; `LobbyState`
exposes `ClientsCount`, `MaxClients`, `MaxPlayers`, `PlayersCount`, `ScenarioMeta`,
`SyncScenarioSlots(...)` and `ValidateScenarioSlots()`. Numeric current server minimums are not
available from those method signatures. In particular, the launch manual's three-per-side prose
must not be reused as a v1.2.0 invariant.

Native readiness includes content and network state. `ClientLobbyStatus` has `None=0`,
`ServerChecking=1`, `NoDLC=2`, `ScenarioNotSynced=31`, `ScenarioNotPossibleToSync=32`,
`ScenarioDownloading=33`, `NotReady=200`, `Ready=201`.
`NetworkLobbyService` exposes `ChangeClientReadyStatus(int clientId, bool setNotReady)`,
`IsLastNotReadyClient(int clientId)`, `AllClientsReady()` and `TryLaunchSingleplayerLobby()`.
`NetworkRoomService` separately exposes `SendClientReadyToPlay(bool allClients)` and
`IsReadyToPlay` for room/battle preparation.

A one-human offline screen should validate a launch plan and start directly; a decorative “ready”
toggle would not reproduce content synchronization, server readiness or a second human client.
For local slot closing, a conservative policy is to protect the human and metadata-required slots,
keep at least one playable opposing participant, and explain any scenario-specific restriction.
That policy is a proposed offline safeguard, not a recovered native-server rule.

## Bot presets, difficulty, countries and decks

Native **AI presets** are not the same thing as a user's saved full deck:

- `TeamPanelBotSlot.OnBotPresetSelected(int presetId)` operates on `AiProfilePreset` IDs.
- `AiProfilePreset` carries `Id`, `TechName`, `DisplayNameLoc`, `ContentMembership`, `Country`,
  `Spec1Id`, `Spec2Id`, cosmetic images/IDs and specialization previews.
- `GameConfig.Instance.AiConfig.AiProfilePresets` is the native preset list.
  `AiConfig.AiNames` groups bot names by country, and `SwitchPresetIfCurrentIsInvalid` exists.
- `LobbySlotView.BotPresetId` is a byte in the lobby model. Native `ChangeSlotStatus` accepts
  a side/index/status plus bot preset/name IDs. These are not direct `IDeckDataModel` references.

This makes a native-style profile picker and country/spec badges plausible local integrations.
It does **not** prove that selecting a profile alone changes the units spawned by an offline map's
script. That must be connected to the local battle plan, or the control must remain unavailable.

The installed difficulty model is `None=0`, `Easy=1`, `Medium=2`, `Hard=3`.
`NetworkLobbyService.ChangeLobbyDifficulty(DifficultyLevel)` is lobby-wide, and native
`LobbySettings` has a difficulty toggle group. No per-bot difficulty field was found in
`AiProfilePreset` or `LobbySlot` during this audit. The publisher confirms difficulty options but
does not establish independent difficulty for every bot. Retain one difficulty control until a
separate, verified per-bot execution path exists.

The native `LobbyForcesSettings` uses per-side country buttons and `UpdateAllowedCountries`.
`NetworkLobbyService.ChangeLobbyTeamCountriesFilter(...)` and `ChangeLobbyMirrorMatches(bool)`
exist; `LobbyOptions.AllowMirrorMatch` and slot `AllowedCountries` are separate restrictions.
Offline country filters should initially filter/validate deck choices, using the game's scenario
deck list and restrictions. Do not implement a country badge that silently disagrees with the deck.

Human deck selection uses `NetworkLobbyService.ChangeClientDeck(DeckDataModel)`. The local mod's
full per-AI custom deck selection is an extension beyond the verified native preset selector.
It should stay available, but its unresolved upstream live-spawn limitation must remain documented.

## Map cards, preview and match options

The native map picker uses `LobbyMapScenarioSelectionPopup` and `LobbyMapSelectionButton`, with
a preview `Image`, name, selected/applied marker and DLC ribbon. `MapCardData` provides `Image`,
`MapPreview`, `SceneName`, `MapName`, `MapNameLocKey`, `ContentMembership`,
`IncludedInCurrentBuild` and `Scenario`. `GameConfig.Instance.MapCardsConfig.Data` contains the
cards; `ScenarioDataManager._mapCardsConfig` is another installed reference. These are existing
runtime Unity sprites, not files that need to be copied into the mod package.

Use scenario-specific preview files when present, then exact `SceneName` matches to native cards.
Prefer tactical `MapPreview` to scenic `Image`, label a scenic fallback honestly, and preserve
shared sprite ownership. Rebuilding a local screen must not destroy the game's shared sprites.
Native `MapDrawerPopup` also has a map preview, objects container and map-related tools; existence
of those components alone does not establish a complete standalone offline planning-map service.

Native `LobbySettings` owns a map/scenario selector, preview, global difficulty controls,
`_publicOptionsWindow`, private-lobby and mirror-match controls. `LobbyPublicOptionsWindow.Init`
accepts the scenario metadata and current UID/value pairs. `ScenarioPublicOption` supplies order,
name/description, default, integer dropdown values, range, step and slider declaration.

In the installed Airbase metadata the public options are size UID 180 (Auto/Large/Small), daytime
UID 221 (Evening/Day/Night/Random), timer UID 226 (30–60, step 3, default 30) and income UID 227
(150–600, step 25, default 300). The random daytime value 3 reuses a nation-related localization key;
the local label fix must leave the value unchanged. Units are not declared by those metadata fields.

`LobbyOptions` also carries `StartingPoints`, `StartingIncome`, `FightFormat`, `AllowMirrorMatch`
and `Difficulty`. Their existence does not prove the offline scenario reads every field. Preserve
the working `ISceneLoadManager.ScenarioPublicOptions` path for supported scenario settings instead
of presenting every network option as an effective local control.

No native named whole-match preset facility was established in this audit. The local named setup
feature can remain as an explicit local convenience; do not confuse it with native AI profile presets.

## Prioritized parity matrix

| Priority | Feature | Native evidence | Offline implementation and proof needed |
|---|---|---|---|
| P0 | Both teams and every existing slot visible | Official manual; `TeamPanelView` | Metadata projection, human identity protected, direct row callbacks bound to UID/object. |
| P0 | Real map preview and map-card selection | `MapCardData`, native map buttons | Borrow native sprites by exact scene; use local preview files; verify ownership and empty states. |
| P0 | Deck selection on each playable row | Human deck and AI profile row controls | Keep local custom deck extension; verify each chosen UID reaches actual spawns. |
| P0 | Closed/active optional AI rows | Slot states plus add/kick/lock handlers | Persist enabled state; prevent all selected closed-bot spawn paths; respect required slots; live-test 1v1/asymmetric setups. |
| P0 | Clear launch validation | `ValidateScenarioSlots`, ready APIs | Validate local plan and display a concrete reason. Do not claim server validation is reused. |
| P1 | Native profile names, countries and spec badges | `AiProfilePreset`, `AiConfig`, force settings | Reuse runtime assets; filter by installed/scenario content; map profile to the actual local unit/deck path before enabling profile changes. |
| P1 | Difficulty and scenario options | Publisher update; native global toggle/options window | Keep one verified global difficulty; accurate dropdown/range controls; preserve UID/value semantics. |
| P1 | Auto-fill/all-close and small-match presets | Native initial-bot setup and slot controls | Compose a valid plan from existing optional slots; protect human/required slots; do not alter scenario assets. |
| P1 | Native-like graphical hierarchy | Manual screenshot; installed native image components | Two force panels, compact row controls, map card, restrained color, consistent focus/hover/disabled states; verify scaling in game. |
| P2 | Team switching and additional human slots | Native change-slot handler | Human/team/session assumptions and scripts require a separate investigation; no arbitrary extra local humans. |
| P2 | Private/public lobby, password, invite, ready clients, spectators, matchmaking | Network service/state models | Network concerns; no useful offline parity from cosmetic copies. Exclude from the offline flow. |
| P2 | Per-bot difficulty, arbitrary starting resources or victory rules | No verified local execution path | Keep unavailable until a real underlying game API/script path is demonstrated. |

## Actionable implementation boundaries

1. Maintain a separate local launch plan keyed by existing scenario identity and player UID.
   Do not mutate original `meta.json`, serialized mission files or shared native configuration assets.
2. Treat an optional bot's active/closed selection as battle behaviour. Deck assignment and scripted
   spawn replacement are distinct hooks; suppressing one cannot establish the other is suppressed.
3. Close/restore controls must preserve the human slot, mandatory slots and source ownership.
   Keep non-configurable metadata rows visible and clearly disabled. Revalidate remembered plans
   against changed metadata on every open/map change.
4. Borrow native visual assets and localization where safely available, without registering the
   full native network lobby as a side effect. `LobbyScreen.InitServices` and row handlers reference
   authorization, lobby and room services; a prefab transplant is not a complete offline adapter.
5. Test the plan logic and native-hook registration separately. Then test real game launches:
   default setup, 1v1, uneven teams, restoring a closed bot, changed presets, restart and return to menu.
   Observe actual spawned owners/units and objectives; a UI checkbox is not runtime evidence.

## Source provenance

Primary external references are linked at the claims they support. Additional publisher release
context: [Slitherine's Update 1.08 media alert](https://www.gamespress.com/en-US/Slitherine-Announces-Update-108-for-Broken-Arrow)
describes selectable small/large layouts and auto-sizing for smaller-than-5v5 matches. This is
historical context; the installed scenario's actual options remain the local authority.

Installed metadata inspected: `C:\Games\Broken Arrow\BrokenArrow_Data\StreamingAssets\SharedMissions\*\meta.json`
for all 22 type-3 skirmish scenarios. API members were inspected in the locally generated
`MelonLoader\Il2CppAssemblies\Il2CppBrokenArrow.dll` and `Il2CppNetworkCommon.dll` with metadata
reading only. No native gameplay methods or network API calls were executed for this audit.

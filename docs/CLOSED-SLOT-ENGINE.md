# Closed-slot runtime investigation (installed v1.2.0)

This investigation reads generated metadata, preserved Cpp2IL address attributes, native instructions
and scenario data from the user's installed game. It does not modify or redistribute game binaries,
scenario archives, decrypted scripts, assets or decoder keys.

Reference executable SHA-256 (`GameAssembly.dll`):
`B1C20BDBF1A44A3093CCEC1EA8F578CF36DDA177BB5D5A78C362E96059710CF3`.
The addresses below identify this build only; production code uses named managed Harmony targets,
not hard-coded executable addresses.

## What native online closure means

`LobbySlotStatus` is `None=0`, `Free=1`, `Occupied=10`, `Locked=20`, `Inaccessible=30`,
`OccupiedByBot=40`. A closed lobby slot is distinct from an open slot awaiting a player.

Native `ScenarioController.LoadTeamsAndPlayers(GameSessionContext sessionContext)`, RVA `0x6F6360`,
iterates scenario `TeamData.Players`, constructs `PlayerInfo` objects, and inserts them directly into
the session dictionary. It does **not** use `GameSessionContext.AddPlayer`. Hooking AddPlayer alone
would miss this path.

The online branch sets runtime `PlayerInfo.SlotStatus` to bot-active (40) only when the corresponding
network lobby slot is occupied by a bot. Other runtime player objects remain present with their UID
and default non-bot-active status. Therefore deleting dictionary entries/renumbering UIDs is not the
native closed-slot model and would break scenario data-link identities.

The offline branch instead marks every `PlayerInfo.IsBot` as bot-active. It does not consume a
network closed-slot mask. Changing `ScenarioSlotMeta.Locked` alone cannot fix that branch.

## Why the cached bot flag matters

`PlayerInfo` constructor RVA `0x30FFA0` copies `PlayerData.IsBot` to runtime `_isBotSlot`.
The verified `PlayerInfo.IsBot` getter (RVA `0x310190`) is equivalent to:

```text
_isBotSlot && NetID == 0
```

It does not read `SlotStatus`. `IsSlotLocked` (RVA `0x3101B0`) reads the shared scenario
`PlayerData.Locked`; that is a separate property, not the network lobby's runtime closed-slot mask.

`NodeGetSlotData.GetSlotNetwork` (RVA `0x2D0210`) computes its script `IsAi` output as the network
slot's `IsBot && Status == OccupiedByBot`, copying the bot preset ID separately.
`NodeGetSlotData.InitSolo` (RVA `0x2D0550`) resolves the same player identity from the session,
then computes `IsPlayer = NetID > 0`, `IsAi = PlayerInfo.IsBot`. If both are false it exposes no
active player/bot name. Setting SlotStatus alone would leave solo `IsAi` true.

The narrow bridge therefore keeps the runtime player object/UID but sets `_isBotSlot=false` and
`_SlotStatus_k__BackingField=None` for selected optional AI slots. No scenario `PlayerData`, team
lists, dictionary membership or original files are mutated. Human/current-player, mandatory and
non-bot rows are protected. Calling the bridge repeatedly is harmless.

## Correct hook timing

`ScenarioController` constructor calls `LoadTeamsAndPlayers` at native RVA `0x6F85C1`.
The subsequent `ScenarioController.<Load>d__30.MoveNext` later calls `NodeController.InitNodes`
at RVA `0x6FA7D1`. A **postfix on LoadTeamsAndPlayers** can therefore apply closed states after
the runtime player objects/human selection exist, before solo slot-data nodes initialize.

The caller must scope this postfix to the prepared local skirmish plan and matching scenario,
including restart handling. It must not apply stale choices to campaign, editor, other scenarios
or online battles. `SlotRuntimeBridge` intentionally does not install a global hook itself.

The early `ScenariosService.ActiveScenario` identity guard is available on this build:
`ScenarioDataManager.Load` (RVA `0x2075F0`) directly assigns the passed scenario to the exact
static ActiveScenario field at `0x2076C4`, before reading the archive. The constructor calls that
loader at `0x6F8508`, then LoadTeamsAndPlayers at `0x6F85C1`, then LoadPlayersDeck at
`0x6F85D6`. This field identity was checked against native ActiveScenario getter (`0x230560`)
and setter (`0x230840`); it is not an assumption based on callback names.

## Scenario evidence

The existing all-mission inventory reports these node counts for all 22 installed dedicated skirmish
maps: 120 `slData` nodes each, except Dam with 125; 293–303 `spawnMultiUnits` nodes; no single-unit
`spawnUnit2` nodes and no Lua nodes. Fresh read-only decoding of each map's `objects.bam` also
found zero preplaced `MEObjectTypeEnum.Unit` objects (enum value 1).

A representative verified graph connection in Airport: slot-data node `219717` (`A1`) sends its
`ai` output to AND node `219730` inputs `inA` and `inB`; A2 node `219720` similarly feeds AND node
`219731`. Thus the solo IsAi result controls actual scenario flow. Disabling the runtime cached
bot flag before node initialization closes these AI branches rather than merely hiding wave units.

A launch-scoped empty-wave safeguard in the existing multi-unit spawn override can still protect
against a custom scenario that spawns without consulting slot-data. That safeguard is secondary;
it must not be described as the sole implementation of native slot closure.

## Limits and tests

`SlotRuntimeBridge.ApplyClosedSlots(session, closedUids, expectedHumanUid)` returns the number of
optional runtime AI rows changed. It preserves UID/object identity, human and mandatory rows,
shared scenario data, and unselected players. The managed regression harness has 14 checks.

The native API/branch trace and graph evidence establish a viable implementation path, but they
do not replace a live match test. Validate 1v1, asymmetric teams, no allied bots, restart, switching
maps and returning to the menu. Confirm both unit absence and scenario completion/win conditions.
Arbitrary Workshop/PvE scripts can ignore slot-data or require specific players; they need their own
validation. No live in-game behavior is claimed by this research or its managed stand-ins.

Native bot preset support is a separate bridge: online slot-data copies BotPresetId and returns
the profile country/specializations; solo country/spec request methods consult player deck data.
Closing runtime players does not automatically implement native preset selection or per-team rules.

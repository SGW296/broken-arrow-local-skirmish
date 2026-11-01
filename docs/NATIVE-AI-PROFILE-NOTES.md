# Native AI profiles in offline skirmish — installed v1.2.0

## Result

There is a real native-army selection path for the 22 installed built-in skirmish scenarios.
It does **not** require creating a pretend full deck from a country/specialization label.
Their graphs read `NodeGetSlotData.AiPresetId` and select the corresponding existing scripted
unit pools. A narrow, launch-scoped postfix on `NodeGetSlotData.InitSolo()` can supply that ID
after the game resolves the slot to a runtime player.

The selected-profile slot must bypass the mod's full-custom-deck wave replacement. Otherwise
the native graph would choose its army, then the mod would replace those units with deck units,
defeating the native profile selection.

This is a verified static/native/graph integration path, **not a completed live-game test**.
Observe actual unit owners, chosen armies, upgrades/transports, restart and match completion in game.

## Native evidence

Reference `GameAssembly.dll` SHA-256:
`B1C20BDBF1A44A3093CCEC1EA8F578CF36DDA177BB5D5A78C362E96059710CF3`.
RVA values below identify this installed binary only. Production code uses named Harmony methods,
not hard-coded addresses.

| Native method | RVA | Verified behaviour relevant to profiles |
|---|---:|---|
| `NodeGetSlotData.GetSlotNetwork()` | `0x2D0210` | Copies the network slot's `BotPresetId` to node `AiPresetId`; resolves profile country/spec fields from `AiConfig.AiProfilePresets`. |
| `NodeGetSlotData.InitSolo()` | `0x2D0550` | Resolves `_slotSoloPlayer`; does not copy an online bot preset ID. |
| `NodeGetSlotData.OnRequestValueAiPreset()` | `0x2D12A0` | Returns the boxed `AiPresetId` field. No deck lookup or network request is needed to return this value. |
| `OnRequestSoloValueCountry()` | `0x2D0ED0` | Consults assigned player deck data rather than trusting the node's country field. |
| `OnRequestSoloValueSpec1()` | `0x2D1020` | Consults `SharedPlayerDeck.TryGetDeckHelper` and assigned deck specifications. |
| `OnRequestSoloValueSpec2()` | `0x2D1160` | Uses the corresponding assigned-deck specialization path. |
| `DeckService.CreateDeck(...)` | `0x6CF200` | Creates a model with name/country/spec identities and category slot structures. This is not an identified native AI army generation API. |
| `DeckService.GetDefaultDeckByCountryId(int)` | `0x6D0420` | Looks up the country's existing default model in a dictionary; it does not select a profile-specific pair of specializations. |

Setting only country/spec fields on a solo slot-data node would be ineffective for its solo
country/spec request methods. Setting the actual `AiPresetId` is different: that request method
returns the field directly, and the built-in graphs use it.

## Graph evidence across all 22 built-ins

Fresh, read-only, in-memory inspection of each installed `nodes.bam` found:

- Each map has 120 `slData.prId` output connections into equality comparisons: profile IDs
  `0` through `11`, each tested ten times across the ten player slots.
- The only connected slot-data output ports are `ai` and `prId`. Country/specification outputs
  are not connected in any of these 22 graphs.
- The `ai` outputs drive active-bot branches. Dam has five additional slot-data nodes but the
  same 120 profile comparisons and the same profile-ID set.

One concrete Airport trace connects selection to real unit data, not just a visible profile label:

1. Slot-data node `543773` (`B1`) supplies `prId` to equality comparison `543775` against ID 0.
2. Its true flow activates named mission-storage relay `543841`.
3. A matching named relay `561107` opens gate `561117`.
4. That gate activates random-data selector `561127`.
5. The selector's `outDt` feeds storage `561131`; matching relay `562574` supplies its `out`
   directly to `spawnMultiUnits` node `562575`'s `UnitInfo` input.

This proves the profile selector reaches a native scripted spawn pool. It does not establish
every behavioural difference between profiles without observing a match. Original UnitInfo data
is left intact in native-profile mode, preserving the script's own chosen unit modifications,
skins and transport/cargo definitions instead of substituting custom deck cards.

No decoder key, decrypted mission script, unit pool or game binary is included in the mod source
or package. Only these small structural facts and node identifiers are recorded.

## Helper APIs and integration contract

`NativeAiProfiles.GetAvailable(ScenarioSource)` returns `NativeAiProfileChoice` objects containing
the native ID, localized name (technical-name fallback), country/spec IDs and borrowed avatar/spec
sprites from the loaded `GameConfig.Instance.AiConfig.AiProfilePresets`. IDs outside the verified
0–11 range are excluded. The catalog does not create or modify decks, presets or entitlement data.
It follows loaded native configuration; country/scenario filtering belongs in the lobby's plan/UI.

`NativeAiProfiles.IsSupportedScenario` restricts the feature to:

- `ScenarioType.Skirmish`, game version `1.2.0`, author `Steel Balalaika`;
- one of the 22 audited original skirmish folder names, directly inside the game's installed
  `BrokenArrow_Data/StreamingAssets/SharedMissions` directory;
- a non-Workshop source.

This is a conservative identity check, not a file-integrity hash check. If the original scenarios
have been edited, their profile wiring needs revalidation. Future game versions and arbitrary
Workshop/PvE/campaign graphs must not inherit this compatibility claim.

`NativeAiProfileBridge.ApplyPreset(node, session, UID-to-profile dictionary, protectedHumanUid)`
uses the already resolved `node._slotSoloPlayer.UID`. It checks native pointer identity against
`session.GetPlayer(uid)`, protects the current human, rejects inactive/closed/non-bot/network-player
nodes, and accepts only planned profile IDs 0–11. It changes only `node.AiPresetId`.
It does not guess UID from slot ordinal or reassign players, teams or native data ownership.

The caller must:

1. Scope the `InitSolo` postfix to an active matching local skirmish plan/session, including restart.
   Merely checking a pending-load flag is insufficient if that flag is cleared before node initialization.
2. Persist profile IDs separately from deck identities, with an explicit native-profile/custom-deck mode.
3. Revalidate saved profile IDs against the current scenario/catalog and country restrictions.
4. Keep selected native-profile UIDs out of both custom deck assignment and wave replacement.
   Keep the closed-slot safeguard before this mode check.
5. Retain the established human deck preload path. Do not mutate shared default decks or profiles.

The helper regression harness checks UID mapping, pointer/session scoping, human/closed protection,
valid-ID boundaries, supported scenario/catalog restrictions and borrowed artwork. It uses managed
stand-ins and cannot verify actual native node initialization or a live match's script execution.

## Required live checks

- Choose two visibly different native profiles for separate active AI slots; verify each actually
  produces its corresponding scripted army, not the human deck or another slot's army.
- Mix native-profile and custom-deck AI slots, then change the human deck. Each mode must retain
  its own source. Confirm custom deck units still preserve the existing selected upgrades/transports.
- Close/reopen a profile slot, use a 1v1 format, switch map, load a preset and restart a battle.
  Closed bots must remain closed and stale profiles must not leak into other scenarios.
- Return to the menu/campaign and confirm the local profile patch no longer changes slot data.

Related evidence: [native lobby audit](NATIVE-SKIRMISH-AUDIT.md) and
[closed-slot engine investigation](CLOSED-SLOT-ENGINE.md).

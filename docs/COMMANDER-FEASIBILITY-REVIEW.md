# Commander feasibility review

Read-only planning review. No implementation or deployment is authorized by this report itself.
The current local skirmish opponent is script-directed, with real conditional behavior; a general
deck/resource/battlefield commander would be a separate control layer, not a cosmetic setting.

## Highest-value near-term work

1. **Separate behavior profile from full-deck selection.** The air investigation identifies an exact
   bottleneck: custom-deck substitution leaves native preset 0's tank-focused request plan in place.
   An explicit air-capable behavior choice can reuse existing conditional air scripts while choosing
   units from the user's deck. Verify category and role, not merely that a plane appears.
2. **Add useful match diagnostics and controlled comparisons.** Record requested category/role,
   chosen replacement, profile, slot UID, request/dispatch outcome and observed ownership. Include
   helicopter, fighter/bomber and transport cases. Current sparse wave logging cannot diagnose all
   air behavior. Do not tune aggressiveness blindly without knowing which path is active.
3. **Stabilize the playable lobby and native army modes.** Test 1v1/asymmetric formats, per-row state,
   presets/restart and native profiles on representative maps. Native-looking presentation should
   clearly distinguish a native scripted army from a custom deck plus behavior profile. Prevent a
   selected army silently falling back to another one after an initialization failure.

These are useful deliverables even if a later general commander proves too costly. Their schedule
depends on live validation; they should not be promised as fixed numbers of days.

## The critical unknown is legitimate purchasing

The installed v1.2.0 metadata exposes real economy/ordering systems, but not an established AI-buy
front door:

- `ShopService.TryOrderUnit(slot, asTransport, infantryCase, isCargo, amount)` has **no buyer UID**.
  It holds the session and a `_playerDeck`. `OrderValidator` caches `_curPlayer`; `ShopOrdering`
  also holds the session. Calling the human shop for an AI may spend the human's money or affect
  their queue. Temporarily switching the global CurrentPlayer is not a sound commander design.
- `PlayerEconomyService` has per-player `ApplyNewUnitInDeck`, `ApplyRemovedUnitFromDeck`,
  `ApplyNewDeck` and `AfterUnitDeadOrRefund`, with player-ID arguments. These show relevant
  infrastructure exists; they do not prove that any one function performs a complete legal purchase.
- `SpawnUtils` exposes valid/nearest/best spawners by unit type and player ID. A spawn location
  helper is not a purchase, card-availability check or wallet debit.

**Go/no-go experiment:** for one AI UID, order one small ground unit through a traced actor-correct
native order path. Observe buyer, deck identity, price/debit, queue, arrival and card availability.
Insufficient funds must reject it; cancellation/refund, death and any card replenishment must behave
as expected. Then wait for actual income/upkeep and verify a later order becomes affordable.
The human wallet, deck and queue must remain unchanged. Repeat after restart.

Until this passes, call the project a commander prototype, not proven deck-and-resource AI. A custom
shadow budget plus raw scripted spawning could demonstrate planning, but would be a different mode
with its own economy rules, not verified native purchase parity.

## Other go/no-go experiments

**Exclusive control of one slot:** keep its native player identity and economy, but stop its stock
purchase/spawn/order branches. Closing the slot's cached bot flag was correct for an empty slot;
it is not automatically correct for a living AI controlled by a new commander. Confirm no stock
wave, wallet-setting node or unit-group order overrides the new controller. Start/stop/restart must
not leave two controllers competing or affect the human/other slots.

**Fair perception:** the engine world contains hidden enemy data. `FogOfWarHelper.IsHiddenForTeam`
exists, as do fog components and detected-unit groups, but a fair AI-specific observation adapter
has not been proven. Test that an enemy completely hidden from the AI's team cannot influence its
decision; then reveal, lose contact and verify only legitimate last-known information remains.
Distinguish public objective ownership from enemy unit positions. Log each contact's source.

**Orders and pathing:** prove that a newly purchased AI unit can accept a native move/capture/attack
order, reaches a valid objective and fights under ordinary engine rules, without the human selection
or command queue changing. Unreachable objectives must cause replanning, not endless command spam.

**Performance and lifecycle:** measure native-call count, decision cost and command volume. Cache
observations, use a bounded decision tick and issue commands only when intent changes; avoid scanning
every enemy and issuing every order each rendered frame. Establish frame-time budgets on the user's
hardware before multiplying the controller across ten slots. Test destruction, pause, speed change,
retreat, restart and return to menu. Unity/IL2CPP objects must remain on appropriate engine threads.

## A bounded first commander

One installed map, 1v1, one controlled AI UID, a small ground-only deck and known reachable capture
zones. Keep ordinary combat/pathfinding in the engine. The controller maintains a simple objective
score, known threats, own force mix and reserve. It legally buys affordable units, groups them,
chooses capture/defend/retreat intents and reacts to losses. Every purchase and order has a reason
trace. Use deterministic utility/rule scoring in the game loop, not a real-time language model.

Success means reproducible lawful purchases, honest perception, sole command ownership, a complete
match and acceptable performance. A win against the stock AI alone does not establish those things.
Only then expand decks, maps, logistics, artillery, air roles and coordination across multiple bots.

## Planning horizon and exit criteria

A bounded ground commander is plausibly a **weeks-scale experiment after the critical interfaces
are proven**. A broadly reliable opponent across maps, decks, logistics, aircraft and multiple teams
is a **months-scale project**, with uncertain iteration and balance costs. That is a scope judgment,
not a delivery commitment. Existing engine services make it plausible; we have not established a
ready-made commander API.

Stop or redesign if purchases require hijacking global human state, hidden information cannot be
reliably filtered, stock control cannot be isolated without breaking objectives/victory, or acceptable
performance requires fragile version-specific native patches. The useful lobby/profile improvements
remain independent of that decision.

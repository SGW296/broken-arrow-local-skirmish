# Broken Arrow: offline skirmish versus AI

Planning baseline: 1 November 2026. Target: installed Broken Arrow v1.2.0 with the existing
offline MelonLoader setup. Scope is skirmish versus AI; campaign, PvE missions and online play
are outside this project unless explicitly added later.

## Recommendation

**Agreed active scope (1 November 2026):** polished lobby and better scripted AI first. The full
commander is deferred. The first live 3.0.0 Airbase/Hard 1v1 smoke test passed on 1 November:
the user reported the setup worked and the enemy fielded a jet. Ground/air reinforcement pressure
was overwhelming. Preserve that baseline, investigate wave ownership/scaling before tuning pacing,
and continue targeted live acceptance rather than treating one short match as broad validation.

Keep two delivery tracks and one shared test foundation:

1. **Offline lobby:** a clear, attractive, dependable RTS setup and launch experience.
2. **Better opponents:** first improve the existing scripted AI, then investigate a separate
   commander that owns decisions, resources and a deck.

The stable lobby and useful scripted-AI improvements should remain worthwhile even if a fully
general commander proves too costly. A commander prototype must not replace the working stock
mode before it is demonstrably useful. This document proposes sequencing and acceptance gates;
it is not a commitment to months of unattended implementation or a fixed delivery date.

## What we have now

### Agreed balance target

**Normal** is the reference difficulty: a good, balanced match for a competent campaign-complete
player. Tune Normal first. The engine's value 2 is named Medium; relabelling it Normal preserves its
existing meaning and does not itself rebalance the scripts. Easy/Hard may intentionally vary pressure
or advantages, with their effects described when measured. Normal's target is a contest where sound
composition and decisions matter, pressure is recoverable, air has counterplay, and unavoidable
reinforcement volume is not the main source of challenge. This is a quality target, not a promised
50% win rate for every deck/map or a claim of currently identical resource rules.

The first Normal iteration is a measurement build: default new/reset skirmish setups to Normal,
preserve explicit saved difficulty choices, log native/custom/closed requests and forwarded payloads,
and record runtime player flags and requested option values. Then run the same Airbase 1v1 setup
on Normal before changing pacing. Investigate ownership/scaling issues before tuning reinforcements.

- Local lobby 3.0.0 passed its first live smoke test. Source is based on the MIT-licensed Local Skirmish mod.
  The 3.0.1 Normal measurement build adds diagnostics and clearer defaults; live acceptance is pending.
- Visible teams, closed optional slots, match formats, native presets, custom decks, nation filters,
  previews and saved setups are implemented. A native-inspired graphical overhaul is implemented.
- The 3.0.1 build and 679 managed checks pass, plus the managed Harmony registration fixture.
- **First live smoke test passed:** Airbase/Hard 1v1, native AI units and a jet observed, eight runtime
  closures logged. Broad visual/match/restart acceptance remains incomplete; 3.0.0 remains a candidate.
  See [the live test record](docs/LIVE-TEST-3.0.0.md). Fair resource rules have not been established.
- The missing-aircraft investigation found a concrete custom-deck limitation: preset-zero's
  behavior can suppress air requests even when the deck contains aircraft. Native preset mode
  and custom-deck mode must not be presented as interchangeable.

Existing research: [native lobby audit](docs/NATIVE-SKIRMISH-AUDIT.md),
[air diagnosis](docs/AIR-AI-INVESTIGATION.md),
[current validation](docs/VALIDATION-3.0.1.md), and
[commander feasibility review](docs/COMMANDER-FEASIBILITY-REVIEW.md).

## First development window: approximately 3–5 focused working days

These are working-session targets with prompt game feedback, not guaranteed calendar dates.
Failed acceptance tests take priority over the next planned feature. UI and read-only AI research
can overlap; experimental behavior must not obscure an unverified launch baseline.

| Milestone | Target window | Deliverable | Evidence required to finish |
|---|---|---|---|
| M0 — Trustworthy baseline | Day 1 | Verify the installed lobby, army choices, closed slots and lifecycle | Complete a known-map 1v1; verify actual owners/armies and eight closed slots. Repeat with mixed native/custom armies, restart, return to menu and map change. Save correlated logs and screenshots. |
| M1 — Clear setup and recovery | Days 1–3 | Explicit army/behavior labels, visible launch summary, reliable presets/search/cancel and usable error handling | UI choices match the actual battle; no silent army substitution. Test missing decks/profiles, corrupt settings, hook failure and failed launch. Timer/income labels must reflect traced and observed effects. |
| M2 — Custom armies use intended capabilities | Days 2–4 | Separate behavior profile from custom unit deck; diagnose and restore supported air roles | An air-capable behavior requests appropriate helicopters/aircraft from the chosen deck. Verify owner, role/loadout, orders and return/rearm behavior. Check airlift separately; never silently turn an intended airlift into a ground transport. |
| M3 — First visibly better AI release | Days 4–5, or next window if M0–M2 slip | One measured improvement to pacing/composition, based on baseline matches | Record a repeatable before/after setup. Candidate: group reinforcement timing or repeated air-sortie pacing. Adjust the request/controller layer rather than blindly scaling all spawns. Keep a stock comparison mode. |
| M4 — Lobby release candidate | End of window or next short window | Thumbnail map browser, finished interaction/scaling, consistent visuals and diagnostics | Targeted checks on three representative maps and 1v1/2v2/5v5/asymmetric setups. Check 720p/1080p/1440p/ultrawide, long names, fallback assets, Escape/Tab/Enter, focus, Alt-Tab, restart and rollback. Freeze feature expansion while fixing failures. |

M3 is deliberately one change, not a promise to fix every stock tactical weakness in a day.
If the recorded baseline shows missing capabilities are the dominant problem, completing M2 is
the first AI-quality release; pacing waits. Low-ammo recovery, resupply and retreat are strong next
candidates only after we observe which stock routines already work and which actually fail.

### What “shipped quality” means for the lobby

- Choosing an army means that army appears; closing a slot means it stays closed.
- Native scripted presets, custom battlegroups and behavior choices have distinct, accurate labels.
- Maps are recognizable and searchable; unsupported configurations explain their limitation.
- Settings have meaningful names, units and help, rather than exposing unexplained engine values.
- Nation/filter changes explain any army replacements. Cancel leaves previous choices intact.
- Presets survive a full game restart and recover visibly from missing content.
- Start is enabled only for a valid plan, and failure does not silently launch a different plan.
- Keyboard/mouse focus, top-modal Escape, scale, long text and absent artwork behave consistently.
- Version, map, slot/army mode and binding/spawn outcomes are recorded compactly for diagnosis.

This is a quality target for the pinned offline PC setup, not a claim of publisher-scale QA across
all hardware/game versions. A visually faithful skin alone does not meet it.

## Commander feasibility: credible, but gated

The engine already provides units, combat, navigation, capture mechanics, money and deck data.
We should reuse those. The new work is an operational decision layer, not new pathfinding,
weapon AI, model training or a language model issuing orders during a match.

The essential loop would observe legitimate information, assess objectives/threats and own force
needs, choose an affordable purchase or order, execute it, and reassess after events or a bounded
decision interval. It must keep an intention long enough to avoid issuing contradictory orders.

### Bounded feasibility experiment before a large commitment

Use one map, 1v1, one controlled enemy, and a small ground-only test deck. Cap the initial
investigation at roughly two or three development/playtest sessions, then record what is proven,
what failed and whether another short session is justified. Do not silently extend this into an
open-ended native-code expedition.

| Gate | Required live proof | If it fails |
|---|---|---|
| Resources and deck | One AI buys a known unit; funds and availability decrease correctly. Insufficient funds prevent it. Income, transport cost, cancellation/refund, deaths/redeployment and deployment limits are accounted for. Human funds/deck/queue stay unchanged. | Trace an actor-correct native purchase path first. An explicitly defined AI economy is a possible alternative, but needs reservations before spawning, failure refunds and measured inventory/cost parity. Raw free spawning plus an unexplained display counter is not success. |
| Exclusive control | Only the selected AI's stock spawning, money-setting and order loops stop. The commander buys and orders units without stock scripts overwriting them; objectives/victory and other players remain intact. | Redesign the adapter or stay with improved scripted AI. Closing a living commander slot's bot identity is not a substitute for isolating its old controller. |
| Fair information | Hidden enemies cannot influence choices. Revealed and lost contacts produce legitimate team-specific knowledge/last-known state; public objective information is handled separately. | Do not advertise fair intelligence. Restrict the prototype to verified observations or stop the fairness claim. |
| Orders and lifecycle | A bought AI unit moves, captures and fights without altering human commands. Death, unreachable objectives, restart, pause and return to menu are handled without command spam. | Fix the adapter before adding strategy. |
| Performance | Decision time, engine calls and command count remain bounded on the user's hardware. | Reduce scope/tick frequency and cache observations before adding more AI slots. |

The native human shop currently caches the current player and does not expose an established
buyer-UID entry point. Therefore “just call the normal buy button for the bot” is not a proven
solution. Temporarily hijacking global human-player state is not an acceptable foundation.

### First useful commander

After the gates pass:

1. Maintain an actual reserve, income, committed purchases and deck availability.
2. Score reachable objectives using ownership, distance, known threats and friendly coverage.
3. Buy a small set of complementary roles: recon, infantry, armour, AA and basic support.
4. Assemble and commit a group; capture/defend, react to losses and withdraw when appropriate.
5. Record a short reason for each meaningful purchase/order. Log state changes, not every frame.
6. Complete repeated matches against a fixed baseline under matched resource/information rules.

Initial success is a coherent, fair, functioning opponent, not maximum win rate. Do not make the
prototype look clever by reading hidden units, spawning unpaid units or receiving extra resources.
Keep normal tactical combat and navigation in the engine.

Later stages add supply/retreat, artillery, air roles, transports, multi-bot coordination and more
maps. Each adds a meaningful validation surface; adding all of them before the first complete
ground match would multiply failure causes.

## Horizon and cost

Engineering planning ranges, assuming regular development and live feedback:

- **Lobby and targeted scripted-AI improvements:** useful deliverables in days; polish and broad
  validation may need further iterations.
- **A credible one-map ground commander:** approximately **4–8 weeks after the interface gates
  pass**, with scope reduction if integration is difficult.
- **Robust combined-arms AI across many maps/decks/teams:** **3–6 months or more**, with no
  guarantee of publisher-level strength or polish. Part-time playtesting stretches calendar time.

These are estimates, not delivery promises. A full general opponent is not a reasonable “few days
and a small token spend” promise. A narrow prototype and incremental player-experience improvements
are reasonable experiments.

No reliable token total can be quoted now. Debugging across an unproven game interface can consume
many iterations. Keep cost bounded with one issue per build, one or two known test maps, compact
diagnostics, durable notes and a stop/review at each gate. More generated code does not replace a
controlled game test. The running AI should use ordinary local decision code, so it does not require
language-model inference or token spending for each match.

Existing research reduces discovery risk, but is not a completed reusable commander. The external
[BrokenArrowAI status report](https://github.com/seaborndan/BrokenArrowAI/blob/master/docs/STATUS.md)
reports in-game reinforcement/economy experiments while marking much tactical decision code as
not live-validated. We have not reproduced those claims on this installation. No explicit reusable
software license was established; code reuse/publication permission must be resolved before relying
on a derivative. Research informs the design; this plan does not assume an effortless code transplant.

## Working rules

- Keep a stable stock/native skirmish mode and a reversible installed build.
- Keep UI/setup logic separate from game adapters, AI decisions and diagnostic records.
- Human units and original game files remain protected. Experimental scenario work uses separate copies.
- Controller ownership is explicit per AI; do not let two planners issue competing orders.
- A passed managed test is not a passed live-game test. Record both separately.
- Build, back up, install and verify updates for the user, then report when the game is ready to launch.
- Update this roadmap from evidence; do not add approval gates to routine authorized fixes.

**Recommended next action:** preserve the first live baseline, add compact native-wave/active-slot
diagnostics and finish targeted acceptance. Then improve behavior/custom-deck routing and measured
pacing. Keep the commander experiment bounded until its foundation passes.

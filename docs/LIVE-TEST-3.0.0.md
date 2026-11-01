# First live 3.0.0 smoke test

Reported 1 November 2026. Installed DLL matches the verified 3.0.0 package. The test log and
configuration were preserved under the workspace's
`outputs/Live-tests/3.0.0-Airbase-1v1-20261101/` before another launch could replace Latest.log.

## What passed

The user reported that the requested setup checks worked and the enemy produced ground units and
a jet. This supports the lobby/native-preset execution path and confirms an observed aircraft in
the running game, beyond static analysis and managed tests.

The archived log confirms:

- Airbase, Hard, human UID 2 using US_DEFAULT_DECK.
- One native AI preset plan: UID 5, preset ID 1; zero custom-deck AI plans.
- Eight closed slots requested; eight runtime closures applied before graph initialization.
- The human deck override and four scenario option values were applied.
- Approximately 5m12s from battle-plan binding to end-battle override detachment.
- No Local Skirmish runtime exception recorded during this launch/match.

## Reported player-experience issue

The enemy produced many tanks/infantry and a jet quickly, overwhelming the human. Record this as
an aggressive Hard native opponent and a pacing/balance issue to investigate, not proof of exact
AI spending, unlimited money, equal resources or a closed-slot defect.

Native waves were not logged exhaustively by owner/category. Therefore the log proves the applied
closed-slot state, not a forensic count of every spawned unit. The user's short test also does not
establish complete victory handling, restart correctness, every map/resolution or long-match stability.

Airbase has explicit slot-data-based size/economy logic. Retained runtime player identities do not
by themselves imply 5v5 scaling; the full active-count/math path has not yet been validated.

## Next work

1. Preserve this as the first positive live baseline; broader acceptance remains open.
2. Add compact native-wave diagnostics: owner, category/count, timestamp, closed-owner suppression,
   map-size selection and effective active-slot counts. Confirm no unintended scaling/leakage.
3. Compare equivalent difficulty/setup cases, then tune measured reinforcement pressure.
4. Continue separating custom deck from behavior profile and polishing setup/recovery.

Short-term pacing limits can improve the match without a full commander. Real budgets/card limits
are a separate economic enforcement task; intelligent spending is a later commander task. Do not
present a reinforcement cooldown as proven human-equivalent resource accounting.

No production or installed files changed during this follow-up.

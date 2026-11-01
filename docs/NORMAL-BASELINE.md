# Normal balance reference and diagnostic build 3.0.1

Agreed 1 November 2026: Normal is the baseline for a competent player who understands the game
and has completed the campaign. Tune this reference first. Easy and Hard can deliberately change
pressure/advantages, but their eventual differences should be clear and reproducible.

## What Normal should achieve

- Sound composition and tactical decisions matter more than unavoidable reinforcement volume.
- The opening allows deployment and a useful response; losses can be recovered from.
- Enemy aircraft have appropriate opportunities and counterplay rather than constant unbounded pressure.
- Equivalent setups produce broadly consistent difficulty; map/deck variety is tested explicitly.
- Resource/deck-rule differences are measured and described. A label or cooldown is not resource parity.

This is a design target, not a claim that stock Medium is already balanced or that every army matchup
must have a 50% win rate. The previous Airbase smoke test used Hard; preserve it as a functional and
high-pressure reference rather than using it to set Normal's reinforcement limits.

## 3.0.1 changes

- Display **Normal** for native `DifficultyLevel.Medium`, integer value 2. The underlying enum/value
  and scenario rules are unchanged. Easy remains 1; Hard remains 3.
- New/reset dedicated skirmish setups default to Normal instead of the map's authored Hard default.
  Existing explicit saved/preset difficulties remain intact. Choose Normal manually for the next test
  if a previous saved Hard setup is restored. Non-skirmish authored defaults retain their old behavior.
- “Reset setup” accurately describes the mod's defaults rather than claiming native scenario defaults.
- Record `[WaveRequest]` entries after custom/closed overrides and before native activation, including
  owner UID, mode, native preset, requested/forwarded totals and categories. Cargo is included.
- Stop detailed request lines after 4,000 per battle, while continuing aggregate accounting. The
  `[WaveSummary]` at battle end/restart reports totals, omitted lines and any forwarded closed-owner units.
- Log runtime player flags and requested scenario option values. Retained player identities are not
  counted as active merely because they exist; Auto is labelled a requested size, not a resolved area.

These records do **not** confirm successful unit creation, purchase cost or resolved Auto play area.
No reinforcement cooldown, quantity, resource value, unit stat or native profile behavior was tuned.
Diagnostics are read-only and exceptions are contained so logging does not change battle cleanup.

## Next test

Use the same Airbase 1v1, human US deck and native enemy preset as the successful Hard test. Change
only difficulty to **Normal**. Keep timer/income/size unchanged; this is a controlled comparison.
Play about 10–15 minutes, then return to the menu and report:

1. Was the opening manageable, too easy or immediately overwhelming?
2. Did pressure build at a reasonable pace, and could you recover from losses?
3. Were enemy tanks/infantry and aircraft manageable with sensible counters?
4. Roughly when did the match become one-sided, if it did?

The assistant collects the local log; no upload is required. Preserve the log before the next
application launch replaces it. User feedback and request data complement each other.

## Work after measurement

1. Confirm owners, closed-slot suppression and runtime flags; investigate any scaling anomaly.
2. Establish the Normal reinforcement/request cadence and practical unit pressure on this setup.
3. Make one targeted pacing/composition adjustment if needed, then repeat the same test.
4. Check representative additional maps, army profiles and team formats before broadening claims.
5. Derive Easy/Hard variants after Normal is stable. Continue UI polish and the separate
   behavior-profile/custom-deck improvement; keep full commander work deferred.

Deployment follows the standard backup/verify workflow. Rollback in the 3.0.1 package is 3.0.0.

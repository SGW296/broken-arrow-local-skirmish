# Normal baseline 3.0.1 validation

Completed 1 November 2026. Branch `feature/normal-baseline`.

Release build: net6.0/x64 against the installed Broken Arrow v1.2.0 MelonLoader-generated APIs.
**0 warnings, 0 errors.** No original game assets, loader or launcher are changed.

| Managed regression suite | Passing checks |
|---|---:|
| Lobby callbacks, setup persistence, nation/native-profile selection | 140 |
| Team roster grouping and paging | 24 |
| Preview selection and asset ownership | 23 |
| Active/closed slot policy, protected rows and 1v1–5v5 | 65 |
| Native runtime closed-slot flags and identity retention | 14 |
| Native AI profile catalog, UID scope and borrowed artwork | 25 |
| Native style asset borrowing and unloaded fallbacks | 13 |
| Wave request/forwarded diagnostics, aggregation, cap and reset | 42 |
| Deck/spawn selection, diagnostic integration, closed waves and lifecycle | 333 |
| **Total** | **679** |

The separate Harmony registration fixture also passes, including compiled opt-out of duplicate
automatic registration, real managed dispatch, native-profile/custom-army separation, closed-slot
initialization, restart, cleanup and input restoration. These fixtures do not run Unity or IL2CPP.

Normal maps to the existing native Medium value 2. Saved numeric difficulty choices remain valid;
only new/reset dedicated skirmish setups choose Normal by default. Reinforcement quantity, timing,
resource rules and native profile behavior have not been tuned in this release.

Diagnostics read spawn requests and the payload forwarded to native activation. They include owner,
mode, selected native profile and unit categories including cargo. They do not confirm successful unit
creation, spending or resolved Auto play area. Detailed output is capped at 4,000 requests per battle;
aggregate counting continues. Read/log failures are contained, including cleanup and summary output.

## Live status and next acceptance

The prior 3.0.0 Airbase/Hard 1v1 smoke test passed; the user observed enemy ground units and a jet
and reported overwhelming reinforcement pressure. That is a historical functional reference, not
evidence that Normal is balanced. See [the preserved record](LIVE-TEST-3.0.0.md).

**3.0.1 has not yet been tested in a running game.** Repeat the same Airbase 1v1 setup, human US
deck and native enemy preset; change only difficulty to Normal. Existing saved Hard choices are
deliberately preserved. Play about 10–15 minutes and return to the menu to flush the wave summary.
Record opening pressure, recovery after losses and when either side becomes overwhelming.

Review the saved local log for hook registration, slot flags, request owners, closed-owner forwarding
and error messages. Confirm successful unit creation through live observation. Request logs alone
cannot establish equal resources or that every unit reached the battlefield.

After the controlled test, investigate ownership/scaling before choosing one pacing change. Broader
map, mixed army, uneven team, full-match completion and restart acceptance remain outstanding.
The test protocol and balance target are in [NORMAL-BASELINE.md](NORMAL-BASELINE.md).

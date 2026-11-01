# Local lobby 3.0.0 validation

Completed 1 November 2026. Branch `feature/native-skirmish-parity`.

Release build: net6.0/x64 against the installed Broken Arrow v1.2.0 MelonLoader-generated APIs.
**0 warnings, 0 errors.** No game/loader/installed-mod files were changed to build or package it.

| Managed regression suite | Passing checks |
|---|---:|
| Lobby callbacks, setup persistence, nation/native-profile selection | 140 |
| Team roster grouping and paging | 24 |
| Preview selection and asset ownership | 23 |
| Active/closed slot policy, protected rows and 1v1–5v5 | 65 |
| Native runtime closed-slot flags and identity retention | 14 |
| Native AI profile catalog, UID scope and borrowed artwork | 25 |
| Native style asset borrowing and unloaded fallbacks | 13 |
| Deck/spawn selection, closed waves, strict failure, restart/lifecycle | 329 |
| **Total** | **633** |

The separate Harmony registration fixture also passes: compiled opt-out of duplicate automatic
registration, single registration of each new hook, real managed Harmony dispatch, closed-slot
initialization, native-profile/custom-army separation, restart and cleanup.

The closed-slot/profile tests are supported by static native-code and actual shipped scenario graph
traces documented in `CLOSED-SLOT-ENGINE.md` and `NATIVE-AI-PROFILE-NOTES.md`. The managed stand-ins
do not execute Unity or the native IL2CPP backend. No live online lobby or new in-game match was
launched as part of this validation.

## Required live acceptance

1. Open the lobby at your normal resolution and check text, images, focus, hover/disabled buttons.
2. On Airbase, select 1v1. Verify you plus one enemy remain active and eight AI slots are closed.
3. Pick a visibly different native enemy army; launch and observe actual units and owner IDs.
4. Finish a match and test an uneven setup, such as you versus three enemies, and a restart.
5. Reopen the lobby/saved preset; verify closed states, team nations, native IDs and deck modes.
6. Mix native and custom-deck AI; verify each mode uses its intended unit path. Existing upstream
   custom-deck limitations remain separate from the native profile implementation.
7. Retain `MelonLoader\Latest.log` before restarting if the match fails or an unwanted bot appears.

Native AI presets are deliberately offered only on the 22 audited built-in v1.2.0 skirmish scenarios.
Workshop scripts/preplaced units and human team switching need separate investigation/live tests.

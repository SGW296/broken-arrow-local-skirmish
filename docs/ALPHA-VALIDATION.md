# Public alpha validation — 3.1.0-alpha.1

Target: Broken Arrow v1.2.0, Windows x64, existing MelonLoader 0.7.3 offline setup.
Newer game versions and online modes have not been validated. This is an experimental alpha.

## Build and automated checks

The local Release build passed with **0 warnings and 0 errors**. The release tooling passed
**694 managed assertions**, the separate managed Harmony-registration fixture, **72 public
installer/package assertions** and **7 diagnostic-collector assertions**. Tests use owned stand-ins
and temporary fake game folders; no game dependencies are bundled with the public CI or package.

The release validation report and package manifest record the actual built DLL hash, source commit,
observed managed assertion totals and Harmony registration result. Public CI runs managed fixtures
without game dependencies; it does not build the native-facing DLL or run Unity/IL2CPP.

Before release, run the local Release build and all applicable managed, Harmony, installer, package
and diagnostic fixtures. Installer/package tests must use temporary fake game folders and verify
hashes, backups, settings preservation and loader/launcher preservation. Do not test installation
against a running game.

## Historical live reference

The 3.0.0 Airbase/Hard 1v1 smoke test ran with a native enemy army and observed jet. The 3.0.1
Airbase/Normal 1v1 test ran for about 25m31s from plan binding to detach. Runtime flags showed
one human, one AI and eight inactive planned-closed identities. All 34 intercepted wave requests
belonged to the intended enemy UID. The player reported much better pressure but weak attacks.

Those logs describe requested/forwarded payloads, not every successful unit arrival, equal resource
rules or a confirmed final victory. They are not public-alpha live acceptance.

## Alpha live checks

The 3.1.0-alpha.1 candidate was backed up, installed and hash-verified on the local v1.2.0 setup.
The startup log identifies the alpha and records successful registration of all seven hooks, with
no recorded mod error. That is startup evidence, not visual or match acceptance.

**The new graphical pass has not yet been visually checked in-game.** User-run acceptance is
pending for the lobby/pickers, native previews, current selections, active counts, closed cards,
Escape handling, saved setup restore and return/reopen. Screenshots have not been captured for
this alpha. Broader acceptance should include another map, uneven teams, mixed army modes,
restart, full-match victory and failure recovery.

Until individually observed and recorded, those checks remain outstanding. Missing evidence must
not be presented as a pass. No new AI decisions, resource rules or online functionality are included.

# Local Skirmish Lobby 3.0.0 — local fork

Native-style offline lobby for the existing Broken Arrow v1.2.0 + MelonLoader setup.
This is a local fork, not an official game or upstream mod release.

## Install

1. Close the game. Back up `C:\Games\Broken Arrow\Mods\BALocalSkirmish.dll` outside `Mods`.
   Also back up `C:\Games\Broken Arrow\UserData\BALocalSkirmish\lobby.json` if it exists.
2. Copy **only this package's top-level `BALocalSkirmish.dll`** to
   `C:\Games\Broken Arrow\Mods`, replacing the existing DLL.
3. Keep the existing loader and working offline launcher. Do not run `install.bat`, copy rollback
   directories into Mods, or leave a second renamed DLL copy in Mods.
4. Launch as usual and choose Skirmish. The log identifies **3.0.0** and should report successful
   closed-slot and native-AI-preset hook registration.

This package has not changed your installed game, loader or mod. No game assets are redistributed.
Existing map/deck presets retain the same saved-settings schema.

## Smaller matches and no-player slots

Click an optional AI card's **ACTIVE / CLOSED** control and choose **Closed — no player**.
Choose **AI player** to restore it. The army choice is retained while closed. Human and required
scenario slots are protected; at least one opposing slot stays active.

**Match format** offers 1v1–5v5, counting you as one player. For 1v1, all four allied AI and four
opposing AI close. You can make uneven teams manually, such as you versus three AI. **Fill all AI
slots** reactivates existing bots. No new player IDs are invented.

This changes runtime activation before scripts initialize. Internal player identities remain because
the game references them. On shipped maps, closed bots no longer report themselves as AI to the
scripts. A second guard prevents direct multi-unit waves from closed owners. It does more than hide
cards or omit deck assignments. Workshop scripts/preplaced units need separate live validation;
the shipped-map investigation does not establish every custom mission's compatibility.

## Armies, nations and saved setups

An AI's army selector offers:

- **AI preset · ...**: real native scripted army profiles, offered only on the 22 audited built-in
  v1.2.0 skirmish maps. Native unit pools/upgrades/transports bypass custom-deck wave replacement.
- A named deck or **Same as your deck**: the existing mod's full-deck extension. Its upstream live
  behaviour limitations remain; this release does not claim every custom-deck issue is fixed.

Native and custom armies can coexist. The team nation selector filters choices and replaces
incompatible selections with matching available armies. **Any nation** permits mixed/mirror armies.
The human selects a full available deck. Difficulty remains one scenario-wide setting.

Closed slots, nation filters and profile IDs persist with named setups. Scenario defaults restore
the original all-active/default-deck baseline. Campaign/PvE player setup remains scenario-controlled.

## Graphical overhaul

Large battlefield/settings sidebar, blue/red force banners, five player cards per side, direct army
and slot-state controls, native fonts/artwork when loaded, and matching larger picker windows.
Borrowed assets stay owned by the game; fallback styling works if an asset is unavailable. Extra
custom rows/teams still page. Previews, search, numeric settings and saved setups remain available.

## Rollback and settings

Close the game and copy `rollback\BALocalSkirmish.dll` (2.8.0) over the installed DLL.
`upstream-original\BALocalSkirmish.dll` is the original 2.6.3 mod. Restore your settings backup when
rolling back: older versions ignore new fields and can discard them when saving.

Settings normally live under `UserData\BALocalSkirmish\lobby.json`, using MelonLoader's UserData
directory. Missing armies/slots and invalid values reset with a notice or block launch. Unreadable
files are preserved and saving is disabled that session. To reset, close the game, move the file
aside and restart. Native closed-slot/profile binding failures stop initialization and log an error
instead of silently starting with different armies.

## Validation and first live test

Built against local v1.2.0 assemblies, net6.0/x64, without warnings/errors. Automated checks cover
slot policy, runtime activation/identity, direct wave suppression, native army binding, mixed armies,
restart/cleanup, persistence and borrowed UI assets. Managed Harmony dispatch is exercised. Native
code and shipped-graph inspection support the chosen hook points; tests do not run the native game.
All 633 managed checks and the Harmony registration fixture pass; see `VALIDATION-3.0.0.md`.

**This release has not been tested in a running match.** First test 1v1 on Airbase with visibly
different native presets, then uneven teams and a restart. Verify closed owners never spawn, chosen
armies appear, objectives work, settings persist and UI fits your resolution. Preserve
`MelonLoader\Latest.log` before relaunch if anything fails.

The included `NATIVE-SKIRMISH-AUDIT.md`, `CLOSED-SLOT-ENGINE.md`, `NATIVE-AI-PROFILE-NOTES.md` and
`NATIVE-UI-DESIGN.md` contain the detailed findings and limits. Online invites, matchmaking, ready
clients, synchronized humans, human team switching and online spectators are not simulated offline.

Upstream: https://github.com/BovineOverlord/broken-arrow-local-skirmish (MIT; license included).

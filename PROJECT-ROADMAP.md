# Offline lobby alpha roadmap

This repository delivers the **offline lobby and setup layer**. It retains the native slot/profile and custom-deck plumbing required to make those choices work. The alpha targets Broken Arrow v1.2.0, Windows x64 and an existing MelonLoader 0.7.3 configuration.

## Public alpha

- Clear team rosters, active/closed states and direct army controls.
- Native map previews, searchable choices, nation filters and saved setups.
- Dependable installation, backups, rollback, useful errors and accurate compatibility notes.
- Reproducible source/package validation, public issue forms and limited live acceptance.

The release is experimental. A successful Airbase test and managed fixtures do not establish all-map, all-resolution, full-match or newer-version compatibility. Record gaps explicitly rather than calling the build stable.

## Next lobby work

Use tester reports to prioritize resolution/scaling problems, picker/navigation issues, missing-preview fallbacks, corrupted/missing-content recovery, restart and varied team formats. Expand the compatibility matrix only after testing the actual game version and relevant native hooks.

## AI is a separate track

New AI behavior is developed separately, **offline first**. The first target is useful attack commitment when a sustained objective/score position is losing. The stock Airbase scripts already have point-count-driven pushback behavior; the next AI project must trace its targeting and command ownership before changing it. A deck/resource commander remains a longer-term experiment.

The lobby alpha does not change native AI timing, production, resource rules or tactical orders. Future AI integration should use a narrow session/owner/army contract and avoid competing hooks. Online/custom co-op support is a later feasibility investigation, not an advertised feature of this release.

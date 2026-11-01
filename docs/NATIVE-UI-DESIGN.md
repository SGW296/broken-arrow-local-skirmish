# Native lobby visual reference and implementation spec

This document records inspected local v1.2.0 API metadata/catalog entries and a proposed visual layout.
No game assets were copied into the repository. No native lobby prefab or networking callback was
instantiated to inspect these definitions. Runtime asset availability and appearance need live testing.

## Native reference

The publisher's [manual, page 15](https://ftp.matrixgames.com/pub/BrokenArrow/BrokenArrowManualEBOOK.pdf#page=15)
shows separate enemy/friendly columns with stacked player cards, portraits/flags, a deck or AI preset
selector on each row, and visible free/locked slots. It is a launch-era screenshot; its old minimum
player and allied-bot restrictions are not a v1.2.0 capability reference.

The publisher's [Allied Command update](https://www6.slitherine.com/news/broken-arrow-allied-command-update-out-now)
describes later allied bots in either side's slots, specialization/difficulty options, automatic bot
population, private lobbies by default, and time-of-day settings. Our offline launcher uses scenario
metadata and scripts rather than the online lobby state. Borrow the visual team/card structure while
keeping controls limited to verified local functionality.

## Concrete composition

Use a full dark lobby surface over the existing main-menu scene, with a shallow header and footer.
Avoid a small blue framed dialog with a long vertical list of equally weighted fields. At a reference
1680×1000 canvas, reserve 40 px safe margins, an 80 px header, a 72 px footer and 24 px column gutters.

- **Left panel, about 340 px:** map category and map selector, a large native tactical preview (about
  316×200), map name, and compact scenario settings. Keep advanced explanations in a help area.
- **Centre/right, two equal team panels, about 570 px each:** a blue friendly header and red opposing
  header, then five stacked player cards. Human row remains part of its team. Each card has an avatar
  area, name/role, flag/spec area when correctly resolved, and one clear deck-selection affordance.
  Empty or scenario-controlled rows look inactive and have no fake add/remove actions.
- **Footer:** restrained preset controls, a clear Start battle button and secondary Back/close action.
  Preserve selected state visibly; keep technical UIDs and saved-state diagnostics out of the primary
  card text unless needed in an expandable details area or an error.

For five rows, use at least 106 px card height plus 10 px gaps. Keep heading text around 26–30 px,
card titles 20–22 px, body/settings 17–19 px, and secondary details at least 15–16 px. Scale to fit
1280×720; verify clipped/wrapped text with long custom deck names. At narrower aspect ratios use
explicit paging or a bounded scroll region rather than shrinking text below readable sizes.

Use the native dark neutral palette, desaturated blue/red team accents, off-white type and thin
separators. Button hover/pressed/disabled states should be clear. Retain text labels alongside icons.
The country shown belongs to the selected deck; neither team needs a fixed Russia/USA designation.
Do not invent rank/level/rating information to fill the native card layout.

## Already-loaded asset borrowing

`NativeLobbyStyle.Capture()` is an isolated helper implementing read-only asset discovery. It does
not instantiate prefabs, request network services or load Addressables. It returns conservative
fallback colors when native assets are not loaded. Fonts may be null; callers keep their existing
OS-font fallback. Borrowed fonts/sprites must never be passed to `Object.Destroy`.

Verified in installed generated metadata:

| Purpose | Exact API / member | Runtime use |
|---|---|---|
| Team colors/banner sprites | `Il2CppBrokenArrow.Client.Ecs.UI.Menu.Network.TeamPanel.TeamIdentifier._friendlyColor`, `_enemyColor`, `_friendlyBackground`, `_enemyBackground` | Read loaded components from `Resources.FindObjectsOfTypeAll<TeamIdentifier>()`; no `Init`/`Refresh` call |
| Popup dim / neutral flag | `GameConfig.Instance.UiConfig.PopupBackgroundColor`, `NeutralFlagIcon` | Read existing config only |
| Generic bot portrait | `GameConfig.Instance.AiConfig.AiProfilePresetGeneric.Avatar` | Generic AI artwork, without fabricating a specific AI profile |
| Native map image | `GameConfig.Instance.MapCardsConfig.Data`; card `SceneName`, `MapPreview`, `Image` | Existing preview resolver uses exact scene match; shared sprite ownership |
| Font objects | `Resources.FindObjectsOfTypeAll<Font>()` | Prefer Inter legacy regular/bold fonts; snapshot borrowed references |
| Native TMP font source | `Il2CppTMPro.TMP_FontAsset.sourceFontFile` | A future TMP integration can inspect loaded font assets; source may be stripped/null |

Exact sprite stems present as catalog internal IDs, and searched among loaded `Sprite.name` values:

- `Lobby Forces Blue Background`, `Lobby Forces Red Background`
- `Empty Slot Background`
- `Host Indicator Icon`
- `Button CTA Background`

These are located under `Assets/Prefabs/GUI/Network/Images` or `Assets/Prefabs/GUI/Elements/Images` in
the installed Addressables catalog. Catalog presence does not prove that a sprite is loaded, that its
runtime name is identical, or that the internal path is a registered Addressables key. Exact-stem lookup
and native component references therefore have fallbacks; do not perform fuzzy image matching.

The catalog includes Inter regular/medium/semibold/bold SDF fonts, corresponding TTFs, and legacy
Unity Font assets named `Inter-Regular-Legacy Dynamic`, `Inter-Regular-Legacy`,
`Inter-Bold-Legacy Dynamic`. Actual loaded Font names and whether the legacy fonts are available
must be checked in a running game. The helper tests use stand-ins to verify lookup priority/fallbacks.

## Country and specialization icons: metadata confirmed, live use deferred

The installed APIs provide a legitimate route for deck icon resolution:

1. `Session.TryGetService<DataBaseService>(out database, false)`.
2. `database.GetCountryById(deck.CountryID).FlagFileName`.
3. `database.GetSpecializationsById(deck.Spec1ID / Spec2ID).Icon`.
4. `Il2CppBrokenArrow.Client.Ecs.UI.IconsLoader.LoadSprite(string address, bool outline, Image addToImage)`
   and `LoadSpecIcon(string address, bool small, Image addToImage)`.

The method signatures exist; how the game's icon loader interprets database addresses and manages
its cache has not been exercised by these stand-in tests. Do not call `Dispose()` on the game's loader
or destroy returned sprites. An alternative read-only fallback is matching an already-loaded
`AiProfilePreset` by country/spec IDs, whose `Spec1Preview`, `Spec2Preview` and `AvatarPreview` exist.
Do not substitute an arbitrary preset with different IDs. Text country/spec labels or a neutral icon
are preferable to wrong artwork.

## Avoid copying native behaviour

The catalog contains complete `Lobby.prefab`, `Team Panel Bot Card.prefab`, `Player Slot.prefab`,
`Lobby Deck Selector Popup.prefab` and map-selection prefabs. Their components expose `Awake`,
initialization, events and network service dependencies. Cloning an active prefab and then stripping
behaviours is too late to guarantee that `Awake`/`OnEnable` did not run. Build the local layout from
plain UI components and borrowed visual assets. Do not call native lobby `Init`, button `ForceClick`,
team slot kick/lock/add-bot callbacks or online deck-assignment methods for visual reuse.

Never package bundle extracts, game fonts, portraits or textures. Tests and installation packages
should contain only the mod's source/DLL and documentation; runtime artwork comes from the user's
existing install. Any mod-created textures/sprites retain separate ownership from borrowed assets.

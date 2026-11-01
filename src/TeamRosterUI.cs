using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MelonLoader;

namespace BALocalSkirmish
{
    public partial class Core
    {
        int _teamPage, _rosterPage;
        readonly List<RosterDeckLabel> _rosterDeckLabels = new List<RosterDeckLabel>();
        sealed class RosterDeckLabel
        {
            public RosterEntry Slot;
            public Text Label, StateLabel, NameLabel, NationLabel, AvatarFallback;
            public Button Button;
            public Image Background, Stripe, Avatar, Spec1, Spec2;
        }

        List<RosterTeam> ReadRoster()
        {
            var entries = new List<RosterEntry>();
            if (!IsSkirmishMap()) return TeamRoster.Build(entries);
            try
            {
                var teams = _maps[_mapIdx].MetaFile.Teams;
                if (teams == null) return TeamRoster.Build(entries);
                bool foundHuman = false;
                var seen = new HashSet<int>();
                foreach (var team in teams)
                {
                    if (team == null || team.Slots == null) continue;
                    for (int i = 0; i < team.Slots.Count; i++)
                    {
                        var slot = team.Slots[i];
                        if (slot == null) continue;
                        int side = (int)team.TeamSide;
                        bool human = side == 0 && !foundHuman;
                        if (human) foundHuman = true; // Same assumption as the existing offline loader.
                        // Match RefreshAiSlots: read-only non-bot rows must not reserve a bot UID.
                        bool eligible = human ? seen.Add(slot.BotId)
                            : slot.IsBot && slot.BotId >= 0 && seen.Add(slot.BotId);
                        int aiIndex = !human && eligible
                            ? _aiSlots.FindIndex(s => s.PlayerUid == slot.BotId && s.TeamSide == side) : -1;
                        entries.Add(new RosterEntry {
                            TeamSide = side, TeamName = team.TeamName,
                            Name = string.IsNullOrEmpty(slot.PlayerName) ? "Player " + (i + 1) : slot.PlayerName,
                            PlayerUid = slot.BotId, IsHuman = human, AiIndex = aiIndex
                        });
                    }
                }
            }
            catch (Exception e) { MelonLogger.Warning("[LocalSkirmish] Team roster: " + e.Message); }
            return TeamRoster.Build(entries);
        }

        Text DeckButton(Transform parent, float x, float y, float width, Action choose)
        {
            return RosterSelect(parent, x, y, width, 34, choose, out _);
        }

        Text RosterSelect(Transform parent, float x, float y, float width, float height, Action choose, out Button button)
        {
            button = MakeButton(parent, x, y, width, height, "", choose, _skin.Control);
            MakeLabel(button.transform, width / 2 - 17, 0, 22, height - 2, "▾", 17, TextAnchor.MiddleCenter, _skin.MutedText);
            var label = MakeLabel(button.transform, -10, 0, width - 38, height - 2, "", 17, TextAnchor.MiddleLeft, _skin.Text);
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 13; label.resizeTextMaxSize = 17;
            return label;
        }

        Image RosterSurface(Transform parent, float x, float y, float width, float height, Color color, Sprite sprite = null)
        {
            var image = MakeImage(parent, x, y, width, height, color);
            image.raycastTarget = false;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = sprite.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple;
            }
            return image;
        }

        Text RosterHeading(Transform parent, float x, float y, float width, float height, string text, int size, Color color)
        {
            var label = MakeLabel(parent, x, y, width, height, text, size, TextAnchor.MiddleLeft, color);
            if (_skin.HeadingFont != null) label.font = _skin.HeadingFont;
            return label;
        }

        void BuildLobbyBody(Transform panel)
        {
            if (_skin == null) _skin = NativeLobbyStyle.Capture();
            RosterSurface(panel, -624, 26, 384, 716, _skin.Panel);
            RosterHeading(panel, -624, 384, 360, 24, "SCENARIO", 15, _skin.MutedText);
            DeckButton(panel, -624, 354, 360, PickFilter).text = FilterNames[_filter];
            RosterHeading(panel, -624, 324, 360, 24, "BATTLEFIELD", 15, _skin.MutedText);
            _mapLabel = DeckButton(panel, -670, 294, 268, PickMap);
            MakeButton(panel, -488, 294, 88, 34, "Random", RandomMap, _skin.Control, 14).interactable = FilteredMaps().Count > 0;

            RosterSurface(panel, -624, 180, 362, 180, _skin.Divider);
            _previewImg = MakeImage(panel, -624, 180, 360, 178, _skin.Background);
            _previewImg.raycastTarget = false;
            _previewCaption = MakeLabel(panel, -624, 77, 360, 26, "", 14, TextAnchor.MiddleLeft, _skin.MutedText);

            RosterHeading(panel, -624, 42, 360, 26, "MATCH SETTINGS", 17, _skin.Text);
            Text SettingRow(string name, float y, Action choose)
            {
                var label = MakeLabel(panel, -736, y, 136, 32, name, 16, TextAnchor.MiddleLeft, _skin.MutedText);
                label.resizeTextForBestFit = true; label.resizeTextMinSize = 13; label.resizeTextMaxSize = 16;
                return DeckButton(panel, -554, y, 220, choose);
            }
            if (_hasDiff) _diffLabel = SettingRow("Difficulty", 4, () =>
            {
                OpenPicker("Scenario difficulty", new List<Choice> { new Choice(1, "Easy"), new Choice(2, "Medium"), new Choice(3, "Hard") }, value => { _diff = value; UpdateLabels(); });
            });
            else MakeLabel(panel, -624, 4, 360, 34, "Difficulty controlled by scenario", 15, TextAnchor.MiddleLeft, _skin.MutedText);
            int optionPages = Math.Max(1, (_opts.Count + 3) / 4);
            _optionPage = Math.Max(0, Math.Min(optionPages - 1, _optionPage));
            int first = _optionPage * 4;
            for (int i = first; i < Math.Min(first + 4, _opts.Count); i++)
            {
                var option = _opts[i];
                _optLabels.Add(SettingRow(option.Label, -36 - (i - first) * 40, () => PickOption(option)));
            }
            if (_opts.Count == 0) MakeLabel(panel, -624, -50, 360, 60, "No additional match settings.", 16, TextAnchor.MiddleLeft, _skin.MutedText);
            if (optionPages > 1)
            {
                MakeButton(panel, -758, -200, 92, 28, "Previous", () => { _optionPage--; BuildUI(); }, _skin.Control, 13).interactable = _optionPage > 0;
                MakeLabel(panel, -624, -200, 160, 28, $"Options {_optionPage + 1} / {optionPages}", 14, TextAnchor.MiddleCenter, _skin.MutedText);
                MakeButton(panel, -490, -200, 92, 28, "Next", () => { _optionPage++; BuildUI(); }, _skin.Control, 13).interactable = _optionPage + 1 < optionPages;
            }
            RosterSurface(panel, -624, -234, 360, 1, _skin.Divider);
            MakeLabel(panel, -624, -275, 360, 62, "Choose each player's deck on their card.\nClose optional slots for smaller teams.", 16, TextAnchor.UpperLeft, _skin.MutedText);
            MakeLabel(panel, -624, -319, 360, 30, "AI timing and orders come from the scenario.", 13, TextAnchor.MiddleLeft, _skin.MutedText);

            var teams = ReadRoster();
            int teamPages = Math.Max(1, (teams.Count + 1) / 2);
            _teamPage = Math.Max(0, Math.Min(teamPages - 1, _teamPage));
            int rowPages = 1;
            for (int i = _teamPage * 2; i < Math.Min(teams.Count, _teamPage * 2 + 2); i++)
                rowPages = Math.Max(rowPages, TeamRoster.PageCount(teams[i]));
            _rosterPage = Math.Max(0, Math.Min(rowPages - 1, _rosterPage));

            if (teams.Count > 0)
            {
                for (int column = 0; column < 2; column++)
                {
                    int index = _teamPage * 2 + column;
                    if (index < teams.Count) DrawRosterTeam(panel, teams[index], column == 0 ? -120 : 504);
                    else DrawEmptyRosterPanel(panel, column == 0 ? -120 : 504);
                }
                _teamSummary = MakeLabel(panel, 192, -282, 1224, 28, "", 15, TextAnchor.MiddleLeft, _skin.MutedText);
                if (rowPages > 1)
                {
                    MakeButton(panel, -350, -314, 140, 28, "Previous slots", () => { _rosterPage--; BuildUI(); }, _skin.Control, 14).interactable = _rosterPage > 0;
                    MakeLabel(panel, -120, -314, 220, 28, $"Slots {_rosterPage + 1} / {rowPages}", 14, TextAnchor.MiddleCenter, _skin.MutedText);
                    MakeButton(panel, 110, -314, 140, 28, "Next slots", () => { _rosterPage++; BuildUI(); }, _skin.Control, 14).interactable = _rosterPage + 1 < rowPages;
                }
                if (teamPages > 1)
                    MakeButton(panel, 654, -314, 300, 28, $"Teams {_teamPage + 1} / {teamPages}  →", () => { _teamPage = (_teamPage + 1) % teamPages; _rosterPage = 0; BuildUI(); }, _skin.Control, 14);
            }
            else
            {
                RosterSurface(panel, 192, 40, 1224, 640, _skin.Panel);
                RosterHeading(panel, 192, 316, 1152, 42, "YOUR ARMY", 27, _skin.Text);
                MakeLabel(panel, -240, 246, 240, 36, "Your deck", 19, TextAnchor.MiddleLeft, _skin.MutedText);
                _deckLabel = RosterSelect(panel, 332, 246, 840, 42, () => PickDeckForSlot(-1), out _);
                MakeLabel(panel, 192, 90, 1152, 220, IsSkirmishMap()
                    ? "The team roster is unavailable for this scenario.\nYour player deck can still be selected."
                    : "This scenario manages its own teams and AI decks.\n\nDedicated skirmish maps show both force rosters here, with a deck choice for each player.", 22, TextAnchor.UpperLeft, _skin.MutedText);
            }

            RosterSurface(panel, 0, -340, 1608, 1, _skin.Divider);
            MakeLabel(panel, -748, -365, 112, 32, "Saved setup", 15, TextAnchor.MiddleLeft, _skin.MutedText);
            MakeInput(panel, -536, -365, 300, _presetName, "Preset name", value => _presetName = value);
            MakeButton(panel, -294, -365, 146, 32, "Save", SavePreset, _skin.Control, 16).interactable = _settingsWritable && _mapIdx >= 0;
            MakeButton(panel, -132, -365, 146, 32, "Load", LoadPreset, _skin.Control, 16).interactable = _mapIdx >= 0;
            MakeButton(panel, 80, -365, 236, 32, "Scenario defaults", ResetDefaults, _skin.Control, 16).interactable = _mapIdx >= 0;
            MakeButton(panel, 382, -365, 280, 32, "Match format  ▾", PickMatchFormat, _skin.Control, 16).interactable = IsSkirmishMap();
            MakeButton(panel, 680, -365, 248, 32, "Fill all AI slots", FillAllBots, _skin.Control, 16).interactable = IsSkirmishMap();

            MakeButton(panel, -704, -418, 200, 42, "Back", CloseSetup, _skin.Control, 18);
            _statusLabel = MakeLabel(panel, -56, -418, 1048, 44, "", 14, TextAnchor.MiddleLeft, _skin.MutedText);
            _startButton = MakeButton(panel, 654, -418, 300, 48, "START BATTLE", StartBattle, _skin.Action, 20);
            if (_skin.PrimaryButton != null)
            {
                var image = _startButton.GetComponent<Image>();
                image.sprite = _skin.PrimaryButton;
                image.type = _skin.PrimaryButton.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple;
                image.color = Color.white;
            }
            _startButton.interactable = string.IsNullOrEmpty(GetLaunchProblem());
            RefreshRosterPresentation();
        }

        void DrawRosterTeam(Transform panel, RosterTeam team, float x)
        {
            bool friendly = team.TeamSide == 0;
            var accent = friendly ? _skin.FriendlyAccent : team.TeamSide == 1 ? _skin.EnemyAccent : _skin.MutedText;
            var banner = friendly ? _skin.FriendlyBanner : team.TeamSide == 1 ? _skin.EnemyBanner : null;
            RosterSurface(panel, x, 352, 600, 56, banner != null ? Color.white : _skin.Panel, banner);
            RosterSurface(panel, x, 379, 600, 3, accent);
            RosterSurface(panel, x - 274, 352, 34, 34, accent);
            MakeLabel(panel, x - 274, 352, 32, 32, friendly ? "A" : team.TeamSide == 1 ? "B" : team.TeamSide.ToString(), 21, TextAnchor.MiddleCenter, _skin.Text);
            var title = RosterHeading(panel, x - 54, 359, 380, 29,
                friendly ? "FRIENDLY FORCES" : team.TeamSide == 1 ? "OPPOSING FORCES" : "SCENARIO FORCES", 23, _skin.Text);
            title.resizeTextForBestFit = true; title.resizeTextMinSize = 17; title.resizeTextMaxSize = 23;
            MakeLabel(panel, x - 54, 335, 380, 20, team.Name, 14, TextAnchor.MiddleLeft, _skin.Text);
            MakeLabel(panel, x + 238, 352, 100, 32, team.Entries.Count + " SLOTS", 15, TextAnchor.MiddleCenter, _skin.Text);
            var nation = DeckButton(panel, x, 306, 600, () => PickTeamCountry(team.TeamSide));
            nation.text = "Nation  ·  " + TeamCountryText(team.TeamSide);
            nation.transform.parent.gameObject.GetComponent<Button>().interactable = team.TeamSide == 0 || team.TeamSide == 1;
            if (_rosterPage >= TeamRoster.PageCount(team))
            {
                MakeLabel(panel, x, 155, 560, 90, "No more players on this page.", 20, TextAnchor.MiddleCenter, _skin.MutedText);
                return;
            }
            var entries = TeamRoster.GetPage(team, _rosterPage);
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                float y = 232 - i * 110;
                var surface = RosterSurface(panel, x, y, 600, 100, _skin.SlotBackground != null ? Color.white : _skin.Panel, _skin.SlotBackground);
                var stripe = RosterSurface(panel, x - 297, y, 5, 98, accent);
                RosterSurface(panel, x - 248, y, 68, 68, _skin.Background);
                Sprite portrait = entry.IsHuman ? _skin.HostIndicator : _skin.BotAvatar;
                var avatar = RosterSurface(panel, x - 248, y + 5, entry.IsHuman ? 34 : 64, entry.IsHuman ? 34 : 64,
                    portrait != null ? Color.white : Color.clear, portrait);
                avatar.preserveAspect = true;
                Text avatarFallback = portrait == null
                    ? MakeLabel(panel, x - 248, y + 4, 64, 40, entry.IsHuman ? "YOU" : entry.AiIndex >= 0 ? "AI" : "—", 22, TextAnchor.MiddleCenter, _skin.MutedText) : null;
                MakeLabel(panel, x - 248, y - 41, 84, 18, entry.IsHuman ? "PLAYER" : entry.AiIndex >= 0 ? "AI BOT" : "SCRIPTED", 11, TextAnchor.MiddleCenter, _skin.MutedText);
                var name = MakeLabel(panel, x - 16, y + 25, 320, 28, entry.IsHuman ? "You" : entry.Name, 20, TextAnchor.MiddleLeft, _skin.Text);
                name.resizeTextForBestFit = true; name.resizeTextMinSize = 15; name.resizeTextMaxSize = 20;
                if (entry.IsHuman || entry.AiIndex >= 0)
                {
                    var label = RosterSelect(panel, x + 42, y - 15, 480, 34,
                        () => PickDeckForSlot(entry.IsHuman ? -1 : entry.AiIndex), out var deckButton);
                    if (entry.IsHuman) _deckLabel = label;
                    else
                    {
                        var stateButton = MakeButton(panel, x + 220, y + 25, 124, 28, "", () => PickSlotState(entry.AiIndex), _skin.Control, 14);
                        var state = MakeLabel(stateButton.transform, 0, 0, 118, 26, "", 13, TextAnchor.MiddleCenter, _skin.MutedText);
                        stateButton.interactable = !_aiSlots[entry.AiIndex].Mandatory;
                        _rosterDeckLabels.Add(new RosterDeckLabel { Slot = entry, Label = label,
                            StateLabel = state, Button = deckButton, NameLabel = name, Background = surface, Stripe = stripe,
                            Avatar = avatar, AvatarFallback = avatarFallback,
                            NationLabel = MakeLabel(panel, x + 158, y - 40, 116, 17, "", 11, TextAnchor.MiddleRight, _skin.MutedText),
                            Spec1 = RosterSurface(panel, x + 236, y - 40, 18, 18, Color.clear),
                            Spec2 = RosterSurface(panel, x + 264, y - 40, 18, 18, Color.clear) });
                    }
                    if (entry.IsHuman) MakeLabel(panel, x + 220, y + 25, 124, 28, "HUMAN", 13, TextAnchor.MiddleCenter, accent);
                    MakeLabel(panel, x - 38, y - 40, 320, 17, entry.IsHuman ? "BATTLEGROUP / DECK" : "ARMY / AI PRESET", 11, TextAnchor.MiddleLeft, _skin.MutedText);
                }
                else
                {
                    MakeLabel(panel, x + 42, y - 15, 480, 34, "Controlled by scenario", 18, TextAnchor.MiddleLeft, _skin.MutedText);
                    MakeLabel(panel, x + 220, y + 25, 124, 28, "SCRIPTED", 13, TextAnchor.MiddleCenter, _skin.MutedText);
                }
            }
        }

        void DrawEmptyRosterPanel(Transform panel, float x)
        {
            RosterSurface(panel, x, 50, 600, 660, _skin.Panel);
            RosterHeading(panel, x, 352, 552, 44, "NO ADDITIONAL TEAM", 22, _skin.MutedText);
            MakeLabel(panel, x, 230, 552, 100, "This scenario does not define another team.", 20, TextAnchor.MiddleCenter, _skin.MutedText);
        }

        void RefreshRosterPresentation()
        {
            if (_skin == null) return;
            foreach (var view in _rosterDeckLabels)
            {
                if (view.Slot.AiIndex < 0 || view.Slot.AiIndex >= _aiSlots.Count) continue;
                var slot = _aiSlots[view.Slot.AiIndex];
                int index = slot.DeckIndex;
                var profile = slot.NativePresetId >= 0 ? NativeProfile(slot.NativePresetId) : null;
                if (view.NationLabel != null) view.NationLabel.text = profile != null
                    ? CountryName(profile.CountryId) : DeckCountryText(index < 0 ? _deckIdx : index);
                if (view.Avatar != null)
                {
                    view.Avatar.sprite = profile?.Avatar ?? _skin.BotAvatar;
                    view.Avatar.color = view.Avatar.sprite != null ? Color.white : Color.clear;
                    if (view.AvatarFallback != null) view.AvatarFallback.text = view.Avatar.sprite == null ? "AI" : "";
                }
                if (view.Spec1 != null) { view.Spec1.sprite = profile?.Spec1Preview; view.Spec1.color = view.Spec1.sprite != null ? Color.white : Color.clear; }
                if (view.Spec2 != null) { view.Spec2.sprite = profile?.Spec2Preview; view.Spec2.color = view.Spec2.sprite != null ? Color.white : Color.clear; }
                if (view.Label != null) view.Label.text = slot.NativePresetId >= 0
                    ? "AI preset · " + (profile?.Name ?? "Unavailable")
                    : index >= 0 && index < _deckNames.Count ? LobbyText.DeckName(_deckNames[index]) : "Same as your deck";
                if (view.StateLabel != null) view.StateLabel.text = slot.Mandatory ? "REQUIRED" : slot.Closed ? "CLOSED  ▾" : "ACTIVE  ▾";
                if (view.Button != null) view.Button.interactable = !slot.Closed;
                if (view.NameLabel != null) view.NameLabel.color = slot.Closed ? _skin.MutedText : _skin.Text;
                if (view.Label != null) view.Label.color = slot.Closed ? _skin.MutedText : _skin.Text;
                if (view.Background != null) view.Background.color = _skin.SlotBackground != null
                    ? new Color(1f, 1f, 1f, slot.Closed ? 0.35f : 1f) : slot.Closed ? _skin.Background : _skin.Panel;
                if (view.Stripe != null) view.Stripe.color = slot.Closed ? _skin.Divider
                    : slot.TeamSide == 0 ? _skin.FriendlyAccent : slot.TeamSide == 1 ? _skin.EnemyAccent : _skin.MutedText;
            }
        }
    }
}

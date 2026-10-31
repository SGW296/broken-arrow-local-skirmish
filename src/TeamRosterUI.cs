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
        sealed class RosterDeckLabel { public RosterEntry Slot; public Text Label; }

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
            var button = MakeButton(parent, x, y, width, 27, "", choose);
            MakeLabel(button.transform, width / 2 - 13, 0, 18, 24, "▾", 14, TextAnchor.MiddleCenter);
            var label = MakeLabel(button.transform, -8, 0, width - 36, 25, "", 14, TextAnchor.MiddleLeft);
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 11; label.resizeTextMaxSize = 14;
            return label;
        }

        void BuildLobbyBody(Transform panel)
        {
            Text MapRow(string name, float y, Action choose)
            {
                MakeLabel(panel, -409, y, 170, 32, name, 15, TextAnchor.MiddleLeft, CLabel);
                return DeckButton(panel, -100, y, 432, choose);
            }
            MapRow("Scenario category", 290, PickFilter).text = FilterNames[_filter];
            _mapLabel = MapRow("Map / scenario", 250, PickMap);
            MakeLabel(panel, -180, 218, 628, 25, _mapIdx >= 0 ? Category(_maps[_mapIdx]) + " · " + NameOf(_maps[_mapIdx]) : "No local scenarios found", 12, TextAnchor.MiddleLeft, CLabel);

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
                    if (index < teams.Count) DrawRosterTeam(panel, teams[index], column == 0 ? -335 : -5);
                }
                _teamSummary = MakeLabel(panel, -180, -142, 628, 25, "", 13, TextAnchor.MiddleLeft, CLabel);
                if (rowPages > 1)
                {
                    MakeButton(panel, -444, -175, 100, 26, "Previous slots", () => { _rosterPage--; BuildUI(); }, size: 12).interactable = _rosterPage > 0;
                    MakeLabel(panel, -270, -175, 210, 25, $"Slots page {_rosterPage + 1} / {rowPages}", 12, TextAnchor.MiddleCenter);
                    MakeButton(panel, -95, -175, 100, 26, "Next slots", () => { _rosterPage++; BuildUI(); }, size: 12).interactable = _rosterPage + 1 < rowPages;
                }
                if (teamPages > 1)
                    MakeButton(panel, 75, -175, 142, 26, $"Teams {_teamPage + 1}/{teamPages} →", () => { _teamPage = (_teamPage + 1) % teamPages; _rosterPage = 0; BuildUI(); }, size: 12);
            }
            else
            {
                MakeLabel(panel, -180, 178, 628, 26, "YOUR ARMY", 16, TextAnchor.MiddleLeft, CLabel);
                _deckLabel = MapRow("Your deck", 138, () => PickDeckForSlot(-1));
                MakeLabel(panel, -180, 35, 628, 130, IsSkirmishMap()
                    ? "No team roster could be read for this scenario. Your deck is still selectable."
                    : "This scenario controls its own teams and AI decks.\n\nDedicated skirmish maps show both teams here, with a deck selector on every configurable row.", 16, TextAnchor.UpperLeft, CLabel);
            }

            MakeLabel(panel, -180, -207, 628, 24, "SAVED SETUP — THIS SCENARIO", 13, TextAnchor.MiddleLeft, CLabel);
            MakeInput(panel, -335, -241, 310, _presetName, "Preset name", value => _presetName = value);
            MakeButton(panel, -90, -241, 146, 32, "Save preset", SavePreset).interactable = _settingsWritable && _mapIdx >= 0;
            MakeButton(panel, 74, -241, 146, 32, "Load preset", LoadPreset).interactable = _mapIdx >= 0;
            MakeButton(panel, -180, -279, 628, 30, "Restore scenario defaults", ResetDefaults).interactable = _mapIdx >= 0;

            _previewImg = MakeImage(panel, 340, 213, 300, 180, new Color(0.05f, 0.07f, 0.10f));
            _previewCaption = MakeLabel(panel, 340, 105, 300, 30, "", 14, TextAnchor.MiddleCenter, CLabel);
            MakeLabel(panel, 340, 66, 300, 26, "SCENARIO SETTINGS", 15, TextAnchor.MiddleLeft, CLabel);
            Text SettingRow(string name, float y, Action choose)
            {
                MakeLabel(panel, 244, y, 108, 30, name, 13, TextAnchor.MiddleLeft, CLabel);
                return DeckButton(panel, 400, y, 180, choose);
            }
            if (_hasDiff) _diffLabel = SettingRow("Difficulty", 30, () =>
            {
                OpenPicker("Scenario difficulty", new List<Choice> { new Choice(1, "Easy"), new Choice(2, "Medium"), new Choice(3, "Hard") }, value => { _diff = value; UpdateLabels(); });
            });
            else MakeLabel(panel, 340, 30, 300, 30, "Difficulty set by scenario", 13, TextAnchor.MiddleLeft);
            int optionPages = Math.Max(1, (_opts.Count + 3) / 4);
            _optionPage = Math.Max(0, Math.Min(optionPages - 1, _optionPage));
            int first = _optionPage * 4;
            for (int i = first; i < Math.Min(first + 4, _opts.Count); i++)
            {
                var option = _opts[i];
                _optLabels.Add(SettingRow(option.Label, -10 - (i - first) * 38, () => PickOption(option)));
            }
            if (_opts.Count == 0) MakeLabel(panel, 340, -25, 300, 50, "No extra settings for this scenario.", 14, TextAnchor.MiddleLeft);
            if (optionPages > 1)
            {
                MakeButton(panel, 228, -164, 76, 26, "Previous", () => { _optionPage--; BuildUI(); }, size: 12).interactable = _optionPage > 0;
                MakeLabel(panel, 340, -164, 130, 26, $"Options {_optionPage + 1}/{optionPages}", 12, TextAnchor.MiddleCenter);
                MakeButton(panel, 452, -164, 76, 26, "Next", () => { _optionPage++; BuildUI(); }, size: 12).interactable = _optionPage + 1 < optionPages;
            }
            MakeLabel(panel, 340, -215, 300, 64, "Click a deck on either team to change it.\nAI numbers and teams are fixed by the scenario.", 13, TextAnchor.UpperLeft, CLabel);
            MakeLabel(panel, 340, -276, 300, 46, "Difficulty and match options depend on the scenario's scripts.", 12, TextAnchor.UpperLeft, CLabel);

            _startButton = MakeButton(panel, -370, -324, 248, 40, "Start battle", StartBattle, CStart, 18);
            MakeButton(panel, -90, -324, 248, 40, "Random map in category", RandomMap).interactable = FilteredMaps().Count > 0;
            MakeButton(panel, 340, -324, 300, 40, "Close & remember", CloseSetup, CCancel);
            _statusLabel = MakeLabel(panel, 0, -358, 988, 28, "", 12, TextAnchor.MiddleLeft, CLabel);
        }

        void DrawRosterTeam(Transform panel, RosterTeam team, float x)
        {
            var title = MakeLabel(panel, x, 181, 310, 30, team.Title, 16, TextAnchor.MiddleLeft, CLabel);
            title.resizeTextForBestFit = true; title.resizeTextMinSize = 11; title.resizeTextMaxSize = 16;
            if (_rosterPage >= TeamRoster.PageCount(team))
            {
                MakeLabel(panel, x, 120, 310, 60, "No more players on this page.", 14, TextAnchor.MiddleCenter, CLabel);
                return;
            }
            var entries = TeamRoster.GetPage(team, _rosterPage);
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                float y = 139 - i * 58;
                MakeImage(panel, x, y, 310, 54, entry.IsHuman ? new Color(0.12f, 0.25f, 0.30f) : CRow);
                var name = MakeLabel(panel, x, y + 13, 290, 21, (entry.IsHuman ? "YOU · " : entry.AiIndex >= 0 ? "AI · " : "Scenario · ") + entry.Name, 13, TextAnchor.MiddleLeft);
                name.resizeTextForBestFit = true; name.resizeTextMinSize = 11; name.resizeTextMaxSize = 13;
                if (entry.IsHuman || entry.AiIndex >= 0)
                {
                    var label = DeckButton(panel, x, y - 12, 290, () => PickDeckForSlot(entry.IsHuman ? -1 : entry.AiIndex));
                    if (entry.IsHuman) _deckLabel = label;
                    else _rosterDeckLabels.Add(new RosterDeckLabel { Slot = entry, Label = label });
                }
                else MakeLabel(panel, x, y - 12, 290, 26, "Controlled by scenario", 13, TextAnchor.MiddleLeft, CLabel);
            }
        }
    }
}

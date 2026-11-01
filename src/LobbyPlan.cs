using System;
using System.Collections.Generic;

namespace BALocalSkirmish
{
    public partial class Core
    {
        List<SlotPolicyEntry> CurrentSlotPlan()
        {
            var result = new List<SlotPolicyEntry>();
            foreach (var team in ReadRoster())
                foreach (var row in team.Entries)
                {
                    var ai = row.AiIndex >= 0 && row.AiIndex < _aiSlots.Count ? _aiSlots[row.AiIndex] : null;
                    result.Add(new SlotPolicyEntry {
                        PlayerUid = row.PlayerUid, TeamSide = row.TeamSide, IsHuman = row.IsHuman,
                        Mandatory = ai?.Mandatory ?? row.IsHuman, Configurable = ai != null,
                        Closed = ai?.Closed ?? false
                    });
                }
            return result;
        }

        string GetLaunchProblem()
        {
            if (_mapIdx < 0 || _mapIdx >= _maps.Count) return "Choose an installed local map or scenario.";
            if (_deckIdx < 0 || _deckIdx >= _decks.Count) return "Choose an available deck in the game's army builder.";
            if (!IsSkirmishMap()) return null;
            if (!_aiDeckPatchReady) return "AI deck support could not load. See the MelonLoader log.";
            bool closed = _aiSlots.Exists(s => s.Closed);
            if (closed && !_slotPatchReady) return "Closed-slot support could not load. Restore all bots or check the MelonLoader log.";
            var plan = CurrentSlotPlan();
            // Unusual custom scenarios retain the original all-active launch path. They cannot
            // opt into slot editing when their identities/sides do not fit the known loader.
            bool standard = plan.Count > 0 && plan.TrueForAll(s => s.TeamSide == 0 || s.TeamSide == 1);
            if (closed || standard)
            {
                string error = SlotSelectionPolicy.Validate(plan);
                if (error != null) return error;
            }
            if (!MatchesTeamCountry(0, _deckIdx)) return "Your deck does not match your team's nation filter.";
            foreach (var slot in _aiSlots)
            {
                if (slot.Closed) continue;
                if (slot.NativePresetId >= 0)
                {
                    if (!_profilePatchReady) return "Native AI preset support could not load. Choose a custom deck or check the MelonLoader log.";
                    var profile = NativeProfile(slot.NativePresetId);
                    if (profile == null) return "Choose an available AI preset for " + slot.Name + ".";
                    if (TeamCountry(slot.TeamSide) != 0 && TeamCountry(slot.TeamSide) != profile.CountryId)
                        return "Choose a matching nation AI preset for " + slot.Name + ".";
                    continue;
                }
                int index = slot.DeckIndex < 0 ? _deckIdx : slot.DeckIndex;
                if (index < 0 || index >= _decks.Count) return "Choose an available deck for " + slot.Name + ".";
                if (!MatchesTeamCountry(slot.TeamSide, index)) return "Choose a matching nation deck for " + slot.Name + ".";
            }
            return null;
        }

        void PickSlotState(int index)
        {
            if (index < 0 || index >= _aiSlots.Count) return;
            var slot = _aiSlots[index];
            var choices = new List<Choice> { new Choice(0, "AI player", "Activate this slot with its selected army.", !slot.Closed) };
            if (!slot.Mandatory)
                choices.Add(new Choice(1, "Closed — no player", "No AI occupies this slot. Its deck is kept for later.", slot.Closed));
            OpenPicker("Slot — " + slot.Name, choices, value =>
            {
                if (!_aiSlots.Contains(slot)) return;
                if (value == 1 && !_slotPatchReady)
                { _notice = "Slot support failed to load. Check the MelonLoader log."; UpdateLabels(); return; }
                bool previous = slot.Closed;
                slot.Closed = value == 1 && !slot.Mandatory;
                string error = SlotSelectionPolicy.Validate(CurrentSlotPlan());
                if (error != null) { slot.Closed = previous; _notice = error; }
                else _notice = slot.Closed ? slot.Name + " is closed. No player will use this slot." : slot.Name + " is active.";
                BuildUI();
            });
        }

        void PickMatchFormat()
        {
            var choices = new List<Choice>();
            for (int i = 1; i <= 5; i++)
                choices.Add(new Choice(i, i + " vs " + i, "Counts you as one player; uses existing scenario slots."));
            OpenPicker("Match format", choices, value =>
            {
                if (!_slotPatchReady) { _notice = "Slot support failed to load. Check the MelonLoader log."; UpdateLabels(); return; }
                if (!SlotSelectionPolicy.TrySetFormat(CurrentSlotPlan(), value, out var closed, out var error))
                { _notice = error; UpdateLabels(); return; }
                foreach (var slot in _aiSlots) slot.Closed = closed.Contains(slot.PlayerUid);
                _notice = $"{value} vs {value} setup applied. Deck choices were retained.";
                BuildUI();
            });
        }

        void FillAllBots()
        {
            foreach (var slot in _aiSlots) slot.Closed = false;
            _notice = "All existing AI slots are active."; BuildUI();
        }

        int TeamCountry(int side) => _teamCountries.TryGetValue(side, out int country) ? country : 0;
        NativeAiProfileChoice NativeProfile(int id)
        {
            if (_mapIdx < 0 || _mapIdx >= _maps.Count) return null;
            return NativeAiProfiles.GetAvailable(_maps[_mapIdx]).Find(p => p.Id == id);
        }
        string TeamCountryText(int side) => CountryName(TeamCountry(side));
        static string CountryName(int country) => country == 1 ? "RUSSIA" : country == 2 ? "USA" : "ANY NATION";
        string DeckCountryText(int index)
        {
            try { return index >= 0 && index < _decks.Count ? CountryName(_decks[index].CountryID) : ""; }
            catch { return ""; }
        }
        bool MatchesTeamCountry(int side, int index)
        {
            if (TeamCountry(side) == 0) return true;
            try { return index >= 0 && index < _decks.Count && _decks[index].CountryID == TeamCountry(side); }
            catch { return false; }
        }

        void PickTeamCountry(int side)
        {
            if (side != 0 && side != 1) return;
            var profiles = NativeAiProfiles.GetAvailable(_maps[_mapIdx]);
            var choices = new List<Choice> { new Choice(0, "Any nation", "Allow any available scenario deck, including mirror matches.", TeamCountry(side) == 0) };
            for (int country = 1; country <= 2; country++)
                if (_decks.Exists(d => d.CountryID == country) || (side == 1 && profiles.Exists(p => p.CountryId == country)))
                    choices.Add(new Choice(country, CountryName(country), "Restrict this team to available decks from this nation.", TeamCountry(side) == country));
            OpenPicker("Team nation", choices, country =>
            {
                int first = country == 0 ? -1 : _decks.FindIndex(d => d.CountryID == country);
                var firstProfile = profiles.Find(p => p.CountryId == country);
                if (country != 0 && first < 0 && (side == 0 || firstProfile == null))
                { _notice = "No available army for that nation."; UpdateLabels(); return; }
                _teamCountries[side] = country;
                if (country != 0)
                {
                    if (side == 0 && !MatchesTeamCountry(side, _deckIdx)) _deckIdx = first;
                    foreach (var slot in _aiSlots)
                    {
                        if (slot.TeamSide != side) continue;
                        if (slot.NativePresetId >= 0)
                        {
                            if (NativeProfile(slot.NativePresetId)?.CountryId == country) continue;
                            if (firstProfile != null) slot.NativePresetId = firstProfile.Id;
                            else { slot.NativePresetId = -1; slot.DeckIndex = first; }
                        }
                        else if (!MatchesTeamCountry(side, slot.DeckIndex < 0 ? _deckIdx : slot.DeckIndex))
                        {
                            if (first >= 0) slot.DeckIndex = first;
                            else slot.NativePresetId = firstProfile.Id;
                        }
                    }
                }
                _notice = country == 0 ? "Team nation filter cleared." : "Team nation set. Incompatible armies were replaced with available matching armies.";
                BuildUI();
            });
        }
    }
}

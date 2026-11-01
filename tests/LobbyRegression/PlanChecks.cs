using System;
using System.Collections.Generic;
using System.IO;

namespace BALocalSkirmish
{
    public partial class Core
    {
        static Core PlanFixture(string root, int alphaCount = 5, int bravoCount = 5)
        {
            var c = new Core { _settings = new LobbySettings(),
                _settingsPath = Path.Combine(root, "plan-" + Guid.NewGuid().ToString("N") + ".json") };
            c._decks.AddRange(new[] {
                new IDeckDataModel { FileName = "ru-main.deck", Name = "RU Main", CountryID = 1 },
                new IDeckDataModel { FileName = "us-main.deck", Name = "US Main", CountryID = 2 },
                new IDeckDataModel { FileName = "ru-alt.deck", Name = "RU Alternate", CountryID = 1 }
            });
            c._deckNames.AddRange(new[] { "RU Main", "US Main", "RU Alternate" });
            var meta = new ScenarioMeta();
            for (int side = 0; side <= 1; side++)
            {
                var team = new ScenarioTeam { TeamSide = side, TeamName = side == 0 ? "Alpha" : "Bravo" };
                int count = side == 0 ? alphaCount : bravoCount;
                for (int i = 0; i < count; i++)
                {
                    int uid = side == 0 ? 2 + i : 20 + i;
                    string name = team.TeamName + " Player " + (i + 1);
                    team.Slots.Add(new ScenarioSlot { BotId = uid, IsBot = true, PlayerName = name });
                    if (side != 0 || i != 0) c._aiSlots.Add(new AiSlot { PlayerUid = uid, TeamSide = side, Name = name });
                }
                meta.Teams.Add(team);
            }
            c._maps.Add(new ScenarioSource { Name = "Plan A", Folder = "fixture/A", Type = ScenarioType.Skirmish, MetaFile = meta });
            c._maps.Add(new ScenarioSource { Name = "Plan B", Folder = "fixture/B", Type = ScenarioType.Skirmish, MetaFile = meta });
            c._opts.Add(new OptDef { Uid = 226, Min = 30, Max = 60, Step = 3, Default = 30 });
            c.RefreshOptions();
            return c;
        }

        int ActiveCount(int side) => CurrentSlotPlan().FindAll(row => row.TeamSide == side && !row.Closed).Count;

        string PlanSignature()
        {
            var values = new List<string> { "human:" + _deckIdx };
            foreach (var slot in _aiSlots) values.Add(slot.PlayerUid + ":" + slot.Closed + ":" + slot.DeckIndex + ":" + slot.NativePresetId);
            values.Add("countries:" + TeamCountry(0) + ":" + TeamCountry(1));
            return string.Join("|", values);
        }

        void ChooseFormat(int size) { PickMatchFormat(); _picked(size); }
        void ChooseSlot(int index, bool closed) { PickSlotState(index); _picked(closed ? 1 : 0); }
        void ChooseCountry(int side, int country) { PickTeamCountry(side); _picked(country); }

        static void PlanChecks(string root)
        {
            var c = PlanFixture(root);
            Check(c.GetLaunchProblem() == null && c.ActiveCount(0) == 5 && c.ActiveCount(1) == 5,
                "Fresh standard plan must launch as 5v5");
            var human = c.CurrentSlotPlan().Find(row => row.IsHuman);
            Check(human.PlayerUid == 2 && human.TeamSide == 0 && human.Mandatory && !human.Configurable && !human.Closed,
                "Human projection must remain fixed, protected and open");
            string original = c.PlanSignature();
            c.PickSlotState(0);
            Check(c._choices.Count == 2 && c._choices[0].Value == 0 && c._choices[1].Value == 1,
                "Optional slot picker must expose active/closed");
            Check(c._choices.FindAll(choice => choice.IsCurrent).Count == 1 && c._choices[0].IsCurrent,
                "Active optional slot was not marked as the current state");
            Check(c.PlanSignature() == original, "Opening/cancelling slot picker changed the plan");
            c._aiSlots[0].DeckIndex = 2; c._picked(1);
            Check(c._aiSlots[0].Closed && c._aiSlots[0].DeckIndex == 2 && c.ActiveCount(0) == 4,
                "Closing optional ally must retain its army and remove it from active count");
            c.PickSlotState(0);
            Check(c._choices.FindAll(choice => choice.IsCurrent).Count == 1 && c._choices[1].IsCurrent,
                "Closed slot picker marked the retained AI army as an active player");
            c.ChooseSlot(0, false);
            Check(!c._aiSlots[0].Closed && c._aiSlots[0].DeckIndex == 2, "Reopening ally lost retained deck");
            var choices = c._choices; c.PickSlotState(-1);
            Check(ReferenceEquals(c._choices, choices) && !c.CurrentSlotPlan().Find(row => row.IsHuman).Closed,
                "Human must not be addressed through the AI slot picker");
            c._aiSlots[0].Mandatory = true; c.PickSlotState(0);
            Check(c._choices.Count == 1 && c._choices[0].Value == 0, "Required slot offered closure");
            c._picked(1);
            Check(!c._aiSlots[0].Closed, "Required slot callback allowed closure");
            c._aiSlots[0].Mandatory = false;

            for (int i = 0; i < c._aiSlots.Count; i++)
                if (c._aiSlots[i].TeamSide == 1 && c._aiSlots[i].PlayerUid != 24) c.ChooseSlot(i, true);
            int last = c._aiSlots.FindIndex(slot => slot.PlayerUid == 24);
            Check(c.ActiveCount(1) == 1, "Manual opponent closure count");
            c.ChooseSlot(last, true);
            Check(!c._aiSlots[last].Closed && c.ActiveCount(1) == 1 && c._notice.Contains("at least one"),
                "Last-opponent closure must roll back with explanation");
            c.FillAllBots(); Check(c._aiSlots.TrueForAll(slot => !slot.Closed), "Fill all did not reopen every existing AI");

            c._aiSlots[0].DeckIndex = 2; original = c.PlanSignature();
            c.PickMatchFormat();
            Check(c._choices.Count == 5 && c._choices[0].Value == 1 && c._choices[4].Value == 5,
                "Quick formats must offer 1v1 through 5v5");
            Check(c.PlanSignature() == original, "Opening/cancelling format picker changed the plan");
            c._picked(1);
            Check(c.ActiveCount(0) == 1 && c.ActiveCount(1) == 1 && c._aiSlots[0].DeckIndex == 2,
                "1v1 must include human and preserve closed armies");
            Check(c.GetLaunchProblem() == null, "Valid 1v1 plan incorrectly blocked");
            c.ChooseFormat(2); Check(c.ActiveCount(0) == 2 && c.ActiveCount(1) == 2, "2v2 active capacities");
            c.ChooseFormat(5); Check(c.ActiveCount(0) == 5 && c.ActiveCount(1) == 5, "5v5 active capacities");
            c._aiSlots[0].Mandatory = true; original = c.PlanSignature(); c.ChooseFormat(1);
            Check(c.PlanSignature() == original && c._notice.Contains("protected"), "1v1 must reject excess required allies atomically");
            c.ChooseFormat(2); Check(c.ActiveCount(0) == 2 && !c._aiSlots[0].Closed, "2v2 closed a protected ally");
            var small = PlanFixture(root, 2, 2); original = small.PlanSignature(); small.ChooseFormat(5);
            Check(small.PlanSignature() == original && small._notice.Contains("enough"), "Unsupported format exceeded installed scenario capacity");

            c = PlanFixture(root); c._slotPatchReady = false; original = c.PlanSignature();
            c.ChooseSlot(0, true); Check(c.PlanSignature() == original && c._notice.Contains("failed"), "Manual close ignored missing hook");
            c.ChooseFormat(1); Check(c.PlanSignature() == original, "Quick format ignored missing hook");
            c._aiSlots[0].Closed = true;
            Check(c.GetLaunchProblem()?.Contains("Closed-slot support") == true, "Saved closed plan launched without its hook");
            c.FillAllBots(); Check(c.GetLaunchProblem() == null, "Fill all must recover original all-active path without slot hook");
            c._aiDeckPatchReady = false;
            Check(c.GetLaunchProblem()?.Contains("AI deck support") == true, "Missing AI deck hook allowed launch");
            c._aiDeckPatchReady = true; c._mapIdx = -1;
            Check(c.GetLaunchProblem()?.Contains("map") == true, "Missing map allowed launch");
            c._mapIdx = 0; c._deckIdx = 9;
            Check(c.GetLaunchProblem()?.Contains("deck") == true, "Missing human deck allowed launch");

            c = PlanFixture(root); original = c.PlanSignature(); c.PickTeamCountry(1);
            Check(c._choices.Count == 3, "Team nation picker must offer any plus installed RU/US decks");
            Check(c._choices.FindAll(choice => choice.IsCurrent).Count == 1 && c._choices.Find(choice => choice.IsCurrent).Value == 0,
                "Unfiltered nation picker did not mark Any nation");
            Check(c.PlanSignature() == original, "Opening/cancelling nation picker changed deck assignments");
            c._picked(2);
            Check(c.TeamCountry(1) == 2 && c._deckIdx == 0 && c._aiSlots.FindAll(slot => slot.TeamSide == 1).TrueForAll(slot => slot.DeckIndex == 1),
                "Opposing nation filter failed to replace incompatible decks or changed human army");
            c.PickTeamCountry(1);
            Check(c._choices.FindAll(choice => choice.IsCurrent).Count == 1 && c._choices.Find(choice => choice.IsCurrent).Value == 2,
                "Nation picker current marker followed the human nation instead of the chosen team");
            int enemy = c._aiSlots.FindIndex(slot => slot.TeamSide == 1);
            c.PickDeckForSlot(enemy);
            Check(c._choices.Count == 1 && c._choices[0].Value == 1, "Nation-filtered AI picker exposed incompatible or same-as-human army");
            c._deckIdx = 1; c._aiSlots[0].DeckIndex = 1; c.ChooseCountry(0, 1);
            Check(c.TeamCountry(0) == 1 && c._deckIdx == 0 && c._aiSlots[0].DeckIndex == 0,
                "Friendly nation filter failed to replace incompatible human/AI decks");
            c.PickDeckForSlot(-1);
            Check(c._choices.Count == 2 && c._choices.TrueForAll(choice => choice.Value == 0 || choice.Value == 2),
                "Human deck picker ignored team nation filter");
            Check(c.GetLaunchProblem() == null && c.TeamCountryText(0) == "RUSSIA" && c.TeamCountryText(1) == "USA",
                "Valid mirror-independent nation settings blocked or mislabeled");
            c._aiSlots[enemy].DeckIndex = 0;
            Check(c.GetLaunchProblem()?.Contains("matching nation") == true, "Incompatible AI deck bypassed launch guard");
            c._aiSlots[enemy].Closed = true;
            Check(c.GetLaunchProblem() == null, "Closed AI army should not block active nation legality");
            c._aiSlots[enemy].Closed = false; c._aiSlots[enemy].DeckIndex = 1;
            c._deckIdx = 1;
            Check(c.GetLaunchProblem()?.Contains("Your deck") == true, "Incompatible human deck bypassed launch guard");
            c._deckIdx = 0; c.ChooseCountry(1, 0);
            Check(c.TeamCountry(1) == 0 && c._aiSlots[enemy].DeckIndex == 1, "Clearing nation filter needlessly changed selected armies");
            c._decks.RemoveAt(1); c._deckNames.RemoveAt(1); c.PickTeamCountry(1);
            Check(c._choices.Count == 2 && c._choices.TrueForAll(choice => choice.Value != 2), "Unavailable nation was offered");
            original = c.PlanSignature(); c._picked(2);
            Check(c.PlanSignature() == original && c._notice.Contains("No available"), "Unavailable nation callback changed the setup");

            c = PlanFixture(root); c.ChooseFormat(2); c.ChooseCountry(0, 1); c.ChooseCountry(1, 2);
            c._aiSlots[0].DeckIndex = 2; c._optVals[226] = 45; c._diff = 1;
            SetupSnapshot saved = c.CaptureSetup();
            Check(saved.ClosedSlots.Count == 6 && saved.TeamCountries[0] == 1 && saved.TeamCountries[1] == 2,
                "Capture lost closed UIDs or team filters");
            c.SaveCurrent(); var disk = LobbySettings.Load(c._settingsPath).Scenarios[ScenarioKey(c._maps[0])];
            Check(disk.ClosedSlots.Count == 6 && disk.TeamCountries[1] == 2 && disk.AiDecks[3] == DeckKey(c._decks[2]),
                "JSON roundtrip lost slot/country/deck configuration");
            c.ResetDefaults();
            Check(c.ActiveCount(0) == 5 && c.ActiveCount(1) == 5 && c._teamCountries.Count == 0 && c._deckIdx == 0 && c._diff == 3,
                "Scenario defaults retained closures or team filters");
            c.RestoreSetup(disk);
            Check(c.ActiveCount(0) == 2 && c.ActiveCount(1) == 2 && c.TeamCountry(1) == 2 && c._optVals[226] == 45,
                "Restore lost a valid remembered plan");
            c._presetName = "2v2 nations"; c.SavePreset(); c.ResetDefaults(); c.LoadPreset(); c._picked(c._choices[0].Value);
            Check(c.ActiveCount(0) == 2 && c.ActiveCount(1) == 2 && c.TeamCountry(0) == 1 && c.TeamCountry(1) == 2,
                "Named preset failed to restore closed slots/nations");
            c.SelectMap(1);
            Check(c.ActiveCount(0) == 5 && c.ActiveCount(1) == 5 && c._teamCountries.Count == 0, "New scenario inherited closed slots/nations");
            c.SelectMap(0);
            Check(c.ActiveCount(0) == 2 && c.ActiveCount(1) == 2 && c.TeamCountry(1) == 2 && c._aiSlots[0].DeckIndex == 2,
                "Returning to scenario lost closed-state/nation identity");

            var invalid = c.CaptureSetup(); c.ResetDefaults();
            c._aiSlots[0].Mandatory = true;
            invalid.ClosedSlots = new List<int> { 2, c._aiSlots[0].PlayerUid, 123456 };
            invalid.TeamCountries = new Dictionary<int, int> { [0] = 1, [1] = 77, [5] = 2 };
            c.RestoreSetup(invalid);
            Check(c._aiSlots.TrueForAll(slot => !slot.Closed) && c._notice.Contains("unavailable"),
                "Restore accepted human/required/missing slot closures");
            Check(c.TeamCountry(0) == 1 && c.TeamCountry(1) == 0 && !c._teamCountries.ContainsKey(5),
                "Restore accepted invalid team side or nation value");
            c._aiSlots[0].Closed = true;
            Check(!c.CaptureSetup().ClosedSlots.Contains(c._aiSlots[0].PlayerUid), "Capture persisted closure of protected AI");
            c._aiSlots[0].Closed = false; c._aiSlots[0].Mandatory = false;
            foreach (var slot in c._aiSlots) if (slot.TeamSide == 1) slot.Closed = true;
            Check(c.GetLaunchProblem()?.Contains("opposing slot") == true, "Restored plan with no opposing player allowed launch");
            c.FillAllBots(); Check(c.GetLaunchProblem() == null, "Fill all did not recover plan after invalid restored closures");
        }
    }
}

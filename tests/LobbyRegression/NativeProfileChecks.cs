using System;
using System.Collections.Generic;

namespace BALocalSkirmish
{
    public partial class Core
    {
        static void NativeProfileChecks(string root)
        {
            NativeAiProfiles.Available.Clear();
            NativeAiProfiles.Available.AddRange(new[] {
                new NativeAiProfileChoice { Id = 0, CountryId = 1, Name = "RU native zero" },
                new NativeAiProfileChoice { Id = 7, CountryId = 2, Name = "US native army" },
                new NativeAiProfileChoice { Id = 42, CountryId = 1, Name = "RU native alternate" }
            });
            var c = PlanFixture(root);
            c._aiSlots[0].DeckIndex = 2;
            string before = c.PlanSignature();
            c.PickDeckForSlot(0);
            Check(c._choices.Count == 7 && c._choices.Exists(choice => choice.Value == -1000)
                && c._choices.Exists(choice => choice.Value == -1007) && c._choices.Exists(choice => choice.Value == -1042),
                "AI army picker omitted native profile IDs or confused them with custom indices");
            Check(c.PlanSignature() == before, "Opening/cancelling native army picker changed selection mode");
            int uid = c._aiSlots[0].PlayerUid;
            c._aiSlots.Reverse(); c._picked(-1000);
            var selected = c._aiSlots.Find(slot => slot.PlayerUid == uid);
            Check(selected.NativePresetId == 0 && selected.DeckIndex == 2,
                "Native ID zero failed or picker rebound to a different UID after reorder");
            Check(c._aiSlots.FindAll(slot => slot.PlayerUid != uid).TrueForAll(slot => slot.NativePresetId == -1),
                "Native profile choice changed another AI slot");
            c._aiSlots.Reverse();
            Check(c.GetLaunchProblem() == null && c.NativeProfile(0)?.CountryId == 1, "Available native ID zero should be launchable");
            c.PickDeckForSlot(-1);
            Check(c._choices.Count == 3 && c._choices.TrueForAll(choice => choice.Value >= 0),
                "Human deck picker offered native AI profiles");
            c.PickDeckForSlot(0); c._picked(1);
            Check(selected.NativePresetId == -1 && selected.DeckIndex == 1, "Custom army choice did not exit native mode");
            c.PickDeckForSlot(0); c._picked(-1007); c.PickDeckForSlot(0); c._picked(-1);
            Check(selected.NativePresetId == -1 && selected.DeckIndex == -1, "Same-as-player did not exit native mode");

            c.ChooseCountry(0, 1); c.PickDeckForSlot(0);
            Check(c._choices.Exists(choice => choice.Value == -1000) && c._choices.Exists(choice => choice.Value == -1042)
                && !c._choices.Exists(choice => choice.Value == -1007), "Nation filter exposed incompatible native profile");
            c._picked(-1042);
            Check(selected.NativePresetId == 42 && c.GetLaunchProblem() == null, "Matching native army was blocked by team nation");
            selected.NativePresetId = 7;
            Check(c.GetLaunchProblem()?.Contains("matching nation AI preset") == true, "Native country mismatch bypassed launch guard");
            selected.Closed = true;
            Check(c.GetLaunchProblem() == null, "Closed native slot should not block nation matching");
            selected.Closed = false; selected.NativePresetId = 42;
            c._profilePatchReady = false;
            Check(c.GetLaunchProblem()?.Contains("Native AI preset support") == true, "Native mode launched without profile hook");
            selected.Closed = true;
            Check(c.GetLaunchProblem() == null, "Closed native slot should not require profile hook");
            selected.Closed = false; c.PickDeckForSlot(0); c._picked(0);
            Check(selected.NativePresetId == -1 && c.GetLaunchProblem() == null, "Custom army mode did not recover missing native hook");
            c._profilePatchReady = true; selected.NativePresetId = 999;
            Check(c.GetLaunchProblem()?.Contains("available AI preset") == true, "Unavailable native profile allowed launch");
            selected.NativePresetId = -1;

            // Changing nation must change whichever army mode is actually active, not merely
            // an ignored custom-deck index underneath the selected native profile.
            int enemy = c._aiSlots.FindIndex(slot => slot.TeamSide == 1);
            c._aiSlots[enemy].NativePresetId = 0; c._aiSlots[enemy].DeckIndex = 2;
            c.ChooseCountry(1, 2);
            Check(c._aiSlots[enemy].NativePresetId == 7 && c.GetLaunchProblem() == null,
                "Nation change failed to replace the active native profile");
            NativeAiProfiles.Available.RemoveAll(profile => profile.CountryId == 1);
            c.ChooseCountry(1, 1);
            Check(c._aiSlots[enemy].NativePresetId == -1 && c._aiSlots[enemy].DeckIndex == 0,
                "Native army without matching profile did not switch to matching custom deck");
            c._decks.RemoveAt(1); c._deckNames.RemoveAt(1); c.PickTeamCountry(1);
            Check(c._choices.Exists(choice => choice.Value == 2), "Enemy nation should be available through native army even without custom deck");
            c._picked(2);
            Check(c._aiSlots.FindAll(slot => slot.TeamSide == 1).TrueForAll(slot => slot.NativePresetId == 7)
                && c.GetLaunchProblem() == null, "Native-only nation failed to replace custom army mode");
            c.PickTeamCountry(0);
            Check(!c._choices.Exists(choice => choice.Value == 2), "Friendly nation without a human deck should not be offered");

            NativeAiProfiles.Available.Add(new NativeAiProfileChoice { Id = 0, CountryId = 1, Name = "RU native zero" });
            c = PlanFixture(root); c.ChooseFormat(2); c.ChooseCountry(0, 1); c.ChooseCountry(1, 2);
            c._aiSlots[0].NativePresetId = 0; c._aiSlots[0].DeckIndex = 2;
            enemy = c._aiSlots.FindIndex(slot => slot.TeamSide == 1);
            c._aiSlots[enemy].NativePresetId = 7;
            var saved = c.CaptureSetup();
            Check(saved.AiPresets[3] == 0 && saved.AiPresets[20] == 7 && saved.AiDecks[3] == DeckKey(c._decks[2]),
                "Capture lost native-mode IDs or retained custom deck identity");
            c.SaveCurrent(); var disk = LobbySettings.Load(c._settingsPath).Scenarios[ScenarioKey(c._maps[0])];
            Check(disk.AiPresets[3] == 0 && disk.AiPresets[20] == 7, "Native ID/UID JSON roundtrip failed");
            c.ResetDefaults();
            Check(c._aiSlots.TrueForAll(slot => slot.NativePresetId == -1), "Scenario defaults retained native army mode");
            c._aiSlots.Reverse(); c.RestoreSetup(disk);
            Check(c._aiSlots.Find(slot => slot.PlayerUid == 3).NativePresetId == 0
                && c._aiSlots.Find(slot => slot.PlayerUid == 20).NativePresetId == 7,
                "Native profile restore used slot list position instead of UID");
            c._aiSlots.Reverse();
            c._presetName = "Native forces"; c.SavePreset(); c.ResetDefaults(); c.LoadPreset(); c._picked(c._choices[0].Value);
            Check(c._aiSlots[0].NativePresetId == 0 && c._aiSlots[enemy].NativePresetId == 7,
                "Named preset did not restore distinct native armies");
            c.SelectMap(1);
            Check(c._aiSlots.TrueForAll(slot => slot.NativePresetId == -1), "New scenario inherited native army mode");
            c.SelectMap(0);
            Check(c._aiSlots[0].NativePresetId == 0 && c._aiSlots[enemy].NativePresetId == 7,
                "Returning to scenario lost native profile IDs");

            NativeAiProfiles.Available.Clear(); c.ResetDefaults(); c.RestoreSetup(saved);
            Check(c._aiSlots.TrueForAll(slot => slot.NativePresetId == -1) && c._notice.Contains("unavailable"),
                "Missing native profile IDs failed to fall back with notice");
            Check(c._aiSlots[0].DeckIndex == 2, "Missing native profile discarded a valid saved custom fallback");
            Check(c.GetLaunchProblem() == null, "Valid retained custom armies should recover unavailable native profiles");
            var legacy = new SetupSnapshot { PlayerDeck = DeckKey(c._decks[0]), Difficulty = 3,
                AiPresets = null, ClosedSlots = null, TeamCountries = null };
            c.ResetDefaults(); c.RestoreSetup(legacy);
            Check(c._aiSlots.TrueForAll(slot => slot.NativePresetId == -1) && c.GetLaunchProblem() == null,
                "Old setup without native profile properties no longer works");
            NativeAiProfiles.Available.Clear();
        }
    }
}

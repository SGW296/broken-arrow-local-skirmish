using System;
using System.Collections.Generic;
using System.IO;

namespace BALocalSkirmish
{
    // Managed stand-ins exercise the production persistence/selection methods. Unity UI
    // and native game dispatch are deliberately outside this fixture's claims.
    enum ScenarioType { Skirmish, MultiplayerPVE, Singleplayer }
    class ScenarioSource { public string Folder, Name; public ScenarioType Type; }
    class IDeckDataModel { public int CountryID, Spec1ID, Spec2ID; public string FileName, Name; }
    class AiSlot { public int PlayerUid, DeckIndex = -1; }
    static class MelonEnvironment { public static string UserDataDirectory; }
    static class MelonLogger { public static void Warning(string value) { } }

    public partial class Core
    {
        LobbySettings _settings;
        string _settingsPath, _notice = "", _presetName = "My setup";
        bool _settingsWritable = true, _saveFailed, _hasDiff = true;
        int _filter, _optionPage, _mapIdx, _deckIdx, _diff = 3;
        readonly List<ScenarioSource> _maps = new List<ScenarioSource>();
        readonly List<IDeckDataModel> _decks = new List<IDeckDataModel>();
        readonly List<AiSlot> _aiSlots = new List<AiSlot>();
        readonly List<OptDef> _opts = new List<OptDef>();
        readonly Dictionary<int, int> _optVals = new Dictionary<int, int>();
        static ScenarioType TypeOf(ScenarioSource value) => value.Type;
        sealed class Choice { public int Value; public Choice(int value, string label, string detail) { Value = value; } }
        List<Choice> _choices;
        Action<int> _picked;
        void OpenPicker(string title, List<Choice> choices, Action<int> picked) { _choices = choices; _picked = picked; }
        void BuildUI() { }
        void UpdateLabels() { }
        void RefreshDecks() { _deckIdx = 0; foreach (var slot in _aiSlots) slot.DeckIndex = -1; }
        void RefreshOptions() { _diff = 3; foreach (var option in _opts) _optVals[option.Uid] = option.Default; }
        static int _passed;
        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            _passed++;
        }

        public static void Main()
        {
            string root = Path.Combine(AppContext.BaseDirectory, "fixtures", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root); MelonEnvironment.UserDataDirectory = root;
            Check(LobbyText.MapName("Airbase_Skirmish") == "Airbase", "Map name remained internal");
            Check(LobbyText.MapName("Custom_Name") == "Custom Name", "Map underscore spacing failed");
            Check(LobbyText.DeckName("RU_DEFAULT_DECK") == "Russia — Default deck", "Default deck label");
            Check(LobbyText.DeckName("My_Custom_DECK") == "My_Custom_DECK", "Custom deck name changed");
            Check(LobbyText.OptionName("ui_lobby_Daytime_Title", "Daytime") == "Time of day", "Daytime label");
            Check(LobbyText.OptionValue("ui_lobby_Daytime_Title", "ui_lobby_random_nation", "Random nation") == "Random", "Random daytime fix");
            Check(LobbyText.OptionValue("custom_option", "ui_lobby_random_nation", "Random nation") == "Random nation", "Context-free replacement changed a custom option");
            Check(LobbyText.OptionValue("ui_lobby_Size_Title", "ui_lobby_Size_Auto", "Size Auto") == "Automatic", "Automatic size label");

            var c = new Core(); c.ReadSettings();
            var a = new ScenarioSource { Name = "SameName", Folder = "built-in/A", Type = ScenarioType.Skirmish };
            var b = new ScenarioSource { Name = "SameName", Folder = "workshop/B", Type = ScenarioType.Skirmish };
            c._maps.AddRange(new[] { a, b, new ScenarioSource { Name = "PvE", Folder = "P", Type = ScenarioType.MultiplayerPVE },
                new ScenarioSource { Name = "Mission", Folder = "M", Type = ScenarioType.Singleplayer } });
            Check(ScenarioKey(a) != ScenarioKey(b), "Same-name scenarios collided");
            Check(c.FilteredMaps().Count == 2, "Default filter must select dedicated skirmish only");
            c._filter = 1; Check(c.FilteredMaps().Count == 1 && c.FilteredMaps()[0] == 2, "PvE filter");
            c._filter = 2; Check(c.FilteredMaps().Count == 1 && c.FilteredMaps()[0] == 3, "Campaign filter");
            c._filter = 3; Check(c.FilteredMaps().Count == 4, "All-local filter"); c._filter = 0;

            var human = new IDeckDataModel { FileName = "human.deck", Name = "Player", CountryID = 1 };
            var ai = new IDeckDataModel { FileName = "armor.deck", Name = "Armor", CountryID = 2 };
            c._decks.AddRange(new[] { human, ai });
            c._aiSlots.AddRange(new[] { new AiSlot { PlayerUid = 3, DeckIndex = 1 }, new AiSlot { PlayerUid = 5 } });
            var daytime = new OptDef { Uid = 221, IsEnum = true, Default = 3, EnumVals = new List<int> { 0, 1, 2, 3 } };
            var timer = new OptDef { Uid = 226, Min = 30, Max = 60, Step = 3, Default = 30 };
            var income = new OptDef { Uid = 227, Min = 150, Max = 600, Step = 25, Default = 300 };
            c._opts.AddRange(new[] { daytime, timer, income }); c.RefreshOptions();
            c._optVals[221] = 2; c._optVals[226] = 45; c._optVals[227] = 450; c._diff = 1;
            Check(timer.Accepts(30) && timer.Accepts(60) && timer.Accepts(45), "Valid numeric boundaries");
            Check(!timer.Accepts(31) && !timer.Accepts(61) && !timer.Accepts(int.MinValue), "Invalid numeric range/step accepted");
            Check(daytime.Accepts(3) && !daytime.Accepts(4), "Enum membership");
            var sparse = new OptDef { IsEnum = true, EnumVals = new List<int> { 2, 17, 90 } };
            Check(sparse.Accepts(17) && !sparse.Accepts(1), "Enum value confused with UI index");
            var fixedOption = new OptDef { Min = 0, Max = 0, Default = 42 };
            Check(fixedOption.Accepts(42) && !fixedOption.Accepts(0), "Missing numeric range must preserve only the scenario default");

            SetupSnapshot saved = c.CaptureSetup();
            c.SaveCurrent();
            string persisted = File.ReadAllText(c._settingsPath);
            var roundtrip = LobbySettings.Load(c._settingsPath);
            Check(roundtrip.Scenarios[ScenarioKey(a)].AiDecks[3] == DeckKey(ai), "JSON lost AI UID/deck identity");
            Check(roundtrip.Scenarios[ScenarioKey(a)].Options[226] == 45, "JSON lost option UID/value");
            Check(!File.Exists(c._settingsPath + ".tmp"), "Successful atomic save left temporary file");
            c._decks.Reverse(); c._aiSlots.Reverse(); c.RefreshDecks(); c.RefreshOptions(); c.RestoreSetup(saved);
            Check(c._deckIdx == 1, "Deck reorder changed restored human selection");
            Check(c._aiSlots.Find(s => s.PlayerUid == 3).DeckIndex == 0, "Slot reorder changed restored AI selection");
            Check(c._aiSlots.Find(s => s.PlayerUid == 5).DeckIndex == -1, "Default AI no longer follows player");
            Check(c._diff == 1 && c._optVals[226] == 45 && c._optVals[221] == 2, "Restored difficulty/options differ");
            c._opts.Add(new OptDef { Uid = 999, Min = 0, Max = 100, Step = 1, Default = 25 });
            c.RefreshOptions(); c.RestoreSetup(saved);
            Check(c._optVals[999] == 25, "New option did not retain scenario default");

            c._presetName = "Armor practice"; c.SavePreset();
            c._presetName = "ARMOR PRACTICE"; c.SavePreset();
            Check(c._settings.Presets.Count == 1, "Same-named preset did not replace scoped preset");
            c._presetName = "  "; c.SavePreset(); Check(c._settings.Presets.Count == 1, "Empty preset accepted");
            c.SelectMap(1);
            Check(c._optionPage == 0, "Map change did not reset option page");
            Check(c._deckIdx == 0 && c._diff == 3 && c._optVals[226] == 30, "New map inherited old settings");
            c.LoadPreset(); Check(c._choices.Count == 0, "Preset leaked across scenarios");
            c._presetName = "Armor practice"; c.SavePreset();
            Check(c._settings.Presets.Count == 2, "Same name should work for another scenario");
            c.SelectMap(0); Check(c._optVals[226] == 45 && c._deckIdx == 1, "Returning to map lost remembered choices");
            c._optVals[226] = 60; c.LoadPreset(); Check(c._choices.Count == 1, "Wrong number of scenario presets");
            c._picked(c._choices[0].Value); Check(c._optVals[226] == 45, "Preset load failed");

            c._decks.Remove(ai); c.RefreshDecks(); c.RefreshOptions(); c.RestoreSetup(saved);
            Check(c._deckIdx == 0 && c._aiSlots.TrueForAll(s => s.DeckIndex == -1), "Missing deck fallback selected another deck");
            Check(c._notice.Contains("unavailable"), "Missing choices were silently discarded");
            c._decks.Add(human); Check(c.FindDeck(DeckKey(human)) == -1, "Ambiguous deck identity silently selected"); c._decks.RemoveAt(1);
            saved.Options[226] = 31; saved.Options[221] = 100; saved.Options[123456] = 8; saved.Difficulty = 9;
            saved.AiDecks[999] = DeckKey(human);
            c.RefreshDecks(); c.RefreshOptions(); c.RestoreSetup(saved);
            Check(c._optVals[226] == 30 && c._optVals[221] == 3 && c._diff == 3, "Invalid saved values bypassed validation");
            Check(!c._optVals.ContainsKey(123456), "Removed option was injected");

            c.SaveCurrent(); Check(File.ReadAllText(c._settingsPath) != persisted, "Second save did not replace old JSON");
            string valid = File.ReadAllText(c._settingsPath);
            File.WriteAllText(c._settingsPath, "{bad json");
            var corrupt = new Core(); corrupt.ReadSettings(); corrupt.SaveCurrent();
            Check(!corrupt._settingsWritable && File.ReadAllText(c._settingsPath) == "{bad json", "Corrupt settings overwritten");
            File.WriteAllText(c._settingsPath, "{\"SchemaVersion\":2}");
            var newer = new Core(); newer.ReadSettings(); newer.SaveCurrent();
            Check(!newer._settingsWritable && File.ReadAllText(c._settingsPath).Contains(":2"), "Newer schema overwritten");
            File.WriteAllText(c._settingsPath, valid);
            var unknownFilter = new LobbySettings { ScenarioFilter = 100 }; unknownFilter.Save(c._settingsPath);
            Check(LobbySettings.Load(c._settingsPath).ScenarioFilter == 3, "Invalid filter not bounded");
            string directory = Path.Combine(root, "existing-directory"); Directory.CreateDirectory(directory);
            c._settingsPath = directory; c.WriteSettings();
            Check(c._saveFailed && Directory.Exists(directory), "Save error was hidden or destroyed original target");
            c._settingsPath = Path.Combine(root, "recovered.json"); c.WriteSettings();
            Check(!c._saveFailed && File.Exists(c._settingsPath), "Successful retry did not clear save error");
            Console.WriteLine($"PASS: {_passed} lobby checks: labels, filtering, stable identities, map switching, presets, bounds, JSON roundtrip and failure preservation.");
        }
    }
}

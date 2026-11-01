using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace BALocalSkirmish
{
    // No game dependencies: the saved format stores identities, never UI list positions.
    public sealed class SetupSnapshot
    {
        public string PlayerDeck { get; set; }
        public int Difficulty { get; set; }
        public Dictionary<int, int> Options { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, string> AiDecks { get; set; } = new Dictionary<int, string>();
        public List<int> ClosedSlots { get; set; } = new List<int>();
        public Dictionary<int, int> TeamCountries { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, int> AiPresets { get; set; } = new Dictionary<int, int>();
    }

    public sealed class SavedPreset
    {
        public string Name { get; set; }
        public string Scenario { get; set; }
        public SetupSnapshot Setup { get; set; }
    }

    public sealed class LobbySettings
    {
        public int SchemaVersion { get; set; } = 1;
        public int ScenarioFilter { get; set; }
        public string LastScenario { get; set; }
        public Dictionary<string, SetupSnapshot> Scenarios { get; set; } = new Dictionary<string, SetupSnapshot>();
        public List<SavedPreset> Presets { get; set; } = new List<SavedPreset>();

        public static LobbySettings Load(string path)
        {
            if (!File.Exists(path)) return new LobbySettings();
            var data = JsonSerializer.Deserialize<LobbySettings>(File.ReadAllText(path));
            if (data == null || data.SchemaVersion != 1 || data.Scenarios == null || data.Presets == null)
                throw new InvalidDataException("Unknown or incomplete lobby settings format.");
            data.ScenarioFilter = Math.Max(0, Math.Min(3, data.ScenarioFilter));
            return data;
        }

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, path, true);
        }
    }

    public static class LobbyText
    {
        public static string MapName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Unnamed scenario";
            string name = raw.EndsWith("_Skirmish", StringComparison.OrdinalIgnoreCase)
                ? raw.Substring(0, raw.Length - 9) : raw;
            return name.Replace('_', ' ').Trim();
        }

        public static string DeckName(string raw)
        {
            if (raw == "RU_DEFAULT_DECK") return "Russia — Default deck";
            if (raw == "US_DEFAULT_DECK" || raw == "USA_DEFAULT_DECK") return "USA — Default deck";
            return string.IsNullOrWhiteSpace(raw) ? "Unnamed deck" : raw;
        }

        public static string OptionName(string raw, string fallback)
        {
            switch ((raw ?? "").ToLowerInvariant())
            {
                case "ui_lobby_size_title": return "Map size";
                case "ui_lobby_daytime_title": return "Time of day";
                case "ui_lobby_timer_title": return "Time limit";
                case "ui_lobby_income_title": return "Income";
                default: return fallback;
            }
        }

        public static string OptionValue(string option, string raw, string fallback)
        {
            // This reused key is in the shipped v1.2.0 daytime dropdown. Only change its
            // presentation in that context; the scenario still receives its original integer.
            if (string.Equals(option, "ui_lobby_Daytime_Title", StringComparison.OrdinalIgnoreCase))
            {
                switch ((raw ?? "").ToLowerInvariant())
                {
                    case "ui_lobby_random_nation": return "Random";
                    case "ui_set_hangar_evening": return "Evening";
                    case "ui_set_hangar_day": return "Day";
                    case "ui_set_hangar_night": return "Night";
                }
            }
            if (string.Equals(option, "ui_lobby_Size_Title", StringComparison.OrdinalIgnoreCase))
            {
                switch ((raw ?? "").ToLowerInvariant())
                {
                    case "ui_lobby_size_auto": return "Automatic";
                    case "ui_lobby_size_large": return "Large";
                    case "ui_lobby_size_small": return "Small";
                }
            }
            return fallback;
        }
    }

    public class OptDef
    {
        public int Uid, Order;
        public string Label, RawName;
        public bool IsEnum;
        public List<int> EnumVals = new List<int>();
        public List<string> EnumLabels = new List<string>();
        public int Min, Max, Step, Default;

        public bool Accepts(int value)
        {
            if (IsEnum) return EnumVals.Contains(value);
            if (Max <= Min) return value == Default;
            return value >= Min && value <= Max && ((long)value - Min) % Math.Max(1, Step) == 0;
        }

        public string Help
        {
            get
            {
                switch ((RawName ?? "").ToLowerInvariant())
                {
                    case "ui_lobby_daytime_title": return "Time of day: Random uses the scenario's random choice.";
                    case "ui_lobby_size_title": return "Map size: Automatic lets the scenario choose the play area.";
                    case "ui_lobby_timer_title": return "Time limit: scenario timer value; units are not declared by its metadata.";
                    case "ui_lobby_income_title": return "Income: scenario-defined amount; its metadata does not declare units or a rate.";
                    default: return Label + ": supplied by this scenario.";
                }
            }
        }
    }
}

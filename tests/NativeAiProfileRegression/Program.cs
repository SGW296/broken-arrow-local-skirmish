using System;
using System.Collections.Generic;
using System.IO;
using BALocalSkirmish;
using Il2CppBrokenArrow.Client.Ecs.Configs;
using Il2CppBrokenArrow.ScriptEngine.Nodes.Functions;
using Il2CppBrokenArrow.MissionEditor.MissionResolver;

namespace UnityEngine { public class Sprite { } }
namespace MelonLoader.Utils { static class MelonEnvironment { public static string GameRootDirectory = Path.GetFullPath("fake-game"); } }
namespace Il2CppBrokenArrow.Shared.Ecs.Services {
    static class Session { public static bool TryGetService<T>(out T value, bool unused) where T : class { value = null; return false; } }
}
namespace Il2CppBrokenArrow.Shared.Ecs.Localization {
    class LocalizationService { public bool TryResolveSimpleKey(string key, out string value, bool unused) { value = null; return false; } }
}
namespace Il2CppBrokenArrow.Client.Ecs.Configs {
    public class PlayerInfo { public int UID, NetID; public bool IsBot = true; public IntPtr Pointer; }
    public class GameSessionContext {
        public PlayerInfo CurrentPlayer;
        public Dictionary<int, PlayerInfo> Players = new Dictionary<int, PlayerInfo>();
        public PlayerInfo GetPlayer(int uid) => Players.TryGetValue(uid, out var p) ? p : null;
    }
    public class AiProfile {
        public int Id, Country, Spec1Id, Spec2Id; public string TechName, DisplayNameLoc;
        public UnityEngine.Sprite AvatarPreview, Spec1Preview, Spec2Preview;
    }
    public class AiConfig { public List<AiProfile> AiProfilePresets = new List<AiProfile>(); }
    public class GameConfig { public static GameConfig Instance; public AiConfig AiConfig; }
}
namespace Il2CppBrokenArrow.ScriptEngine.Nodes.Functions {
    public class NodeGetSlotData { public PlayerInfo _slotSoloPlayer; public bool IsAi = true, IsPlayer; public int AiPresetId; }
}
namespace Il2CppBrokenArrow.MissionEditor.MissionResolver {
    public enum ScenarioType { Skirmish, Singleplayer }
    public class Meta { public string Author = "Steel Balalaika", GameVersion = "1.2.0"; public ScenarioType Type; }
    public class ScenarioSource { public bool IsWorkshop; public string Folder; public Meta MetaFile = new Meta(); }
}

static class Program
{
    static int checks;
    static void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
    static void Main()
    {
        var human = new PlayerInfo { UID = 2, Pointer = (IntPtr)2, NetID = 1 };
        var ai = new PlayerInfo { UID = 77, Pointer = (IntPtr)77 };
        var session = new GameSessionContext { CurrentPlayer = human };
        session.Players[2] = human; session.Players[77] = ai;
        var node = new NodeGetSlotData { _slotSoloPlayer = ai };
        var choices = new Dictionary<int, int> { [77] = 8, [2] = 11 };
        Check(NativeAiProfileBridge.ApplyPreset(node, session, choices, 2) && node.AiPresetId == 8, "Native UID profile binding failed");
        Check(ReferenceEquals(node._slotSoloPlayer, ai) && ai.IsBot, "Player identity was mutated");
        choices[77] = 11; Check(NativeAiProfileBridge.ApplyPreset(node, session, choices, 2) && node.AiPresetId == 11, "Profile changes not reflected");
        choices[77] = 12; Check(!NativeAiProfileBridge.ApplyPreset(node, session, choices, 2) && node.AiPresetId == 11, "Unverified profile accepted");
        choices[77] = -1; Check(!NativeAiProfileBridge.ApplyPreset(node, session, choices, 2), "Custom deck marker accepted as native profile");
        choices[77] = 0; Check(NativeAiProfileBridge.ApplyPreset(node, session, choices, 2) && node.AiPresetId == 0, "Native profile zero rejected");
        node._slotSoloPlayer = human; Check(!NativeAiProfileBridge.ApplyPreset(node, session, choices, 2), "Human profile overwritten");
        node._slotSoloPlayer = ai; ai.IsBot = false; Check(!NativeAiProfileBridge.ApplyPreset(node, session, choices, 2), "Closed/nonbot slot overwritten");
        ai.IsBot = true; ai.NetID = 9; Check(!NativeAiProfileBridge.ApplyPreset(node, session, choices, 2), "Network/human player overwritten"); ai.NetID = 0;
        node.IsAi = false; Check(!NativeAiProfileBridge.ApplyPreset(node, session, choices, 2), "Inactive slot-data overwritten"); node.IsAi = true;
        node.IsPlayer = true; Check(!NativeAiProfileBridge.ApplyPreset(node, session, choices, 2), "Player node overwritten"); node.IsPlayer = false;
        node._slotSoloPlayer = new PlayerInfo { UID = 77, Pointer = (IntPtr)123 };
        Check(!NativeAiProfileBridge.ApplyPreset(node, session, choices, 2), "Different native session object accepted");
        node._slotSoloPlayer = ai; Check(!NativeAiProfileBridge.ApplyPreset(node, session, choices, 99), "Different human/session accepted");
        choices.Remove(77); Check(!NativeAiProfileBridge.ApplyPreset(node, session, choices, 2), "Unplanned UID overwritten");
        Check(!NativeAiProfileBridge.ApplyPreset(null, session, choices, 2) && !NativeAiProfileBridge.ApplyPreset(node, null, choices, 2)
            && !NativeAiProfileBridge.ApplyPreset(node, session, null, 2), "Null input accepted");

        string root = MelonLoader.Utils.MelonEnvironment.GameRootDirectory;
        var scenario = new ScenarioSource { Folder = Path.Combine(root, "BrokenArrow_Data", "StreamingAssets", "SharedMissions", "Airbase_Skirmish") };
        Check(NativeAiProfiles.IsSupportedScenario(scenario), "Verified built-in rejected");
        scenario.IsWorkshop = true; Check(!NativeAiProfiles.IsSupportedScenario(scenario), "Workshop graph accepted"); scenario.IsWorkshop = false;
        string valid = scenario.Folder; scenario.Folder = Path.Combine(root, "Mods", "Scenarios", "Airbase_Skirmish");
        Check(!NativeAiProfiles.IsSupportedScenario(scenario), "Same-name custom graph accepted");
        scenario.Folder = valid; scenario.MetaFile.GameVersion = "1.2.1"; Check(!NativeAiProfiles.IsSupportedScenario(scenario), "Unverified version accepted");
        scenario.MetaFile.GameVersion = "1.2.0"; scenario.MetaFile.Author = "Custom"; Check(!NativeAiProfiles.IsSupportedScenario(scenario), "Custom author accepted");
        scenario.MetaFile.Author = "Steel Balalaika"; scenario.MetaFile.Type = ScenarioType.Singleplayer;
        Check(!NativeAiProfiles.IsSupportedScenario(scenario), "Campaign accepted"); scenario.MetaFile.Type = ScenarioType.Skirmish;
        GameConfig.Instance = new GameConfig { AiConfig = new AiConfig() };
        var art = new UnityEngine.Sprite();
        GameConfig.Instance.AiConfig.AiProfilePresets.AddRange(new[] {
            new AiProfile { Id = 11, TechName = "Native_Profile", Country = 1, Spec1Id = 10, Spec2Id = 12, AvatarPreview = art },
            new AiProfile { Id = 0, TechName = "First" }, new AiProfile { Id = 0, TechName = "Duplicate" },
            new AiProfile { Id = 12, TechName = "Unverified" }
        });
        var profiles = NativeAiProfiles.GetAvailable(scenario);
        Check(profiles.Count == 2 && profiles[0].Id == 0 && profiles[1].Id == 11, "Available IDs not filtered/sorted/unique");
        Check(profiles[1].Name == "Native Profile" && profiles[1].CountryId == 1 && profiles[1].Spec2Id == 12, "Native metadata lost");
        Check(ReferenceEquals(profiles[1].Avatar, art), "Shared artwork copied or replaced");
        scenario.IsWorkshop = true; Check(NativeAiProfiles.GetAvailable(scenario).Count == 0, "Unverified scenario exposed native profiles");
        Console.WriteLine($"PASS: {checks} native AI profile checks: UID scoping, human/closed protection, verified scenario/catalog and borrowed artwork.");
    }
}

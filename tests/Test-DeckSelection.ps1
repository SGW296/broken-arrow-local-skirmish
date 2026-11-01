param([string]$ExportHarness)
# Runs the production selection methods with managed stand-ins for game objects.
# This checks selection, lifecycle, and scripted spawn conversion without starting Unity/IL2CPP.
$ErrorActionPreference = 'Stop'
$source = Get-Content (Join-Path $PSScriptRoot '../src/LocalSkirmish.cs') -Raw

function Get-MethodSource([string]$signature) {
    $start = $source.IndexOf($signature, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Missing production method: $signature" }
    $open = $source.IndexOf('{', $start)
    $depth = 0
    for ($i = $open; $i -lt $source.Length; $i++) {
        if ($source[$i] -eq '{') { $depth++ }
        if ($source[$i] -eq '}') { $depth-- }
        if ($depth -eq 0) { return $source.Substring($start, $i - $start + 1) }
    }
    throw "Unclosed production method: $signature"
}

$methods = @(
    'static ScenarioType TypeOf('
    'void RefreshDecks('
    'bool IsSkirmishMap('
    'string AiDeckText('
    'void RefreshAiSlots('
    'void ClearBattleAiDecks('
    'void PrepareDecks('
    'static int HumanUidOf('
    'internal void ApplyClosedSlotPlan('
    'internal void ApplyNativeAiProfile('
    'bool MatchesLaunchingScenario('
    'bool MatchesBattleScenario('
    'internal void StopDeckOverrides('
    'internal void BeginDeckLoad('
    'internal void OverrideDeckAssignment('
    'internal void OverrideAiSpawn('
    'static void ReplaceSpawnUnit('
    'static DeckSlotModel PickSpawnSlot('
    'static UnitModData MakeSpawnUnit('
    'void CycleAiDeck('
    'void CycleAiPlayer('
) | ForEach-Object { Get-MethodSource $_ }
$mainSource = $source
$source = Get-Content (Join-Path $PSScriptRoot '../src/MatchDiagnostics.cs') -Raw
$methods += @('internal void TraceSpawnDecision(', 'static Dictionary<int, int> CountSpawnPayload(', 'void DiagnosticFailure(', 'void FlushWaveDiagnostics(') | ForEach-Object { Get-MethodSource $_ }
$source = $mainSource
$diagnosticsHelper = (Get-Content (Join-Path $PSScriptRoot '../src/SkirmishWaveDiagnostics.cs') -Raw) -replace '(?m)^using .+;\r?$', ''

$prefix = @'
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BALocalSkirmish;
namespace Il2CppSystem.Collections.Generic { class List<T> : System.Collections.Generic.List<T> {} }
namespace DeckSelectionRegression {
enum ScenarioType { Singleplayer, MultiplayerPVE, MultiplayerPVP, Skirmish }
interface IDeckDataModel { string Name { get; } Dictionary<UnitCategoryType, DeckSlotModel[]> GetSlots { get; } }
class Deck : IDeckDataModel {
    public string Name { get; set; }
    public bool Fail;
    public Dictionary<UnitCategoryType, DeckSlotModel[]> Slots = new Dictionary<UnitCategoryType, DeckSlotModel[]>();
    public Dictionary<UnitCategoryType, DeckSlotModel[]> GetSlots { get { if (Fail) throw new Exception("Unreadable deck"); return Slots; } }
}
enum UnitCategoryType { Recon, Infantry, Vehicles, Support, Logistic, Helicopters, Aircrafts }
class UnitData { public int Type, Role; public UnitCategoryType CategoryType; }
class DeckModData { public int ModID, OptionID; }
struct ModDataItem { public int ModID, OptionID; public ModDataItem(int m, int o) { ModID=m; OptionID=o; } }
class DeckSlotModel {
    public int UnitID, UnitSkinId, Count=1, TransportID, TransportSkinId, TransportCount=1;
    public UnitCategoryType Category;
    public bool IsValid { get { return UnitID>0; } }
    public bool IsValidTransport { get { return TransportID>0; } }
    public Il2CppSystem.Collections.Generic.List<DeckModData> ModList, TransportModList;
    public UnitData Data = new UnitData(), TransportData = new UnitData();
    public UnitData GetUnitData() { return Data; }
    public UnitData GetTransportUnitData() { return TransportData; }
}
class CargoData { public Il2CppSystem.Collections.Generic.List<UnitModData> Slots; }
class UnitModData {
    public int UnitID, SkinId;
    public UnitCategoryType Category;
    public Il2CppSystem.Collections.Generic.List<ModDataItem> Items;
    public CargoData Cargo;
}
class Il2CppReferenceArray<T> : List<T> {
    public Il2CppReferenceArray(T[] values) : base(values) {}
    public Il2CppReferenceArray(int count) { for(int i=0;i<count;i++) Add(default(T)); }
    public int Length { get { return Count; } }
}
class MultiUnits {
    public Il2CppReferenceArray<UnitModData> Units;
    public int TotalUnitsCount { get { int n=Units.Count; foreach(var u in Units) n+=u.Cargo.Slots.Count; return n; } }
}
class UnitObject { public int Count; public UnitObject(int count) { Count=count; } }
class PlayerLink { public int DataUID; }
class NodeSpawnMultiUnits {
    public PlayerLink PlayerOwner;
    public MultiUnits UnitInfo, NativeRequest;
    public UnitObject Units;
    public int ExpectedCallbacks;
    public bool Completed;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void OnActivated() {
        Completed=false;
        if(UnitInfo.Units.Length==0) { Completed=true; return; }
        ExpectedCallbacks=UnitInfo.TotalUnitsCount;
        Units=new UnitObject(ExpectedCallbacks);
        NativeRequest=UnitInfo;
    }
    public void FinishAsync() { Completed=NativeRequest.TotalUnitsCount==ExpectedCallbacks; }
}
class DataBaseService {
    public Dictionary<int, UnitData> Units = new Dictionary<int, UnitData>();
    public UnitData GetUnitById(int id, bool copy) { return Units.TryGetValue(id,out var u)?u:null; }
}
class Session {
    public static DataBaseService Database = new DataBaseService();
    public static void TryGetService<T>(out T service, bool unused) where T:class { service=Database as T; }
}
class Meta { public ScenarioType Type; public List<Team> Teams = new List<Team>(); }
class Team { public int TeamSide; public string TeamName; public List<Slot> Slots = new List<Slot>(); }
class Slot { public bool IsBot = true, Mandatory = false; public int BotId; public string PlayerName; }
class ScenarioSource { public Meta MetaFile; public string Folder, Name; }
class ScenariosService { public static ScenarioSource ActiveScenario; }
class ISceneLoadManager { public static ISceneLoadManager Instance; public ScenarioSource LoadScenario; }
class DeckService {
    public List<IDeckDataModel> Available;
    public bool Fail;
    public List<IDeckDataModel> GetAvailableDecksForScenario(ScenarioSource s, int slot) {
        if (Fail) throw new Exception("Simulated deck load failure");
        return Available;
    }
}
static class PreloadSharedPlayerDeck {
    public static bool IsMissionRestart;
    public static IDeckDataModel ScenarioStartDeck, Alpha, Bravo;
}
static class SharedPlayerDeck {
    public static readonly Dictionary<int, IDeckDataModel> Assigned = new Dictionary<int, IDeckDataModel>();
    public static void SetDeck(int uid, IDeckDataModel input, out IDeckDataModel output) {
        // Simulate the native hook dispatch; the actual selection method is compiled below.
        Checks.Active.OverrideDeckAssignment(uid, ref input);
        output = new Deck { Name = input.Name }; // game creates a per-player clone
        Assigned[uid] = output;
    }
}
class Player { public int UID, NetID = 0; public bool IsBot = true; public string PlayerName = "AI"; }
class GameSessionContext {
    public Player CurrentPlayer;
    public readonly Dictionary<int, Player> Players = new Dictionary<int, Player>();
    public Player GetPlayer(int uid) { return Players.TryGetValue(uid, out var p) ? p : null; }
}
static class SlotRuntimeBridge {
    public static int ApplyClosedSlots(GameSessionContext session, IEnumerable<int> closed, int human) {
        int count=0;
        foreach(int uid in closed) {
            var player=session.GetPlayer(uid);
            if(uid!=human && (session.CurrentPlayer==null || uid!=session.CurrentPlayer.UID) && player!=null && player.IsBot) { player.IsBot=false; count++; }
        }
        return count;
    }
}
class ScenarioController {
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void LoadTeamsAndPlayers(GameSessionContext session) {}
}
class NodeGetSlotData {
    public Player _slotSoloPlayer=null;
    public int AiPresetId=-1;
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void InitSolo() {}
}
static class NativeAiProfileBridge {
    public static bool ApplyPreset(NodeGetSlotData node, GameSessionContext session, IReadOnlyDictionary<int,int> plan, int human) {
        var player=node._slotSoloPlayer;
        if(player==null || !player.IsBot || player.UID==human || !ReferenceEquals(session.GetPlayer(player.UID),player)
            || !plan.TryGetValue(player.UID,out int id)) return false;
        node.AiPresetId=id; return true;
    }
}
class Game { public GameSessionContext GameSession = new GameSessionContext(); }
class MissionLoader {
    public Game _game = new Game();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void LoadPlayersDeck() {}
}
static class MelonLogger { public static void Error(string message) {} public static void Msg(string message) {} public static void Warning(string message) {} }
'@
$prefix += Get-MethodSource 'class AiSlot'
$prefix += (Get-MethodSource 'static class AiSpawnPatch').Replace('static void Prefix(', 'public static void Prefix(').Replace('static void Postfix(', 'public static void Postfix(').Replace('Core.Instance', 'Checks.Active')
$mainSource = $source
$source = Get-Content (Join-Path $PSScriptRoot '../src/ClosedSlotHooks.cs') -Raw
$prefix += (Get-MethodSource 'static class ClosedSlotInitializationPatch').Replace('Core.Instance', 'Checks.Active')
$source = Get-Content (Join-Path $PSScriptRoot '../src/NativeAiPresetHooks.cs') -Raw
$prefix += (Get-MethodSource 'static class NativeAiPresetInitializationPatch').Replace('Core.Instance', 'Checks.Active')
$source = $mainSource
$prefix += @'
public class Checks {
    readonly List<ScenarioSource> _maps = new List<ScenarioSource>();
    readonly List<IDeckDataModel> _decks = new List<IDeckDataModel>();
    readonly List<string> _deckNames = new List<string>();
    readonly List<AiSlot> _aiSlots = new List<AiSlot>();
    readonly Dictionary<int, IDeckDataModel> _battleAiDecks = new Dictionary<int, IDeckDataModel>();
    readonly HashSet<int> _battleClosedSlots = new HashSet<int>();
    readonly Dictionary<int,int> _battleAiProfiles = new Dictionary<int,int>();
    int _battleHumanUid = -1;
    readonly Dictionary<int, int> _aiWaveCounts = new Dictionary<int, int>();
    readonly SkirmishWaveDiagnostics _waveDiagnostics = new SkirmishWaveDiagnostics();
    bool _diagnosticFailureLogged;
    static readonly Random _rng = new Random(123);
    internal static Checks Active;
    ScenarioSource _battleScenario;
    IDeckDataModel _battlePlayerDeck;
    GameSessionContext _deckSession;
    bool _deckOverridesActive;
    bool _aiLoadPending, _aiRestartAllowed;
    int _mapIdx, _deckIdx, _aiSlotIdx;
    static DeckService service;
    static DeckService DecksSvc() { return service; }
    void UpdateLabels() {}
    void LogRuntimeSlotSnapshot(GameSessionContext session) {}
'@
$suffix = @'
    static int passed;
    static void Check(bool condition, string message) {
        if (!condition) throw new Exception(message);
        passed++;
    }
    static void CheckHuman(IDeckDataModel human) {
        Check(ReferenceEquals(PreloadSharedPlayerDeck.ScenarioStartDeck, human), "Human preload changed");
        Check(ReferenceEquals(PreloadSharedPlayerDeck.Alpha, human), "Alpha default changed");
        Check(ReferenceEquals(PreloadSharedPlayerDeck.Bravo, human), "Bravo default changed");
    }
    static UnitModData Request(int id, UnitCategoryType category) {
        return MakeSpawnUnit(id, category, 0, null);
    }
    static NodeSpawnMultiUnits Wave(int uid, params UnitModData[] units) {
        return new NodeSpawnMultiUnits { PlayerOwner = new PlayerLink { DataUID=uid }, UnitInfo=new MultiUnits { Units=new Il2CppReferenceArray<UnitModData>(units) } };
    }
    static MultiUnits Activate(NodeSpawnMultiUnits node) {
        var original=node.UnitInfo;
        MultiUnits saved;
        AiSpawnPatch.Prefix(node, out saved);
        var replacement=node.UnitInfo;
        node.OnActivated();
        AiSpawnPatch.Postfix(node,saved);
        Check(ReferenceEquals(node.UnitInfo,original), "Hook did not restore shared node input");
        if(replacement.Units.Count>0) node.FinishAsync();
        Check(node.Completed, "Changed unit count left a wave waiting for callbacks");
        return replacement;
    }
    static void SpawnChecks(Checks c, MissionLoader loader, ScenarioSource scenario, Deck human, Deck armor, Deck air) {
        var ground = new DeckSlotModel { UnitID=101, Category=UnitCategoryType.Vehicles, UnitSkinId=7,
            Data=new UnitData { Type=4, Role=11 }, ModList=new Il2CppSystem.Collections.Generic.List<DeckModData> { new DeckModData {ModID=1,OptionID=2} } };
        var infantry = new DeckSlotModel { UnitID=102, Category=UnitCategoryType.Infantry, UnitSkinId=8,
            TransportID=103, TransportSkinId=9, Data=new UnitData { Type=2, Role=30 },
            TransportData=new UnitData { Type=4, CategoryType=UnitCategoryType.Vehicles },
            ModList=new Il2CppSystem.Collections.Generic.List<DeckModData> { new DeckModData {ModID=3,OptionID=4} },
            TransportModList=new Il2CppSystem.Collections.Generic.List<DeckModData> { new DeckModData {ModID=5,OptionID=6} } };
        var other = new DeckSlotModel { UnitID=201, Category=UnitCategoryType.Vehicles };
        var own = new DeckSlotModel { UnitID=301, Category=UnitCategoryType.Vehicles };
        armor.Slots[UnitCategoryType.Vehicles]=new[] {ground};
        armor.Slots[UnitCategoryType.Infantry]=new[] {infantry};
        air.Slots[UnitCategoryType.Vehicles]=new[] {other};
        human.Slots[UnitCategoryType.Vehicles]=new[] {own};
        var stock=Request(901,UnitCategoryType.Vehicles);
        var source=Wave(3,stock);
        Check(ReferenceEquals(Activate(source),source.UnitInfo), "Inactive hook modified a wave");
        Check(c._waveDiagnostics.RequestCount==0, "Inactive diagnostics recorded an unrelated wave");
        c.PrepareDecks(scenario,human); c.BeginDeckLoad(loader);
        var a=Activate(source);
        Check(c._waveDiagnostics.RequestCount==1, "Active custom wave was not recorded");
        Check(a.Units.Count==1 && a.Units[0].UnitID==101, "Scripted wave bypassed selected AI deck");
        Check(a.Units[0].SkinId==7 && a.Units[0].Items[0].OptionID==2, "Selected skin/upgrades lost");
        Check(Activate(Wave(5,stock)).Units[0].UnitID==201, "Second AI inherited first AI roster");
        Check(Activate(Wave(10,stock)).Units[0].UnitID==301, "Same-as-player AI ignored player deck");
        var playerWave=Wave(2,stock);
        Check(ReferenceEquals(Activate(playerWave),playerWave.UnitInfo), "Human scripted unit changed");
        var unknownWave=Wave(999,stock);
        Check(ReferenceEquals(Activate(unknownWave),unknownWave.UnitInfo), "Unknown owner changed");
        loader._game.GameSession.Players[5].IsBot=false;
        var nonbot=Wave(5,stock);
        Check(ReferenceEquals(Activate(nonbot),nonbot.UnitInfo), "Non-bot wave changed");
        loader._game.GameSession.Players[5].IsBot=true;
        var loaded=Request(902,UnitCategoryType.Vehicles);
        loaded.Cargo.Slots.Add(Request(903,UnitCategoryType.Infantry));
        loaded.Cargo.Slots.Add(Request(904,UnitCategoryType.Infantry));
        var payloadCounts=CountSpawnPayload(new MultiUnits {Units=new Il2CppReferenceArray<UnitModData>(new[]{loaded})});
        Check(payloadCounts[(int)UnitCategoryType.Vehicles]==1 && payloadCounts[(int)UnitCategoryType.Infantry]==2,
            "Diagnostic payload counts omitted cargo or changed categories");
        var transportWave=Wave(3,loaded);
        var b=Activate(transportWave);
        Check(b.Units.Count==2 && b.TotalUnitsCount==4 && transportWave.ExpectedCallbacks==4, "Transport count mismatch");
        Check(b.Units.TrueForAll(u=>u.UnitID==103 && u.Cargo.Slots[0].UnitID==102), "Stock transport/cargo survived");
        Check(b.Units[0].SkinId==9 && b.Units[0].Items[0].OptionID==6, "Transport loadout lost");
        Check(b.Units[0].Cargo.Slots[0].SkinId==8 && b.Units[0].Cargo.Slots[0].Items[0].OptionID==4, "Passenger loadout lost");
        Check(!ReferenceEquals(b.Units[0],b.Units[1]) && !ReferenceEquals(b.Units[0].Cargo.Slots[0],b.Units[1].Cargo.Slots[0]), "Spawn copies share mutable objects");
        b.Units[0].Items.Clear(); b.Units[0].Cargo.Slots.Clear();
        Check(b.Units[1].Items.Count==1 && b.Units[1].Cargo.Slots.Count==1, "Mutation crossed spawn copies");
        Check(loaded.UnitID==902 && loaded.Cargo.Slots.Count==2 && infantry.ModList[0].OptionID==4, "Source pool or saved deck was mutated");
        infantry.TransportCount=0;
        var foot=Activate(Wave(3,loaded));
        Check(foot.Units.Count==2 && foot.Units.TrueForAll(u=>u.UnitID==102 && u.Cargo.Slots.Count==0), "Zero-count transport was spawned");
        infantry.TransportCount=1;
        var missing=Wave(3,Request(905,UnitCategoryType.Aircrafts)); missing.Units=new UnitObject(8);
        Check(Activate(missing).Units.Count==0 && missing.Units.Count==0, "Missing category fell back to stock or stale output");
        var mixed=Activate(Wave(3,stock,Request(905,UnitCategoryType.Aircrafts)));
        Check(mixed.Units.Count==1 && mixed.Units[0].UnitID==101, "Partial wave did not filter absent category");
        ground.Count=0;
        Check(Activate(Wave(3,stock)).Units.Count==0, "Zero-count card included");
        ground.Count=1;
        armor.Fail=true;
        Check(Activate(Wave(3,stock)).Units.Count==0, "Unreadable custom deck spawned stock roster");
        armor.Fail=false;
        var wrongRole=new DeckSlotModel { UnitID=104, Category=UnitCategoryType.Vehicles, Data=new UnitData {Type=4,Role=12} };
        var wrongType=new DeckSlotModel { UnitID=105, Category=UnitCategoryType.Vehicles, Data=new UnitData {Type=2,Role=11} };
        armor.Slots[UnitCategoryType.Vehicles]=new[] {wrongRole,wrongType,ground};
        Session.Database.Units[901]=new UnitData {Type=4,Role=11};
        for(int i=0;i<60;i++) Check(Activate(Wave(3,stock)).Units[0].UnitID==101, "Role/type preference or later wave enforcement failed");
        c.StopDeckOverrides();
        Check(c._aiWaveCounts.Count==0 && ReferenceEquals(Activate(source),source.UnitInfo), "Dispose retained spawn override");
        PreloadSharedPlayerDeck.IsMissionRestart=true; c.BeginDeckLoad(loader);
        Check(Activate(source).Units[0].UnitID==101, "Restart lost spawn roster");
        PreloadSharedPlayerDeck.IsMissionRestart=false;
        ScenariosService.ActiveScenario=new ScenarioSource { Folder="elsewhere",Name="Other",MetaFile=scenario.MetaFile };
        Check(ReferenceEquals(Activate(source),source.UnitInfo) && !c._deckOverridesActive, "Other map inherited spawn override");
        ScenariosService.ActiveScenario=scenario;
        c.ClearBattleAiDecks();
    }
    public static string Run() {
        passed = 0;
        var human = new Deck { Name = "Player custom deck" };
        var armor = new Deck { Name = "Armor" };
        var air = new Deck { Name = "Airborne" };
        var c = new Checks(); Active = c;
        var scenario = new ScenarioSource { Folder = "maps/test", Name = "Test", MetaFile = new Meta { Type = ScenarioType.Skirmish } };
        var alpha = new Team { TeamSide = 0, TeamName = "Alpha" };
        var bravo = new Team { TeamSide = 1, TeamName = "Bravo" };
        foreach (int id in new[] {2,3,10,11,12}) alpha.Slots.Add(new Slot { BotId = id, PlayerName = "Alpha " + id });
        foreach (int id in new[] {5,6,13,14,15}) bravo.Slots.Add(new Slot { BotId = id, PlayerName = "Bravo " + id });
        scenario.MetaFile.Teams.Add(alpha); scenario.MetaFile.Teams.Add(bravo);
        c._maps.Add(scenario);
        service = new DeckService { Available = new List<IDeckDataModel> { human, null, armor, air } };
        c.RefreshDecks();
        Check(c._aiSlots.Count == 9, "Expected nine AI slots, excluding first Alpha human");
        Check(c._aiSlots[0].PlayerUid == 3 && c._aiSlots[4].PlayerUid == 5, "IDs or order incorrect");
        Check(c._decks.Count == 3 && c._deckNames.Count == 3, "Null decks must be skipped");
        Check(c._aiSlots.TrueForAll(s => s.DeckIndex == -1), "Slots must default to same as player");
        c.CycleAiDeck(-1);
        Check(c._aiSlots[0].DeckIndex == 2 && c.AiDeckText() == "3/3  Airborne", "Backward wrap incorrect");
        c.CycleAiDeck(1);
        Check(c.AiDeckText() == "Same as player", "Forward wrap must include default");
        c.CycleAiDeck(1); c.CycleAiDeck(1);
        Check(c._aiSlots[0].DeckIndex == 1, "First AI armor selection failed");
        c.CycleAiPlayer(4); c.CycleAiDeck(-1);
        Check(c._aiSlots[4].DeckIndex == 2 && c._aiSlots[0].DeckIndex == 1, "Second AI overwrote first");
        c.CycleAiPlayer(-4);
        Check(c.AiDeckText() == "2/3  Armor", "AI choice was not retained when cycling players");
        c.CycleAiPlayer(-1);
        Check(c._aiSlotIdx == 8 && c.AiDeckText() == "Same as player", "AI player backward wrap failed");
        c.CycleAiPlayer(1);
        Check(c._aiSlotIdx == 0 && c._deckIdx == 0, "Player deck changed when cycling AI");
        c._deckIdx = 2;
        Check(c._aiSlots[0].DeckIndex == 1 && c._aiSlots[4].DeckIndex == 2, "Changing human selection changed AI selections");

        c.PrepareDecks(scenario, human);
        CheckHuman(human);
        Check(c._battleAiDecks.Count == 9 && c._aiLoadPending, "Snapshot must include same-as-player bots");
        Check(ReferenceEquals(c._battleAiDecks[3], armor) && ReferenceEquals(c._battleAiDecks[5], air), "Decks assigned by index instead of UID");
        Check(ReferenceEquals(c._battleAiDecks[10], human), "Default bot must follow chosen player deck");
        Check(!PreloadSharedPlayerDeck.IsMissionRestart, "Restart flag not reset");
        var loader = new MissionLoader();
        var session = loader._game.GameSession;
        session.CurrentPlayer = new Player { UID = 2 };
        foreach (int id in new[] {2,3,10,11,12,5,6,13,14,15}) session.Players[id] = new Player { UID = id };
        ScenariosService.ActiveScenario = scenario;
        IDeckDataModel requested = new Deck { Name = "RU default" };
        c.OverrideDeckAssignment(3, ref requested);
        Check(requested.Name == "RU default", "Plan must not apply before the correct load binds it");
        c.BeginDeckLoad(loader);
        Check(c._deckOverridesActive && !c._aiLoadPending && c._aiRestartAllowed, "Load did not bind choices");
        var economy = new Dictionary<int, IDeckDataModel>();
        var shop = new Dictionary<int, IDeckDataModel>();
        // Exercise repeated deck API calls. Stock skirmish AI actually uses the separate spawn path.
        for (int pass = 0; pass < 3; pass++) {
            foreach (int id in new[] {2,3,5,10}) {
                var defaultDeck = new Deck { Name = id == 5 ? "USA default" : "RU default" };
                IDeckDataModel result;
                SharedPlayerDeck.SetDeck(id, defaultDeck, out result);
                // ChangePlayerDeck sends SetDeck's returned clone to economy and shop.
                economy[id] = result; shop[id] = result;
                string expected = id == 3 ? "Armor" : id == 5 ? "Airborne" : human.Name;
                Check(result.Name == expected, "Late default reset defeated selected deck for UID " + id);
                Check(economy[id].Name == expected && shop[id].Name == expected, "Economy/shop received stale default");
                Check(!ReferenceEquals(result, defaultDeck), "Game should retain its normal clone path");
            }
        }
        CheckHuman(human);
        c._battleAiDecks[2] = air;
        requested = air;
        c.OverrideDeckAssignment(2, ref requested);
        Check(ReferenceEquals(requested, human), "Human must use own selection even if present in AI map");
        c._battleAiDecks[999] = air;
        requested = human;
        c.OverrideDeckAssignment(999, ref requested);
        Check(ReferenceEquals(requested, human), "Unknown player was overridden");
        session.Players[5].IsBot = false;
        c.OverrideDeckAssignment(5, ref requested);
        Check(ReferenceEquals(requested, human), "Non-bot player was overridden");
        session.Players[5].IsBot = true;

        c.StopDeckOverrides();
        requested = human;
        c.OverrideDeckAssignment(3, ref requested);
        Check(ReferenceEquals(requested, human) && !c._deckOverridesActive, "Battle disposal left enforcement active");
        PreloadSharedPlayerDeck.IsMissionRestart = true;
        c.BeginDeckLoad(loader);
        c.OverrideDeckAssignment(3, ref requested);
        Check(ReferenceEquals(requested, armor), "Restart did not restore choices");
        PreloadSharedPlayerDeck.IsMissionRestart = false;
        c.BeginDeckLoad(loader);
        requested = human;
        c.OverrideDeckAssignment(3, ref requested);
        Check(ReferenceEquals(requested, human) && c._battleAiDecks.Count == 0, "Unrelated launch inherited choices");

        c.PrepareDecks(scenario, human); c.BeginDeckLoad(loader);
        ScenariosService.ActiveScenario = new ScenarioSource { Folder = "other", Name = scenario.Name, MetaFile = scenario.MetaFile };
        requested = human;
        c.OverrideDeckAssignment(3, ref requested);
        Check(ReferenceEquals(requested, human) && !c._deckOverridesActive, "Different scenario inherited live hook");
        c.PrepareDecks(scenario, human); c.BeginDeckLoad(loader);
        Check(c._battleAiDecks.Count == 0, "Different scenario accepted pending plan");
        ScenariosService.ActiveScenario = scenario;
        c.PrepareDecks(scenario, human);
        session.CurrentPlayer = null;
        c.BeginDeckLoad(loader);
        Check(c._battleAiDecks.Count == 0 && !c._deckOverridesActive, "Missing human must fail closed");
        session.CurrentPlayer = session.Players[2];
        c.PrepareDecks(scenario, human); c.BeginDeckLoad(loader);
        session.CurrentPlayer = null;
        requested = air;
        c.OverrideDeckAssignment(3, ref requested);
        Check(ReferenceEquals(requested, air) && !c._deckOverridesActive, "Lost session did not disable hook");
        session.CurrentPlayer = session.Players[2];

        SpawnChecks(c,loader,scenario,human,armor,air);
        c.RefreshAiSlots();
        c._aiSlots[0].Closed = true;
        loader._game.GameSession.Players[3].IsBot = true;
        ScenariosService.ActiveScenario = scenario;
        c.PrepareDecks(scenario,human);
        Check(c._battleClosedSlots.Contains(3) && !c._battleAiDecks.ContainsKey(3), "Closed slot still received active deck plan");
        c.ApplyClosedSlotPlan(loader._game.GameSession);
        Check(!loader._game.GameSession.Players[3].IsBot && loader._game.GameSession.Players.ContainsKey(3), "Closed slot identity/activation incorrect");
        c.BeginDeckLoad(loader);
        Check(Activate(Wave(3,Request(901,UnitCategoryType.Vehicles))).Units.Count==0, "Direct scripted wave leaked from a closed UID");
        Check(c._waveDiagnostics.Summary().Contains("closedRequests=1") && c._waveDiagnostics.Summary().Contains("closedForwardedUnits=0"),
            "Closed-wave request/forwarded diagnostic state is incorrect");
        PreloadSharedPlayerDeck.IsMissionRestart=true;
        loader._game.GameSession.Players[3].IsBot=true;
        c.ApplyClosedSlotPlan(loader._game.GameSession);
        Check(!loader._game.GameSession.Players[3].IsBot, "Restart lost closed-slot plan");
        PreloadSharedPlayerDeck.IsMissionRestart=false;
        c.StopDeckOverrides(); loader._game.GameSession.Players[3].IsBot=true;
        c.ApplyClosedSlotPlan(loader._game.GameSession);
        Check(loader._game.GameSession.Players[3].IsBot, "Closed-slot patch leaked after battle ended");
        c.PrepareDecks(scenario,human);
        ISceneLoadManager.Instance=new ISceneLoadManager {LoadScenario=new ScenarioSource {Folder="other",Name="other",MetaFile=new Meta{Type=ScenarioType.Skirmish}}};
        ScenariosService.ActiveScenario=ISceneLoadManager.Instance.LoadScenario;
        c.ApplyClosedSlotPlan(loader._game.GameSession);
        Check(loader._game.GameSession.Players[3].IsBot, "Closed-slot patch changed another loading scenario");
        ISceneLoadManager.Instance.LoadScenario=scenario; ScenariosService.ActiveScenario=null;
        c.ApplyClosedSlotPlan(loader._game.GameSession);
        Check(!loader._game.GameSession.Players[3].IsBot, "Early loading scenario could not apply plan before ActiveScenario binding");
        ScenariosService.ActiveScenario=scenario;ISceneLoadManager.Instance.LoadScenario=new ScenarioSource{Folder="stale",Name="stale",MetaFile=new Meta{Type=ScenarioType.Skirmish}};
        loader._game.GameSession.Players[3].IsBot=true;c.ApplyClosedSlotPlan(loader._game.GameSession);
        Check(!loader._game.GameSession.Players[3].IsBot, "Stale scene-load metadata overrode the authoritative active scenario");
        ISceneLoadManager.Instance=null; ScenariosService.ActiveScenario=scenario;
        c.ClearBattleAiDecks();
        Check(c._battleClosedSlots.Count==0 && c._battleHumanUid==-1 && c._battleAiProfiles.Count==0, "Clear retained slot/profile state");
        c._aiSlots[0].Closed=false; loader._game.GameSession.Players[3].IsBot=true;
        c._aiSlots[0].NativePresetId=2; c.PrepareDecks(scenario,human);
        var savedHuman=loader._game.GameSession.CurrentPlayer;loader._game.GameSession.CurrentPlayer=null;
        bool strictFailure=false;try{c.BeginDeckLoad(loader);}catch(InvalidOperationException){strictFailure=true;}
        Check(strictFailure && c._battleAiProfiles.Count==0 && !c._deckOverridesActive,"Required native army plan silently fell back after binding failure");
        loader._game.GameSession.CurrentPlayer=savedHuman;c._aiSlots[0].NativePresetId=-1;
        c.PrepareDecks(scenario, human);
        scenario.MetaFile.Type = ScenarioType.MultiplayerPVE;
        c.PrepareDecks(scenario, human);
        CheckHuman(human);
        Check(c._battleAiDecks.Count == 0 && !c._aiLoadPending, "PvE launch retained overrides");
        c.RefreshDecks(); c.CycleAiDeck(1); c.CycleAiPlayer(1);
        Check(c._aiSlots.Count == 0, "Non-skirmish must not offer AI choices");
        scenario.MetaFile.Type = ScenarioType.Skirmish;
        c.RefreshDecks();
        Check(c._aiSlotIdx == 0 && c._aiSlots.TrueForAll(s => s.DeckIndex == -1), "Map refresh retained selections");
        service.Available.Clear();
        c.RefreshDecks(); c.CycleAiDeck(1); c.CycleAiDeck(-1);
        Check(c._aiSlots[0].DeckIndex == -1, "Empty catalog must retain default");
        service.Fail = true;
        c.RefreshDecks();
        Check(c._decks.Count == 0 && c._aiSlots.TrueForAll(s => s.DeckIndex == -1), "Failed catalog retained selections");
        service = null;
        c.RefreshDecks();
        Check(c._decks.Count == 0, "Missing service retained decks");
        c._mapIdx = -1;
        c.RefreshDecks();
        Check(c._aiSlots.Count == 0 && !c.IsSkirmishMap(), "Invalid map index accepted");
        c._mapIdx = 0;
        alpha.Slots.Add(new Slot { BotId = 3 });
        bravo.Slots.Add(new Slot { BotId = -1 });
        bravo.Slots.Add(new Slot { BotId = 99, IsBot = false });
        bravo.Slots.Add(null);
        scenario.MetaFile.Teams.Add(null);
        c.RefreshAiSlots();
        Check(c._aiSlots.Count == 9, "Invalid/non-bot/duplicate slots were not excluded");
        scenario.MetaFile.Teams = null;
        c.RefreshAiSlots();
        Check(c._aiSlots.Count == 0, "Missing teams retained slots");
        return "PASS: " + passed + " deck selection, scripted AI spawn replacement, loadouts, callback counts, input restoration, and lifecycle checks.";
    }
}}
'@
if ($ExportHarness) {
    $registrationFixture = @'
    public static bool RegisteredLoadSurvives() {
        var c = new Checks(); Active = c;
        var scenario = new ScenarioSource { Folder="map", Name="registration", MetaFile=new Meta {Type=ScenarioType.Skirmish} };
        ScenariosService.ActiveScenario = scenario;
        var human = new Deck {Name="Human"};
        var custom = new Deck {Name="AI custom"};
        custom.Slots[UnitCategoryType.Vehicles] = new[] { new DeckSlotModel {UnitID=123, Category=UnitCategoryType.Vehicles} };
        c._decks.Add(custom);
        c._aiSlots.Add(new AiSlot {PlayerUid=3, Name="AI", DeckIndex=0});
        var loader = new MissionLoader();
        loader._game.GameSession.CurrentPlayer = new Player {UID=2};
        loader._game.GameSession.Players[3] = new Player {UID=3};
        c.PrepareDecks(scenario,human);
        // Real Harmony dispatches the production prefix here, possibly twice.
        loader.LoadPlayersDeck();
        var node = Wave(3, Request(901,UnitCategoryType.Vehicles));
        var sharedInput = node.UnitInfo;
        node.OnActivated();
        return c._deckOverridesActive && c._battleAiDecks.Count==1
            && node.NativeRequest.Units[0].UnitID==123
            && ReferenceEquals(node.UnitInfo,sharedInput);
    }
    public static bool RegisteredClosedSlotsSurvive() {
        var c=new Checks(); Active=c;
        var scenario=new ScenarioSource {Folder="closed-map",Name="closed-registration",MetaFile=new Meta{Type=ScenarioType.Skirmish}};
        var alpha=new Team{TeamSide=0}; alpha.Slots.Add(new Slot{BotId=2}); alpha.Slots.Add(new Slot{BotId=3});
        scenario.MetaFile.Teams.Add(alpha);
        ScenariosService.ActiveScenario=scenario;
        var human=new Deck{Name="Human"}; var army=new Deck{Name="AI"};
        army.Slots[UnitCategoryType.Vehicles]=new[]{new DeckSlotModel{UnitID=123,Category=UnitCategoryType.Vehicles}};
        c._decks.Add(army); c._aiSlots.Add(new AiSlot{PlayerUid=3,Name="Closed",Closed=true});
        c._aiSlots.Add(new AiSlot{PlayerUid=5,Name="Active",DeckIndex=0});
        var loader=new MissionLoader(); var session=loader._game.GameSession;
        session.CurrentPlayer=new Player{UID=2}; session.Players[3]=new Player{UID=3}; session.Players[5]=new Player{UID=5};
        c.PrepareDecks(scenario,human);
        new ScenarioController().LoadTeamsAndPlayers(session);
        if(session.Players[3].IsBot || !session.Players[5].IsBot || session.Players.Count!=2) return false;
        loader.LoadPlayersDeck();
        var closedWave=Wave(3,Request(901,UnitCategoryType.Vehicles)); closedWave.OnActivated();
        var activeWave=Wave(5,Request(901,UnitCategoryType.Vehicles)); activeWave.OnActivated();
        if(!closedWave.Completed || closedWave.NativeRequest!=null || closedWave.Units.Count!=0
            || activeWave.NativeRequest.Units[0].UnitID!=123) return false;
        PreloadSharedPlayerDeck.IsMissionRestart=true; session.Players[3].IsBot=true;
        new ScenarioController().LoadTeamsAndPlayers(session);
        bool restart=!session.Players[3].IsBot;
        PreloadSharedPlayerDeck.IsMissionRestart=false; c.ClearBattleAiDecks(); session.Players[3].IsBot=true;
        new ScenarioController().LoadTeamsAndPlayers(session);
        return restart && session.Players[3].IsBot;
    }
    public static bool RegisteredNativeProfilesSurvive() {
        var c=new Checks(); Active=c;
        var scenario=new ScenarioSource{Folder="native-map",Name="profile-registration",MetaFile=new Meta{Type=ScenarioType.Skirmish}};
        var alpha=new Team{TeamSide=0};alpha.Slots.Add(new Slot{BotId=2});scenario.MetaFile.Teams.Add(alpha);
        ScenariosService.ActiveScenario=scenario;
        var human=new Deck{Name="Human"};var custom=new Deck{Name="Custom"};
        custom.Slots[UnitCategoryType.Vehicles]=new[]{new DeckSlotModel{UnitID=123,Category=UnitCategoryType.Vehicles}};
        c._decks.Add(custom);
        c._aiSlots.Add(new AiSlot{PlayerUid=5,Name="Native",NativePresetId=2});
        c._aiSlots.Add(new AiSlot{PlayerUid=6,Name="Custom",DeckIndex=0});
        var loader=new MissionLoader();var session=loader._game.GameSession;
        session.CurrentPlayer=new Player{UID=2};session.Players[5]=new Player{UID=5};session.Players[6]=new Player{UID=6};
        c.PrepareDecks(scenario,human);loader.LoadPlayersDeck();
        var node=new NodeGetSlotData{_slotSoloPlayer=session.Players[5]};node.InitSolo();
        if(node.AiPresetId!=2 || c._battleAiDecks.ContainsKey(5) || c._battleAiProfiles[5]!=2) return false;
        var native=Wave(5,Request(901,UnitCategoryType.Vehicles));native.OnActivated();
        var edited=Wave(6,Request(901,UnitCategoryType.Vehicles));edited.OnActivated();
        if(native.NativeRequest.Units[0].UnitID!=901 || edited.NativeRequest.Units[0].UnitID!=123) return false;
        c.StopDeckOverrides();node.AiPresetId=-1;node.InitSolo();if(node.AiPresetId!=-1)return false;
        PreloadSharedPlayerDeck.IsMissionRestart=true;loader.LoadPlayersDeck();node.InitSolo();
        bool restart=node.AiPresetId==2;PreloadSharedPlayerDeck.IsMissionRestart=false;
        c.ClearBattleAiDecks();return restart && c._battleAiProfiles.Count==0;
    }
'@
    $prefix = "using HarmonyLib;`n" + $prefix
    $prefix = $prefix.Replace('static class AiSpawnPatch', '[HarmonyPatch(typeof(NodeSpawnMultiUnits), nameof(NodeSpawnMultiUnits.OnActivated))] static class AiSpawnPatch')
    $prefix = $prefix.Replace('static class ClosedSlotInitializationPatch', '[HarmonyPatch(typeof(ScenarioController), nameof(ScenarioController.LoadTeamsAndPlayers))] static class ClosedSlotInitializationPatch')
    $prefix = $prefix.Replace('static class NativeAiPresetInitializationPatch', '[HarmonyPatch(typeof(NodeGetSlotData), nameof(NodeGetSlotData.InitSolo))] static class NativeAiPresetInitializationPatch')
    $suffix = $suffix.Replace('    public static string Run() {', $registrationFixture + "`n    public static string Run() {")
    $loadPatch = (Get-MethodSource 'static class AiPlayerDeckPatch').Replace('Core.Instance', 'Checks.Active')
    $harness = $prefix + ($methods -join "`n") + $suffix + "`nnamespace DeckSelectionRegression { [HarmonyPatch(typeof(MissionLoader), nameof(MissionLoader.LoadPlayersDeck))] " + $loadPatch + ' }' + "`n" + $diagnosticsHelper
    Set-Content -LiteralPath $ExportHarness -Value $harness
    return
}
Add-Type -TypeDefinition ($prefix + ($methods -join "`n") + $suffix + "`n" + $diagnosticsHelper)
[DeckSelectionRegression.Checks]::Run()

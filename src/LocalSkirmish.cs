using System;
using System.IO;
using System.Collections.Generic;
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppBrokenArrow.Client.Ecs.GNetwork.Services.Lobby;               // NetworkLobbyService
using Il2CppBrokenArrow.Client.Ecs.UI;                                    // ISceneTransition
using Il2CppBrokenArrow.Client.Ecs.Utils;                                // SceneLoadManager
using Il2CppBrokenArrow.MissionEditor.MissionResolver;                    // ScenarioSource, ScenariosService, ScenarioType
using Il2CppBrokenArrow.MissionEditor.Storage;                           // ScenarioMetaFile, ScenarioPublicOption
using Il2CppBrokenArrow.Shared.Ecs.Services;                             // Session
using Il2CppBrokenArrow.Client.Ecs.Decks;                                // DeckService
using Il2CppBrokenArrow.Client.Ecs.Decks.Models;                         // IDeckDataModel
using Il2CppBrokenArrow.Client.Ecs.Decks_v2;                             // PreloadSharedPlayerDeck
using Il2CppBrokenArrow.Client.Ecs.Campaign;                             // CampaignService
using Il2CppBrokenArrow.Shared.Ecs.Enums;                                // DifficultyLevel
using Il2CppNetworkCommon.Enums;                                          // LobbyType
using Il2CppBrokenArrow.MissionEditor;                                  // MissionLoader
using Il2CppBrokenArrow.Client.Ecs.Configs;                             // GameSessionContext
using Il2CppBrokenArrow.Client.Ecs.Controllers;                         // GameController
using Il2CppBrokenArrow.ScriptEngine.Nodes.Spawn;
using Il2CppBrokenArrow.ScriptEngine.Nodes.Functions;
using Il2CppBrokenArrow.ScriptEngine.Editor.Properties;
using Il2CppBrokenArrow.Shared.Ecs.MissionEditor;
using Il2CppBrokenArrow.MissionEditor.Inspector;
using Il2CppBrokenArrow.Shared.Ecs;
using Il2CppNetworkCommon.Enums.Common;

[assembly: MelonInfo(typeof(BALocalSkirmish.Core), "BA Local Skirmish", "3.0.1", "BovineOverlord; local UI fork")]
[assembly: MelonGame(null, null)]
// Hooks are installed explicitly below so failures can block launching. Without this
// opt-out, MelonLoader installs them again and the second load prefix clears the plan.
[assembly: HarmonyDontPatchAll]

namespace BALocalSkirmish
{
    class AiSlot
    {
        public int PlayerUid;
        public string Name;
        public int TeamSide;
        public string TeamName;
        public bool Mandatory;
        public bool Closed;
        public int NativePresetId = -1; // -1 keeps the existing custom/full-deck path.
        public int DeckIndex = -1; // Same as player
    }

    // v2.2: Skirmish setup panel with map + deck + the scenario's own game options (income, time
    // limit, day/night, map size, ...). Options are injected into SceneLoadManager.ScenarioPublicOptions
    // at load time so the local battle honours them.
    public partial class Core : MelonMod
    {
        public static Core Instance;
        static readonly System.Random _rng = new System.Random();

        // Options the user chose for the pending launch (uid -> value).
        public static readonly Dictionary<int, int> PendingOptions = new Dictionary<int, int>();
        int _applyFrames;
        bool _appliedLogged;

        // scenario/deck/option state
        readonly List<ScenarioSource> _maps = new List<ScenarioSource>();
        readonly List<string> _mapNames = new List<string>();
        int _mapIdx;
        readonly List<IDeckDataModel> _decks = new List<IDeckDataModel>();
        readonly List<string> _deckNames = new List<string>();
        int _deckIdx;
        readonly List<AiSlot> _aiSlots = new List<AiSlot>();
        int _aiSlotIdx;
        readonly Dictionary<int, IDeckDataModel> _battleAiDecks = new Dictionary<int, IDeckDataModel>();
        readonly HashSet<int> _battleClosedSlots = new HashSet<int>();
        readonly Dictionary<int, int> _battleAiProfiles = new Dictionary<int, int>();
        int _battleHumanUid = -1;
        readonly Dictionary<int, int> _aiWaveCounts = new Dictionary<int, int>();
        ScenarioSource _battleScenario;
        IDeckDataModel _battlePlayerDeck;
        GameSessionContext _deckSession;
        bool _deckOverridesActive;
        bool _aiLoadPending, _aiRestartAllowed, _aiDeckPatchReady;
        bool _slotPatchReady;
        bool _profilePatchReady;
        readonly List<OptDef> _opts = new List<OptDef>();
        readonly Dictionary<int, int> _optVals = new Dictionary<int, int>();
        int _diff = 2;          // 1=Easy, 2=Normal (native Medium), 3=Hard
        bool _hasDiff = true;
        static readonly string[] _diffNames = { "None", "Easy", "Normal", "Hard" };

        // UI
        GameObject _root;
        Text _mapLabel, _deckLabel, _aiPlayerLabel, _aiDeckLabel, _statusLabel, _diffLabel, _previewCaption;
        Image _previewImg;
        readonly List<Text> _optLabels = new List<Text>();
        Font _font;

        // palette
        static readonly Color CDim = new Color(0f, 0f, 0f, 0.6f);
        static readonly Color CFrame = new Color(0.30f, 0.45f, 0.62f, 1f);
        static readonly Color CPanel = new Color(0.10f, 0.12f, 0.16f, 0.99f);
        static readonly Color CHeader = new Color(0.15f, 0.28f, 0.42f, 1f);
        static readonly Color CRow = new Color(1f, 1f, 1f, 0.04f);
        static readonly Color CLabel = new Color(0.62f, 0.78f, 0.98f);
        static readonly Color CVal = new Color(0.95f, 0.96f, 0.98f);
        static readonly Color CBtn = new Color(0.20f, 0.24f, 0.32f, 1f);
        static readonly Color CStart = new Color(0.20f, 0.50f, 0.30f, 1f);
        static readonly Color CCancel = new Color(0.48f, 0.24f, 0.26f, 1f);

        public override void OnInitializeMelon()
        {
            Instance = this;
            LoggerInstance.Msg("BA Local Skirmish v3.0.1 Normal-baseline diagnostics initializing (manual patch registration only)...");
            Patch(typeof(CreateLobbyPatch));
            _aiDeckPatchReady = Patch(typeof(AiPlayerDeckPatch))
                & Patch(typeof(DeckAssignmentPatch)) & Patch(typeof(EndBattleDeckPatch))
                & Patch(typeof(AiSpawnPatch));
            _slotPatchReady = Patch(typeof(ClosedSlotInitializationPatch));
            _profilePatchReady = Patch(typeof(NativeAiPresetInitializationPatch));
        }

        // Push the chosen game options into the live SceneLoadManager options dict.
        static bool ApplyOptionsOnce()
        {
            try
            {
                var slm = ISceneLoadManager.Instance;
                if (slm == null) return false;
                var dict = slm.ScenarioPublicOptions;
                if (dict == null) return false;
                foreach (var kv in PendingOptions) { try { dict[kv.Key] = kv.Value; } catch { } }
                return true;
            }
            catch { return false; }
        }

        public override void OnUpdate()
        {
            RetryPreviewWhenReady();
            if (_applyFrames <= 0) return;
            _applyFrames--;
            if (PendingOptions.Count == 0) { _applyFrames = 0; return; }
            if (ApplyOptionsOnce() && !_appliedLogged)
            {
                _appliedLogged = true;
                MelonLogger.Msg($"[LocalSkirmish] Applied {PendingOptions.Count} game option(s) to the load.");
            }
        }
        bool Patch(Type t)
        {
            try { HarmonyInstance.CreateClassProcessor(t).Patch(); LoggerInstance.Msg("Patched " + t.Name); return true; }
            catch (Exception e) { LoggerInstance.Error("Patch failed for " + t.Name + ": " + e); return false; }
        }

        // ---------- data ----------
        static ScenarioType TypeOf(ScenarioSource s)
        { try { var m = s.MetaFile; if (m != null) return m.Type; } catch { } return ScenarioType.MultiplayerPVP; }
        static string NameOf(ScenarioSource s)
        { try { return s.MetaFile != null ? s.MetaFile.ScenarioName : "?"; } catch { return "?"; } }
        static ScenariosService ScenariosSvc()
        { try { ScenariosService s = null; Session.TryGetService<ScenariosService>(out s, false); return s; } catch { return null; } }
        static DeckService DecksSvc()
        { try { DeckService s = null; Session.TryGetService<DeckService>(out s, false); return s; } catch { return null; } }

        // Make a loc-key readable: "ui_lobby_Income_title" -> "Income"
        static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            string r = s;
            foreach (var p in new[] { "ui_lobby_", "ui_set_hangar_", "ui_lobby_random_", "ui_", })
                if (r.StartsWith(p, StringComparison.OrdinalIgnoreCase)) { r = r.Substring(p.Length); break; }
            foreach (var suf in new[] { "_title", "_Title", "_desc", "_Description", "_Desc" })
                if (r.EndsWith(suf, StringComparison.OrdinalIgnoreCase)) { r = r.Substring(0, r.Length - suf.Length); break; }
            r = r.Replace('_', ' ').Trim();
            if (r.Length > 0) r = char.ToUpper(r[0]) + r.Substring(1);
            return r.Length == 0 ? s : r;
        }

        void BuildMapList()
        {
            _maps.Clear(); _mapNames.Clear();
            var svc = ScenariosSvc();
            if (svc == null) return;
            var skirmish = new List<ScenarioSource>();
            var pve = new List<ScenarioSource>(); var solo = new List<ScenarioSource>();
            try
            {
                var list = svc._scenarios; int n = list != null ? list.Count : 0;
                for (int i = 0; i < n; i++)
                {
                    ScenarioSource s = null; try { s = list[i]; } catch { continue; }
                    if (s == null) continue;
                    var t = TypeOf(s);
                    // Dedicated skirmish scenarios have their own type and bot slots.
                    // Do not confuse them with MultiplayerPVP (*_network) scenarios.
                    if (t == ScenarioType.Skirmish) skirmish.Add(s);
                    else if (t == ScenarioType.MultiplayerPVE) pve.Add(s);
                    else if (t == ScenarioType.Singleplayer) solo.Add(s);
                }
            }
            catch (Exception e) { MelonLogger.Error("[LocalSkirmish] BuildMapList: " + e.Message); }
            foreach (var group in new[] { skirmish, pve, solo })
            {
                group.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(NameOf(a), NameOf(b)));
                foreach (var s in group) { _maps.Add(s); _mapNames.Add(LobbyText.MapName(NameOf(s))); }
            }
        }

        void RefreshDecks()
        {
            _decks.Clear(); _deckNames.Clear(); _deckIdx = 0;
            RefreshAiSlots();
            if (_mapIdx < 0 || _mapIdx >= _maps.Count) return;
            var ds = DecksSvc(); if (ds == null) return;
            try
            {
                var arr = ds.GetAvailableDecksForScenario(_maps[_mapIdx], 0);
                int n = arr != null ? arr.Count : 0;
                for (int i = 0; i < n; i++)
                {
                    IDeckDataModel d = null; try { d = arr[i]; } catch { continue; }
                    if (d == null) continue;
                    string nm = "?"; try { nm = d.Name; } catch { }
                    _decks.Add(d); _deckNames.Add(nm);
                }
            }
            catch (Exception e) { MelonLogger.Error("[LocalSkirmish] RefreshDecks: " + e.Message); }
        }

        bool IsSkirmishMap()
        {
            return _mapIdx >= 0 && _mapIdx < _maps.Count
                && TypeOf(_maps[_mapIdx]) == ScenarioType.Skirmish;
        }

        string AiDeckText()
        {
            int idx = _aiSlotIdx >= 0 && _aiSlotIdx < _aiSlots.Count ? _aiSlots[_aiSlotIdx].DeckIndex : -1;
            return idx >= 0 && idx < _deckNames.Count
                ? $"{idx + 1}/{_deckNames.Count}  {_deckNames[idx]}"
                : "Same as player";
        }

        void RefreshAiSlots()
        {
            _aiSlots.Clear(); _aiSlotIdx = 0;
            if (!IsSkirmishMap()) return;
            try
            {
                var teams = _maps[_mapIdx].MetaFile.Teams;
                if (teams == null) return;
                bool skippedHuman = false;
                var seen = new HashSet<int>();
                foreach (var team in teams)
                {
                    if (team == null || team.Slots == null) continue;
                    for (int i = 0; i < team.Slots.Count; i++)
                    {
                        var slot = team.Slots[i];
                        if (slot == null) continue;
                        // The offline loader chooses the first Alpha player as the human.
                        // Built-in skirmish metadata marks even that slot as a bot.
                        if ((int)team.TeamSide == 0 && !skippedHuman)
                        { skippedHuman = true; seen.Add(slot.BotId); continue; }
                        if (!slot.IsBot || slot.BotId < 0 || !seen.Add(slot.BotId)) continue;
                        string name = string.IsNullOrEmpty(slot.PlayerName)
                            ? $"{team.TeamName} player {i + 1}" : slot.PlayerName;
                        _aiSlots.Add(new AiSlot { PlayerUid = slot.BotId, Name = name,
                            TeamSide = (int)team.TeamSide, TeamName = team.TeamName, Mandatory = slot.Mandatory });
                    }
                }
            }
            catch (Exception e)
            {
                _aiSlots.Clear();
                MelonLogger.Error("[LocalSkirmish] Read AI players: " + e.Message);
            }
        }

        void ClearBattleAiDecks()
        {
            _battleClosedSlots.Clear(); _battleAiProfiles.Clear(); _battleHumanUid = -1;
            _battleAiDecks.Clear(); _battleScenario = null;
            _battlePlayerDeck = null;
            StopDeckOverrides();
            _waveDiagnostics.Reset(); _diagnosticFailureLogged = false;
            _aiLoadPending = false; _aiRestartAllowed = false;
        }

        internal void StopDeckOverrides()
        {
            try
            {
                if (_deckOverridesActive)
                {
                    FlushWaveDiagnostics();
                    MelonLogger.Msg("[LocalSkirmish] Deck and AI spawn overrides detached from the local battle.");
                }
            }
            catch (Exception e) { DiagnosticFailure(e); }
            finally
            {
                _deckOverridesActive = false; _deckSession = null;
                _aiWaveCounts.Clear();
            }
        }

        bool MatchesBattleScenario()
        {
            var active = ScenariosService.ActiveScenario;
            return active != null && _battleScenario != null
                && TypeOf(active) == ScenarioType.Skirmish
                && string.Equals(active.Folder, _battleScenario.Folder, StringComparison.OrdinalIgnoreCase)
                && string.Equals(active.Name, _battleScenario.Name, StringComparison.Ordinal);
        }

        void PrepareDecks(ScenarioSource scenario, IDeckDataModel playerDeck)
        {
            ClearBattleAiDecks();
            // Keep the working human preload path. Scripted AI waves need their own
            // override because NodeSpawnMultiUnits deliberately bypasses player decks.
            PreloadSharedPlayerDeck.IsMissionRestart = false;
            PreloadSharedPlayerDeck.ScenarioStartDeck = playerDeck;
            PreloadSharedPlayerDeck.Alpha = playerDeck;
            PreloadSharedPlayerDeck.Bravo = playerDeck;
            if (TypeOf(scenario) != ScenarioType.Skirmish) return;
            foreach (var slot in _aiSlots)
            {
                if (slot.Closed)
                {
                    if (slot.Mandatory) throw new InvalidOperationException("A required scenario slot cannot be closed.");
                    _battleClosedSlots.Add(slot.PlayerUid); continue;
                }
                if (slot.NativePresetId >= 0)
                {
                    _battleAiProfiles.Add(slot.PlayerUid, slot.NativePresetId);
                    MelonLogger.Msg($"[LocalSkirmish] AI UID {slot.PlayerUid} uses native scripted army preset {slot.NativePresetId}.");
                    continue;
                }
                if (slot.DeckIndex >= _decks.Count)
                    throw new InvalidOperationException("Choose an available deck for " + slot.Name + ".");
                _battleAiDecks.Add(slot.PlayerUid, slot.DeckIndex < 0 ? playerDeck : _decks[slot.DeckIndex]);
            }
            _battleHumanUid = HumanUidOf(scenario);
            _battleScenario = scenario;
            _battlePlayerDeck = playerDeck;
            _aiLoadPending = true;
        }

        internal void ApplyNativeAiProfile(NodeGetSlotData node)
        {
            if (!_deckOverridesActive || _battleAiProfiles.Count == 0 || _deckSession == null || !MatchesBattleScenario()) return;
            var player = node?._slotSoloPlayer;
            if (player == null || !_battleAiProfiles.ContainsKey(player.UID)) return;
            if (!NativeAiProfileBridge.ApplyPreset(node, _deckSession, _battleAiProfiles, _battleHumanUid))
                throw new InvalidOperationException("Native AI preset could not be bound to slot UID " + player.UID + ".");
        }

        static int HumanUidOf(ScenarioSource scenario)
        {
            try
            {
                foreach (var team in scenario.MetaFile.Teams)
                    if (team != null && (int)team.TeamSide == 0 && team.Slots != null)
                        foreach (var slot in team.Slots) if (slot != null) return slot.BotId;
            }
            catch { }
            return -1;
        }

        internal void ApplyClosedSlotPlan(GameSessionContext session)
        {
            if (_battleClosedSlots.Count == 0 || _battlePlayerDeck == null || session == null) return;
            if (!(_aiLoadPending || (_aiRestartAllowed && PreloadSharedPlayerDeck.IsMissionRestart)) || !MatchesLaunchingScenario()) return;
            try
            {
                int applied = SlotRuntimeBridge.ApplyClosedSlots(session, _battleClosedSlots, _battleHumanUid);
                foreach (int uid in _battleClosedSlots)
                {
                    var player = session.GetPlayer(uid);
                    if (player != null && (player.IsBot || player.NetID > 0))
                        throw new InvalidOperationException("Unable to close runtime slot UID " + uid + "; scenario initialization stopped.");
                }
                MelonLogger.Msg($"[LocalSkirmish] Closed {applied}/{_battleClosedSlots.Count} optional AI slots before scenario graph initialization.");
            }
            catch (Exception e)
            {
                MelonLogger.Error("[LocalSkirmish] Apply closed slots: " + e);
                throw; // Do not silently continue a battle with bots the user explicitly closed.
            }
        }

        bool MatchesLaunchingScenario()
        {
            try { if (ScenariosService.ActiveScenario != null) return MatchesBattleScenario(); } catch { }
            try
            {
                var loading = ISceneLoadManager.Instance?.LoadScenario;
                if (loading != null)
                    return _battleScenario != null && TypeOf(loading) == ScenarioType.Skirmish
                        && string.Equals(loading.Folder, _battleScenario.Folder, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(loading.Name, _battleScenario.Name, StringComparison.Ordinal);
            }
            catch { }
            return MatchesBattleScenario();
        }

        internal void BeginDeckLoad(MissionLoader loader)
        {
            bool needsExactPlan = _battleClosedSlots.Count > 0 || _battleAiProfiles.Count > 0;
            StopDeckOverrides();
            _waveDiagnostics.Reset(); _diagnosticFailureLogged = false;
            if (_battlePlayerDeck == null) return;
            try
            {
                if (!MatchesBattleScenario() || (!_aiLoadPending && !(_aiRestartAllowed && PreloadSharedPlayerDeck.IsMissionRestart)))
                {
                    MelonLogger.Msg($"[LocalSkirmish] Deck plan discarded: matching scenario={MatchesBattleScenario()}, "
                        + $"pending={_aiLoadPending}, restart={PreloadSharedPlayerDeck.IsMissionRestart}.");
                    ClearBattleAiDecks();
                    if (needsExactPlan) throw new InvalidOperationException("The selected closed-slot/native AI plan does not match this scenario load.");
                    return;
                }

                var session = loader._game.GameSession;
                var human = session.CurrentPlayer;
                if (human == null) throw new InvalidOperationException("Local player is unavailable; deck overrides skipped.");
                _deckSession = session;
                _deckOverridesActive = true;
                _aiLoadPending = false;
                _aiRestartAllowed = true;
                LogRuntimeSlotSnapshot(session);
                MelonLogger.Msg($"[LocalSkirmish] Local plan bound: human UID {human.UID}, {_battleAiDecks.Count} custom AI decks, {_battleAiProfiles.Count} native AI presets, {_battleClosedSlots.Count} closed slots.");
            }
            catch (Exception e)
            {
                ClearBattleAiDecks();
                MelonLogger.Error("[LocalSkirmish] Bind deck choices: " + e);
                if (needsExactPlan) throw new InvalidOperationException("Selected slots and AI presets could not be bound; scenario initialization stopped.", e);
            }
        }

        internal void OverrideDeckAssignment(int playerUid, ref IDeckDataModel inputDeck)
        {
            if (!_deckOverridesActive) return;
            try
            {
                if (!MatchesBattleScenario()) { ClearBattleAiDecks(); return; }
                var human = _deckSession.CurrentPlayer;
                if (human == null) { ClearBattleAiDecks(); return; }
                IDeckDataModel chosen;
                if (playerUid == human.UID) chosen = _battlePlayerDeck;
                else
                {
                    var player = _deckSession.GetPlayer(playerUid);
                    if (player == null || !player.IsBot || !_battleAiDecks.TryGetValue(playerUid, out chosen)) return;
                }
                if (chosen == null) return;
                string originalName = inputDeck == null ? "(none)" : inputDeck.Name;
                inputDeck = chosen;
                MelonLogger.Msg($"[LocalSkirmish] Player ID {playerUid}: deck request '{originalName}' -> selected '{chosen.Name}'.");
                // Let SetDeck run normally. Its returned clone is then used by the game's
                // economy/shop updates, including UnitGroupAndNameSystem.ChangePlayerDeck.
            }
            catch (Exception e)
            {
                ClearBattleAiDecks();
                MelonLogger.Error("[LocalSkirmish] Override deck assignment: " + e);
            }
        }

        // Replace the node input BEFORE native OnActivated calculates its completion count.
        // The request retains this new object during async spawning; the node's original
        // shared input is restored by the postfix, without changing any scenario pool.
        internal void OverrideAiSpawn(NodeSpawnMultiUnits node)
        {
            if (!_deckOverridesActive || node == null || node.PlayerOwner == null) return;
            int uid = node.PlayerOwner.DataUID;
            IDeckDataModel deck = null;
            try
            {
                if (!MatchesBattleScenario()) { ClearBattleAiDecks(); return; }
                var human = _deckSession.CurrentPlayer;
                if (human == null) { ClearBattleAiDecks(); return; }
                if (uid == human.UID) return;
                if (_battleClosedSlots.Contains(uid))
                {
                    node.UnitInfo = new MultiUnits { Units = new Il2CppReferenceArray<UnitModData>(0) };
                    node.Units = new UnitObject(0); return;
                }
                if (!_battleAiDecks.TryGetValue(uid, out deck)) return;
                var player = _deckSession.GetPlayer(uid);
                if (player == null || !player.IsBot) return;
                var original = node.UnitInfo;
                if (original == null || original.Units == null || original.Units.Length == 0) return;

                var slots = new List<DeckSlotModel>();
                if (deck?.GetSlots != null)
                    foreach (var pair in deck.GetSlots)
                        if (pair.Value != null)
                            foreach (var slot in pair.Value)
                                if (slot != null && slot.IsValid && slot.Count > 0) slots.Add(slot);

                DataBaseService database = null;
                Session.TryGetService<DataBaseService>(out database, false);
                var replacements = new List<UnitModData>();
                foreach (var requested in original.Units)
                    ReplaceSpawnUnit(requested, slots, database, replacements);
                node.UnitInfo = new MultiUnits { Units = new Il2CppReferenceArray<UnitModData>(replacements.ToArray()) };
                // Native empty-input handling completes the node immediately. Clear the
                // previous activation's output so it cannot send old units down the graph.
                if (replacements.Count == 0) node.Units = new UnitObject(0);

                _aiWaveCounts.TryGetValue(uid, out int wave);
                _aiWaveCounts[uid] = ++wave;
                if (wave <= 3 || wave % 50 == 0)
                {
                    var ids = new List<string>();
                    foreach (var unit in replacements)
                    {
                        ids.Add(unit.UnitID.ToString());
                        foreach (var cargo in unit.Cargo.Slots) ids.Add("cargo:" + cargo.UnitID);
                    }
                    MelonLogger.Msg($"[LocalSkirmish] AI spawn UID {uid}, deck '{deck?.Name}', wave {wave}: "
                        + $"{original.TotalUnitsCount} -> {node.UnitInfo.TotalUnitsCount} units [{string.Join(",", ids)}].");
                }
            }
            catch (Exception e)
            {
                // Once this bot's deck is selected, a conversion error must not silently
                // spawn the stock roster. An empty wave follows the native completion path.
                if (deck != null)
                {
                    node.UnitInfo = new MultiUnits { Units = new Il2CppReferenceArray<UnitModData>(0) };
                    node.Units = new UnitObject(0);
                }
                MelonLogger.Error($"[LocalSkirmish] AI spawn UID {uid}: {e}");
            }
        }

        static void ReplaceSpawnUnit(UnitModData requested, List<DeckSlotModel> slots,
            DataBaseService database, List<UnitModData> output)
        {
            if (requested == null) return;
            // A scripted loaded transport represents its passengers' deck cards. Build
            // each passenger with the transport actually selected on that card, instead
            // of retaining a stock vehicle or putting unrelated cargo in it.
            var passengers = requested.Cargo?.Slots;
            if (passengers != null && passengers.Count > 0)
            {
                foreach (var passenger in passengers) ReplaceSpawnUnit(passenger, slots, database, output);
                return;
            }
            var selected = PickSpawnSlot(requested, slots, database);
            if (selected == null) return; // Missing categories never fall back to default units.
            var unit = MakeSpawnUnit(selected.UnitID, selected.Category, selected.UnitSkinId, selected.ModList);
            if (selected.IsValidTransport && selected.TransportCount > 0)
            {
                var transportData = selected.GetTransportUnitData();
                if (transportData != null)
                {
                    var transport = MakeSpawnUnit(selected.TransportID, transportData.CategoryType,
                        selected.TransportSkinId, selected.TransportModList);
                    transport.Cargo.Slots.Add(unit);
                    output.Add(transport);
                    return;
                }
            }
            output.Add(unit);
        }

        static DeckSlotModel PickSpawnSlot(UnitModData requested, List<DeckSlotModel> slots, DataBaseService database)
        {
            var original = database?.GetUnitById(requested.UnitID, false);
            DeckSlotModel selected = null;
            int bestScore = -1, weight = 0;
            foreach (var slot in slots)
            {
                if (slot.Category != requested.Category) continue;
                var data = slot.GetUnitData();
                if (data == null) continue;
                // Prefer the same physical type and role, such as AA versus artillery.
                // Card counts weight variety, but the scenario still controls wave timing.
                int score = original == null ? 0
                    : (data.Type == original.Type ? 4 : 0) + (data.Role == original.Role ? 2 : 0);
                if (score < bestScore) continue;
                if (score > bestScore) { bestScore = score; weight = 0; }
                weight += slot.Count;
                if (_rng.Next(weight) < slot.Count) selected = slot;
            }
            return selected;
        }

        static UnitModData MakeSpawnUnit(int id, UnitCategoryType category, int skin,
            Il2CppSystem.Collections.Generic.List<DeckModData> mods)
        {
            var unit = new UnitModData
            {
                UnitID = id, Category = category, SkinId = skin,
                Items = new Il2CppSystem.Collections.Generic.List<ModDataItem>(),
                Cargo = new CargoData { Slots = new Il2CppSystem.Collections.Generic.List<UnitModData>() }
            };
            if (mods != null)
                foreach (var mod in mods)
                    if (mod != null) unit.Items.Add(new ModDataItem(mod.ModID, mod.OptionID));
            return unit;
        }

        void RefreshOptions()
        {
            _opts.Clear(); _optVals.Clear();
            if (_mapIdx < 0 || _mapIdx >= _maps.Count) return;
            try
            {
                var meta0 = _maps[_mapIdx].MetaFile;
                _hasDiff = meta0 != null && meta0.HasDifficulty;
                int d = 2; try { d = (int)meta0.MEStartDifficulty; } catch { }
                _diff = TypeOf(_maps[_mapIdx]) == ScenarioType.Skirmish ? 2 : (d >= 1 && d <= 3) ? d : 2;
            }
            catch { _hasDiff = true; _diff = 2; }
            try
            {
                var meta = _maps[_mapIdx].MetaFile;
                var pos = meta != null ? meta.PublicOptions : null;
                int n = pos != null ? pos.Count : 0;
                for (int i = 0; i < n; i++)
                {
                    ScenarioPublicOption po = null; try { po = pos[i]; } catch { continue; }
                    if (po == null) continue;
                    var od = new OptDef();
                    try { od.Uid = po.Uid; } catch { }
                    try { od.RawName = po.Name; od.Label = LobbyText.OptionName(po.Name, Clean(po.Name)); }
                    catch { od.Label = "Option " + od.Uid; }
                    try { od.Order = po.Order; } catch { }
                    try { od.Default = po.DefaultValue; } catch { }
                    try { od.Min = po.MinValue; od.Max = po.MaxValue; od.Step = po.StepValue > 0 ? po.StepValue : 1; } catch { }
                    try
                    {
                        var dd = po.DropdownValues;
                        if (dd != null && dd.Count > 0)
                        {
                            od.IsEnum = true;
                            var keys = new List<int>();
                            foreach (var kv in dd) keys.Add(kv.Key);
                            keys.Sort();
                            foreach (int key in keys)
                            {
                                od.EnumVals.Add(key);
                                od.EnumLabels.Add(LobbyText.OptionValue(od.RawName, dd[key], Clean(dd[key])));
                            }
                        }
                    }
                    catch { }
                    if (!_optVals.ContainsKey(od.Uid))
                    { _opts.Add(od); _optVals[od.Uid] = od.Default; }
                }
                _opts.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Uid.CompareTo(b.Uid));
            }
            catch (Exception e) { MelonLogger.Error("[LocalSkirmish] RefreshOptions: " + e.Message); }
        }

        string OptValueText(OptDef od)
        {
            int v = _optVals.TryGetValue(od.Uid, out var x) ? x : od.Default;
            if (od.IsEnum)
            {
                int idx = od.EnumVals.IndexOf(v);
                if (idx >= 0 && idx < od.EnumLabels.Count) return od.EnumLabels[idx];
                return v.ToString();
            }
            return v.ToString();
        }

        void CycleOpt(OptDef od, int dir)
        {
            int v = _optVals.TryGetValue(od.Uid, out var x) ? x : od.Default;
            if (od.IsEnum && od.EnumVals.Count > 0)
            {
                int idx = od.EnumVals.IndexOf(v); if (idx < 0) idx = 0;
                idx = (idx + dir + od.EnumVals.Count) % od.EnumVals.Count;
                v = od.EnumVals[idx];
            }
            else
            {
                v += dir * (od.Step > 0 ? od.Step : 1);
                if (od.Max > od.Min) { if (v < od.Min) v = od.Min; if (v > od.Max) v = od.Max; }
            }
            _optVals[od.Uid] = v;
            UpdateLabels();
        }

        void CycleDeck(int dir)
        { if (_decks.Count == 0) return; _deckIdx = (_deckIdx + dir + _decks.Count) % _decks.Count; UpdateLabels(); }
        void CycleAiDeck(int dir)
        {
            if (!IsSkirmishMap() || _aiSlotIdx < 0 || _aiSlotIdx >= _aiSlots.Count) return;
            // Include the default entry when wrapping in either direction.
            int count = _decks.Count + 1;
            var slot = _aiSlots[_aiSlotIdx];
            slot.DeckIndex = ((slot.DeckIndex + 1 + dir) % count + count) % count - 1;
            UpdateLabels();
        }
        void CycleAiPlayer(int dir)
        {
            if (_aiSlots.Count == 0) return;
            int count = _aiSlots.Count;
            _aiSlotIdx = ((_aiSlotIdx + dir) % count + count) % count;
            UpdateLabels();
        }
        void CycleDiff(int dir)
        { _diff += dir; if (_diff < 1) _diff = 3; if (_diff > 3) _diff = 1; UpdateLabels(); }
        void StartBattle()
        {
            try
            {
                string problem = GetLaunchProblem();
                if (problem != null) { if (_statusLabel != null) _statusLabel.text = problem; return; }
                if (_mapIdx < 0 || _mapIdx >= _maps.Count) { if (_statusLabel != null) _statusLabel.text = "Pick a map."; return; }
                if (_deckIdx < 0 || _deckIdx >= _decks.Count) { if (_statusLabel != null) _statusLabel.text = "This map has no valid deck for you."; return; }
                var scenario = _maps[_mapIdx];
                var deck = _decks[_deckIdx];
                if (IsSkirmishMap() && !_aiDeckPatchReady)
                {
                    if (_statusLabel != null) _statusLabel.text = "Skirmish deck support could not load. Check the MelonLoader log.";
                    return;
                }
                var st = ISceneTransition.Instance;
                if (st == null) { if (_statusLabel != null) _statusLabel.text = "Scene transition unavailable."; return; }
                SaveCurrent();
                PrepareDecks(scenario, deck);

                // difficulty (scenario scripts read CampaignService.Difficulty)
                if (_hasDiff)
                {
                    try
                    {
                        CampaignService cs = null; Session.TryGetService<CampaignService>(out cs, false);
                        if (cs != null) { cs.Difficulty = (DifficultyLevel)_diff; MelonLogger.Msg($"[LocalSkirmish] Difficulty = {_diffNames[_diff]}."); }
                        else MelonLogger.Warning("[LocalSkirmish] CampaignService not available; difficulty not set.");
                    }
                    catch (Exception e) { MelonLogger.Error("[LocalSkirmish] set difficulty: " + e.Message); }
                }

                // hand our option choices to the applier (runs after ChangeScene sets up the load)
                PendingOptions.Clear();
                foreach (var kv in _optVals) PendingOptions[kv.Key] = kv.Value;
                foreach (var option in _opts)
                    MelonLogger.Msg($"[LocalSkirmish] Requested option UID {option.Uid} '{option.RawName}' = {_optVals[option.Uid]}."
                        + (string.Equals(option.RawName, "ui_lobby_Size_Title", StringComparison.OrdinalIgnoreCase)
                            ? " This is a requested size option, not a measured resolved play area." : ""));

                MelonLogger.Msg($"[LocalSkirmish] Launching '{_mapNames[_mapIdx]}' player deck '{_deckNames[_deckIdx]}', {_battleAiDecks.Count} custom AI deck(s), {_battleAiProfiles.Count} native preset(s), {_battleClosedSlots.Count} closed slot(s), {PendingOptions.Count} option(s).");
                DestroyUI();
                st.ChangeScene(scenario);
                _appliedLogged = false;
                _applyFrames = 300;      // keep pushing options in until the battle load consumes them
                ApplyOptionsOnce();      // and try immediately
            }
            catch (Exception e)
            {
                ClearBattleAiDecks();
                _applyFrames = 0; PendingOptions.Clear();
                if (_root == null) BuildUI();
                if (_statusLabel != null) _statusLabel.text = e.Message;
                MelonLogger.Error("[LocalSkirmish] StartBattle: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(MissionLoader), nameof(MissionLoader.LoadPlayersDeck))]
    static class AiPlayerDeckPatch
    {
        static void Prefix(MissionLoader __instance) { Core.Instance?.BeginDeckLoad(__instance); }
    }

    [HarmonyPatch(typeof(SharedPlayerDeck), nameof(SharedPlayerDeck.SetDeck))]
    static class DeckAssignmentPatch
    {
        static void Prefix(int playerUID, ref IDeckDataModel inputDeck)
        { Core.Instance?.OverrideDeckAssignment(playerUID, ref inputDeck); }
    }

    [HarmonyPatch(typeof(GameController), nameof(GameController.Dispose))]
    static class EndBattleDeckPatch
    {
        static void Prefix() { Core.Instance?.StopDeckOverrides(); }
    }

    [HarmonyPatch(typeof(NodeSpawnMultiUnits), nameof(NodeSpawnMultiUnits.OnActivated))]
    static class AiSpawnPatch
    {
        static void Prefix(NodeSpawnMultiUnits __instance, out MultiUnits __state)
        {
            __state = __instance.UnitInfo;
            Core.Instance?.OverrideAiSpawn(__instance);
            Core.Instance?.TraceSpawnDecision(__instance, __state);
        }

        static void Postfix(NodeSpawnMultiUnits __instance, MultiUnits __state)
        { __instance.UnitInfo = __state; }
    }

    [HarmonyPatch(typeof(NetworkLobbyService), nameof(NetworkLobbyService.CreateLobby))]
    static class CreateLobbyPatch
    {
        static bool Prefix(LobbyType lobbyType, ScenarioSource initScenario, bool isPrivateLobby)
        {
            try
            {
                if (lobbyType == LobbyType.Skirmish)
                {
                    MelonLogger.Msg("[LocalSkirmish] Skirmish clicked -> opening setup panel.");
                    Core.Instance?.OpenSetup();
                    return false;
                }
            }
            catch (Exception e) { MelonLogger.Error("[LocalSkirmish] Prefix error: " + e); }
            return true;
        }
    }
}

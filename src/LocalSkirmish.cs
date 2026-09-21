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

[assembly: MelonInfo(typeof(BALocalSkirmish.Core), "BA Local Skirmish", "2.4.1", "BovineOverlord")]
[assembly: MelonGame(null, null)]

namespace BALocalSkirmish
{
    // One configurable game option (map size, day/night, time limit, starting income, etc.),
    // read from the scenario's own public-option definitions.
    class OptDef
    {
        public int Uid;
        public string Label;
        public bool IsEnum;
        public List<int> EnumVals = new List<int>();
        public List<string> EnumLabels = new List<string>();
        public int Min, Max, Step, Default;
    }

    // v2.2: Skirmish setup panel with map + deck + the scenario's own game options (income, time
    // limit, day/night, map size, ...). Options are injected into SceneLoadManager.ScenarioPublicOptions
    // at load time so the local battle honours them.
    public class Core : MelonMod
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
        readonly List<OptDef> _opts = new List<OptDef>();
        readonly Dictionary<int, int> _optVals = new Dictionary<int, int>();
        int _diff = 2;          // 1=Easy, 2=Medium, 3=Hard (DifficultyLevel values)
        bool _hasDiff = true;
        static readonly string[] _diffNames = { "None", "Easy", "Medium", "Hard" };

        // UI
        GameObject _root;
        Text _mapLabel, _deckLabel, _statusLabel, _diffLabel, _previewCaption;
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
            LoggerInstance.Msg("BA Local Skirmish v2.4.1 initializing...");
            Patch(typeof(CreateLobbyPatch));
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
            if (_applyFrames <= 0) return;
            _applyFrames--;
            if (PendingOptions.Count == 0) { _applyFrames = 0; return; }
            if (ApplyOptionsOnce() && !_appliedLogged)
            {
                _appliedLogged = true;
                MelonLogger.Msg($"[LocalSkirmish] Applied {PendingOptions.Count} game option(s) to the load.");
            }
        }
        void Patch(Type t)
        {
            try { HarmonyInstance.CreateClassProcessor(t).Patch(); LoggerInstance.Msg("Patched " + t.Name); }
            catch (Exception e) { LoggerInstance.Error("Patch failed for " + t.Name + ": " + e); }
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
            foreach (var s in skirmish) { _maps.Add(s); _mapNames.Add(NameOf(s) + "  (skirmish)"); }
            foreach (var s in pve) { _maps.Add(s); _mapNames.Add(NameOf(s)); }
            foreach (var s in solo) { _maps.Add(s); _mapNames.Add(NameOf(s) + "  (mission)"); }
        }

        void RefreshDecks()
        {
            _decks.Clear(); _deckNames.Clear(); _deckIdx = 0;
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

        void RefreshOptions()
        {
            _opts.Clear(); _optVals.Clear();
            if (_mapIdx < 0 || _mapIdx >= _maps.Count) return;
            try
            {
                var meta0 = _maps[_mapIdx].MetaFile;
                _hasDiff = meta0 != null && meta0.HasDifficulty;
                int d = 2; try { d = (int)meta0.MEStartDifficulty; } catch { }
                _diff = (d >= 1 && d <= 3) ? d : 2;
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
                    try { od.Label = Clean(po.Name); } catch { od.Label = "Opt " + od.Uid; }
                    try { od.Default = po.DefaultValue; } catch { }
                    try { od.Min = po.MinValue; od.Max = po.MaxValue; od.Step = po.StepValue > 0 ? po.StepValue : 1; } catch { }
                    try
                    {
                        var dd = po.DropdownValues;
                        if (dd != null && dd.Count > 0)
                        {
                            od.IsEnum = true;
                            foreach (var kv in dd) { od.EnumVals.Add(kv.Key); od.EnumLabels.Add(Clean(kv.Value)); }
                        }
                    }
                    catch { }
                    _opts.Add(od);
                    _optVals[od.Uid] = od.Default;
                }
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

        // ---------- UI ----------
        RectTransform RT(GameObject go) => go.GetComponent<RectTransform>();
        void Place(GameObject go, Transform parent, float x, float y, float w, float h)
        {
            go.transform.SetParent(parent, false);
            var rt = RT(go);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
        }
        Text MakeLabel(Transform p, float x, float y, float w, float h, string s, int size, TextAnchor a, Color? c = null)
        {
            var go = new GameObject("L"); var t = go.AddComponent<Text>(); Place(go, p, x, y, w, h);
            t.font = _font; t.fontSize = size; t.alignment = a; t.color = c ?? Color.white; t.text = s;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
        Image MakeImage(Transform p, float x, float y, float w, float h, Color col)
        {
            var go = new GameObject("I"); var img = go.AddComponent<Image>(); Place(go, p, x, y, w, h); img.color = col; return img;
        }
        void MakeButton(Transform p, float x, float y, float w, float h, string label, Action onClick, Color? col = null, int fontSize = 15)
        {
            var go = new GameObject("B"); var img = go.AddComponent<Image>(); Place(go, p, x, y, w, h);
            img.color = col ?? CBtn;
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>((Action)onClick));
            MakeLabel(go.transform, 0f, 0f, w, h, label, fontSize, TextAnchor.MiddleCenter);
        }

        // Load a scenario's preview.png into a Sprite (best-effort).
        Sprite LoadPreview(ScenarioSource s)
        {
            try
            {
                string path = null;
                try { path = s.PreviewPath; } catch { }
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    string folder = null; try { folder = s.Folder; } catch { }
                    if (!string.IsNullOrEmpty(folder)) path = Path.Combine(folder, "preview.png");
                }
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                byte[] bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2);
                ImageConversion.LoadImage(tex, (Il2CppStructArray<byte>)bytes);
                return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            catch (Exception e) { MelonLogger.Warning("[LocalSkirmish] preview load: " + e.Message); return null; }
        }

        void SetPreview()
        {
            if (_previewImg == null) return;
            Sprite sp = (_mapIdx >= 0 && _mapIdx < _maps.Count) ? LoadPreview(_maps[_mapIdx]) : null;
            _previewImg.sprite = sp;
            _previewImg.preserveAspect = true;
            _previewImg.color = sp != null ? Color.white : new Color(0.06f, 0.07f, 0.09f, 1f);
        }

        void BuildUI()
        {
            DestroyUI();
            if (_font == null)
            {
                try { _font = Font.CreateDynamicFontFromOSFont("Arial", 16); } catch { }
                if (_font == null) { try { _font = Font.CreateDynamicFontFromOSFont("Segoe UI", 16); } catch { } }
            }
            _optLabels.Clear();

            _root = new GameObject("BASkirmishSetup");
            UnityEngine.Object.DontDestroyOnLoad(_root);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30000;
            _root.AddComponent<GraphicRaycaster>();
            MakeImage(_root.transform, 0f, 0f, 5000f, 3000f, CDim); // dim backdrop

            const float W = 860f;
            const float rowH = 34f;
            const float headH = 52f;
            int optRows = Math.Min(_opts.Count, 6);
            int rows = 2 + (_hasDiff ? 1 : 0) + optRows; // map, deck, [diff], options
            float bodyH = rows * rowH;
            float h = headH + 24f + bodyH + 64f + 30f;   // header + pad + rows + buttons + status
            if (h < 380f) h = 380f;                       // room for preview

            // frame + panel
            MakeImage(_root.transform, 0f, 0f, W + 4f, h + 4f, CFrame);
            var panel = MakeImage(_root.transform, 0f, 0f, W, h, CPanel);
            var pt = panel.transform;
            float top = h / 2f;

            // header bar
            MakeImage(pt, 0f, top - headH / 2f, W, headH, CHeader);
            MakeLabel(pt, 0f, top - headH / 2f, W - 40f, 34f, "Local AI Skirmish", 24, TextAnchor.MiddleCenter);

            // layout: controls on the left column, preview on the right
            float colX = -W / 2f + 30f;      // left edge of controls
            float labX = colX + 90f;         // label center
            float ltX = colX + 175f;         // "<" center
            float valX = colX + 210f;        // value left
            float rtX = colX + 470f;         // ">" center
            float y = top - headH - 22f;

            void Row(string name, Color nameCol, string val, Action dec, Action inc, Action<Text> keep)
            {
                MakeImage(pt, colX + 250f, y, 500f, rowH - 4f, CRow);
                MakeLabel(pt, labX, y, 170f, 26f, name, 15, TextAnchor.MiddleLeft, nameCol);
                MakeButton(pt, ltX, y, 30f, 26f, "<", dec);
                var vl = MakeLabel(pt, valX + 130f, y, 260f, 26f, val, 15, TextAnchor.MiddleLeft, CVal);
                MakeButton(pt, rtX, y, 30f, 26f, ">", inc);
                keep?.Invoke(vl);
                y -= rowH;
            }

            Row("Map", CLabel, "-", () => CycleMap(-1), () => CycleMap(1), t => _mapLabel = t);
            Row("Deck", CLabel, "-", () => CycleDeck(-1), () => CycleDeck(1), t => _deckLabel = t);
            if (_hasDiff)
                Row("Difficulty", new Color(1f, 0.82f, 0.55f), _diffNames[_diff], () => CycleDiff(-1), () => CycleDiff(1), t => _diffLabel = t);
            for (int i = 0; i < optRows; i++)
            {
                var od = _opts[i];
                Row(od.Label, new Color(0.85f, 0.85f, 0.7f), OptValueText(od), () => CycleOpt(od, -1), () => CycleOpt(od, 1), t => _optLabels.Add(t));
            }

            // preview (right side)
            float pvW = 300f, pvH = 170f;
            float pvX = W / 2f - pvW / 2f - 28f;
            float pvY = top - headH - 22f - pvH / 2f + 13f;
            MakeImage(pt, pvX, pvY, pvW + 4f, pvH + 4f, CFrame);
            _previewImg = MakeImage(pt, pvX, pvY, pvW, pvH, new Color(0.06f, 0.07f, 0.09f, 1f));
            _previewCaption = MakeLabel(pt, pvX, pvY - pvH / 2f - 16f, pvW + 20f, 22f, "", 13, TextAnchor.MiddleCenter, new Color(0.8f, 0.85f, 0.95f));

            // buttons
            float by = -top + 74f;
            MakeButton(pt, colX + 110f, by, 200f, 40f, "Start Battle", StartBattle, CStart, 17);
            MakeButton(pt, colX + 330f, by, 150f, 40f, "Random", RandomMap, CBtn, 15);
            MakeButton(pt, W / 2f - 90f, by, 120f, 40f, "Cancel", CloseSetup, CCancel, 15);

            _statusLabel = MakeLabel(pt, 0f, -top + 26f, W - 40f, 24f, "", 12, TextAnchor.MiddleCenter, new Color(0.72f, 0.75f, 0.8f));

            SetPreview();
            UpdateLabels();
        }

        void DestroyUI()
        {
            if (_root != null) { try { UnityEngine.Object.Destroy(_root); } catch { } _root = null; }
            _mapLabel = _deckLabel = _statusLabel = _diffLabel = _previewCaption = null; _previewImg = null; _optLabels.Clear();
        }

        void UpdateLabels()
        {
            if (_mapLabel != null) _mapLabel.text = _maps.Count > 0 ? $"{_mapIdx + 1}/{_maps.Count}  {_mapNames[_mapIdx]}" : "(none)";
            if (_previewCaption != null) _previewCaption.text = _maps.Count > 0 ? _mapNames[_mapIdx] : "";
            if (_deckLabel != null) _deckLabel.text = _decks.Count > 0 ? $"{_deckIdx + 1}/{_decks.Count}  {_deckNames[_deckIdx]}" : "(no valid decks)";
            if (_diffLabel != null) _diffLabel.text = _diffNames[Math.Max(0, Math.Min(3, _diff))];
            for (int i = 0; i < _optLabels.Count && i < _opts.Count; i++)
                if (_optLabels[i] != null) _optLabels[i].text = OptValueText(_opts[i]);
            if (_statusLabel != null)
            {
                string extra = _opts.Count > 0 ? $"{_opts.Count} map option(s)." : "This map has no extra options.";
                _statusLabel.text = $"Map {_mapIdx + 1}/{_maps.Count}.  {extra}  Set difficulty & deck, then Start.";
            }
        }

        // ---------- actions ----------
        public void OpenSetup()
        {
            if (_root != null) return;
            BuildMapList(); _mapIdx = 0; RefreshDecks(); RefreshOptions(); BuildUI();
            MelonLogger.Msg($"[LocalSkirmish] Setup opened ({_maps.Count} maps).");
        }
        void CloseSetup() { DestroyUI(); }
        void CycleMap(int dir)
        {
            if (_maps.Count == 0) return;
            _mapIdx = (_mapIdx + dir + _maps.Count) % _maps.Count;
            RefreshDecks(); RefreshOptions(); BuildUI();
        }
        void CycleDeck(int dir)
        { if (_decks.Count == 0) return; _deckIdx = (_deckIdx + dir + _decks.Count) % _decks.Count; UpdateLabels(); }
        void CycleDiff(int dir)
        { _diff += dir; if (_diff < 1) _diff = 3; if (_diff > 3) _diff = 1; UpdateLabels(); }
        void RandomMap()
        {
            if (_maps.Count == 0) return; _mapIdx = _rng.Next(_maps.Count);
            RefreshDecks(); RefreshOptions(); BuildUI();
        }

        void StartBattle()
        {
            try
            {
                if (_mapIdx < 0 || _mapIdx >= _maps.Count) { if (_statusLabel != null) _statusLabel.text = "Pick a map."; return; }
                if (_deckIdx < 0 || _deckIdx >= _decks.Count) { if (_statusLabel != null) _statusLabel.text = "This map has no valid deck for you."; return; }
                var scenario = _maps[_mapIdx];
                var deck = _decks[_deckIdx];

                PreloadSharedPlayerDeck.IsMissionRestart = false;
                PreloadSharedPlayerDeck.ScenarioStartDeck = deck;
                PreloadSharedPlayerDeck.Alpha = deck;
                PreloadSharedPlayerDeck.Bravo = deck;

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

                var st = ISceneTransition.Instance;
                if (st == null) { if (_statusLabel != null) _statusLabel.text = "Scene transition unavailable."; return; }

                MelonLogger.Msg($"[LocalSkirmish] Launching '{_mapNames[_mapIdx]}' deck '{_deckNames[_deckIdx]}' with {PendingOptions.Count} option(s).");
                DestroyUI();
                st.ChangeScene(scenario);
                _appliedLogged = false;
                _applyFrames = 300;      // keep pushing options in until the battle load consumes them
                ApplyOptionsOnce();      // and try immediately
            }
            catch (Exception e) { MelonLogger.Error("[LocalSkirmish] StartBattle: " + e); }
        }
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

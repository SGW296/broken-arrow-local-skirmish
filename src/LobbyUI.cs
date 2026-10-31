using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppBrokenArrow.MissionEditor.MissionResolver;
using Il2CppBrokenArrow.Client.Ecs.Decks.Models;

namespace BALocalSkirmish
{
    public partial class Core
    {
        static readonly string[] FilterNames = { "Skirmish", "PvE / co-op", "Campaign / missions", "All local scenarios" };
        LobbySettings _settings;
        string _settingsPath, _notice = "", _presetName = "My setup";
        bool _settingsWritable = true;
        bool _saveFailed;
        int _filter, _optionPage;
        Text _teamSummary;
        Button _startButton;
        MapPreviewResult _previewResult;
        int _previewRetryFrames;
        GameObject _picker, _pickerRows;
        Text _pickerCount;
        Button _pickerPrev, _pickerNext;
        int _pickerPage;
        string _pickerSearch = "";
        List<Choice> _pickerChoices;
        Action<int> _pick;

        sealed class Choice
        {
            public int Value;
            public string Label, Detail;
            public Choice(int value, string label, string detail = "") { Value = value; Label = label; Detail = detail; }
        }

        static string ScenarioKey(ScenarioSource scenario)
        {
            // Include the folder to distinguish Workshop scenarios with identical names.
            return ((int)TypeOf(scenario)) + "|" + scenario.Folder + "|" + scenario.Name;
        }

        static string DeckKey(IDeckDataModel deck)
        {
            try
            {
                return deck.CountryID + "|" + deck.Spec1ID + "|" + deck.Spec2ID + "|"
                    + (string.IsNullOrEmpty(deck.FileName) ? "name:" + deck.Name : "file:" + deck.FileName);
            }
            catch { return ""; }
        }

        int FindDeck(string key)
        {
            if (string.IsNullOrEmpty(key)) return -1;
            int found = -1;
            for (int i = 0; i < _decks.Count; i++)
            {
                if (DeckKey(_decks[i]) != key) continue;
                if (found >= 0) return -1; // Ambiguous identities must not silently select the wrong deck.
                found = i;
            }
            return found;
        }

        SetupSnapshot CaptureSetup()
        {
            var setup = new SetupSnapshot { Difficulty = _diff };
            if (_deckIdx >= 0 && _deckIdx < _decks.Count) setup.PlayerDeck = DeckKey(_decks[_deckIdx]);
            foreach (var pair in _optVals) setup.Options[pair.Key] = pair.Value;
            foreach (var slot in _aiSlots)
                if (slot.DeckIndex >= 0 && slot.DeckIndex < _decks.Count)
                    setup.AiDecks[slot.PlayerUid] = DeckKey(_decks[slot.DeckIndex]);
            return setup;
        }

        void RestoreSetup(SetupSnapshot setup)
        {
            if (setup == null) return;
            bool changed = false;
            int player = FindDeck(setup.PlayerDeck);
            if (player >= 0) _deckIdx = player;
            else if (!string.IsNullOrEmpty(setup.PlayerDeck)) changed = true;
            if (_hasDiff && setup.Difficulty >= 1 && setup.Difficulty <= 3) _diff = setup.Difficulty;
            else if (_hasDiff) changed = true;
            if (setup.Options != null)
            {
                foreach (var pair in setup.Options)
                {
                    var option = _opts.Find(o => o.Uid == pair.Key);
                    if (option != null && option.Accepts(pair.Value)) _optVals[pair.Key] = pair.Value;
                    else changed = true;
                }
            }
            if (setup.AiDecks != null)
            {
                foreach (var pair in setup.AiDecks)
                {
                    var slot = _aiSlots.Find(s => s.PlayerUid == pair.Key);
                    int deck = FindDeck(pair.Value);
                    if (slot != null && deck >= 0) slot.DeckIndex = deck;
                    else changed = true;
                }
            }
            if (changed) _notice = "Some saved choices are unavailable; scenario defaults were used for those choices.";
        }

        void ReadSettings()
        {
            if (_settings != null) return;
            _settingsPath = Path.Combine(MelonEnvironment.UserDataDirectory, "BALocalSkirmish", "lobby.json");
            try { _settings = LobbySettings.Load(_settingsPath); }
            catch (Exception e)
            {
                // Preserve unreadable/newer settings for diagnosis instead of overwriting them.
                _settings = new LobbySettings(); _settingsWritable = false;
                _notice = "Saved settings could not be read. They were preserved; saving is disabled this session.";
                MelonLogger.Warning("[LocalSkirmish] Read settings: " + e.Message);
            }
            _filter = _settings.ScenarioFilter;
        }

        void SaveCurrent()
        {
            if (_settings == null) return;
            _settings.ScenarioFilter = _filter;
            if (_mapIdx >= 0 && _mapIdx < _maps.Count)
            {
                string key = ScenarioKey(_maps[_mapIdx]);
                _settings.LastScenario = key;
                _settings.Scenarios[key] = CaptureSetup();
            }
            WriteSettings();
        }

        void WriteSettings()
        {
            if (!_settingsWritable) return;
            try { _settings.Save(_settingsPath); _saveFailed = false; }
            catch (Exception e)
            {
                _saveFailed = true;
                _notice = "Could not save settings. Your current selections still work for this session.";
                MelonLogger.Warning("[LocalSkirmish] Save settings: " + e.Message);
            }
        }

        void SelectMap(int index)
        {
            if (index < 0 || index >= _maps.Count) return;
            SaveCurrent();
            _mapIdx = index; _optionPage = 0; _teamPage = _rosterPage = 0; _notice = "";
            RefreshDecks(); RefreshOptions();
            if (_settings.Scenarios.TryGetValue(ScenarioKey(_maps[index]), out var saved)) RestoreSetup(saved);
            BuildUI();
        }

        string Category(ScenarioSource scenario)
        {
            var type = TypeOf(scenario);
            return type == ScenarioType.Skirmish ? "Skirmish" : type == ScenarioType.MultiplayerPVE ? "PvE / co-op" : "Campaign / mission";
        }

        List<int> FilteredMaps()
        {
            var result = new List<int>();
            for (int i = 0; i < _maps.Count; i++)
            {
                var type = TypeOf(_maps[i]);
                if (_filter == 3 || (_filter == 0 && type == ScenarioType.Skirmish)
                    || (_filter == 1 && type == ScenarioType.MultiplayerPVE)
                    || (_filter == 2 && type == ScenarioType.Singleplayer)) result.Add(i);
            }
            return result;
        }

        public void OpenSetup()
        {
            if (_root != null) return;
            ClearBattleAiDecks(); ReadSettings(); BuildMapList();
            _mapIdx = _maps.FindIndex(s => ScenarioKey(s) == _settings.LastScenario);
            if (_mapIdx < 0)
            {
                var available = FilteredMaps();
                _mapIdx = available.Count > 0 ? available[0] : (_maps.Count > 0 ? 0 : -1);
            }
            _optionPage = 0; _teamPage = _rosterPage = 0;
            RefreshDecks(); RefreshOptions();
            if (_mapIdx >= 0 && _settings.Scenarios.TryGetValue(ScenarioKey(_maps[_mapIdx]), out var saved)) RestoreSetup(saved);
            BuildUI();
            MelonLogger.Msg($"[LocalSkirmish] Setup opened ({_maps.Count} local scenarios; filter {FilterNames[_filter]}).");
        }

        void CloseSetup() { SaveCurrent(); DestroyUI(); }

        void RandomMap()
        {
            var available = FilteredMaps();
            if (available.Count > 0) SelectMap(available[_rng.Next(available.Count)]);
        }

        void PickFilter()
        {
            var choices = new List<Choice>();
            for (int i = 0; i < FilterNames.Length; i++) choices.Add(new Choice(i, FilterNames[i]));
            OpenPicker("Scenario category", choices, value =>
            {
                _filter = value;
                var available = FilteredMaps();
                if (available.Count > 0 && !available.Contains(_mapIdx)) SelectMap(available[0]);
                else
                {
                    _notice = available.Count == 0 ? "No scenarios in this category. The current selection is unchanged." : "";
                    SaveCurrent(); BuildUI();
                }
            });
        }

        void PickMap()
        {
            var choices = new List<Choice>();
            foreach (int i in FilteredMaps())
            {
                string raw = NameOf(_maps[i]);
                bool duplicate = _maps.FindAll(s => NameOf(s) == raw).Count > 1;
                choices.Add(new Choice(i, _mapNames[i], Category(_maps[i]) + " · " + raw
                    + (duplicate ? " · " + _maps[i].Folder : "")));
            }
            OpenPicker("Choose a scenario — " + FilterNames[_filter], choices, SelectMap);
        }

        void PickDeckForSlot(int aiIndex)
        {
            if (aiIndex < -1 || aiIndex >= _aiSlots.Count) return;
            // Bind the button to this slot object, never to a shared 'currently selected' index.
            var slot = aiIndex >= 0 ? _aiSlots[aiIndex] : null;
            var choices = new List<Choice>();
            if (slot != null) choices.Add(new Choice(-1, "Same as your deck", "Follows your deck selection for this AI slot."));
            for (int i = 0; i < _decks.Count; i++)
                choices.Add(new Choice(i, LobbyText.DeckName(_deckNames[i]), _deckNames[i]));
            OpenPicker(slot != null ? "Deck for " + slot.Name : "Your deck — available for this scenario", choices, value =>
            {
                if (slot != null)
                {
                    if (!_aiSlots.Contains(slot)) return;
                    slot.DeckIndex = value;
                }
                else _deckIdx = value;
                UpdateLabels();
            });
        }

        void PickOption(OptDef option)
        {
            if (option.IsEnum)
            {
                var choices = new List<Choice>();
                for (int i = 0; i < option.EnumVals.Count; i++)
                    choices.Add(new Choice(option.EnumVals[i], option.EnumLabels[i], "Scenario value: " + option.EnumVals[i]));
                OpenPicker(option.Label, choices, value => { _optVals[option.Uid] = value; UpdateLabels(); });
            }
            else OpenNumber(option);
        }

        void ResetDefaults()
        {
            RefreshDecks(); RefreshOptions(); _optionPage = 0; _teamPage = _rosterPage = 0;
            _notice = "Scenario defaults restored, including decks and difficulty.";
            BuildUI();
        }

        void SavePreset()
        {
            if (_mapIdx < 0) return;
            if (!_settingsWritable) { UpdateLabels(); return; }
            string name = (_presetName ?? "").Trim();
            if (name.Length == 0) { _notice = "Enter a preset name first."; UpdateLabels(); return; }
            string scenario = ScenarioKey(_maps[_mapIdx]);
            var existing = _settings.Presets.Find(p => p != null && p.Scenario == scenario && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing == null) _settings.Presets.Add(new SavedPreset { Name = name, Scenario = scenario, Setup = CaptureSetup() });
            else existing.Setup = CaptureSetup();
            _notice = "Preset saved for this scenario: " + name;
            SaveCurrent(); UpdateLabels();
        }

        void LoadPreset()
        {
            if (_mapIdx < 0) return;
            string scenario = ScenarioKey(_maps[_mapIdx]);
            var choices = new List<Choice>();
            for (int i = 0; i < _settings.Presets.Count; i++)
            {
                var preset = _settings.Presets[i];
                if (preset != null && preset.Scenario == scenario && preset.Setup != null)
                    choices.Add(new Choice(i, preset.Name, "Saved for this scenario"));
            }
            OpenPicker("Load a preset for this scenario", choices, value =>
            {
                var preset = _settings.Presets[value];
                RefreshDecks(); RefreshOptions();
                _presetName = preset.Name; _notice = "Loaded preset: " + preset.Name;
                RestoreSetup(preset.Setup); BuildUI();
            });
        }

        // Native Unity UI only: reuse the game's event system and loader dependencies.
        RectTransform RT(GameObject go) => go.GetComponent<RectTransform>();
        void Place(GameObject go, Transform parent, float x, float y, float w, float h)
        {
            go.transform.SetParent(parent, false);
            var rt = RT(go);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h); rt.anchoredPosition = new Vector2(x, y);
        }

        Text MakeLabel(Transform parent, float x, float y, float w, float h, string text, int size, TextAnchor alignment, Color? color = null)
        {
            var go = new GameObject("Label"); var label = go.AddComponent<Text>(); Place(go, parent, x, y, w, h);
            label.font = _font; label.fontSize = size; label.alignment = alignment;
            label.color = color ?? CVal; label.text = text; label.raycastTarget = false;
            label.supportRichText = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        Image MakeImage(Transform parent, float x, float y, float w, float h, Color color)
        {
            var go = new GameObject("Panel"); var image = go.AddComponent<Image>();
            Place(go, parent, x, y, w, h); image.color = color; return image;
        }

        Button MakeButton(Transform parent, float x, float y, float w, float h, string text, Action action, Color? color = null, int size = 15)
        {
            var image = MakeImage(parent, x, y, w, h, color ?? CBtn);
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(action));
            var label = MakeLabel(image.transform, 0, 0, w - 16, h - 2, text, size, TextAnchor.MiddleCenter);
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 11; label.resizeTextMaxSize = size;
            return button;
        }

        InputField MakeInput(Transform parent, float x, float y, float w, string initial, string placeholder, Action<string> changed)
        {
            var image = MakeImage(parent, x, y, w, 32, new Color(0.05f, 0.07f, 0.10f));
            var input = image.gameObject.AddComponent<InputField>();
            input.targetGraphic = image; input.characterLimit = 100;
            input.textComponent = MakeLabel(image.transform, 0, 0, w - 16, 30, "", 15, TextAnchor.MiddleLeft);
            input.placeholder = MakeLabel(image.transform, 0, 0, w - 16, 30, placeholder, 14, TextAnchor.MiddleLeft, CLabel);
            input.text = initial ?? "";
            input.onValueChanged.AddListener(DelegateSupport.ConvertDelegate<UnityAction<string>>(changed));
            return input;
        }

        void BuildUI()
        {
            DestroyUI();
            if (_font == null)
            {
                try { _font = Font.CreateDynamicFontFromOSFont("Arial", 16); } catch { }
                if (_font == null) { try { _font = Font.CreateDynamicFontFromOSFont("Segoe UI", 16); } catch { } }
            }
            _root = new GameObject("BASkirmishSetup"); UnityEngine.Object.DontDestroyOnLoad(_root);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30000;
            var scaler = _root.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1120, 800); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            _root.AddComponent<GraphicRaycaster>();
            MakeImage(_root.transform, 0, 0, 10000, 10000, CDim);
            MakeImage(_root.transform, 0, 0, 1044, 752, CFrame);
            var panel = MakeImage(_root.transform, 0, 0, 1040, 748, CPanel).transform;
            MakeImage(panel, 0, 344, 1040, 60, CHeader);
            MakeLabel(panel, 0, 344, 980, 40, "LOCAL AI SKIRMISH", 25, TextAnchor.MiddleCenter);

            BuildLobbyBody(panel);
            _previewRetryFrames = 300;
            SetPreview(); UpdateLabels();
        }

        void SetPreview()
        {
            _previewResult?.Release(); _previewResult = null;
            if (_mapIdx < 0 || _mapIdx >= _maps.Count) return;
            _previewResult = MapPreviewResolver.Resolve(_maps[_mapIdx]);
            if (_previewResult == null)
            {
                _previewCaption.text = "No map preview available";
                return;
            }
            _previewImg.sprite = _previewResult.Sprite;
            _previewImg.color = Color.white; _previewImg.preserveAspect = true;
            _previewCaption.text = _previewResult.Caption;
        }

        void RetryPreviewWhenReady()
        {
            // Native map-card assets may become available shortly after opening the menu.
            if (_root == null || _previewImg == null || _previewResult != null || _previewRetryFrames <= 0) return;
            _previewRetryFrames--;
            if (_previewRetryFrames % 30 == 0) SetPreview();
        }
        void DestroyUI()
        {
            ClosePicker();
            if (_root != null) { _root.SetActive(false); UnityEngine.Object.Destroy(_root); _root = null; }
            _previewResult?.Release(); _previewResult = null;
            _previewRetryFrames = 0;
            _mapLabel = _deckLabel = _aiPlayerLabel = _aiDeckLabel = _statusLabel = _diffLabel = _previewCaption = null;
            _teamSummary = null; _startButton = null; _previewImg = null; _optLabels.Clear(); _rosterDeckLabels.Clear();
        }

        void UpdateLabels()
        {
            if (_mapLabel != null) _mapLabel.text = _mapIdx >= 0 ? _mapNames[_mapIdx] : "No scenarios available";
            if (_deckLabel != null) _deckLabel.text = _decks.Count > 0 ? LobbyText.DeckName(_deckNames[_deckIdx]) : "No valid decks for this scenario";
            foreach (var view in _rosterDeckLabels)
            {
                int index = _aiSlots[view.Slot.AiIndex].DeckIndex;
                view.Label.text = index >= 0 && index < _deckNames.Count ? LobbyText.DeckName(_deckNames[index]) : "Same as your deck";
            }
            if (_diffLabel != null) _diffLabel.text = _diffNames[Math.Max(1, Math.Min(3, _diff))];
            for (int i = 0; i < _optLabels.Count; i++) _optLabels[i].text = OptValueText(_opts[_optionPage * 4 + i]);
            if (_teamSummary != null)
            {
                int allies = _aiSlots.FindAll(s => s.TeamSide == 0).Count;
                int opponents = _aiSlots.FindAll(s => s.TeamSide == 1).Count;
                int custom = _aiSlots.FindAll(s => s.DeckIndex >= 0).Count;
                int others = _aiSlots.Count - allies - opponents;
                _teamSummary.text = $"{allies} allied AI · {opponents} opposing AI"
                    + (others > 0 ? $" · {others} other AI" : "") + $" · {custom} custom decks";
            }
            if (_startButton != null) _startButton.interactable = _mapIdx >= 0 && _decks.Count > 0 && (!IsSkirmishMap() || _aiDeckPatchReady);
            if (_statusLabel != null)
            {
                string blocked = _mapIdx < 0 ? "No local scenarios found." : _decks.Count == 0 ? "No valid decks. Create a compatible deck in the game's deck builder."
                    : IsSkirmishMap() && !_aiDeckPatchReady ? "AI deck hooks could not load. See MelonLoader/Latest.log." : "";
                _statusLabel.text = blocked.Length > 0 ? blocked : !_settingsWritable
                    ? "Settings preserved but unreadable. Saving is disabled; see the MelonLoader log."
                    : _saveFailed ? "Settings could not be saved. Current selections still work; see the MelonLoader log."
                    : _notice.Length > 0 ? _notice : $"{FilteredMaps().Count} scenarios in category · Settings remembered on map change, close and launch.";
            }
        }

        void OpenPicker(string title, List<Choice> choices, Action<int> picked)
        {
            ClosePicker(); _pickerChoices = choices; _pick = picked; _pickerPage = 0; _pickerSearch = "";
            _picker = MakeImage(_root.transform, 0, 0, 10000, 10000, new Color(0, 0, 0, 0.8f)).gameObject;
            var panel = MakeImage(_picker.transform, 0, 0, 900, 620, CPanel).transform;
            MakeLabel(panel, 0, 278, 850, 38, title, 22, TextAnchor.MiddleCenter, CLabel);
            MakeInput(panel, 0, 230, 840, "", "Search by name or identifier…", value => { _pickerSearch = value; _pickerPage = 0; DrawPickerRows(panel); });
            _pickerCount = MakeLabel(panel, 0, -275, 370, 30, "", 13, TextAnchor.MiddleCenter);
            _pickerPrev = MakeButton(panel, -376, -275, 94, 32, "Previous", () => { _pickerPage--; DrawPickerRows(panel); });
            _pickerNext = MakeButton(panel, -266, -275, 94, 32, "Next", () => { _pickerPage++; DrawPickerRows(panel); });
            MakeButton(panel, 345, -275, 150, 32, "Cancel", ClosePicker, CCancel);
            DrawPickerRows(panel);
        }

        void DrawPickerRows(Transform panel)
        {
            if (_pickerRows != null) { _pickerRows.SetActive(false); UnityEngine.Object.Destroy(_pickerRows); }
            _pickerRows = new GameObject("Choices"); _pickerRows.AddComponent<RectTransform>(); Place(_pickerRows, panel, 0, 0, 840, 440);
            var matches = _pickerChoices.FindAll(c => (c.Label ?? "").IndexOf(_pickerSearch, StringComparison.OrdinalIgnoreCase) >= 0
                || (c.Detail ?? "").IndexOf(_pickerSearch, StringComparison.OrdinalIgnoreCase) >= 0);
            int pages = Math.Max(1, (matches.Count + 7) / 8);
            _pickerPage = Math.Max(0, Math.Min(pages - 1, _pickerPage));
            for (int i = _pickerPage * 8; i < Math.Min(matches.Count, (_pickerPage + 1) * 8); i++)
            {
                var choice = matches[i];
                var button = MakeButton(_pickerRows.transform, 0, 175 - (i % 8) * 54, 840, 50, "", () =>
                {
                    var action = _pick; ClosePicker(); action(choice.Value);
                });
                var label = MakeLabel(button.transform, 0, string.IsNullOrEmpty(choice.Detail) ? 0 : 10, 810, 26, choice.Label, 17, TextAnchor.MiddleLeft);
                label.resizeTextForBestFit = true; label.resizeTextMinSize = 11; label.resizeTextMaxSize = 17;
                if (!string.IsNullOrEmpty(choice.Detail)) MakeLabel(button.transform, 0, -13, 810, 21, choice.Detail, 12, TextAnchor.MiddleLeft, CLabel);
            }
            if (matches.Count == 0) MakeLabel(_pickerRows.transform, 0, 80, 800, 60, "No matching choices. Try another search or category.", 17, TextAnchor.MiddleCenter);
            _pickerCount.text = $"{matches.Count} choices · Page {_pickerPage + 1} / {pages}";
            _pickerPrev.interactable = _pickerPage > 0; _pickerNext.interactable = _pickerPage + 1 < pages;
        }

        void OpenNumber(OptDef option)
        {
            ClosePicker();
            _picker = MakeImage(_root.transform, 0, 0, 10000, 10000, new Color(0, 0, 0, 0.8f)).gameObject;
            var panel = MakeImage(_picker.transform, 0, 0, 700, 350, CPanel).transform;
            MakeLabel(panel, 0, 135, 640, 36, option.Label, 23, TextAnchor.MiddleCenter, CLabel);
            bool fixedValue = option.Max <= option.Min;
            MakeLabel(panel, 0, 70, 640, 80, option.Help + (fixedValue
                ? "\nNo editable range declared. Fixed scenario default: " + option.Default
                : $"\nRange: {option.Min}–{option.Max} · Step: {Math.Max(1, option.Step)} · Default: {option.Default}"), 15, TextAnchor.MiddleCenter);
            var value = MakeInput(panel, 0, -5, 240, OptValueText(option), "Enter a value", _ => { });
            value.characterLimit = 12;
            value.readOnly = fixedValue;
            var error = MakeLabel(panel, 0, -60, 640, 50, "", 14, TextAnchor.MiddleCenter, CLabel);
            MakeButton(panel, -135, -125, 230, 38, "Apply", () =>
            {
                if (!int.TryParse(value.text, out int parsed) || !option.Accepts(parsed))
                { error.text = "Enter a whole number in the displayed range and step."; return; }
                _optVals[option.Uid] = parsed; ClosePicker(); UpdateLabels();
            }, CStart);
            MakeButton(panel, 135, -125, 230, 38, "Cancel", ClosePicker, CCancel);
        }

        void ClosePicker()
        {
            if (_picker != null) { _picker.SetActive(false); UnityEngine.Object.Destroy(_picker); }
            _picker = _pickerRows = null; _pickerChoices = null; _pick = null;
            _pickerCount = null; _pickerPrev = _pickerNext = null;
        }
    }
}

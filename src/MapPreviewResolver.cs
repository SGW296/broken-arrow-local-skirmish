using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppBrokenArrow.Client.Ecs.Configs;
using Il2CppBrokenArrow.Client.Ecs.UI.Menu;
using Il2CppBrokenArrow.MissionEditor.MissionResolver;
using Il2CppBrokenArrow.MissionEditor.Storage;

namespace BALocalSkirmish
{
    public sealed class MapPreviewResult
    {
        public Sprite Sprite { get; internal set; }
        public Texture2D OwnedTexture { get; internal set; }
        public bool OwnsSprite { get; internal set; }
        public string Caption { get; internal set; }
        public string Source { get; internal set; }

        public void Release()
        {
            if (OwnsSprite && Sprite != null) UnityEngine.Object.Destroy(Sprite);
            if (OwnedTexture != null) UnityEngine.Object.Destroy(OwnedTexture);
            Sprite = null; OwnedTexture = null; OwnsSprite = false;
        }
    }

    // Built-in skirmish folders usually contain no preview file. Their images are already
    // loaded by the game's MapCardsConfig as shared Unity sprites; never destroy those.
    public static class MapPreviewResolver
    {
        public static MapPreviewResult Resolve(ScenarioSource scenario)
        {
            if (scenario == null) return null;
            string preview = null, folder = null, scene = null;
            try { preview = scenario.PreviewPath; } catch { }
            try { folder = scenario.Folder; } catch { }
            try { scene = scenario.MetaFile?.SceneName; } catch { }

            foreach (string path in FileCandidates(preview, folder))
            {
                var loaded = LoadFile(path);
                if (loaded != null) return loaded;
            }

            // Exact scene identity matters. Fuzzy title matching can silently show the
            // wrong battlefield for similarly named Workshop or campaign scenarios.
            if (string.IsNullOrWhiteSpace(scene)) return null;
            try
            {
                var cards = GameConfig.Instance?.MapCardsConfig;
                var result = ResolveNative(cards, scene);
                if (result != null) return result;
            }
            catch { }
            try { return ResolveNative(ScenarioDataManager._mapCardsConfig, scene); }
            catch { return null; }
        }

        public static IEnumerable<string> FileCandidates(string previewPath, string scenarioFolder)
        {
            var paths = new List<string>();
            void Add(string path)
            {
                if (!string.IsNullOrWhiteSpace(path) && !paths.Exists(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)))
                    paths.Add(path);
            }
            Add(previewPath);
            if (!string.IsNullOrWhiteSpace(scenarioFolder))
            {
                if (!string.IsNullOrWhiteSpace(previewPath) && !Path.IsPathRooted(previewPath))
                {
                    try { Add(Path.Combine(scenarioFolder, previewPath)); } catch { }
                }
                foreach (string extension in new[] { ".png", ".jpg", ".jpeg" })
                {
                    try { Add(Path.Combine(scenarioFolder, "preview" + extension)); } catch { }
                }
            }
            return paths;
        }

        static MapPreviewResult LoadFile(string path)
        {
            Texture2D texture = null;
            try
            {
                if (!File.Exists(path)) return null;
                texture = new Texture2D(2, 2);
                if (!ImageConversion.LoadImage(texture, (Il2CppStructArray<byte>)File.ReadAllBytes(path)))
                { UnityEngine.Object.Destroy(texture); return null; }
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                if (sprite == null) { UnityEngine.Object.Destroy(texture); return null; }
                return new MapPreviewResult { Sprite = sprite, OwnedTexture = texture, OwnsSprite = true,
                    Caption = "Scenario preview", Source = path };
            }
            catch
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
                return null;
            }
        }

        static MapPreviewResult ResolveNative(MapCardsConfig config, string scene)
        {
            var cards = config?.Data;
            if (cards == null) return null;
            // Prefer a tactical MapPreview over a scenic Image even when a duplicate
            // native card for the same scene appears earlier in the collection.
            Sprite scenic = null;
            for (int i = 0; i < cards.Count; i++)
            {
                try
                {
                    var card = cards[i];
                    if (card == null || !string.Equals(card.SceneName, scene, StringComparison.OrdinalIgnoreCase)) continue;
                    if (card.MapPreview != null)
                        return new MapPreviewResult { Sprite = card.MapPreview, Caption = "Base map preview", Source = "Native map card: " + scene };
                    if (scenic == null) scenic = card.Image;
                }
                catch { } // One unreadable card should not hide previews for every other map.
            }
            return scenic == null ? null : new MapPreviewResult { Sprite = scenic, Caption = "Map image", Source = "Native map card: " + scene };
        }
    }
}

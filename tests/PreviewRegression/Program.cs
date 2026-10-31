using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BALocalSkirmish;
using UnityEngine;
using Il2CppBrokenArrow.Client.Ecs.Configs;
using Il2CppBrokenArrow.Client.Ecs.UI.Menu;
using Il2CppBrokenArrow.MissionEditor.MissionResolver;
using Il2CppBrokenArrow.MissionEditor.Storage;

// Test stand-ins model ownership and resolver control flow. They do not prove Unity's
// PNG/JPEG decoding or availability of the game's loaded config in a live session.
namespace UnityEngine
{
    public class Object { public static readonly List<Object> Destroyed = new List<Object>(); public static void Destroy(Object value) { Destroyed.Add(value); } }
    public class Texture2D : Object { public int width = 100, height = 80; public Texture2D(int width, int height) { } }
    public class Sprite : Object
    {
        public static bool FailCreation;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot)
        { if (FailCreation) throw new InvalidOperationException("Fixture create failure"); return new Sprite(); }
    }
    public struct Rect { public Rect(float x, float y, float width, float height) { } }
    public struct Vector2 { public Vector2(float x, float y) { } }
    public static class ImageConversion
    {
        public static bool LoadImage(Texture2D texture, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> data)
        { return data.Data.Length > 0 && data.Data[0] == 0x89; }
    }
}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays
{
    public class Il2CppStructArray<T> { public T[] Data; public static implicit operator Il2CppStructArray<T>(T[] data) => new Il2CppStructArray<T> { Data = data }; }
}
namespace Il2CppBrokenArrow.MissionEditor.Storage
{
    public class ScenarioMetaFile { public string SceneName; }
    public static class ScenarioDataManager { public static MapCardsConfig _mapCardsConfig; }
}
namespace Il2CppBrokenArrow.MissionEditor.MissionResolver
{
    public class ScenarioSource { public string PreviewPath, Folder; public ScenarioMetaFile MetaFile; }
}
namespace Il2CppBrokenArrow.Client.Ecs.UI.Menu
{
    public class MapCardData
    {
        string _sceneName;
        public bool Unreadable;
        public string SceneName { get { if (Unreadable) throw new Exception("Unreadable fixture card"); return _sceneName; } set { _sceneName = value; } }
        public Sprite MapPreview, Image;
    }
    public class MapCardsConfig { public List<MapCardData> Data = new List<MapCardData>(); }
}
namespace Il2CppBrokenArrow.Client.Ecs.Configs
{
    public class GameConfig { public static GameConfig Instance; public MapCardsConfig MapCardsConfig; }
}

static class Program
{
    static int passed;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); passed++; }
    static void Main()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "fixtures", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var scene = new ScenarioSource { Folder = root, MetaFile = new ScenarioMetaFile { SceneName = "Airbase" } };
        var scenic = new Sprite(); var tactical = new Sprite();
        var config = new MapCardsConfig();
        config.Data.Add(new MapCardData { SceneName = "Airbase", Image = scenic });
        config.Data.Add(new MapCardData { SceneName = "Airbase", MapPreview = tactical });
        GameConfig.Instance = new GameConfig { MapCardsConfig = config };

        Check(MapPreviewResolver.Resolve(null) == null, "Null scenario should have no preview");
        var native = MapPreviewResolver.Resolve(scene);
        Check(ReferenceEquals(native.Sprite, tactical), "Native tactical preview must beat earlier scenic image");
        Check(!native.OwnsSprite && native.OwnedTexture == null && native.Caption == "Base map preview", "Native sprite ownership/caption wrong");
        native.Release(); native.Release();
        Check(UnityEngine.Object.Destroyed.Count == 0, "Shared native sprite destroyed");
        scene.MetaFile.SceneName = "airBASE";
        Check(ReferenceEquals(MapPreviewResolver.Resolve(scene).Sprite, tactical), "Scene matching must ignore case");
        scene.MetaFile.SceneName = "Airbase expansion";
        Check(MapPreviewResolver.Resolve(scene) == null, "Fuzzy scene match showed another map");
        scene.MetaFile.SceneName = null;
        Check(MapPreviewResolver.Resolve(scene) == null, "Missing scene identity should not select arbitrary card");
        scene.MetaFile.SceneName = "Airbase";
        config.Data.RemoveAt(1);
        Check(ReferenceEquals(MapPreviewResolver.Resolve(scene).Sprite, scenic), "Scenic Image fallback missing");
        config.Data.Insert(0, new MapCardData { Unreadable = true });
        Check(ReferenceEquals(MapPreviewResolver.Resolve(scene).Sprite, scenic), "Unreadable card prevented valid match");

        GameConfig.Instance = null; ScenarioDataManager._mapCardsConfig = config;
        Check(ReferenceEquals(MapPreviewResolver.Resolve(scene).Sprite, scenic), "ScenarioDataManager config fallback missing");
        ScenarioDataManager._mapCardsConfig = null;
        Check(MapPreviewResolver.Resolve(scene) == null, "Missing native config should fail safely");
        GameConfig.Instance = new GameConfig { MapCardsConfig = config };

        string png = Path.Combine(root, "preview.png");
        File.WriteAllBytes(png, new byte[] { 0x89 });
        var local = MapPreviewResolver.Resolve(scene);
        Check(local.OwnsSprite && local.OwnedTexture != null && local.Caption == "Scenario preview", "Scenario loose PNG must take precedence");
        Check(local.Source == png && !ReferenceEquals(local.Sprite, scenic), "Loose file source incorrect");
        int before = UnityEngine.Object.Destroyed.Count;
        local.Release(); local.Release();
        Check(UnityEngine.Object.Destroyed.Count == before + 2, "Owned assets must be released exactly once");

        File.WriteAllBytes(png, new byte[] { 0 });
        before = UnityEngine.Object.Destroyed.Count;
        Check(ReferenceEquals(MapPreviewResolver.Resolve(scene).Sprite, scenic), "Invalid PNG did not fall back to native sprite");
        Check(UnityEngine.Object.Destroyed.Count == before + 1, "Failed decode leaked texture");
        File.WriteAllBytes(png, new byte[] { 0x89 }); Sprite.FailCreation = true;
        before = UnityEngine.Object.Destroyed.Count;
        Check(ReferenceEquals(MapPreviewResolver.Resolve(scene).Sprite, scenic), "Sprite-creation exception did not fall back safely");
        Check(UnityEngine.Object.Destroyed.Count == before + 1, "Sprite-creation exception leaked texture"); Sprite.FailCreation = false;
        File.Delete(png);
        string jpg = Path.Combine(root, "preview.jpg"); File.WriteAllBytes(jpg, new byte[] { 0x89 });
        Check(MapPreviewResolver.Resolve(scene).Source == jpg, "JPEG-named scenario fallback absent");
        string explicitFile = Path.Combine(root, "custom.png"); File.WriteAllBytes(explicitFile, new byte[] { 0x89 });
        scene.PreviewPath = explicitFile;
        Check(MapPreviewResolver.Resolve(scene).Source == explicitFile, "Explicit preview path must win");
        scene.PreviewPath = "custom.png";
        Check(MapPreviewResolver.Resolve(scene).Source == explicitFile, "Relative explicit path not resolved under scenario folder");
        var candidates = MapPreviewResolver.FileCandidates(Path.Combine(root, "PREVIEW.PNG"), root).ToList();
        Check(candidates.Count == 3, "Case-insensitive duplicate candidates not removed");
        Check(!MapPreviewResolver.FileCandidates(null, null).Any(), "Empty input should not invent paths");
        Console.WriteLine($"PASS: {passed} preview checks: native scene matching, loose image precedence, config fallback, corrupt images and shared/owned resource lifecycle.");
    }
}

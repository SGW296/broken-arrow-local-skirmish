using System;
using System.Collections.Generic;
using BALocalSkirmish;
using UnityEngine;
using Il2CppBrokenArrow.Client.Ecs.Configs;
using Il2CppBrokenArrow.Client.Ecs.UI.Menu.Network.TeamPanel;

namespace UnityEngine
{
    public class Object { public string name; }
    public class Font : Object { }
    public class Sprite : Object { }
    public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; } }
    public static class Resources
    {
        public static readonly List<Object> Assets = new List<Object>();
        public static bool Throw;
        public static T[] FindObjectsOfTypeAll<T>() where T : Object
        {
            if (Throw) throw new InvalidOperationException("Assets unavailable");
            return Assets.FindAll(a => a is T).ConvertAll(a => (T)a).ToArray();
        }
    }
}
namespace Il2CppBrokenArrow.Client.Ecs.Configs
{
    public class UiConfig { public Color PopupBackgroundColor; public Sprite NeutralFlagIcon; }
    public class AiGeneric { public Sprite Avatar; }
    public class AiConfig { public AiGeneric AiProfilePresetGeneric; }
    public class GameConfig { public static GameConfig Instance; public UiConfig UiConfig; public AiConfig AiConfig; }
}
namespace Il2CppBrokenArrow.Client.Ecs.UI.Menu.Network.TeamPanel
{
    public class TeamIdentifier : UnityEngine.Object
    {
        public Color _friendlyColor, _enemyColor;
        public Sprite _friendlyBackground, _enemyBackground;
    }
}
static class Program
{
    static int passed;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); passed++; }
    static void Main()
    {
        var fallback = NativeLobbyStyle.Capture();
        Check(!fallback.HasNativeAssets && fallback.BodyFont == null, "Missing assets must produce safe fallback");
        Check(fallback.Background.a > 0.9f && fallback.Text.r > fallback.Panel.r, "Fallback background/text contrast");
        Resources.Throw = true;
        Check(NativeLobbyStyle.Capture().EnemyBanner == null, "Unavailable Resources must not throw");
        Resources.Throw = false;

        var legacy = new Font { name = "Inter-Regular-Legacy Dynamic" };
        var regular = new Font { name = "Inter-Regular" };
        var bold = new Font { name = "Inter-Bold" };
        var wrong = new Sprite { name = "Not Lobby Forces Blue Background" };
        var blue = new Sprite { name = "Lobby Forces Blue Background" };
        var red = new Sprite { name = "Lobby Forces Red Background.png" };
        var slot = new Sprite { name = "Empty Slot Background" };
        var host = new Sprite { name = "Host Indicator Icon" };
        var cta = new Sprite { name = "Button CTA Background" };
        Resources.Assets.AddRange(new UnityEngine.Object[] { regular, legacy, bold, wrong, blue, red, slot, host, cta });
        var native = NativeLobbyStyle.Capture();
        Check(ReferenceEquals(native.BodyFont, legacy) && ReferenceEquals(native.HeadingFont, bold), "Native Inter font priority");
        Check(ReferenceEquals(native.FriendlyBanner, blue) && ReferenceEquals(native.EnemyBanner, red), "Exact sprite names/extensions");
        Check(ReferenceEquals(native.SlotBackground, slot) && ReferenceEquals(native.HostIndicator, host) && ReferenceEquals(native.PrimaryButton, cta), "Native role assignment");
        Check(native.HasNativeAssets && Resources.Assets.Count == 9, "Asset capture must borrow without changing loaded assets");

        Resources.Assets.Clear(); Resources.Assets.Add(wrong);
        Check(NativeLobbyStyle.Capture().FriendlyBanner == null, "Fuzzy matching must not borrow another sprite");
        var team = new TeamIdentifier { _friendlyColor = new Color(0.1f, 0.2f, 0.3f, 1), _enemyColor = new Color(0.6f, 0.1f, 0.2f, 1), _friendlyBackground = blue, _enemyBackground = red };
        Resources.Assets.Add(team);
        GameConfig.Instance = new GameConfig { UiConfig = new UiConfig { PopupBackgroundColor = new Color(0, 0, 0, 0.7f), NeutralFlagIcon = host }, AiConfig = new AiConfig { AiProfilePresetGeneric = new AiGeneric { Avatar = slot } } };
        var component = NativeLobbyStyle.Capture();
        Check(ReferenceEquals(component.FriendlyBanner, blue) && ReferenceEquals(component.EnemyBanner, red), "Native TeamIdentifier sprite fallback");
        Check(component.FriendlyAccent.b == 0.3f && component.EnemyAccent.r == 0.6f, "Native header colors");
        Check(component.PopupBackground.a == 0.7f, "Native popup dim color");
        Check(ReferenceEquals(component.BotAvatar, slot) && ReferenceEquals(component.NeutralFlag, host), "Native generic AI avatar/neutral flag");
        Resources.Assets.Clear(); Resources.Assets.Add(regular);
        Check(ReferenceEquals(NativeLobbyStyle.Capture().HeadingFont, regular), "Regular native font must backfill absent heading font");
        Console.WriteLine($"PASS: {passed} native style checks: exact borrowed assets, Inter selection, metadata colors and safe unloaded fallbacks.");
    }
}

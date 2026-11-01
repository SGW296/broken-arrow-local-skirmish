using System;
using System.Collections.Generic;
using UnityEngine;
using Il2CppBrokenArrow.Client.Ecs.Configs;
using Il2CppBrokenArrow.Client.Ecs.UI.Menu.Network.TeamPanel;

namespace BALocalSkirmish
{
    // Borrow already-loaded presentation assets only. No native UI prefab is instantiated,
    // no lobby events are attached, and ownership stays with the game.
    public sealed class NativeLobbyStyle
    {
        public Font BodyFont { get; private set; }
        public Font HeadingFont { get; private set; }
        public Sprite FriendlyBanner { get; private set; }
        public Sprite EnemyBanner { get; private set; }
        public Sprite SlotBackground { get; private set; }
        public Sprite HostIndicator { get; private set; }
        public Sprite PrimaryButton { get; private set; }
        public Sprite BotAvatar { get; private set; }
        public Sprite NeutralFlag { get; private set; }

        public Color Background { get; private set; } = new Color(0.025f, 0.033f, 0.044f, 0.97f);
        public Color Panel { get; private set; } = new Color(0.055f, 0.068f, 0.084f, 0.97f);
        public Color Control { get; private set; } = new Color(0.10f, 0.12f, 0.145f, 1f);
        public Color Text { get; private set; } = new Color(0.93f, 0.94f, 0.94f, 1f);
        public Color MutedText { get; private set; } = new Color(0.62f, 0.67f, 0.71f, 1f);
        public Color Divider { get; private set; } = new Color(0.28f, 0.33f, 0.37f, 0.65f);
        public Color FriendlyAccent { get; private set; } = new Color(0.19f, 0.43f, 0.61f, 1f);
        public Color EnemyAccent { get; private set; } = new Color(0.66f, 0.26f, 0.21f, 1f);
        public Color Action { get; private set; } = new Color(0.29f, 0.40f, 0.29f, 1f);
        public Color PopupBackground { get; private set; } = new Color(0f, 0f, 0f, 0.85f);

        public bool HasNativeAssets => BodyFont != null || HeadingFont != null || FriendlyBanner != null
            || EnemyBanner != null || SlotBackground != null || HostIndicator != null || PrimaryButton != null
            || BotAvatar != null || NeutralFlag != null;

        public static NativeLobbyStyle Capture()
        {
            var style = new NativeLobbyStyle();
            try
            {
                var fonts = new Dictionary<string, Font>(StringComparer.OrdinalIgnoreCase);
                foreach (var font in Resources.FindObjectsOfTypeAll<Font>())
                {
                    try { if (font != null && !string.IsNullOrEmpty(font.name) && !fonts.ContainsKey(font.name)) fonts.Add(font.name, font); }
                    catch { }
                }
                style.BodyFont = FindFont(fonts, "Inter-Regular-Legacy Dynamic", "Inter-Regular-Legacy",
                    "Inter-Regular", "Inter Regular", "Inter-Medium", "Arial", "Arial Unicode MS");
                style.HeadingFont = FindFont(fonts, "Inter-Bold-Legacy Dynamic", "Inter-Bold", "Inter Bold",
                    "Inter-SemiBold", "Inter SemiBold") ?? style.BodyFont;
            }
            catch { }

            try
            {
                var sprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
                foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>())
                {
                    try { if (sprite != null && !string.IsNullOrEmpty(sprite.name) && !sprites.ContainsKey(sprite.name)) sprites.Add(sprite.name, sprite); }
                    catch { }
                }
                style.FriendlyBanner = FindSprite(sprites, "Lobby Forces Blue Background");
                style.EnemyBanner = FindSprite(sprites, "Lobby Forces Red Background");
                style.SlotBackground = FindSprite(sprites, "Empty Slot Background");
                style.HostIndicator = FindSprite(sprites, "Host Indicator Icon");
                style.PrimaryButton = FindSprite(sprites, "Button CTA Background");
            }
            catch { }

            try
            {
                foreach (var identifier in Resources.FindObjectsOfTypeAll<TeamIdentifier>())
                {
                    if (identifier == null) continue;
                    try
                    {
                        if (identifier._friendlyColor.a > 0.1f) style.FriendlyAccent = identifier._friendlyColor;
                        if (identifier._enemyColor.a > 0.1f) style.EnemyAccent = identifier._enemyColor;
                        if (style.FriendlyBanner == null) style.FriendlyBanner = identifier._friendlyBackground;
                        if (style.EnemyBanner == null) style.EnemyBanner = identifier._enemyBackground;
                        break;
                    }
                    catch { }
                }
            }
            catch { }

            try
            {
                var config = GameConfig.Instance?.UiConfig;
                if (config != null)
                {
                    if (config.PopupBackgroundColor.a > 0.1f) style.PopupBackground = config.PopupBackgroundColor;
                    style.NeutralFlag = config.NeutralFlagIcon;
                }
            }
            catch { }
            try { style.BotAvatar = GameConfig.Instance?.AiConfig?.AiProfilePresetGeneric?.Avatar; }
            catch { }
            return style;
        }

        static Font FindFont(Dictionary<string, Font> fonts, params string[] names)
        {
            foreach (string name in names) if (fonts.TryGetValue(name, out var font)) return font;
            return null;
        }

        static Sprite FindSprite(Dictionary<string, Sprite> sprites, string name)
        {
            if (sprites.TryGetValue(name, out var sprite)) return sprite;
            return sprites.TryGetValue(name + ".png", out sprite) ? sprite : null;
        }
    }
}

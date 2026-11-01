using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using MelonLoader.Utils;
using Il2CppBrokenArrow.Client.Ecs.Configs;
using Il2CppBrokenArrow.Shared.Ecs.Services;
using Il2CppBrokenArrow.Shared.Ecs.Localization;
using Il2CppBrokenArrow.MissionEditor.MissionResolver;

namespace BALocalSkirmish
{
    public sealed class NativeAiProfileChoice
    {
        public int Id, CountryId, Spec1Id, Spec2Id;
        public string Name;
        // Borrowed sprites. The game retains ownership.
        public Sprite Avatar, Spec1Preview, Spec2Preview;
    }

    public static class NativeAiProfiles
    {
        static readonly HashSet<string> VerifiedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "Airbase_Skirmish", "Airport_Skirmish", "Baltisk_Skirmish", "Central_Village_Skirmish",
            "Chernyakhovsk_Skirmish", "Coal Mountain_Skirmish", "Coast_Skirmish", "Dam_Skirmish",
            "Daugavpils_Skirmish", "Frontiers_Skirmish", "Ignalina Powerplant_Skirmish", "Jelgava_Skirmish",
            "Kadaga_Skirmish", "Kaliningrad_Skirmish", "Klaipeda_Skirmish", "Narva_Skirmish",
            "Oil refinery_Skirmish", "Parnu_Skirmish", "River_Skirmish", "Ruda_Skirmish",
            "Suwalki_Skirmish", "Tallinn Harbour_Skirmish"
        };

        public static bool IsSupportedScenario(ScenarioSource scenario)
        {
            try
            {
                if (scenario == null || scenario.IsWorkshop || scenario.MetaFile == null
                    || scenario.MetaFile.Type != ScenarioType.Skirmish || scenario.MetaFile.GameVersion != "1.2.0"
                    || scenario.MetaFile.Author != "Steel Balalaika" || string.IsNullOrWhiteSpace(scenario.Folder)) return false;
                string gameRoot = Path.GetFullPath(MelonEnvironment.GameRootDirectory);
                string shared = Path.GetFullPath(Path.Combine(gameRoot, "BrokenArrow_Data", "StreamingAssets", "SharedMissions"));
                string folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(scenario.Folder, gameRoot));
                return string.Equals(Path.GetDirectoryName(folder), shared, StringComparison.OrdinalIgnoreCase)
                    && VerifiedFolders.Contains(Path.GetFileName(folder));
            }
            catch { return false; }
        }

        public static List<NativeAiProfileChoice> GetAvailable(ScenarioSource scenario)
        {
            var result = new List<NativeAiProfileChoice>();
            if (!IsSupportedScenario(scenario)) return result;
            try
            {
                var profiles = GameConfig.Instance?.AiConfig?.AiProfilePresets;
                if (profiles == null) return result;
                LocalizationService localization = null;
                try { Session.TryGetService<LocalizationService>(out localization, false); } catch { }
                var seen = new HashSet<int>();
                for (int i = 0; i < profiles.Count; i++)
                {
                    try
                    {
                        var profile = profiles[i];
                        if (profile == null || profile.Id < 0 || profile.Id > 11 || !seen.Add(profile.Id)) continue;
                        string name = null;
                        if (localization != null && !string.IsNullOrEmpty(profile.DisplayNameLoc))
                            localization.TryResolveSimpleKey(profile.DisplayNameLoc, out name, false);
                        if (string.IsNullOrWhiteSpace(name) || name == profile.DisplayNameLoc)
                            name = string.IsNullOrWhiteSpace(profile.TechName) ? "AI profile " + profile.Id : profile.TechName.Replace('_', ' ');
                        result.Add(new NativeAiProfileChoice {
                            Id = profile.Id, Name = name, CountryId = (int)profile.Country,
                            Spec1Id = profile.Spec1Id, Spec2Id = profile.Spec2Id,
                            Avatar = profile.AvatarPreview, Spec1Preview = profile.Spec1Preview, Spec2Preview = profile.Spec2Preview
                        });
                    }
                    catch { }
                }
                result.Sort((a, b) => a.Id.CompareTo(b.Id));
            }
            catch { }
            return result;
        }
    }
}

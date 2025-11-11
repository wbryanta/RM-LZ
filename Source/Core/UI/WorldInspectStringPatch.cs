using System.Collections.Generic;
using System.Linq;
using System.Text;
using LandingZone.Data;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LandingZone.Core.UI
{
    [HarmonyPatch(typeof(WorldInspectPane), "get_TileInspectString")]
    internal static class WorldInspectStringPatch
    {
        public static void Postfix(WorldInspectPane __instance, ref string __result)
        {
            if (__result.Contains("LandingZone Match:"))
                return;

            if (LandingZoneContext.HighlightState == null || !LandingZoneContext.HighlightState.ShowBestSites)
                return;

            int tileId = Find.WorldInterface?.SelectedTile ?? -1;
            if (tileId < 0)
                return;

            if (!LandingZoneContext.TryGetBreakdown(tileId, out var breakdown))
                return;

            var builder = new StringBuilder();
            builder.AppendLine();
            builder.AppendLine(BuildHeadline(tileId, breakdown));
            builder.AppendLine(BuildScoreLine(breakdown));
            AppendContextLines(tileId, breakdown, builder);

            __result = string.Concat(__result, "\n", builder.ToString().TrimEnd());
        }

        private static string BuildHeadline(int tileId, MatchBreakdown breakdown)
        {
            var percent = breakdown.FinalScore * 100f;
            if (LandingZoneContext.TryGetMatchRank(tileId, out var rank))
            {
                var total = Mathf.Max(LandingZoneContext.HighlightedMatchCount, rank + 1);
                return $"LandingZone Match #{rank + 1}/{total}: {percent:F0}%";
            }

            return $"LandingZone Match: {percent:F0}%";
        }

        private static string BuildScoreLine(MatchBreakdown breakdown)
        {
            var parts = new[]
            {
                ScoreLabel("Temp", breakdown.TemperatureEnabled, breakdown.TemperatureScore),
                ScoreLabel("Rain", breakdown.RainfallEnabled, breakdown.RainfallScore),
                ScoreLabel("Grow", breakdown.GrowingSeasonEnabled, breakdown.GrowingSeasonScore),
                ScoreLabel("Poll", breakdown.PollutionEnabled, breakdown.PollutionScore),
                ScoreLabel("Forage", breakdown.ForageEnabled, breakdown.ForageScore),
                ScoreLabel("Move", breakdown.MovementEnabled, breakdown.MovementScore)
            };
            return string.Join(" | ", parts);
        }

        private static string ScoreLabel(string label, bool enabled, float value)
        {
            return enabled ? $"{label} {(value * 100f):F0}%" : $"{label} n/a";
        }

        private static void AppendContextLines(int tileId, MatchBreakdown breakdown, StringBuilder builder)
        {
            var snapshot = LandingZoneContext.State?.WorldSnapshot;
            if (snapshot == null || !snapshot.TryGetInfo(tileId, out var basicInfo))
                return;

            // Fetch extended properties from cache
            var extended = LandingZoneContext.Filters?.TileCache.GetOrCompute(tileId) ?? default;

            builder.AppendLine($"Climate: {FormatTemperature(basicInfo.Temperature)} / Rain {basicInfo.Rainfall:F0}mm / Growing {extended.GrowingDays:F0}d");
            builder.AppendLine($"Terrain: {basicInfo.Hilliness.GetLabelCap()} / Move {extended.MovementDifficulty:F1} / Pollution {(extended.Pollution * 100f):F0}% / Forage {(extended.Forageability * 100f):F0}%");

            if (extended.StoneDefNames != null && extended.StoneDefNames.Length > 0)
            {
                var stoneLabels = extended.StoneDefNames.Select(ResolveStoneLabel);
                builder.AppendLine("Stone: " + string.Join(", ", stoneLabels));
            }

            if (basicInfo.FeatureDef != null)
            {
                builder.AppendLine($"Feature: {basicInfo.FeatureDef.label}");
            }

            var tags = new List<string>();
            AppendRequirement(tags, "Coastal", breakdown.CoastalImportance, breakdown.HasCoastal);
            AppendRequirement(tags, "River", breakdown.RiverImportance, breakdown.HasRiver);
            AppendRequirement(tags, "Graze", breakdown.GrazeImportance, breakdown.CanGraze);

            if (breakdown.FeatureImportance != FilterImportance.Ignored && !string.IsNullOrEmpty(breakdown.RequiredFeature))
            {
                AppendRequirement(tags, breakdown.RequiredFeature!, breakdown.FeatureImportance, breakdown.FeatureMatched);
            }

            if (breakdown.StoneImportance != FilterImportance.Ignored && breakdown.RequiredStoneCount > 0)
            {
                var label = $"Stone {breakdown.StoneMatches}/{breakdown.RequiredStoneCount}";
                if (breakdown.StoneMatches >= breakdown.RequiredStoneCount)
                {
                    tags.Add($"{label} {ImportanceIcon(breakdown.StoneImportance)}");
                }
                else
                {
                    tags.Add($"{label} ✗");
                }
            }

            if (!breakdown.HillinessAllowed)
            {
                tags.Add("Terrain ✗");
            }

            if (tags.Count > 0)
            {
                builder.AppendLine(string.Join(" | ", tags));
            }
        }

        private static void AppendRequirement(List<string> tags, string label, FilterImportance importance, bool satisfied)
        {
            if (importance == FilterImportance.Ignored)
                return;

            if (satisfied)
            {
                tags.Add($"{label} {ImportanceIcon(importance)}");
            }
            else
            {
                tags.Add($"{label} ✗");
            }
        }

        private static string ImportanceIcon(FilterImportance importance)
        {
            return importance switch
            {
                FilterImportance.Critical => "!",
                FilterImportance.Preferred => "✓",
                _ => "X"
            };
        }

        private static string ResolveStoneLabel(string defName)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            return def?.label ?? defName;
        }

        private static string FormatTemperature(float celsius)
        {
            return LandingZoneMod.UseFahrenheit
                ? $"{(celsius * 9f / 5f + 32f):F1}°F"
                : $"{celsius:F1}°C";
        }
    }
}

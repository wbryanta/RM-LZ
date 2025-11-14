using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LandingZone
{
    /// <summary>
    /// RimWorld Mod Settings for LandingZone.
    /// Provides user control over scoring weights, logging verbosity, and performance settings.
    /// NOTE: Class name must remain LandingZoneSettings for backward compatibility with saved configs.
    /// </summary>
    public class LandingZoneSettings : ModSettings
    {
        // ===== PERFORMANCE SETTINGS =====

        public bool AutoRunSearchOnWorldLoad = false;
        public int EvaluationChunkSize = 250;
        public static MaxCandidateTilesLimit MaxCandidates = MaxCandidateTilesLimit.Standard;
        public static bool AllowCancelSearch = true;

        // ===== SCORING WEIGHT PRESETS =====

        public static ScoringWeightPreset WeightPreset = ScoringWeightPreset.CriticalFocused;

        /// <summary>
        /// Gets the scoring weight values for the currently selected preset.
        /// Returns (critBase, prefBase, mutatorWeight) tuple.
        /// </summary>
        public static (float critBase, float prefBase, float mutatorWeight) GetWeightValues()
        {
            return WeightPreset switch
            {
                ScoringWeightPreset.Balanced => (4.0f, 1.0f, 0.1f),
                ScoringWeightPreset.CriticalFocused => (3.35f, 1.0f, 0.071f),
                ScoringWeightPreset.StrictHierarchy => (4.0f, 1.0f, 0.05f),
                ScoringWeightPreset.UltraCritical => (5.0f, 1.0f, 0.04f),
                ScoringWeightPreset.PrecisionMatch => (5.0f, 1.0f, 0.0f),
                _ => (3.35f, 1.0f, 0.071f) // Default to CriticalFocused
            };
        }

        // ===== LOGGING LEVELS =====

        public static LoggingLevel LogLevel = LoggingLevel.Standard;

        // ===== MOD SETTINGS UI =====

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref AutoRunSearchOnWorldLoad, "autoRunSearchOnWorldLoad", false);
            Scribe_Values.Look(ref EvaluationChunkSize, "evaluationChunkSize", 250);
            Scribe_Values.Look(ref WeightPreset, "weightPreset", ScoringWeightPreset.CriticalFocused);
            Scribe_Values.Look(ref LogLevel, "logLevel", LoggingLevel.Standard);
            Scribe_Values.Look(ref MaxCandidates, "maxCandidates", MaxCandidateTilesLimit.Standard);
            Scribe_Values.Look(ref AllowCancelSearch, "allowCancelSearch", true);

            // Clamp evaluation chunk size
            EvaluationChunkSize = Mathf.Clamp(EvaluationChunkSize, 50, 1000);
        }

        public void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            // Section: Performance Settings
            listingStandard.CheckboxLabeled("Auto-run search when world loads", ref AutoRunSearchOnWorldLoad);
            listingStandard.CheckboxLabeled("Allow cancel search (show Stop button)", ref AllowCancelSearch);

            listingStandard.Gap(8f);

            listingStandard.Label($"Tiles processed per frame: {EvaluationChunkSize}");
            EvaluationChunkSize = Mathf.RoundToInt(listingStandard.Slider(EvaluationChunkSize, 50, 1000));
            listingStandard.Gap(4f);
            Text.Font = GameFont.Tiny;
            listingStandard.Label("Lower values keep the UI snappier but take longer to finish.");
            Text.Font = GameFont.Small;

            listingStandard.Gap(12f);

            // Max Candidate Tiles Limit
            listingStandard.Label("Max Candidate Tiles:");

            if (listingStandard.ButtonTextLabeled("Current limit:", MaxCandidates.ToLabel()))
            {
                var options = new List<FloatMenuOption>();
                foreach (MaxCandidateTilesLimit limit in System.Enum.GetValues(typeof(MaxCandidateTilesLimit)))
                {
                    options.Add(new FloatMenuOption(limit.ToLabel(), () => MaxCandidates = limit));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            listingStandard.Gap(4f);
            Text.Font = GameFont.Tiny;
            listingStandard.Label(MaxCandidates.GetTooltip());
            Text.Font = GameFont.Small;

            listingStandard.Gap(12f);

            // Section: Scoring Weights
            listingStandard.Label("Scoring Weight Preset:");

            if (listingStandard.ButtonTextLabeled("Current preset:", WeightPreset.ToLabel()))
            {
                var options = new List<FloatMenuOption>();
                foreach (ScoringWeightPreset preset in System.Enum.GetValues(typeof(ScoringWeightPreset)))
                {
                    options.Add(new FloatMenuOption(preset.ToLabel(), () => WeightPreset = preset));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            listingStandard.Gap(4f);
            Text.Font = GameFont.Tiny;
            listingStandard.Label(WeightPreset.GetTooltip());
            Text.Font = GameFont.Small;

            listingStandard.Gap(12f);

            // Section: Logging Level
            listingStandard.Label("Logging Verbosity:");

            if (listingStandard.ButtonTextLabeled("Current level:", LogLevel.ToLabel()))
            {
                var options = new List<FloatMenuOption>();
                foreach (LoggingLevel level in System.Enum.GetValues(typeof(LoggingLevel)))
                {
                    options.Add(new FloatMenuOption(level.ToLabel(), () => LogLevel = level));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            listingStandard.Gap(4f);
            Text.Font = GameFont.Tiny;
            listingStandard.Label(LogLevel.GetTooltip());
            Text.Font = GameFont.Small;

            listingStandard.End();
        }
    }

    // ===== ENUMS =====

    /// <summary>
    /// Scoring weight presets that control the critical:preferred:bonus hierarchy.
    /// Each preset defines critBase, prefBase, and mutatorWeight values.
    /// </summary>
    public enum ScoringWeightPreset
    {
        /// <summary>
        /// Balanced (7:2:1) - Original default. Good all-around balance.
        /// critBase=4.0, prefBase=1.0, mutWeight=0.1
        /// </summary>
        Balanced,

        /// <summary>
        /// Critical Focused (10:3:1) - NEW DEFAULT. Critical requirements dominate, mutators are tiebreakers.
        /// critBase=3.35, prefBase=1.0, mutWeight=0.071
        /// </summary>
        CriticalFocused,

        /// <summary>
        /// Strict Hierarchy (16:4:1) - Very critical-dominant. Missing 1 critical = 4 preferreds.
        /// critBase=4.0, prefBase=1.0, mutWeight=0.05
        /// </summary>
        StrictHierarchy,

        /// <summary>
        /// Ultra Critical (20:4:1) - Amplified critical importance. Criticals overwhelmingly important.
        /// critBase=5.0, prefBase=1.0, mutWeight=0.04
        /// </summary>
        UltraCritical,

        /// <summary>
        /// Precision Match (5:1:0) - Pure filter matching. Mutator bonuses completely ignored.
        /// critBase=5.0, prefBase=1.0, mutWeight=0.0
        /// </summary>
        PrecisionMatch
    }

    /// <summary>
    /// Logging verbosity levels to control log spam.
    /// </summary>
    public enum LoggingLevel
    {
        /// <summary>
        /// Verbose - Log every tile evaluation. Floods log with thousands of lines. Use for debugging.
        /// </summary>
        Verbose,

        /// <summary>
        /// Standard (DEFAULT) - Log phase summaries only. ~10-20 lines per search. Recommended for normal use.
        /// </summary>
        Standard,

        /// <summary>
        /// Brief - Log only final results count and timing. ~3-5 lines per search. Minimal noise.
        /// </summary>
        Brief
    }

    /// <summary>
    /// Maximum candidate tiles to evaluate after cheap filter phase.
    /// Controls memory usage and search performance.
    /// </summary>
    public enum MaxCandidateTilesLimit
    {
        /// <summary>
        /// Conservative (25,000) - Original limit. Good for low-memory systems (8-16GB RAM).
        /// </summary>
        Conservative,

        /// <summary>
        /// Moderate (50,000) - Increased limit. For systems with 16GB+ RAM.
        /// </summary>
        Moderate,

        /// <summary>
        /// High (75,000) - High limit. For systems with 32GB+ RAM.
        /// </summary>
        High,

        /// <summary>
        /// Standard (100,000) - DEFAULT. Very high limit. For systems with 32GB+ RAM.
        /// </summary>
        Standard,

        /// <summary>
        /// Maximum (150,000) - Essentially unlimited for most worlds (50% coverage = ~150k settleable tiles).
        /// Recommended for development/testing only. Requires 32GB+ RAM.
        /// </summary>
        Maximum
    }

    // ===== EXTENSION METHODS =====

    public static class ScoringWeightPresetExtensions
    {
        public static string ToLabel(this ScoringWeightPreset preset)
        {
            return preset switch
            {
                ScoringWeightPreset.Balanced => "Balanced (7:2:1)",
                ScoringWeightPreset.CriticalFocused => "Critical Focused (10:3:1)",
                ScoringWeightPreset.StrictHierarchy => "Strict Hierarchy (16:4:1)",
                ScoringWeightPreset.UltraCritical => "Ultra Critical (20:4:1)",
                ScoringWeightPreset.PrecisionMatch => "Precision Match (5:1:0)",
                _ => "Unknown"
            };
        }

        public static string GetTooltip(this ScoringWeightPreset preset)
        {
            return preset switch
            {
                ScoringWeightPreset.Balanced =>
                    "Good all-around balance. Mutators have meaningful impact. Original default.",
                ScoringWeightPreset.CriticalFocused =>
                    "Critical requirements dominate. Mutators are tiebreakers. NEW DEFAULT.",
                ScoringWeightPreset.StrictHierarchy =>
                    "Very critical-dominant. Missing 1 critical = missing 4 preferreds.",
                ScoringWeightPreset.UltraCritical =>
                    "Amplified critical importance. Criticals overwhelmingly important.",
                ScoringWeightPreset.PrecisionMatch =>
                    "Pure filter matching. Mutator bonuses completely ignored.",
                _ => ""
            };
        }
    }

    public static class LoggingLevelExtensions
    {
        public static string ToLabel(this LoggingLevel level)
        {
            return level switch
            {
                LoggingLevel.Verbose => "Verbose",
                LoggingLevel.Standard => "Standard",
                LoggingLevel.Brief => "Brief",
                _ => "Unknown"
            };
        }

        public static string GetTooltip(this LoggingLevel level)
        {
            return level switch
            {
                LoggingLevel.Verbose =>
                    "Log every tile evaluation. Floods log with thousands of lines. Use for debugging.",
                LoggingLevel.Standard =>
                    "Log phase summaries only. ~10-20 lines per search. DEFAULT. Recommended for normal use.",
                LoggingLevel.Brief =>
                    "Log only final results count and timing. ~3-5 lines per search. Minimal noise.",
                _ => ""
            };
        }
    }

    public static class MaxCandidateTilesLimitExtensions
    {
        public static string ToLabel(this MaxCandidateTilesLimit limit)
        {
            return limit switch
            {
                MaxCandidateTilesLimit.Conservative => "Conservative (25k)",
                MaxCandidateTilesLimit.Moderate => "Moderate (50k)",
                MaxCandidateTilesLimit.High => "High (75k)",
                MaxCandidateTilesLimit.Standard => "Standard (100k)",
                MaxCandidateTilesLimit.Maximum => "Maximum (150k)",
                _ => "Unknown"
            };
        }

        public static string GetTooltip(this MaxCandidateTilesLimit limit)
        {
            return limit switch
            {
                MaxCandidateTilesLimit.Conservative =>
                    "25,000 tiles. Original limit. Good for low-memory systems (8-16GB RAM).",
                MaxCandidateTilesLimit.Moderate =>
                    "50,000 tiles. Increased limit. For systems with 16GB+ RAM.",
                MaxCandidateTilesLimit.High =>
                    "75,000 tiles. High limit. For systems with 32GB+ RAM.",
                MaxCandidateTilesLimit.Standard =>
                    "100,000 tiles. DEFAULT. Very high limit. For systems with 32GB+ RAM.",
                MaxCandidateTilesLimit.Maximum =>
                    "150,000 tiles. Essentially unlimited for most worlds. Recommended for development. Requires 32GB+ RAM.",
                _ => ""
            };
        }

        public static int GetValue(this MaxCandidateTilesLimit limit)
        {
            return limit switch
            {
                MaxCandidateTilesLimit.Conservative => 25000,
                MaxCandidateTilesLimit.Moderate => 50000,
                MaxCandidateTilesLimit.High => 75000,
                MaxCandidateTilesLimit.Standard => 100000,
                MaxCandidateTilesLimit.Maximum => 150000,
                _ => 100000
            };
        }
    }
}

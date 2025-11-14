using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LandingZone.Core
{
    /// <summary>
    /// RimWorld Mod Settings for LandingZone.
    /// Provides user control over scoring weights, logging verbosity, and performance settings.
    /// </summary>
    public class LandingZoneModSettings : ModSettings
    {
        // ===== PERFORMANCE SETTINGS =====

        public bool AutoRunSearchOnWorldLoad = false;
        public int EvaluationChunkSize = 250;

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

            // Clamp evaluation chunk size
            EvaluationChunkSize = Mathf.Clamp(EvaluationChunkSize, 50, 1000);
        }

        public void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            // Section: Performance Settings
            listingStandard.CheckboxLabeled("Auto-run search when world loads", ref AutoRunSearchOnWorldLoad);
            listingStandard.Label($"Tiles processed per frame: {EvaluationChunkSize}");
            EvaluationChunkSize = Mathf.RoundToInt(listingStandard.Slider(EvaluationChunkSize, 50, 1000));
            listingStandard.Gap(4f);
            Text.Font = GameFont.Tiny;
            listingStandard.Label("Lower values keep the UI snappier but take longer to finish.");
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
}

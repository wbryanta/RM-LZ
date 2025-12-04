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
        public int EvaluationChunkSize = 500;
        public static MaxCandidateTilesLimit MaxCandidates = MaxCandidateTilesLimit.Standard;
        public static bool AllowCancelSearch = true;
        public static PerformanceProfile CurrentPerformanceProfile = PerformanceProfile.Default;

        // ===== FEATURES =====

        /// <summary>
        /// Enable LandingZone UI on the in-game world map (during gameplay).
        /// DEFAULT: true - Shows LandingZone controls when viewing the world map.
        /// </summary>
        public static bool EnableInGameWorldMap = true;

        /// <summary>
        /// Maximum candidates to process with Heavy filters (Growing Days, etc).
        /// Heavy filters are deferred until top N candidates by cheap filter scoring.
        /// This prevents 100k+ candidates × expensive operations = multi-minute delays.
        /// Default: 1000 (1000 tiles × 3ms = 3 seconds vs 100k tiles × 3ms = 5 minutes)
        /// </summary>
        public static int MaxCandidatesForHeavyFilters = 1000;

        // ===== DEFAULT PRESET =====

        /// <summary>
        /// The ID of the preset to apply automatically when loading a new world.
        /// DEFAULT: "balanced" - The Balanced preset is applied by default.
        /// Users can set any preset (stock or user-created) as their persistent default.
        /// If the selected default is deleted, falls back to "balanced".
        /// </summary>
        public static string DefaultPresetId = "balanced";

        // ===== MUTATOR QUALITY SETTINGS =====

        /// <summary>
        /// User-defined quality overrides for mutators.
        /// Key: mutator defName, Value: quality rating (-10 to +10).
        /// Overrides take precedence over default ratings.
        /// </summary>
        public static Dictionary<string, int> UserMutatorQualityOverrides = new Dictionary<string, int>();

        /// <summary>
        /// When enabled, inverts all mutator quality scores (Challenge Mode).
        /// Positive mutators become negative and vice versa.
        /// Useful for players who want harsh environments.
        /// </summary>
        public static bool InvertMutatorQuality = false;

        // ===== SCORING WEIGHT PRESETS =====

        public static ScoringWeightPreset WeightPreset = ScoringWeightPreset.CriticalFocused;

        // ===== USER PRESETS (GLOBAL PERSISTENCE) =====

        private List<Data.Preset> _userPresets = new List<Data.Preset>();

        /// <summary>
        /// Gets the global user presets list. Presets are stored in ModSettings and
        /// persist across all saves/games. Modified by PresetLibrary.
        /// </summary>
        public List<Data.Preset> UserPresets => _userPresets;

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
            Scribe_Values.Look(ref EvaluationChunkSize, "evaluationChunkSize", 500);
            Scribe_Values.Look(ref WeightPreset, "weightPreset", ScoringWeightPreset.CriticalFocused);
            Scribe_Values.Look(ref LogLevel, "logLevel", LoggingLevel.Standard);
            Scribe_Values.Look(ref MaxCandidates, "maxCandidates", MaxCandidateTilesLimit.Standard);
            Scribe_Values.Look(ref AllowCancelSearch, "allowCancelSearch", true);
            Scribe_Values.Look(ref CurrentPerformanceProfile, "performanceProfile", PerformanceProfile.Default);
            Scribe_Values.Look(ref MaxCandidatesForHeavyFilters, "maxCandidatesForHeavyFilters", 1000);
            Scribe_Values.Look(ref EnableInGameWorldMap, "enableInGameWorldMap", true);
            Scribe_Values.Look(ref DefaultPresetId, "defaultPresetId", "balanced");

            // Mutator quality settings
            Scribe_Collections.Look(ref UserMutatorQualityOverrides, "userMutatorQualityOverrides", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref InvertMutatorQuality, "invertMutatorQuality", false);

            // User presets (global persistence)
            Scribe_Collections.Look(ref _userPresets, "userPresets", LookMode.Deep);

            // Ensure dictionary is never null after load
            if (Scribe.mode == LoadSaveMode.LoadingVars && UserMutatorQualityOverrides == null)
            {
                UserMutatorQualityOverrides = new Dictionary<string, int>();
            }

            // Clamp evaluation chunk size
            EvaluationChunkSize = Mathf.Clamp(EvaluationChunkSize, 50, 1000);

            // Clamp heavy filter candidates (100 to unlimited)
            MaxCandidatesForHeavyFilters = Mathf.Max(MaxCandidatesForHeavyFilters, 100);

            // Ensure user presets list is never null
            if (Scribe.mode == LoadSaveMode.LoadingVars && _userPresets == null)
            {
                _userPresets = new List<Data.Preset>();
            }
        }

        /// <summary>
        /// Applies a performance profile, updating EvaluationChunkSize and MaxCandidates.
        /// Persists settings to disk immediately.
        /// </summary>
        public void ApplyPerformanceProfile(PerformanceProfile profile)
        {
            CurrentPerformanceProfile = profile;

            switch (profile)
            {
                case PerformanceProfile.Default:
                    EvaluationChunkSize = 500;
                    MaxCandidates = MaxCandidateTilesLimit.Standard;
                    break;

                case PerformanceProfile.HighEnd:
                    EvaluationChunkSize = 1000;
                    MaxCandidates = MaxCandidateTilesLimit.Unlimited;
                    break;

                case PerformanceProfile.Safe:
                    EvaluationChunkSize = 250;
                    MaxCandidates = MaxCandidateTilesLimit.Conservative;
                    break;
            }

            LandingZoneMod.Instance?.WriteSettings();
        }

        // Scroll position for settings window
        private static Vector2 _scrollPosition = Vector2.zero;

        // Estimated content height for scrollable area
        private const float EstimatedContentHeight = 900f;

        public void DoSettingsWindowContents(Rect inRect)
        {
            // Create scrollable area
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, EstimatedContentHeight);
            Widgets.BeginScrollView(inRect, ref _scrollPosition, viewRect);

            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(viewRect);

            // ═══════════════════════════════════════════════════════════════
            // SECTION: Performance Profile (Quick Apply)
            // ═══════════════════════════════════════════════════════════════
            listingStandard.Label("Performance Profile:");
            listingStandard.Gap(4f);

            Rect profileButtonsRect = listingStandard.GetRect(30f);
            float buttonWidth = (profileButtonsRect.width - 16f) / 3f; // 3 buttons with 8px gaps

            // Default button
            if (Widgets.ButtonText(new Rect(profileButtonsRect.x, profileButtonsRect.y, buttonWidth, 30f),
                PerformanceProfile.Default.ToLabel()))
            {
                ApplyPerformanceProfile(PerformanceProfile.Default);
                Messages.Message("Performance profile set to Default (Chunk: 500, Max: 100k)", MessageTypeDefOf.NeutralEvent, false);
            }

            // High-end button
            if (Widgets.ButtonText(new Rect(profileButtonsRect.x + buttonWidth + 8f, profileButtonsRect.y, buttonWidth, 30f),
                PerformanceProfile.HighEnd.ToLabel()))
            {
                ApplyPerformanceProfile(PerformanceProfile.HighEnd);
                Messages.Message("Performance profile set to High-end (Chunk: 1000, Max: Unlimited)", MessageTypeDefOf.TaskCompletion, false);
            }

            // Safe button
            if (Widgets.ButtonText(new Rect(profileButtonsRect.x + (buttonWidth + 8f) * 2f, profileButtonsRect.y, buttonWidth, 30f),
                PerformanceProfile.Safe.ToLabel()))
            {
                ApplyPerformanceProfile(PerformanceProfile.Safe);
                Messages.Message("Performance profile set to Safe (Chunk: 250, Max: 25k)", MessageTypeDefOf.CautionInput, false);
            }

            listingStandard.Gap(4f);
            Text.Font = GameFont.Tiny;
            listingStandard.Label($"Current: {CurrentPerformanceProfile.ToLabel()} - {CurrentPerformanceProfile.GetTooltip()}");
            Text.Font = GameFont.Small;

            listingStandard.GapLine(12f);

            // ═══════════════════════════════════════════════════════════════
            // SECTION: Features
            // ═══════════════════════════════════════════════════════════════
            listingStandard.Label("Features:");
            listingStandard.Gap(4f);
            listingStandard.CheckboxLabeled("Auto-run search when world loads", ref AutoRunSearchOnWorldLoad);
            listingStandard.CheckboxLabeled("Allow cancel search (show Stop button)", ref AllowCancelSearch);
            listingStandard.CheckboxLabeled("Enable in-game world map", ref EnableInGameWorldMap);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            listingStandard.Label("   Shows LandingZone controls when viewing the world map during gameplay.");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            listingStandard.GapLine(12f);

            // ═══════════════════════════════════════════════════════════════
            // SECTION: Advanced Performance
            // ═══════════════════════════════════════════════════════════════
            listingStandard.Label("Advanced Performance:");
            listingStandard.Gap(4f);
            listingStandard.Label($"Tiles processed per frame: {EvaluationChunkSize}");
            EvaluationChunkSize = Mathf.RoundToInt(listingStandard.Slider(EvaluationChunkSize, 50, 1000));
            listingStandard.Gap(4f);
            Text.Font = GameFont.Tiny;
            listingStandard.Label("Lower values keep UI responsive. High-end systems can use 1000 for faster searches.");
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

            // Max candidates for Heavy filters (adaptive threshold tuning)
            string heavyLabel = MaxCandidatesForHeavyFilters >= 500000 ? "Unlimited" : MaxCandidatesForHeavyFilters.ToString("N0");
            listingStandard.Label($"Max candidates for Heavy filters: {heavyLabel}");
            MaxCandidatesForHeavyFilters = Mathf.RoundToInt(listingStandard.Slider(MaxCandidatesForHeavyFilters, 100, 500000));
            listingStandard.Gap(4f);
            Text.Font = GameFont.Tiny;
            listingStandard.Label("Affects adaptive k-of-n threshold. Higher = stricter gate enforcement. 500k+ = unlimited.");
            Text.Font = GameFont.Small;

            listingStandard.GapLine(12f);

            // ═══════════════════════════════════════════════════════════════
            // SECTION: Scoring
            // ═══════════════════════════════════════════════════════════════
            listingStandard.Label("Scoring:");

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

            listingStandard.GapLine(12f);

            // ═══════════════════════════════════════════════════════════════
            // SECTION: Logging & Debug
            // ═══════════════════════════════════════════════════════════════
            listingStandard.Label("Logging & Debug:");

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

            listingStandard.GapLine(12f);

            // ═══════════════════════════════════════════════════════════════
            // SECTION: Mutator Quality
            // ═══════════════════════════════════════════════════════════════
            listingStandard.Label("LandingZone_Settings_MutatorQualityHeader".Translate());

            // Challenge Mode toggle
            listingStandard.CheckboxLabeled("LandingZone_Settings_ChallengeMode".Translate(), ref InvertMutatorQuality);
            listingStandard.Gap(4f);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            listingStandard.Label("LandingZone_Settings_ChallengeModeDesc".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            listingStandard.Gap(8f);

            // Configure Mutators button
            if (listingStandard.ButtonText("LandingZone_Settings_ConfigureMutators".Translate()))
            {
                Find.WindowStack.Add(new Core.UI.Dialog_MutatorQualitySettings());
            }

            listingStandard.Gap(4f);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            listingStandard.Label("LandingZone_Settings_ConfigureMutatorsDesc".Translate());
            int overrideCount = UserMutatorQualityOverrides?.Count ?? 0;
            if (overrideCount > 0)
            {
                string plural = overrideCount == 1 ? "" : "s";
                listingStandard.Label("LandingZone_Settings_CustomRatingsCount".Translate(overrideCount, plural));
            }
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            listingStandard.End();
            Widgets.EndScrollView();
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
        /// Verbose - Full diagnostic logging: all match dumps, progress ticks, stack traces, deep predicate logs.
        /// Use for debugging and forensic analysis. Floods log with thousands of lines.
        /// </summary>
        Verbose,

        /// <summary>
        /// Standard (DEFAULT) - Balanced logging: start/complete lines, one filter summary block, top-3 DEBUG dump.
        /// Recommended for normal use. ~10-20 lines per search.
        /// </summary>
        Standard,

        /// <summary>
        /// Minimal - Only start/complete lines with preset, mode, strictness, duration, best score.
        /// No filter dumps, no progress, no stack traces. ~2-4 lines per search. Minimal noise.
        /// </summary>
        Minimal
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
        /// Memory/performance guardrail to prevent evaluating excessive candidates.
        /// </summary>
        Standard,

        /// <summary>
        /// Maximum (150,000) - Very high limit for relaxed filters or development/testing.
        /// Requires 32GB+ RAM. May result in long search times.
        /// </summary>
        Maximum,

        /// <summary>
        /// Unlimited - Process ALL settleable tiles (no cap). Use with caution!
        /// Can process 150k+ tiles on 50% coverage worlds. Requires 32GB+ RAM.
        /// WARNING: May cause long delays and memory pressure on large worlds.
        /// </summary>
        Unlimited
    }

    /// <summary>
    /// Performance profile presets for quick configuration.
    /// Provides one-click settings for different hardware capabilities and use cases.
    /// </summary>
    public enum PerformanceProfile
    {
        /// <summary>
        /// Default - Balanced settings for most systems.
        /// EvaluationChunkSize=500, MaxCandidates=Standard (100k).
        /// Recommended for systems with 16GB+ RAM.
        /// </summary>
        Default,

        /// <summary>
        /// HighEnd - Maximum performance for powerful systems.
        /// EvaluationChunkSize=1000, MaxCandidates=Unlimited.
        /// Requires 32GB+ RAM. Fastest searches, highest memory usage.
        /// </summary>
        HighEnd,

        /// <summary>
        /// Safe - Conservative settings for low-memory systems or large worlds.
        /// EvaluationChunkSize=250, MaxCandidates=Conservative (25k).
        /// Recommended for systems with 8-16GB RAM or worlds with 200k+ tiles.
        /// </summary>
        Safe
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
                LoggingLevel.Minimal => "Minimal",
                _ => "Unknown"
            };
        }

        public static string GetTooltip(this LoggingLevel level)
        {
            return level switch
            {
                LoggingLevel.Verbose =>
                    "Full diagnostic logging: all match dumps, progress ticks, stack traces, deep logs. Use for debugging.",
                LoggingLevel.Standard =>
                    "Balanced logging: start/complete + filter summary + top-3 DEBUG dump. DEFAULT. Recommended for normal use.",
                LoggingLevel.Minimal =>
                    "Only start/complete lines with key metrics. No dumps, no progress. ~2-4 lines per search. Minimal noise.",
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
                MaxCandidateTilesLimit.Unlimited => "Unlimited (All settleable)",
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
                    "100,000 tiles. DEFAULT. Memory/performance guardrail to prevent evaluating excessive candidates. For systems with 32GB+ RAM.",
                MaxCandidateTilesLimit.Maximum =>
                    "150,000 tiles. Very high limit. Recommended for relaxed filters or development. Requires 32GB+ RAM.",
                MaxCandidateTilesLimit.Unlimited =>
                    "NO LIMIT. Process ALL settleable tiles (150k+ on large worlds). WARNING: May cause long delays and memory pressure. Use with caution!",
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
                MaxCandidateTilesLimit.Unlimited => int.MaxValue,
                _ => 100000
            };
        }
    }

    public static class PerformanceProfileExtensions
    {
        public static string ToLabel(this PerformanceProfile profile)
        {
            return profile switch
            {
                PerformanceProfile.Default => "Default",
                PerformanceProfile.HighEnd => "High-end",
                PerformanceProfile.Safe => "Safe",
                _ => "Unknown"
            };
        }

        public static string GetTooltip(this PerformanceProfile profile)
        {
            return profile switch
            {
                PerformanceProfile.Default =>
                    "Balanced settings. Chunk: 500, Max Candidates: 100k. Good for most systems (16GB+ RAM).",
                PerformanceProfile.HighEnd =>
                    "Maximum performance. Chunk: 1000, Max Candidates: Unlimited. Requires 32GB+ RAM. Fastest searches.",
                PerformanceProfile.Safe =>
                    "Conservative settings. Chunk: 250, Max Candidates: 25k. For low-memory systems (8-16GB) or large worlds (200k+ tiles).",
                _ => ""
            };
        }
    }
}

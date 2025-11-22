using System;
using System.Linq;
using LandingZone.Data;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LandingZone.Core.Filtering
{
    /// <summary>
    /// Provides fast selectivity estimates for filters using heuristics.
    /// Unlike FilterSelectivityAnalyzer (which evaluates predicates on all tiles),
    /// this uses canonical data and heuristics for instant feedback in the UI.
    /// </summary>
    public sealed class FilterSelectivityEstimator
    {
        private int _totalSettleableTiles;
        private int _totalWorldTiles;
        private bool _initialized;

        public void Initialize()
        {
            if (_initialized) return;

            var world = Find.World;
            if (world == null) return;

            _totalWorldTiles = world.grid.TilesCount;

            // Count settleable tiles (approximation: non-ocean, non-lake, non-seaice)
            _totalSettleableTiles = 0;
            var grid = Find.WorldGrid;
            if (grid == null) return;

            for (int i = 0; i < _totalWorldTiles; i++)
            {
                var tile = grid[i];
                var biome = tile?.PrimaryBiome;
                if (biome != null &&
                    biome.canBuildBase &&
                    !biome.impassable)
                {
                    _totalSettleableTiles++;
                }
            }

            _initialized = true;
        }

        /// <summary>
        /// Estimates selectivity for a temperature range filter.
        /// Assumes normal distribution centered at 21°C with σ=25°C.
        /// </summary>
        public SelectivityEstimate EstimateTemperatureRange(FloatRange range, FilterImportance importance)
        {
            if (importance == FilterImportance.Ignored)
                return SelectivityEstimate.FullMatch(_totalSettleableTiles);

            // Heuristic: ~40% of tiles in 10-32°C range (default)
            // Widen/narrow based on range
            float rangeMid = (range.min + range.max) / 2f;
            float rangeWidth = range.max - range.min;

            // Base selectivity for temperate range (10-32°C, 22° wide)
            float baseSelectivity = 0.40f;

            // Adjust for range width
            float widthFactor = Math.Min(rangeWidth / 22f, 2.0f); // Cap at 2x
            float selectivity = baseSelectivity * widthFactor;

            // Adjust for extreme ranges
            if (rangeMid < -20f || rangeMid > 40f)
                selectivity *= 0.5f; // Extreme climates less common

            selectivity = Mathf.Clamp(selectivity, 0.01f, 0.95f);

            int estimatedMatches = (int)(_totalSettleableTiles * selectivity);
            return new SelectivityEstimate(estimatedMatches, _totalSettleableTiles, importance, false);
        }

        /// <summary>
        /// Estimates selectivity for rainfall range.
        /// </summary>
        public SelectivityEstimate EstimateRainfallRange(FloatRange range, FilterImportance importance)
        {
            if (importance == FilterImportance.Ignored)
                return SelectivityEstimate.FullMatch(_totalSettleableTiles);

            // Heuristic: 1000-2200mm covers ~38% of tiles
            float rangeMid = (range.min + range.max) / 2f;
            float rangeWidth = range.max - range.min;

            float baseSelectivity = 0.38f;
            float widthFactor = Math.Min(rangeWidth / 1200f, 2.5f);
            float selectivity = baseSelectivity * widthFactor;

            // Very low rainfall (<300mm) or very high (>3000mm) is rare
            if (rangeMid < 300f || rangeMid > 3000f)
                selectivity *= 0.6f;

            selectivity = Mathf.Clamp(selectivity, 0.05f, 0.90f);

            int estimatedMatches = (int)(_totalSettleableTiles * selectivity);
            return new SelectivityEstimate(estimatedMatches, _totalSettleableTiles, importance, false);
        }

        /// <summary>
        /// Estimates selectivity for growing days range.
        /// </summary>
        public SelectivityEstimate EstimateGrowingDaysRange(FloatRange range, FilterImportance importance)
        {
            if (importance == FilterImportance.Ignored)
                return SelectivityEstimate.FullMatch(_totalSettleableTiles);

            // Heuristic: 40-60 days covers ~35% of tiles
            float rangeWidth = range.max - range.min;
            float selectivity = 0.35f * Math.Min(rangeWidth / 20f, 2.0f);

            // Very short seasons (<20 days) are rare
            if (range.max < 20f)
                selectivity *= 0.3f;

            selectivity = Mathf.Clamp(selectivity, 0.05f, 0.85f);

            int estimatedMatches = (int)(_totalSettleableTiles * selectivity);
            return new SelectivityEstimate(estimatedMatches, _totalSettleableTiles, importance, false);
        }

        /// <summary>
        /// Estimates selectivity for hilliness multi-select.
        /// </summary>
        public SelectivityEstimate EstimateHilliness(System.Collections.Generic.HashSet<Hilliness> allowed)
        {
            if (allowed == null || allowed.Count == 0)
                return SelectivityEstimate.FullMatch(_totalSettleableTiles);

            // Approximate distribution: Flat 25%, SmallHills 30%, LargeHills 30%, Mountainous 15%
            float selectivity = 0f;
            if (allowed.Contains(Hilliness.Flat)) selectivity += 0.25f;
            if (allowed.Contains(Hilliness.SmallHills)) selectivity += 0.30f;
            if (allowed.Contains(Hilliness.LargeHills)) selectivity += 0.30f;
            if (allowed.Contains(Hilliness.Mountainous)) selectivity += 0.15f;

            int estimatedMatches = (int)(_totalSettleableTiles * selectivity);
            return new SelectivityEstimate(estimatedMatches, _totalSettleableTiles, FilterImportance.Critical, false);
        }

        /// <summary>
        /// Estimates selectivity for coastal filter.
        /// </summary>
        public SelectivityEstimate EstimateCoastal(FilterImportance importance)
        {
            if (importance == FilterImportance.Ignored)
                return SelectivityEstimate.FullMatch(_totalSettleableTiles);

            // Approximate: ~15% of settleable tiles are coastal
            float selectivity = 0.15f;
            int estimatedMatches = (int)(_totalSettleableTiles * selectivity);
            return new SelectivityEstimate(estimatedMatches, _totalSettleableTiles, importance, false);
        }

        /// <summary>
        /// Estimates selectivity for water access (coastal OR river).
        /// </summary>
        public SelectivityEstimate EstimateWaterAccess(FilterImportance importance)
        {
            if (importance == FilterImportance.Ignored)
                return SelectivityEstimate.FullMatch(_totalSettleableTiles);

            // Approximate: ~30% coastal + river (with overlap correction)
            float selectivity = 0.30f;
            int estimatedMatches = (int)(_totalSettleableTiles * selectivity);
            return new SelectivityEstimate(estimatedMatches, _totalSettleableTiles, importance, false);
        }

        /// <summary>
        /// Estimates selectivity for a specific map feature/mutator.
        /// Uses canonical rarity data where available.
        /// </summary>
        public SelectivityEstimate EstimateMapFeature(string featureDefName, FilterImportance importance)
        {
            if (importance == FilterImportance.Ignored)
                return SelectivityEstimate.FullMatch(_totalSettleableTiles);

            // Canonical rarity lookup (from docs/data)
            float selectivity = featureDefName switch
            {
                // Common features (5%+)
                "Caves" => 0.081f,           // 8.1%
                "Mountain" => 0.170f,        // 17.0%

                // Uncommon features (1-5%)
                "SunnyMutator" => 0.019f,    // 1.9%
                "AnimalLife_Increased" => 0.015f,
                "PlantLife_Increased" => 0.014f,
                "WildPlants" => 0.017f,

                // Rare features (0.1-1%)
                "SteamGeysers_Increased" => 0.010f,
                "Fertile" => 0.006f,
                "Junkyard" => 0.008f,
                "WindyMutator" => 0.004f,

                // Very rare features (0.01-0.1%)
                "AncientRuins" => 0.0003f,
                "ArcheanTrees" => 0.00003f,
                "MineralRich" => 0.0002f,
                "Stockpile" => 0.0003f,

                // Default: assume uncommon
                _ => 0.03f
            };

            int estimatedMatches = (int)(_totalSettleableTiles * selectivity);
            return new SelectivityEstimate(estimatedMatches, _totalSettleableTiles, importance, false);
        }

        /// <summary>
        /// Estimates selectivity for multi-select map features with AND/OR operator.
        /// </summary>
        public SelectivityEstimate EstimateMapFeatures(
            IndividualImportanceContainer<string> features,
            FilterImportance minimumImportance = FilterImportance.Preferred)
        {
            var criticalFeatures = features.GetCriticalItems().ToList();
            var preferredFeatures = features.GetPreferredItems().ToList();

            if (criticalFeatures.Count == 0 && preferredFeatures.Count == 0)
                return SelectivityEstimate.FullMatch(_totalSettleableTiles);

            // Only consider features at or above minimum importance
            var relevantFeatures = minimumImportance == FilterImportance.Critical
                ? criticalFeatures
                : criticalFeatures.Concat(preferredFeatures).ToList();

            if (relevantFeatures.Count == 0)
                return SelectivityEstimate.FullMatch(_totalSettleableTiles);

            // Estimate based on operator
            if (features.Operator == ImportanceOperator.OR)
            {
                // OR: Union of all features (account for overlap)
                // Sum individual probabilities, reduce by overlap factor
                float totalSelectivity = 0f;
                foreach (var feature in relevantFeatures)
                {
                    var estimate = EstimateMapFeature(feature, FilterImportance.Critical);
                    totalSelectivity += estimate.Selectivity;
                }

                // Reduce by overlap factor (features rarely co-occur)
                totalSelectivity = Math.Min(totalSelectivity * 0.85f, 0.95f);

                int estimatedMatches = (int)(_totalSettleableTiles * totalSelectivity);
                return new SelectivityEstimate(estimatedMatches, _totalSettleableTiles, FilterImportance.Critical, false);
            }
            else // AND
            {
                // AND: Intersection - multiply probabilities
                float combinedSelectivity = 1.0f;
                foreach (var feature in relevantFeatures)
                {
                    var estimate = EstimateMapFeature(feature, FilterImportance.Critical);
                    combinedSelectivity *= estimate.Selectivity;
                }

                int estimatedMatches = (int)(_totalSettleableTiles * combinedSelectivity);
                return new SelectivityEstimate(estimatedMatches, _totalSettleableTiles, FilterImportance.Critical, false);
            }
        }

        /// <summary>
        /// Gets the total number of settleable tiles for display.
        /// </summary>
        public int GetSettleableTiles()
        {
            Initialize();
            return _totalSettleableTiles;
        }
    }

    /// <summary>
    /// Fast selectivity estimate without full tile evaluation.
    /// </summary>
    public readonly struct SelectivityEstimate
    {
        public SelectivityEstimate(int matchCount, int totalTiles, FilterImportance importance, bool isHeavy)
        {
            MatchCount = matchCount;
            TotalTiles = totalTiles;
            Importance = importance;
            IsHeavy = isHeavy;
        }

        public int MatchCount { get; }
        public int TotalTiles { get; }
        public FilterImportance Importance { get; }
        public bool IsHeavy { get; }

        public float Selectivity => TotalTiles == 0 ? 0f : (float)MatchCount / TotalTiles;

        public static SelectivityEstimate FullMatch(int totalTiles)
        {
            return new SelectivityEstimate(totalTiles, totalTiles, FilterImportance.Ignored, false);
        }

        /// <summary>
        /// Formats for display: "~45% of tiles (127k/295k)"
        /// </summary>
        public string FormatForDisplay()
        {
            if (Importance == FilterImportance.Ignored)
                return "All tiles";

            return $"~{Selectivity:P0} of tiles ({FormatCount(MatchCount)}/{FormatCount(TotalTiles)})";
        }

        private static string FormatCount(int count)
        {
            if (count >= 1000000)
                return $"{count / 1000000.0:F1}M";
            if (count >= 1000)
                return $"{count / 1000.0:F0}k";
            return count.ToString();
        }
    }
}

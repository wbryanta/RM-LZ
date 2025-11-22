using System.Collections.Generic;
using System.Linq;
using LandingZone.Core.Filtering;
using LandingZone.Data;

namespace LandingZone.Core.UI
{
    /// <summary>
    /// Severity level for filter conflicts.
    /// </summary>
    public enum ConflictSeverity
    {
        /// <summary>
        /// Informational - suggests improvements but not critical
        /// </summary>
        Info,

        /// <summary>
        /// Warning - configuration may produce unexpected results
        /// </summary>
        Warning,

        /// <summary>
        /// Error - configuration is logically impossible or will return 0 results
        /// </summary>
        Error
    }

    /// <summary>
    /// Represents a detected conflict in filter configuration.
    /// </summary>
    public class FilterConflict
    {
        public ConflictSeverity Severity { get; set; }
        public string Message { get; set; } = "";
        public string Suggestion { get; set; } = "";
        public string FilterId { get; set; } = "";  // Which filter group this applies to
    }

    /// <summary>
    /// Detects logical conflicts and impossibilities in filter configurations.
    /// Provides inline warnings to help users avoid zero-result searches.
    /// Uses FilterSelectivityEstimator for accurate overall restrictiveness detection.
    /// </summary>
    public static class ConflictDetector
    {
        /// <summary>
        /// Analyzes filter settings and returns detected conflicts.
        /// Uses selectivity estimation for better accuracy.
        /// </summary>
        public static List<FilterConflict> DetectConflicts(FilterSettings filters)
        {
            var conflicts = new List<FilterConflict>();

            // Rule 1: Impossible AND for single-slot items (Rivers, Stones)
            DetectImpossibleRiverAnd(filters, conflicts);
            DetectImpossibleStoneAnd(filters, conflicts);

            // Rule 2: Contradictory climate ranges
            DetectClimateContradictions(filters, conflicts);

            // Rule 3: Overall restrictiveness (uses selectivity estimation)
            DetectOverlyRestrictive(filters, conflicts);

            // Rule 4: Map feature conflicts (AND with mutually exclusive features)
            DetectMapFeatureConflicts(filters, conflicts);

            return conflicts;
        }

        /// <summary>
        /// Rule 1a: Detects impossible AND operators for rivers.
        /// Tiles can only have ONE river type, so AND with multiple rivers = 0 results.
        /// </summary>
        private static void DetectImpossibleRiverAnd(FilterSettings filters, List<FilterConflict> conflicts)
        {
            if (!filters.Rivers.HasCritical)
                return;

            if (filters.Rivers.Operator == ImportanceOperator.AND)
            {
                var criticalCount = filters.Rivers.GetCriticalItems().Count();
                if (criticalCount > 1)
                {
                    conflicts.Add(new FilterConflict
                    {
                        Severity = ConflictSeverity.Error,
                        FilterId = "rivers",
                        Message = $"Impossible: {criticalCount} river types with AND (tiles have max 1 river)",
                        Suggestion = "Switch to OR operator or keep only 1 Critical river type"
                    });
                }
            }
        }

        /// <summary>
        /// Rule 1b: Detects impossible AND operators for single-slot items.
        /// RimWorld tiles only have 1 ore type, so multiple Critical stones with AND is impossible.
        /// </summary>
        private static void DetectImpossibleStoneAnd(FilterSettings filters, List<FilterConflict> conflicts)
        {
            if (!filters.Stones.HasCritical)
                return;

            var criticalStones = filters.Stones.GetCriticalItems().ToList();

            if (criticalStones.Count > 1 && filters.Stones.Operator == ImportanceOperator.AND)
            {
                conflicts.Add(new FilterConflict
                {
                    Severity = ConflictSeverity.Error,
                    FilterId = "stones",
                    Message = $"Impossible requirement: {criticalStones.Count} stones with AND operator",
                    Suggestion = "Switch to OR operator (tiles only have 1 ore type)"
                });
            }
        }

        /// <summary>
        /// Rule 2: Detects contradictory climate filter combinations.
        /// Example: Cold biome + hot temperature, high rainfall + desert, etc.
        /// </summary>
        private static void DetectClimateContradictions(FilterSettings filters, List<FilterConflict> conflicts)
        {
            // Check: High growing days + extreme cold
            if (filters.GrowingDaysImportance == FilterImportance.Critical &&
                filters.AverageTemperatureImportance == FilterImportance.Critical)
            {
                if (filters.GrowingDaysRange.min > 40 && filters.AverageTemperatureRange.max < -10f)
                {
                    conflicts.Add(new FilterConflict
                    {
                        Severity = ConflictSeverity.Error,
                        FilterId = "climate",
                        Message = "Contradictory: High growing days require warm temperatures",
                        Suggestion = "Increase minimum temperature or decrease growing days requirement"
                    });
                }
            }

            // Check: Desert + high rainfall
            if (filters.RainfallImportance == FilterImportance.Critical)
            {
                if (filters.RainfallRange.min > 1200f)
                {
                    // Check if any desert biome is selected in locked biome
                    // For now, just warn about high rainfall requirements
                    conflicts.Add(new FilterConflict
                    {
                        Severity = ConflictSeverity.Info,
                        FilterId = "climate",
                        Message = "High rainfall requirement limits biome options",
                        Suggestion = "Desert and arid biomes will be excluded"
                    });
                }
            }
        }

        /// <summary>
        /// Rule 3: Detects overly restrictive Critical filter combinations.
        /// Uses selectivity estimation to predict number of matching tiles.
        /// </summary>
        private static void DetectOverlyRestrictive(FilterSettings filters, List<FilterConflict> conflicts)
        {
            try
            {
                var estimator = LandingZoneContext.SelectivityEstimator;

                // Collect all critical filter estimates
                var estimates = new List<SelectivityEstimate>();

                // Temperature filters
                if (filters.AverageTemperatureImportance == FilterImportance.Critical)
                    estimates.Add(estimator.EstimateTemperatureRange(filters.AverageTemperatureRange, FilterImportance.Critical));
                if (filters.MinimumTemperatureImportance == FilterImportance.Critical)
                    estimates.Add(estimator.EstimateTemperatureRange(filters.MinimumTemperatureRange, FilterImportance.Critical));
                if (filters.MaximumTemperatureImportance == FilterImportance.Critical)
                    estimates.Add(estimator.EstimateTemperatureRange(filters.MaximumTemperatureRange, FilterImportance.Critical));

                // Rainfall
                if (filters.RainfallImportance == FilterImportance.Critical)
                    estimates.Add(estimator.EstimateRainfallRange(filters.RainfallRange, FilterImportance.Critical));

                // Growing days
                if (filters.GrowingDaysImportance == FilterImportance.Critical)
                    estimates.Add(estimator.EstimateGrowingDaysRange(filters.GrowingDaysRange, FilterImportance.Critical));

                // Hilliness (always critical if restricted)
                if (filters.AllowedHilliness.Count < 4)
                    estimates.Add(estimator.EstimateHilliness(filters.AllowedHilliness));

                // Coastal
                if (filters.CoastalImportance == FilterImportance.Critical)
                    estimates.Add(estimator.EstimateCoastal(FilterImportance.Critical));

                // Map features (if critical)
                if (filters.MapFeatures.HasCritical)
                    estimates.Add(estimator.EstimateMapFeatures(filters.MapFeatures, FilterImportance.Critical));

                // If no critical filters, no conflict
                if (estimates.Count == 0)
                    return;

                // Estimate combined selectivity (multiply probabilities for AND logic)
                float combinedSelectivity = 1.0f;
                foreach (var estimate in estimates)
                {
                    combinedSelectivity *= estimate.Selectivity;
                }

                int estimatedMatches = (int)(estimator.GetSettleableTiles() * combinedSelectivity);

                // Thresholds for warnings
                const int VeryLowThreshold = 100;   // Red alert: < 100 tiles
                const int LowThreshold = 500;       // Warning: < 500 tiles

                if (estimatedMatches < VeryLowThreshold)
                {
                    conflicts.Add(new FilterConflict
                    {
                        Severity = ConflictSeverity.Error,
                        FilterId = "general",
                        Message = $"Very restrictive config: Only ~{estimatedMatches} tiles estimated ({estimates.Count} critical filters)",
                        Suggestion = "Relax some Critical→Preferred, use Fallback Tiers, or widen ranges"
                    });
                }
                else if (estimatedMatches < LowThreshold)
                {
                    conflicts.Add(new FilterConflict
                    {
                        Severity = ConflictSeverity.Warning,
                        FilterId = "general",
                        Message = $"Restrictive config: Only ~{estimatedMatches} tiles estimated ({estimates.Count} critical filters)",
                        Suggestion = "Consider relaxing some Critical filters to Preferred if search yields few results"
                    });
                }
            }
            catch
            {
                // Fallback to simple count-based detection if estimation fails
                int criticalCount = CountCriticalFilters(filters);

                if (criticalCount >= 8)
                {
                    conflicts.Add(new FilterConflict
                    {
                        Severity = ConflictSeverity.Warning,
                        FilterId = "general",
                        Message = $"{criticalCount} Critical filters may be too restrictive",
                        Suggestion = "Consider changing some Critical filters to Preferred for more results"
                    });
                }
            }
        }

        /// <summary>
        /// Rule 4: Detects problematic map feature configurations.
        /// Warns about AND with ultra-rare features or mutually exclusive combinations.
        /// </summary>
        private static void DetectMapFeatureConflicts(FilterSettings filters, List<FilterConflict> conflicts)
        {
            if (!filters.MapFeatures.HasCritical)
                return;

            // Only check if using AND operator
            if (filters.MapFeatures.Operator != ImportanceOperator.AND)
                return;

            var criticalFeatures = filters.MapFeatures.GetCriticalItems().ToList();

            // Check for AND with ultra-rare features (< 0.1% selectivity)
            var ultraRareFeatures = new[] { "ArcheanTrees", "MineralRich", "Stockpile", "AncientRuins", "AncientWarehouse" };
            var presentUltraRare = criticalFeatures.Where(f => ultraRareFeatures.Contains(f)).ToList();

            if (presentUltraRare.Count > 1)
            {
                conflicts.Add(new FilterConflict
                {
                    Severity = ConflictSeverity.Error,
                    FilterId = "map_features",
                    Message = $"Ultra-rare AND: {presentUltraRare.Count} ultra-rare features (each <0.1% of tiles)",
                    Suggestion = "Switch to OR operator or relax some to Preferred importance"
                });
            }

            // Check for AND with many critical mutators (even if not ultra-rare)
            if (criticalFeatures.Count >= 4)
            {
                conflicts.Add(new FilterConflict
                {
                    Severity = ConflictSeverity.Warning,
                    FilterId = "map_features",
                    Message = $"{criticalFeatures.Count} Critical features with AND may produce very few results",
                    Suggestion = "Switch to OR operator or reduce Critical feature count"
                });
            }
        }

        /// <summary>
        /// Counts the total number of Critical filters configured.
        /// </summary>
        private static int CountCriticalFilters(FilterSettings filters)
        {
            int count = 0;

            // Individual filters
            if (filters.AverageTemperatureImportance == FilterImportance.Critical) count++;
            if (filters.RainfallImportance == FilterImportance.Critical) count++;
            if (filters.GrowingDaysImportance == FilterImportance.Critical) count++;
            if (filters.PollutionImportance == FilterImportance.Critical) count++;
            if (filters.SwampinessImportance == FilterImportance.Critical) count++;
            if (filters.ForageImportance == FilterImportance.Critical) count++;
            if (filters.PlantDensityImportance == FilterImportance.Critical) count++;
            if (filters.AnimalDensityImportance == FilterImportance.Critical) count++;
            if (filters.FishPopulationImportance == FilterImportance.Critical) count++;
            if (filters.GrazeImportance == FilterImportance.Critical) count++;
            if (filters.CoastalImportance == FilterImportance.Critical) count++;
            if (filters.CoastalLakeImportance == FilterImportance.Critical) count++;
            if (filters.WaterAccessImportance == FilterImportance.Critical) count++;
            if (filters.MovementDifficultyImportance == FilterImportance.Critical) count++;
            if (filters.LandmarkImportance == FilterImportance.Critical) count++;

            // Container filters (count as 1 each if they have critical items)
            if (filters.Stones.HasCritical) count++;
            if (filters.Rivers.HasCritical) count++;
            if (filters.Roads.HasCritical) count++;
            if (filters.MapFeatures.HasCritical) count++;
            if (filters.Stockpiles.HasCritical) count++;
            if (filters.ForageableFoodImportance == FilterImportance.Critical) count++;

            // Hilliness
            if (filters.AllowedHilliness.Count > 0 && filters.AllowedHilliness.Count < 5) count++;

            // Biome lock
            if (filters.LockedBiome != null) count++;

            return count;
        }

        /// <summary>
        /// Gets a user-friendly description for an ImportanceOperator.
        /// </summary>
        public static string GetOperatorDescription(ImportanceOperator op, string itemType)
        {
            return op == ImportanceOperator.OR
                ? $"Match: ANY of the selected {itemType}"
                : $"Match: ALL of the selected {itemType}";
        }

        /// <summary>
        /// Gets a detailed explanation of operator behavior.
        /// </summary>
        public static string GetOperatorTooltip(ImportanceOperator op, string itemType, int criticalCount)
        {
            if (op == ImportanceOperator.OR)
            {
                return $"OR operator: Tiles need AT LEAST ONE of the {criticalCount} selected {itemType}.\n\n" +
                       $"More flexible - typically produces more results.";
            }
            else
            {
                return $"AND operator: Tiles need ALL {criticalCount} selected {itemType}.\n\n" +
                       $"Very restrictive - may produce few or zero results.";
            }
        }
    }
}

using System.Collections.Generic;
using System.Linq;
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
    /// </summary>
    public static class ConflictDetector
    {
        /// <summary>
        /// Analyzes filter settings and returns detected conflicts.
        /// </summary>
        public static List<FilterConflict> DetectConflicts(FilterSettings filters)
        {
            var conflicts = new List<FilterConflict>();

            // Rule 1: Impossible AND for single-slot items (Stones)
            DetectImpossibleStoneAnd(filters, conflicts);

            // Rule 2: Contradictory climate ranges
            DetectClimateContradictions(filters, conflicts);

            // Rule 3: Too many Critical filters
            DetectOverlyRestrictive(filters, conflicts);

            return conflicts;
        }

        /// <summary>
        /// Rule 1: Detects impossible AND operators for single-slot items.
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
        /// Many Critical filters may produce zero results.
        /// </summary>
        private static void DetectOverlyRestrictive(FilterSettings filters, List<FilterConflict> conflicts)
        {
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

            // Check for multiple Critical mutators with AND
            if (filters.MapFeatures.HasCritical)
            {
                var criticalMutators = filters.MapFeatures.GetCriticalItems().ToList();
                if (criticalMutators.Count >= 4 && filters.MapFeatures.Operator == ImportanceOperator.AND)
                {
                    conflicts.Add(new FilterConflict
                    {
                        Severity = ConflictSeverity.Warning,
                        FilterId = "map_features",
                        Message = $"{criticalMutators.Count} Critical mutators with AND may produce few results",
                        Suggestion = "Switch to OR operator or reduce Critical mutator count"
                    });
                }
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

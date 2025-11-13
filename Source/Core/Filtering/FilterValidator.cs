using System;
using System.Collections.Generic;
using System.Text;
using LandingZone.Data;
using UnityEngine;
using Verse;

namespace LandingZone.Core.Filtering
{
    /// <summary>
    /// Validates filter settings and detects conflicts or impossible conditions.
    /// Provides diagnostic messages to help users fix problematic filter configurations.
    /// </summary>
    public static class FilterValidator
    {
        /// <summary>
        /// Validation issue severity level.
        /// </summary>
        public enum IssueSeverity
        {
            Info,       // Informational - not a problem
            Warning,    // May cause unexpected results
            Error       // Likely to cause 0 results or errors
        }

        /// <summary>
        /// A validation issue found in filter settings.
        /// </summary>
        public class ValidationIssue
        {
            public IssueSeverity Severity { get; set; }
            public string FilterName { get; set; }
            public string Message { get; set; }
            public string Suggestion { get; set; }

            public Color GetSeverityColor()
            {
                return Severity switch
                {
                    IssueSeverity.Info => new Color(0.5f, 0.8f, 1f),      // Light blue
                    IssueSeverity.Warning => new Color(1f, 0.8f, 0.2f),   // Yellow
                    IssueSeverity.Error => new Color(1f, 0.3f, 0.3f),     // Red
                    _ => Color.white
                };
            }

            public string GetSeverityIcon()
            {
                return Severity switch
                {
                    IssueSeverity.Info => "ℹ",
                    IssueSeverity.Warning => "⚠",
                    IssueSeverity.Error => "✗",
                    _ => "?"
                };
            }
        }

        /// <summary>
        /// Validates all filter settings and returns a list of issues.
        /// </summary>
        public static List<ValidationIssue> ValidateFilters(FilterSettings filters)
        {
            var issues = new List<ValidationIssue>();

            if (filters == null)
                return issues;

            // Validate temperature ranges
            ValidateTemperatureFilters(filters, issues);

            // Validate other range filters
            ValidateRangeFilter(filters.RainfallRange, "Rainfall", issues);
            ValidateRangeFilter(filters.GrowingDaysRange, "Growing Days", issues);
            ValidateRangeFilter(filters.PollutionRange, "Pollution", issues);
            ValidateRangeFilter(filters.ForageabilityRange, "Forageability", issues);
            ValidateRangeFilter(filters.MovementDifficultyRange, "Movement Difficulty", issues);
            ValidateRangeFilter(filters.ElevationRange, "Elevation", issues);
            ValidateRangeFilter(filters.SwampinessRange, "Swampiness", issues);
            ValidateRangeFilter(filters.AnimalDensityRange, "Animal Density", issues);
            ValidateRangeFilter(filters.FishPopulationRange, "Fish Population", issues);
            ValidateRangeFilter(filters.PlantDensityRange, "Plant Density", issues);

            // Validate IndividualImportanceContainer filters
            ValidateIndividualImportanceContainer(filters.Rivers, "Rivers", issues);
            ValidateIndividualImportanceContainer(filters.Roads, "Roads", issues);
            ValidateIndividualImportanceContainer(filters.Stones, "Stones", issues);
            ValidateIndividualImportanceContainer(filters.MapFeatures, "Map Features", issues);
            ValidateIndividualImportanceContainer(filters.AdjacentBiomes, "Adjacent Biomes", issues);

            // Validate stone count mode
            if (filters.UseStoneCount)
            {
                ValidateRangeFilter(filters.StoneCountRange, "Stone Count", issues);
            }

            // Validate critical strictness
            ValidateCriticalStrictness(filters, issues);

            // Validate hilliness
            if (filters.AllowedHilliness.Count == 0)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Error,
                    FilterName = "Hilliness",
                    Message = "No hilliness types are selected.",
                    Suggestion = "Select at least one hilliness type (Flat, Small Hills, Large Hills, Mountainous)."
                });
            }

            // Check for overly restrictive combinations
            ValidateFilterCombinations(filters, issues);

            return issues;
        }

        /// <summary>
        /// Validates temperature filter settings for conflicts.
        /// </summary>
        private static void ValidateTemperatureFilters(FilterSettings filters, List<ValidationIssue> issues)
        {
            ValidateRangeFilter(filters.AverageTemperatureRange, "Average Temperature", issues);
            ValidateRangeFilter(filters.MinimumTemperatureRange, "Minimum Temperature", issues);
            ValidateRangeFilter(filters.MaximumTemperatureRange, "Maximum Temperature", issues);

            // Check if min temp max is greater than max temp min (physically impossible)
            if (filters.MinimumTemperatureImportance != FilterImportance.Ignored &&
                filters.MaximumTemperatureImportance != FilterImportance.Ignored)
            {
                if (filters.MinimumTemperatureRange.max > filters.MaximumTemperatureRange.min)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Warning,
                        FilterName = "Temperature",
                        Message = "Minimum temperature max is higher than maximum temperature min.",
                        Suggestion = "This creates a narrow temperature band. Consider adjusting the ranges."
                    });
                }
            }

            // Check if average temp is outside min/max bounds
            if (filters.AverageTemperatureImportance != FilterImportance.Ignored &&
                filters.MinimumTemperatureImportance != FilterImportance.Ignored)
            {
                if (filters.AverageTemperatureRange.min < filters.MinimumTemperatureRange.max)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Info,
                        FilterName = "Temperature",
                        Message = "Average temperature overlaps with minimum temperature range.",
                        Suggestion = "This is usually fine, but verify your temperature ranges make sense."
                    });
                }
            }
        }

        /// <summary>
        /// Validates a float range filter.
        /// </summary>
        private static void ValidateRangeFilter(FloatRange range, string filterName, List<ValidationIssue> issues)
        {
            if (range.min > range.max)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Error,
                    FilterName = filterName,
                    Message = $"Minimum value ({range.min:F1}) is greater than maximum value ({range.max:F1}).",
                    Suggestion = "Swap the min and max values or adjust the range."
                });
            }

            if (Mathf.Approximately(range.min, range.max))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Warning,
                    FilterName = filterName,
                    Message = $"Range is very narrow ({range.min:F1}).",
                    Suggestion = "A single-value filter may be too restrictive. Consider widening the range."
                });
            }
        }

        /// <summary>
        /// Validates an IndividualImportanceContainer filter.
        /// </summary>
        private static void ValidateIndividualImportanceContainer<T>(
            IndividualImportanceContainer<T> container,
            string filterName,
            List<ValidationIssue> issues)
        {
            if (container == null)
                return;

            // Check if all items are set to Critical (might be too restrictive)
            int criticalCount = container.CountByImportance(FilterImportance.Critical);
            int preferredCount = container.CountByImportance(FilterImportance.Preferred);

            if (container.HasCritical && criticalCount > 5)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Warning,
                    FilterName = filterName,
                    Message = $"{criticalCount} items marked as Critical.",
                    Suggestion = "Many critical items may eliminate all tiles. Consider changing some to 'Preferred'."
                });
            }

            // Info if many items are active
            if (container.HasAnyImportance && (criticalCount + preferredCount) > 10)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Info,
                    FilterName = filterName,
                    Message = $"{criticalCount + preferredCount} items configured.",
                    Suggestion = "Many active items may reduce results significantly."
                });
            }
        }

        /// <summary>
        /// Validates the critical strictness parameter.
        /// </summary>
        private static void ValidateCriticalStrictness(FilterSettings filters, List<ValidationIssue> issues)
        {
            if (filters.CriticalStrictness < 0f || filters.CriticalStrictness > 1f)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Error,
                    FilterName = "Critical Strictness",
                    Message = $"Value ({filters.CriticalStrictness:F2}) is outside valid range [0.0, 1.0].",
                    Suggestion = "Reset to 1.0 for strict matching or adjust to a valid value."
                });
            }

            // Info about relaxed strictness
            if (filters.CriticalStrictness < 1f && filters.CriticalStrictness >= 0.5f)
            {
                int criticalCount = CountCriticalFilters(filters);
                if (criticalCount > 0)
                {
                    int requiredMatches = Mathf.CeilToInt(criticalCount * filters.CriticalStrictness);
                    issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Info,
                        FilterName = "Critical Strictness",
                        Message = $"Relaxed to {filters.CriticalStrictness:P0} - tiles must match {requiredMatches} of {criticalCount} critical filters.",
                        Suggestion = "This k-of-n matching allows more flexible results."
                    });
                }
            }
        }

        /// <summary>
        /// Validates filter combinations that might be overly restrictive.
        /// </summary>
        private static void ValidateFilterCombinations(FilterSettings filters, List<ValidationIssue> issues)
        {
            int criticalFilterCount = CountCriticalFilters(filters);
            int preferredFilterCount = CountPreferredFilters(filters);
            int totalActive = criticalFilterCount + preferredFilterCount;

            // Warn if too many critical filters
            if (criticalFilterCount >= 8)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Warning,
                    FilterName = "Overall",
                    Message = $"{criticalFilterCount} critical filters active.",
                    Suggestion = "Many critical filters may eliminate all tiles. Consider changing some to 'Preferred' or use Critical Strictness < 1.0."
                });
            }

            // Info if many filters active
            if (totalActive >= 15)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Info,
                    FilterName = "Overall",
                    Message = $"{totalActive} filters active ({criticalFilterCount} critical, {preferredFilterCount} preferred).",
                    Suggestion = "Many active filters may reduce results significantly. Use presets to save configurations."
                });
            }

            // Check for conflicting biome + climate filters
            if (filters.LockedBiome != null && filters.AverageTemperatureImportance == FilterImportance.Critical)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Info,
                    FilterName = "Biome + Temperature",
                    Message = "Both biome lock and temperature filters are active.",
                    Suggestion = "Locked biome already constrains temperature. Consider relaxing temperature filter."
                });
            }

            // Check for conflicting stone filters
            if (filters.UseStoneCount && filters.Stones.HasAnyImportance)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Warning,
                    FilterName = "Stones",
                    Message = "Both 'Stone Count' mode and individual stone selections are active.",
                    Suggestion = "Use either Stone Count mode OR individual stone selections, not both."
                });
            }
        }

        /// <summary>
        /// Counts all critical filters across all filter types.
        /// </summary>
        private static int CountCriticalFilters(FilterSettings filters)
        {
            int count = 0;

            // Simple importance filters
            if (filters.AverageTemperatureImportance == FilterImportance.Critical) count++;
            if (filters.MinimumTemperatureImportance == FilterImportance.Critical) count++;
            if (filters.MaximumTemperatureImportance == FilterImportance.Critical) count++;
            if (filters.RainfallImportance == FilterImportance.Critical) count++;
            if (filters.GrowingDaysImportance == FilterImportance.Critical) count++;
            if (filters.PollutionImportance == FilterImportance.Critical) count++;
            if (filters.ForageImportance == FilterImportance.Critical) count++;
            if (filters.ForageableFoodImportance == FilterImportance.Critical) count++;
            if (filters.ElevationImportance == FilterImportance.Critical) count++;
            if (filters.SwampinessImportance == FilterImportance.Critical) count++;
            if (filters.GrazeImportance == FilterImportance.Critical) count++;
            if (filters.AnimalDensityImportance == FilterImportance.Critical) count++;
            if (filters.FishPopulationImportance == FilterImportance.Critical) count++;
            if (filters.PlantDensityImportance == FilterImportance.Critical) count++;
            if (filters.MovementDifficultyImportance == FilterImportance.Critical) count++;
            if (filters.CoastalImportance == FilterImportance.Critical) count++;
            if (filters.CoastalLakeImportance == FilterImportance.Critical) count++;
            if (filters.FeatureImportance == FilterImportance.Critical) count++;
            if (filters.LandmarkImportance == FilterImportance.Critical) count++;

            // IndividualImportanceContainer filters
            count += filters.Rivers.CountByImportance(FilterImportance.Critical);
            count += filters.Roads.CountByImportance(FilterImportance.Critical);
            count += filters.Stones.CountByImportance(FilterImportance.Critical);
            count += filters.MapFeatures.CountByImportance(FilterImportance.Critical);
            count += filters.AdjacentBiomes.CountByImportance(FilterImportance.Critical);

            return count;
        }

        /// <summary>
        /// Counts all preferred filters across all filter types.
        /// </summary>
        private static int CountPreferredFilters(FilterSettings filters)
        {
            int count = 0;

            // Simple importance filters
            if (filters.AverageTemperatureImportance == FilterImportance.Preferred) count++;
            if (filters.MinimumTemperatureImportance == FilterImportance.Preferred) count++;
            if (filters.MaximumTemperatureImportance == FilterImportance.Preferred) count++;
            if (filters.RainfallImportance == FilterImportance.Preferred) count++;
            if (filters.GrowingDaysImportance == FilterImportance.Preferred) count++;
            if (filters.PollutionImportance == FilterImportance.Preferred) count++;
            if (filters.ForageImportance == FilterImportance.Preferred) count++;
            if (filters.ForageableFoodImportance == FilterImportance.Preferred) count++;
            if (filters.ElevationImportance == FilterImportance.Preferred) count++;
            if (filters.SwampinessImportance == FilterImportance.Preferred) count++;
            if (filters.GrazeImportance == FilterImportance.Preferred) count++;
            if (filters.AnimalDensityImportance == FilterImportance.Preferred) count++;
            if (filters.FishPopulationImportance == FilterImportance.Preferred) count++;
            if (filters.PlantDensityImportance == FilterImportance.Preferred) count++;
            if (filters.MovementDifficultyImportance == FilterImportance.Preferred) count++;
            if (filters.CoastalImportance == FilterImportance.Preferred) count++;
            if (filters.CoastalLakeImportance == FilterImportance.Preferred) count++;
            if (filters.FeatureImportance == FilterImportance.Preferred) count++;
            if (filters.LandmarkImportance == FilterImportance.Preferred) count++;

            // IndividualImportanceContainer filters
            count += filters.Rivers.CountByImportance(FilterImportance.Preferred);
            count += filters.Roads.CountByImportance(FilterImportance.Preferred);
            count += filters.Stones.CountByImportance(FilterImportance.Preferred);
            count += filters.MapFeatures.CountByImportance(FilterImportance.Preferred);
            count += filters.AdjacentBiomes.CountByImportance(FilterImportance.Preferred);

            return count;
        }

        /// <summary>
        /// Gets a summary message for validation results.
        /// </summary>
        public static string GetValidationSummary(List<ValidationIssue> issues)
        {
            if (issues.Count == 0)
                return "All filters are valid.";

            int errors = 0, warnings = 0, infos = 0;
            foreach (var issue in issues)
            {
                switch (issue.Severity)
                {
                    case IssueSeverity.Error: errors++; break;
                    case IssueSeverity.Warning: warnings++; break;
                    case IssueSeverity.Info: infos++; break;
                }
            }

            var sb = new StringBuilder();
            if (errors > 0) sb.Append($"{errors} error{(errors != 1 ? "s" : "")}");
            if (warnings > 0)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append($"{warnings} warning{(warnings != 1 ? "s" : "")}");
            }
            if (infos > 0)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append($"{infos} info");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets a formatted multi-line report of all validation issues.
        /// </summary>
        public static string GetValidationReport(List<ValidationIssue> issues)
        {
            if (issues.Count == 0)
                return "✓ All filters are valid.";

            var sb = new StringBuilder();
            sb.AppendLine("Filter Validation Issues:");
            sb.AppendLine();

            foreach (var issue in issues)
            {
                sb.AppendLine($"{issue.GetSeverityIcon()} [{issue.Severity}] {issue.FilterName}");
                sb.AppendLine($"   {issue.Message}");
                if (!string.IsNullOrEmpty(issue.Suggestion))
                {
                    sb.AppendLine($"   → {issue.Suggestion}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Checks if validation issues contain any errors.
        /// </summary>
        public static bool HasErrors(List<ValidationIssue> issues)
        {
            foreach (var issue in issues)
            {
                if (issue.Severity == IssueSeverity.Error)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Filters validation issues by severity.
        /// </summary>
        public static List<ValidationIssue> GetIssuesBySeverity(List<ValidationIssue> issues, IssueSeverity severity)
        {
            var filtered = new List<ValidationIssue>();
            foreach (var issue in issues)
            {
                if (issue.Severity == severity)
                    filtered.Add(issue);
            }
            return filtered;
        }
    }
}

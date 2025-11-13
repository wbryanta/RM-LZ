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

            // Validate stone count filter
            if (filters.UseStoneCount)
            {
                ValidateRangeFilter(filters.StoneCountRange, "Stone Count", issues);

                if (filters.RequiredStoneDefNames.Count == 0 && filters.StoneImportance != FilterImportance.Ignored)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Warning,
                        FilterName = "Stone Types",
                        Message = "Stone count filter is enabled but no stone types are selected.",
                        Suggestion = "Select some stone types or disable the stone count filter."
                    });
                }
            }

            // TODO: Update validation for IndividualImportanceContainer filters
            // ValidateMultiSelectFilter(filters.RiverTypes, "River Types", filters.RiverImportance, issues);
            // ValidateMultiSelectFilter(filters.RoadTypes, "Road Types", filters.RoadImportance, issues);
            // ValidateMultiSelectFilter(filters.MapFeatures, "Map Features", filters.MapFeatureImportance, issues);
            // ValidateMultiSelectFilter(filters.AdjacentBiomes, "Adjacent Biomes", filters.AdjacentBiomeImportance, issues);

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
        /// Validates a multi-select filter container.
        /// </summary>
        private static void ValidateMultiSelectFilter<T>(
            MultiSelectFilterContainer<T> container,
            string filterName,
            FilterImportance importance,
            List<ValidationIssue> issues)
        {
            if (importance == FilterImportance.Ignored)
                return; // Filter is disabled, no validation needed

            if (!container.HasSelection)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = importance == FilterImportance.Critical ? IssueSeverity.Error : IssueSeverity.Warning,
                    FilterName = filterName,
                    Message = "Filter is enabled but no items are selected.",
                    Suggestion = importance == FilterImportance.Critical
                        ? "Select at least one item or change importance to Preferred/Ignored."
                        : "Select at least one item or disable this filter."
                });
            }
            else if (container.LogicMode == FilterLogicMode.All && container.Count > 5)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Warning,
                    FilterName = filterName,
                    Message = $"AND logic with {container.Count} items may be too restrictive.",
                    Suggestion = "Consider using OR logic or reducing the number of selected items."
                });
            }
        }

        /// <summary>
        /// Validates filter combinations that might be overly restrictive.
        /// </summary>
        private static void ValidateFilterCombinations(FilterSettings filters, List<ValidationIssue> issues)
        {
            int criticalFilterCount = 0;
            int preferredFilterCount = 0;

            // Count active filters by importance
            if (filters.AverageTemperatureImportance == FilterImportance.Critical) criticalFilterCount++;
            if (filters.RainfallImportance == FilterImportance.Critical) criticalFilterCount++;
            if (filters.GrowingDaysImportance == FilterImportance.Critical) criticalFilterCount++;
            if (filters.RiverImportance == FilterImportance.Critical) criticalFilterCount++;
            if (filters.CoastalImportance == FilterImportance.Critical) criticalFilterCount++;
            if (filters.StoneImportance == FilterImportance.Critical) criticalFilterCount++;

            if (filters.AverageTemperatureImportance == FilterImportance.Preferred) preferredFilterCount++;
            if (filters.RainfallImportance == FilterImportance.Preferred) preferredFilterCount++;
            if (filters.GrowingDaysImportance == FilterImportance.Preferred) preferredFilterCount++;

            // Warn if too many critical filters
            if (criticalFilterCount >= 5)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Warning,
                    FilterName = "Overall",
                    Message = $"{criticalFilterCount} critical filters active.",
                    Suggestion = "Many critical filters may eliminate all tiles. Consider changing some to 'Preferred'."
                });
            }

            // Info if many filters active
            if (criticalFilterCount + preferredFilterCount >= 10)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Info,
                    FilterName = "Overall",
                    Message = $"{criticalFilterCount + preferredFilterCount} filters active.",
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
                    Message = "Both biome and temperature filters are active.",
                    Suggestion = "Biome selection already constrains temperature. Consider relaxing temperature filter."
                });
            }
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

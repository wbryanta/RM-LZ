using System;
using System.Collections.Generic;
using System.Linq;
using LandingZone.Core.Filtering.Filters;
using LandingZone.Data;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LandingZone.Core.UI
{
    /// <summary>
    /// Filter control implementations for Advanced Mode.
    /// Separated from main AdvancedModeUI for clarity given the size (~100+ controls).
    /// </summary>
    public static partial class AdvancedModeUI
    {
        // Scroll positions for embedded lists
        private static Vector2 _stonesScrollPosition = Vector2.zero;
        private static Vector2 _mineablesScrollPosition = Vector2.zero;

        // Vanilla common stone defNames (appear first in list)
        private static readonly HashSet<string> VanillaStones = new HashSet<string>
        {
            "Granite", "Limestone", "Marble", "Sandstone", "Slate"
        };

        // ============================================================================
        // HELPER METHODS - Common filter control patterns
        // ============================================================================

        private static FilterControl FloatRangeControl(
            string label,
            Func<FilterSettings, FloatRange> rangeGetter,
            Action<FilterSettings, FloatRange> rangeSetter,
            Func<FilterSettings, FilterImportance> importanceGetter,
            Action<FilterSettings, FilterImportance> importanceSetter,
            float min,
            float max,
            string unit = "",
            string filterId = null)
        {
            return new FilterControl(
                label,
                (listing, filters) =>
                {
                    var range = rangeGetter(filters);
                    var importance = importanceGetter(filters);

                    listing.Label($"{label}: {range.min:F1}{unit} to {range.max:F1}{unit}");
                    var rangeRect = listing.GetRect(30f);
                    Widgets.FloatRange(rangeRect, rangeRect.GetHashCode(), ref range, min, max);
                    rangeSetter(filters, range);

                    var importanceVal = importance;
                    UIHelpers.DrawImportanceSelector(listing.GetRect(30f), $"{label} Importance", ref importanceVal);
                    importanceSetter(filters, importanceVal);

                    // Live selectivity feedback (Tier 3)
                    if (!string.IsNullOrEmpty(filterId))
                    {
                        DrawSelectivityFeedback(listing, filterId, importanceVal);
                    }
                },
                filters =>
                {
                    var importance = importanceGetter(filters);
                    return (importance != FilterImportance.Ignored, importance);
                }
            );
        }

        private static FilterControl ImportanceOnlyControl(
            string label,
            Func<FilterSettings, FilterImportance> importanceGetter,
            Action<FilterSettings, FilterImportance> importanceSetter,
            string filterId = null)
        {
            return new FilterControl(
                label,
                (listing, filters) =>
                {
                    var importance = importanceGetter(filters);
                    UIHelpers.DrawImportanceSelector(listing.GetRect(30f), label, ref importance);
                    importanceSetter(filters, importance);

                    // Live selectivity feedback (Tier 3)
                    if (!string.IsNullOrEmpty(filterId))
                    {
                        DrawSelectivityFeedback(listing, filterId, importance);
                    }
                },
                filters =>
                {
                    var importance = importanceGetter(filters);
                    return (importance != FilterImportance.Ignored, importance);
                }
            );
        }

        // ============================================================================
        // USER INTENT GROUPS
        // ============================================================================

        private static List<FilterGroup> GetUserIntentGroups()
        {
            return new List<FilterGroup>
            {
                GetClimateComfortGroup(),
                GetTerrainAccessGroup(),
                GetResourcesProductionGroup(),
                GetSpecialFeaturesGroup(),
                GetBiomeControlGroup(),
                GetResultsControlGroup()
            };
        }

        private static FilterGroup GetClimateComfortGroup()
        {
            var filters = new List<FilterControl>
            {
                FloatRangeControl(
                    "LandingZone_Filter_TemperatureAverage".Translate(),
                    f => f.AverageTemperatureRange,
                    (f, v) => f.AverageTemperatureRange = v,
                    f => f.AverageTemperatureImportance,
                    (f, v) => f.AverageTemperatureImportance = v,
                    -60f, 60f, "°C",
                    "average_temperature"
                ),
                FloatRangeControl(
                    "LandingZone_Filter_TemperatureMinimum".Translate(),
                    f => f.MinimumTemperatureRange,
                    (f, v) => f.MinimumTemperatureRange = v,
                    f => f.MinimumTemperatureImportance,
                    (f, v) => f.MinimumTemperatureImportance = v,
                    -60f, 40f, "°C",
                    "minimum_temperature"
                ),
                FloatRangeControl(
                    "LandingZone_Filter_TemperatureMaximum".Translate(),
                    f => f.MaximumTemperatureRange,
                    (f, v) => f.MaximumTemperatureRange = v,
                    f => f.MaximumTemperatureImportance,
                    (f, v) => f.MaximumTemperatureImportance = v,
                    0f, 60f, "°C",
                    "maximum_temperature"
                ),
                FloatRangeControl(
                    "LandingZone_Filter_Rainfall".Translate(),
                    f => f.RainfallRange,
                    (f, v) => f.RainfallRange = v,
                    f => f.RainfallImportance,
                    (f, v) => f.RainfallImportance = v,
                    0f, 5300f, " mm/year",
                    "rainfall"
                ),
                FloatRangeControl(
                    "LandingZone_Filter_GrowingDays".Translate(),
                    f => f.GrowingDaysRange,
                    (f, v) => f.GrowingDaysRange = v,
                    f => f.GrowingDaysImportance,
                    (f, v) => f.GrowingDaysImportance = v,
                    0f, 60f, " days/year",
                    "growing_days"
                ),
                FloatRangeControl(
                    "LandingZone_Filter_Pollution".Translate(),
                    f => f.PollutionRange,
                    (f, v) => f.PollutionRange = v,
                    f => f.PollutionImportance,
                    (f, v) => f.PollutionImportance = v,
                    0f, 1f
                    // No filter ID - pollution may not have dedicated predicate
                ),
                new FilterControl(
                    "LandingZone_Filter_WeatherPatterns".Translate(),
                    (listing, filters) =>
                    {
                        listing.Label("LandingZone_Filter_WeatherPatterns".Translate());

                        var weatherMutators = GetWeatherMutators();
                        foreach (var mutator in weatherMutators)
                        {
                            var importance = filters.MapFeatures.GetImportance(mutator);
                            var friendlyLabel = MapFeatureFilter.GetMutatorFriendlyLabel(mutator);

                            // Check DLC requirement
                            string dlcRequirement = MapFeatureFilter.GetMutatorDLCRequirement(mutator);
                            bool isEnabled = string.IsNullOrEmpty(dlcRequirement) || DLCDetectionService.IsDLCAvailable(dlcRequirement);
                            string disabledReason = !isEnabled ? "LandingZone_Filter_DLCRequired".Translate(dlcRequirement) : null;

                            UIHelpers.DrawImportanceSelector(listing.GetRect(30f), friendlyLabel, ref importance, null, isEnabled, disabledReason);

                            if (isEnabled)
                            {
                                filters.MapFeatures.SetImportance(mutator, importance);
                            }
                        }
                    },
                    filters =>
                    {
                        var weatherMutators = GetWeatherMutators();
                        bool hasAny = weatherMutators.Any(m => filters.MapFeatures.GetImportance(m) != FilterImportance.Ignored);
                        bool hasCritical = weatherMutators.Any(m => filters.MapFeatures.GetImportance(m) == FilterImportance.Critical);
                        return (hasAny, hasCritical ? FilterImportance.Critical : FilterImportance.Preferred);
                    }
                ),
                new FilterControl(
                    "_ClimateWarnings",
                    (listing, filters) =>
                    {
                        // Show climate-specific conflict warnings
                        UIHelpers.DrawFilterConflicts(listing, AdvancedModeUI.GetActiveConflicts(), "climate");
                    },
                    filters => (false, FilterImportance.Ignored)  // Never active/counted
                )
            };

            return new FilterGroup("climate_comfort", "LandingZone_Filter_ClimateWeatherGroup".Translate(), filters);
        }

        private static FilterGroup GetTerrainAccessGroup()
        {
            var filters = new List<FilterControl>
            {
                new FilterControl(
                    "Hilliness",
                    (listing, filters) =>
                    {
                        // Show status: filtering vs allowing all
                        bool isFiltering = filters.AllowedHilliness.Count < 4;
                        string statusText = isFiltering
                            ? "LandingZone_Filter_HillinessFiltering".Translate(filters.AllowedHilliness.Count)
                            : "LandingZone_Filter_HillinessAllAllowed".Translate();
                        listing.Label(statusText);

                        var rect = listing.GetRect(30f);
                        DrawHillinessToggles(rect, filters);
                    },
                    filters => (filters.AllowedHilliness.Count < 4, FilterImportance.Critical)
                ),
                ImportanceOnlyControl(
                    "LandingZone_Filter_CoastalOcean".Translate(),
                    f => f.CoastalImportance,
                    (f, v) => f.CoastalImportance = v,
                    "coastal"
                ),
                ImportanceOnlyControl(
                    "LandingZone_Filter_CoastalLake".Translate(),
                    f => f.CoastalLakeImportance,
                    (f, v) => f.CoastalLakeImportance = v,
                    "coastal_lake"
                ),
                FloatRangeControl(
                    "LandingZone_Filter_Elevation".Translate(),
                    f => f.ElevationRange,
                    (f, v) => f.ElevationRange = v,
                    f => f.ElevationImportance,
                    (f, v) => f.ElevationImportance = v,
                    0f, 3100f, " m"
                ),
                FloatRangeControl(
                    "LandingZone_Filter_MovementDifficulty".Translate(),
                    f => f.MovementDifficultyRange,
                    (f, v) => f.MovementDifficultyRange = v,
                    f => f.MovementDifficultyImportance,
                    (f, v) => f.MovementDifficultyImportance = v,
                    0f, 2f
                ),
                FloatRangeControl(
                    "LandingZone_Filter_Swampiness".Translate(),
                    f => f.SwampinessRange,
                    (f, v) => f.SwampinessRange = v,
                    f => f.SwampinessImportance,
                    (f, v) => f.SwampinessImportance = v,
                    0f, 1.2f
                ),
                new FilterControl(
                    "Geographic Features",
                    (listing, filters) =>
                    {
                        listing.Label("LandingZone_Filter_GeographicFeatures".Translate());

                        var geographyMutators = GetGeographyMutators();
                        foreach (var mutator in geographyMutators)
                        {
                            var importance = filters.MapFeatures.GetImportance(mutator);
                            var friendlyLabel = MapFeatureFilter.GetMutatorFriendlyLabel(mutator);

                            // Check DLC requirement
                            string dlcRequirement = MapFeatureFilter.GetMutatorDLCRequirement(mutator);
                            bool isEnabled = string.IsNullOrEmpty(dlcRequirement) || DLCDetectionService.IsDLCAvailable(dlcRequirement);
                            string disabledReason = !isEnabled ? "LandingZone_Filter_DLCRequired".Translate(dlcRequirement) : null;

                            UIHelpers.DrawImportanceSelector(listing.GetRect(30f), friendlyLabel, ref importance, null, isEnabled, disabledReason);

                            if (isEnabled)
                            {
                                filters.MapFeatures.SetImportance(mutator, importance);
                            }
                        }
                    },
                    filters =>
                    {
                        var geographyMutators = GetGeographyMutators();
                        bool hasAny = geographyMutators.Any(m => filters.MapFeatures.GetImportance(m) != FilterImportance.Ignored);
                        bool hasCritical = geographyMutators.Any(m => filters.MapFeatures.GetImportance(m) == FilterImportance.Critical);
                        return (hasAny, hasCritical ? FilterImportance.Critical : FilterImportance.Preferred);
                    }
                ),
                new FilterControl(
                    "Rivers",
                    (listing, filters) =>
                    {
                        listing.Label("LandingZone_Filter_Rivers".Translate());

                        // Operator toggle (only show if critical items configured)
                        if (filters.Rivers.HasCritical)
                        {
                            var operatorRect = listing.GetRect(30f);
                            var operatorLabel = ConflictDetector.GetOperatorDescription(filters.Rivers.Operator, "rivers");
                            var operatorTooltip = ConflictDetector.GetOperatorTooltip(filters.Rivers.Operator, "rivers",
                                filters.Rivers.GetCriticalItems().Count());

                            if (Widgets.ButtonText(operatorRect, operatorLabel))
                            {
                                filters.Rivers.Operator = filters.Rivers.Operator == ImportanceOperator.OR
                                    ? ImportanceOperator.AND
                                    : ImportanceOperator.OR;
                            }
                            TooltipHandler.TipRegion(operatorRect, operatorTooltip);
                            listing.Gap(4f);

                            // Show conflict warnings specific to rivers
                            UIHelpers.DrawFilterConflicts(listing, AdvancedModeUI.GetActiveConflicts(), "rivers");
                        }

                        // Individual river list
                        var riverTypes = RiverFilter.GetAllRiverTypes().Select(r => r.defName).ToList();
                        foreach (var riverType in riverTypes)
                        {
                            var importance = filters.Rivers.GetImportance(riverType);
                            UIHelpers.DrawImportanceSelector(listing.GetRect(30f), riverType, ref importance);
                            filters.Rivers.SetImportance(riverType, importance);
                        }
                    },
                    filters => (filters.Rivers.HasAnyImportance, filters.Rivers.HasCritical ? FilterImportance.Critical : FilterImportance.Preferred)
                )
            };

            return new FilterGroup("terrain_access", "LandingZone_Filter_TerrainAccessGroup".Translate(), filters);
        }

        private static FilterGroup GetResourcesProductionGroup()
        {
            var filters = new List<FilterControl>
            {
                FloatRangeControl(
                    "LandingZone_Filter_Forageability".Translate(),
                    f => f.ForageabilityRange,
                    (f, v) => f.ForageabilityRange = v,
                    f => f.ForageImportance,
                    (f, v) => f.ForageImportance = v,
                    0f, 1f,
                    "",
                    "forageable_food"
                ),
                ImportanceOnlyControl(
                    "LandingZone_Filter_Grazeable".Translate(),
                    f => f.GrazeImportance,
                    (f, v) => f.GrazeImportance = v,
                    "graze"
                ),
                FloatRangeControl(
                    "LandingZone_Filter_AnimalDensity".Translate(),
                    f => f.AnimalDensityRange,
                    (f, v) => f.AnimalDensityRange = v,
                    f => f.AnimalDensityImportance,
                    (f, v) => f.AnimalDensityImportance = v,
                    0f, 6.5f
                ),
                FloatRangeControl(
                    "LandingZone_Filter_FishPopulation".Translate(),
                    f => f.FishPopulationRange,
                    (f, v) => f.FishPopulationRange = v,
                    f => f.FishPopulationImportance,
                    (f, v) => f.FishPopulationImportance = v,
                    0f, 900f
                ),
                FloatRangeControl(
                    "LandingZone_Filter_PlantDensity".Translate(),
                    f => f.PlantDensityRange,
                    (f, v) => f.PlantDensityRange = v,
                    f => f.PlantDensityImportance,
                    (f, v) => f.PlantDensityImportance = v,
                    0f, 1.3f
                ),
                new FilterControl(
                    "Resource Modifiers",
                    (listing, filters) =>
                    {
                        listing.Label("Resource Modifiers (tile-wide bonuses):");
                        Text.Font = GameFont.Tiny;
                        GUI.color = new Color(0.7f, 0.7f, 0.7f);
                        listing.Label("MineralRich = more ore veins on tile; Fertile = better soil quality");
                        GUI.color = Color.white;
                        Text.Font = GameFont.Small;
                        listing.Gap(2f);

                        var resourceMutators = GetResourceMutators();
                        foreach (var mutator in resourceMutators)
                        {
                            var importance = filters.MapFeatures.GetImportance(mutator);
                            var friendlyLabel = MapFeatureFilter.GetMutatorFriendlyLabel(mutator);

                            // Check DLC requirement
                            string dlcRequirement = MapFeatureFilter.GetMutatorDLCRequirement(mutator);
                            bool isEnabled = string.IsNullOrEmpty(dlcRequirement) || DLCDetectionService.IsDLCAvailable(dlcRequirement);
                            string disabledReason = !isEnabled ? "LandingZone_Filter_DLCRequired".Translate(dlcRequirement) : null;

                            UIHelpers.DrawImportanceSelector(listing.GetRect(30f), friendlyLabel, ref importance, null, isEnabled, disabledReason);

                            if (isEnabled)
                            {
                                filters.MapFeatures.SetImportance(mutator, importance);
                            }
                        }
                    },
                    filters =>
                    {
                        var resourceMutators = GetResourceMutators();
                        bool hasAny = resourceMutators.Any(m => filters.MapFeatures.GetImportance(m) != FilterImportance.Ignored);
                        bool hasCritical = resourceMutators.Any(m => filters.MapFeatures.GetImportance(m) == FilterImportance.Critical);
                        return (hasAny, hasCritical ? FilterImportance.Critical : FilterImportance.Preferred);
                    }
                ),
                new FilterControl(
                    "Life & Wildlife Modifiers",
                    (listing, filters) =>
                    {
                        listing.Label("Life & Wildlife Modifiers (animal/plant/fish abundance):");

                        var lifeMutators = GetLifeModifierMutators();
                        foreach (var mutator in lifeMutators)
                        {
                            var importance = filters.MapFeatures.GetImportance(mutator);
                            var friendlyLabel = MapFeatureFilter.GetMutatorFriendlyLabel(mutator);

                            // Check DLC requirement
                            string dlcRequirement = MapFeatureFilter.GetMutatorDLCRequirement(mutator);
                            bool isEnabled = string.IsNullOrEmpty(dlcRequirement) || DLCDetectionService.IsDLCAvailable(dlcRequirement);
                            string disabledReason = !isEnabled ? "LandingZone_Filter_DLCRequired".Translate(dlcRequirement) : null;

                            UIHelpers.DrawImportanceSelector(listing.GetRect(30f), friendlyLabel, ref importance, null, isEnabled, disabledReason);

                            if (isEnabled)
                            {
                                filters.MapFeatures.SetImportance(mutator, importance);
                            }
                        }
                    },
                    filters =>
                    {
                        var lifeMutators = GetLifeModifierMutators();
                        bool hasAny = lifeMutators.Any(m => filters.MapFeatures.GetImportance(m) != FilterImportance.Ignored);
                        bool hasCritical = lifeMutators.Any(m => filters.MapFeatures.GetImportance(m) == FilterImportance.Critical);
                        return (hasAny, hasCritical ? FilterImportance.Critical : FilterImportance.Preferred);
                    }
                ),
                new FilterControl(
                    "Natural Stones",
                    (listing, filters) =>
                    {
                        listing.Label("Natural Stones (construction materials):");
                        listing.Gap(4f);

                        // Operator toggle (only show if critical items configured)
                        var naturalStones = GetAllStoneTypes();
                        var hasNaturalCritical = naturalStones.Any(s => filters.Stones.GetImportance(s) == FilterImportance.Critical);

                        if (hasNaturalCritical)
                        {
                            var operatorRect = listing.GetRect(30f);
                            var operatorLabel = ConflictDetector.GetOperatorDescription(filters.Stones.Operator, "natural stones");
                            var criticalCount = naturalStones.Count(s => filters.Stones.GetImportance(s) == FilterImportance.Critical);
                            var operatorTooltip = ConflictDetector.GetOperatorTooltip(filters.Stones.Operator, "natural stones", criticalCount);

                            if (Widgets.ButtonText(operatorRect, operatorLabel))
                            {
                                filters.Stones.Operator = filters.Stones.Operator == ImportanceOperator.OR
                                    ? ImportanceOperator.AND
                                    : ImportanceOperator.OR;
                            }
                            TooltipHandler.TipRegion(operatorRect, operatorTooltip);
                            listing.Gap(4f);
                        }

                        // Stone list with scroll view (fixed height to prevent overflow)
                        const float itemHeight = 30f;
                        const float dividerHeight = 15f;
                        const float scrollViewMaxHeight = 200f;

                        // Separate vanilla and modded for height calculation
                        var vanillaList = naturalStones.Where(s => VanillaStones.Contains(s)).ToList();
                        var moddedList = naturalStones.Where(s => !VanillaStones.Contains(s)).ToList();
                        bool hasDivider = vanillaList.Any() && moddedList.Any();

                        float contentHeight = naturalStones.Count * itemHeight + (hasDivider ? dividerHeight : 0f);
                        float scrollHeight = Mathf.Min(contentHeight, scrollViewMaxHeight);

                        // Show count hint if list is scrollable
                        if (contentHeight > scrollViewMaxHeight)
                        {
                            Text.Font = GameFont.Tiny;
                            GUI.color = new Color(0.7f, 0.7f, 0.7f);
                            listing.Label($"({naturalStones.Count} stone types - scroll to see all)");
                            GUI.color = Color.white;
                            Text.Font = GameFont.Small;
                        }

                        var scrollRect = listing.GetRect(scrollHeight);
                        var viewRect = new Rect(0f, 0f, scrollRect.width - 16f, contentHeight);

                        Widgets.BeginScrollView(scrollRect, ref _stonesScrollPosition, viewRect);
                        var innerListing = new Listing_Standard();
                        innerListing.Begin(viewRect);

                        // Draw vanilla stones first
                        foreach (var stoneType in vanillaList)
                        {
                            var importance = filters.Stones.GetImportance(stoneType);
                            UIHelpers.DrawImportanceSelector(innerListing.GetRect(itemHeight), stoneType, ref importance);
                            filters.Stones.SetImportance(stoneType, importance);
                        }

                        // Draw modded stones (with subtle divider if both exist)
                        if (hasDivider)
                        {
                            var dividerRect = innerListing.GetRect(dividerHeight);
                            Text.Font = GameFont.Tiny;
                            GUI.color = new Color(0.6f, 0.6f, 0.6f);
                            Widgets.Label(dividerRect, "─── Modded Stones ───");
                            GUI.color = Color.white;
                            Text.Font = GameFont.Small;
                        }

                        foreach (var stoneType in moddedList)
                        {
                            var importance = filters.Stones.GetImportance(stoneType);
                            UIHelpers.DrawImportanceSelector(innerListing.GetRect(itemHeight), stoneType, ref importance);
                            filters.Stones.SetImportance(stoneType, importance);
                        }

                        innerListing.End();
                        Widgets.EndScrollView();

                        listing.Gap(5f);
                        bool useStoneCount = filters.UseStoneCount;
                        listing.CheckboxLabeled("Minimum Stone Types (any X types)", ref useStoneCount);
                        filters.UseStoneCount = useStoneCount;
                        if (filters.UseStoneCount)
                        {
                            var range = filters.StoneCountRange;
                            listing.Label($"Required Stone Count: {range.min:F0} to {range.max:F0} types");
                            var rangeRect = listing.GetRect(30f);
                            Widgets.FloatRange(rangeRect, rangeRect.GetHashCode(), ref range, 1f, naturalStones.Count);
                            filters.StoneCountRange = range;
                        }

                        // Live selectivity feedback
                        var stoneImportance = hasNaturalCritical ? FilterImportance.Critical : FilterImportance.Preferred;
                        DrawSelectivityFeedback(listing, "stone", stoneImportance);
                    },
                    filters =>
                    {
                        var naturalStones = GetAllStoneTypes();
                        bool hasAny = naturalStones.Any(s => filters.Stones.GetImportance(s) != FilterImportance.Ignored) || filters.UseStoneCount;
                        bool hasCritical = naturalStones.Any(s => filters.Stones.GetImportance(s) == FilterImportance.Critical);
                        return (hasAny, hasCritical ? FilterImportance.Critical : FilterImportance.Preferred);
                    }
                ),
                new FilterControl(
                    "Mineable Resources",
                    (listing, filters) =>
                    {
                        listing.Label("Mineable Ores (specific ore deposits):");
                        Text.Font = GameFont.Tiny;
                        GUI.color = new Color(0.7f, 0.7f, 0.7f);
                        listing.Label("Distinct from 'MineralRich' mutator - these are specific ore types.");
                        GUI.color = new Color(1f, 0.8f, 0.4f);
                        listing.Label("Note: Tiles have only ONE mineable ore type (Gold OR Silver OR Uranium, etc.)");
                        GUI.color = Color.white;
                        Text.Font = GameFont.Small;
                        listing.Gap(4f);

                        // Mineable resources are always OR-only (tiles can only have one type)
                        var mineables = GetMineableResources();
                        var hasMineableCritical = mineables.Any(m => filters.Stones.GetImportance(m) == FilterImportance.Critical);

                        // Individual mineable resource list (Gold, Plasteel, etc.)
                        foreach (var mineable in mineables)
                        {
                            var importance = filters.Stones.GetImportance(mineable);
                            UIHelpers.DrawImportanceSelector(listing.GetRect(30f), mineable, ref importance);
                            filters.Stones.SetImportance(mineable, importance);
                        }

                        // Live selectivity feedback
                        var mineableImportance = hasMineableCritical ? FilterImportance.Critical : FilterImportance.Preferred;
                        DrawSelectivityFeedback(listing, "mineable", mineableImportance);
                    },
                    filters =>
                    {
                        var mineables = GetMineableResources();
                        bool hasAny = mineables.Any(m => filters.Stones.GetImportance(m) != FilterImportance.Ignored);
                        bool hasCritical = mineables.Any(m => filters.Stones.GetImportance(m) == FilterImportance.Critical);
                        return (hasAny, hasCritical ? FilterImportance.Critical : FilterImportance.Preferred);
                    }
                )
            };

            return new FilterGroup("resources_production", "LandingZone_Filter_ResourcesGroup".Translate(), filters);
        }

        private static FilterGroup GetSpecialFeaturesGroup()
        {
            var filters = new List<FilterControl>
            {
                new FilterControl(
                    "Roads",
                    (listing, filters) =>
                    {
                        listing.Label("LandingZone_Filter_Roads".Translate());

                        // Operator toggle (only show if critical items configured)
                        if (filters.Roads.HasCritical)
                        {
                            var operatorRect = listing.GetRect(30f);
                            var operatorLabel = ConflictDetector.GetOperatorDescription(filters.Roads.Operator, "roads");
                            var operatorTooltip = ConflictDetector.GetOperatorTooltip(filters.Roads.Operator, "roads",
                                filters.Roads.GetCriticalItems().Count());

                            if (Widgets.ButtonText(operatorRect, operatorLabel))
                            {
                                filters.Roads.Operator = filters.Roads.Operator == ImportanceOperator.OR
                                    ? ImportanceOperator.AND
                                    : ImportanceOperator.OR;
                            }
                            TooltipHandler.TipRegion(operatorRect, operatorTooltip);
                            listing.Gap(4f);

                            // Show conflict warnings specific to roads
                            UIHelpers.DrawFilterConflicts(listing, AdvancedModeUI.GetActiveConflicts(), "roads");
                        }

                        // Individual road list
                        var roadTypes = RoadFilter.GetAllRoadTypes().Select(r => r.defName).ToList();
                        foreach (var roadType in roadTypes)
                        {
                            var importance = filters.Roads.GetImportance(roadType);
                            UIHelpers.DrawImportanceSelector(listing.GetRect(30f), roadType, ref importance);
                            filters.Roads.SetImportance(roadType, importance);
                        }
                    },
                    filters => (filters.Roads.HasAnyImportance, filters.Roads.HasCritical ? FilterImportance.Critical : FilterImportance.Preferred)
                ),
                new FilterControl(
                    "Special Sites",
                    (listing, filters) =>
                    {
                        listing.Label("Special Sites (ancient ruins, exotic features):");

                        var specialSites = GetSpecialSiteMutators();
                        foreach (var mutator in specialSites)
                        {
                            var importance = filters.MapFeatures.GetImportance(mutator);
                            var friendlyLabel = MapFeatureFilter.GetMutatorFriendlyLabel(mutator);

                            // Check DLC requirement
                            string dlcRequirement = MapFeatureFilter.GetMutatorDLCRequirement(mutator);
                            bool isEnabled = string.IsNullOrEmpty(dlcRequirement) || DLCDetectionService.IsDLCAvailable(dlcRequirement);
                            string disabledReason = !isEnabled ? "LandingZone_Filter_DLCRequired".Translate(dlcRequirement) : null;

                            UIHelpers.DrawImportanceSelector(listing.GetRect(30f), friendlyLabel, ref importance, null, isEnabled, disabledReason);

                            if (isEnabled)
                            {
                                filters.MapFeatures.SetImportance(mutator, importance);
                            }
                        }
                    },
                    filters =>
                    {
                        var specialSites = GetSpecialSiteMutators();
                        bool hasAny = specialSites.Any(m => filters.MapFeatures.GetImportance(m) != FilterImportance.Ignored);
                        bool hasCritical = specialSites.Any(m => filters.MapFeatures.GetImportance(m) == FilterImportance.Critical);
                        return (hasAny, hasCritical ? FilterImportance.Critical : FilterImportance.Preferred);
                    }
                ),
                new FilterControl(
                    "Stockpiles",
                    (listing, filters) =>
                    {
                        listing.Label("Stockpiles (abandoned supply caches):");

                        // Operator toggle (only show if critical items configured)
                        if (filters.Stockpiles.HasCritical)
                        {
                            var operatorRect = listing.GetRect(30f);
                            var operatorLabel = ConflictDetector.GetOperatorDescription(filters.Stockpiles.Operator, "stockpiles");
                            var operatorTooltip = ConflictDetector.GetOperatorTooltip(filters.Stockpiles.Operator, "stockpiles",
                                filters.Stockpiles.GetCriticalItems().Count());

                            if (Widgets.ButtonText(operatorRect, operatorLabel))
                            {
                                filters.Stockpiles.Operator = filters.Stockpiles.Operator == ImportanceOperator.OR
                                    ? ImportanceOperator.AND
                                    : ImportanceOperator.OR;
                            }
                            TooltipHandler.TipRegion(operatorRect, operatorTooltip);
                            listing.Gap(4f);
                        }

                        // Known stockpile types from StockpileFilter.GetStockpileQuality
                        var stockpileTypes = new List<(string defName, string label, string dlc)>
                        {
                            ("Gravcore", "Compacted Gravcore", "Anomaly"),
                            ("Weapons", "Weapons Cache", null),
                            ("Medicine", "Medical Supplies", null),
                            ("Chemfuel", "Chemfuel Stockpile", null),
                            ("Component", "Components & Parts", null),
                            ("Drugs", "Drug Stockpile", null)
                        };

                        foreach (var (defName, label, dlc) in stockpileTypes)
                        {
                            var importance = filters.Stockpiles.GetImportance(defName);

                            // Check DLC requirement
                            bool isEnabled = string.IsNullOrEmpty(dlc) || DLCDetectionService.IsDLCAvailable(dlc);
                            string disabledReason = !isEnabled ? "LandingZone_Filter_DLCRequired".Translate(dlc) : null;
                            string displayLabel = isEnabled ? label : $"{label} (DLC Required)";

                            UIHelpers.DrawImportanceSelector(listing.GetRect(30f), displayLabel, ref importance, null, isEnabled, disabledReason);

                            if (isEnabled)
                            {
                                filters.Stockpiles.SetImportance(defName, importance);
                            }
                        }

                        // Live selectivity feedback
                        var stockpileImportance = filters.Stockpiles.HasCritical ? FilterImportance.Critical : FilterImportance.Preferred;
                        DrawSelectivityFeedback(listing, "stockpile", stockpileImportance);
                    },
                    filters => (filters.Stockpiles.HasAnyImportance,
                               filters.Stockpiles.HasCritical ? FilterImportance.Critical : FilterImportance.Preferred)
                ),
                ImportanceOnlyControl(
                    "Landmarks",
                    f => f.LandmarkImportance,
                    (f, v) => f.LandmarkImportance = v
                )
            };

            return new FilterGroup("special_features", "LandingZone_Filter_ConnectionsGroup".Translate(), filters);
        }

        private static FilterGroup GetBiomeControlGroup()
        {
            var filters = new List<FilterControl>
            {
                new FilterControl(
                    "Locked Biome",
                    (listing, filters) =>
                    {
                        listing.Label("Locked Biome (restrict to single biome):");
                        var biomes = GetAllBiomes();
                        var currentBiome = filters.LockedBiome;
                        var biomeLabels = new List<string> { "(None)" };
                        biomeLabels.AddRange(biomes.Select(b => b.label));

                        int currentIndex = currentBiome == null ? 0 : biomes.IndexOf(currentBiome) + 1;
                        var buttonRect = listing.GetRect(30f);

                        if (Widgets.ButtonText(buttonRect, biomeLabels[currentIndex]))
                        {
                            var options = new List<FloatMenuOption>();
                            for (int i = 0; i < biomeLabels.Count; i++)
                            {
                                int index = i;
                                options.Add(new FloatMenuOption(biomeLabels[index], () =>
                                {
                                    filters.LockedBiome = index == 0 ? null : biomes[index - 1];
                                }));
                            }
                            Find.WindowStack.Add(new FloatMenu(options));
                        }
                    },
                    filters => (filters.LockedBiome != null, FilterImportance.Critical)
                ),
                new FilterControl(
                    "Adjacent Biomes",
                    (listing, filters) =>
                    {
                        listing.Label("Adjacent Biomes:");
                        Text.Font = GameFont.Tiny;
                        GUI.color = new Color(0.7f, 0.7f, 0.7f);
                        listing.Label("(Affects weather patterns from neighboring tiles)");
                        GUI.color = Color.white;
                        Text.Font = GameFont.Small;
                        listing.Gap(4f);

                        // Operator toggle (only show if critical items configured)
                        if (filters.AdjacentBiomes.HasCritical)
                        {
                            var operatorRect = listing.GetRect(30f);
                            var operatorLabel = filters.AdjacentBiomes.Operator == ImportanceOperator.OR
                                ? "Match: ANY of the selected biomes"
                                : "Match: ALL of the selected biomes";

                            if (Widgets.ButtonText(operatorRect, operatorLabel))
                            {
                                filters.AdjacentBiomes.Operator = filters.AdjacentBiomes.Operator == ImportanceOperator.OR
                                    ? ImportanceOperator.AND
                                    : ImportanceOperator.OR;
                            }
                            listing.Gap(4f);
                        }

                        // Individual biome list
                        var biomes = GetAllBiomes();
                        foreach (var biome in biomes)
                        {
                            var importance = filters.AdjacentBiomes.GetImportance(biome.defName);
                            UIHelpers.DrawImportanceSelector(listing.GetRect(30f), biome.label, ref importance);
                            filters.AdjacentBiomes.SetImportance(biome.defName, importance);
                        }
                    },
                    filters => (filters.AdjacentBiomes.HasAnyImportance,
                               filters.AdjacentBiomes.HasCritical ? FilterImportance.Critical : FilterImportance.Preferred)
                )
            };

            return new FilterGroup("biome_control", "Biome Control", filters);
        }

        private static FilterGroup GetResultsControlGroup()
        {
            var filters = new List<FilterControl>
            {
                new FilterControl(
                    "Result Limit",
                    (listing, filters) =>
                    {
                        listing.Label($"Result Limit: {filters.MaxResults}");
                        var sliderRect = listing.GetRect(30f);
                        int resultCount = (int)Widgets.HorizontalSlider(
                            sliderRect,
                            filters.MaxResults,
                            FilterSettings.MinMaxResults,
                            FilterSettings.MaxResultsLimit,
                            true,
                            $"{filters.MaxResults} results",
                            $"{FilterSettings.MinMaxResults}",
                            $"{FilterSettings.MaxResultsLimit}"
                        );
                        filters.MaxResults = resultCount;

                        Text.Font = GameFont.Tiny;
                        GUI.color = new Color(0.7f, 0.7f, 0.7f);
                        listing.Label("Number of top-scored tiles to return from search");
                        GUI.color = Color.white;
                        Text.Font = GameFont.Small;
                    },
                    filters => (true, FilterImportance.Ignored) // Always active, not a filter
                ),
                new FilterControl(
                    "Critical Filtering",
                    (listing, filters) =>
                    {
                        Text.Font = GameFont.Small;
                        listing.Label("Critical Filtering: Adaptive (automatic)");

                        Text.Font = GameFont.Tiny;
                        GUI.color = new Color(0.7f, 0.85f, 0.7f);
                        listing.Label("Stage A uses intelligent k-of-n fallback to keep candidate count reasonable.");
                        listing.Label("Example: 6 criticals → tries 6/6, 5/6, 4/6... until 1,000-10,000 candidates found.");
                        GUI.color = Color.white;
                        Text.Font = GameFont.Small;
                        listing.Gap(4f);
                    },
                    filters => (true, FilterImportance.Ignored) // Always show
                ),
                new FilterControl(
                    "Fallback Tiers",
                    (listing, filters) =>
                    {
                        DrawFallbackTierManager(listing, filters);
                    },
                    filters => (false, FilterImportance.Ignored)
                )
            };

            return new FilterGroup("results_control", "Results Control", filters);
        }

        // ============================================================================
        // DATA TYPE GROUPS (alternative organization)
        // ============================================================================

        // GetDataTypeGroups removed - no longer using Data Type organization toggle

        // ============================================================================
        // HELPER UI METHODS
        // ============================================================================

        private static void DrawHillinessToggles(Rect rect, FilterSettings filters)
        {
            var hillinessValues = new[] { Hilliness.Flat, Hilliness.SmallHills, Hilliness.LargeHills, Hilliness.Mountainous };
            var labels = new[] { "Flat", "Small", "Large", "Mountain" };

            // 5 buttons: All + 4 individual types
            float buttonWidth = (rect.width - 15f) / 5f;
            float x = rect.x;

            // "All" button (toggles between all types and default subset)
            Rect allButtonRect = new Rect(x, rect.y, buttonWidth, rect.height);
            bool allSelected = filters.AllowedHilliness.Count == 4;

            // Highlight "All" when all 4 types are allowed (no restriction)
            var prevColor = GUI.color;
            if (allSelected)
            {
                GUI.color = new Color(0.8f, 1f, 0.8f); // Light green = no restriction
            }

            bool allClicked = Widgets.ButtonText(allButtonRect, "LandingZone_All".Translate());
            GUI.color = prevColor;

            if (allClicked)
            {
                if (allSelected)
                {
                    // Toggle off: Reset to default subset (Small/Large/Mountain - most common)
                    filters.AllowedHilliness.Clear();
                    filters.AllowedHilliness.Add(Hilliness.SmallHills);
                    filters.AllowedHilliness.Add(Hilliness.LargeHills);
                    filters.AllowedHilliness.Add(Hilliness.Mountainous);
                }
                else
                {
                    // Toggle on: Select all 4 types (removes restriction)
                    filters.AllowedHilliness.Clear();
                    filters.AllowedHilliness.Add(Hilliness.Flat);
                    filters.AllowedHilliness.Add(Hilliness.SmallHills);
                    filters.AllowedHilliness.Add(Hilliness.LargeHills);
                    filters.AllowedHilliness.Add(Hilliness.Mountainous);
                }
            }

            x += buttonWidth + 3f;

            // Individual hilliness type buttons
            for (int i = 0; i < hillinessValues.Length; i++)
            {
                var hilliness = hillinessValues[i];
                Rect buttonRect = new Rect(x, rect.y, buttonWidth, rect.height);

                bool isSelected = filters.AllowedHilliness.Contains(hilliness);

                // Visual feedback: highlight selected buttons
                if (isSelected)
                {
                    GUI.color = new Color(0.8f, 1f, 0.8f); // Light green tint
                }

                bool clicked = Widgets.ButtonText(buttonRect, labels[i], active: isSelected);
                GUI.color = prevColor;

                if (clicked)
                {
                    if (isSelected)
                        filters.AllowedHilliness.Remove(hilliness);
                    else
                        filters.AllowedHilliness.Add(hilliness);
                }

                x += buttonWidth + 3f;
            }
        }

        // ============================================================================
        // CANONICAL DATA GETTERS (SSoT)
        // ============================================================================

        private static List<string> GetAllStoneTypes()
        {
            // Get ONLY natural construction stones (Granite, Marble, Sandstone, Limestone, Slate, etc.)
            // Exclude mineable ores which are handled separately in GetMineableResources()
            var stoneTypes = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def.building != null &&
                             def.building.isNaturalRock &&
                             !def.defName.StartsWith("Mineable"))  // Exclude mineables
                .Select(def => def.defName)
                .ToList();

            // Fallback: if no rocks found, return common known types
            if (stoneTypes.Count == 0)
            {
                stoneTypes = new List<string> { "Granite", "Limestone", "Marble", "Sandstone", "Slate" };
            }

            // Sort: Vanilla stones first (alphabetically), then modded stones (alphabetically)
            return stoneTypes
                .OrderBy(name => VanillaStones.Contains(name) ? 0 : 1)
                .ThenBy(name => name)
                .ToList();
        }

        private static List<string> GetMineableResources()
        {
            // Get all mineable resources (gold, silver, plasteel, uranium, etc.)
            // These are rare ores, not common construction stones
            var mineableResources = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def.defName.StartsWith("Mineable") && !def.building?.isNaturalRock == true)
                .Select(def => def.defName)
                .OrderBy(name => name)
                .ToList();

            // Fallback: if no mineables found, return common known types
            if (mineableResources.Count == 0)
            {
                mineableResources = new List<string>
                {
                    "MineableGold", "MineableSilver", "MineableSteel",
                    "MineablePlasteel", "MineableUranium", "MineableJade",
                    "MineableComponentsIndustrial"
                };
            }

            return mineableResources;
        }

        private static List<BiomeDef> GetAllBiomes()
        {
            // Get only starting-location-compatible biomes (filter out space, orbit, labyrinth, etc.)
            // Non-starting biomes: space, orbit, labyrinth, metal hell, lava field
            var nonStartingBiomes = new HashSet<string>
            {
                "BiomeSpace", "Space", // Space biomes
                "BiomeOrbit", "Orbit", // Orbital biomes
                "Labyrinth", // Labyrinth (Anomaly DLC)
                "MetalHell", "MechanoidBase", // Metal hell / mechanoid bases
                "LavaField", // Lava field biomes
            };

            return DefDatabase<BiomeDef>.AllDefsListForReading
                .Where(b => !b.impassable && !nonStartingBiomes.Contains(b.defName))
                .OrderBy(b => b.label)
                .ToList();
        }

        // ============================================================================
        // MUTATOR CATEGORIZATION (83 total mutators organized by user intent)
        // ============================================================================

        /// <summary>
        /// Weather mutators only (for Climate & Weather tab).
        /// </summary>
        private static List<string> GetWeatherMutators()
        {
            return new List<string>
            {
                "SunnyMutator",
                "FoggyMutator",
                "WindyMutator",
                "WetClimate",
                "Pollution_Increased"
            };
        }

        /// <summary>
        /// Life/resource availability modifiers (for Resources & Production tab).
        /// </summary>
        private static List<string> GetLifeModifierMutators()
        {
            return new List<string>
            {
                "AnimalLife_Increased",
                "AnimalLife_Decreased",
                "PlantLife_Increased",
                "PlantLife_Decreased",
                "Fish_Increased",
                "Fish_Decreased"
            };
        }

        /// <summary>
        /// DEPRECATED: Use GetWeatherMutators() + GetLifeModifierMutators() instead.
        /// Kept for backward compatibility.
        /// </summary>
        private static List<string> GetClimateMutators()
        {
            var all = new List<string>();
            all.AddRange(GetWeatherMutators());
            all.AddRange(GetLifeModifierMutators());
            return all;
        }

        private static List<string> GetGeographyMutators()
        {
            return new List<string>
            {
                // Water features
                "River", "RiverDelta", "RiverConfluence", "RiverIsland", "Headwater",
                "Lake", "LakeWithIsland", "LakeWithIslands", "Lakeshore", "Pond", "CaveLakes", "ToxicLake",
                "LavaLake",  // Volcanic lake

                // Coastal/maritime
                "Coast", "Peninsula", "Bay", "Cove", "Harbor", "Fjord",
                "Archipelago", "CoastalAtoll", "CoastalIsland", "Iceberg",

                // Geological Landforms mod (GL_*)
                "GL_Atoll", "GL_DesertPlateau", "GL_Glacier", "GL_Gorge", "GL_IceOasis", "GL_Island",
                "GL_RiverConfluence", "GL_RiverDelta", "GL_RiverIsland", "GL_SecludedValley", "GL_Sinkhole", "GL_Skerry",

                // Mountain/elevation
                "Mountain", "Valley", "Basin", "Plateau", "Hollow",
                "Caves", "Cavern", "LavaCaves", "IceCaves", "Cliffs", "Chasm", "Crevasse",

                // Desert/arid
                "Oasis", "Dunes", "IceDunes",

                // Volcanic/lava
                "LavaFlow", "LavaCrater", "HotSprings",

                // Alpha Biomes mod (AB_*) - geography features
                "AB_GeothermalHotspots", "AB_HealingSprings", "AB_MagmaticQuagmire", "AB_PetalStorms",
                "AB_PropaneLakes", "AB_QuiveringSurface", "AB_TarLakes",

                // Terrain types
                "DryGround", "DryLake", "Muddy", "Sandy", "Marshy", "Wetland",

                // Biome blending
                "MixedBiome"
            };
        }

        private static List<string> GetResourceMutators()
        {
            return new List<string>
            {
                "Fertile",
                "MineralRich",
                "SteamGeysers_Increased",
                "WildPlants",
                "WildTropicalPlants",
                "PlantGrove",
                "AnimalHabitat",
                "ObsidianDeposits",  // Volcanic glass - valuable construction material

                // Alpha Biomes mod (AB_*) - resource modifiers
                "AB_OversaturatedSoil",  // Enhanced fertility
                "AB_PollinationFrenzy"   // Plant growth boost
            };
        }

        private static List<string> GetSpecialSiteMutators()
        {
            // Alphabetically sorted for easy scanning
            return new List<string>
            {
                // Alpha Biomes mod (AB_*) - special sites
                "AB_DerelictResort",

                // Vanilla/Core special sites
                "AbandonedColonyOutlander",
                "AbandonedColonyTribal",
                "AncientChemfuelRefinery",
                "AncientGarrison",
                "AncientHeatVent",
                "AncientInfestedSettlement",
                "AncientLaunchSite",
                "AncientQuarry",
                "AncientRuins",
                "AncientRuins_Frozen",
                "AncientSmokeVent",
                "AncientToxVent",
                "AncientUplink",
                "AncientWarehouse",
                "ArcheanTrees",
                "InsectMegahive",
                "Junkyard",
                "Stockpile",
                "TerraformingScar"
            };
        }

        /// <summary>
        /// Diagnostics helper: returns the curated mutator set used by the Advanced UI.
        /// This matches the buckets shown to users (weather, life, geography, resources, special).
        /// </summary>
        public static IEnumerable<string> GetCuratedMutatorsForDiagnostics()
        {
            var all = new HashSet<string>();
            all.UnionWith(GetWeatherMutators());
            all.UnionWith(GetLifeModifierMutators());
            all.UnionWith(GetGeographyMutators());
            all.UnionWith(GetResourceMutators());
            all.UnionWith(GetSpecialSiteMutators());
            return all;
        }

        // ============================================================================
        // LIVE SELECTIVITY FEEDBACK
        // ============================================================================

        /// <summary>
        /// Draws live selectivity feedback for a filter (Tier 3 feature).
        /// Shows "~45% of tiles (127k/295k)" below the filter control.
        /// Only renders if filter is not ignored and estimator is available.
        /// Uses fast heuristic estimation for instant feedback.
        /// </summary>
        private static void DrawSelectivityFeedback(Listing_Standard listing, string filterId, FilterImportance importance)
        {
            // Skip if ignored - no point showing feedback
            if (importance == FilterImportance.Ignored)
                return;

            // Skip if context not ready
            if (LandingZoneContext.State == null)
                return;

            try
            {
                var estimator = LandingZoneContext.SelectivityEstimator;
                var filters = LandingZoneContext.State.Preferences.GetActiveFilters();

                Filtering.SelectivityEstimate estimate;

                // Map filterId to appropriate estimator method
                switch (filterId)
                {
                    case "average_temperature":
                        estimate = estimator.EstimateTemperatureRange(filters.AverageTemperatureRange, importance);
                        break;

                    case "minimum_temperature":
                        estimate = estimator.EstimateTemperatureRange(filters.MinimumTemperatureRange, importance);
                        break;

                    case "maximum_temperature":
                        estimate = estimator.EstimateTemperatureRange(filters.MaximumTemperatureRange, importance);
                        break;

                    case "rainfall":
                        estimate = estimator.EstimateRainfallRange(filters.RainfallRange, importance);
                        break;

                    case "growing_days":
                        estimate = estimator.EstimateGrowingDaysRange(filters.GrowingDaysRange, importance);
                        break;

                    case "coastal":
                        estimate = estimator.EstimateCoastal(importance);
                        break;

                    case "coastal_lake":
                        // For lake coastal, use similar estimate as ocean coastal (slightly lower)
                        estimate = estimator.EstimateCoastal(importance);
                        break;

                    case "water_access":
                        estimate = estimator.EstimateWaterAccess(importance);
                        break;

                    case "forageable_food":
                        // Approximate forageability as common feature (~50% of tiles)
                        estimate = new Filtering.SelectivityEstimate(
                            (int)(estimator.GetSettleableTiles() * 0.5f),
                            estimator.GetSettleableTiles(),
                            importance,
                            false
                        );
                        break;

                    case "graze":
                        // Grazing available on ~60% of tiles (excluding deserts/ice)
                        estimate = new Filtering.SelectivityEstimate(
                            (int)(estimator.GetSettleableTiles() * 0.6f),
                            estimator.GetSettleableTiles(),
                            importance,
                            false
                        );
                        break;

                    default:
                        // Unknown filter - skip feedback
                        return;
                }

                // Format and display
                string matchText = $"  ⟳ {estimate.FormatForDisplay()}";

                // Render in small gray text
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.65f, 0.65f, 0.65f);
                listing.Label(matchText);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                listing.Gap(3f);
            }
            catch (System.Exception ex)
            {
                // Silently fail - don't break UI if estimation fails
                Log.Warning($"[LandingZone] Failed to estimate selectivity for {filterId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Draws fallback tier suggestions for relaxing strictness when searches are too restrictive.
        /// Shows current strictness estimate and alternative tiers with click-to-apply buttons.
        /// </summary>
        private static void DrawFallbackTierManager(Listing_Standard listing, FilterSettings filters)
        {
            // Skip if context not ready
            if (LandingZoneContext.Filters == null || LandingZoneContext.State == null)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                listing.Label("LandingZone_FallbackTierManager_NoWorld".Translate());
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                return;
            }

            try
            {
                // Get all critical filter selectivities
                var allSelectivities = LandingZoneContext.Filters.GetAllSelectivities(LandingZoneContext.State);
                var criticalSelectivities = allSelectivities
                    .Where(s => s.Importance == FilterImportance.Critical)
                    .ToList();

                // No criticals? No need for fallback tiers
                if (criticalSelectivities.Count == 0)
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = new Color(0.6f, 0.6f, 0.6f);
                    listing.Label("LandingZone_FallbackTierManager_NoCriticals".Translate());
                    GUI.color = Color.white;
                    Text.Font = GameFont.Small;
                    return;
                }

                // Get current strictness estimate
                var currentLikelihood = filters.CriticalStrictness >= 1.0f
                    ? Filtering.MatchLikelihoodEstimator.EstimateAllCriticals(criticalSelectivities)
                    : Filtering.MatchLikelihoodEstimator.EstimateRelaxedCriticals(criticalSelectivities, filters.CriticalStrictness);

                // Header
                Text.Font = GameFont.Small;
                listing.Label("LandingZone_FallbackTierManager_Title".Translate(criticalSelectivities.Count));
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                listing.Label("LandingZone_FallbackTierManager_Instructions".Translate());
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                listing.Gap(8f);

                // Current strictness indicator
                var currentRect = listing.GetRect(40f);
                Color currentBgColor = currentLikelihood.Category switch
                {
                    Filtering.LikelihoodCategory.Guaranteed => new Color(0.2f, 0.3f, 0.2f),
                    Filtering.LikelihoodCategory.VeryHigh => new Color(0.2f, 0.3f, 0.2f),
                    Filtering.LikelihoodCategory.High => new Color(0.2f, 0.25f, 0.2f),
                    Filtering.LikelihoodCategory.Medium => new Color(0.25f, 0.25f, 0.15f),
                    Filtering.LikelihoodCategory.Low => new Color(0.3f, 0.2f, 0.15f),
                    _ => new Color(0.3f, 0.15f, 0.15f)
                };

                Widgets.DrawBoxSolid(currentRect, currentBgColor);
                Widgets.DrawBox(currentRect);

                var contentRect = currentRect.ContractedBy(4f);
                Text.Font = GameFont.Tiny;
                Widgets.Label(
                    new Rect(contentRect.x, contentRect.y, contentRect.width, 14f),
                    "LandingZone_FallbackTierManager_CurrentLabel".Translate()
                );
                Text.Font = GameFont.Small;
                Widgets.Label(
                    new Rect(contentRect.x, contentRect.y + 16f, contentRect.width, 18f),
                    currentLikelihood.GetUserMessage()
                );
                Text.Font = GameFont.Small;
                listing.Gap(8f);

                // Get fallback suggestions
                var suggestions = Filtering.MatchLikelihoodEstimator.SuggestStrictness(criticalSelectivities);

                // Only show suggestions different from current strictness
                var relevantSuggestions = suggestions
                    .Where(s => Math.Abs(s.Strictness - filters.CriticalStrictness) > 0.01f)
                    .Take(3) // Show max 3 alternatives
                    .ToList();

                if (relevantSuggestions.Any())
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = new Color(0.7f, 0.7f, 0.7f);
                    listing.Label("LandingZone_FallbackTierManager_ClickToApply".Translate());
                    GUI.color = Color.white;
                    Text.Font = GameFont.Small;
                    listing.Gap(4f);

                    foreach (var suggestion in relevantSuggestions)
                    {
                        var suggestionRect = listing.GetRect(36f);
                        Color bgColor = suggestion.Category switch
                        {
                            Filtering.LikelihoodCategory.Guaranteed => new Color(0.15f, 0.25f, 0.15f),
                            Filtering.LikelihoodCategory.VeryHigh => new Color(0.15f, 0.25f, 0.15f),
                            Filtering.LikelihoodCategory.High => new Color(0.15f, 0.2f, 0.15f),
                            Filtering.LikelihoodCategory.Medium => new Color(0.2f, 0.2f, 0.1f),
                            Filtering.LikelihoodCategory.Low => new Color(0.25f, 0.15f, 0.1f),
                            _ => new Color(0.25f, 0.1f, 0.1f)
                        };

                        Widgets.DrawBoxSolid(suggestionRect, bgColor);
                        Widgets.DrawBox(suggestionRect);

                        var suggestionContent = suggestionRect.ContractedBy(4f);
                        Text.Font = GameFont.Small;
                        Widgets.Label(
                            new Rect(suggestionContent.x, suggestionContent.y, suggestionContent.width, 18f),
                            suggestion.GetDisplayText()
                        );
                        Text.Font = GameFont.Tiny;
                        GUI.color = new Color(0.7f, 0.7f, 0.7f);
                        Widgets.Label(
                            new Rect(suggestionContent.x, suggestionContent.y + 18f, suggestionContent.width, 14f),
                            "LandingZone_FallbackTierManager_Strictness".Translate(suggestion.Strictness.ToStringPercent())
                        );
                        GUI.color = Color.white;
                        Text.Font = GameFont.Small;

                        if (Widgets.ButtonInvisible(suggestionRect))
                        {
                            filters.CriticalStrictness = suggestion.Strictness;
                            Messages.Message(
                                "LandingZone_FallbackTierManager_Applied".Translate(suggestion.Description, suggestion.Strictness.ToStringPercent()),
                                MessageTypeDefOf.NeutralEvent,
                                false
                            );
                        }

                        TooltipHandler.TipRegion(suggestionRect, "LandingZone_FallbackTierManager_Tooltip".Translate(suggestion.Description, suggestion.Strictness.ToStringPercent()));
                        listing.Gap(4f);
                    }
                }
                else
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = new Color(0.6f, 0.6f, 0.6f);
                    listing.Label("LandingZone_FallbackTierManager_NoAlternatives".Translate());
                    GUI.color = Color.white;
                    Text.Font = GameFont.Small;
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[LandingZone] Failed to draw fallback tier manager: {ex.Message}");
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.3f, 0.3f);
                listing.Label("LandingZone_FallbackTierManager_Error".Translate(ex.Message));
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }
        }
    }
}

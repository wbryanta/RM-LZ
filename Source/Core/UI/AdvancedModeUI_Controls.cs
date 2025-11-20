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
                    "Temperature (Average)",
                    f => f.AverageTemperatureRange,
                    (f, v) => f.AverageTemperatureRange = v,
                    f => f.AverageTemperatureImportance,
                    (f, v) => f.AverageTemperatureImportance = v,
                    -60f, 60f, "°C",
                    "average_temperature"
                ),
                FloatRangeControl(
                    "Temperature (Minimum)",
                    f => f.MinimumTemperatureRange,
                    (f, v) => f.MinimumTemperatureRange = v,
                    f => f.MinimumTemperatureImportance,
                    (f, v) => f.MinimumTemperatureImportance = v,
                    -60f, 40f, "°C",
                    "minimum_temperature"
                ),
                FloatRangeControl(
                    "Temperature (Maximum)",
                    f => f.MaximumTemperatureRange,
                    (f, v) => f.MaximumTemperatureRange = v,
                    f => f.MaximumTemperatureImportance,
                    (f, v) => f.MaximumTemperatureImportance = v,
                    0f, 60f, "°C",
                    "maximum_temperature"
                ),
                FloatRangeControl(
                    "Rainfall",
                    f => f.RainfallRange,
                    (f, v) => f.RainfallRange = v,
                    f => f.RainfallImportance,
                    (f, v) => f.RainfallImportance = v,
                    0f, 5300f, " mm/year",
                    "rainfall"
                ),
                FloatRangeControl(
                    "Growing Days",
                    f => f.GrowingDaysRange,
                    (f, v) => f.GrowingDaysRange = v,
                    f => f.GrowingDaysImportance,
                    (f, v) => f.GrowingDaysImportance = v,
                    0f, 60f, " days/year"
                    // No filter ID - growing days computed from TileDataCache, not a direct filter
                ),
                FloatRangeControl(
                    "Pollution",
                    f => f.PollutionRange,
                    (f, v) => f.PollutionRange = v,
                    f => f.PollutionImportance,
                    (f, v) => f.PollutionImportance = v,
                    0f, 1f
                    // No filter ID - pollution may not have dedicated predicate
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

            return new FilterGroup("climate_comfort", "Climate Comfort", filters);
        }

        private static FilterGroup GetTerrainAccessGroup()
        {
            var filters = new List<FilterControl>
            {
                new FilterControl(
                    "Hilliness",
                    (listing, filters) =>
                    {
                        listing.Label("Hilliness (select allowed types):");
                        var rect = listing.GetRect(30f);
                        DrawHillinessToggles(rect, filters);
                    },
                    filters => (filters.AllowedHilliness.Count < 4, FilterImportance.Critical)
                ),
                ImportanceOnlyControl(
                    "Coastal (Ocean)",
                    f => f.CoastalImportance,
                    (f, v) => f.CoastalImportance = v,
                    "coastal"
                ),
                ImportanceOnlyControl(
                    "Coastal (Lake)",
                    f => f.CoastalLakeImportance,
                    (f, v) => f.CoastalLakeImportance = v,
                    "coastal_lake"
                ),
                FloatRangeControl(
                    "Elevation",
                    f => f.ElevationRange,
                    (f, v) => f.ElevationRange = v,
                    f => f.ElevationImportance,
                    (f, v) => f.ElevationImportance = v,
                    0f, 3100f, " m"
                ),
                FloatRangeControl(
                    "Movement Difficulty",
                    f => f.MovementDifficultyRange,
                    (f, v) => f.MovementDifficultyRange = v,
                    f => f.MovementDifficultyImportance,
                    (f, v) => f.MovementDifficultyImportance = v,
                    0f, 2f
                ),
                FloatRangeControl(
                    "Swampiness",
                    f => f.SwampinessRange,
                    (f, v) => f.SwampinessRange = v,
                    f => f.SwampinessImportance,
                    (f, v) => f.SwampinessImportance = v,
                    0f, 1.2f
                )
            };

            return new FilterGroup("terrain_access", "Geography", filters);
        }

        private static FilterGroup GetResourcesProductionGroup()
        {
            var filters = new List<FilterControl>
            {
                FloatRangeControl(
                    "Forageability",
                    f => f.ForageabilityRange,
                    (f, v) => f.ForageabilityRange = v,
                    f => f.ForageImportance,
                    (f, v) => f.ForageImportance = v,
                    0f, 1f,
                    "",
                    "forageable_food"
                ),
                ImportanceOnlyControl(
                    "Animals Can Graze",
                    f => f.GrazeImportance,
                    (f, v) => f.GrazeImportance = v,
                    "graze"
                ),
                FloatRangeControl(
                    "Animal Density",
                    f => f.AnimalDensityRange,
                    (f, v) => f.AnimalDensityRange = v,
                    f => f.AnimalDensityImportance,
                    (f, v) => f.AnimalDensityImportance = v,
                    0f, 6.5f
                ),
                FloatRangeControl(
                    "Fish Population",
                    f => f.FishPopulationRange,
                    (f, v) => f.FishPopulationRange = v,
                    f => f.FishPopulationImportance,
                    (f, v) => f.FishPopulationImportance = v,
                    0f, 900f
                ),
                FloatRangeControl(
                    "Plant Density",
                    f => f.PlantDensityRange,
                    (f, v) => f.PlantDensityRange = v,
                    f => f.PlantDensityImportance,
                    (f, v) => f.PlantDensityImportance = v,
                    0f, 1.3f
                )
            };

            return new FilterGroup("resources_production", "Resources & Production", filters);
        }

        private static FilterGroup GetSpecialFeaturesGroup()
        {
            var filters = new List<FilterControl>
            {
                new FilterControl(
                    "Rivers",
                    (listing, filters) =>
                    {
                        listing.Label("Rivers:");

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
                ),
                new FilterControl(
                    "Roads",
                    (listing, filters) =>
                    {
                        listing.Label("Roads:");

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
                    "Stones",
                    (listing, filters) =>
                    {
                        listing.Label("Stones (individual selection):");

                        // Operator toggle (only show if critical items configured)
                        if (filters.Stones.HasCritical)
                        {
                            var operatorRect = listing.GetRect(30f);
                            var operatorLabel = ConflictDetector.GetOperatorDescription(filters.Stones.Operator, "stones");
                            var operatorTooltip = ConflictDetector.GetOperatorTooltip(filters.Stones.Operator, "stones",
                                filters.Stones.GetCriticalItems().Count());

                            if (Widgets.ButtonText(operatorRect, operatorLabel))
                            {
                                filters.Stones.Operator = filters.Stones.Operator == ImportanceOperator.OR
                                    ? ImportanceOperator.AND
                                    : ImportanceOperator.OR;
                            }
                            TooltipHandler.TipRegion(operatorRect, operatorTooltip);
                            listing.Gap(4f);

                            // Show conflict warnings specific to stones
                            UIHelpers.DrawFilterConflicts(listing, AdvancedModeUI.GetActiveConflicts(), "stones");
                        }

                        // Individual stone list
                        var stoneTypes = GetAllStoneTypes();
                        foreach (var stoneType in stoneTypes)
                        {
                            var importance = filters.Stones.GetImportance(stoneType);
                            UIHelpers.DrawImportanceSelector(listing.GetRect(30f), stoneType, ref importance);
                            filters.Stones.SetImportance(stoneType, importance);
                        }

                        listing.Gap(5f);
                        bool useStoneCount = filters.UseStoneCount;
                        listing.CheckboxLabeled("Minimum Stone Types (any X types)", ref useStoneCount);
                        filters.UseStoneCount = useStoneCount;
                        if (filters.UseStoneCount)
                        {
                            var range = filters.StoneCountRange;
                            listing.Label($"Required Stone Count: {range.min:F0} to {range.max:F0} types");
                            var rangeRect = listing.GetRect(30f);
                            Widgets.FloatRange(rangeRect, rangeRect.GetHashCode(), ref range, 1f, stoneTypes.Count);
                            filters.StoneCountRange = range;
                        }

                        // Live selectivity feedback (Tier 3)
                        var stoneImportance = filters.Stones.HasCritical ? FilterImportance.Critical : FilterImportance.Preferred;
                        DrawSelectivityFeedback(listing, "stone", stoneImportance);
                    },
                    filters => (filters.Stones.HasAnyImportance || filters.UseStoneCount,
                               filters.Stones.HasCritical ? FilterImportance.Critical : FilterImportance.Preferred)
                ),
                new FilterControl(
                    "Map Features (Mutators)",
                    (listing, filters) =>
                    {
                        listing.Label("Map Features (scroll for all types):");

                        // Operator toggle (only show if critical items configured)
                        if (filters.MapFeatures.HasCritical)
                        {
                            var operatorRect = listing.GetRect(30f);
                            var operatorLabel = ConflictDetector.GetOperatorDescription(filters.MapFeatures.Operator, "features");
                            var operatorTooltip = ConflictDetector.GetOperatorTooltip(filters.MapFeatures.Operator, "features",
                                filters.MapFeatures.GetCriticalItems().Count());

                            if (Widgets.ButtonText(operatorRect, operatorLabel))
                            {
                                filters.MapFeatures.Operator = filters.MapFeatures.Operator == ImportanceOperator.OR
                                    ? ImportanceOperator.AND
                                    : ImportanceOperator.OR;
                            }
                            TooltipHandler.TipRegion(operatorRect, operatorTooltip);
                            listing.Gap(4f);

                            // Show conflict warnings specific to map features
                            UIHelpers.DrawFilterConflicts(listing, AdvancedModeUI.GetActiveConflicts(), "map_features");
                        }

                        var featureTypes = MapFeatureFilter.GetAllMapFeatureTypes().OrderBy(f => f).ToList();

                        // Only show features matching search
                        var visibleFeatures = string.IsNullOrEmpty(_searchText)
                            ? featureTypes
                            : featureTypes.Where(f => MatchesSearch(f)).ToList();

                        // Use scrollable view for all features (no hard limit)
                        const float itemHeight = 30f;
                        const float maxScrollHeight = 400f;
                        float totalHeight = visibleFeatures.Count * itemHeight;
                        bool needsScroll = totalHeight > maxScrollHeight;

                        if (needsScroll)
                        {
                            var viewRect = listing.GetRect(maxScrollHeight);
                            var scrollRect = new Rect(0f, 0f, viewRect.width - 16f, totalHeight);

                            Widgets.BeginScrollView(viewRect, ref _mapFeaturesScrollPosition, scrollRect);

                            float y = 0f;
                            foreach (var feature in visibleFeatures)
                            {
                                var itemRect = new Rect(0f, y, scrollRect.width, itemHeight);
                                var importance = filters.MapFeatures.GetImportance(feature);
                                UIHelpers.DrawImportanceSelector(itemRect, feature, ref importance);
                                filters.MapFeatures.SetImportance(feature, importance);
                                y += itemHeight;
                            }

                            Widgets.EndScrollView();
                        }
                        else
                        {
                            // Draw without scrolling if all items fit
                            foreach (var feature in visibleFeatures)
                            {
                                var importance = filters.MapFeatures.GetImportance(feature);
                                UIHelpers.DrawImportanceSelector(listing.GetRect(itemHeight), feature, ref importance);
                                filters.MapFeatures.SetImportance(feature, importance);
                            }
                        }

                        if (visibleFeatures.Count == 0 && !string.IsNullOrEmpty(_searchText))
                        {
                            listing.Label("(no features match search)");
                        }
                    },
                    filters => (filters.MapFeatures.HasAnyImportance,
                               filters.MapFeatures.HasCritical ? FilterImportance.Critical : FilterImportance.Preferred)
                ),
                ImportanceOnlyControl(
                    "Landmarks",
                    f => f.LandmarkImportance,
                    (f, v) => f.LandmarkImportance = v
                ),
                ImportanceOnlyControl(
                    "World Features (legacy)",
                    f => f.FeatureImportance,
                    (f, v) => f.FeatureImportance = v
                )
            };

            return new FilterGroup("special_features", "Features", filters);
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
                    "Strictness (Legacy k-of-n)",
                    (listing, filters) =>
                    {
                        listing.Label($"Critical Strictness: {filters.CriticalStrictness:F2}");
                        var sliderRect = listing.GetRect(30f);
                        float strictness = Widgets.HorizontalSlider(
                            sliderRect,
                            filters.CriticalStrictness,
                            0f,
                            1f,
                            true,
                            $"{filters.CriticalStrictness:F2}",
                            "0.0 (fuzzy)",
                            "1.0 (hard)"
                        );
                        filters.CriticalStrictness = strictness;

                        Text.Font = GameFont.Tiny;
                        GUI.color = new Color(0.7f, 0.7f, 0.7f);
                        listing.Label("0.0 = fuzzy matching (k-of-n), 1.0 = all critical filters must match (legacy)");
                        GUI.color = Color.white;
                        Text.Font = GameFont.Small;
                    },
                    filters => (filters.CriticalStrictness < 1.0f, FilterImportance.Ignored)
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

            float buttonWidth = (rect.width - 10f) / 4f;
            float x = rect.x;

            for (int i = 0; i < hillinessValues.Length; i++)
            {
                var hilliness = hillinessValues[i];
                Rect buttonRect = new Rect(x, rect.y, buttonWidth, rect.height);

                bool isSelected = filters.AllowedHilliness.Contains(hilliness);
                bool clicked = Widgets.ButtonText(buttonRect, labels[i], active: isSelected);

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
            // Get all natural rock types from ThingDef (Granite, Marble, Sandstone, Limestone, Slate, etc.)
            // These are the construction materials, not mineable ores
            var stoneTypes = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def.building != null && def.building.isNaturalRock)
                .Select(def => def.defName)
                .OrderBy(name => name)
                .ToList();

            // Fallback: if no rocks found, return common known types
            if (stoneTypes.Count == 0)
            {
                stoneTypes = new List<string> { "Granite", "Limestone", "Marble", "Sandstone", "Slate" };
            }

            return stoneTypes;
        }

        private static List<BiomeDef> GetAllBiomes()
        {
            // Get all 16 biomes from canonical SSoT
            return DefDatabase<BiomeDef>.AllDefsListForReading
                .Where(b => !b.impassable)
                .OrderBy(b => b.label)
                .ToList();
        }

        // ============================================================================
        // LIVE SELECTIVITY FEEDBACK
        // ============================================================================

        /// <summary>
        /// Draws live selectivity feedback for a filter (Tier 3 feature).
        /// Shows "📊 Match: ~45% of settleable tiles (127k / 295k)" below the filter control.
        /// Only renders if filter is not ignored and selectivity data is available.
        /// </summary>
        private static void DrawSelectivityFeedback(Listing_Standard listing, string filterId, FilterImportance importance)
        {
            // Skip if ignored - no point showing feedback
            if (importance == FilterImportance.Ignored)
                return;

            // Skip if context not ready
            if (LandingZoneContext.Filters == null || LandingZoneContext.State == null)
                return;

            try
            {
                var selectivity = LandingZoneContext.Filters.GetFilterSelectivity(filterId, LandingZoneContext.State);
                if (!selectivity.HasValue)
                    return;

                var s = selectivity.Value;
                var percentage = s.Ratio * 100f;

                // Format: "📊 Match: ~45.2% of tiles (127,342 / 295,732)"
                string matchText = $"📊 Match: ~{percentage:F1}% of tiles ({s.MatchCount:N0} / {s.TotalTiles:N0})";

                // Add category label (VeryCommon, Common, Uncommon, Rare, VeryRare, ExtremelyRare)
                string categoryLabel = s.Category switch
                {
                    Filtering.SelectivityCategory.VeryCommon => "Very Common",
                    Filtering.SelectivityCategory.Common => "Common",
                    Filtering.SelectivityCategory.Uncommon => "Uncommon",
                    Filtering.SelectivityCategory.Rare => "Rare",
                    Filtering.SelectivityCategory.VeryRare => "Very Rare",
                    Filtering.SelectivityCategory.ExtremelyRare => "Extremely Rare",
                    _ => ""
                };

                if (!string.IsNullOrEmpty(categoryLabel))
                    matchText += $" [{categoryLabel}]";

                // Render in small gray text
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                listing.Label(matchText);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                listing.Gap(3f);
            }
            catch (System.Exception ex)
            {
                // Silently fail - don't break UI if selectivity analysis fails
                Log.Warning($"[LandingZone] Failed to get selectivity for {filterId}: {ex.Message}");
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
                listing.Label("Fallback tier manager available when world is loaded");
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
                    listing.Label("Set filters to Critical importance to see fallback tiers");
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
                listing.Label($"Fallback Tier Manager ({criticalSelectivities.Count} critical filters)");
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                listing.Label("If current strictness is too restrictive, use these preset fallback tiers:");
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
                    "CURRENT:"
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
                    listing.Label("Click to apply fallback tier:");
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
                            $"Strictness: {suggestion.Strictness:P0}"
                        );
                        GUI.color = Color.white;
                        Text.Font = GameFont.Small;

                        if (Widgets.ButtonInvisible(suggestionRect))
                        {
                            filters.CriticalStrictness = suggestion.Strictness;
                            Messages.Message(
                                $"Applied fallback tier: {suggestion.Description} (strictness {suggestion.Strictness:P0})",
                                MessageTypeDefOf.NeutralEvent,
                                false
                            );
                        }

                        TooltipHandler.TipRegion(suggestionRect, $"{suggestion.Description}\nClick to set strictness to {suggestion.Strictness:P0}");
                        listing.Gap(4f);
                    }
                }
                else
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = new Color(0.6f, 0.6f, 0.6f);
                    listing.Label("No alternative tiers available (adjust strictness manually if needed)");
                    GUI.color = Color.white;
                    Text.Font = GameFont.Small;
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[LandingZone] Failed to draw fallback tier manager: {ex.Message}");
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.3f, 0.3f);
                listing.Label($"Error loading fallback tiers: {ex.Message}");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }
        }
    }
}

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
            string unit = "")
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
            Action<FilterSettings, FilterImportance> importanceSetter)
        {
            return new FilterControl(
                label,
                (listing, filters) =>
                {
                    var importance = importanceGetter(filters);
                    UIHelpers.DrawImportanceSelector(listing.GetRect(30f), label, ref importance);
                    importanceSetter(filters, importance);
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
                GetBiomeControlGroup()
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
                    -60f, 60f, "°C"
                ),
                FloatRangeControl(
                    "Temperature (Minimum)",
                    f => f.MinimumTemperatureRange,
                    (f, v) => f.MinimumTemperatureRange = v,
                    f => f.MinimumTemperatureImportance,
                    (f, v) => f.MinimumTemperatureImportance = v,
                    -60f, 40f, "°C"
                ),
                FloatRangeControl(
                    "Temperature (Maximum)",
                    f => f.MaximumTemperatureRange,
                    (f, v) => f.MaximumTemperatureRange = v,
                    f => f.MaximumTemperatureImportance,
                    (f, v) => f.MaximumTemperatureImportance = v,
                    0f, 60f, "°C"
                ),
                FloatRangeControl(
                    "Rainfall",
                    f => f.RainfallRange,
                    (f, v) => f.RainfallRange = v,
                    f => f.RainfallImportance,
                    (f, v) => f.RainfallImportance = v,
                    0f, 5300f, " mm/year"
                ),
                FloatRangeControl(
                    "Growing Days",
                    f => f.GrowingDaysRange,
                    (f, v) => f.GrowingDaysRange = v,
                    f => f.GrowingDaysImportance,
                    (f, v) => f.GrowingDaysImportance = v,
                    0f, 60f, " days/year"
                ),
                FloatRangeControl(
                    "Pollution",
                    f => f.PollutionRange,
                    (f, v) => f.PollutionRange = v,
                    f => f.PollutionImportance,
                    (f, v) => f.PollutionImportance = v,
                    0f, 1f
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
                    (f, v) => f.CoastalImportance = v
                ),
                ImportanceOnlyControl(
                    "Coastal (Lake)",
                    f => f.CoastalLakeImportance,
                    (f, v) => f.CoastalLakeImportance = v
                ),
                new FilterControl(
                    "Rivers",
                    (listing, filters) =>
                    {
                        listing.Label("Rivers:");
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

            return new FilterGroup("terrain_access", "Terrain & Access", filters);
        }

        private static FilterGroup GetResourcesProductionGroup()
        {
            var filters = new List<FilterControl>
            {
                new FilterControl(
                    "Stones",
                    (listing, filters) =>
                    {
                        listing.Label("Stones (individual selection):");
                        var stoneTypes = GetAllStoneTypes();
                        foreach (var stoneType in stoneTypes)
                        {
                            var importance = filters.Stones.GetImportance(stoneType);
                            UIHelpers.DrawImportanceSelector(listing.GetRect(30f), stoneType, ref importance);
                            filters.Stones.SetImportance(stoneType, importance);
                        }

                        listing.Gap(5f);
                        bool useStoneCount = filters.UseStoneCount;
                        listing.CheckboxLabeled("Use Stone Count Mode (any X types)", ref useStoneCount);
                        filters.UseStoneCount = useStoneCount;
                        if (filters.UseStoneCount)
                        {
                            var range = filters.StoneCountRange;
                            listing.Label($"Required Stone Count: {range.min:F0} to {range.max:F0} types");
                            var rangeRect = listing.GetRect(30f);
                            Widgets.FloatRange(rangeRect, rangeRect.GetHashCode(), ref range, 1f, stoneTypes.Count);
                            filters.StoneCountRange = range;
                        }
                    },
                    filters => (filters.Stones.HasAnyImportance || filters.UseStoneCount,
                               filters.Stones.HasCritical ? FilterImportance.Critical : FilterImportance.Preferred)
                ),
                FloatRangeControl(
                    "Forageability",
                    f => f.ForageabilityRange,
                    (f, v) => f.ForageabilityRange = v,
                    f => f.ForageImportance,
                    (f, v) => f.ForageImportance = v,
                    0f, 1f
                ),
                ImportanceOnlyControl(
                    "Animals Can Graze",
                    f => f.GrazeImportance,
                    (f, v) => f.GrazeImportance = v
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
                    "Map Features (Mutators)",
                    (listing, filters) =>
                    {
                        listing.Label("Map Features (83 types - use search!):");
                        var featureTypes = MapFeatureFilter.GetAllMapFeatureTypes().OrderBy(f => f).ToList();

                        // Only show features matching search
                        var visibleFeatures = string.IsNullOrEmpty(_searchText)
                            ? featureTypes
                            : featureTypes.Where(f => MatchesSearch(f)).ToList();

                        foreach (var feature in visibleFeatures.Take(20)) // Limit display to avoid UI overflow
                        {
                            var importance = filters.MapFeatures.GetImportance(feature);
                            UIHelpers.DrawImportanceSelector(listing.GetRect(30f), feature, ref importance);
                            filters.MapFeatures.SetImportance(feature, importance);
                        }

                        if (visibleFeatures.Count > 20)
                        {
                            listing.Label($"... and {visibleFeatures.Count - 20} more (use search to narrow)");
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

            return new FilterGroup("special_features", "Special Features", filters);
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

        // ============================================================================
        // DATA TYPE GROUPS (alternative organization)
        // ============================================================================

        private static List<FilterGroup> GetDataTypeGroups()
        {
            return new List<FilterGroup>
            {
                GetClimateGroup(),
                GetGeographyGroup(),
                GetWaterRoutesGroup(),
                GetResourcesGroup(),
                GetFeaturesBiomesGroup()
            };
        }

        private static FilterGroup GetClimateGroup()
        {
            // Same filters as Climate Comfort, just different grouping context
            var comfortGroup = GetClimateComfortGroup();
            return new FilterGroup("climate", "Climate", comfortGroup.Filters);
        }

        private static FilterGroup GetGeographyGroup()
        {
            var filters = new List<FilterControl>
            {
                FloatRangeControl(
                    "Elevation",
                    f => f.ElevationRange,
                    (f, v) => f.ElevationRange = v,
                    f => f.ElevationImportance,
                    (f, v) => f.ElevationImportance = v,
                    0f, 3100f, " m"
                ),
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
                FloatRangeControl(
                    "Swampiness",
                    f => f.SwampinessRange,
                    (f, v) => f.SwampinessRange = v,
                    f => f.SwampinessImportance,
                    (f, v) => f.SwampinessImportance = v,
                    0f, 1.2f
                ),
                FloatRangeControl(
                    "Movement Difficulty",
                    f => f.MovementDifficultyRange,
                    (f, v) => f.MovementDifficultyRange = v,
                    f => f.MovementDifficultyImportance,
                    (f, v) => f.MovementDifficultyImportance = v,
                    0f, 2f
                ),
                ImportanceOnlyControl(
                    "Coastal (Ocean)",
                    f => f.CoastalImportance,
                    (f, v) => f.CoastalImportance = v
                ),
                ImportanceOnlyControl(
                    "Coastal (Lake)",
                    f => f.CoastalLakeImportance,
                    (f, v) => f.CoastalLakeImportance = v
                )
            };

            return new FilterGroup("geography", "Geography", filters);
        }

        private static FilterGroup GetWaterRoutesGroup()
        {
            // Extract Rivers and Roads from Terrain Access group
            var terrainGroup = GetTerrainAccessGroup();
            var riversRoads = terrainGroup.Filters.Where(f => f.Label == "Rivers" || f.Label == "Roads").ToList();

            return new FilterGroup("water_routes", "Water & Routes", riversRoads);
        }

        private static FilterGroup GetResourcesGroup()
        {
            // Same as Resources & Production
            var productionGroup = GetResourcesProductionGroup();
            return new FilterGroup("resources", "Resources", productionGroup.Filters);
        }

        private static FilterGroup GetFeaturesBiomesGroup()
        {
            // Combine Special Features + Biome Control
            var specialFilters = GetSpecialFeaturesGroup().Filters;
            var biomeFilters = GetBiomeControlGroup().Filters;

            var combined = new List<FilterControl>();
            combined.AddRange(specialFilters);
            combined.AddRange(biomeFilters);

            return new FilterGroup("features_biomes", "Features & Biomes", combined);
        }

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
            // Get all stone types from ThingDef where category is Building and stuffCategories contains Stony
            return DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def.building != null &&
                              def.stuffProps != null &&
                              def.stuffProps.categories != null &&
                              def.stuffProps.categories.Contains(StuffCategoryDefOf.Stony))
                .Select(def => def.defName)
                .OrderBy(name => name)
                .ToList();
        }

        private static List<BiomeDef> GetAllBiomes()
        {
            // Get all 16 biomes from canonical SSoT
            return DefDatabase<BiomeDef>.AllDefsListForReading
                .Where(b => !b.impassable)
                .OrderBy(b => b.label)
                .ToList();
        }
    }
}

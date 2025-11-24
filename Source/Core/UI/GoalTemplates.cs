using System;
using System.Collections.Generic;
using LandingZone.Data;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LandingZone.Core.UI
{
    /// <summary>
    /// Defines goal-based filter templates for Tier 2 (Guided Builder).
    /// Each template translates user goals into recommended filter configurations.
    /// </summary>
    public static class GoalTemplates
    {
        public enum GoalCategory
        {
            ClimateComfort,
            ResourceWealth,
            Defensibility,
            FoodProduction,
            PowerGeneration,
            TradeAccess,
            ChallengeRarity,
            SpecificFeature
        }

        public enum FoodProductionType
        {
            Farming,
            Hunting,
            Fishing,
            Mixed
        }

        /// <summary>
        /// Represents a filter recommendation with explanation.
        /// </summary>
        public class FilterRecommendation
        {
            public string FilterName { get; }
            public string Explanation { get; }
            public FilterImportance Importance { get; }
            public Action<FilterSettings> ApplyAction { get; }

            public FilterRecommendation(
                string filterName,
                string explanation,
                FilterImportance importance,
                Action<FilterSettings> applyAction)
            {
                FilterName = filterName;
                Explanation = explanation;
                Importance = importance;
                ApplyAction = applyAction;
            }
        }

        /// <summary>
        /// Gets filter recommendations for a goal category.
        /// </summary>
        public static List<FilterRecommendation> GetRecommendationsForGoal(
            GoalCategory category,
            FoodProductionType? foodType = null)
        {
            return category switch
            {
                GoalCategory.ClimateComfort => GetClimateComfortRecommendations(),
                GoalCategory.ResourceWealth => GetResourceWealthRecommendations(),
                GoalCategory.Defensibility => GetDefensibilityRecommendations(),
                GoalCategory.FoodProduction => GetFoodProductionRecommendations(foodType ?? FoodProductionType.Mixed),
                GoalCategory.PowerGeneration => GetPowerGenerationRecommendations(),
                GoalCategory.TradeAccess => GetTradeAccessRecommendations(),
                GoalCategory.ChallengeRarity => GetChallengeRarityRecommendations(),
                _ => new List<FilterRecommendation>()
            };
        }

        public static string GetGoalLabel(GoalCategory category)
        {
            return category switch
            {
                GoalCategory.ClimateComfort => "LandingZone_GoalClimateComfort".Translate(),
                GoalCategory.ResourceWealth => "LandingZone_GoalResourceWealth".Translate(),
                GoalCategory.Defensibility => "LandingZone_GoalDefensibility".Translate(),
                GoalCategory.FoodProduction => "LandingZone_GoalFoodProduction".Translate(),
                GoalCategory.PowerGeneration => "LandingZone_GoalPowerGeneration".Translate(),
                GoalCategory.TradeAccess => "LandingZone_GoalTradeAccess".Translate(),
                GoalCategory.ChallengeRarity => "LandingZone_GoalChallengeRarity".Translate(),
                GoalCategory.SpecificFeature => "LandingZone_GoalSpecificFeature".Translate(),
                _ => category.ToString()
            };
        }

        public static string GetGoalDescription(GoalCategory category)
        {
            return category switch
            {
                GoalCategory.ClimateComfort => "LandingZone_GoalClimateComfortDesc".Translate(),
                GoalCategory.ResourceWealth => "LandingZone_GoalResourceWealthDesc".Translate(),
                GoalCategory.Defensibility => "LandingZone_GoalDefensibilityDesc".Translate(),
                GoalCategory.FoodProduction => "LandingZone_GoalFoodProductionDesc".Translate(),
                GoalCategory.PowerGeneration => "LandingZone_GoalPowerGenerationDesc".Translate(),
                GoalCategory.TradeAccess => "LandingZone_GoalTradeAccessDesc".Translate(),
                GoalCategory.ChallengeRarity => "LandingZone_GoalChallengeRarityDesc".Translate(),
                GoalCategory.SpecificFeature => "LandingZone_GoalSpecificFeatureDesc".Translate(),
                _ => ""
            };
        }

        private static List<FilterRecommendation> GetClimateComfortRecommendations()
        {
            return new List<FilterRecommendation>
            {
                new FilterRecommendation(
                    "LandingZone_FilterTemp10to25".Translate(),
                    "LandingZone_FilterTemp10to25Desc".Translate(),
                    FilterImportance.Critical,
                    f => {
                        f.AverageTemperatureRange = new FloatRange(10f, 25f);
                        f.AverageTemperatureImportance = FilterImportance.Critical;
                    }
                ),
                new FilterRecommendation(
                    "LandingZone_FilterRainfall600to1400".Translate(),
                    "LandingZone_FilterRainfall600to1400Desc".Translate(),
                    FilterImportance.Critical,
                    f => {
                        f.RainfallRange = new FloatRange(600f, 1400f);
                        f.RainfallImportance = FilterImportance.Critical;
                    }
                ),
                new FilterRecommendation(
                    "LandingZone_FilterGrowingDays30to60".Translate(),
                    "LandingZone_FilterGrowingDays30to60Desc".Translate(),
                    FilterImportance.Preferred,
                    f => {
                        f.GrowingDaysRange = new FloatRange(30f, 60f);
                        f.GrowingDaysImportance = FilterImportance.Preferred;
                    }
                )
            };
        }

        private static List<FilterRecommendation> GetResourceWealthRecommendations()
        {
            return new List<FilterRecommendation>
            {
                new FilterRecommendation(
                    "LandingZone_FilterHighForage".Translate(),
                    "LandingZone_FilterHighForageDesc".Translate(),
                    FilterImportance.Preferred,
                    f => {
                        f.ForageabilityRange = new FloatRange(0.7f, 1.0f);
                        f.ForageImportance = FilterImportance.Preferred;
                    }
                ),
                new FilterRecommendation(
                    "LandingZone_FilterHighAnimalDensity".Translate(),
                    "LandingZone_FilterHighAnimalDensityDesc".Translate(),
                    FilterImportance.Preferred,
                    f => {
                        f.AnimalDensityRange = new FloatRange(3.0f, 6.5f);
                        f.AnimalDensityImportance = FilterImportance.Preferred;
                    }
                ),
                new FilterRecommendation(
                    "LandingZone_FilterHighPlantDensity".Translate(),
                    "LandingZone_FilterHighPlantDensityDesc".Translate(),
                    FilterImportance.Preferred,
                    f => {
                        f.PlantDensityRange = new FloatRange(0.7f, 1.0f);
                        f.PlantDensityImportance = FilterImportance.Preferred;
                    }
                )
            };
        }

        private static List<FilterRecommendation> GetDefensibilityRecommendations()
        {
            return new List<FilterRecommendation>
            {
                new FilterRecommendation(
                    "LandingZone_FilterMountainous".Translate(),
                    "LandingZone_FilterMountainousDesc".Translate(),
                    FilterImportance.Critical,
                    f => {
                        f.AllowedHilliness.Clear();
                        f.AllowedHilliness.Add(Hilliness.Mountainous);
                    }
                ),
                new FilterRecommendation(
                    "LandingZone_FilterCaveSystems".Translate(),
                    "LandingZone_FilterCaveSystemsDesc".Translate(),
                    FilterImportance.Preferred,
                    f => {
                        f.MapFeatures.SetImportance("Caves", FilterImportance.Preferred);
                    }
                )
            };
        }

        private static List<FilterRecommendation> GetFoodProductionRecommendations(FoodProductionType type)
        {
            var recommendations = new List<FilterRecommendation>();

            switch (type)
            {
                case FoodProductionType.Farming:
                    recommendations.Add(new FilterRecommendation(
                        "LandingZone_FilterGrowingDays50to60".Translate(),
                        "LandingZone_FilterGrowingDays50to60Desc".Translate(),
                        FilterImportance.Critical,
                        f => {
                            f.GrowingDaysRange = new FloatRange(50f, 60f);
                            f.GrowingDaysImportance = FilterImportance.Critical;
                        }
                    ));
                    recommendations.Add(new FilterRecommendation(
                        "LandingZone_FilterRainfall1200to2500".Translate(),
                        "LandingZone_FilterRainfall1200to2500Desc".Translate(),
                        FilterImportance.Critical,
                        f => {
                            f.RainfallRange = new FloatRange(1200f, 2500f);
                            f.RainfallImportance = FilterImportance.Critical;
                        }
                    ));
                    recommendations.Add(new FilterRecommendation(
                        "LandingZone_FilterFertileSoil".Translate(),
                        "LandingZone_FilterFertileSoilDesc".Translate(),
                        FilterImportance.Preferred,
                        f => {
                            f.MapFeatures.SetImportance("Fertile", FilterImportance.Preferred);
                        }
                    ));
                    break;

                case FoodProductionType.Hunting:
                    recommendations.Add(new FilterRecommendation(
                        "LandingZone_FilterHighAnimalDensity".Translate(),
                        "LandingZone_FilterHighAnimalDensityDesc".Translate(),
                        FilterImportance.Critical,
                        f => {
                            f.AnimalDensityRange = new FloatRange(3.0f, 6.5f);
                            f.AnimalDensityImportance = FilterImportance.Critical;
                        }
                    ));
                    recommendations.Add(new FilterRecommendation(
                        "LandingZone_FilterIncreasedAnimalLife".Translate(),
                        "LandingZone_FilterIncreasedAnimalLifeDesc".Translate(),
                        FilterImportance.Preferred,
                        f => {
                            f.MapFeatures.SetImportance("AnimalLife_Increased", FilterImportance.Preferred);
                        }
                    ));
                    break;

                case FoodProductionType.Fishing:
                    recommendations.Add(new FilterRecommendation(
                        "LandingZone_FilterCoastalAccess".Translate(),
                        "LandingZone_FilterCoastalAccessDesc".Translate(),
                        FilterImportance.Critical,
                        f => {
                            f.CoastalImportance = FilterImportance.Critical;
                        }
                    ));
                    recommendations.Add(new FilterRecommendation(
                        "LandingZone_FilterRivers".Translate(),
                        "LandingZone_FilterRiversDesc".Translate(),
                        FilterImportance.Preferred,
                        f => {
                            f.Rivers.SetImportance("River", FilterImportance.Preferred);
                        }
                    ));
                    recommendations.Add(new FilterRecommendation(
                        "LandingZone_FilterHighFishPopulation".Translate(),
                        "LandingZone_FilterHighFishPopulationDesc".Translate(),
                        FilterImportance.Preferred,
                        f => {
                            f.FishPopulationRange = new FloatRange(300f, 900f);
                            f.FishPopulationImportance = FilterImportance.Preferred;
                        }
                    ));
                    break;

                case FoodProductionType.Mixed:
                    recommendations.Add(new FilterRecommendation(
                        "LandingZone_FilterGrowingDays30to60".Translate(),
                        "LandingZone_FilterGrowingDays30to60Desc".Translate(),
                        FilterImportance.Preferred,
                        f => {
                            f.GrowingDaysRange = new FloatRange(30f, 60f);
                            f.GrowingDaysImportance = FilterImportance.Preferred;
                        }
                    ));
                    recommendations.Add(new FilterRecommendation(
                        "LandingZone_FilterModerateAnimalDensity".Translate(),
                        "LandingZone_FilterModerateAnimalDensityDesc".Translate(),
                        FilterImportance.Preferred,
                        f => {
                            f.AnimalDensityRange = new FloatRange(2.0f, 6.5f);
                            f.AnimalDensityImportance = FilterImportance.Preferred;
                        }
                    ));
                    recommendations.Add(new FilterRecommendation(
                        "LandingZone_FilterWaterAccess".Translate(),
                        "LandingZone_FilterWaterAccessDesc".Translate(),
                        FilterImportance.Preferred,
                        f => {
                            f.WaterAccessImportance = FilterImportance.Preferred;
                        }
                    ));
                    break;
            }

            return recommendations;
        }

        private static List<FilterRecommendation> GetPowerGenerationRecommendations()
        {
            return new List<FilterRecommendation>
            {
                new FilterRecommendation(
                    "LandingZone_FilterSteamGeysers".Translate(),
                    "LandingZone_FilterSteamGeysersDesc".Translate(),
                    FilterImportance.Preferred,
                    f => {
                        f.MapFeatures.SetImportance("SteamGeysers_Increased", FilterImportance.Preferred);
                    }
                ),
                new FilterRecommendation(
                    "LandingZone_FilterWindyClimate".Translate(),
                    "LandingZone_FilterWindyClimateDesc".Translate(),
                    FilterImportance.Preferred,
                    f => {
                        f.MapFeatures.SetImportance("WindyMutator", FilterImportance.Preferred);
                    }
                ),
                new FilterRecommendation(
                    "LandingZone_FilterSunnyClimate".Translate(),
                    "LandingZone_FilterSunnyClimateDesc".Translate(),
                    FilterImportance.Preferred,
                    f => {
                        f.MapFeatures.SetImportance("SunnyMutator", FilterImportance.Preferred);
                    }
                ),
                new FilterRecommendation(
                    "LandingZone_FilterRiversHydro".Translate(),
                    "LandingZone_FilterRiversHydroDesc".Translate(),
                    FilterImportance.Preferred,
                    f => {
                        f.Rivers.SetImportance("River", FilterImportance.Preferred);
                    }
                )
            };
        }

        private static List<FilterRecommendation> GetTradeAccessRecommendations()
        {
            return new List<FilterRecommendation>
            {
                new FilterRecommendation(
                    "LandingZone_FilterCoastalOrRoads".Translate(),
                    "LandingZone_FilterCoastalOrRoadsDesc".Translate(),
                    FilterImportance.Critical,
                    f => {
                        // Water access covers both coastal and rivers
                        f.WaterAccessImportance = FilterImportance.Preferred;
                        // Roads increase caravan frequency
                        f.Roads.SetImportance("AncientAsphaltRoad", FilterImportance.Preferred);
                    }
                ),
                new FilterRecommendation(
                    "LandingZone_FilterModerateClimate".Translate(),
                    "LandingZone_FilterModerateClimateDesc".Translate(),
                    FilterImportance.Preferred,
                    f => {
                        f.AverageTemperatureRange = new FloatRange(5f, 35f);
                        f.AverageTemperatureImportance = FilterImportance.Preferred;
                    }
                )
            };
        }

        private static List<FilterRecommendation> GetChallengeRarityRecommendations()
        {
            return new List<FilterRecommendation>
            {
                new FilterRecommendation(
                    "LandingZone_FilterUltraRareFeatures".Translate(),
                    "LandingZone_FilterUltraRareFeaturesDesc".Translate(),
                    FilterImportance.Preferred,
                    f => {
                        // Examples of rare features
                        f.MapFeatures.SetImportance("ArcheanTrees", FilterImportance.Preferred);
                        f.MapFeatures.SetImportance("MineralRich", FilterImportance.Preferred);
                        f.MapFeatures.SetImportance("AncientWarehouse", FilterImportance.Preferred);
                    }
                ),
                new FilterRecommendation(
                    "LandingZone_FilterExtremeClimate".Translate(),
                    "LandingZone_FilterExtremeClimateDesc".Translate(),
                    FilterImportance.Preferred,
                    f => {
                        // Either very hot or very cold
                        f.AverageTemperatureRange = new FloatRange(-60f, -20f);
                        f.AverageTemperatureImportance = FilterImportance.Preferred;
                    }
                )
            };
        }
    }
}

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
                GoalCategory.ClimateComfort => "Climate Comfort",
                GoalCategory.ResourceWealth => "Resource Wealth",
                GoalCategory.Defensibility => "Defensibility",
                GoalCategory.FoodProduction => "Food Production",
                GoalCategory.PowerGeneration => "Power Generation",
                GoalCategory.TradeAccess => "Trade Access",
                GoalCategory.ChallengeRarity => "Challenge/Rarity",
                GoalCategory.SpecificFeature => "Specific Feature",
                _ => category.ToString()
            };
        }

        public static string GetGoalDescription(GoalCategory category)
        {
            return category switch
            {
                GoalCategory.ClimateComfort => "Find a pleasant, moderate climate with comfortable temperatures year-round",
                GoalCategory.ResourceWealth => "Maximize available resources: minerals, plants, animals, and materials",
                GoalCategory.Defensibility => "Prioritize defensive terrain: mountains, caves, cliffs, and natural barriers",
                GoalCategory.FoodProduction => "Optimize for food security through farming, hunting, or fishing",
                GoalCategory.PowerGeneration => "Access to renewable energy: geothermal, wind, solar, or hydroelectric",
                GoalCategory.TradeAccess => "Coastal or road access for trading and visitor traffic",
                GoalCategory.ChallengeRarity => "Find ultra-rare map features for challenging or unique gameplay",
                GoalCategory.SpecificFeature => "Search for specific map features, biomes, or conditions",
                _ => ""
            };
        }

        private static List<FilterRecommendation> GetClimateComfortRecommendations()
        {
            return new List<FilterRecommendation>
            {
                new FilterRecommendation(
                    "Temperature: 10-25°C",
                    "Comfortable year-round temperatures (no extreme heat or cold)",
                    FilterImportance.Critical,
                    f => {
                        f.AverageTemperatureRange = new FloatRange(10f, 25f);
                        f.AverageTemperatureImportance = FilterImportance.Critical;
                    }
                ),
                new FilterRecommendation(
                    "Rainfall: 600-1400mm",
                    "Moderate rainfall (not too dry, not swampy)",
                    FilterImportance.Critical,
                    f => {
                        f.RainfallRange = new FloatRange(600f, 1400f);
                        f.RainfallImportance = FilterImportance.Critical;
                    }
                ),
                new FilterRecommendation(
                    "Growing Days: 30-60 days",
                    "Long growing season for year-round crops",
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
                    "High Forageability",
                    "Abundant wild plants for foraging and medicine",
                    FilterImportance.Preferred,
                    f => {
                        f.ForageabilityRange = new FloatRange(0.7f, 1.0f);
                        f.ForageImportance = FilterImportance.Preferred;
                    }
                ),
                new FilterRecommendation(
                    "High Animal Density",
                    "Plenty of wildlife for hunting and taming",
                    FilterImportance.Preferred,
                    f => {
                        f.AnimalDensityRange = new FloatRange(3.0f, 6.5f);
                        f.AnimalDensityImportance = FilterImportance.Preferred;
                    }
                ),
                new FilterRecommendation(
                    "High Plant Density",
                    "Dense vegetation for wood and materials",
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
                    "Mountainous Terrain",
                    "Natural barriers and defensible positions",
                    FilterImportance.Critical,
                    f => {
                        f.AllowedHilliness.Clear();
                        f.AllowedHilliness.Add(Hilliness.Mountainous);
                    }
                ),
                new FilterRecommendation(
                    "Cave Systems",
                    "Underground defensibility and storage",
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
                        "Growing Days: 50-60 days",
                        "Year-round farming capability",
                        FilterImportance.Critical,
                        f => {
                            f.GrowingDaysRange = new FloatRange(50f, 60f);
                            f.GrowingDaysImportance = FilterImportance.Critical;
                        }
                    ));
                    recommendations.Add(new FilterRecommendation(
                        "Rainfall: 1200-2500mm",
                        "Consistent moisture for crops",
                        FilterImportance.Critical,
                        f => {
                            f.RainfallRange = new FloatRange(1200f, 2500f);
                            f.RainfallImportance = FilterImportance.Critical;
                        }
                    ));
                    recommendations.Add(new FilterRecommendation(
                        "Fertile Soil",
                        "Faster crop growth",
                        FilterImportance.Preferred,
                        f => {
                            f.MapFeatures.SetImportance("Fertile", FilterImportance.Preferred);
                        }
                    ));
                    break;

                case FoodProductionType.Hunting:
                    recommendations.Add(new FilterRecommendation(
                        "High Animal Density",
                        "Abundant wildlife for hunting",
                        FilterImportance.Critical,
                        f => {
                            f.AnimalDensityRange = new FloatRange(3.0f, 6.5f);
                            f.AnimalDensityImportance = FilterImportance.Critical;
                        }
                    ));
                    recommendations.Add(new FilterRecommendation(
                        "Increased Animal Life",
                        "More hunting opportunities",
                        FilterImportance.Preferred,
                        f => {
                            f.MapFeatures.SetImportance("AnimalLife_Increased", FilterImportance.Preferred);
                        }
                    ));
                    break;

                case FoodProductionType.Fishing:
                    recommendations.Add(new FilterRecommendation(
                        "Coastal Access",
                        "Ocean fishing opportunities",
                        FilterImportance.Critical,
                        f => {
                            f.CoastalImportance = FilterImportance.Critical;
                        }
                    ));
                    recommendations.Add(new FilterRecommendation(
                        "Rivers",
                        "Freshwater fishing + irrigation",
                        FilterImportance.Preferred,
                        f => {
                            f.Rivers.SetImportance("River", FilterImportance.Preferred);
                        }
                    ));
                    recommendations.Add(new FilterRecommendation(
                        "High Fish Population",
                        "Better fishing yields",
                        FilterImportance.Preferred,
                        f => {
                            f.FishPopulationRange = new FloatRange(300f, 900f);
                            f.FishPopulationImportance = FilterImportance.Preferred;
                        }
                    ));
                    break;

                case FoodProductionType.Mixed:
                    recommendations.Add(new FilterRecommendation(
                        "Growing Days: 30-60 days",
                        "Decent growing season",
                        FilterImportance.Preferred,
                        f => {
                            f.GrowingDaysRange = new FloatRange(30f, 60f);
                            f.GrowingDaysImportance = FilterImportance.Preferred;
                        }
                    ));
                    recommendations.Add(new FilterRecommendation(
                        "Moderate Animal Density",
                        "Hunting opportunities",
                        FilterImportance.Preferred,
                        f => {
                            f.AnimalDensityRange = new FloatRange(2.0f, 6.5f);
                            f.AnimalDensityImportance = FilterImportance.Preferred;
                        }
                    ));
                    recommendations.Add(new FilterRecommendation(
                        "Water Access",
                        "Coastal OR river for fishing",
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
                    "Steam Geysers",
                    "Free geothermal power and heat",
                    FilterImportance.Preferred,
                    f => {
                        f.MapFeatures.SetImportance("SteamGeysers_Increased", FilterImportance.Preferred);
                    }
                ),
                new FilterRecommendation(
                    "Windy Climate",
                    "Wind turbine bonus",
                    FilterImportance.Preferred,
                    f => {
                        f.MapFeatures.SetImportance("WindyMutator", FilterImportance.Preferred);
                    }
                ),
                new FilterRecommendation(
                    "Sunny Climate",
                    "Solar panel efficiency bonus",
                    FilterImportance.Preferred,
                    f => {
                        f.MapFeatures.SetImportance("SunnyMutator", FilterImportance.Preferred);
                    }
                ),
                new FilterRecommendation(
                    "Rivers",
                    "Hydroelectric potential",
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
                    "Coastal OR Roads",
                    "Trade caravans and ships can reach you",
                    FilterImportance.Critical,
                    f => {
                        // Water access covers both coastal and rivers
                        f.WaterAccessImportance = FilterImportance.Preferred;
                        // Roads increase caravan frequency
                        f.Roads.SetImportance("AncientAsphaltRoad", FilterImportance.Preferred);
                    }
                ),
                new FilterRecommendation(
                    "Moderate Climate",
                    "Easier travel for caravans",
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
                    "Ultra-Rare Features",
                    "Unique map features for challenging gameplay",
                    FilterImportance.Preferred,
                    f => {
                        // Examples of rare features
                        f.MapFeatures.SetImportance("ArcheanTrees", FilterImportance.Preferred);
                        f.MapFeatures.SetImportance("MineralRich", FilterImportance.Preferred);
                        f.MapFeatures.SetImportance("AncientWarehouse", FilterImportance.Preferred);
                    }
                ),
                new FilterRecommendation(
                    "Extreme Climate",
                    "Challenging temperature conditions",
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

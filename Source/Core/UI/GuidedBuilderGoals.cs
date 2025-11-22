using System.Collections.Generic;
using LandingZone.Data;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core.UI
{
    /// <summary>
    /// Defines priority goals for Guided Builder and their filter recommendations.
    /// </summary>
    public enum PriorityGoal
    {
        None,
        ClimateComfort,
        ResourceWealth,
        Defensibility,
        Agriculture,
        WaterAccess,
        Challenge
    }

    public static class GuidedBuilderGoals
    {
        public static string GetGoalName(PriorityGoal goal)
        {
            return goal switch
            {
                PriorityGoal.ClimateComfort => "Climate Comfort",
                PriorityGoal.ResourceWealth => "Resource Wealth",
                PriorityGoal.Defensibility => "Defensibility",
                PriorityGoal.Agriculture => "Agriculture",
                PriorityGoal.WaterAccess => "Water Access",
                PriorityGoal.Challenge => "Challenge/Exotic",
                _ => "None"
            };
        }

        public static string GetGoalDescription(PriorityGoal goal)
        {
            return goal switch
            {
                PriorityGoal.ClimateComfort => "Temperate zones with mild weather, good growing seasons, minimal temperature extremes",
                PriorityGoal.ResourceWealth => "Rich mineral deposits, abundant foraging, diverse stones, geothermal/wind power",
                PriorityGoal.Defensibility => "Mountains, caves, chokepoints, natural barriers against raids",
                PriorityGoal.Agriculture => "Fertile soil, consistent rainfall, long growing seasons, plant-rich biomes",
                PriorityGoal.WaterAccess => "Rivers, coastal access, fishing opportunities, natural water sources",
                PriorityGoal.Challenge => "Rare biomes, extreme environments, unique map features, difficult starts",
                _ => ""
            };
        }

        /// <summary>
        /// Applies filter recommendations for a priority goal.
        /// Priority 1 = Critical filters, Priority 2-4 = Preferred filters with decreasing weight.
        /// </summary>
        public static void ApplyGoalFilters(PriorityGoal goal, int prioritySlot, FilterSettings filters)
        {
            // Priority 1 = Critical, Priority 2-4 = Preferred
            var importance = prioritySlot == 1 ? FilterImportance.Critical : FilterImportance.Preferred;

            switch (goal)
            {
                case PriorityGoal.ClimateComfort:
                    ApplyClimateComfortFilters(filters, importance);
                    break;

                case PriorityGoal.ResourceWealth:
                    ApplyResourceWealthFilters(filters, importance);
                    break;

                case PriorityGoal.Defensibility:
                    ApplyDefensibilityFilters(filters, importance);
                    break;

                case PriorityGoal.Agriculture:
                    ApplyAgricultureFilters(filters, importance);
                    break;

                case PriorityGoal.WaterAccess:
                    ApplyWaterAccessFilters(filters, importance);
                    break;

                case PriorityGoal.Challenge:
                    ApplyChallengeFilters(filters, importance);
                    break;
            }
        }

        private static void ApplyClimateComfortFilters(FilterSettings filters, FilterImportance importance)
        {
            // Temperate range: 15-25°C
            filters.AverageTemperatureRange = new FloatRange(15f, 25f);
            filters.AverageTemperatureImportance = importance;

            // Good growing season
            filters.GrowingDaysRange = new FloatRange(30f, 60f);
            filters.GrowingDaysImportance = importance;

            // Moderate rainfall (not desert, not swamp)
            filters.RainfallRange = new FloatRange(600f, 2000f);
            filters.RainfallImportance = importance;
        }

        private static void ApplyResourceWealthFilters(FilterSettings filters, FilterImportance importance)
        {
            // Rich stone diversity (3+ types)
            filters.UseStoneCount = true;
            filters.StoneCountRange = new FloatRange(3f, 10f);

            // Foraging opportunities
            filters.ForageabilityRange = new FloatRange(0.8f, 1.0f);
            filters.ForageImportance = importance;

            // Prefer geothermal/wind mutators (Preferred only - these are rare)
            if (importance == FilterImportance.Preferred)
            {
                filters.MapFeatures.SetImportance("SteamGeysers_Increased", FilterImportance.Preferred);
                filters.MapFeatures.SetImportance("WindyMutator", FilterImportance.Preferred);
                filters.MapFeatures.SetImportance("MineralRich", FilterImportance.Preferred);
            }
        }

        private static void ApplyDefensibilityFilters(FilterSettings filters, FilterImportance importance)
        {
            // Mountains for defensibility
            if (!filters.AllowedHilliness.Contains(Hilliness.Mountainous))
            {
                filters.AllowedHilliness.Clear();
                filters.AllowedHilliness.Add(Hilliness.Mountainous);
                filters.AllowedHilliness.Add(Hilliness.LargeHills);
            }

            // Caves for fallback positions
            filters.MapFeatures.SetImportance("Caves", importance);
            filters.MapFeatures.SetImportance("Mountain", importance);

            // Avoid flat, open terrain
            if (importance == FilterImportance.Critical)
            {
                filters.AllowedHilliness.Remove(Hilliness.Flat);
            }
        }

        private static void ApplyAgricultureFilters(FilterSettings filters, FilterImportance importance)
        {
            // Long growing season
            filters.GrowingDaysRange = new FloatRange(40f, 60f);
            filters.GrowingDaysImportance = importance;

            // Good rainfall
            filters.RainfallRange = new FloatRange(800f, 2500f);
            filters.RainfallImportance = importance;

            // Fertile soil mutator
            if (importance == FilterImportance.Preferred)
            {
                filters.MapFeatures.SetImportance("Fertile", FilterImportance.Preferred);
                filters.MapFeatures.SetImportance("PlantLife_Increased", FilterImportance.Preferred);
            }

            // Plant-rich biomes
            filters.AnimalDensityRange = new FloatRange(0.0f, 5.0f);
            filters.PlantDensityRange = new FloatRange(0.8f, 1.0f);
            filters.PlantDensityImportance = importance;
        }

        private static void ApplyWaterAccessFilters(FilterSettings filters, FilterImportance importance)
        {
            // Coastal or river access (WaterAccessImportance = coastal OR any river)
            filters.WaterAccessImportance = importance;

            // Fish availability
            filters.FishPopulationImportance = importance;
            filters.FishPopulationRange = new FloatRange(100f, 900f);
        }

        private static void ApplyChallengeFilters(FilterSettings filters, FilterImportance importance)
        {
            // Extreme temperatures
            filters.AverageTemperatureRange = new FloatRange(-40f, 10f);
            filters.AverageTemperatureImportance = importance;

            // Rare/exotic map features (Preferred only to avoid zero results)
            if (importance == FilterImportance.Preferred)
            {
                filters.MapFeatures.SetImportance("ArcheanTrees", FilterImportance.Preferred);
                filters.MapFeatures.SetImportance("InsectMegahive", FilterImportance.Preferred);
                filters.MapFeatures.SetImportance("AncientRuins", FilterImportance.Preferred);
                filters.MapFeatures.SetImportance("LavaCaves", FilterImportance.Preferred);
            }

            // Difficult terrain
            filters.SwampinessRange = new FloatRange(0.5f, 1.0f);
            filters.SwampinessImportance = FilterImportance.Preferred;
        }

        public static List<PriorityGoal> GetAllGoals()
        {
            return new List<PriorityGoal>
            {
                PriorityGoal.ClimateComfort,
                PriorityGoal.ResourceWealth,
                PriorityGoal.Defensibility,
                PriorityGoal.Agriculture,
                PriorityGoal.WaterAccess,
                PriorityGoal.Challenge
            };
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by fish population (fishing yield potential).
    /// Based on biome-specific fish resource availability.
    /// Requires Biotech DLC for fish mechanics.
    /// </summary>
    public sealed class FishPopulationFilter : ISiteFilter
    {
        public string Id => "fish_population";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            var importance = filters.FishPopulationImportance;

            // Only hard gates (MustHave/MustNotHave) filter in Apply phase
            if (!importance.IsHardGate())
                return inputTiles;

            var range = filters.FishPopulationRange;
            var worldGrid = Find.World.grid;

            return inputTiles.Where(id =>
            {
                var tile = worldGrid[id];
                if (tile == null) return false;

                var biome = tile.PrimaryBiome;
                if (biome == null) return false;

                float fishPopulation = GetFishPopulation(biome);
                bool inRange = fishPopulation >= range.min && fishPopulation <= range.max;
                return importance == FilterImportance.MustNotHave ? !inRange : inRange;
            });
        }

        private float GetFishPopulation(BiomeDef biome)
        {
            // Fish population calculation based on biome properties
            // This is a heuristic since RimWorld doesn't expose a direct "fish count" property
            if (biome == null) return 0f;

            // Check if biome has fishing potential (waterMult > 0 or fishing resources)
            // This is an approximation - may need adjustment based on actual RimWorld data
            float fishingPotential = 0f;

            // Coastal/water biomes typically have fishing
            if (biome.defName.Contains("Ocean") || biome.defName.Contains("Lake"))
            {
                fishingPotential = 500f; // Baseline for water biomes
            }

            // Check for fishing-related resources in the biome
            if (biome.wildPlants != null)
            {
                foreach (var plantRecord in biome.wildPlants)
                {
                    if (plantRecord?.plant?.defName != null &&
                        (plantRecord.plant.defName.Contains("Fish") || plantRecord.plant.defName.Contains("fish")))
                    {
                        fishingPotential += plantRecord.commonality * 100f;
                    }
                }
            }

            return fishingPotential;
        }

        public string Describe(FilterContext context)
        {
            var filters = context.Filters;
            if (filters.FishPopulationImportance == FilterImportance.Ignored)
                return "Any fish population";

            var range = filters.FishPopulationRange;
            string importanceLabel = filters.FishPopulationImportance == FilterImportance.Critical ? " (required)" : " (preferred)";
            return $"Fish population {range.min:F0} - {range.max:F0}{importanceLabel}";
        }

        public float Membership(int tileId, FilterContext context)
        {
            var range = context.Filters.FishPopulationRange;
            var tile = Find.World.grid[tileId];
            if (tile == null) return 0.0f;

            var biome = tile.PrimaryBiome;
            if (biome == null) return 0.0f;

            float fishPopulation = GetFishPopulation(biome);
            return MembershipFunctions.Trapezoid(fishPopulation, range.min, range.max);
        }
    }
}

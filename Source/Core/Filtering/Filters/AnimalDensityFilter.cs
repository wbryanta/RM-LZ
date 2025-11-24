using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by animal density (expected animal spawns per tile area).
    /// Higher density = more hunting opportunities, but also more threats.
    /// </summary>
    public sealed class AnimalDensityFilter : ISiteFilter
    {
        public string Id => "animal_density";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            if (filters.AnimalDensityImportance == FilterImportance.Ignored)
                return inputTiles;

            // K-of-N architecture: Apply() only filters for Critical.
            // Preferred is handled by scoring phase.
            if (filters.AnimalDensityImportance != FilterImportance.Critical)
                return inputTiles;

            var range = filters.AnimalDensityRange;
            var worldGrid = Find.World.grid;

            return inputTiles.Where(id =>
            {
                var tile = worldGrid[id];
                if (tile == null) return false;

                var biome = tile.PrimaryBiome;
                if (biome == null) return false;

                float animalDensity = biome.animalDensity;
                return animalDensity >= range.min && animalDensity <= range.max;
            });
        }

        public string Describe(FilterContext context)
        {
            var filters = context.Filters;
            if (filters.AnimalDensityImportance == FilterImportance.Ignored)
                return "Any animal density";

            var range = filters.AnimalDensityRange;
            string importanceLabel = filters.AnimalDensityImportance == FilterImportance.Critical ? " (required)" : " (preferred)";
            return $"Animal density {range.min:F1} - {range.max:F1}{importanceLabel}";
        }

        public float Membership(int tileId, FilterContext context)
        {
            var range = context.Filters.AnimalDensityRange;
            var tile = Find.World.grid[tileId];
            if (tile == null) return 0.0f;

            var biome = tile.PrimaryBiome;
            if (biome == null) return 0.0f;

            float animalDensity = biome.animalDensity;
            return MembershipFunctions.Trapezoid(animalDensity, range.min, range.max);
        }
    }
}

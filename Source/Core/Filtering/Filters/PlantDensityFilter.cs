using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by plant density (vegetation coverage factor).
    /// Higher density = more wood, better cover, but slower movement.
    /// </summary>
    public sealed class PlantDensityFilter : ISiteFilter
    {
        public string Id => "plant_density";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            if (filters.PlantDensityImportance == FilterImportance.Ignored)
                return inputTiles;

            // K-of-N architecture: Apply() only filters for Critical.
            // Preferred is handled by scoring phase.
            if (filters.PlantDensityImportance != FilterImportance.Critical)
                return inputTiles;

            var range = filters.PlantDensityRange;
            var worldGrid = Find.World.grid;

            return inputTiles.Where(id =>
            {
                var tile = worldGrid[id];
                if (tile == null) return false;

                var biome = tile.PrimaryBiome;
                if (biome == null) return false;

                float plantDensity = biome.plantDensity;
                return plantDensity >= range.min && plantDensity <= range.max;
            });
        }

        public string Describe(FilterContext context)
        {
            var filters = context.Filters;
            if (filters.PlantDensityImportance == FilterImportance.Ignored)
                return "Any plant density";

            var range = filters.PlantDensityRange;
            string importanceLabel = filters.PlantDensityImportance == FilterImportance.Critical ? " (required)" : " (preferred)";
            return $"Plant density {range.min:F1} - {range.max:F1}{importanceLabel}";
        }

        public float Membership(int tileId, FilterContext context)
        {
            var range = context.Filters.PlantDensityRange;
            var tile = Find.World.grid[tileId];
            if (tile == null) return 0.0f;

            var biome = tile.PrimaryBiome;
            if (biome == null) return 0.0f;

            float plantDensity = biome.plantDensity;
            return MembershipFunctions.Trapezoid(plantDensity, range.min, range.max);
        }
    }
}

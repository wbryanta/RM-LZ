using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by forageability (wild plant food availability for gathering).
    /// 0.0 = no forageable plants, 1.0 = abundant wild food.
    /// Note: This is separate from ForageableFoodFilter which filters by specific food types.
    /// </summary>
    public sealed class ForageabilityFilter : ISiteFilter
    {
        public string Id => "forageability";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            if (filters.ForageImportance == FilterImportance.Ignored)
                return inputTiles;

            // K-of-N architecture: Apply() only filters for Critical.
            // Preferred is handled by scoring phase.
            if (filters.ForageImportance != FilterImportance.Critical)
                return inputTiles;

            var range = filters.ForageabilityRange;
            var worldGrid = Find.World.grid;

            return inputTiles.Where(id =>
            {
                var tile = worldGrid[id];
                if (tile == null) return false;

                var biome = tile.PrimaryBiome;
                if (biome == null) return false;

                float forageability = biome.forageability;
                return forageability >= range.min && forageability <= range.max;
            });
        }

        public string Describe(FilterContext context)
        {
            var filters = context.Filters;
            if (filters.ForageImportance == FilterImportance.Ignored)
                return "Any forageability";

            var range = filters.ForageabilityRange;
            string importanceLabel = filters.ForageImportance == FilterImportance.Critical ? " (required)" : " (preferred)";
            return $"Forageability {range.min:F1} - {range.max:F1}{importanceLabel}";
        }

        public float Membership(int tileId, FilterContext context)
        {
            var range = context.Filters.ForageabilityRange;
            var tile = Find.World.grid[tileId];
            if (tile == null) return 0.0f;

            var biome = tile.PrimaryBiome;
            if (biome == null) return 0.0f;

            float forageability = biome.forageability;
            return MembershipFunctions.Trapezoid(forageability, range.min, range.max);
        }
    }
}

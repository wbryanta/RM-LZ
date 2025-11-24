using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by hilliness (Flat, SmallHills, LargeHills, Mountainous).
    /// Hilliness affects base defensibility, construction difficulty, and mining opportunities.
    /// </summary>
    public sealed class HillinessFilter : ISiteFilter
    {
        public string Id => "hilliness";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            var allowed = filters.AllowedHilliness;

            // If all hilliness types are allowed (count == 4), no filtering needed
            if (allowed.Count == 4)
                return inputTiles;

            // K-of-N architecture: Hilliness is always Critical when restricted (count < 4)
            var worldGrid = Find.World.grid;

            return inputTiles.Where(id =>
            {
                var tile = worldGrid[id];
                if (tile == null) return false;
                return allowed.Contains(tile.hilliness);
            });
        }

        public string Describe(FilterContext context)
        {
            var allowed = context.Filters.AllowedHilliness;

            if (allowed.Count == 4)
                return "Any hilliness";

            var labels = allowed.Select(h => h.GetLabel()).ToList();
            return $"Hilliness: {string.Join(", ", labels)}";
        }

        public float Membership(int tileId, FilterContext context)
        {
            var allowed = context.Filters.AllowedHilliness;
            var tile = Find.World.grid[tileId];
            if (tile == null) return 0.0f;

            // Binary membership: 1.0 if allowed, 0.0 if not
            return allowed.Contains(tile.hilliness) ? 1.0f : 0.0f;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by swampiness (0.0 = dry land, 1.0 = fully swampy).
    /// Swampiness affects movement speed and construction difficulty.
    /// </summary>
    public sealed class SwampinessFilter : ISiteFilter
    {
        public string Id => "swampiness";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            if (filters.SwampinessImportance == FilterImportance.Ignored)
                return inputTiles;

            // K-of-N architecture: Apply() only filters for Critical.
            // Preferred is handled by scoring phase.
            if (filters.SwampinessImportance != FilterImportance.Critical)
                return inputTiles;

            var range = filters.SwampinessRange;
            var worldGrid = Find.World.grid;

            return inputTiles.Where(id =>
            {
                var tile = worldGrid[id];
                if (tile == null) return false;
                return tile.swampiness >= range.min && tile.swampiness <= range.max;
            });
        }

        public string Describe(FilterContext context)
        {
            var filters = context.Filters;
            if (filters.SwampinessImportance == FilterImportance.Ignored)
                return "Any swampiness";

            var range = filters.SwampinessRange;
            string importanceLabel = filters.SwampinessImportance == FilterImportance.Critical ? " (required)" : " (preferred)";
            return $"Swampiness {range.min:F1} - {range.max:F1}{importanceLabel}";
        }

        public float Membership(int tileId, FilterContext context)
        {
            var range = context.Filters.SwampinessRange;
            var tile = Find.World.grid[tileId];
            if (tile == null) return 0.0f;

            return MembershipFunctions.Trapezoid(tile.swampiness, range.min, range.max);
        }
    }
}

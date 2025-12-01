using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by elevation (in meters).
    /// Useful for finding high-altitude or low-altitude locations.
    /// </summary>
    public sealed class ElevationFilter : ISiteFilter
    {
        public string Id => "elevation";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            var importance = filters.ElevationImportance;

            // Only hard gates (MustHave/MustNotHave) filter in Apply phase
            if (!importance.IsHardGate())
                return inputTiles;

            var range = filters.ElevationRange;
            var worldGrid = Find.World.grid;

            return inputTiles.Where(id =>
            {
                var tile = worldGrid[id];
                if (tile == null) return false;
                bool inRange = tile.elevation >= range.min && tile.elevation <= range.max;
                return importance == FilterImportance.MustNotHave ? !inRange : inRange;
            });
        }

        public string Describe(FilterContext context)
        {
            var filters = context.Filters;
            if (filters.ElevationImportance == FilterImportance.Ignored)
                return "Any elevation";

            var range = filters.ElevationRange;
            string importanceLabel = filters.ElevationImportance == FilterImportance.Critical ? " (required)" : " (preferred)";
            return $"Elevation {range.min:F0}m - {range.max:F0}m{importanceLabel}";
        }

        public float Membership(int tileId, FilterContext context)
        {
            var range = context.Filters.ElevationRange;
            var tile = Find.World.grid[tileId];
            if (tile == null) return 0.0f;

            return MembershipFunctions.Trapezoid(tile.elevation, range.min, range.max);
        }
    }
}

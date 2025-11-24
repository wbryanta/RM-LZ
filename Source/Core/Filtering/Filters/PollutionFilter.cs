using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by pollution level (0.0 = pristine, 1.0 = heavily polluted).
    /// Pollution affects plant growth, animal populations, and colonist mood.
    /// </summary>
    public sealed class PollutionFilter : ISiteFilter
    {
        public string Id => "pollution";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            if (filters.PollutionImportance == FilterImportance.Ignored)
                return inputTiles;

            // K-of-N architecture: Apply() only filters for Critical.
            // Preferred is handled by scoring phase.
            if (filters.PollutionImportance != FilterImportance.Critical)
                return inputTiles;

            var range = filters.PollutionRange;
            var worldGrid = Find.World.grid;

            return inputTiles.Where(id =>
            {
                var tile = worldGrid[id];
                if (tile == null) return false;
                return tile.pollution >= range.min && tile.pollution <= range.max;
            });
        }

        public string Describe(FilterContext context)
        {
            var filters = context.Filters;
            if (filters.PollutionImportance == FilterImportance.Ignored)
                return "Any pollution level";

            var range = filters.PollutionRange;
            string importanceLabel = filters.PollutionImportance == FilterImportance.Critical ? " (required)" : " (preferred)";
            return $"Pollution {range.min:P0} - {range.max:P0}{importanceLabel}";
        }

        public float Membership(int tileId, FilterContext context)
        {
            var range = context.Filters.PollutionRange;
            var tile = Find.World.grid[tileId];
            if (tile == null) return 0.0f;

            return MembershipFunctions.Trapezoid(tile.pollution, range.min, range.max);
        }
    }
}

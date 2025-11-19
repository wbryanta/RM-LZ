using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by average temperature.
    /// Uses RimWorld's native cached grid data - O(1) access, no computation required.
    /// </summary>
    public sealed class AverageTemperatureFilter : ISiteFilter
    {
        public string Id => "average_temperature";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            if (filters.AverageTemperatureImportance == FilterImportance.Ignored)
                return inputTiles;

            // K-of-N architecture: Apply() only filters for Critical.
            // Preferred is handled by scoring phase.
            if (filters.AverageTemperatureImportance != FilterImportance.Critical)
                return inputTiles;

            var range = filters.AverageTemperatureRange;
            var worldGrid = Find.World.grid;

            return inputTiles.Where(id =>
            {
                var tile = worldGrid[id];
                if (tile == null) return false;
                return tile.temperature >= range.min && tile.temperature <= range.max;
            });
        }

        public string Describe(FilterContext context)
        {
            var filters = context.Filters;
            if (filters.AverageTemperatureImportance == FilterImportance.Ignored)
                return "Any average temperature";

            var range = filters.AverageTemperatureRange;
            string importanceLabel = filters.AverageTemperatureImportance == FilterImportance.Critical ? " (required)" : " (preferred)";
            return $"Average temp {range.min:F0}°C - {range.max:F0}°C{importanceLabel}";
        }

        public float Membership(int tileId, FilterContext context)
        {
            var range = context.Filters.AverageTemperatureRange;
            var tile = Find.World.grid[tileId];
            if (tile == null) return 0.0f;

            return MembershipFunctions.Trapezoid(tile.temperature, range.min, range.max);
        }
    }
}

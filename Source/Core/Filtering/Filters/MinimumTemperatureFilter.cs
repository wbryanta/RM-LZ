using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by minimum (winter) temperature.
    /// </summary>
    public sealed class MinimumTemperatureFilter : ISiteFilter
    {
        public string Id => "minimum_temperature";
        public FilterHeaviness Heaviness => FilterHeaviness.Heavy;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            if (filters.MinimumTemperatureImportance == FilterImportance.Ignored)
                return inputTiles;

            // K-of-N architecture: Apply() only filters for Critical.
            // Preferred is handled by scoring phase.
            if (filters.MinimumTemperatureImportance != FilterImportance.Critical)
                return inputTiles;

            var range = filters.MinimumTemperatureRange;
            var worldGrid = Find.World.grid;

            return inputTiles.Where(id =>
            {
                var tile = worldGrid[id];
                if (tile == null) return false;
                return tile.MinTemperature >= range.min && tile.MinTemperature <= range.max;
            });
        }

        public string Describe(FilterContext context)
        {
            var filters = context.Filters;
            if (filters.MinimumTemperatureImportance == FilterImportance.Ignored)
                return "Any minimum temperature";

            var range = filters.MinimumTemperatureRange;
            string importanceLabel = filters.MinimumTemperatureImportance == FilterImportance.Critical ? " (required)" : " (preferred)";
            return $"Min temp {range.min:F0}°C - {range.max:F0}°C{importanceLabel}";
        }

        public float Membership(int tileId, FilterContext context)
        {
            var range = context.Filters.MinimumTemperatureRange;
            var tile = Find.World.grid[tileId];
            if (tile == null) return 0.0f;

            return MembershipFunctions.Trapezoid(tile.MinTemperature, range.min, range.max);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by maximum (summer) temperature.
    /// Uses cached SurfaceTile.MaxTemperature property (fast, O(1) lookup).
    /// </summary>
    public sealed class MaximumTemperatureFilter : ISiteFilter
    {
        public string Id => "maximum_temperature";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            if (filters.MaximumTemperatureImportance == FilterImportance.Ignored)
                return inputTiles;

            // K-of-N architecture: Apply() only filters for Critical.
            // Preferred is handled by scoring phase.
            if (filters.MaximumTemperatureImportance != FilterImportance.Critical)
                return inputTiles;

            var range = filters.MaximumTemperatureRange;
            var worldGrid = Find.World.grid;

            return inputTiles.Where(id =>
            {
                var tile = worldGrid[id];
                if (tile == null) return false;
                return tile.MaxTemperature >= range.min && tile.MaxTemperature <= range.max;
            });
        }

        public string Describe(FilterContext context)
        {
            var filters = context.Filters;
            if (filters.MaximumTemperatureImportance == FilterImportance.Ignored)
                return "Any maximum temperature";

            var range = filters.MaximumTemperatureRange;
            string importanceLabel = filters.MaximumTemperatureImportance == FilterImportance.Critical ? " (required)" : " (preferred)";
            return $"Max temp {range.min:F0}°C - {range.max:F0}°C{importanceLabel}";
        }

        public float Membership(int tileId, FilterContext context)
        {
            var range = context.Filters.MaximumTemperatureRange;
            var tile = Find.World.grid[tileId];
            if (tile == null) return 0.0f;

            return MembershipFunctions.Trapezoid(tile.MaxTemperature, range.min, range.max);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by minimum (winter) temperature.
    /// Uses cached SurfaceTile.MinTemperature property (fast, O(1) lookup).
    /// </summary>
    public sealed class MinimumTemperatureFilter : ISiteFilter
    {
        public string Id => "minimum_temperature";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            var importance = filters.MinimumTemperatureImportance;

            // Only hard gates (MustHave/MustNotHave) filter in Apply phase
            if (!importance.IsHardGate())
                return inputTiles;

            var range = filters.MinimumTemperatureRange;
            var worldGrid = Find.World.grid;

            return inputTiles.Where(id =>
            {
                var tile = worldGrid[id];
                if (tile == null) return false;
                bool inRange = tile.MinTemperature >= range.min && tile.MinTemperature <= range.max;
                return importance == FilterImportance.MustNotHave ? !inRange : inRange;
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

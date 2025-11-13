using System.Collections.Generic;
using System.Linq;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    public sealed class TemperatureFilter : ISiteFilter
    {
        public string Id => "temperature";
        public FilterHeaviness Heaviness => FilterHeaviness.Medium;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var range = context.State.Preferences.Filters.TemperatureRange;
            var worldGrid = Find.World.grid;
            return inputTiles.Where(id =>
            {
                var tile = worldGrid[id];
                return tile != null && tile.temperature >= range.min && tile.temperature <= range.max;
            });
        }

        public string Describe(FilterContext context)
        {
            var range = context.State.Preferences.Filters.TemperatureRange;
            return $"Temperature between {range.min:F0} and {range.max:F0}";
        }
    }
}

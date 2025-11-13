using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    public sealed class RainfallFilter : ISiteFilter
    {
        public string Id => "rainfall";
        public FilterHeaviness Heaviness => FilterHeaviness.Medium;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var range = context.State.Preferences.Filters.RainfallRange;
            var worldGrid = Find.World.grid;
            return inputTiles.Where(id =>
            {
                var tile = worldGrid[id];
                return tile != null && tile.rainfall >= range.min && tile.rainfall <= range.max;
            });
        }

        public string Describe(FilterContext context)
        {
            var range = context.State.Preferences.Filters.RainfallRange;
            return $"Rainfall between {range.min:F0} and {range.max:F0}";
        }
    }
}

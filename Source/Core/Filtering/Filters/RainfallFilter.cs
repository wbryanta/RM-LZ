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
            var snapshot = context.State.WorldSnapshot;
            return inputTiles.Where(id => snapshot.TryGetInfo(id, out var info) && info.Rainfall >= range.min && info.Rainfall <= range.max);
        }

        public string Describe(FilterContext context)
        {
            var range = context.State.Preferences.Filters.RainfallRange;
            return $"Rainfall between {range.min:F0} and {range.max:F0}";
        }
    }
}

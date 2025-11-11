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
            var snapshot = context.State.WorldSnapshot;
            return inputTiles.Where(id => snapshot.TryGetInfo(id, out var info) && info.Temperature >= range.min && info.Temperature <= range.max);
        }

        public string Describe(FilterContext context)
        {
            var range = context.State.Preferences.Filters.TemperatureRange;
            return $"Temperature between {range.min:F0} and {range.max:F0}";
        }
    }
}

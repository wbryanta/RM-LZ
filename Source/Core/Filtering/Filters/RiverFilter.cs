using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    public sealed class RiverFilter : ISiteFilter
    {
        public string Id => "river";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var importance = context.State.Preferences.Filters.RiverImportance;
            if (importance != FilterImportance.Critical)
                return inputTiles;

            var snapshot = context.State.WorldSnapshot;
            return inputTiles.Where(id => snapshot.TryGetInfo(id, out var info) && info.HasRiver);
        }

        public string Describe(FilterContext context)
        {
            var importance = context.State.Preferences.Filters.RiverImportance;
            return importance switch
            {
                FilterImportance.Critical => "River required",
                FilterImportance.Preferred => "River preferred",
                _ => "River ignored"
            };
        }
    }
}

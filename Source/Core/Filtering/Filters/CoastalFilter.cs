using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    public sealed class CoastalFilter : ISiteFilter
    {
        public string Id => "coastal";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var importance = context.State.Preferences.Filters.CoastalImportance;
            if (importance != FilterImportance.Critical)
                return inputTiles;

            var worldGrid = Find.World.grid;
            return inputTiles.Where(id =>
            {
                var tile = worldGrid[id];
                return tile != null && tile.IsCoastal;
            });
        }

        public string Describe(FilterContext context)
        {
            var importance = context.State.Preferences.Filters.CoastalImportance;
            return importance switch
            {
                FilterImportance.Critical => "Coastal required",
                FilterImportance.Preferred => "Coastal preferred",
                _ => "Coastal ignored"
            };
        }
    }
}

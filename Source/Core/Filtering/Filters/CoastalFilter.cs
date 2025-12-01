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
            var importance = context.Filters.CoastalImportance;

            // Only hard gates (MustHave/MustNotHave) filter in Apply phase
            if (!importance.IsHardGate())
                return inputTiles;

            var worldGrid = Find.World.grid;
            return inputTiles.Where(id =>
            {
                var tile = worldGrid[id];
                if (tile == null) return false;
                bool isCoastal = tile.IsCoastal;
                return importance == FilterImportance.MustNotHave ? !isCoastal : isCoastal;
            });
        }

        public string Describe(FilterContext context)
        {
            var importance = context.Filters.CoastalImportance;
            return importance switch
            {
                FilterImportance.Critical => "Coastal required",
                FilterImportance.Preferred => "Coastal preferred",
                _ => "Coastal ignored"
            };
        }

        public float Membership(int tileId, FilterContext context)
        {
            var tile = Find.World.grid[tileId];
            if (tile == null) return 0.0f;

            // Binary membership: 1.0 if coastal, 0.0 if not
            return MembershipFunctions.Binary(tile.IsCoastal);
        }
    }
}

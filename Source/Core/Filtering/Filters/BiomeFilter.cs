using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    public sealed class BiomeFilter : ISiteFilter
    {
        public string Id => "biome";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var targetBiome = context.Filters.LockedBiome;
            if (targetBiome == null)
                return inputTiles;

            var worldGrid = Find.World.grid;
            return inputTiles.Where(id =>
            {
                var tile = worldGrid[id];
                return tile?.PrimaryBiome == targetBiome;
            });
        }

        public string Describe(FilterContext context)
        {
            return context.Filters.LockedBiome?.LabelCap ?? "Any biome";
        }

        public float Membership(int tileId, FilterContext context)
        {
            var targetBiome = context.Filters.LockedBiome;

            // If no biome locked, no membership constraint
            if (targetBiome == null)
                return 1.0f;

            var tile = Find.World.grid[tileId];
            if (tile == null) return 0.0f;

            // Binary membership: 1.0 if biome matches, 0.0 if not
            bool matches = tile.PrimaryBiome == targetBiome;
            return MembershipFunctions.Binary(matches);
        }
    }
}

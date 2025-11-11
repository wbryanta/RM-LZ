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
            var targetBiome = context.State.Preferences.Filters.LockedBiome;
            if (targetBiome == null)
                return inputTiles;

            var snapshot = context.State.WorldSnapshot;
            return inputTiles.Where(id => snapshot.TryGetInfo(id, out var info) && info.Biome == targetBiome);
        }

        public string Describe(FilterContext context)
        {
            return context.State.Preferences.Filters.LockedBiome?.LabelCap ?? "Any biome";
        }
    }
}

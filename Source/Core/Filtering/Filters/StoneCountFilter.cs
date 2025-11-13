using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by number of available stone types.
    /// Alternative to specific stone selection - checks "any X stone types" present.
    /// Uses Preferred/Critical importance.
    /// </summary>
    public sealed class StoneCountFilter : ISiteFilter
    {
        public string Id => "stone_count";
        public FilterHeaviness Heaviness => FilterHeaviness.Heavy;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.State.Preferences.Filters;

            // Only apply if UseStoneCount is enabled
            if (!filters.UseStoneCount)
                return inputTiles;

            if (filters.StoneImportance == FilterImportance.Ignored)
                return inputTiles;

            // K-of-N architecture: Apply() only filters for Critical.
            // Preferred is handled by scoring phase.
            if (filters.StoneImportance != FilterImportance.Critical)
                return inputTiles;

            var countRange = filters.StoneCountRange;

            return inputTiles.Where(id =>
            {
                int stoneCount = GetStoneTypeCount(id);
                return stoneCount >= countRange.min && stoneCount <= countRange.max;
            });
        }

        public string Describe(FilterContext context)
        {
            var filters = context.State.Preferences.Filters;

            if (!filters.UseStoneCount || filters.StoneImportance == FilterImportance.Ignored)
                return "Stone count not considered";

            var countRange = filters.StoneCountRange;
            string importanceLabel = filters.StoneImportance == FilterImportance.Critical ? " (required)" : " (preferred)";

            if (countRange.min == countRange.max)
                return $"Exactly {countRange.min:F0} stone types{importanceLabel}";

            return $"{countRange.min:F0}-{countRange.max:F0} stone types{importanceLabel}";
        }

        /// <summary>
        /// Gets the number of distinct stone types available on a tile.
        /// This is an expensive operation that queries the world's natural rock types.
        /// </summary>
        private static int GetStoneTypeCount(int tileId)
        {
            var world = Find.World;
            if (world == null)
                return 0;

            var stoneTypes = world.NaturalRockTypesIn(tileId);
            if (stoneTypes == null)
                return 0;

            // Count non-null stone types
            return stoneTypes.Count(s => s != null);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by specific required stone types (e.g., granite, marble).
    /// This filter runs in the Apply phase when StoneImportance is Critical,
    /// preventing the "0 results" issue when expensive stone checks happen too late.
    /// Uses Heavy weight since it requires expensive tile data computation.
    /// </summary>
    public sealed class SpecificStoneFilter : ISiteFilter
    {
        public string Id => "specific_stones";
        public FilterHeaviness Heaviness => FilterHeaviness.Heavy;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.State.Preferences.Filters;

            // Only apply if specific stones are required
            if (filters.RequiredStoneDefNames == null || filters.RequiredStoneDefNames.Count == 0)
                return inputTiles;

            if (filters.StoneImportance == FilterImportance.Ignored)
                return inputTiles;

            // K-of-N architecture: Apply() only filters for Critical.
            // Preferred is handled by scoring phase.
            if (filters.StoneImportance != FilterImportance.Critical)
                return inputTiles;

            var cache = context.TileCache;
            var requiredStones = filters.RequiredStoneDefNames;

            var result = inputTiles.Where(id =>
            {
                var extended = cache.GetOrCompute(id);
                if (extended.StoneDefNames == null || extended.StoneDefNames.Length == 0)
                    return false;

                // Check if ALL required stones are present
                foreach (var requiredStone in requiredStones)
                {
                    if (!extended.StoneDefNames.Contains(requiredStone))
                        return false;
                }

                return true;
            }).ToList();

            return result;
        }

        public string Describe(FilterContext context)
        {
            var filters = context.State.Preferences.Filters;

            if (filters.RequiredStoneDefNames == null ||
                filters.RequiredStoneDefNames.Count == 0 ||
                filters.StoneImportance == FilterImportance.Ignored)
            {
                return "Specific stones not required";
            }

            string importanceLabel = filters.StoneImportance == FilterImportance.Critical ? " (required)" : " (preferred)";
            var stoneNames = string.Join(", ", filters.RequiredStoneDefNames);
            return $"Stones: {stoneNames}{importanceLabel}";
        }
    }
}

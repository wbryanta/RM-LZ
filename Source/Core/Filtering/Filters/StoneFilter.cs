using System.Collections.Generic;
using System.Linq;
using LandingZone.Core;
using LandingZone.Data;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by specific stone/mineral types.
    /// Uses MineralStockpileCache to resolve MineralRich tiles to specific rock types.
    /// Supports multi-select with AND/OR logic and Preferred/Critical importance.
    /// </summary>
    public sealed class StoneFilter : ISiteFilter
    {
        public string Id => "stone";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;  // Cache is pre-computed

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            var stones = filters.Stones;

            // If no stones configured, pass all tiles through
            if (!stones.HasAnyImportance)
                return inputTiles;

            // If no Critical stones, pass all tiles through (Preferred handled by scoring)
            if (!stones.HasCritical)
                return inputTiles;

            var cache = context.State.MineralStockpileCache;

            // Filter for tiles that meet Critical stone requirements
            return inputTiles.Where(id =>
            {
                var tileStones = cache.GetMineralTypes(id);
                if (tileStones == null || tileStones.Count == 0)
                    return false;  // No minerals = doesn't meet Critical requirement

                // Check if this tile's stones meet Critical requirements
                return stones.MeetsCriticalRequirements(tileStones);
            });
        }

        public string Describe(FilterContext context)
        {
            var filters = context.Filters;
            var stones = filters.Stones;

            if (!stones.HasAnyImportance)
                return "Stones not configured";

            var parts = new List<string>();
            var criticalCount = stones.CountByImportance(FilterImportance.Critical);
            var preferredCount = stones.CountByImportance(FilterImportance.Preferred);

            if (criticalCount > 0)
                parts.Add($"{criticalCount} required");
            if (preferredCount > 0)
                parts.Add($"{preferredCount} preferred");

            return $"Stones: {string.Join(", ", parts)}";
        }

        public float Membership(int tileId, FilterContext context)
        {
            var stones = context.Filters.Stones;

            // If no stones configured, no membership
            if (!stones.HasAnyImportance)
                return 0.0f;

            var cache = context.State.MineralStockpileCache;
            var tileStones = cache.GetMineralTypes(tileId);

            if (tileStones == null || tileStones.Count == 0)
                return 0.0f; // No minerals on this tile

            // Count matches for scoring
            int criticalMatches = 0;
            int criticalTotal = stones.CountByImportance(FilterImportance.Critical);
            int preferredMatches = 0;
            int preferredTotal = stones.CountByImportance(FilterImportance.Preferred);

            foreach (var stone in tileStones)
            {
                var importance = stones.GetImportance(stone);
                if (importance == FilterImportance.Critical)
                    criticalMatches++;
                else if (importance == FilterImportance.Preferred)
                    preferredMatches++;
            }

            // Align with Apply phase: prioritize Critical requirements
            if (criticalTotal > 0)
            {
                // Use operator logic
                if (stones.Operator == ImportanceOperator.OR)
                {
                    // OR: Any match is good (1.0), no match is bad (0.0)
                    return criticalMatches > 0 ? 1.0f : 0.0f;
                }
                else
                {
                    // AND: Score by coverage
                    return criticalTotal > 0 ? (float)criticalMatches / criticalTotal : 0.0f;
                }
            }

            // Only Preferred stones configured
            if (preferredTotal > 0)
            {
                if (stones.Operator == ImportanceOperator.OR)
                {
                    return preferredMatches > 0 ? 1.0f : 0.0f;
                }
                else
                {
                    return (float)preferredMatches / preferredTotal;
                }
            }

            return 0.0f;
        }
    }
}

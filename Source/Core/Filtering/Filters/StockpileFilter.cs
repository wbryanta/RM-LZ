using System.Collections.Generic;
using System.Linq;
using LandingZone.Core;
using LandingZone.Data;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by stockpile contents (abandoned supply caches).
    /// Uses MineralStockpileCache to resolve Stockpile mutator tiles to specific loot types.
    /// Supports multi-select with AND/OR logic and Preferred/Critical importance.
    ///
    /// Status: STUB - Pending reflection investigation to discover stockpile content API.
    /// See docs/stockpile_scoring_design.md for implementation plan.
    /// </summary>
    public sealed class StockpileFilter : ISiteFilter
    {
        public string Id => "stockpile";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;  // Cache is pre-computed

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            var stockpiles = filters.Stockpiles;

            // If no stockpiles configured, pass all tiles through
            if (!stockpiles.HasAnyImportance)
                return inputTiles;

            // If no Critical stockpiles, pass all tiles through (Preferred handled by scoring)
            if (!stockpiles.HasCritical)
                return inputTiles;

            var cache = context.State.MineralStockpileCache;

            // Filter for tiles that meet Critical stockpile requirements
            return inputTiles.Where(id =>
            {
                var tileStockpiles = cache.GetStockpileTypes(id);
                if (tileStockpiles == null || tileStockpiles.Count == 0)
                    return false;  // No stockpiles = doesn't meet Critical requirement

                // Check if this tile's stockpiles meet Critical requirements
                return stockpiles.MeetsCriticalRequirements(tileStockpiles);
            });
        }

        public string Describe(FilterContext context)
        {
            var filters = context.Filters;
            var stockpiles = filters.Stockpiles;

            if (!stockpiles.HasAnyImportance)
                return "Stockpiles not configured";

            var parts = new List<string>();
            var criticalCount = stockpiles.CountByImportance(FilterImportance.Critical);
            var preferredCount = stockpiles.CountByImportance(FilterImportance.Preferred);

            if (criticalCount > 0)
                parts.Add($"{criticalCount} required");
            if (preferredCount > 0)
                parts.Add($"{preferredCount} preferred");

            return $"Stockpiles: {string.Join(", ", parts)}";
        }

        public float Membership(int tileId, FilterContext context)
        {
            var stockpiles = context.Filters.Stockpiles;

            // If no stockpiles configured, no membership
            if (!stockpiles.HasAnyImportance)
                return 0.0f;

            var cache = context.State.MineralStockpileCache;
            var tileStockpiles = cache.GetStockpileTypes(tileId);

            if (tileStockpiles == null || tileStockpiles.Count == 0)
                return 0.0f; // No stockpiles on this tile

            // Count matches for scoring
            int criticalMatches = 0;
            int criticalTotal = stockpiles.CountByImportance(FilterImportance.Critical);
            int preferredMatches = 0;
            int preferredTotal = stockpiles.CountByImportance(FilterImportance.Preferred);

            foreach (var stockpile in tileStockpiles)
            {
                var importance = stockpiles.GetImportance(stockpile);
                if (importance == FilterImportance.Critical)
                    criticalMatches++;
                else if (importance == FilterImportance.Preferred)
                    preferredMatches++;
            }

            // Align with Apply phase: prioritize Critical requirements
            if (criticalTotal > 0)
            {
                // Use operator logic
                if (stockpiles.Operator == ImportanceOperator.OR)
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

            // Only Preferred stockpiles configured
            if (preferredTotal > 0)
            {
                if (stockpiles.Operator == ImportanceOperator.OR)
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

        /// <summary>
        /// Maps stockpile types to quality ratings for scoring contributions.
        /// Based on strategic value of starting resources.
        ///
        /// Enum values from TileMutatorWorker_Stockpile.StockpileType:
        /// - Chemfuel, Component, Drugs, Gravcore, Medicine, Weapons
        /// </summary>
        public static int GetStockpileQuality(string stockpileType)
        {
            // Match exact enum names from TileMutatorWorker_Stockpile.StockpileType
            return stockpileType switch
            {
                "Gravcore" => 9,   // Ultra-rare end-game material (Anomaly DLC)
                "Weapons" => 8,     // High-value armory start (guns, melee weapons)
                "Medicine" => 7,    // Survival-critical (glitterworld meds)
                "Chemfuel" => 6,    // Power generation + trading commodity
                "Component" => 5,   // Industrial progression bottleneck
                "Drugs" => 4,       // Medical/recreation/trading value
                _ => 4              // Unknown type gets generic bonus
            };
        }
    }
}

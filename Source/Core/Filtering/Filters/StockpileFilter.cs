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

            // Only hard gates (MustHave/MustNotHave) filter in Apply phase
            if (!stockpiles.HasHardGates)
                return inputTiles;

            var cache = context.State.MineralStockpileCache;

            // Filter for tiles that meet hard gate requirements (MustHave AND MustNotHave)
            return inputTiles.Where(id =>
            {
                var tileStockpiles = cache.GetStockpileTypes(id);
                // Note: Empty list is valid for MeetsHardGateRequirements (no MustHave will fail, no MustNotHave is ok)
                var stockpileList = tileStockpiles ?? new List<string>();

                return stockpiles.MeetsHardGateRequirements(stockpileList);
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
            // For tiles with no stockpiles, use empty list for scoring
            var stockpileList = (IEnumerable<string>)(tileStockpiles ?? new List<string>());

            // Align with Apply phase: prioritize Critical (MustHave) requirements
            if (stockpiles.HasCritical)
            {
                // Use GetCriticalSatisfaction to respect AND/OR operator
                float satisfaction = stockpiles.GetCriticalSatisfaction(stockpileList);
                return satisfaction;
            }

            // Priority OR Preferred: Use GetScoringScore for weighted scoring (Priority=2x, Preferred=1x)
            if (stockpiles.HasPriority || stockpiles.HasPreferred)
            {
                float score = stockpiles.GetScoringScore(stockpileList);
                // Normalize to [0,1] range based on maximum possible score
                int maxScore = stockpiles.CountByImportance(FilterImportance.Priority) * 2
                             + stockpiles.CountByImportance(FilterImportance.Preferred);
                float membership = maxScore > 0 ? UnityEngine.Mathf.Clamp01(score / maxScore) : 0f;
                return membership;
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

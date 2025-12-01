using System.Collections.Generic;
using System.Linq;
using LandingZone.Core;
using LandingZone.Data;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by specific mineral ore types found on MineralRich tiles.
    /// Uses MineralStockpileCache to resolve ore types per tile.
    /// Supports multi-select with AND/OR logic and Preferred/Critical importance.
    /// </summary>
    public sealed class MineralOresFilter : ISiteFilter
    {
        public string Id => "mineral_ores";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;  // Cache is pre-computed

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            var mineralOres = filters.MineralOres;

            // If no ores configured, pass all tiles through
            if (!mineralOres.HasAnyImportance)
                return inputTiles;

            // Only hard gates (MustHave/MustNotHave) filter in Apply phase
            if (!mineralOres.HasHardGates)
                return inputTiles;

            var cache = context.State.MineralStockpileCache;

            // Filter for tiles that meet hard gate requirements (MustHave AND MustNotHave)
            return inputTiles.Where(id =>
            {
                var tileOres = cache.GetMineralTypes(id);
                // Note: Empty list is valid for MeetsHardGateRequirements (no MustHave will fail, no MustNotHave is ok)
                var oreList = tileOres ?? new List<string>();

                return mineralOres.MeetsHardGateRequirements(oreList);
            });
        }

        public string Describe(FilterContext context)
        {
            var filters = context.Filters;
            var mineralOres = filters.MineralOres;

            if (!mineralOres.HasAnyImportance)
                return "Mineral ores not configured";

            var parts = new List<string>();
            var criticalCount = mineralOres.CountByImportance(FilterImportance.Critical);
            var preferredCount = mineralOres.CountByImportance(FilterImportance.Preferred);

            if (criticalCount > 0)
                parts.Add($"{criticalCount} required");
            if (preferredCount > 0)
                parts.Add($"{preferredCount} preferred");

            return $"Mineral Ores: {string.Join(", ", parts)}";
        }

        public float Membership(int tileId, FilterContext context)
        {
            var mineralOres = context.Filters.MineralOres;

            // If no ores configured, no membership
            if (!mineralOres.HasAnyImportance)
                return 0.0f;

            var cache = context.State.MineralStockpileCache;
            var tileOres = cache.GetMineralTypes(tileId);
            // For tiles with no ores, use empty list for scoring
            var oreList = (IEnumerable<string>)(tileOres ?? new List<string>());

            // Align with Apply phase: prioritize Critical (MustHave) requirements
            if (mineralOres.HasCritical)
            {
                // Use GetCriticalSatisfaction to respect AND/OR operator
                float satisfaction = mineralOres.GetCriticalSatisfaction(oreList);
                return satisfaction;
            }

            // Priority OR Preferred: Use GetScoringScore for weighted scoring (Priority=2x, Preferred=1x)
            if (mineralOres.HasPriority || mineralOres.HasPreferred)
            {
                float score = mineralOres.GetScoringScore(oreList);
                // Normalize to [0,1] range based on maximum possible score
                int maxScore = mineralOres.CountByImportance(FilterImportance.Priority) * 2
                             + mineralOres.CountByImportance(FilterImportance.Preferred);
                float membership = maxScore > 0 ? UnityEngine.Mathf.Clamp01(score / maxScore) : 0f;
                return membership;
            }

            return 0.0f;
        }
    }
}

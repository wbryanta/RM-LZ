using System.Collections.Generic;
using System.Linq;
using LandingZone.Core;
using LandingZone.Data;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by specific plant species found on PlantGrove/WildPlants tiles.
    /// Uses MineralStockpileCache to resolve plant species per tile.
    /// Supports multi-select with AND/OR logic and Preferred/Critical importance.
    /// </summary>
    public sealed class PlantGroveFilter : ISiteFilter
    {
        public string Id => "plant_grove";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;  // Cache is pre-computed

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            var plantGrove = filters.PlantGrove;

            // If no plants configured, pass all tiles through
            if (!plantGrove.HasAnyImportance)
                return inputTiles;

            // Only hard gates (MustHave/MustNotHave) filter in Apply phase
            if (!plantGrove.HasHardGates)
                return inputTiles;

            var cache = context.State.MineralStockpileCache;

            // Filter for tiles that meet hard gate requirements (MustHave AND MustNotHave)
            return inputTiles.Where(id =>
            {
                var tilePlants = cache.GetPlantSpecies(id);
                // Note: Empty list is valid for MeetsHardGateRequirements (no MustHave will fail, no MustNotHave is ok)
                var plantList = tilePlants ?? new List<string>();

                return plantGrove.MeetsHardGateRequirements(plantList);
            });
        }

        public string Describe(FilterContext context)
        {
            var filters = context.Filters;
            var plantGrove = filters.PlantGrove;

            if (!plantGrove.HasAnyImportance)
                return "Plant grove not configured";

            var parts = new List<string>();
            var criticalCount = plantGrove.CountByImportance(FilterImportance.Critical);
            var preferredCount = plantGrove.CountByImportance(FilterImportance.Preferred);

            if (criticalCount > 0)
                parts.Add($"{criticalCount} required");
            if (preferredCount > 0)
                parts.Add($"{preferredCount} preferred");

            return $"Plant Grove: {string.Join(", ", parts)}";
        }

        public float Membership(int tileId, FilterContext context)
        {
            var plantGrove = context.Filters.PlantGrove;

            // If no plants configured, no membership
            if (!plantGrove.HasAnyImportance)
                return 0.0f;

            var cache = context.State.MineralStockpileCache;
            var tilePlants = cache.GetPlantSpecies(tileId);
            // For tiles with no plants, use empty list for scoring
            var plantList = (IEnumerable<string>)(tilePlants ?? new List<string>());

            // Align with Apply phase: prioritize Critical (MustHave) requirements
            if (plantGrove.HasCritical)
            {
                // Use GetCriticalSatisfaction to respect AND/OR operator
                float satisfaction = plantGrove.GetCriticalSatisfaction(plantList);
                return satisfaction;
            }

            // Priority OR Preferred: Use GetScoringScore for weighted scoring (Priority=2x, Preferred=1x)
            if (plantGrove.HasPriority || plantGrove.HasPreferred)
            {
                float score = plantGrove.GetScoringScore(plantList);
                // Normalize to [0,1] range based on maximum possible score
                int maxScore = plantGrove.CountByImportance(FilterImportance.Priority) * 2
                             + plantGrove.CountByImportance(FilterImportance.Preferred);
                float membership = maxScore > 0 ? UnityEngine.Mathf.Clamp01(score / maxScore) : 0f;
                return membership;
            }

            return 0.0f;
        }
    }
}

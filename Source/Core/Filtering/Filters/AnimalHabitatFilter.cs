using System.Collections.Generic;
using System.Linq;
using LandingZone.Core;
using LandingZone.Data;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by specific animal species found on AnimalHabitat tiles.
    /// Uses MineralStockpileCache to resolve animal species per tile.
    /// Supports multi-select with AND/OR logic and Preferred/Critical importance.
    /// </summary>
    public sealed class AnimalHabitatFilter : ISiteFilter
    {
        public string Id => "animal_habitat";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;  // Cache is pre-computed

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            var animalHabitat = filters.AnimalHabitat;

            // If no animals configured, pass all tiles through
            if (!animalHabitat.HasAnyImportance)
                return inputTiles;

            // Only hard gates (MustHave/MustNotHave) filter in Apply phase
            if (!animalHabitat.HasHardGates)
                return inputTiles;

            var cache = context.State.MineralStockpileCache;

            // Filter for tiles that meet hard gate requirements (MustHave AND MustNotHave)
            return inputTiles.Where(id =>
            {
                var tileAnimals = cache.GetAnimalSpecies(id);
                // Note: Empty list is valid for MeetsHardGateRequirements (no MustHave will fail, no MustNotHave is ok)
                var animalList = tileAnimals ?? new List<string>();

                return animalHabitat.MeetsHardGateRequirements(animalList);
            });
        }

        public string Describe(FilterContext context)
        {
            var filters = context.Filters;
            var animalHabitat = filters.AnimalHabitat;

            if (!animalHabitat.HasAnyImportance)
                return "Animal habitat not configured";

            var parts = new List<string>();
            var criticalCount = animalHabitat.CountByImportance(FilterImportance.Critical);
            var preferredCount = animalHabitat.CountByImportance(FilterImportance.Preferred);

            if (criticalCount > 0)
                parts.Add($"{criticalCount} required");
            if (preferredCount > 0)
                parts.Add($"{preferredCount} preferred");

            return $"Animal Habitat: {string.Join(", ", parts)}";
        }

        public float Membership(int tileId, FilterContext context)
        {
            var animalHabitat = context.Filters.AnimalHabitat;

            // If no animals configured, no membership
            if (!animalHabitat.HasAnyImportance)
                return 0.0f;

            var cache = context.State.MineralStockpileCache;
            var tileAnimals = cache.GetAnimalSpecies(tileId);
            // For tiles with no animals, use empty list for scoring
            var animalList = (IEnumerable<string>)(tileAnimals ?? new List<string>());

            // Align with Apply phase: prioritize Critical (MustHave) requirements
            if (animalHabitat.HasCritical)
            {
                // Use GetCriticalSatisfaction to respect AND/OR operator
                float satisfaction = animalHabitat.GetCriticalSatisfaction(animalList);
                return satisfaction;
            }

            // Priority OR Preferred: Use GetScoringScore for weighted scoring (Priority=2x, Preferred=1x)
            if (animalHabitat.HasPriority || animalHabitat.HasPreferred)
            {
                float score = animalHabitat.GetScoringScore(animalList);
                // Normalize to [0,1] range based on maximum possible score
                int maxScore = animalHabitat.CountByImportance(FilterImportance.Priority) * 2
                             + animalHabitat.CountByImportance(FilterImportance.Preferred);
                float membership = maxScore > 0 ? UnityEngine.Mathf.Clamp01(score / maxScore) : 0f;
                return membership;
            }

            return 0.0f;
        }
    }
}

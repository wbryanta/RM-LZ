using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by road presence and specific road types.
    /// Supports individual importance per road type (Critical/Preferred/Ignored).
    /// </summary>
    public sealed class RoadFilter : ISiteFilter
    {
        public string Id => "road";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            var roads = filters.Roads;

            // If no roads configured, pass all tiles through
            if (!roads.HasAnyImportance)
            {
                if (Prefs.DevMode)
                    Log.Message($"[LandingZone][RoadFilter] No road importance configured, passing all tiles");
                return inputTiles;
            }

            // Only hard gates (MustHave/MustNotHave) filter in Apply phase
            if (!roads.HasHardGates)
            {
                if (Prefs.DevMode)
                    Log.Message($"[LandingZone][RoadFilter] No hard gates (MustHave/MustNotHave), passing all tiles. HasPriority={roads.HasPriority}, HasPreferred={roads.HasPreferred}");
                return inputTiles;
            }

            // Debug: Log the road filter configuration
            if (Prefs.DevMode)
            {
                var mustHaves = roads.GetMustHaveItems().ToList();
                var mustNotHaves = roads.GetMustNotHaveItems().ToList();
                Log.Message($"[LandingZone][RoadFilter] Applying hard gates: MustHave=[{string.Join(", ", mustHaves)}] ({roads.Operator}), MustNotHave=[{string.Join(", ", mustNotHaves)}]");
            }

            int inputCount = 0;
            int matchCount = 0;

            // Filter for tiles that meet hard gate requirements (MustHave AND MustNotHave)
            var result = inputTiles.Where(id =>
            {
                inputCount++;
                var roadDefs = GetTileRoadDefs(id).ToList();
                // Note: If no roads, use empty list for requirements checking
                var tileRoads = roadDefs.Select(r => r.defName).ToList();

                bool meets = roads.MeetsHardGateRequirements(tileRoads);

                // Log first few matches/non-matches in dev mode
                if (Prefs.DevMode && inputCount <= 5)
                {
                    Log.Message($"[LandingZone][RoadFilter] Tile {id}: Roads=[{string.Join(", ", tileRoads)}], Meets={meets}");
                }

                if (meets) matchCount++;
                return meets;
            }).ToList();

            if (Prefs.DevMode)
                Log.Message($"[LandingZone][RoadFilter] Result: {matchCount}/{inputCount} tiles passed road filter");

            return result;
        }

        public string Describe(FilterContext context)
        {
            var filters = context.Filters;
            var roads = filters.Roads;

            if (!roads.HasAnyImportance)
                return "Roads not configured";

            var parts = new List<string>();
            var criticalCount = roads.CountByImportance(FilterImportance.Critical);
            var preferredCount = roads.CountByImportance(FilterImportance.Preferred);

            if (criticalCount > 0)
                parts.Add($"{criticalCount} required");
            if (preferredCount > 0)
                parts.Add($"{preferredCount} preferred");

            return $"Roads: {string.Join(", ", parts)}";
        }

        /// <summary>
        /// Gets all RoadDefs present on a specific tile.
        /// </summary>
        private static IEnumerable<RoadDef> GetTileRoadDefs(int tileId)
        {
            var tile = Find.WorldGrid?[tileId];
            if (tile?.Roads == null || tile.Roads.Count == 0)
                yield break;

            // Roads in RimWorld are stored on tile.Roads (List<Tile.RoadLink>)
            // Each RoadLink has a RoadDef
            foreach (var roadLink in tile.Roads)
            {
                if (roadLink.road != null)
                    yield return roadLink.road;
            }
        }

        /// <summary>
        /// Gets all available road types in the game.
        /// Useful for UI dropdown population.
        /// </summary>
        public static IEnumerable<RoadDef> GetAllRoadTypes()
        {
            return DefDatabase<RoadDef>.AllDefsListForReading
                .Where(r => r.priority > 0) // Filter out "no road"
                .OrderBy(r => r.priority); // Order by priority (dirt road -> stone road -> asphalt road)
        }

        public float Membership(int tileId, FilterContext context)
        {
            var roads = context.Filters.Roads;

            // If no roads configured, no membership
            if (!roads.HasAnyImportance)
                return 0.0f;

            var roadDefs = GetTileRoadDefs(tileId).ToList();
            // For tiles with no roads, use empty list for scoring
            var tileRoads = roadDefs?.Select(r => r.defName).ToList() ?? new List<string>();

            // Align with Apply phase: prioritize Critical (MustHave) requirements
            if (roads.HasCritical)
            {
                // Use GetCriticalSatisfaction to respect AND/OR operator
                float satisfaction = roads.GetCriticalSatisfaction(tileRoads);
                return satisfaction;
            }

            // Priority OR Preferred: Use GetScoringScore for weighted scoring (Priority=2x, Preferred=1x)
            if (roads.HasPriority || roads.HasPreferred)
            {
                float score = roads.GetScoringScore(tileRoads);
                // Normalize to [0,1] range based on maximum possible score
                int maxScore = roads.CountByImportance(FilterImportance.Priority) * 2
                             + roads.CountByImportance(FilterImportance.Preferred);
                float membership = maxScore > 0 ? UnityEngine.Mathf.Clamp01(score / maxScore) : 0f;
                return membership;
            }

            return 0.0f;
        }
    }
}

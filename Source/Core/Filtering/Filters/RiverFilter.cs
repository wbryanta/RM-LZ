#nullable enable
using System.Collections.Generic;
using System.Linq;
using LandingZone.Core;
using LandingZone.Data;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by river presence and specific river types.
    /// Supports multi-select with AND/OR logic and Preferred/Critical importance.
    /// </summary>
    public sealed class RiverFilter : ISiteFilter
    {
        public string Id => "river";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            var rivers = filters.Rivers;

            // If no rivers configured, pass all tiles through
            if (!rivers.HasAnyImportance)
                return inputTiles;

            // Only hard gates (MustHave/MustNotHave) filter in Apply phase
            if (!rivers.HasHardGates)
                return inputTiles;

            // Filter for tiles that meet hard gate requirements (MustHave AND MustNotHave)
            return inputTiles.Where(id =>
            {
                var riverDef = GetTileRiverDef(id);
                // Note: If no river, use empty list for requirements checking
                var tileRivers = riverDef != null ? new[] { riverDef.defName } : System.Array.Empty<string>();

                return rivers.MeetsHardGateRequirements(tileRivers);
            });
        }

        public string Describe(FilterContext context)
        {
            var filters = context.Filters;
            var rivers = filters.Rivers;

            if (!rivers.HasAnyImportance)
                return "Rivers not configured";

            var parts = new List<string>();
            var criticalCount = rivers.CountByImportance(FilterImportance.Critical);
            var preferredCount = rivers.CountByImportance(FilterImportance.Preferred);

            if (criticalCount > 0)
                parts.Add($"{criticalCount} required");
            if (preferredCount > 0)
                parts.Add($"{preferredCount} preferred");

            return $"Rivers: {string.Join(", ", parts)}";
        }

        /// <summary>
        /// Gets the RiverDef for a specific tile, if any.
        /// </summary>
        private static RiverDef? GetTileRiverDef(int tileId)
        {
            var tile = Find.WorldGrid?[tileId];
            if (tile == null)
                return null;

            // Rivers in RimWorld are stored on tile.Rivers (List<Tile.RiverLink>)
            // Each RiverLink has a RiverDef
            if (tile.Rivers != null && tile.Rivers.Count > 0)
            {
                // Return the largest river on this tile (rivers are sorted by size)
                return tile.Rivers[0].river;
            }

            return null;
        }

        /// <summary>
        /// Gets all available river types in the game.
        /// Useful for UI dropdown population.
        /// </summary>
        public static IEnumerable<RiverDef> GetAllRiverTypes()
        {
            return DefDatabase<RiverDef>.AllDefsListForReading
                .Where(r => r.degradeThreshold > 0) // Filter out "no river"
                .OrderBy(r => r.degradeThreshold); // Order by size (stream -> river -> huge river)
        }

        public float Membership(int tileId, FilterContext context)
        {
            var rivers = context.Filters.Rivers;

            // If no rivers configured, no membership
            if (!rivers.HasAnyImportance)
                return 0.0f;

            var riverDef = GetTileRiverDef(tileId);
            // For tiles with no river, use empty array for scoring
            var tileRivers = riverDef != null ? new[] { riverDef.defName } : System.Array.Empty<string>();

            // Align with Apply phase: prioritize Critical (MustHave) requirements
            if (rivers.HasCritical)
            {
                // Use GetCriticalSatisfaction to respect AND/OR operator
                float satisfaction = rivers.GetCriticalSatisfaction(tileRivers);

                if (Prefs.DevMode && LandingZoneLogger.IsVerbose && tileId < 10)
                {
                    LandingZoneLogger.LogVerbose($"[LandingZone] RiverFilter.Membership(tile={tileId}): river={riverDef?.defName ?? "none"}");
                    LandingZoneLogger.LogVerbose($"  Critical rivers: [{string.Join(", ", rivers.GetCriticalItems())}], Operator={rivers.Operator}");
                    LandingZoneLogger.LogVerbose($"  → Satisfaction: {satisfaction:F2}");
                }

                return satisfaction;
            }

            // Priority OR Preferred: Use GetScoringScore for weighted scoring (Priority=2x, Preferred=1x)
            if (rivers.HasPriority || rivers.HasPreferred)
            {
                float score = rivers.GetScoringScore(tileRivers);
                // Normalize to [0,1] range based on maximum possible score
                int maxScore = rivers.CountByImportance(FilterImportance.Priority) * 2
                             + rivers.CountByImportance(FilterImportance.Preferred);
                float membership = maxScore > 0 ? UnityEngine.Mathf.Clamp01(score / maxScore) : 0f;

                if (Prefs.DevMode && LandingZoneLogger.IsVerbose && tileId < 10)
                {
                    LandingZoneLogger.LogVerbose($"[LandingZone] RiverFilter.Membership(tile={tileId}): river={riverDef?.defName ?? "none"}");
                    LandingZoneLogger.LogVerbose($"  Priority rivers: [{string.Join(", ", rivers.GetPriorityItems())}]");
                    LandingZoneLogger.LogVerbose($"  Preferred rivers: [{string.Join(", ", rivers.GetPreferredItems())}]");
                    LandingZoneLogger.LogVerbose($"  → Score={score:F2}, MaxScore={maxScore}, Membership={membership:F2}");
                }

                return membership;
            }

            return 0.0f;
        }
    }
}

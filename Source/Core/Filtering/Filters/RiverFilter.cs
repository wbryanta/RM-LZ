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

            // If no Critical rivers, pass all tiles through (Preferred handled by scoring)
            if (!rivers.HasCritical)
                return inputTiles;

            // Filter for tiles that meet Critical river requirements
            return inputTiles.Where(id =>
            {
                var riverDef = GetTileRiverDef(id);
                if (riverDef == null)
                    return false;  // No river = doesn't meet Critical requirement

                // Check if this river type meets Critical requirements
                var tileRivers = new[] { riverDef.defName };
                return rivers.MeetsCriticalRequirements(tileRivers);
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
        private static RiverDef GetTileRiverDef(int tileId)
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
            if (riverDef == null)
                return 0.0f; // No river on this tile

            // Align with Apply phase: prioritize Critical requirements
            if (rivers.HasCritical)
            {
                // Use GetCriticalSatisfaction to respect AND/OR operator
                var tileRivers = new[] { riverDef.defName };
                float satisfaction = rivers.GetCriticalSatisfaction(tileRivers);

                if (Prefs.DevMode && LandingZoneLogger.IsVerbose && tileId < 10)
                {
                    LandingZoneLogger.LogVerbose($"[LandingZone] RiverFilter.Membership(tile={tileId}): river={riverDef.defName}");
                    LandingZoneLogger.LogVerbose($"  Critical rivers: [{string.Join(", ", rivers.GetCriticalItems())}], Operator={rivers.Operator}");
                    LandingZoneLogger.LogVerbose($"  → Satisfaction: {satisfaction:F2}");
                }

                return satisfaction;
            }

            // No Critical rivers, check Preferred (binary: 1.0 if river is Preferred)
            if (rivers.HasPreferred)
            {
                var importance = rivers.GetImportance(riverDef.defName);

                if (Prefs.DevMode && LandingZoneLogger.IsVerbose && tileId < 10)
                {
                    LandingZoneLogger.LogVerbose($"[LandingZone] RiverFilter.Membership(tile={tileId}): river={riverDef.defName}");
                    LandingZoneLogger.LogVerbose($"  Preferred rivers: [{string.Join(", ", rivers.GetPreferredItems())}]");
                    LandingZoneLogger.LogVerbose($"  → Importance={importance}, returning {(importance == FilterImportance.Preferred ? "1.0" : "0.0")}");
                }

                return importance == FilterImportance.Preferred ? 1.0f : 0.0f;
            }

            return 0.0f;
        }
    }
}

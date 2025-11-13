using System.Collections.Generic;
using System.Linq;
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
            var filters = context.State.Preferences.Filters;
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
            var filters = context.State.Preferences.Filters;
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
    }
}

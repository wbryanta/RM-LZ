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
                return inputTiles;

            // If no Critical roads, pass all tiles through (Preferred handled by scoring)
            if (!roads.HasCritical)
                return inputTiles;

            // Filter for tiles that meet Critical road requirements
            return inputTiles.Where(id =>
            {
                var roadDefs = GetTileRoadDefs(id);
                if (roadDefs == null || !roadDefs.Any())
                    return false;  // No roads = doesn't meet Critical requirement

                // Check if this tile's roads meet Critical requirements
                var tileRoads = roadDefs.Select(r => r.defName);
                return roads.MeetsCriticalRequirements(tileRoads);
            });
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

            var roadDefs = GetTileRoadDefs(tileId);
            if (roadDefs == null || !roadDefs.Any())
                return 0.0f; // No roads on this tile

            // Check if ANY of this tile's roads are selected (have any importance)
            foreach (var roadDef in roadDefs)
            {
                var importance = roads.GetImportance(roadDef.defName);
                if (importance != FilterImportance.Ignored)
                    return 1.0f; // At least one selected road present
            }

            return 0.0f;
        }
    }
}

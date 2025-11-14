using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by adjacency to lakes (non-ocean water).
    /// Uses tri-state logic: On (must be lakeside), Off (ignored), Partial (preferred).
    /// </summary>
    public sealed class CoastalLakeFilter : ISiteFilter
    {
        public string Id => "coastal_lake";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.State.Preferences.Filters;
            if (filters.CoastalLakeImportance == FilterImportance.Ignored)
                return inputTiles;

            // K-of-N architecture: Apply() only filters for Critical.
            // Preferred is handled by scoring phase.
            if (filters.CoastalLakeImportance != FilterImportance.Critical)
                return inputTiles;

            return inputTiles.Where(id => TileIsAdjacentToLake(id));
        }

        public string Describe(FilterContext context)
        {
            var filters = context.State.Preferences.Filters;
            return filters.CoastalLakeImportance switch
            {
                FilterImportance.Ignored => "Lake adjacency not considered",
                FilterImportance.Critical => "Must be adjacent to lake",
                FilterImportance.Preferred => "Lake adjacency preferred",
                _ => "Any"
            };
        }

        /// <summary>
        /// Checks if a tile is adjacent to a lake (non-ocean water body).
        /// </summary>
        private static bool TileIsAdjacentToLake(int tileId)
        {
            var worldGrid = Find.WorldGrid;
            var tile = worldGrid[tileId];

            // Can't be lakeside if already in water
            if (tile.WaterCovered)
                return false;

            // Simplified approach: Use a fixed neighbor checking pattern
            // RimWorld uses icosahedral grid, so we need to check adjacent tiles
            // For now, use a simpler heuristic - check if any nearby water has higher elevation
            // This is a simplified implementation that may need refinement

            // TODO: Properly implement neighbor checking once RimWorld API is clarified
            // For now, always return false (disabled) until we can properly detect lakes
            return false;
        }

        public float Membership(int tileId, FilterContext context)
        {
            // Binary membership: 1.0 if adjacent to lake, 0.0 if not
            bool isLakeside = TileIsAdjacentToLake(tileId);
            return MembershipFunctions.Binary(isLakeside);
        }
    }
}

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

        // Reusable list for neighbor queries (avoid allocations)
        private static readonly List<PlanetTile> _neighborBuffer = new List<PlanetTile>(7);

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            var importance = filters.CoastalLakeImportance;

            // Only hard gates (MustHave/MustNotHave) filter in Apply phase
            if (!importance.IsHardGate())
                return inputTiles;

            return inputTiles.Where(id =>
            {
                bool isLakeside = TileIsAdjacentToLake(id);
                return importance == FilterImportance.MustNotHave ? !isLakeside : isLakeside;
            });
        }

        public string Describe(FilterContext context)
        {
            var filters = context.Filters;
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
        /// A "lake" is defined as a water tile that is NOT ocean coastal (inland water).
        /// </summary>
        private static bool TileIsAdjacentToLake(int tileId)
        {
            var worldGrid = Find.WorldGrid;
            if (worldGrid == null) return false;

            var tile = worldGrid[tileId];

            // Can't be lakeside if already in water
            if (tile.WaterCovered)
                return false;

            // Get neighbors using RimWorld's icosahedral grid API
            // PlanetTile has implicit conversion from int
            _neighborBuffer.Clear();
            worldGrid.GetTileNeighbors((PlanetTile)tileId, _neighborBuffer);

            // Check each neighbor for lake water (water that's not ocean)
            foreach (var neighborTile in _neighborBuffer)
            {
                int neighborId = (int)neighborTile;
                if (neighborId < 0 || neighborId >= worldGrid.TilesCount)
                    continue;

                var neighborSurfaceTile = worldGrid[neighborId];
                if (neighborSurfaceTile == null)
                    continue;

                // Check if neighbor is water
                if (!neighborSurfaceTile.WaterCovered)
                    continue;

                // Check if it's NOT ocean (i.e., it's a lake/inland water)
                // Ocean biome is typically detected via biome.defName or biome properties
                var biome = neighborSurfaceTile.PrimaryBiome;
                if (biome != null && biome.defName != "Ocean")
                {
                    // Found a non-ocean water neighbor = lake
                    return true;
                }
            }

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

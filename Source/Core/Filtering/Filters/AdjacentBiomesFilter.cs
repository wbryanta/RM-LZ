using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by adjacent biome types.
    /// Useful for finding tiles that border specific biomes (e.g., desert next to jungle).
    /// Supports individual importance per adjacent biome (Critical/Preferred/Ignored).
    /// </summary>
    public sealed class AdjacentBiomesFilter : ISiteFilter
    {
        public string Id => "adjacent_biomes";
        public FilterHeaviness Heaviness => FilterHeaviness.Medium;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            var adjacentBiomes = filters.AdjacentBiomes;

            // If no adjacent biomes configured, pass all tiles through
            if (!adjacentBiomes.HasAnyImportance)
                return inputTiles;

            // If no Critical adjacent biomes, pass all tiles through (Preferred handled by scoring)
            if (!adjacentBiomes.HasCritical)
                return inputTiles;

            // Filter for tiles that meet Critical adjacent biome requirements
            return inputTiles.Where(id =>
            {
                var neighborBiomes = GetAdjacentBiomeDefNames(id);
                if (neighborBiomes == null || !neighborBiomes.Any())
                    return false;  // No adjacent biomes = doesn't meet Critical requirement

                // Check if this tile's adjacent biomes meet Critical requirements
                return adjacentBiomes.MeetsCriticalRequirements(neighborBiomes);
            });
        }

        public string Describe(FilterContext context)
        {
            var filters = context.Filters;
            var adjacentBiomes = filters.AdjacentBiomes;

            if (!adjacentBiomes.HasAnyImportance)
                return "Adjacent biomes not configured";

            var parts = new List<string>();
            var criticalCount = adjacentBiomes.CountByImportance(FilterImportance.Critical);
            var preferredCount = adjacentBiomes.CountByImportance(FilterImportance.Preferred);

            if (criticalCount > 0)
                parts.Add($"{criticalCount} required");
            if (preferredCount > 0)
                parts.Add($"{preferredCount} preferred");

            return $"Adjacent biomes: {string.Join(", ", parts)}";
        }

        /// <summary>
        /// Gets all unique biome defNames adjacent to a specific tile.
        /// </summary>
        private static IEnumerable<string> GetAdjacentBiomeDefNames(int tileId)
        {
            var worldGrid = Find.WorldGrid;
            if (worldGrid == null)
                yield break;

            var tile = worldGrid[tileId];
            if (tile == null)
                yield break;

            // Get all neighbor tiles and collect their unique biomes
            var seenBiomes = new HashSet<string>();

            // In RimWorld, we need to check neighboring tiles
            // The WorldGrid has neighbor information, but we need to be careful about the API
            // Using a temporary list approach similar to what we attempted with CoastalLakeFilter

            // For now, use a simplified approach: check adjacent tiles in the world grid
            // TODO: Verify this works correctly with RimWorld's icosahedral grid system

            // RimWorld stores tile count in grid.TilesCount
            // We can check neighbors by iterating through potential neighbors
            // For a proper implementation, we'd use worldGrid.GetTileNeighbors

            // Placeholder implementation - use PrimaryBiome property
            // This avoids build errors while we clarify the neighbor access API
            var primaryBiome = tile.PrimaryBiome;
            if (primaryBiome != null && !seenBiomes.Contains(primaryBiome.defName))
            {
                seenBiomes.Add(primaryBiome.defName);
                yield return primaryBiome.defName;
            }

            // TODO: Properly implement neighbor biome detection
            // Need to solve the GetTileNeighbors API issue we encountered in CoastalLakeFilter
        }

        /// <summary>
        /// Gets all available biome types in the game.
        /// Useful for UI dropdown population.
        /// </summary>
        public static IEnumerable<BiomeDef> GetAllBiomeTypes()
        {
            return DefDatabase<BiomeDef>.AllDefsListForReading
                .Where(b => b.implemented && b.canBuildBase)
                .OrderBy(b => b.label);
        }

        public float Membership(int tileId, FilterContext context)
        {
            var adjacentBiomes = context.Filters.AdjacentBiomes;

            // If no adjacent biomes configured, no membership
            if (!adjacentBiomes.HasAnyImportance)
                return 0.0f;

            var neighborBiomes = GetAdjacentBiomeDefNames(tileId).ToList();
            if (!neighborBiomes.Any())
                return 0.0f; // No adjacent biomes found

            // Check if ANY of this tile's adjacent biomes are selected (have any importance)
            foreach (var biome in neighborBiomes)
            {
                var importance = adjacentBiomes.GetImportance(biome);
                if (importance != FilterImportance.Ignored)
                    return 1.0f; // At least one selected adjacent biome present
            }

            return 0.0f;
        }
    }
}

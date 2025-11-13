using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by map features (geysers, ancient shrines, etc.).
    /// RimWorld 1.6+ feature. Supports multi-select with AND/OR logic.
    /// </summary>
    public sealed class MapFeatureFilter : ISiteFilter
    {
        public string Id => "map_features";
        public FilterHeaviness Heaviness => FilterHeaviness.Medium;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.State.Preferences.Filters;
            var mapFeatures = filters.MapFeatures;

            // If no features configured, pass all tiles through
            if (!mapFeatures.HasAnyImportance)
                return inputTiles;

            // If no Critical features, pass all tiles through (Preferred handled by scoring)
            if (!mapFeatures.HasCritical)
                return inputTiles;

            // Filter for tiles that meet Critical requirements
            // Tiles must have ALL Critical features
            return inputTiles.Where(id =>
            {
                var tileFeatures = GetTileMapFeatures(id);
                return mapFeatures.MeetsCriticalRequirements(tileFeatures);
            });
        }

        public string Describe(FilterContext context)
        {
            var filters = context.State.Preferences.Filters;
            var mapFeatures = filters.MapFeatures;

            if (!mapFeatures.HasAnyImportance)
                return "Map features not configured";

            var parts = new List<string>();
            var criticalCount = mapFeatures.CountByImportance(FilterImportance.Critical);
            var preferredCount = mapFeatures.CountByImportance(FilterImportance.Preferred);

            if (criticalCount > 0)
                parts.Add($"{criticalCount} required");
            if (preferredCount > 0)
                parts.Add($"{preferredCount} preferred");

            return $"Features: {string.Join(", ", parts)}";
        }

        /// <summary>
        /// Gets all map feature defNames (Mutators) for a specific tile.
        /// Map features (Mutators) include: Caves, Mountain, MixedBiome, Ruins, Junkyard, Archean trees, etc.
        /// </summary>
        private static IEnumerable<string> GetTileMapFeatures(int tileId)
        {
            var result = new List<string>();
            var tile = Find.WorldGrid?[tileId];
            if (tile == null)
                return result;

            // Access the Mutators property via reflection (RimWorld 1.6+)
            try
            {
                var mutatorsProp = tile.GetType().GetProperty("Mutators");
                if (mutatorsProp != null)
                {
                    var mutators = mutatorsProp.GetValue(tile);
                    if (mutators is System.Collections.IEnumerable enumerable)
                    {
                        foreach (var mutator in enumerable)
                        {
                            if (mutator != null)
                            {
                                // Try to get the mutator's string representation (likely ToString() gives the name)
                                var mutatorName = mutator.ToString();
                                if (!string.IsNullOrEmpty(mutatorName))
                                {
                                    result.Add(mutatorName);
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Return empty on error
            }

            return result;
        }

        /// <summary>
        /// Gets all available map feature types by scanning a sample of world tiles.
        /// Discovers actual Mutator types present in the current world.
        /// </summary>
        public static IEnumerable<string> GetAllMapFeatureTypes()
        {
            var featureTypes = new HashSet<string>();
            var world = Find.World;

            if (world?.grid == null)
                return featureTypes;

            // Sample tiles to find all mutator types (checking ~1000 tiles should be enough)
            int sampleSize = System.Math.Min(1000, world.grid.TilesCount);
            for (int i = 0; i < sampleSize; i++)
            {
                var features = GetTileMapFeatures(i);
                foreach (var feature in features)
                {
                    featureTypes.Add(feature);
                }
            }

            // Sort with Caves first (most commonly used), then alphabetically
            return featureTypes.OrderBy(f => f == "Caves" ? "0" : f);
        }
    }
}

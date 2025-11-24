using RimWorld.Planet;
using System.Linq;
using System.Reflection;
using Verse;

namespace LandingZone.Core.Diagnostics
{
    /// <summary>
    /// Diagnostic utility to inspect Tile properties and discover RimWorld's API for landmarks and map features.
    /// </summary>
    public static class TilePropertyInspector
    {
        /// <summary>
        /// Logs all properties available on a Tile object for the first settleable tile found.
        /// This helps us understand what RimWorld 1.6+ exposes for landmarks and map features.
        /// </summary>
        public static void InspectFirstSettleableTile()
        {
            var world = Find.World;
            if (world?.grid == null)
            {
                Log.Warning("LandingZone_DevTools_WorldNullError".Translate());
                return;
            }

            // Find first settleable tile
            int tileId = -1;
            for (int i = 0; i < world.grid.TilesCount; i++)
            {
                var tile = world.grid[i];
                var biome = tile?.PrimaryBiome;
                if (tile != null && biome != null && !biome.impassable && !world.Impassable(i))
                {
                    tileId = i;
                    break;
                }
            }

            if (tileId == -1)
            {
                Log.Warning("LandingZone_DevTools_NoSettleableTile".Translate());
                return;
            }

            var targetTile = world.grid[tileId];
            Log.Message("LandingZone_DevTools_TilePropertyInspectorTitle".Translate(tileId));

            // Inspect all properties using reflection
            var tileType = targetTile.GetType();
            var properties = tileType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                try
                {
                    var value = prop.GetValue(targetTile);
                    var valueStr = value?.ToString() ?? "null";

                    // Special handling for collections
                    if (value is System.Collections.IEnumerable enumerable && !(value is string))
                    {
                        var items = enumerable.Cast<object>().Take(5).ToList();
                        if (items.Any())
                        {
                            valueStr = $"[{items.Count} items: {string.Join(", ", items.Select(x => x?.ToString() ?? "null"))}]";
                        }
                        else
                        {
                            valueStr = "[empty collection]";
                        }
                    }

                    Log.Message($"[LandingZone]   {prop.Name} ({prop.PropertyType.Name}): {valueStr}");
                }
                catch (System.Exception ex)
                {
                    Log.Warning($"[LandingZone]   {prop.Name}: Error reading - {ex.Message}");
                }
            }

            // Check for landmark specifically
            var landmarkProp = properties.FirstOrDefault(p => p.Name.ToLower().Contains("landmark"));
            if (landmarkProp != null)
            {
                Log.Message("LandingZone_DevTools_LandmarkFound".Translate(landmarkProp.Name));
            }

            // Check for MapFeatures specifically
            var mapFeatureProp = properties.FirstOrDefault(p => p.Name.ToLower().Contains("feature") || p.Name.ToLower().Contains("mapfeature"));
            if (mapFeatureProp != null)
            {
                Log.Message("LandingZone_DevTools_FeatureFound".Translate(mapFeatureProp.Name));
            }

            Log.Message("LandingZone_DevTools_TilePropertyInspectorEnd".Translate());
        }
    }
}

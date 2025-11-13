using RimWorld;
using RimWorld.Planet;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Verse;

namespace LandingZone.Core.Diagnostics
{
    /// <summary>
    /// Dumps world generation data to a file for analysis.
    /// Helps understand RimWorld's API for landmarks, features, etc.
    /// </summary>
    public static class WorldDataDumper
    {
        /// <summary>
        /// Dumps comprehensive world data to a file in the mod directory.
        /// Samples tiles across the world to show different terrain types.
        /// </summary>
        public static void DumpWorldData()
        {
            try
            {
                var world = Find.World;
                if (world?.grid == null)
                {
                    Log.Error("[LandingZone] WorldDataDumper: World or grid is null");
                    return;
                }

                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var filePath = Path.Combine(GenFilePaths.ConfigFolderPath, $"LandingZone_WorldDump_{timestamp}.txt");

                var sb = new StringBuilder();
                sb.AppendLine("=".PadRight(80, '='));
                sb.AppendLine($"LandingZone World Data Dump - {timestamp}");
                // Skip version - not essential for data dump
                sb.AppendLine($"World: {world.info.name}");
                sb.AppendLine($"Seed: {world.info.seedString}");
                sb.AppendLine($"Total Tiles: {world.grid.TilesCount}");
                sb.AppendLine("=".PadRight(80, '='));
                sb.AppendLine();

                // Sample tiles strategically: first few, random samples, and tiles with interesting properties
                var tilesToDump = new System.Collections.Generic.HashSet<int>();

                // First 10 settleable tiles
                int settled = 0;
                for (int i = 0; i < world.grid.TilesCount && settled < 10; i++)
                {
                    var tile = world.grid[i];
                    var biome = tile?.PrimaryBiome;
                    if (tile != null && biome != null && !biome.impassable && !world.Impassable(i))
                    {
                        tilesToDump.Add(i);
                        settled++;
                    }
                }

                // Random sample of 20 more tiles
                var rand = new Random();
                for (int i = 0; i < 20; i++)
                {
                    tilesToDump.Add(rand.Next(world.grid.TilesCount));
                }

                sb.AppendLine($"Dumping {tilesToDump.Count} sample tiles:");
                sb.AppendLine();

                foreach (var tileId in tilesToDump.OrderBy(x => x))
                {
                    DumpTileData(tileId, world, sb);
                }

                // Dump world features (landmarks/regions)
                sb.AppendLine();
                sb.AppendLine("=".PadRight(80, '='));
                sb.AppendLine("WORLD FEATURES (Find.World.features)");
                sb.AppendLine("=".PadRight(80, '='));
                sb.AppendLine();

                if (world.features?.features != null)
                {
                    int featureCount = 0;
                    foreach (var feature in world.features.features)
                    {
                        if (feature == null) continue;

                        sb.AppendLine($"Feature #{featureCount++}:");
                        sb.AppendLine($"  Name: {feature.name ?? "(unnamed)"}");
                        sb.AppendLine($"  Def: {feature.def?.defName ?? "(no def)"}");
                        sb.AppendLine($"  Max Draw Size: {feature.maxDrawSizeInTiles}");
                        var tileCount = feature.Tiles != null ? feature.Tiles.Count() : 0;
                        sb.AppendLine($"  Tiles: {tileCount}");

                        if (feature.Tiles != null && feature.Tiles.Any())
                        {
                            var sampleTiles = feature.Tiles.Take(5).ToList();
                            sb.AppendLine($"  Sample Tile IDs: {string.Join(", ", sampleTiles)}");
                        }

                        sb.AppendLine();
                    }
                }
                else
                {
                    sb.AppendLine("(No world features found)");
                }

                File.WriteAllText(filePath, sb.ToString());

                Log.Message($"[LandingZone] World data dumped to: {filePath}");
                Messages.Message($"LandingZone: World data dumped to {Path.GetFileName(filePath)}", MessageTypeDefOf.SilentInput, false);
            }
            catch (Exception ex)
            {
                Log.Error($"[LandingZone] WorldDataDumper error: {ex}");
            }
        }

        private static void DumpTileData(int tileId, World world, StringBuilder sb)
        {
            sb.AppendLine("-".PadRight(80, '-'));
            sb.AppendLine($"TILE {tileId}");
            sb.AppendLine("-".PadRight(80, '-'));

            var tile = world.grid[tileId];
            if (tile == null)
            {
                sb.AppendLine("(null tile)");
                sb.AppendLine();
                return;
            }

            // Basic properties
            var biome = tile.PrimaryBiome;
            sb.AppendLine($"Biome: {biome?.defName ?? "null"} ({biome?.label ?? "null"})");
            sb.AppendLine($"Hilliness: {tile.hilliness}");
            sb.AppendLine($"Temperature: {tile.temperature}°C");
            sb.AppendLine($"Rainfall: {tile.rainfall}mm");
            sb.AppendLine($"Elevation: {tile.elevation}m");
            sb.AppendLine($"Swampiness: {tile.swampiness}");
            sb.AppendLine($"Pollution: {tile.pollution}");

            // Rivers
            if (tile is SurfaceTile surface)
            {
                if (surface.Rivers != null && surface.Rivers.Any())
                {
                    sb.AppendLine($"Rivers: {surface.Rivers.Count}");
                    foreach (var river in surface.Rivers)
                    {
                        sb.AppendLine($"  - {river.river?.defName ?? "null"} (size: {river.river?.widthOnWorld ?? 0})");
                    }
                }
                else
                {
                    sb.AppendLine("Rivers: None");
                }

                // Roads
                if (surface.Roads != null && surface.Roads.Any())
                {
                    sb.AppendLine($"Roads: {surface.Roads.Count}");
                    foreach (var road in surface.Roads)
                    {
                        sb.AppendLine($"  - {road.road?.defName ?? "null"}");
                    }
                }
                else
                {
                    sb.AppendLine("Roads: None");
                }
            }

            // World features (landmark check)
            var features = world.features?.features;
            if (features != null)
            {
                var matchingFeatures = features.Where(f => f != null && f.Tiles != null && f.Tiles.Contains(tileId)).ToList();
                if (matchingFeatures.Any())
                {
                    sb.AppendLine($"WorldFeatures: {matchingFeatures.Count}");
                    foreach (var feature in matchingFeatures)
                    {
                        sb.AppendLine($"  - {feature.name ?? "(unnamed)"} [{feature.def?.defName ?? "(no def)"}]");
                    }
                }
                else
                {
                    sb.AppendLine("WorldFeatures: None");
                }
            }

            // Try to find map features and other properties using reflection
            try
            {
                var tileType = tile.GetType();

                // Dump ALL properties to see what's available
                var props = tileType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                sb.AppendLine("All Tile Properties (FULL DUMP - NO FILTERING):");
                foreach (var prop in props)
                {
                    try
                    {
                        var value = prop.GetValue(tile);
                        if (value != null)
                        {
                            var valueStr = FormatValue(value);
                            // SHOW EVERYTHING - no filtering to find hidden temperature properties
                            sb.AppendLine($"  {prop.Name}: {valueStr}");
                        }
                    }
                    catch { /* Skip properties that throw */ }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Error inspecting tile properties: {ex.Message}");
            }

            sb.AppendLine();
        }

        private static string FormatValue(object value)
        {
            if (value == null) return "null";

            // Handle Landmark objects specially
            if (value.GetType().Name == "Landmark")
            {
                try
                {
                    var nameProp = value.GetType().GetProperty("name");
                    if (nameProp != null)
                    {
                        var name = nameProp.GetValue(value) as string;
                        return $"Landmark(\"{name ?? "unnamed"}\")";
                    }
                }
                catch { }
                return "Landmark(?)";
            }

            // Handle collections
            if (value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                var items = enumerable.Cast<object>().Take(10).ToList();
                if (!items.Any()) return "(empty collection)";

                var itemStrings = items.Select(item => {
                    if (item == null) return "null";

                    // If it's a def, show defName
                    var defProp = item.GetType().GetProperty("defName");
                    if (defProp != null)
                    {
                        var defName = defProp.GetValue(item) as string;
                        return $"{item.GetType().Name}({defName})";
                    }

                    return item.ToString();
                }).ToList();

                return $"[{items.Count} items: {string.Join(", ", itemStrings)}]";
            }

            // Handle defs
            if (value is Def def)
            {
                return $"{def.defName} ({def.label})";
            }

            return value.ToString();
        }
    }
}

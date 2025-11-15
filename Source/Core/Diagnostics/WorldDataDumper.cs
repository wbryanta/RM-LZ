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
        /// Legacy entry point retained for compatibility; now performs a full world cache dump.
        /// </summary>
        public static void DumpWorldData()
        {
            DumpFullWorldCache();
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

            // Dump ALL tile fields and properties (comprehensive)
            try
            {
                var tileType = tile.GetType();

                // Get ALL fields - public, private, internal
                var allFields = tileType.GetFields(
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                foreach (var field in allFields)
                {
                    try
                    {
                        var value = field.GetValue(tile);
                        sb.AppendLine($"  {field.Name}: {DumpValue(value)}");
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"  {field.Name}: <error: {ex.Message}>");
                    }
                }

                // Get ALL properties - public, private, internal
                var allProps = tileType.GetProperties(
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                foreach (var prop in allProps)
                {
                    try
                    {
                        var value = prop.GetValue(tile);
                        sb.AppendLine($"  {prop.Name}: {DumpValue(value)}");
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"  {prop.Name}: <error: {ex.Message}>");
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Error inspecting tile: {ex.Message}");
            }

            sb.AppendLine();
        }

        /// <summary>
        /// Dumps a value with full detail - unwraps collections, shows actual data.
        /// </summary>
        private static string DumpValue(object value, int depth = 0)
        {
            if (value == null) return "null";
            if (depth > 3) return value.ToString(); // Prevent infinite recursion

            var type = value.GetType();

            // Primitives and strings
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
                return value.ToString();

            // Enums
            if (type.IsEnum)
                return value.ToString();

            // Collections - unwrap and show contents
            if (value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                var items = new System.Collections.Generic.List<string>();
                int count = 0;
                foreach (var item in enumerable)
                {
                    if (count >= 20) // Limit to first 20 items to avoid huge output
                    {
                        items.Add($"... ({count} more)");
                        break;
                    }
                    items.Add(DumpValue(item, depth + 1));
                    count++;
                }

                if (items.Count == 0)
                    return "[]";

                return $"[{string.Join(", ", items)}]";
            }

            // Defs - show defName
            if (value is Verse.Def def)
                return $"{def.defName}";

            // RimWorld specific types
            if (type.FullName?.Contains("RimWorld") == true || type.FullName?.Contains("Verse") == true)
            {
                // Try to get a meaningful string representation
                var nameField = type.GetField("name", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (nameField != null)
                {
                    var nameValue = nameField.GetValue(value);
                    if (nameValue != null)
                        return $"{type.Name}({nameValue})";
                }

                var defNameProp = type.GetProperty("defName", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (defNameProp != null)
                {
                    var defNameValue = defNameProp.GetValue(value);
                    if (defNameValue != null)
                        return $"{type.Name}({defNameValue})";
                }
            }

            // Default ToString()
            return value.ToString();
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

        /// <summary>
        /// Dumps the ENTIRE world cache - all tiles, all properties, 100% raw data.
        /// This will be a very large file (~295k tiles).
        /// </summary>
        public static void DumpFullWorldCache()
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
                var filePath = Path.Combine(GenFilePaths.ConfigFolderPath, $"LandingZone_FullCache_{timestamp}.txt");

                var sb = new StringBuilder();
                sb.AppendLine("=".PadRight(80, '='));
                sb.AppendLine($"LandingZone FULL WORLD CACHE DUMP - {timestamp}");
                sb.AppendLine($"World: {world.info.name}");
                sb.AppendLine($"Seed: {world.info.seedString}");
                sb.AppendLine($"Total Tiles: {world.grid.TilesCount}");
                sb.AppendLine("=".PadRight(80, '='));
                sb.AppendLine();

                // Dump EVERY tile in the world
                for (int tileId = 0; tileId < world.grid.TilesCount; tileId++)
                {
                    var tile = world.grid[tileId];
                    if (tile == null) continue;

                    sb.AppendLine($"TILE {tileId}");

                    // Dump EVERYTHING - all fields (public, private, internal) and properties
                    var tileType = tile.GetType();

                    // Get ALL fields - public, private, internal, everything
                    var allFields = tileType.GetFields(
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);

                    foreach (var field in allFields)
                    {
                        try
                        {
                            var value = field.GetValue(tile);
                            sb.AppendLine($"  {field.Name}: {DumpValue(value)}");
                        }
                        catch (Exception ex)
                        {
                            sb.AppendLine($"  {field.Name}: <error: {ex.Message}>");
                        }
                    }

                    // Get ALL properties - public, private, internal
                    var allProps = tileType.GetProperties(
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);

                    foreach (var prop in allProps)
                    {
                        try
                        {
                            var value = prop.GetValue(tile);
                            sb.AppendLine($"  {prop.Name}: {DumpValue(value)}");
                        }
                        catch (Exception ex)
                        {
                            sb.AppendLine($"  {prop.Name}: <error: {ex.Message}>");
                        }
                    }

                    sb.AppendLine();

                    // Write to file every 1000 tiles to avoid memory issues
                    if (tileId % 1000 == 0)
                    {
                        File.AppendAllText(filePath, sb.ToString());
                        sb.Clear();
                    }
                }

                // Write any remaining data
                if (sb.Length > 0)
                {
                    File.AppendAllText(filePath, sb.ToString());
                }

                Log.Message($"[LandingZone] Full world cache dumped to: {filePath}");
                Messages.Message($"LandingZone: Full cache dumped to {Path.GetFileName(filePath)}", MessageTypeDefOf.SilentInput, false);
            }
            catch (Exception ex)
            {
                Log.Error($"[LandingZone] WorldDataDumper full cache error: {ex}");
            }
        }
    }
}

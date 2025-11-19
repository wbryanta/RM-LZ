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

                // INSPECT WORLD OBJECT - DUMP ALL FIELDS AND PROPERTIES
                sb.AppendLine("=".PadRight(80, '='));
                sb.AppendLine("WORLD OBJECT INSPECTION (ALL fields/properties)");
                sb.AppendLine("=".PadRight(80, '='));
                sb.AppendLine();

                try
                {
                    var worldType = world.GetType();

                    // Dump ALL fields
                    var worldFields = worldType.GetFields(
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);

                    sb.AppendLine($"--- WORLD FIELDS ({worldFields.Length} total) ---");
                    sb.AppendLine();

                    foreach (var field in worldFields)
                    {
                        try
                        {
                            var value = field.GetValue(world);
                            sb.AppendLine($"WORLD.{field.Name} ({field.FieldType.Name}):");

                            // For any complex object (not primitive, not string), dump its internals
                            bool shouldInspectInternals = value != null &&
                                                         !field.FieldType.IsPrimitive &&
                                                         !field.FieldType.IsEnum &&
                                                         field.FieldType != typeof(string) &&
                                                         !(value is System.Collections.IDictionary) &&
                                                         !(value is System.Collections.IList);

                            if (shouldInspectInternals)
                            {
                                var objType = value.GetType();
                                sb.AppendLine($"  {objType.FullName}");
                                sb.AppendLine($"  --- Internals of {field.Name} ---");

                                var objFields = objType.GetFields(
                                    System.Reflection.BindingFlags.Public |
                                    System.Reflection.BindingFlags.NonPublic |
                                    System.Reflection.BindingFlags.Instance);

                                foreach (var objField in objFields)
                                {
                                    try
                                    {
                                        var objValue = objField.GetValue(value);

                                        if (objValue is System.Collections.IDictionary objDict)
                                        {
                                            sb.AppendLine($"    {objField.Name}: Dictionary with {objDict.Count} entries");
                                            int showCount = Math.Min(10, objDict.Count);
                                            int dictCount = 0;
                                            foreach (System.Collections.DictionaryEntry entry in objDict)
                                            {
                                                if (dictCount >= showCount)
                                                {
                                                    sb.AppendLine($"      ... and {objDict.Count - dictCount} more");
                                                    break;
                                                }
                                                sb.AppendLine($"      [{entry.Key}] = {DumpValue(entry.Value, depth: 0)}");
                                                dictCount++;
                                            }
                                        }
                                        else if (objValue is System.Collections.ICollection objColl && !(objValue is string))
                                        {
                                            sb.AppendLine($"    {objField.Name}: Collection with {objColl.Count} items (first 5)");
                                            int collCount = 0;
                                            foreach (var collItem in objColl)
                                            {
                                                if (collCount >= 5)
                                                {
                                                    sb.AppendLine($"      ... and {objColl.Count - 5} more");
                                                    break;
                                                }
                                                sb.AppendLine($"      [{collCount}] = {DumpValue(collItem, depth: 0)}");
                                                collCount++;
                                            }
                                        }
                                        else
                                        {
                                            sb.AppendLine($"    {objField.Name}: {DumpValue(objValue, depth: 0)}");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        sb.AppendLine($"    {objField.Name}: <error: {ex.Message}>");
                                    }
                                }
                                sb.AppendLine();
                            }
                            // Special handling for WorldGenData - dump its internals
                            else if ((field.Name == "genData" || field.Name == "worldGenData") && value != null)
                            {
                                var genDataType = value.GetType();
                                sb.AppendLine($"  {genDataType.FullName}");

                                var genDataFields = genDataType.GetFields(
                                    System.Reflection.BindingFlags.Public |
                                    System.Reflection.BindingFlags.NonPublic |
                                    System.Reflection.BindingFlags.Instance);

                                foreach (var genField in genDataFields)
                                {
                                    try
                                    {
                                        var genValue = genField.GetValue(value);

                                        if (genValue is System.Collections.IDictionary genDict)
                                        {
                                            sb.AppendLine($"    {genField.Name}: Dictionary with {genDict.Count} entries");
                                            int showCount = Math.Min(10, genDict.Count);
                                            int dictCount = 0;
                                            foreach (System.Collections.DictionaryEntry entry in genDict)
                                            {
                                                if (dictCount >= showCount)
                                                {
                                                    sb.AppendLine($"      ... and {genDict.Count - dictCount} more");
                                                    break;
                                                }
                                                sb.AppendLine($"      [{entry.Key}] = {DumpValue(entry.Value, depth: 0)}");
                                                dictCount++;
                                            }
                                        }
                                        else if (genValue is System.Collections.ICollection genColl && !(genValue is string))
                                        {
                                            sb.AppendLine($"    {genField.Name}: Collection with {genColl.Count} items (first 5)");
                                            int collCount = 0;
                                            foreach (var collItem in genColl)
                                            {
                                                if (collCount >= 5)
                                                {
                                                    sb.AppendLine($"      ... and {genColl.Count - 5} more");
                                                    break;
                                                }
                                                sb.AppendLine($"      [{collCount}] = {DumpValue(collItem, depth: 0)}");
                                                collCount++;
                                            }
                                        }
                                        else
                                        {
                                            sb.AppendLine($"    {genField.Name}: {DumpValue(genValue, depth: 0)}");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        sb.AppendLine($"    {genField.Name}: <error: {ex.Message}>");
                                    }
                                }
                                sb.AppendLine();
                            }
                            // Special handling for WorldComponents - dump their internals
                            else if (field.Name == "components" && value is System.Collections.IList componentList)
                            {
                                sb.AppendLine($"  Collection with {componentList.Count} items");
                                for (int i = 0; i < componentList.Count; i++)
                                {
                                    var component = componentList[i];
                                    if (component == null) continue;

                                    var componentType = component.GetType();
                                    sb.AppendLine($"  [{i}] = {componentType.FullName}");

                                    // Dump all fields of this component
                                    var componentFields = componentType.GetFields(
                                        System.Reflection.BindingFlags.Public |
                                        System.Reflection.BindingFlags.NonPublic |
                                        System.Reflection.BindingFlags.Instance);

                                    foreach (var compField in componentFields)
                                    {
                                        try
                                        {
                                            var compValue = compField.GetValue(component);

                                            if (compValue is System.Collections.IDictionary compDict)
                                            {
                                                sb.AppendLine($"      {compField.Name}: Dictionary with {compDict.Count} entries");
                                                int showCount = Math.Min(10, compDict.Count);
                                                int dictCount = 0;
                                                foreach (System.Collections.DictionaryEntry entry in compDict)
                                                {
                                                    if (dictCount >= showCount)
                                                    {
                                                        sb.AppendLine($"        ... and {compDict.Count - dictCount} more");
                                                        break;
                                                    }
                                                    sb.AppendLine($"        [{entry.Key}] = {DumpValue(entry.Value, depth: 0)}");
                                                    dictCount++;
                                                }
                                            }
                                            else if (compValue is System.Collections.ICollection compColl && !(compValue is string))
                                            {
                                                sb.AppendLine($"      {compField.Name}: Collection with {compColl.Count} items (first 5)");
                                                int collCount = 0;
                                                foreach (var collItem in compColl)
                                                {
                                                    if (collCount >= 5)
                                                    {
                                                        sb.AppendLine($"        ... and {compColl.Count - 5} more");
                                                        break;
                                                    }
                                                    sb.AppendLine($"        [{collCount}] = {DumpValue(collItem, depth: 0)}");
                                                    collCount++;
                                                }
                                            }
                                            else
                                            {
                                                sb.AppendLine($"      {compField.Name}: {DumpValue(compValue, depth: 0)}");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            sb.AppendLine($"      {compField.Name}: <error: {ex.Message}>");
                                        }
                                    }
                                    sb.AppendLine();
                                }
                            }
                            // If it's a dictionary, dump ALL entries
                            else if (value is System.Collections.IDictionary dict)
                            {
                                sb.AppendLine($"  Total entries: {dict.Count}");
                                foreach (System.Collections.DictionaryEntry entry in dict)
                                {
                                    sb.AppendLine($"  [{entry.Key}] = {DumpValue(entry.Value, depth: 0)}");
                                }
                            }
                            // If it's a collection (but not dictionary), dump ALL items
                            else if (value is System.Collections.ICollection collection)
                            {
                                sb.AppendLine($"  Collection with {collection.Count} items");
                                int count = 0;
                                foreach (var item in collection)
                                {
                                    sb.AppendLine($"  [{count}] = {DumpValue(item, depth: 0)}");
                                    count++;
                                }
                            }
                            else
                            {
                                sb.AppendLine($"  {DumpValue(value, depth: 0)}");
                            }
                            sb.AppendLine();
                        }
                        catch (Exception ex)
                        {
                            sb.AppendLine($"WORLD.{field.Name}: <error: {ex.Message}>");
                            sb.AppendLine();
                        }
                    }

                    // Dump ALL properties
                    var worldProps = worldType.GetProperties(
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);

                    sb.AppendLine($"--- WORLD PROPERTIES ({worldProps.Length} total) ---");
                    sb.AppendLine();

                    foreach (var prop in worldProps)
                    {
                        try
                        {
                            // Skip indexed properties (they require parameters)
                            if (prop.GetIndexParameters().Length > 0)
                            {
                                sb.AppendLine($"WORLD.{prop.Name} (indexed property - skipped)");
                                sb.AppendLine();
                                continue;
                            }

                            var value = prop.GetValue(world);
                            sb.AppendLine($"WORLD.{prop.Name} ({prop.PropertyType.Name}):");

                            // Same logic as fields - dump EVERYTHING
                            if (value is System.Collections.IDictionary dict)
                            {
                                sb.AppendLine($"  Total entries: {dict.Count}");
                                foreach (System.Collections.DictionaryEntry entry in dict)
                                {
                                    sb.AppendLine($"  [{entry.Key}] = {DumpValue(entry.Value, depth: 0)}");
                                }
                            }
                            else if (value is System.Collections.ICollection collection)
                            {
                                sb.AppendLine($"  Collection with {collection.Count} items");
                                int count = 0;
                                foreach (var item in collection)
                                {
                                    sb.AppendLine($"  [{count}] = {DumpValue(item, depth: 0)}");
                                    count++;
                                }
                            }
                            else
                            {
                                sb.AppendLine($"  {DumpValue(value, depth: 0)}");
                            }
                            sb.AppendLine();
                        }
                        catch (Exception ex)
                        {
                            sb.AppendLine($"WORLD.{prop.Name}: <error: {ex.Message}>");
                            sb.AppendLine();
                        }
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Error inspecting World object: {ex.Message}");
                }

                sb.AppendLine("=".PadRight(80, '='));
                sb.AppendLine("TILE DATA");
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

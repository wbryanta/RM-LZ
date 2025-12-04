#nullable enable
using System.Collections.Generic;
using System.Linq;
using LandingZone.Core;
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

        #region Runtime Mutator Cache and Alias Resolution

        /// <summary>
        /// Cache of mutators that actually exist in the current world.
        /// Rebuilt when world seed changes.
        /// </summary>
        private static HashSet<string> _runtimeMutatorCache = new HashSet<string>();
        private static string? _cachedWorldSeed;

        /// <summary>
        /// Known aliases for mutators that mods replace.
        /// Key = vanilla/common name, Value = array of possible runtime names (checked in order).
        /// </summary>
        private static readonly Dictionary<string, string[]> MutatorAliases = new Dictionary<string, string[]>
        {
            // Geological Landforms replacements
            { "River", new[] { "GL_River", "River" } },
            { "RiverConfluence", new[] { "GL_RiverConfluence", "RiverConfluence" } },
            { "RiverDelta", new[] { "GL_RiverDelta", "RiverDelta" } },
            { "RiverIsland", new[] { "GL_RiverIsland", "RiverIsland" } },
            { "Headwater", new[] { "GL_RiverSource", "Headwater" } },
            // Add reverse mappings so GL_ names also resolve
            { "GL_River", new[] { "GL_River", "River" } },
            { "GL_RiverConfluence", new[] { "GL_RiverConfluence", "RiverConfluence" } },
            { "GL_RiverDelta", new[] { "GL_RiverDelta", "RiverDelta" } },
            { "GL_RiverIsland", new[] { "GL_RiverIsland", "RiverIsland" } },
            { "GL_RiverSource", new[] { "GL_RiverSource", "Headwater" } },
        };

        /// <summary>
        /// List of mutators that were requested but couldn't be resolved in the current world.
        /// Used to show warnings to the user.
        /// </summary>
        public static List<string> UnresolvedMutators { get; private set; } = new List<string>();

        /// <summary>
        /// Ensures the runtime mutator cache is up to date for the current world.
        /// </summary>
        private static void EnsureRuntimeCacheValid()
        {
            var world = Find.World;
            var currentSeed = world?.info?.seedString ?? string.Empty;

            if (_cachedWorldSeed == currentSeed && _runtimeMutatorCache.Count > 0)
                return; // Cache is valid

            // Rebuild cache
            _runtimeMutatorCache.Clear();
            _cachedWorldSeed = currentSeed;

            if (world?.grid == null)
                return;

            // Scan ALL tiles to catch rare mutators (not just a sample)
            int tileCount = world.grid.TilesCount;
            for (int i = 0; i < tileCount; i++)
            {
                var features = GetTileMapFeatures(i);
                foreach (var feature in features)
                {
                    _runtimeMutatorCache.Add(feature);
                }
            }

            if (Prefs.DevMode && LandingZoneLogger.IsStandardOrVerbose)
                LandingZoneLogger.LogStandard($"[LandingZone] Runtime mutator cache rebuilt: {_runtimeMutatorCache.Count} mutators found in world '{currentSeed}'");
        }

        /// <summary>
        /// Checks if a mutator exists in the current world's runtime cache.
        /// </summary>
        public static bool IsRuntimeMutator(string defName)
        {
            EnsureRuntimeCacheValid();
            return _runtimeMutatorCache.Contains(defName);
        }

        /// <summary>
        /// Resolves a requested mutator name to one that exists in the current world.
        /// Uses alias mapping to handle mod replacements (e.g., River → GL_River).
        /// Returns null if no match found.
        /// </summary>
        public static string? ResolveToRuntimeMutator(string requested)
        {
            EnsureRuntimeCacheValid();

            // Direct match?
            if (_runtimeMutatorCache.Contains(requested))
                return requested;

            // Check aliases
            if (MutatorAliases.TryGetValue(requested, out var aliases))
            {
                foreach (var alias in aliases)
                {
                    if (_runtimeMutatorCache.Contains(alias))
                    {
                        if (Prefs.DevMode && LandingZoneLogger.IsVerbose)
                            LandingZoneLogger.LogVerbose($"[LandingZone] Mutator alias resolved: '{requested}' → '{alias}'");
                        return alias;
                    }
                }
            }

            // No match found
            return null;
        }

        /// <summary>
        /// Checks if a tile has a specific mutator, using alias resolution.
        /// </summary>
        public static bool TileHasMutatorWithAlias(int tileId, string requestedMutator)
        {
            var tileFeatures = GetTileMapFeatures(tileId);

            // Direct match?
            if (tileFeatures.Contains(requestedMutator))
                return true;

            // Check aliases
            if (MutatorAliases.TryGetValue(requestedMutator, out var aliases))
            {
                foreach (var alias in aliases)
                {
                    if (tileFeatures.Contains(alias))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets all runtime mutators (what actually exists in the current world).
        /// This is the authoritative source for UI pickers.
        /// </summary>
        public static IEnumerable<string> GetRuntimeMutators()
        {
            EnsureRuntimeCacheValid();
            return _runtimeMutatorCache.OrderBy(f => f == "Caves" ? "0" : f);
        }

        #endregion

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            var mapFeatures = filters.MapFeatures;

            // If no features configured, pass all tiles through
            if (!mapFeatures.HasAnyImportance)
            {
                if (Prefs.DevMode && LandingZoneLogger.IsVerbose)
                    LandingZoneLogger.LogVerbose("[LandingZone] MapFeatureFilter.Apply: No features configured, passing all tiles");
                return inputTiles;
            }

            // Only hard gates (MustHave/MustNotHave) filter in Apply phase
            if (!mapFeatures.HasHardGates)
            {
                if (Prefs.DevMode && LandingZoneLogger.IsVerbose)
                    LandingZoneLogger.LogVerbose("[LandingZone] MapFeatureFilter.Apply: No hard gate features, passing all tiles");
                return inputTiles;
            }

            // Pre-resolve MustHave items to their runtime equivalents (handles mod aliasing)
            var mustHaveFeats = mapFeatures.GetMustHaveItems().ToList();
            var mustNotHaveFeats = mapFeatures.GetMustNotHaveItems().ToList();

            // Track unresolved mutators for warning
            UnresolvedMutators.Clear();
            var resolvedMustHave = new List<string>();
            foreach (var feat in mustHaveFeats)
            {
                var resolved = ResolveToRuntimeMutator(feat);
                if (resolved != null)
                {
                    resolvedMustHave.Add(resolved);
                }
                else
                {
                    UnresolvedMutators.Add(feat);
                    if (Prefs.DevMode)
                        LandingZoneLogger.LogWarning($"[LandingZone] MapFeatureFilter: MustHave mutator '{feat}' not found in world (no known aliases). Skipping.");
                }
            }

            var resolvedMustNotHave = new List<string>();
            foreach (var feat in mustNotHaveFeats)
            {
                var resolved = ResolveToRuntimeMutator(feat);
                if (resolved != null)
                {
                    resolvedMustNotHave.Add(resolved);
                }
                else
                {
                    UnresolvedMutators.Add(feat);
                    if (Prefs.DevMode)
                        LandingZoneLogger.LogWarning($"[LandingZone] MapFeatureFilter: MustNotHave mutator '{feat}' not found in world (no known aliases). Skipping.");
                }
            }

            if (Prefs.DevMode && LandingZoneLogger.IsVerbose)
            {
                LandingZoneLogger.LogVerbose($"[LandingZone] MapFeatureFilter.Apply: Original MustHave=[{string.Join(", ", mustHaveFeats)}], MustNotHave=[{string.Join(", ", mustNotHaveFeats)}]");
                LandingZoneLogger.LogVerbose($"[LandingZone] MapFeatureFilter.Apply: Resolved MustHave=[{string.Join(", ", resolvedMustHave)}], MustNotHave=[{string.Join(", ", resolvedMustNotHave)}]");
                if (UnresolvedMutators.Count > 0)
                    LandingZoneLogger.LogVerbose($"[LandingZone] MapFeatureFilter.Apply: Unresolved=[{string.Join(", ", UnresolvedMutators)}]");
            }

            // If all MustHave items were unresolved, pass all tiles (don't filter on nothing)
            if (resolvedMustHave.Count == 0 && mustHaveFeats.Count > 0)
            {
                if (Prefs.DevMode)
                    LandingZoneLogger.LogWarning($"[LandingZone] MapFeatureFilter.Apply: All MustHave mutators unresolved, skipping filter (would return 0 tiles otherwise)");
                return inputTiles;
            }

            // Filter for tiles that meet resolved requirements
            int rejectionCount = 0;
            var result = inputTiles.Where(id =>
            {
                var tileFeatures = GetTileMapFeatures(id).ToList();

                // Check resolved MustHave (using AND/OR operator from container)
                bool meetsMustHave = MeetsResolvedMustHave(tileFeatures, resolvedMustHave, mapFeatures.Operator);

                // Check resolved MustNotHave (always AND - ALL exclusions must be satisfied)
                bool meetsMustNotHave = !resolvedMustNotHave.Any(excl => tileFeatures.Contains(excl));

                bool meets = meetsMustHave && meetsMustNotHave;

                if (Prefs.DevMode && LandingZoneLogger.IsVerbose && !meets && rejectionCount < 5)
                {
                    LandingZoneLogger.LogVerbose($"[LandingZone] MapFeatureFilter: Tile {id} REJECTED - has features: [{string.Join(", ", tileFeatures)}]");
                    rejectionCount++;
                }

                return meets;
            }).ToList();

            if (Prefs.DevMode && LandingZoneLogger.IsStandardOrVerbose)
                LandingZoneLogger.LogStandard($"[LandingZone] MapFeatureFilter.Apply: Filtered {inputTiles.Count()} → {result.Count} tiles");

            // Diagnostic: Log first 20 passing tiles and their features
            if (Prefs.DevMode && LandingZoneLogger.IsVerbose && result.Count > 0)
            {
                LandingZoneLogger.LogVerbose($"[LandingZone] MapFeatureFilter.Apply: First {System.Math.Min(20, result.Count)} passing tiles:");
                foreach (var tileId in result.Take(20))
                {
                    var tileFeatures = GetTileMapFeatures(tileId).ToList();
                    var featureList = tileFeatures.Count > 0 ? string.Join(", ", tileFeatures) : "(no features)";
                    LandingZoneLogger.LogVerbose($"[LandingZone]   Tile {tileId}: [{featureList}]");
                }
            }

            return result;
        }

        /// <summary>
        /// Checks if tile features meet resolved MustHave requirements, respecting AND/OR operator.
        /// </summary>
        private static bool MeetsResolvedMustHave(List<string> tileFeatures, List<string> resolvedMustHave, ImportanceOperator op)
        {
            if (resolvedMustHave.Count == 0)
                return true; // No requirements = pass

            if (op == ImportanceOperator.OR)
            {
                // OR: At least one must be present
                return resolvedMustHave.Any(req => tileFeatures.Contains(req));
            }
            else
            {
                // AND: All must be present
                return resolvedMustHave.All(req => tileFeatures.Contains(req));
            }
        }

        public string Describe(FilterContext context)
        {
            var filters = context.Filters;
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
        internal static IEnumerable<string> GetTileMapFeatures(int tileId)
        {
            var result = new List<string>();
            var tile = Find.WorldGrid?[tileId];
            bool shouldLogVerbose = Prefs.DevMode && LandingZoneLogger.IsVerbose;
            if (tile == null)
            {
                if (shouldLogVerbose && tileId < 5)
                    LandingZoneLogger.LogWarning($"[LandingZone] GetTileMapFeatures({tileId}): Find.WorldGrid or tile is null");
                return result;
            }

            // Access the Mutators property via reflection (RimWorld 1.6+)
            try
            {
                var tileType = tile.GetType();
                var mutatorsProp = tileType.GetProperty("Mutators");

                if (shouldLogVerbose && tileId < 5)
                {
                    LandingZoneLogger.LogVerbose($"[LandingZone] GetTileMapFeatures({tileId}): Tile type = {tileType.FullName}");
                    LandingZoneLogger.LogVerbose($"[LandingZone] GetTileMapFeatures({tileId}): Mutators property found = {mutatorsProp != null}");
                }

                if (mutatorsProp != null)
                {
                    var mutators = mutatorsProp.GetValue(tile);

                    if (shouldLogVerbose && tileId < 5)
                    {
                        LandingZoneLogger.LogVerbose($"[LandingZone] GetTileMapFeatures({tileId}): Mutators value type = {mutators?.GetType().FullName ?? "null"}");
                        LandingZoneLogger.LogVerbose($"[LandingZone] GetTileMapFeatures({tileId}): Is IEnumerable = {mutators is System.Collections.IEnumerable}");
                    }

                    if (mutators is System.Collections.IEnumerable enumerable)
                    {
                        int mutatorCount = 0;
                        foreach (var mutator in enumerable)
                        {
                            mutatorCount++;
                            if (mutator != null)
                            {
                                var mutatorType = mutator.GetType();
                                var mutatorName = mutator.ToString();

                                if (shouldLogVerbose && tileId < 5)
                                {
                                    LandingZoneLogger.LogVerbose($"[LandingZone] GetTileMapFeatures({tileId}): Mutator #{mutatorCount}: Type={mutatorType.FullName}, ToString()='{mutatorName}'");
                                }

                                if (!string.IsNullOrEmpty(mutatorName))
                                {
                                    result.Add(mutatorName);
                                }
                            }
                            else if (shouldLogVerbose && tileId < 5)
                            {
                                LandingZoneLogger.LogWarning($"[LandingZone] GetTileMapFeatures({tileId}): Mutator #{mutatorCount} is null");
                            }
                        }

                        if (shouldLogVerbose && tileId < 5)
                            LandingZoneLogger.LogVerbose($"[LandingZone] GetTileMapFeatures({tileId}): Total mutators found = {mutatorCount}, extracted names = {result.Count}");
                    }
                }
                else if (shouldLogVerbose && tileId < 5)
                {
                    // Log available properties for diagnostic purposes
                    var properties = tileType.GetProperties().Select(p => p.Name);
                    LandingZoneLogger.LogWarning($"[LandingZone] GetTileMapFeatures({tileId}): 'Mutators' property not found. Available properties: [{string.Join(", ", properties)}]");
                }
            }
            catch (System.Exception ex)
            {
                if (shouldLogVerbose && tileId < 5)
                    LandingZoneLogger.LogError($"[LandingZone] GetTileMapFeatures({tileId}): Exception during reflection: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }

            return result;
        }

        /// <summary>
        /// Gets all available map feature types for UI display.
        /// CHANGED: Now prioritizes runtime world scan (what actually exists) over DefDatabase.
        /// This fixes the issue where mods replace vanilla mutators (e.g., River → GL_River).
        /// </summary>
        public static IEnumerable<string> GetAllMapFeatureTypes()
        {
            // PRIMARY SOURCE: Runtime mutators (what actually exists in the current world)
            // This is authoritative - if a mutator doesn't exist here, selecting it won't find any tiles
            EnsureRuntimeCacheValid();

            if (_runtimeMutatorCache.Count > 0)
            {
                if (Prefs.DevMode && LandingZoneLogger.IsStandardOrVerbose)
                    LandingZoneLogger.LogStandard($"[LandingZone] GetAllMapFeatureTypes: Using {_runtimeMutatorCache.Count} runtime mutators from world scan");

                // Sort with Caves first (most commonly used), then alphabetically
                return _runtimeMutatorCache.OrderBy(f => f == "Caves" ? "0" : f);
            }

            // FALLBACK: DefDatabase (only if no world is loaded yet)
            var featureTypes = new HashSet<string>();
            try
            {
                var worldTileDefType = GenTypes.GetTypeInAnyAssembly("RimWorld.Planet.WorldTileDef");
                if (worldTileDefType != null)
                {
                    var defDatabaseType = typeof(DefDatabase<>).MakeGenericType(worldTileDefType);
                    var allDefsProperty = defDatabaseType.GetProperty("AllDefsListForReading");

                    if (allDefsProperty != null)
                    {
                        var allDefs = allDefsProperty.GetValue(null) as System.Collections.IEnumerable;
                        if (allDefs != null)
                        {
                            foreach (var def in allDefs)
                            {
                                if (def != null)
                                {
                                    var defNameProp = def.GetType().GetProperty("defName");
                                    if (defNameProp != null)
                                    {
                                        var defNameObj = defNameProp.GetValue(def);
                                        var defName = defNameObj?.ToString();
                                        if (!string.IsNullOrWhiteSpace(defName))
                                        {
                                            featureTypes.Add(defName!);
                                        }
                                    }
                                }
                            }

                            if (Prefs.DevMode && LandingZoneLogger.IsStandardOrVerbose)
                                LandingZoneLogger.LogStandard($"[LandingZone] GetAllMapFeatureTypes: Fallback to DefDatabase - {featureTypes.Count} mutators (world not yet loaded)");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                if (Prefs.DevMode)
                    LandingZoneLogger.LogError($"[LandingZone] GetAllMapFeatureTypes: DefDatabase query failed: {ex.Message}");
            }

            // Sort with Caves first (most commonly used), then alphabetically
            return featureTypes.OrderBy(f => f == "Caves" ? "0" : f);
        }

        /// <summary>
        /// Converts mutator defName to user-friendly label.
        /// Attempts to load from DefDatabase and get .label property, falls back to defName if unavailable.
        /// </summary>
        public static string GetMutatorFriendlyLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName))
                return defName;

            try
            {
                // Try to get the Def from DefDatabase using generic GetNamed
                var defDatabaseType = typeof(DefDatabase<>);
                var worldTileDefType = GenTypes.GetTypeInAnyAssembly("RimWorld.Planet.WorldTileDef");

                if (worldTileDefType != null)
                {
                    var specificDefDatabase = defDatabaseType.MakeGenericType(worldTileDefType);
                    var getNamedMethod = specificDefDatabase.GetMethod("GetNamedSilentFail");

                    if (getNamedMethod != null)
                    {
                        var mutatorDef = getNamedMethod.Invoke(null, new object[] { defName });

                        if (mutatorDef != null)
                        {
                            // Try to get label or LabelCap property
                            var labelCapProp = mutatorDef.GetType().GetProperty("LabelCap");
                            if (labelCapProp != null)
                            {
                                var labelCap = labelCapProp.GetValue(mutatorDef);
                                if (labelCap != null)
                                    return labelCap.ToString();
                            }

                            var labelProp = mutatorDef.GetType().GetProperty("label");
                            if (labelProp != null)
                            {
                                var label = labelProp.GetValue(mutatorDef);
                                if (label != null && !string.IsNullOrEmpty(label.ToString()))
                                    return GenText.ToTitleCaseSmart(label.ToString());
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                // Silently fail - this is just for friendly labels
                if (Prefs.DevMode && LandingZoneLogger.IsVerbose)
                    LandingZoneLogger.LogVerbose($"[LandingZone] GetMutatorFriendlyLabel({defName}): {ex.Message}");
            }

            // Fallback: Convert defName to friendly format
            // "SteamGeysers_Increased" → "Steam Geysers Increased"
            return System.Text.RegularExpressions.Regex.Replace(defName, "([a-z])([A-Z])", "$1 $2")
                .Replace("_", " ");
        }

        /// <summary>
        /// Gets the DLC requirement for a specific mutator.
        /// Returns null if mutator is from Core game or couldn't be determined.
        /// </summary>
        public static string? GetMutatorDLCRequirement(string defName)
        {
            if (string.IsNullOrEmpty(defName))
                return null;

            try
            {
                // Try to get the Def from DefDatabase
                var defDatabaseType = typeof(DefDatabase<>);
                var worldTileDefType = GenTypes.GetTypeInAnyAssembly("RimWorld.Planet.WorldTileDef");

                if (worldTileDefType != null)
                {
                    var specificDefDatabase = defDatabaseType.MakeGenericType(worldTileDefType);
                    var getNamedMethod = specificDefDatabase.GetMethod("GetNamedSilentFail");

                    if (getNamedMethod != null)
                    {
                        var mutatorDef = getNamedMethod.Invoke(null, new object[] { defName });

                        if (mutatorDef != null)
                        {
                            // Get modContentPack property
                            var modContentPackProp = mutatorDef.GetType().GetProperty("modContentPack");
                            if (modContentPackProp != null)
                            {
                                var modContentPack = modContentPackProp.GetValue(mutatorDef);
                                if (modContentPack != null)
                                {
                                    // Get PackageId from modContentPack
                                    var packageIdProp = modContentPack.GetType().GetProperty("PackageId");
                                    if (packageIdProp != null)
                                    {
                                        var packageIdObj = packageIdProp.GetValue(modContentPack);
                                        var packageId = packageIdObj?.ToString();
                                        if (!string.IsNullOrEmpty(packageId))
                                        {
                                            // Use DLCDetectionService to convert packageId to DLC label
                                            string dlcLabel = DLCDetectionService.GetDLCLabel(packageId!);

                                            // Only return non-Core DLCs
                                            if (dlcLabel != "Core")
                                                return dlcLabel;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                // Silently fail - this is just for DLC detection
                if (Prefs.DevMode && LandingZoneLogger.IsVerbose)
                    LandingZoneLogger.LogVerbose($"[LandingZone] GetMutatorDLCRequirement({defName}): {ex.Message}");
            }

            return null; // Core or couldn't determine
        }

        /// <summary>
        /// Source type for a mutator - Core (vanilla), DLC, or Mod
        /// </summary>
        public enum MutatorSourceType { Core, DLC, Mod }

        /// <summary>
        /// Information about where a mutator comes from.
        /// </summary>
        public readonly struct MutatorSourceInfo
        {
            public MutatorSourceInfo(MutatorSourceType type, string sourceName)
            {
                Type = type;
                SourceName = sourceName;
            }

            public MutatorSourceType Type { get; }
            public string SourceName { get; }

            /// <summary>
            /// Returns badge text like "[DLC]", "[MOD]", or empty for Core.
            /// </summary>
            public string BadgeText => Type switch
            {
                MutatorSourceType.DLC => "LandingZone_MutatorSourceDLC".Translate(),
                MutatorSourceType.Mod => "LandingZone_MutatorSourceMod".Translate(),
                _ => ""
            };

            /// <summary>
            /// Returns tooltip text describing the source.
            /// </summary>
            public string TooltipText => Type switch
            {
                MutatorSourceType.DLC => $"Requires {SourceName} DLC",
                MutatorSourceType.Mod => $"Added by {SourceName} mod",
                _ => "Base game content"
            };
        }

        // Known DLC package IDs (case-insensitive matching)
        private static readonly HashSet<string> KnownDLCKeywords = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "Royalty", "Ideology", "Biotech", "Anomaly", "Odyssey"
        };

        /// <summary>
        /// Gets detailed source information for a mutator (Core, DLC, or Mod).
        /// </summary>
        public static MutatorSourceInfo GetMutatorSource(string defName)
        {
            if (string.IsNullOrEmpty(defName))
                return new MutatorSourceInfo(MutatorSourceType.Core, "Core");

            try
            {
                var defDatabaseType = typeof(DefDatabase<>);
                var worldTileDefType = GenTypes.GetTypeInAnyAssembly("RimWorld.Planet.WorldTileDef");

                if (worldTileDefType != null)
                {
                    var specificDefDatabase = defDatabaseType.MakeGenericType(worldTileDefType);
                    var getNamedMethod = specificDefDatabase.GetMethod("GetNamedSilentFail");

                    if (getNamedMethod != null)
                    {
                        var mutatorDef = getNamedMethod.Invoke(null, new object[] { defName });

                        if (mutatorDef != null)
                        {
                            var modContentPackProp = mutatorDef.GetType().GetProperty("modContentPack");
                            if (modContentPackProp != null)
                            {
                                var modContentPack = modContentPackProp.GetValue(mutatorDef);
                                if (modContentPack != null)
                                {
                                    // Get PackageId
                                    var packageIdProp = modContentPack.GetType().GetProperty("PackageId");
                                    var nameProp = modContentPack.GetType().GetProperty("Name");

                                    string? packageId = packageIdProp?.GetValue(modContentPack)?.ToString();
                                    string? modName = nameProp?.GetValue(modContentPack)?.ToString();

                                    if (!string.IsNullOrEmpty(packageId))
                                    {
                                        // Check if it's a known DLC
                                        foreach (var dlcKeyword in KnownDLCKeywords)
                                        {
                                            if (packageId!.IndexOf(dlcKeyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                                            {
                                                return new MutatorSourceInfo(MutatorSourceType.DLC, dlcKeyword);
                                            }
                                        }

                                        // Check if it's Core
                                        if (packageId!.IndexOf("ludeon.rimworld", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                                            !packageId.Contains("."))
                                        {
                                            return new MutatorSourceInfo(MutatorSourceType.Core, "Core");
                                        }

                                        // It's a mod - use friendly name if available
                                        string displayName = !string.IsNullOrEmpty(modName) ? modName! : packageId!;
                                        return new MutatorSourceInfo(MutatorSourceType.Mod, displayName);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                if (Prefs.DevMode && LandingZoneLogger.IsVerbose)
                    LandingZoneLogger.LogVerbose($"[LandingZone] GetMutatorSource({defName}): {ex.Message}");
            }

            return new MutatorSourceInfo(MutatorSourceType.Core, "Core");
        }

        public float Membership(int tileId, FilterContext context)
        {
            var mapFeatures = context.Filters.MapFeatures;

            // If no features configured, no membership
            if (!mapFeatures.HasAnyImportance)
                return 0.0f;

            var tileFeatures = GetTileMapFeatures(tileId).ToList();

            if (Prefs.DevMode && LandingZoneLogger.IsVerbose && tileId < 10)
            {
                // Debug logging for first 10 tiles
                var criticals = mapFeatures.GetCriticalItems().ToList();
                var priorities = mapFeatures.GetPriorityItems().ToList();
                var preferreds = mapFeatures.GetPreferredItems().ToList();
                LandingZoneLogger.LogVerbose($"[LandingZone] MapFeatureFilter.Membership(tile={tileId}):");
                LandingZoneLogger.LogVerbose($"  User configured: Critical=[{string.Join(", ", criticals)}], Priority=[{string.Join(", ", priorities)}], Preferred=[{string.Join(", ", preferreds)}], Operator={mapFeatures.Operator}");
                LandingZoneLogger.LogVerbose($"  Tile has: [{string.Join(", ", tileFeatures)}]");
            }

            // Align with Apply phase: prioritize Critical (MustHave) requirements
            if (mapFeatures.HasCritical)
            {
                // Use GetCriticalSatisfaction to respect AND/OR operator
                float satisfaction = mapFeatures.GetCriticalSatisfaction(tileFeatures);

                if (Prefs.DevMode && LandingZoneLogger.IsVerbose && tileId < 10)
                    LandingZoneLogger.LogVerbose($"  → Critical satisfaction: {satisfaction:F2}");

                return satisfaction;
            }

            // Priority OR Preferred: Use GetScoringScore for weighted scoring (Priority=2x, Preferred=1x)
            if (mapFeatures.HasPriority || mapFeatures.HasPreferred)
            {
                float score = mapFeatures.GetScoringScore(tileFeatures);
                // Normalize to [0,1] range based on maximum possible score
                int maxScore = mapFeatures.CountByImportance(FilterImportance.Priority) * 2
                             + mapFeatures.CountByImportance(FilterImportance.Preferred);
                float membership = maxScore > 0 ? UnityEngine.Mathf.Clamp01(score / maxScore) : 0f;

                if (Prefs.DevMode && LandingZoneLogger.IsVerbose && tileId < 10)
                    LandingZoneLogger.LogVerbose($"  → Priority+Preferred score: {score:F2}, maxScore={maxScore}, membership={membership:F2}");

                return membership;
            }

            if (Prefs.DevMode && LandingZoneLogger.IsVerbose && tileId < 10)
                LandingZoneLogger.LogVerbose("  → Returning 0.0 (no configured features found)");

            return 0.0f;
        }
    }
}

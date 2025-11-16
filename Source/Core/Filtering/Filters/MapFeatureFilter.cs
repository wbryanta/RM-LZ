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

            // If no Critical features, pass all tiles through (Preferred handled by scoring)
            if (!mapFeatures.HasCritical)
            {
                if (Prefs.DevMode && LandingZoneLogger.IsVerbose)
                    LandingZoneLogger.LogVerbose("[LandingZone] MapFeatureFilter.Apply: No critical features, passing all tiles");
                return inputTiles;
            }

            if (Prefs.DevMode && LandingZoneLogger.IsVerbose)
            {
                var criticalFeats = mapFeatures.GetCriticalItems().ToList();
                LandingZoneLogger.LogVerbose($"[LandingZone] MapFeatureFilter.Apply: Filtering for critical features: {string.Join(", ", criticalFeats)}");
            }

            // Filter for tiles that meet Critical requirements
            // Tiles must have ALL Critical features
            int rejectionCount = 0;
            var result = inputTiles.Where(id =>
            {
                var tileFeatures = GetTileMapFeatures(id).ToList();
                bool meets = mapFeatures.MeetsCriticalRequirements(tileFeatures);

                if (Prefs.DevMode && LandingZoneLogger.IsVerbose && !meets && rejectionCount < 5)
                {
                    // Log first 5 rejections for debugging
                    LandingZoneLogger.LogVerbose($"[LandingZone] MapFeatureFilter: Tile {id} REJECTED - has features: [{string.Join(", ", tileFeatures)}]");
                    rejectionCount++;
                }

                return meets;
            }).ToList();

            if (Prefs.DevMode && LandingZoneLogger.IsStandardOrVerbose)
                LandingZoneLogger.LogStandard($"[LandingZone] MapFeatureFilter.Apply: Filtered {inputTiles.Count()} → {result.Count} tiles");

            return result;
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

        public float Membership(int tileId, FilterContext context)
        {
            var mapFeatures = context.Filters.MapFeatures;

            // If no features configured, no membership
            if (!mapFeatures.HasAnyImportance)
                return 0.0f;

            var tileFeatures = GetTileMapFeatures(tileId).ToList();
            if (!tileFeatures.Any())
                return 0.0f; // No features on this tile

            if (Prefs.DevMode && LandingZoneLogger.IsVerbose && tileId < 10)
            {
                // Debug logging for first 10 tiles
                var criticals = mapFeatures.GetCriticalItems().ToList();
                var preferreds = mapFeatures.GetPreferredItems().ToList();
                LandingZoneLogger.LogVerbose($"[LandingZone] MapFeatureFilter.Membership(tile={tileId}):");
                LandingZoneLogger.LogVerbose($"  User configured: Critical=[{string.Join(", ", criticals)}], Preferred=[{string.Join(", ", preferreds)}], Operator={mapFeatures.Operator}");
                LandingZoneLogger.LogVerbose($"  Tile has: [{string.Join(", ", tileFeatures)}]");
            }

            // Align with Apply phase: prioritize Critical requirements
            if (mapFeatures.HasCritical)
            {
                // Use GetCriticalSatisfaction to respect AND/OR operator
                float satisfaction = mapFeatures.GetCriticalSatisfaction(tileFeatures);

                if (Prefs.DevMode && LandingZoneLogger.IsVerbose && tileId < 10)
                    LandingZoneLogger.LogVerbose($"  → Critical satisfaction: {satisfaction:F2}");

                return satisfaction;
            }

            // No Critical features, check Preferred (partial membership based on matches)
            if (mapFeatures.HasPreferred)
            {
                int preferredMatches = mapFeatures.CountPreferredMatches(tileFeatures);
                int preferredTotal = mapFeatures.CountByImportance(FilterImportance.Preferred);

                if (preferredTotal == 0)
                    return 0.0f;

                float preferredFraction = (float)preferredMatches / preferredTotal;

                if (Prefs.DevMode && LandingZoneLogger.IsVerbose && tileId < 10)
                    LandingZoneLogger.LogVerbose($"  → Preferred: {preferredMatches}/{preferredTotal} = {preferredFraction:F2}");

                return preferredFraction;
            }

            if (Prefs.DevMode && LandingZoneLogger.IsVerbose && tileId < 10)
                LandingZoneLogger.LogVerbose("  → Returning 0.0 (no configured features found)");

            return 0.0f;
        }
    }
}

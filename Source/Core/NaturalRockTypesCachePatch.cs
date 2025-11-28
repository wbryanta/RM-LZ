#nullable enable
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core
{
    /// <summary>
    /// Caches World.NaturalRockTypesIn() results to avoid repeated expensive lookups.
    /// Cache is cleared on world change (via WorldFinalizeInitPatch or seed change detection).
    /// </summary>
    internal static class NaturalRockTypesCachePatch
    {
        private static readonly Dictionary<int, List<ThingDef>> Cache = new();
        private static string _cachedWorldSeed = string.Empty;

        /// <summary>
        /// Postfix that caches the result for future lookups.
        /// </summary>
        public static void Postfix(ref IEnumerable<ThingDef> __result, int tile)
        {
            // Ensure cache is valid for current world
            var currentSeed = Find.World?.info?.seedString ?? string.Empty;
            if (currentSeed != _cachedWorldSeed)
            {
                ClearCache();
                _cachedWorldSeed = currentSeed;
            }

            // If already cached, return cached result
            if (Cache.TryGetValue(tile, out var cached))
            {
                __result = cached;
                return;
            }

            // Cache the result (materialize the enumerable to avoid repeated computation)
            var resultList = __result?.ToList() ?? new List<ThingDef>();
            Cache[tile] = resultList;
            __result = resultList;
        }

        /// <summary>
        /// Clears the cache. Called on world change.
        /// </summary>
        public static void ClearCache()
        {
            Cache.Clear();
            _cachedWorldSeed = string.Empty;
        }

        /// <summary>
        /// Gets cached stone types for a tile, or null if not cached.
        /// Use this for direct cache access without triggering the original method.
        /// </summary>
        public static List<ThingDef>? GetCached(int tileId)
        {
            // Ensure cache is valid for current world
            var currentSeed = Find.World?.info?.seedString ?? string.Empty;
            if (currentSeed != _cachedWorldSeed)
            {
                ClearCache();
                _cachedWorldSeed = currentSeed;
                return null;
            }

            return Cache.TryGetValue(tileId, out var cached) ? cached : null;
        }

        /// <summary>
        /// Gets the number of cached entries (for diagnostics).
        /// </summary>
        public static int CacheCount => Cache.Count;
    }
}

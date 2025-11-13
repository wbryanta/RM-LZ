using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core
{
    /// <summary>
    /// Patches World.FinalizeInit to trigger tile cache precomputation on world load.
    /// This pre-caches all tile data so searches are instant.
    ///
    /// COMMENTED OUT: Testing lazy evaluation only (no eager precomputation).
    /// With RimWorld's cached cheap properties, lazy computation should be sufficient.
    /// </summary>
    [HarmonyPatch(typeof(World), "FinalizeInit")]
    internal static class WorldFinalizeInitPatch
    {
        public static void Postfix(World __instance)
        {
            // COMMENTED OUT: Testing performance without eager precomputation
            // The lazy TileDataCache should handle on-demand computation for ~15 candidates
            // instead of eagerly computing all 295k+ tiles (which took 524 seconds).

            //try
            //{
            //    // Start tile cache precomputation in the background
            //    LandingZoneContext.StartTileCachePrecomputation();
            //}
            //catch (System.Exception ex)
            //{
            //    Log.Error($"[LandingZone] Error starting tile cache precomputation: {ex}");
            //}

            // Diagnostic dumper removed - we've discovered the API:
            // - Landmarks: tile.Landmark.name
            // - Map Features: tile.Mutators (Caves, MixedBiome, Ruins, etc.)
        }
    }
}

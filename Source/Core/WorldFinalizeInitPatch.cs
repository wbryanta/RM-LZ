using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core
{
    /// <summary>
    /// Patches World.FinalizeInit to trigger tile cache precomputation on world load.
    /// When PrecomputeGrowingDaysOnLoad is enabled, pre-caches all tile data
    /// so Growing Days filter becomes instant (like a cheap filter).
    /// </summary>
    [HarmonyPatch(typeof(World), "FinalizeInit")]
    internal static class WorldFinalizeInitPatch
    {
        public static void Postfix(World __instance)
        {
            // Clear NaturalRockTypesIn cache for new world
            NaturalRockTypesCachePatch.ClearCache();

            // Optionally start tile cache precomputation if enabled in settings
            if (LandingZoneSettings.PrecomputeGrowingDaysOnLoad)
            {
                var tileCount = __instance?.grid?.TilesCount ?? 0;
                Log.Message($"[LandingZone] Precompute Growing Days enabled - starting background precomputation for {tileCount} tiles...");
                LandingZoneContext.StartTileCachePrecomputation();
            }
        }
    }
}

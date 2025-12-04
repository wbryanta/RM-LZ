using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core
{
    /// <summary>
    /// Patches World.FinalizeInit to clear caches when a new world loads.
    /// </summary>
    [HarmonyPatch(typeof(World), "FinalizeInit")]
    internal static class WorldFinalizeInitPatch
    {
        public static void Postfix(World __instance)
        {
            // Clear NaturalRockTypesIn cache for new world
            NaturalRockTypesCachePatch.ClearCache();

            // Reset Advanced filters to empty (clean canvas for new world)
            // User's choice: Advanced starts empty unless Remix is used
            // NOTE: Don't clear ActivePreset - it refers to Simple mode's default preset
            var prefs = LandingZoneContext.State?.Preferences;
            if (prefs != null)
            {
                prefs.AdvancedFilters.ClearAll();
            }
        }
    }
}

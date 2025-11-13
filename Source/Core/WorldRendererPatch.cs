using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core
{
    /// <summary>
    /// Patches WorldRenderer to draw bookmark markers.
    /// </summary>
    [HarmonyPatch(typeof(WorldRenderer), "DrawWorldLayers")]
    internal static class WorldRendererDrawPatch
    {
        public static void Postfix()
        {
            try
            {
                // Draw bookmarks after world layers are drawn
                WorldLayerBookmarks.Draw();
            }
            catch (System.Exception ex)
            {
                Log.Error($"[LandingZone] Error drawing bookmarks: {ex}");
            }
        }
    }
}

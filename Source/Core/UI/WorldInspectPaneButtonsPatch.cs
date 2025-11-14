using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;
using LandingZone.Core;

namespace LandingZone.Core.UI
{
    [HarmonyPatch(typeof(WorldInspectPane), nameof(WorldInspectPane.DoInspectPaneButtons))]
    internal static class WorldInspectPaneButtonsPatch
    {
        public static void Postfix(Rect rect, ref float lineEndWidth)
        {
            // No longer adding buttons here - all UI consolidated to bottom panel
            // (SelectStartingSiteButtonsPatch handles everything now)
            // Keeping this patch in case we need it later, but it's a no-op for now
        }

        // Removed DrawBookmarkButton and DrawBookmarkManagerButton
        // These are now handled by icon buttons in SelectStartingSiteButtonsPatch
    }
}

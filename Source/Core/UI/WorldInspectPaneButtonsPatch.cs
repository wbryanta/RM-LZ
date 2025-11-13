using HarmonyLib;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using LandingZone.Core;

namespace LandingZone.Core.UI
{
    [HarmonyPatch(typeof(WorldInspectPane), nameof(WorldInspectPane.DoInspectPaneButtons))]
    internal static class WorldInspectPaneButtonsPatch
    {
        private const float TabWidth = 120f;
        private const float TabHeight = 24f;
        private const float TabGap = 4f;

        public static void Postfix(Rect rect, ref float lineEndWidth)
        {
            // Calculate position for tab after Planet/Terrain tabs
            // Vanilla tabs are positioned on the left side - we want to be after them
            // Use a larger estimate for vanilla tabs width to ensure we're positioned correctly
            float vanillaTabsWidth = 280f;
            float x = vanillaTabsWidth + TabGap;

            int matchCount = LandingZoneContext.LastEvaluationCount;
            string buttonLabel = matchCount > 0 ? $"Show LZs ({matchCount})" : "Show LZs";

            var buttonRect = new Rect(x, 0f, TabWidth, TabHeight);
            TooltipHandler.TipRegion(buttonRect, "View LandingZone's ranked landing site matches.");

            if (Widgets.ButtonText(buttonRect, buttonLabel))
            {
                LandingZoneResultsController.Toggle();
            }
        }
    }
}

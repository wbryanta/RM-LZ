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
        public static void Postfix(Rect rect, ref float lineEndWidth)
        {
            const float width = 150f;
            const float height = 24f;
            const float gap = 6f;

            var x = rect.width - lineEndWidth - width - gap;
            if (x < 0f)
                x = 0f;

            var buttonRect = new Rect(x, 0f, width, height);
            TooltipHandler.TipRegion(buttonRect, "View LandingZone's ranked matches.");

            if (Widgets.ButtonText(buttonRect, "LZ Results"))
            {
                LandingZoneResultsController.Toggle();
            }

            lineEndWidth += width + gap;
            LandingZoneMatchHud.Draw();
        }
    }
}

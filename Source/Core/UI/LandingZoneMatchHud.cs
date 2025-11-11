using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LandingZone.Core.UI
{
    internal static class LandingZoneMatchHud
    {
        public static void Draw()
        {
            if (!WorldRendererUtility.WorldSelected || Find.WorldCamera == null)
                return;

            var highlightState = LandingZoneContext.HighlightState;
            if (highlightState == null || !highlightState.ShowBestSites)
                return;

            if (!LandingZoneContext.TryGetCurrentMatch(out var rank, out var score))
                return;

            var total = LandingZoneContext.HighlightedMatchCount;
            var matchDisplay = total > 0 ? $"#{rank + 1}/{total}" : $"#{rank + 1}";
            var label = $"LandingZone {matchDisplay}: {(score.Score * 100f):F0}%";
            var size = Text.CalcSize(label);
            var rect = new Rect(16f, 16f, size.x + 18f, size.y + 6f);
            Widgets.DrawBoxSolid(rect, new Color(0f, 0f, 0f, 0.55f));
            var prevFont = Text.Font;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 2f, rect.width - 16f, rect.height - 4f), label);
            Text.Font = prevFont;
        }
    }
}

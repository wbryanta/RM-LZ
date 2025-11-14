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
        private const float TabWidth = 120f;
        private const float TabHeight = 24f;
        private const float TabGap = 4f;
        private const float BookmarkWidth = 80f;

        public static void Postfix(Rect rect, ref float lineEndWidth)
        {
            // Use lineEndWidth to position our buttons correctly after vanilla tabs
            // This ensures we don't overlap with Planet/Terrain buttons
            float x = lineEndWidth + TabGap;

            // Button 1: LZ Results (XX)
            int matchCount = LandingZoneContext.LastEvaluationCount;
            string resultsLabel = matchCount > 0 ? $"LZ Results ({matchCount})" : "LZ Results";
            var resultsRect = new Rect(x, 0f, TabWidth, TabHeight);
            TooltipHandler.TipRegion(resultsRect, "View LandingZone's ranked landing site matches");

            if (Widgets.ButtonText(resultsRect, resultsLabel))
            {
                LandingZoneResultsController.Toggle();
            }
            x += TabWidth + TabGap;

            // Button 2: Bookmark (POI icon toggle)
            var bookmarkRect = new Rect(x, 0f, BookmarkWidth, TabHeight);
            DrawBookmarkButton(bookmarkRect);
            x += BookmarkWidth + TabGap;

            // Button 3: Bookmark Manager (YY) - stub for future work
            var bookmarkMgrRect = new Rect(x, 0f, TabWidth, TabHeight);
            DrawBookmarkManagerButton(bookmarkMgrRect);
            x += TabWidth + TabGap;

            // Update lineEndWidth so other UI elements position correctly
            lineEndWidth = x - TabGap;
        }

        private static void DrawBookmarkButton(Rect rect)
        {
            int selectedTile = Find.WorldInterface.SelectedTile;
            bool hasTileSelected = selectedTile >= 0 && selectedTile < Find.WorldGrid.TilesCount;

            var manager = BookmarkManager.Get();
            bool isBookmarked = hasTileSelected && manager != null && manager.IsBookmarked(selectedTile);

            var prevEnabled = GUI.enabled;
            var prevColor = GUI.color;

            // Disable button if no tile selected or no game running
            GUI.enabled = hasTileSelected && manager != null;

            // Color button yellow if tile is bookmarked
            if (isBookmarked)
            {
                GUI.color = new Color(1f, 0.9f, 0.3f);
            }
            else if (!GUI.enabled)
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            }

            // POI icon: ★ (filled star) when bookmarked, ☆ (empty star) when not
            string buttonText = isBookmarked ? "★ Mark" : "☆ Mark";

            if (Widgets.ButtonText(rect, buttonText))
            {
                if (manager != null && hasTileSelected)
                {
                    manager.ToggleBookmark(selectedTile);
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
            }

            GUI.enabled = prevEnabled;
            GUI.color = prevColor;

            string tooltip = hasTileSelected
                ? (isBookmarked ? "Remove bookmark from this tile" : "Bookmark this tile for later reference")
                : "Select a tile to bookmark it";
            TooltipHandler.TipRegion(rect, tooltip);
        }

        private static void DrawBookmarkManagerButton(Rect rect)
        {
            var manager = BookmarkManager.Get();
            int bookmarkCount = manager?.Bookmarks?.Count ?? 0;

            // Disable if no bookmarks exist
            var prevEnabled = GUI.enabled;
            GUI.enabled = bookmarkCount > 0;

            string label = bookmarkCount > 0 ? $"Bookmarks ({bookmarkCount})" : "Bookmarks";

            if (Widgets.ButtonText(rect, label))
            {
                // TODO: Open bookmark manager window (LZ-BOOKMARKS task)
                Messages.Message("Bookmark Manager coming soon (task LZ-BOOKMARKS)", MessageTypeDefOf.RejectInput, historical: false);
            }

            GUI.enabled = prevEnabled;

            string tooltip = bookmarkCount > 0
                ? $"View and manage {bookmarkCount} bookmarked tiles"
                : "No bookmarks yet - mark tiles to save them";
            TooltipHandler.TipRegion(rect, tooltip);
        }
    }
}

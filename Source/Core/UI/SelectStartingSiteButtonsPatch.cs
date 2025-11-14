using System;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using LandingZone.Core;
using Verse.Sound;

namespace LandingZone.Core.UI
{
    [HarmonyPatch(typeof(Page_SelectStartingSite), "DoCustomBottomButtons")]
    internal static class SelectStartingSiteButtonsPatch
    {
        public static bool Prefix(Page_SelectStartingSite __instance)
        {
            LandingZoneBottomButtonDrawer.Draw(__instance);
            return false;
        }
    }

    /// <summary>
    /// Patches DoWindowContents to tick the evaluation job every frame.
    /// This allows background evaluation to work on the world selection screen
    /// where GameComponent.GameComponentTick() doesn't run.
    /// </summary>
    [HarmonyPatch(typeof(Page_SelectStartingSite), "DoWindowContents")]
    internal static class SelectStartingSiteDoWindowContentsPatch
    {
        public static void Postfix()
        {
            // Tick the background evaluation job if one is active
            LandingZoneContext.StepEvaluation();
        }
    }

    internal static class LandingZoneBottomButtonDrawer
    {
        private const float Gap = 10f;
        private static readonly Vector2 ButtonSize = new Vector2(150f, 38f);
        private static readonly Func<Page, bool> CanDoBack = AccessTools.MethodDelegate<Func<Page, bool>>(AccessTools.Method(typeof(Page), "CanDoBack"));
        private static readonly Action<Page> DoBack = AccessTools.MethodDelegate<Action<Page>>(AccessTools.Method(typeof(Page), "DoBack"));
        private static readonly Func<Page_SelectStartingSite, bool> CanDoNext = AccessTools.MethodDelegate<Func<Page_SelectStartingSite, bool>>(AccessTools.Method(typeof(Page_SelectStartingSite), "CanDoNext"));
        private static readonly Action<Page_SelectStartingSite> DoNext = AccessTools.MethodDelegate<Action<Page_SelectStartingSite>>(AccessTools.Method(typeof(Page_SelectStartingSite), "DoNext"));

        public static void Draw(Page_SelectStartingSite page)
        {
            const int primaryButtons = 4; // Back, Random, Factions, Next
            float width = ButtonSize.x * primaryButtons + Gap * (primaryButtons + 1);
            float height = ButtonSize.y * 2f + Gap * 4f + 16f;
            Rect rect = new Rect(((float)Verse.UI.screenWidth - width) / 2f, (float)Verse.UI.screenHeight - height - 4f, width, height);

            WorldInspectPane inspectPane = Find.WindowStack.WindowOfType<WorldInspectPane>();
            if (inspectPane != null)
            {
                float paneWidth = InspectPaneUtility.PaneWidthFor(inspectPane) + 4f;
                if (rect.x < paneWidth)
                {
                    rect.x = paneWidth;
                }
            }

            Widgets.DrawWindowBackground(rect);
            Text.Font = GameFont.Small;

            float cursorX = rect.xMin + Gap;
            float cursorY = rect.yMin + Gap;

            DrawBackButton(page, new Rect(cursorX, cursorY, ButtonSize.x, ButtonSize.y));
            cursorX += ButtonSize.x + Gap;
            DrawRandomSiteButton(new Rect(cursorX, cursorY, ButtonSize.x, ButtonSize.y));
            cursorX += ButtonSize.x + Gap;
            DrawFactionsButton(new Rect(cursorX, cursorY, ButtonSize.x, ButtonSize.y));
            cursorX += ButtonSize.x + Gap;
            DrawNextButton(page, new Rect(cursorX, cursorY, ButtonSize.x, ButtonSize.y));

            var highlightRow = new Rect(rect.xMin + Gap, cursorY + ButtonSize.y + Gap, width - Gap * 2f, ButtonSize.y);
            DrawHighlightRow(highlightRow);

            // Status/icons row: status on left, bookmark icons on right
            var statusIconsRow = new Rect(highlightRow.x, highlightRow.yMax + 2f, highlightRow.width, 20f);
            DrawStatusAndIcons(statusIconsRow);

            GenUI.AbsorbClicksInRect(rect);
        }

        private static void DrawBackButton(Page_SelectStartingSite page, Rect rect)
        {
            bool clicked = Widgets.ButtonText(rect, "Back".Translate());
            bool cancelKey = KeyBindingDefOf.Cancel.KeyDownEvent;
            if ((clicked || cancelKey) && CanDoBack(page))
            {
                DoBack(page);
            }
        }

        private static void DrawRandomSiteButton(Rect rect)
        {
            if (!Widgets.ButtonText(rect, "SelectRandomSite".Translate()))
                return;

            SoundDefOf.Click.PlayOneShotOnCamera();
            if (ModsConfig.OdysseyActive && Rand.Bool)
            {
                Find.WorldInterface.SelectedTile = TileFinder.RandomSettlementTileFor(Find.WorldGrid.Surface, Faction.OfPlayer, mustBeAutoChoosable: true, (PlanetTile x) => x.Tile.Landmark != null);
            }
            else
            {
                Find.WorldInterface.SelectedTile = TileFinder.RandomStartingTile();
            }

            Find.WorldCameraDriver.JumpTo(Find.WorldGrid.GetTileCenter(Find.WorldInterface.SelectedTile));
        }

        private static void DrawBookmarkToggleButton(Rect rect)
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

            string buttonText = isBookmarked ? "★ Bookmarked" : "☆ Bookmark";

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

        private static void DrawHighlightRow(Rect rect)
        {
            var highlightState = LandingZoneContext.HighlightState;
            bool isShowing = highlightState?.ShowBestSites ?? false;

            const float navWidth = 44f;
            const float filtersWidth = 80f;
            const float topButtonWidth = 90f;
            const float innerGap = 6f;

            // Top row: [ < ][ Search Landing Zones ][ Filters ][ Top (XX) ][ > ]
            var leftNavRect = new Rect(rect.x, rect.y, navWidth, rect.height);
            var rightNavRect = new Rect(rect.xMax - navWidth, rect.y, navWidth, rect.height);
            var topButtonRect = new Rect(rightNavRect.x - topButtonWidth - innerGap, rect.y, topButtonWidth, rect.height);
            var filtersRect = new Rect(topButtonRect.x - filtersWidth - innerGap, rect.y, filtersWidth, rect.height);
            float searchButtonWidth = filtersRect.x - innerGap - (leftNavRect.xMax + innerGap);
            var searchButtonRect = new Rect(leftNavRect.xMax + innerGap, rect.y, searchButtonWidth, rect.height);

            // Search button (green when active)
            var prevColor = GUI.color;
            GUI.color = isShowing ? new Color(0.55f, 0.85f, 0.55f) : Color.white;
            if (Widgets.ButtonText(searchButtonRect, "Search Landing Zones"))
            {
                if (highlightState != null)
                {
                    CheckComplexityAndStartSearch(highlightState);
                }
            }
            GUI.color = prevColor;
            TooltipHandler.TipRegion(searchButtonRect, "Search for best landing sites based on current filters");

            // Filters button (opens results window)
            if (Widgets.ButtonText(filtersRect, "Filters"))
            {
                LandingZoneResultsController.Toggle();
            }
            TooltipHandler.TipRegion(filtersRect, "View LandingZone's ranked landing site matches");

            // Top (XX) button
            int maxResults = LandingZoneContext.State?.Preferences?.Filters?.MaxResults ?? 20;
            string topLabel = $"Top ({maxResults})";
            if (Widgets.ButtonText(topButtonRect, topLabel))
            {
                // TODO: Open dialog to adjust MaxResults (could be a simple slider dialog)
                Messages.Message("Max results control coming soon", MessageTypeDefOf.RejectInput, historical: false);
            }
            TooltipHandler.TipRegion(topButtonRect, $"Show top {maxResults} matches (click to adjust)");

            // Navigation arrows
            DrawMatchNavigation(leftNavRect, rightNavRect);
        }

        private static string GetPresetDisplayName()
        {
            var state = LandingZoneContext.State;
            if (state == null) return "default";

            var filters = state.Preferences.Filters;

            // Check if user has modified any filter values from defaults
            bool isCustom =
                filters.AverageTemperatureRange.min != 10f || filters.AverageTemperatureRange.max != 32f ||
                filters.RainfallRange.min != 1000f || filters.RainfallRange.max != 2200f ||
                filters.GrowingDaysRange.min != 40f || filters.GrowingDaysRange.max != 60f ||
                filters.CoastalImportance != LandingZone.Data.FilterImportance.Ignored ||
                filters.LockedBiome != null ||
                filters.RequiredFeatureDefName != null;

            return isCustom ? "custom" : "default";
        }

        private static void DrawStatusAndIcons(Rect rect)
        {
            const float iconSize = 20f;
            const float iconGap = 4f;

            // Right side: icon buttons
            float iconsWidth = iconSize * 2 + iconGap;
            var iconsRect = new Rect(rect.xMax - iconsWidth, rect.y, iconsWidth, rect.height);
            var bookmarkMgrIconRect = new Rect(iconsRect.xMax - iconSize, rect.y, iconSize, iconSize);
            var bookmarkIconRect = new Rect(bookmarkMgrIconRect.x - iconSize - iconGap, rect.y, iconSize, iconSize);

            // Left side: status text
            var statusRect = new Rect(rect.x, rect.y, rect.width - iconsWidth - 8f, rect.height);

            // Draw status text
            var prevFont = Text.Font;
            Text.Font = GameFont.Tiny;
            var prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            string status;

            if (LandingZoneContext.IsTileCachePrecomputing)
            {
                int percent = (int)(LandingZoneContext.TileCacheProgress * 100f);
                int processed = LandingZoneContext.TileCacheProcessedTiles;
                int total = LandingZoneContext.TileCacheTotalTiles;
                status = $"Analyzing world... {percent}% ({processed:N0}/{total:N0} tiles)";
            }
            else if (LandingZoneContext.IsEvaluating)
            {
                string phaseDesc = LandingZoneContext.CurrentPhaseDescription;
                if (!string.IsNullOrEmpty(phaseDesc))
                {
                    status = $"{(LandingZoneContext.EvaluationProgress * 100f):F0}% - {phaseDesc}";
                }
                else
                {
                    status = $"Searching... {(LandingZoneContext.EvaluationProgress * 100f):F0}%";
                }
            }
            else if (LandingZoneContext.LastEvaluationCount > 0)
            {
                status = $"{LandingZoneContext.LastEvaluationCount} matches | {LandingZoneContext.LastEvaluationMs:F0} ms";
            }
            else
            {
                status = "No matches yet";
            }
            Widgets.Label(statusRect, status);
            GUI.color = prevColor;
            Text.Font = prevFont;

            // Draw bookmark toggle icon
            DrawBookmarkIcon(bookmarkIconRect);

            // Draw bookmark manager icon
            DrawBookmarkManagerIcon(bookmarkMgrIconRect);
        }

        private static void DrawBookmarkIcon(Rect rect)
        {
            int selectedTile = Find.WorldInterface.SelectedTile;
            bool hasTileSelected = selectedTile >= 0 && selectedTile < Find.WorldGrid.TilesCount;

            var manager = BookmarkManager.Get();
            bool isBookmarked = hasTileSelected && manager != null && manager.IsBookmarked(selectedTile);

            var prevEnabled = GUI.enabled;
            var prevColor = GUI.color;

            GUI.enabled = hasTileSelected && manager != null;

            // Icon: ★ (filled star) when bookmarked (green), ☆ (empty star) when not (white)
            if (isBookmarked)
            {
                GUI.color = new Color(0.4f, 1f, 0.4f); // Bright green
            }
            else if (!GUI.enabled)
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.5f); // Dimmed
            }
            else
            {
                GUI.color = Color.white;
            }

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            string icon = isBookmarked ? "★" : "☆";

            if (Widgets.ButtonText(rect, icon, drawBackground: false))
            {
                if (manager != null && hasTileSelected)
                {
                    manager.ToggleBookmark(selectedTile);
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
            }

            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            GUI.enabled = prevEnabled;
            GUI.color = prevColor;

            string tooltip = hasTileSelected
                ? (isBookmarked ? "Remove bookmark from this tile" : "Bookmark this tile for later reference")
                : "Select a tile to bookmark it";
            TooltipHandler.TipRegion(rect, tooltip);
        }

        private static void DrawBookmarkManagerIcon(Rect rect)
        {
            var manager = BookmarkManager.Get();
            int bookmarkCount = manager?.Bookmarks?.Count ?? 0;

            var prevEnabled = GUI.enabled;
            var prevColor = GUI.color;
            GUI.enabled = bookmarkCount > 0;

            if (!GUI.enabled)
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            }
            else
            {
                GUI.color = Color.white;
            }

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;

            // Use ≡ (triple bar) icon for manager
            if (Widgets.ButtonText(rect, "≡", drawBackground: false))
            {
                Messages.Message("Bookmark Manager coming soon", MessageTypeDefOf.RejectInput, historical: false);
            }

            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            GUI.enabled = prevEnabled;
            GUI.color = prevColor;

            string tooltip = bookmarkCount > 0
                ? $"View and manage {bookmarkCount} bookmarked tiles"
                : "No bookmarks yet - mark tiles to save them";
            TooltipHandler.TipRegion(rect, tooltip);
        }

        private static void DrawMatchNavigation(Rect leftRect, Rect rightRect)
        {
            bool enabled = LandingZoneContext.HasMatches && !LandingZoneContext.IsEvaluating;

            var prevEnabled = GUI.enabled;
            var prevColor = GUI.color;

            // Manually grey out buttons when disabled
            if (!enabled)
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            }
            GUI.enabled = enabled;

            if (Widgets.ButtonText(leftRect, "<") && enabled)
            {
                LandingZoneContext.FocusNextMatch(-1);
            }
            if (Widgets.ButtonText(rightRect, ">") && enabled)
            {
                LandingZoneContext.FocusNextMatch(1);
            }

            GUI.enabled = prevEnabled;
            GUI.color = prevColor;
        }

        private static void DrawFactionsButton(Rect rect)
        {
            if (Widgets.ButtonText(rect, "WorldFactionsTab".Translate()))
            {
                Find.WindowStack.Add(new Dialog_FactionDuringLanding());
            }
        }

        private static void TogglePreferencesWindow()
        {
            var existing = Find.WindowStack.WindowOfType<LandingZonePreferencesWindow>();
            if (existing != null)
            {
                existing.Close();
            }
            else
            {
                Find.WindowStack.Add(new LandingZonePreferencesWindow());
            }
        }

        private static void DrawNextButton(Page_SelectStartingSite page, Rect rect)
        {
            if (Widgets.ButtonText(rect, "Next".Translate()) && CanDoNext(page))
            {
                DoNext(page);
            }
        }

        private static void CheckComplexityAndStartSearch(Highlighting.HighlightState highlightState)
        {
            var state = LandingZoneContext.State;
            var filters = LandingZoneContext.Filters;

            if (state == null || filters == null)
            {
                // Fallback: start search without warning
                highlightState.ShowBestSites = true;
                LandingZoneContext.RequestEvaluation(EvaluationRequestSource.ShowBestSites, focusOnComplete: true);
                return;
            }

            // Estimate search complexity
            var (estimatedSeconds, warningMessage, shouldWarn) = filters.EstimateSearchComplexity(state);

            if (shouldWarn)
            {
                // Show confirmation dialog for expensive searches
                Dialog_MessageBox dialog = new Dialog_MessageBox(
                    warningMessage,
                    "Continue".Translate(),
                    () =>
                    {
                        // User confirmed - start the search
                        highlightState.ShowBestSites = true;
                        LandingZoneContext.RequestEvaluation(EvaluationRequestSource.ShowBestSites, focusOnComplete: true);
                    },
                    "Cancel".Translate(),
                    null,
                    null,
                    false,
                    null,
                    null
                );
                Find.WindowStack.Add(dialog);
            }
            else
            {
                // Fast search - start immediately
                highlightState.ShowBestSites = true;
                LandingZoneContext.RequestEvaluation(EvaluationRequestSource.ShowBestSites, focusOnComplete: true);
            }
        }

    }
}

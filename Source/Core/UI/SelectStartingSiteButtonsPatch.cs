#nullable enable
using System;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using LandingZone.Core;
using LandingZone.Data;
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
        private static string? _cachedWorldStats = null;
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
            const float devWidth = 60f;
            const float topButtonWidth = 90f;
            const float innerGap = 6f;

            // Top row: [ < ][ Search Landing Zones ][ Filters ][ Dev? ][ Top (XX) ][ > ]
            var leftNavRect = new Rect(rect.x, rect.y, navWidth, rect.height);
            var rightNavRect = new Rect(rect.xMax - navWidth, rect.y, navWidth, rect.height);
            var topButtonRect = new Rect(rightNavRect.x - topButtonWidth - innerGap, rect.y, topButtonWidth, rect.height);

            // Insert Dev button if in dev mode
            Rect devRect = default;
            Rect filtersRect;
            if (Prefs.DevMode)
            {
                devRect = new Rect(topButtonRect.x - devWidth - innerGap, rect.y, devWidth, rect.height);
                filtersRect = new Rect(devRect.x - filtersWidth - innerGap, rect.y, filtersWidth, rect.height);
            }
            else
            {
                filtersRect = new Rect(topButtonRect.x - filtersWidth - innerGap, rect.y, filtersWidth, rect.height);
            }

            float searchButtonWidth = filtersRect.x - innerGap - (leftNavRect.xMax + innerGap);
            var searchButtonRect = new Rect(leftNavRect.xMax + innerGap, rect.y, searchButtonWidth, rect.height);

            // Search button (green when active, shows current mode context)
            // Priority: ActivePreset > GuidedBuilder > Simple/Advanced mode
            string modeLabel;
            var activePreset = LandingZoneContext.State?.Preferences?.ActivePreset;
            if (activePreset != null)
            {
                modeLabel = activePreset.Name; // e.g., "Elysian", "Desert Oasis"
            }
            else
            {
                var currentMode = LandingZoneContext.State?.Preferences?.Options?.PreferencesUIMode ?? UIMode.Simple;
                modeLabel = currentMode switch
                {
                    UIMode.GuidedBuilder => "Guided",
                    UIMode.Advanced => "Advanced",
                    _ => "Simple"
                };
            }
            string searchLabel = $"Search Landing Zones ({modeLabel})";

            var prevColor = GUI.color;
            GUI.color = isShowing ? new Color(0.55f, 0.85f, 0.55f) : Color.white;
            if (Widgets.ButtonText(searchButtonRect, searchLabel))
            {
                if (highlightState != null)
                {
                    CheckComplexityAndStartSearch(highlightState);
                }
            }
            GUI.color = prevColor;
            TooltipHandler.TipRegion(searchButtonRect, "LandingZone_SearchTooltip".Translate(modeLabel));

            // Filters button (opens preferences/filters window)
            if (Widgets.ButtonText(filtersRect, "LandingZone_FiltersButton".Translate()))
            {
                TogglePreferencesWindow();
            }
            TooltipHandler.TipRegion(filtersRect, "LandingZone_FiltersTooltip".Translate());

            // Dev button (Dev Mode only)
            if (Prefs.DevMode)
            {
                var devPrevColor = GUI.color;
                GUI.color = new Color(1f, 0.7f, 0.7f); // Pinkish to indicate dev-only
                if (Widgets.ButtonText(devRect, "LandingZone_DevButton".Translate()))
                {
                    Find.WindowStack.Add(new DevToolsWindow());
                }
                GUI.color = devPrevColor;
                TooltipHandler.TipRegion(devRect, "LandingZone_DevTooltip".Translate());
            }

            // Top (XX) button - opens results window
            bool hasResults = LandingZoneContext.HasMatches;
            int maxResults = LandingZoneContext.State?.Preferences?.GetActiveFilters()?.MaxResults ?? 20;
            string topLabel = $"Top ({maxResults})";

            var topPrevEnabled = GUI.enabled;
            var topPrevColor = GUI.color;

            // Grey out when no results
            GUI.enabled = hasResults;
            if (!hasResults)
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            }

            if (Widgets.ButtonText(topButtonRect, topLabel))
            {
                LandingZoneResultsController.Toggle();
            }

            GUI.enabled = topPrevEnabled;
            GUI.color = topPrevColor;

            string tooltip = hasResults
                ? $"View top {maxResults} matches"
                : "No matches yet - run a search first";
            TooltipHandler.TipRegion(topButtonRect, tooltip);

            // Navigation arrows
            DrawMatchNavigation(leftNavRect, rightNavRect);
        }

        private static string GetPresetDisplayName()
        {
            var state = LandingZoneContext.State;
            if (state == null) return "default";

            var filters = state.Preferences.GetActiveFilters();

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

            // Stop button (between status text and icons, only when evaluating)
            const float stopButtonWidth = 50f;
            bool showStopButton = LandingZoneContext.IsEvaluating && LandingZoneSettings.AllowCancelSearch;
            float stopButtonSpace = showStopButton ? stopButtonWidth + iconGap : 0f;
            var stopButtonRect = new Rect(iconsRect.x - stopButtonSpace, rect.y, stopButtonWidth, rect.height);

            // Left side: status text
            var statusRect = new Rect(rect.x, rect.y, rect.width - iconsWidth - stopButtonSpace - 8f, rect.height);

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
                status = $"{LandingZoneContext.LastEvaluationCount} matches | {LandingZoneContext.LastEvaluationMs:F0} ms | {GetWorldTileStats()}";
            }
            else
            {
                status = $"No matches yet | {GetWorldTileStats()}";
            }
            Widgets.Label(statusRect, status);
            GUI.color = prevColor;
            Text.Font = prevFont;

            // Draw Stop button when evaluating (if enabled)
            if (showStopButton)
            {
                DrawStopButton(stopButtonRect);
            }

            // Draw bookmark toggle icon
            DrawBookmarkIcon(bookmarkIconRect);

            // Draw bookmark manager icon
            DrawBookmarkManagerIcon(bookmarkMgrIconRect);
        }

        private static void DrawStopButton(Rect rect)
        {
            var prevColor = GUI.color;
            var prevFont = Text.Font;

            // Dark red color
            GUI.color = new Color(0.7f, 0.15f, 0.15f);
            Text.Font = GameFont.Tiny;

            if (Widgets.ButtonText(rect, "LandingZone_StopButton".Translate(), drawBackground: true, doMouseoverSound: true, active: true))
            {
                LandingZoneContext.CancelEvaluation();
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            GUI.color = prevColor;
            Text.Font = prevFont;

            TooltipHandler.TipRegion(rect, "LandingZone_CancelSearchTooltip".Translate());
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

            // Always enable button, but tooltip shows different message
            GUI.enabled = true;

            // Dim icon when no bookmarks
            if (bookmarkCount == 0)
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
                Find.WindowStack.Add(new BookmarkManagerWindow());
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            GUI.enabled = prevEnabled;
            GUI.color = prevColor;

            string tooltip = bookmarkCount > 0
                ? $"View and manage {bookmarkCount} bookmarked tiles"
                : "Open bookmark manager (no bookmarks yet)";
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
                LandingZoneContext.RequestEvaluationWithWarning(EvaluationRequestSource.ShowBestSites, focusOnComplete: true);
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
                        // User confirmed - start the search (with heavy filter warning if needed)
                        highlightState.ShowBestSites = true;
                        LandingZoneContext.RequestEvaluationWithWarning(EvaluationRequestSource.ShowBestSites, focusOnComplete: true);
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
                // Fast search - start (with heavy filter warning if needed)
                highlightState.ShowBestSites = true;
                LandingZoneContext.RequestEvaluationWithWarning(EvaluationRequestSource.ShowBestSites, focusOnComplete: true);
            }
        }

        /// <summary>
        /// Gets formatted string showing world tile statistics: total tiles and inhabitable count/percentage.
        /// Cached to avoid recomputing every frame.
        /// </summary>
        private static string GetWorldTileStats()
        {
            if (_cachedWorldStats != null)
                return _cachedWorldStats;

            var worldGrid = Find.WorldGrid;
            if (worldGrid == null) return "";

            int totalTiles = worldGrid.TilesCount;
            int inhabitable = 0;

            // Count inhabitable tiles (biome != Ocean, SeaIce, Lake)
            for (int i = 0; i < totalTiles; i++)
            {
                var tile = worldGrid[i];
                if (tile?.PrimaryBiome != null)
                {
                    string biomeName = tile.PrimaryBiome.defName;
                    if (biomeName != "Ocean" && biomeName != "SeaIce" && biomeName != "Lake")
                    {
                        inhabitable++;
                    }
                }
            }

            float percentage = totalTiles > 0 ? (inhabitable * 100f / totalTiles) : 0f;
            _cachedWorldStats = $"World: {totalTiles:N0} tiles ({inhabitable:N0} inhabitable, {percentage:F0}%)";
            return _cachedWorldStats;
        }

    }
}

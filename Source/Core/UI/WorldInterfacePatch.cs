using HarmonyLib;
using LandingZone.Core;
using LandingZone.Data;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LandingZone.Core.UI
{
    /// <summary>
    /// Patches WorldInterface.WorldInterfaceUpdate to tick the evaluation job every frame.
    /// This is a safety net to ensure scoring continues even if GUI-driven ticks pause.
    /// </summary>
    [HarmonyPatch(typeof(WorldInterface), "WorldInterfaceUpdate")]
    internal static class WorldInterfaceUpdatePatch
    {
        public static void Postfix()
        {
            // Tick the background evaluation job if one is active
            // This is a safety net alongside the UI-driven ticks in SelectStartingSiteDoWindowContentsPatch
            LandingZoneContext.StepEvaluation();
        }
    }

    /// <summary>
    /// Patches WorldInterface to add LandingZone UI when the in-game world map is visible.
    /// This allows players to use LandingZone when planning caravans, forming new settlements, etc.
    /// </summary>
    [HarmonyPatch(typeof(WorldInterface), "WorldInterfaceOnGUI")]
    internal static class WorldInterfacePatch
    {
        public static void Postfix()
        {
            // Check if in-game world map feature is enabled (default: false / opt-in)
            if (!LandingZoneSettings.EnableInGameWorldMap)
                return;

            // Only draw when world map is actually selected (not colony view)
            if (!WorldRendererUtility.WorldSelected)
                return;

            // Only draw when in playing state (not main menu or loading)
            if (Current.ProgramState != ProgramState.Playing)
                return;

            // Don't draw if we're in the new game site selection (Page_SelectStartingSite handles that)
            if (Find.WindowStack.IsOpen<Page_SelectStartingSite>())
                return;

            // Don't draw if ESC menu or other full-screen dialogs are open
            if (Find.WindowStack.WindowsForcePause)
                return;

            // Draw LandingZone buttons on the world map
            LandingZoneWorldMapDrawer.Draw();
        }
    }

    /// <summary>
    /// Draws LandingZone UI buttons on the in-game world map screen.
    /// Similar to LandingZoneBottomButtonDrawer but adapted for the in-game context.
    /// </summary>
    internal static class LandingZoneWorldMapDrawer
    {
        private const float Gap = 10f;
        private const float ButtonHeight = 38f;

        // Button widths matching SelectStartingSiteButtonsPatch for consistency
        private const float NavWidth = 44f;
        private const float SearchButtonWidth = 220f; // Fixed width for in-game (no dynamic fill like world gen)
        private const float FiltersWidth = 80f;
        private const float DevWidth = 60f;
        private const float TopButtonWidth = 90f;
        private const float InnerGap = 6f;

        // From RimWorld's GizmoGridDrawer.DrawGizmoGrid() - gizmos occupy bottom ~124px of screen
        private const float GIZMO_GRID_BOTTOM_OFFSET = 124f;
        private const float SAFE_VERTICAL_MARGIN = 10f;
        private const float SAFE_HORIZONTAL_MARGIN = 10f;

        public static void Draw()
        {
            // Calculate button panel dimensions
            // Layout: [<] [Search] [Filters] [Dev?] [Top] [>]
            // Navigation arrows are always present (like world gen screen)
            const float labelHeight = 18f;
            const float statusRowHeight = 22f;

            // Calculate total width based on which buttons are shown
            float buttonsWidth = SearchButtonWidth + InnerGap + FiltersWidth + InnerGap + TopButtonWidth;
            if (Prefs.DevMode)
            {
                buttonsWidth += InnerGap + DevWidth;
            }
            float width = NavWidth + InnerGap + buttonsWidth + InnerGap + NavWidth + Gap * 2f;
            float height = labelHeight + Gap + ButtonHeight + Gap + statusRowHeight + Gap;

            // Position ABOVE the gizmo grid to avoid overlap with utility buttons
            // Gizmos start at screenHeight - 124, so we position above with safe margin
            float panelBottom = (float)Verse.UI.screenHeight - GIZMO_GRID_BOTTOM_OFFSET - SAFE_VERTICAL_MARGIN;
            float panelTop = panelBottom - height;

            Rect rect = new Rect(
                ((float)Verse.UI.screenWidth - width) / 2f,  // Center horizontally
                panelTop,
                width,
                height
            );

            // Adjust for inspect pane if visible (LEFT side)
            WorldInspectPane inspectPane = Find.WindowStack.WindowOfType<WorldInspectPane>();
            if (inspectPane != null)
            {
                float paneWidth = InspectPaneUtility.PaneWidthFor(inspectPane) + 4f;
                if (rect.x < paneWidth)
                {
                    rect.x = paneWidth;
                }
            }

            // Ensure we don't go off right edge
            float maxRight = (float)Verse.UI.screenWidth - SAFE_HORIZONTAL_MARGIN;
            if (rect.xMax > maxRight)
            {
                rect.x = maxRight - rect.width;
            }

            Widgets.DrawWindowBackground(rect);
            Text.Font = GameFont.Small;

            float cursorX = rect.xMin + Gap;
            float cursorY = rect.yMin + Gap;

            // Label row
            var labelRect = new Rect(cursorX, cursorY, width - Gap * 2f, labelHeight);
            DrawLabel(labelRect);
            cursorY += labelHeight + Gap;

            // Button row: [<] [Search] [Filters] [Dev?] [Top] [>] (matches world gen screen)
            var leftNavRect = new Rect(cursorX, cursorY, NavWidth, ButtonHeight);
            cursorX += NavWidth + InnerGap;

            DrawSearchButton(new Rect(cursorX, cursorY, SearchButtonWidth, ButtonHeight));
            cursorX += SearchButtonWidth + InnerGap;

            DrawFiltersButton(new Rect(cursorX, cursorY, FiltersWidth, ButtonHeight));
            cursorX += FiltersWidth + InnerGap;

            // Dev button (Dev Mode only) - same style as in world-gen ribbon
            if (Prefs.DevMode)
            {
                DrawDevButton(new Rect(cursorX, cursorY, DevWidth, ButtonHeight));
                cursorX += DevWidth + InnerGap;
            }

            DrawTopButton(new Rect(cursorX, cursorY, TopButtonWidth, ButtonHeight));
            cursorX += TopButtonWidth + InnerGap;

            var rightNavRect = new Rect(cursorX, cursorY, NavWidth, ButtonHeight);

            // Draw navigation arrows
            DrawMatchNavigation(leftNavRect, rightNavRect);

            // Status row (with bookmark icons)
            cursorX = rect.xMin + Gap;
            cursorY += ButtonHeight + Gap;
            var statusRow = new Rect(cursorX, cursorY, width - Gap * 2f, 22f);
            DrawStatusRow(statusRow);

            GenUI.AbsorbClicksInRect(rect);
        }

        private static void DrawLabel(Rect rect)
        {
            var prevFont = Text.Font;
            var prevAnchor = Text.Anchor;
            var prevColor = GUI.color;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);

            Widgets.Label(rect, "LandingZone");

            Text.Font = prevFont;
            Text.Anchor = prevAnchor;
            GUI.color = prevColor;
        }

        private static void DrawFiltersButton(Rect rect)
        {
            LandingZoneRibbonHelpers.DrawFiltersButton(rect);
        }

        private static void DrawSearchButton(Rect rect)
        {
            var highlightState = LandingZoneContext.HighlightState;
            bool isShowing = highlightState?.ShowBestSites ?? false;

            string modeLabel = LandingZoneRibbonHelpers.GetModeLabel();
            string searchLabel = $"Search ({modeLabel})";

            var prevColor = GUI.color;
            GUI.color = isShowing ? new Color(0.55f, 0.85f, 0.55f) : Color.white;

            if (Widgets.ButtonText(rect, searchLabel))
            {
                if (highlightState != null)
                {
                    highlightState.ShowBestSites = true;
                    LandingZoneContext.RequestEvaluationWithWarning(EvaluationRequestSource.ShowBestSites, focusOnComplete: true);
                }
            }

            GUI.color = prevColor;
            TooltipHandler.TipRegion(rect, "LandingZone_SearchTooltip".Translate(modeLabel));
        }

        private static void DrawTopButton(Rect rect)
        {
            LandingZoneRibbonHelpers.DrawTopButton(rect);
        }

        private static void DrawDevButton(Rect rect)
        {
            LandingZoneRibbonHelpers.DrawDevButton(rect);
        }

        private static void DrawStatusRow(Rect rect)
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

            string status = LandingZoneRibbonHelpers.GetStatusText(includeTileCacheStatus: false);
            Widgets.Label(statusRect, status);
            GUI.color = prevColor;
            Text.Font = prevFont;

            // Draw Stop button when evaluating (if enabled)
            if (showStopButton)
            {
                LandingZoneRibbonHelpers.DrawStopButton(stopButtonRect);
            }

            // Draw bookmark toggle icon
            LandingZoneRibbonHelpers.DrawBookmarkIcon(bookmarkIconRect);

            // Draw bookmark manager icon
            LandingZoneRibbonHelpers.DrawBookmarkManagerIcon(bookmarkMgrIconRect);
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
    }
}

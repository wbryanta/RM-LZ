#nullable enable
using LandingZone.Core;
using LandingZone.Data;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LandingZone.Core.UI
{
    /// <summary>
    /// Shared helper methods for LandingZone ribbon UI elements.
    /// Used by both WorldInterfacePatch (in-game world map) and SelectStartingSiteButtonsPatch (world gen).
    /// </summary>
    internal static class LandingZoneRibbonHelpers
    {
        /// <summary>
        /// Gets the current mode label for display on search buttons.
        /// Priority: ActivePreset name > GuidedBuilder > Advanced > Simple
        /// </summary>
        public static string GetModeLabel()
        {
            var activePreset = LandingZoneContext.State?.Preferences?.ActivePreset;
            if (activePreset != null)
            {
                return activePreset.Name;
            }

            var currentMode = LandingZoneContext.State?.Preferences?.Options?.PreferencesUIMode ?? UIMode.Simple;
            return currentMode switch
            {
                UIMode.GuidedBuilder => "Guided",
                UIMode.Advanced => "Advanced",
                _ => "Simple"
            };
        }

        /// <summary>
        /// Draws the Filters button that opens the preferences window.
        /// </summary>
        public static void DrawFiltersButton(Rect rect)
        {
            if (Widgets.ButtonText(rect, "LandingZone_FiltersButton".Translate()))
            {
                TogglePreferencesWindow();
            }
            TooltipHandler.TipRegion(rect, "LandingZone_FiltersTooltip".Translate());
        }

        /// <summary>
        /// Draws the Dev button (only visible in Dev Mode).
        /// </summary>
        public static void DrawDevButton(Rect rect)
        {
            var prevColor = GUI.color;
            GUI.color = new Color(1f, 0.7f, 0.7f); // Pinkish-red to indicate dev-only

            if (Widgets.ButtonText(rect, "LandingZone_DevButton".Translate()))
            {
                Find.WindowStack.Add(new DevToolsWindow());
            }

            GUI.color = prevColor;
            TooltipHandler.TipRegion(rect, "LandingZone_DevTooltip".Translate());
        }

        /// <summary>
        /// Draws the Top (XX) button that opens the results window.
        /// </summary>
        public static void DrawTopButton(Rect rect)
        {
            bool hasResults = LandingZoneContext.HasMatches;
            int maxResults = LandingZoneContext.State?.Preferences?.GetActiveFilters()?.MaxResults ?? 20;
            string topLabel = $"Top ({maxResults})";

            var prevEnabled = GUI.enabled;
            var prevColor = GUI.color;

            GUI.enabled = hasResults;
            if (!hasResults)
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            }

            if (Widgets.ButtonText(rect, topLabel))
            {
                LandingZoneResultsController.Toggle();
            }

            GUI.enabled = prevEnabled;
            GUI.color = prevColor;

            string tooltip = hasResults
                ? $"View top {maxResults} matches"
                : "No matches yet - run a search first";
            TooltipHandler.TipRegion(rect, tooltip);
        }

        /// <summary>
        /// Draws the Stop button when a search is in progress.
        /// </summary>
        public static void DrawStopButton(Rect rect)
        {
            var prevColor = GUI.color;
            var prevFont = Text.Font;

            GUI.color = new Color(0.7f, 0.15f, 0.15f); // Dark red
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

        /// <summary>
        /// Draws the bookmark toggle icon (star).
        /// </summary>
        public static void DrawBookmarkIcon(Rect rect)
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

        /// <summary>
        /// Draws the bookmark manager icon (triple bar).
        /// </summary>
        public static void DrawBookmarkManagerIcon(Rect rect)
        {
            var manager = BookmarkManager.Get();
            int bookmarkCount = manager?.Bookmarks?.Count ?? 0;

            var prevEnabled = GUI.enabled;
            var prevColor = GUI.color;

            GUI.enabled = true;

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

        /// <summary>
        /// Gets status text for display in the ribbon.
        /// </summary>
        /// <param name="includeTileCacheStatus">Whether to include tile cache precomputing status (for world gen screen).</param>
        /// <param name="worldStatsGetter">Optional function to get world stats string.</param>
        public static string GetStatusText(bool includeTileCacheStatus = false, System.Func<string>? worldStatsGetter = null)
        {
            if (includeTileCacheStatus && LandingZoneContext.IsTileCachePrecomputing)
            {
                int percent = (int)(LandingZoneContext.TileCacheProgress * 100f);
                int processed = LandingZoneContext.TileCacheProcessedTiles;
                int total = LandingZoneContext.TileCacheTotalTiles;
                return $"Analyzing world... {percent}% ({processed:N0}/{total:N0} tiles)";
            }

            if (LandingZoneContext.IsEvaluating)
            {
                string phaseDesc = LandingZoneContext.CurrentPhaseDescription;
                if (!string.IsNullOrEmpty(phaseDesc))
                {
                    return $"{(LandingZoneContext.EvaluationProgress * 100f):F0}% - {phaseDesc}";
                }
                return $"Searching... {(LandingZoneContext.EvaluationProgress * 100f):F0}%";
            }

            if (LandingZoneContext.LastEvaluationCount > 0)
            {
                string baseStatus = $"{LandingZoneContext.LastEvaluationCount} matches | {LandingZoneContext.LastEvaluationMs:F0} ms";
                if (worldStatsGetter != null)
                {
                    string worldStats = worldStatsGetter();
                    if (!string.IsNullOrEmpty(worldStats))
                    {
                        return $"{baseStatus} | {worldStats}";
                    }
                }
                return baseStatus;
            }

            if (worldStatsGetter != null)
            {
                string worldStats = worldStatsGetter();
                if (!string.IsNullOrEmpty(worldStats))
                {
                    return $"No matches yet | {worldStats}";
                }
            }

            return "No matches yet - click Search to find settlements";
        }

        /// <summary>
        /// Toggles the preferences window open/closed.
        /// </summary>
        public static void TogglePreferencesWindow()
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
    }
}

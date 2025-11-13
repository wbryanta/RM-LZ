using System.Linq;
using UnityEngine;
using Verse;
using LandingZone.Data;

namespace LandingZone.Core.UI
{
    /// <summary>
    /// Reusable UI component helpers for LandingZone windows.
    /// Provides consistent styling and common controls.
    /// </summary>
    public static class UIHelpers
    {
        // Colors
        public static readonly Color ActiveFilterColor = new Color(0.29f, 0.95f, 0.29f); // Bright green
        public static readonly Color PreferredFilterColor = new Color(0.95f, 0.9f, 0.3f); // Yellow
        public static readonly Color InactiveFilterColor = new Color(0.5f, 0.5f, 0.5f); // Grey
        public static readonly Color PartialFilterColor = new Color(0.7f, 0.7f, 1f); // Light blue

        /// <summary>
        /// Draws an importance selector (Ignored/Preferred/Critical).
        /// Returns true if filter is active (not Ignored).
        /// </summary>
        public static bool DrawImportanceSelector(Rect rect, string label, ref FilterImportance importance, string tooltip = null)
        {
            var buttonRect = rect;

            // Draw state indicator
            var indicatorRect = new Rect(rect.x, rect.y, 20f, rect.height);
            var labelRect = new Rect(rect.x + 24f, rect.y, rect.width - 24f, rect.height);

            // Draw colored indicator
            Color stateColor = importance switch
            {
                FilterImportance.Critical => ActiveFilterColor,
                FilterImportance.Preferred => PreferredFilterColor,
                FilterImportance.Ignored => InactiveFilterColor,
                _ => Color.white
            };

            var prevColor = GUI.color;
            GUI.color = stateColor;
            Widgets.DrawBoxSolid(indicatorRect, stateColor);
            GUI.color = prevColor;

            // Draw button
            string stateLabel = importance switch
            {
                FilterImportance.Critical => "Required",
                FilterImportance.Preferred => "Preferred",
                FilterImportance.Ignored => "Ignored",
                _ => "?"
            };

            if (Widgets.ButtonText(labelRect, $"{label}: {stateLabel}"))
            {
                // Cycle through states: Ignored -> Preferred -> Critical -> Ignored
                importance = importance switch
                {
                    FilterImportance.Ignored => FilterImportance.Preferred,
                    FilterImportance.Preferred => FilterImportance.Critical,
                    FilterImportance.Critical => FilterImportance.Ignored,
                    _ => FilterImportance.Ignored
                };
            }

            if (!string.IsNullOrEmpty(tooltip))
            {
                TooltipHandler.TipRegion(buttonRect, tooltip);
            }

            return importance != FilterImportance.Ignored;
        }

        /// <summary>
        /// Draws a section header with active filter count badge.
        /// </summary>
        public static void DrawSectionHeaderWithBadge(Rect rect, string title, int activeCount, bool expanded)
        {
            string arrow = expanded ? "\u25BC" : "\u25B6";
            string badgeText = activeCount > 0 ? $" ({activeCount} active)" : "";

            var prevColor = GUI.color;
            if (activeCount > 0)
            {
                GUI.color = new Color(0.8f, 1f, 0.8f); // Light green tint for active sections
            }

            Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.2f, 0.2f, 0.5f));

            var labelRect = rect.ContractedBy(4f);
            var prevFont = Text.Font;
            var prevAnchor = Text.Anchor;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;

            Widgets.Label(labelRect, $"{arrow} {title}{badgeText}");

            Text.Font = prevFont;
            Text.Anchor = prevAnchor;
            GUI.color = prevColor;
        }

        /// <summary>
        /// Draws All/None/Reset utility buttons for multi-select lists.
        /// </summary>
        public static void DrawMultiSelectUtilityButtons(Listing_Standard listing, System.Action onAll, System.Action onNone, System.Action onReset, float width = 300f)
        {
            var rect = listing.GetRect(24f);
            var buttonWidth = (width - 16f) / 3f;

            var allRect = new Rect(rect.x, rect.y, buttonWidth, rect.height);
            var noneRect = new Rect(rect.x + buttonWidth + 4f, rect.y, buttonWidth, rect.height);
            var resetRect = new Rect(rect.x + (buttonWidth + 4f) * 2, rect.y, buttonWidth, rect.height);

            if (Widgets.ButtonText(allRect, "All"))
            {
                onAll?.Invoke();
            }

            if (Widgets.ButtonText(noneRect, "None"))
            {
                onNone?.Invoke();
            }

            if (Widgets.ButtonText(resetRect, "Reset"))
            {
                onReset?.Invoke();
            }

            listing.Gap(2f);
        }

        /// <summary>
        /// Draws a DLC label badge next to content.
        /// </summary>
        public static void DrawDLCBadge(Rect rect, string dlcName, bool isAvailable)
        {
            var prevColor = GUI.color;
            GUI.color = isAvailable ? new Color(0.8f, 0.8f, 1f) : new Color(0.5f, 0.5f, 0.5f);

            Widgets.DrawBoxSolid(rect, GUI.color * 0.3f);

            var prevFont = Text.Font;
            Text.Font = GameFont.Tiny;

            Widgets.Label(rect, dlcName);

            Text.Font = prevFont;
            GUI.color = prevColor;

            if (!isAvailable)
            {
                TooltipHandler.TipRegion(rect, $"{dlcName} DLC not installed");
            }
        }

        /// <summary>
        /// Draws a search text field for filtering lists.
        /// </summary>
        public static string DrawSearchBox(Listing_Standard listing, string currentSearch, string placeholder = "Search...")
        {
            var rect = listing.GetRect(28f);
            var searchRect = rect.ContractedBy(2f);

            // Draw background
            Widgets.DrawBoxSolid(searchRect, new Color(0.15f, 0.15f, 0.15f));

            // Draw text field
            var textRect = searchRect.ContractedBy(4f);
            string newSearch = Widgets.TextField(textRect, currentSearch);

            // Draw placeholder if empty
            if (string.IsNullOrEmpty(newSearch))
            {
                var prevColor = GUI.color;
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                var prevAnchor = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(textRect, placeholder);
                Text.Anchor = prevAnchor;
                GUI.color = prevColor;
            }

            listing.Gap(2f);
            return newSearch;
        }

        /// <summary>
        /// Draws a tooltip icon that shows help text on hover.
        /// </summary>
        public static void DrawTooltipIcon(Rect rect, string tooltipText)
        {
            var prevColor = GUI.color;
            GUI.color = new Color(0.7f, 0.7f, 1f);

            Widgets.DrawBoxSolid(rect, GUI.color * 0.5f);

            var prevFont = Text.Font;
            Text.Font = GameFont.Tiny;
            var prevAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;

            Widgets.Label(rect, "?");

            Text.Anchor = prevAnchor;
            Text.Font = prevFont;
            GUI.color = prevColor;

            TooltipHandler.TipRegion(rect, tooltipText);
        }

        /// <summary>
        /// Draws a list of items with individual importance selectors.
        /// Each item gets its own Ignored/Preferred/Critical button.
        /// Compact presentation: one line per item with colored indicator.
        /// </summary>
        /// <typeparam name="T">Item type (typically string for defNames)</typeparam>
        /// <param name="listing">The listing to draw into</param>
        /// <param name="container">The IndividualImportanceContainer to modify</param>
        /// <param name="availableItems">All available items to show</param>
        /// <param name="getLabel">Function to get display label for an item</param>
        /// <param name="itemHeight">Height of each item row</param>
        /// <param name="maxHeight">Maximum height for scrollable area (0 = no scroll)</param>
        /// <returns>Number of items with non-Ignored importance</returns>
        public static int DrawIndividualImportanceList<T>(
            Listing_Standard listing,
            IndividualImportanceContainer<T> container,
            System.Collections.Generic.IEnumerable<T> availableItems,
            System.Func<T, string> getLabel,
            float itemHeight = 28f,
            float maxHeight = 0f) where T : notnull
        {
            var items = availableItems.ToList();
            if (items.Count == 0)
            {
                listing.Label("No items available");
                return 0;
            }

            int activeCount = 0;

            // If maxHeight is set, use a scrollable view
            if (maxHeight > 0f && items.Count * itemHeight > maxHeight)
            {
                var viewRect = listing.GetRect(maxHeight);
                var scrollRect = new Rect(0f, 0f, viewRect.width - 16f, items.Count * itemHeight);

                // Note: For simplicity, we're not implementing scroll state here
                // In a real implementation, you'd maintain scroll position in the window
                Widgets.BeginScrollView(viewRect, ref _scrollPosition, scrollRect);

                float y = 0f;
                foreach (var item in items)
                {
                    var itemRect = new Rect(0f, y, scrollRect.width, itemHeight - 2f);
                    var importance = container.GetImportance(item);

                    if (DrawIndividualImportanceItem(itemRect, getLabel(item), ref importance))
                    {
                        container.SetImportance(item, importance);
                    }

                    if (importance != FilterImportance.Ignored)
                        activeCount++;

                    y += itemHeight;
                }

                Widgets.EndScrollView();
            }
            else
            {
                // Draw items without scrolling
                foreach (var item in items)
                {
                    var rect = listing.GetRect(itemHeight - 2f);
                    var importance = container.GetImportance(item);

                    if (DrawIndividualImportanceItem(rect, getLabel(item), ref importance))
                    {
                        container.SetImportance(item, importance);
                    }

                    if (importance != FilterImportance.Ignored)
                        activeCount++;

                    listing.Gap(2f);
                }
            }

            return activeCount;
        }

        /// <summary>
        /// Draws a single item with importance selector.
        /// Returns true if importance changed.
        /// </summary>
        private static bool DrawIndividualImportanceItem(Rect rect, string label, ref FilterImportance importance)
        {
            var indicatorRect = new Rect(rect.x, rect.y, 20f, rect.height);
            var buttonRect = new Rect(rect.x + 24f, rect.y, rect.width - 24f, rect.height);

            // Draw colored indicator
            Color stateColor = importance switch
            {
                FilterImportance.Critical => ActiveFilterColor,
                FilterImportance.Preferred => PreferredFilterColor,
                FilterImportance.Ignored => InactiveFilterColor,
                _ => Color.white
            };

            var prevColor = GUI.color;
            GUI.color = stateColor;
            Widgets.DrawBoxSolid(indicatorRect, stateColor);
            GUI.color = prevColor;

            // Draw button with label and current state
            string stateLabel = importance switch
            {
                FilterImportance.Critical => "Required",
                FilterImportance.Preferred => "Preferred",
                FilterImportance.Ignored => "Ignored",
                _ => "?"
            };

            var oldImportance = importance;
            if (Widgets.ButtonText(buttonRect, $"{label}: {stateLabel}"))
            {
                // Cycle through states: Ignored -> Preferred -> Critical -> Ignored
                importance = importance switch
                {
                    FilterImportance.Ignored => FilterImportance.Preferred,
                    FilterImportance.Preferred => FilterImportance.Critical,
                    FilterImportance.Critical => FilterImportance.Ignored,
                    _ => FilterImportance.Ignored
                };
            }

            return importance != oldImportance;
        }

        // Scroll position for scrollable individual importance lists
        private static UnityEngine.Vector2 _scrollPosition = UnityEngine.Vector2.zero;

        /// <summary>
        /// Draws utility buttons for individual importance lists.
        /// Sets all items to specified importance level.
        /// </summary>
        public static void DrawIndividualImportanceUtilityButtons<T>(
            Listing_Standard listing,
            IndividualImportanceContainer<T> container,
            System.Collections.Generic.IEnumerable<T> availableItems,
            float width = 300f) where T : notnull
        {
            var rect = listing.GetRect(24f);
            var buttonWidth = (width - 16f) / 4f;

            var allCriticalRect = new Rect(rect.x, rect.y, buttonWidth, rect.height);
            var allPreferredRect = new Rect(rect.x + buttonWidth + 4f, rect.y, buttonWidth, rect.height);
            var allIgnoredRect = new Rect(rect.x + (buttonWidth + 4f) * 2, rect.y, buttonWidth, rect.height);
            var resetRect = new Rect(rect.x + (buttonWidth + 4f) * 3, rect.y, buttonWidth, rect.height);

            if (Widgets.ButtonText(allCriticalRect, "All Critical"))
            {
                container.SetAllTo(availableItems, FilterImportance.Critical);
            }

            if (Widgets.ButtonText(allPreferredRect, "All Preferred"))
            {
                container.SetAllTo(availableItems, FilterImportance.Preferred);
            }

            if (Widgets.ButtonText(allIgnoredRect, "All Ignored"))
            {
                container.SetAllTo(availableItems, FilterImportance.Ignored);
            }

            if (Widgets.ButtonText(resetRect, "Reset"))
            {
                container.Reset();
            }

            listing.Gap(2f);
        }
    }
}

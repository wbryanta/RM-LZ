using LandingZone.Data;
using UnityEngine;
using Verse;

namespace LandingZone.Core.UI
{
    /// <summary>
    /// Full-featured UI renderer for power users.
    /// Shows all 40+ filters organized by user intent groups, with search and data type grouping.
    /// </summary>
    public static class AdvancedModeUI
    {
        private const float SearchBoxHeight = 30f;
        private const float GroupToggleHeight = 30f;
        private const float FiltersContentHeight = 2000f; // Scrollable

        // Organization mode for advanced view
        private static FilterOrganization _currentOrganization = FilterOrganization.UserIntent;

        /// <summary>
        /// Renders the Advanced mode UI (search + grouped filters).
        /// </summary>
        /// <param name="inRect">Available drawing area</param>
        /// <param name="preferences">User preferences containing filter settings</param>
        /// <returns>Total height consumed by rendering</returns>
        public static float DrawContent(Rect inRect, UserPreferences preferences)
        {
            float curY = inRect.y;

            // Search box for filtering visible controls
            curY += DrawSearchBox(new Rect(inRect.x, curY, inRect.width, SearchBoxHeight));
            curY += 10f;

            // Organization mode toggle (User Intent vs Data Type)
            curY += DrawOrganizationToggle(new Rect(inRect.x, curY, inRect.width, GroupToggleHeight));
            curY += 10f;

            // Grouped filters (collapsible sections)
            curY += DrawGroupedFilters(new Rect(inRect.x, curY, inRect.width, FiltersContentHeight), preferences);

            return curY - inRect.y;
        }

        private static float DrawSearchBox(Rect rect)
        {
            // TODO: Implement search box
            // - Filter visible controls by name
            // - Real-time filtering as user types
            // - Highlight matching filter names

            Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.2f, 0.2f, 0.5f));
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, "SEARCH BOX - Coming in Phase 2B");
            Text.Anchor = TextAnchor.UpperLeft;

            return rect.height;
        }

        private static float DrawOrganizationToggle(Rect rect)
        {
            // TODO: Implement organization toggle
            // - Toggle between User Intent groups and Data Type groups
            // - Maybe radio buttons or segmented control
            // - Update _currentOrganization and re-render

            Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.2f, 0.2f, 0.5f));
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, "ORGANIZATION TOGGLE (Intent/Data Type) - Coming in Phase 2B");
            Text.Anchor = TextAnchor.UpperLeft;

            return rect.height;
        }

        private static float DrawGroupedFilters(Rect rect, UserPreferences preferences)
        {
            // TODO: Implement grouped filters based on _currentOrganization
            //
            // User Intent groups:
            // - Climate Comfort (temperature, rainfall, growing days)
            // - Terrain Access (coastal, rivers, roads, hilliness)
            // - Resources (stones, foraging, grazing, animals)
            // - Special Features (caves, landmarks, world features, adjacent biomes)
            //
            // Data Type groups:
            // - Temperature (avg, min, max)
            // - Geography (elevation, coastal, hilliness, swampiness)
            // - Features (caves, landmarks, rivers, roads, etc.)
            //
            // Each group:
            // - Collapsible header with active filter count badge
            // - Filter controls inside (reuse UIHelpers patterns)

            Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.2f, 0.2f, 0.5f));
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, "GROUPED FILTERS (40+ controls) - Coming in Phase 2B");
            Text.Anchor = TextAnchor.UpperLeft;

            return rect.height;
        }

        private enum FilterOrganization
        {
            UserIntent,
            DataType
        }
    }
}

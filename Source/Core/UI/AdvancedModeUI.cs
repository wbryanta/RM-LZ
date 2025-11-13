using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using UnityEngine;
using Verse;

namespace LandingZone.Core.UI
{
    /// <summary>
    /// Full-featured UI renderer for power users.
    /// Shows all 40+ filters organized by user intent groups, with search and data type grouping.
    /// </summary>
    public static partial class AdvancedModeUI
    {
        private const float SearchBoxHeight = 30f;
        private const float GroupToggleHeight = 30f;
        private const float GroupHeaderHeight = 32f;
        private const float FilterItemHeight = 30f;

        // UI State
        private static string _searchText = "";
        private static FilterOrganization _currentOrganization = FilterOrganization.UserIntent;
        private static HashSet<string> _collapsedGroups = new HashSet<string>();
        private static Vector2 _scrollPosition = Vector2.zero;

        /// <summary>
        /// Renders the Advanced mode UI (search + grouped filters).
        /// </summary>
        /// <param name="inRect">Available drawing area</param>
        /// <param name="preferences">User preferences containing filter settings</param>
        /// <returns>Total height consumed by rendering</returns>
        public static float DrawContent(Rect inRect, UserPreferences preferences)
        {
            var listing = new Listing_Standard { ColumnWidth = inRect.width };
            listing.Begin(inRect);

            // Search box for filtering visible controls
            var searchRect = listing.GetRect(SearchBoxHeight);
            DrawSearchBox(searchRect);
            listing.Gap(10f);

            // Organization mode toggle (User Intent vs Data Type)
            var toggleRect = listing.GetRect(GroupToggleHeight);
            DrawOrganizationToggle(toggleRect);
            listing.Gap(10f);

            // Grouped filters (collapsible sections)
            var groups = GetFilterGroups(_currentOrganization);
            DrawFilterGroups(listing, groups, preferences);

            listing.End();
            return listing.CurHeight;
        }

        private static void DrawSearchBox(Rect rect)
        {
            _searchText = UIHelpers.DrawSearchBox(new Listing_Standard { ColumnWidth = rect.width }, _searchText, "Search filters...");
        }

        private static void DrawOrganizationToggle(Rect rect)
        {
            // Split rect into two buttons
            float buttonWidth = rect.width / 2f - 2f;
            Rect intentRect = new Rect(rect.x, rect.y, buttonWidth, rect.height);
            Rect dataTypeRect = new Rect(rect.x + buttonWidth + 4f, rect.y, buttonWidth, rect.height);

            bool isUserIntent = _currentOrganization == FilterOrganization.UserIntent;

            if (Widgets.ButtonText(intentRect, "User Intent", active: isUserIntent))
            {
                if (_currentOrganization != FilterOrganization.UserIntent)
                {
                    _currentOrganization = FilterOrganization.UserIntent;
                    _collapsedGroups.Clear(); // Reset collapse state when switching
                }
            }

            if (Widgets.ButtonText(dataTypeRect, "Data Type", active: !isUserIntent))
            {
                if (_currentOrganization != FilterOrganization.DataType)
                {
                    _currentOrganization = FilterOrganization.DataType;
                    _collapsedGroups.Clear(); // Reset collapse state when switching
                }
            }
        }

        private static void DrawFilterGroups(Listing_Standard listing, List<FilterGroup> groups, UserPreferences preferences)
        {
            foreach (var group in groups)
            {
                // Check if group or any filters match search
                bool groupMatches = MatchesSearch(group.Name) || group.Filters.Any(f => MatchesSearch(f.Label));
                if (!string.IsNullOrEmpty(_searchText) && !groupMatches)
                    continue;

                // Count active filters
                var (totalActive, criticalCount, preferredCount) = CountActiveFilters(group, preferences.Filters);

                // Draw collapsible group header with counts
                bool isCollapsed = _collapsedGroups.Contains(group.Id);
                string headerText = totalActive > 0
                    ? $"{group.Name}          {totalActive} active ({criticalCount}c/{preferredCount}p)"
                    : group.Name;

                var headerRect = listing.GetRect(GroupHeaderHeight);
                UIHelpers.DrawSectionHeaderWithBadge(headerRect, headerText, totalActive, !isCollapsed);

                // Detect clicks for collapse/expand
                if (Widgets.ButtonInvisible(headerRect))
                {
                    if (isCollapsed)
                        _collapsedGroups.Remove(group.Id);
                    else
                        _collapsedGroups.Add(group.Id);
                }

                // Draw filters if not collapsed
                if (!isCollapsed)
                {
                    listing.Gap(5f);
                    foreach (var filter in group.Filters)
                    {
                        // Check if filter matches search
                        if (!string.IsNullOrEmpty(_searchText) && !MatchesSearch(filter.Label))
                            continue;

                        // Draw the filter control
                        filter.DrawAction(listing, preferences.Filters);
                        listing.Gap(5f);
                    }
                    listing.Gap(10f);
                }
            }
        }

        private static bool MatchesSearch(string text)
        {
            if (string.IsNullOrEmpty(_searchText))
                return true;

            return text.ToLowerInvariant().Contains(_searchText.ToLowerInvariant());
        }

        private static (int total, int critical, int preferred) CountActiveFilters(FilterGroup group, FilterSettings filters)
        {
            int total = 0;
            int critical = 0;
            int preferred = 0;

            foreach (var filter in group.Filters)
            {
                var (isActive, importance) = filter.IsActiveFunc(filters);
                if (isActive)
                {
                    total++;
                    if (importance == FilterImportance.Critical)
                        critical++;
                    else if (importance == FilterImportance.Preferred)
                        preferred++;
                }
            }

            return (total, critical, preferred);
        }

        private static List<FilterGroup> GetFilterGroups(FilterOrganization organization)
        {
            return organization == FilterOrganization.UserIntent
                ? GetUserIntentGroups()
                : GetDataTypeGroups();
        }

        // NOTE: GetUserIntentGroups() and GetDataTypeGroups() are implemented in AdvancedModeUI_Controls.cs

        // Data structures
        private enum FilterOrganization
        {
            UserIntent,
            DataType
        }

        private class FilterGroup
        {
            public string Id { get; }
            public string Name { get; }
            public List<FilterControl> Filters { get; }

            public FilterGroup(string id, string name, List<FilterControl> filters)
            {
                Id = id;
                Name = name;
                Filters = filters;
            }
        }

        private class FilterControl
        {
            public string Label { get; }
            public System.Action<Listing_Standard, FilterSettings> DrawAction { get; }
            public System.Func<FilterSettings, (bool isActive, FilterImportance importance)> IsActiveFunc { get; }

            public FilterControl(
                string label,
                System.Action<Listing_Standard, FilterSettings> drawAction,
                System.Func<FilterSettings, (bool isActive, FilterImportance importance)> isActiveFunc)
            {
                Label = label;
                DrawAction = drawAction;
                IsActiveFunc = isActiveFunc;
            }
        }
    }
}

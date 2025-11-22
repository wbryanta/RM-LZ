using System;
using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld;
using UnityEngine;
using Verse;

namespace LandingZone.Core.UI
{
    /// <summary>
    /// Full-featured UI renderer for power users.
    /// Shows all 40+ filters organized by user intent groups, with tabbed layout.
    /// </summary>
    public static partial class AdvancedModeUI
    {
        private const float SearchBoxHeight = 30f;
        private const float TabHeight = 32f;
        private const float GroupHeaderHeight = 32f;
        private const float FilterItemHeight = 30f;
        private const float RightPanelWidth = 280f;
        private const float ColumnGap = 12f;

        // UI State
        private static string _searchText = "";
        private static AdvancedTab _selectedTab = AdvancedTab.Climate;
        private static HashSet<string> _collapsedGroups = new HashSet<string>();
        private static Vector2 _scrollPosition = Vector2.zero;
        private static Vector2 _rightPanelScrollPosition = Vector2.zero;
        private static Vector2 _mapFeaturesScrollPosition = Vector2.zero;
        private static List<FilterConflict> _activeConflicts = new List<FilterConflict>();

        /// <summary>
        /// Tab categories for Advanced mode (Tier 3).
        /// </summary>
        private enum AdvancedTab
        {
            Climate,
            Geography,
            Resources,
            Features,
            Results
        }

        /// <summary>
        /// Gets the currently detected conflicts for use by filter controls.
        /// </summary>
        internal static List<FilterConflict> GetActiveConflicts() => _activeConflicts;

        /// <summary>
        /// Renders the Advanced mode UI (tabs + search + grouped filters + live preview panel).
        /// </summary>
        /// <param name="inRect">Available drawing area</param>
        /// <param name="preferences">User preferences containing filter settings</param>
        /// <returns>Total height consumed by rendering</returns>
        public static float DrawContent(Rect inRect, UserPreferences preferences)
        {
            // Run conflict detection on current filter settings
            _activeConflicts = ConflictDetector.DetectConflicts(preferences.GetActiveFilters());

            // Two-column layout: left = filters, right = live preview
            float leftColumnWidth = inRect.width - RightPanelWidth - ColumnGap;
            Rect leftColumn = new Rect(inRect.x, inRect.y, leftColumnWidth, inRect.height);
            Rect rightColumn = new Rect(inRect.x + leftColumnWidth + ColumnGap, inRect.y, RightPanelWidth, inRect.height);

            // LEFT COLUMN: Tabs, Search, Filters
            var listing = new Listing_Standard { ColumnWidth = leftColumn.width };
            listing.Begin(leftColumn);

            // Show general conflicts (not filter-specific) at the top
            var generalConflicts = _activeConflicts.Where(c => c.FilterId == "general").ToList();
            if (generalConflicts.Any())
            {
                foreach (var conflict in generalConflicts)
                {
                    UIHelpers.DrawConflictWarning(listing, conflict);
                }
                listing.Gap(8f);
            }

            // Tab navigation (Tier 3)
            var tabRect = listing.GetRect(TabHeight);
            DrawTabs(tabRect, preferences.GetActiveFilters());
            listing.Gap(12f);
            listing.GapLine(); // Visual separator after tabs
            listing.Gap(10f);

            // Search box for filtering visible controls
            var searchRect = listing.GetRect(SearchBoxHeight);
            DrawSearchBox(searchRect);
            listing.Gap(8f);
            listing.GapLine(); // Visual separator after search
            listing.Gap(10f);

            // Grouped filters (collapsible sections) - filtered by selected tab
            var groups = GetFilterGroupsForTab(_selectedTab);
            DrawFilterGroups(listing, groups, preferences);

            listing.End();

            // RIGHT COLUMN: Live Preview Panel (Tier 3)
            DrawLivePreviewPanel(rightColumn, preferences);

            return listing.CurHeight;
        }

        private static void DrawSearchBox(Rect rect)
        {
            _searchText = UIHelpers.DrawSearchBox(new Listing_Standard { ColumnWidth = rect.width }, _searchText, "Search filters...");
        }

        private static void DrawTabs(Rect rect, FilterSettings filters)
        {
            const int tabCount = 5;
            float tabWidth = rect.width / tabCount;

            // Count active filters per tab for badges
            var tabCounts = GetActiveFilterCountsPerTab(filters);

            for (int i = 0; i < tabCount; i++)
            {
                var tab = (AdvancedTab)i;
                Rect tabRect = new Rect(rect.x + i * tabWidth, rect.y, tabWidth, rect.height);

                // Tab label with active count badge
                string label = GetTabLabel(tab);
                if (tabCounts.TryGetValue(tab, out int count) && count > 0)
                {
                    label += $" ({count})";
                }

                // Selected tab styling
                bool isSelected = tab == _selectedTab;
                Color bgColor = isSelected
                    ? new Color(0.3f, 0.3f, 0.3f)
                    : new Color(0.15f, 0.15f, 0.15f);

                Widgets.DrawBoxSolid(tabRect, bgColor);
                Widgets.DrawBox(tabRect);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = isSelected ? Color.white : new Color(0.7f, 0.7f, 0.7f);
                Widgets.Label(tabRect, label);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;

                if (Widgets.ButtonInvisible(tabRect))
                {
                    _selectedTab = tab;
                    // Clear search when switching tabs for cleaner UX
                    _searchText = "";
                }

                // Tooltip
                string tooltip = GetTabTooltip(tab);
                if (!string.IsNullOrEmpty(tooltip))
                {
                    TooltipHandler.TipRegion(tabRect, tooltip);
                }
            }
        }

        private static string GetTabLabel(AdvancedTab tab)
        {
            return tab switch
            {
                AdvancedTab.Climate => "Climate",
                AdvancedTab.Geography => "Geography",
                AdvancedTab.Resources => "Resources",
                AdvancedTab.Features => "Features",
                AdvancedTab.Results => "Results",
                _ => tab.ToString()
            };
        }

        private static string GetTabTooltip(AdvancedTab tab)
        {
            return tab switch
            {
                AdvancedTab.Climate => "Temperature, rainfall, growing days, pollution",
                AdvancedTab.Geography => "Hilliness, coastal access, movement difficulty, swampiness",
                AdvancedTab.Resources => "Stones, forageability, plant/animal density, fish, grazing",
                AdvancedTab.Features => "Map features (mutators), rivers, roads, biomes",
                AdvancedTab.Results => "Result count, strictness, fallback tiers",
                _ => ""
            };
        }

        private static Dictionary<AdvancedTab, int> GetActiveFilterCountsPerTab(FilterSettings filters)
        {
            var counts = new Dictionary<AdvancedTab, int>();

            // Get all groups and count active filters per tab
            var allGroups = GetUserIntentGroups();
            foreach (var group in allGroups)
            {
                var tab = MapGroupToTab(group.Id);
                if (!counts.ContainsKey(tab))
                    counts[tab] = 0;

                var (total, _, _) = CountActiveFilters(group, filters);
                counts[tab] += total;
            }

            return counts;
        }

        private static AdvancedTab MapGroupToTab(string groupId)
        {
            return groupId switch
            {
                "climate_comfort" => AdvancedTab.Climate,
                "terrain_access" => AdvancedTab.Geography,
                "resources_production" => AdvancedTab.Resources,
                "special_features" => AdvancedTab.Features,
                "biome_control" => AdvancedTab.Features,
                "results_control" => AdvancedTab.Results,
                _ => AdvancedTab.Climate
            };
        }

        private static List<FilterGroup> GetFilterGroupsForTab(AdvancedTab tab)
        {
            var allGroups = GetUserIntentGroups();
            return allGroups.Where(g => MapGroupToTab(g.Id) == tab).ToList();
        }

        private static void DrawLivePreviewPanel(Rect rect, UserPreferences preferences)
        {
            var filters = preferences.GetActiveFilters();

            // Panel background
            Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.1f));
            Widgets.DrawBox(rect);

            var contentRect = rect.ContractedBy(8f);

            // Create scrollable view for panel content
            var viewRect = new Rect(0f, 0f, contentRect.width - 16f, 600f); // Subtract scrollbar width

            Widgets.BeginScrollView(contentRect, ref _rightPanelScrollPosition, viewRect);

            var listing = new Listing_Standard { ColumnWidth = viewRect.width };
            listing.Begin(viewRect);

            // Header
            Text.Font = GameFont.Medium;
            listing.Label("LIVE COVERAGE PREVIEW");
            Text.Font = GameFont.Small;
            listing.GapLine();
            listing.Gap(8f);

            // Build filter lists first (needed for both display and selectivity checks)
            var allGroups = GetUserIntentGroups();
            var criticalFilters = new List<string>();
            var preferredFilters = new List<string>();

            foreach (var group in allGroups)
            {
                foreach (var filter in group.Filters)
                {
                    var (isActive, importance) = filter.IsActiveFunc(filters);
                    if (isActive)
                    {
                        if (importance == FilterImportance.Critical)
                            criticalFilters.Add(filter.Label);
                        else if (importance == FilterImportance.Preferred)
                            preferredFilters.Add(filter.Label);
                    }
                }
            }

            // Live tile count estimates (try selectivity analysis if available)
            var selectivities = LandingZoneContext.Filters?.GetAllSelectivities(LandingZoneContext.State);
            if (selectivities != null && selectivities.Any())
            {
                int totalSettleable = selectivities.FirstOrDefault().TotalTiles;
                var criticalSelectivities = selectivities.Where(s => s.Importance == FilterImportance.Critical).ToList();

                // Show baseline
                Text.Font = GameFont.Small;
                GUI.color = new Color(0.8f, 0.8f, 0.8f);
                listing.Label("Baseline (all settleable tiles):");
                Text.Font = GameFont.Medium;
                GUI.color = Color.white;
                listing.Label($"{totalSettleable:N0} tiles");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                listing.Gap(8f);

                // Show estimate only if we have selectivity data for critical filters
                if (criticalSelectivities.Any())
                {
                    // Estimate combined selectivity (product of individual ratios)
                    float combinedRatio = 1.0f;
                    foreach (var s in criticalSelectivities)
                    {
                        combinedRatio *= s.Ratio;
                    }

                    int estimatedMatches = (int)(combinedRatio * totalSettleable);
                    float percentage = combinedRatio * 100f;

                    // Show estimated matches after applying critical filters
                    Text.Font = GameFont.Small;
                    listing.Label("After applying filters:");
                    Text.Font = GameFont.Medium;

                    // Color code based on how restrictive
                    if (estimatedMatches < 100)
                        GUI.color = new Color(1f, 0.4f, 0.4f); // Red
                    else if (estimatedMatches < 1000)
                        GUI.color = new Color(1f, 0.8f, 0.3f); // Yellow
                    else
                        GUI.color = new Color(0.4f, 1f, 0.4f); // Green

                    listing.Label($"~{estimatedMatches:N0} tiles ({percentage:F1}%)");
                    GUI.color = Color.white;

                    // Warning for very restrictive filters
                    if (estimatedMatches < 50)
                    {
                        Text.Font = GameFont.Tiny;
                        GUI.color = new Color(1f, 0.5f, 0.5f);
                        listing.Label("⚠ Very restrictive! May return 0 results.");
                        GUI.color = Color.white;
                    }
                    else if (estimatedMatches < 100)
                    {
                        Text.Font = GameFont.Tiny;
                        GUI.color = new Color(1f, 0.8f, 0.3f);
                        listing.Label("⚠ Highly restrictive - results may be limited.");
                        GUI.color = Color.white;
                    }

                    Text.Font = GameFont.Small;
                    listing.Gap(12f);
                }
                else if (criticalFilters.Any())
                {
                    // We have critical filters but no selectivity data for them yet
                    Text.Font = GameFont.Tiny;
                    GUI.color = new Color(0.7f, 0.7f, 0.7f);
                    listing.Label("(Tile count estimate unavailable for these filter types)");
                    GUI.color = Color.white;
                    Text.Font = GameFont.Small;
                    listing.Gap(12f);
                }
            }

            // Critical Filters
            if (criticalFilters.Any())
            {
                listing.GapLine(); // Visual separator before filter lists
                listing.Gap(8f);

                Text.Font = GameFont.Small;
                GUI.color = new Color(1f, 0.7f, 0.7f);
                listing.Label($"Critical Filters ({criticalFilters.Count}):");
                GUI.color = Color.white;
                Text.Font = GameFont.Tiny;
                foreach (var f in criticalFilters.Take(10)) // Limit to first 10
                {
                    listing.Label($"  ✓ {f}");
                }
                if (criticalFilters.Count > 10)
                    listing.Label($"  ... and {criticalFilters.Count - 10} more");
                Text.Font = GameFont.Small;
                listing.Gap(12f);
            }

            if (preferredFilters.Any())
            {
                if (!criticalFilters.Any())
                {
                    listing.GapLine(); // Visual separator if no critical filters above
                    listing.Gap(8f);
                }

                Text.Font = GameFont.Small;
                GUI.color = new Color(0.7f, 0.7f, 1f);
                listing.Label($"Preferred Filters ({preferredFilters.Count}):");
                GUI.color = Color.white;
                Text.Font = GameFont.Tiny;
                foreach (var f in preferredFilters.Take(10)) // Limit to first 10
                {
                    listing.Label($"  • {f}");
                }
                if (preferredFilters.Count > 10)
                    listing.Label($"  ... and {preferredFilters.Count - 10} more");
                Text.Font = GameFont.Small;
                listing.Gap(12f);
            }

            // Fallback Tier Preview (Tier 3)
            if (criticalFilters.Any())
            {
                DrawFallbackTierPreview(listing, filters);
            }

            // Warnings
            if (_activeConflicts.Any())
            {
                listing.GapLine();
                listing.Gap(8f);
                GUI.color = new Color(1f, 0.7f, 0.3f);
                Text.Font = GameFont.Small;
                listing.Label($"⚠ Warnings ({_activeConflicts.Count}):");
                GUI.color = Color.white;
                Text.Font = GameFont.Tiny;
                foreach (var conflict in _activeConflicts.Take(5))
                {
                    listing.Label($"  • {conflict.Message}");
                }
                if (_activeConflicts.Count > 5)
                    listing.Label($"  ... and {_activeConflicts.Count - 5} more");
                Text.Font = GameFont.Small;
                listing.Gap(8f);
            }

            // Search Now button
            listing.GapLine();
            listing.Gap(12f);
            if (listing.ButtonText("Search Now"))
            {
                LandingZoneContext.RequestEvaluation(EvaluationRequestSource.Manual, focusOnComplete: true);
            }

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            listing.Label("Tip: Adjust filters in left panel to refine your search");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            listing.End();
            Widgets.EndScrollView();
        }

        /// <summary>
        /// Draws a compact fallback tier preview in the Live Preview sidebar.
        /// Shows current strictness and top 1-2 alternative tiers with click-to-apply.
        /// </summary>
        private static void DrawFallbackTierPreview(Listing_Standard listing, FilterSettings filters)
        {
            // Skip if context not ready
            if (LandingZoneContext.Filters == null || LandingZoneContext.State == null)
                return;

            try
            {
                // Get all critical filter selectivities
                var allSelectivities = LandingZoneContext.Filters.GetAllSelectivities(LandingZoneContext.State);
                var criticalSelectivities = allSelectivities
                    .Where(s => s.Importance == FilterImportance.Critical)
                    .ToList();

                // No criticals? Skip
                if (criticalSelectivities.Count == 0)
                    return;

                // Get current strictness estimate
                var currentLikelihood = filters.CriticalStrictness >= 1.0f
                    ? Filtering.MatchLikelihoodEstimator.EstimateAllCriticals(criticalSelectivities)
                    : Filtering.MatchLikelihoodEstimator.EstimateRelaxedCriticals(criticalSelectivities, filters.CriticalStrictness);

                // Only show if current strictness is low/medium (when fallback tiers are most useful)
                if (currentLikelihood.Category == Filtering.LikelihoodCategory.High ||
                    currentLikelihood.Category == Filtering.LikelihoodCategory.VeryHigh ||
                    currentLikelihood.Category == Filtering.LikelihoodCategory.Guaranteed)
                {
                    return; // Don't clutter the sidebar if filters are already reasonable
                }

                // Visual separator
                listing.GapLine();
                listing.Gap(8f);

                // Header
                Text.Font = GameFont.Small;
                GUI.color = new Color(1f, 0.8f, 0.4f);
                listing.Label("Fallback Tiers");
                GUI.color = Color.white;
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                listing.Label("Consider relaxing strictness:");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                listing.Gap(6f);

                // Current strictness (compact display)
                var currentRect = listing.GetRect(30f);
                Color currentBgColor = currentLikelihood.Category switch
                {
                    Filtering.LikelihoodCategory.Medium => new Color(0.25f, 0.25f, 0.15f),
                    Filtering.LikelihoodCategory.Low => new Color(0.3f, 0.2f, 0.15f),
                    _ => new Color(0.3f, 0.15f, 0.15f)
                };

                Widgets.DrawBoxSolid(currentRect, currentBgColor);
                Widgets.DrawBox(currentRect);

                var contentRect = currentRect.ContractedBy(3f);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.8f, 0.8f, 0.8f);
                Widgets.Label(
                    new Rect(contentRect.x, contentRect.y + 2f, contentRect.width, 24f),
                    $"Current: {currentLikelihood.GetUserMessage()}"
                );
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                listing.Gap(4f);

                // Get fallback suggestions
                var suggestions = Filtering.MatchLikelihoodEstimator.SuggestStrictness(criticalSelectivities);

                // Only show top 2 suggestions different from current
                var relevantSuggestions = suggestions
                    .Where(s => Math.Abs(s.Strictness - filters.CriticalStrictness) > 0.01f)
                    .Where(s => s.Category > currentLikelihood.Category) // Only show improvements
                    .Take(2)
                    .ToList();

                if (relevantSuggestions.Any())
                {
                    foreach (var suggestion in relevantSuggestions)
                    {
                        var suggestionRect = listing.GetRect(28f);
                        Color bgColor = suggestion.Category switch
                        {
                            Filtering.LikelihoodCategory.Guaranteed => new Color(0.15f, 0.25f, 0.15f),
                            Filtering.LikelihoodCategory.VeryHigh => new Color(0.15f, 0.25f, 0.15f),
                            Filtering.LikelihoodCategory.High => new Color(0.15f, 0.2f, 0.15f),
                            Filtering.LikelihoodCategory.Medium => new Color(0.2f, 0.2f, 0.1f),
                            _ => new Color(0.25f, 0.15f, 0.1f)
                        };

                        Widgets.DrawBoxSolid(suggestionRect, bgColor);
                        Widgets.DrawBox(suggestionRect);

                        var suggestionContent = suggestionRect.ContractedBy(3f);
                        Text.Font = GameFont.Tiny;
                        Widgets.Label(
                            new Rect(suggestionContent.x, suggestionContent.y + 2f, suggestionContent.width, 24f),
                            $"→ {suggestion.GetDisplayText()} ({suggestion.Strictness:P0})"
                        );
                        Text.Font = GameFont.Small;

                        if (Widgets.ButtonInvisible(suggestionRect))
                        {
                            filters.CriticalStrictness = suggestion.Strictness;
                            Messages.Message(
                                $"Applied fallback tier: {suggestion.Description} (strictness {suggestion.Strictness:P0})",
                                MessageTypeDefOf.NeutralEvent,
                                false
                            );
                        }

                        TooltipHandler.TipRegion(suggestionRect, $"{suggestion.Description}\nClick to set strictness to {suggestion.Strictness:P0}");
                        listing.Gap(3f);
                    }
                }

                listing.Gap(8f);
            }
            catch (System.Exception ex)
            {
                // Silently fail - don't break UI
                Log.Warning($"[LandingZone] Failed to draw fallback tier preview: {ex.Message}");
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
                var (totalActive, criticalCount, preferredCount) = CountActiveFilters(group, preferences.GetActiveFilters());

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
                        filter.DrawAction(listing, preferences.GetActiveFilters());
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

        private static List<FilterGroup> GetFilterGroups()
        {
            // Use UserIntent organization (Climate, Terrain, Resources, Special Features, Biome Control)
            return GetUserIntentGroups();
        }

        // NOTE: GetUserIntentGroups() is implemented in AdvancedModeUI_Controls.cs

        // Data structures

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

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
        private static float _cachedLivePreviewContentHeight = 1200f; // Cache measured content height
        private static float _cachedFilterContentHeight = 3000f; // Cache measured filter content height (starts large)
        private static Vector2 _mapFeaturesScrollPosition = Vector2.zero;
        private static List<FilterConflict> _activeConflicts = new List<FilterConflict>();

        // Workspace mode toggle
        // Default to Bucket view (Workspace mode) - the primary Advanced UI
        private static bool _useWorkspaceMode = true;

        /// <summary>
        /// Gets whether workspace (bucket) mode is active.
        /// Used by parent window to determine scroll behavior.
        /// </summary>
        public static bool IsWorkspaceMode => _useWorkspaceMode;

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

        // Shared selectivity estimator for lightweight, always-available estimates
        private static readonly Filtering.FilterSelectivityEstimator _selectivityEstimator = new Filtering.FilterSelectivityEstimator();

        /// <summary>
        /// Computes lightweight selectivity estimates for active critical filters.
        /// Uses FilterSelectivityEstimator for cheap, always-available estimates without requiring a prior search.
        /// Returns FilterSelectivity structs for compatibility with MatchLikelihoodEstimator.
        /// </summary>
        private static List<Filtering.FilterSelectivity> ComputeLightweightSelectivities(FilterSettings filters)
        {
            var estimates = new List<Filtering.FilterSelectivity>();

            // Ensure estimator is initialized
            _selectivityEstimator.Initialize();

            // Temperature (Average)
            if (filters.AverageTemperatureImportance == FilterImportance.Critical)
            {
                var est = _selectivityEstimator.EstimateTemperatureRange(filters.AverageTemperatureRange, FilterImportance.Critical);
                estimates.Add(new Filtering.FilterSelectivity("avg_temp", FilterImportance.Critical, est.MatchCount, est.TotalTiles, false));
            }

            // Temperature (Minimum)
            if (filters.MinimumTemperatureImportance == FilterImportance.Critical)
            {
                var est = _selectivityEstimator.EstimateTemperatureRange(filters.MinimumTemperatureRange, FilterImportance.Critical);
                estimates.Add(new Filtering.FilterSelectivity("min_temp", FilterImportance.Critical, est.MatchCount, est.TotalTiles, false));
            }

            // Temperature (Maximum)
            if (filters.MaximumTemperatureImportance == FilterImportance.Critical)
            {
                var est = _selectivityEstimator.EstimateTemperatureRange(filters.MaximumTemperatureRange, FilterImportance.Critical);
                estimates.Add(new Filtering.FilterSelectivity("max_temp", FilterImportance.Critical, est.MatchCount, est.TotalTiles, false));
            }

            // Rainfall
            if (filters.RainfallImportance == FilterImportance.Critical)
            {
                var est = _selectivityEstimator.EstimateRainfallRange(filters.RainfallRange, FilterImportance.Critical);
                estimates.Add(new Filtering.FilterSelectivity("rainfall", FilterImportance.Critical, est.MatchCount, est.TotalTiles, false));
            }

            // Growing Days
            if (filters.GrowingDaysImportance == FilterImportance.Critical)
            {
                var est = _selectivityEstimator.EstimateGrowingDaysRange(filters.GrowingDaysRange, FilterImportance.Critical);
                estimates.Add(new Filtering.FilterSelectivity("growing_days", FilterImportance.Critical, est.MatchCount, est.TotalTiles, false));
            }

            // Hilliness - included when filtering (count < 4), implicitly Critical
            if (filters.AllowedHilliness.Count < 4)
            {
                var est = _selectivityEstimator.EstimateHilliness(filters.AllowedHilliness);
                estimates.Add(new Filtering.FilterSelectivity("hilliness", FilterImportance.Critical, est.MatchCount, est.TotalTiles, false));
            }

            // Coastal
            if (filters.CoastalImportance == FilterImportance.Critical)
            {
                var est = _selectivityEstimator.EstimateCoastal(FilterImportance.Critical);
                estimates.Add(new Filtering.FilterSelectivity("coastal", FilterImportance.Critical, est.MatchCount, est.TotalTiles, false));
            }

            // Water Access
            if (filters.WaterAccessImportance == FilterImportance.Critical)
            {
                var est = _selectivityEstimator.EstimateWaterAccess(FilterImportance.Critical);
                estimates.Add(new Filtering.FilterSelectivity("water_access", FilterImportance.Critical, est.MatchCount, est.TotalTiles, false));
            }

            // Pollution
            if (filters.PollutionImportance == FilterImportance.Critical)
            {
                var est = _selectivityEstimator.EstimatePollutionRange(filters.PollutionRange, FilterImportance.Critical);
                estimates.Add(new Filtering.FilterSelectivity("pollution", FilterImportance.Critical, est.MatchCount, est.TotalTiles, false));
            }

            // Forageability
            if (filters.ForageImportance == FilterImportance.Critical)
            {
                var est = _selectivityEstimator.EstimateForageabilityRange(filters.ForageabilityRange, FilterImportance.Critical);
                estimates.Add(new Filtering.FilterSelectivity("forageability", FilterImportance.Critical, est.MatchCount, est.TotalTiles, false));
            }

            // Swampiness
            if (filters.SwampinessImportance == FilterImportance.Critical)
            {
                var est = _selectivityEstimator.EstimateSwampinessRange(filters.SwampinessRange, FilterImportance.Critical);
                estimates.Add(new Filtering.FilterSelectivity("swampiness", FilterImportance.Critical, est.MatchCount, est.TotalTiles, false));
            }

            // Animal Density
            if (filters.AnimalDensityImportance == FilterImportance.Critical)
            {
                var est = _selectivityEstimator.EstimateAnimalDensityRange(filters.AnimalDensityRange, FilterImportance.Critical);
                estimates.Add(new Filtering.FilterSelectivity("animal_density", FilterImportance.Critical, est.MatchCount, est.TotalTiles, false));
            }

            // Fish Population
            if (filters.FishPopulationImportance == FilterImportance.Critical)
            {
                var est = _selectivityEstimator.EstimateFishPopulationRange(filters.FishPopulationRange, FilterImportance.Critical);
                estimates.Add(new Filtering.FilterSelectivity("fish_population", FilterImportance.Critical, est.MatchCount, est.TotalTiles, false));
            }

            // Plant Density
            if (filters.PlantDensityImportance == FilterImportance.Critical)
            {
                var est = _selectivityEstimator.EstimatePlantDensityRange(filters.PlantDensityRange, FilterImportance.Critical);
                estimates.Add(new Filtering.FilterSelectivity("plant_density", FilterImportance.Critical, est.MatchCount, est.TotalTiles, false));
            }

            // Elevation
            if (filters.ElevationImportance == FilterImportance.Critical)
            {
                var est = _selectivityEstimator.EstimateElevationRange(filters.ElevationRange, FilterImportance.Critical);
                estimates.Add(new Filtering.FilterSelectivity("elevation", FilterImportance.Critical, est.MatchCount, est.TotalTiles, false));
            }

            // Movement Difficulty
            if (filters.MovementDifficultyImportance == FilterImportance.Critical)
            {
                var est = _selectivityEstimator.EstimateMovementDifficultyRange(filters.MovementDifficultyRange, FilterImportance.Critical);
                estimates.Add(new Filtering.FilterSelectivity("movement_difficulty", FilterImportance.Critical, est.MatchCount, est.TotalTiles, false));
            }

            // Map Features (aggregate all critical features)
            if (filters.MapFeatures.GetCriticalItems().Any())
            {
                var est = _selectivityEstimator.EstimateMapFeatures(filters.MapFeatures, FilterImportance.Critical);
                estimates.Add(new Filtering.FilterSelectivity("map_features", FilterImportance.Critical, est.MatchCount, est.TotalTiles, false));
            }

            return estimates;
        }

        /// <summary>
        /// Renders the Advanced mode UI.
        /// Supports two modes:
        /// - Workspace Mode (default): 4-bucket drag-and-drop interface with OR grouping
        /// - Classic Mode: Tabbed filter lists with importance selectors
        /// </summary>
        /// <param name="inRect">Available drawing area</param>
        /// <param name="preferences">User preferences containing filter settings</param>
        /// <returns>Total height consumed by rendering</returns>
        public static float DrawContent(Rect inRect, UserPreferences preferences)
        {
            // Run conflict detection on current filter settings
            _activeConflicts = ConflictDetector.DetectConflicts(preferences.GetActiveFilters());

            // Mode toggle at top
            var toggleRect = new Rect(inRect.x, inRect.y, inRect.width, 28f);
            DrawModeToggle(toggleRect);

            // Content area below toggle
            var contentRect = new Rect(inRect.x, inRect.y + 32f, inRect.width, inRect.height - 32f);

            if (_useWorkspaceMode)
            {
                // New bucket workspace mode
                DrawBucketWorkspace(contentRect, preferences);
                return contentRect.height + 32f;
            }
            else
            {
                // Classic tabbed mode
                return DrawClassicContent(contentRect, preferences) + 32f;
            }
        }

        /// <summary>
        /// Counts active filters that are not visible/editable in Workspace mode.
        /// Used to warn users before switching to limited-coverage workspace.
        /// </summary>
        private static (int count, List<string> names) CountHiddenActiveFilters(FilterSettings filters)
        {
            var hiddenNames = new List<string>();

            // Container filters (rivers, roads, stones, map features, adjacent biomes, stockpiles)
            if (filters.Rivers.HasAnyImportance)
                hiddenNames.Add("Rivers");
            if (filters.Roads.HasAnyImportance)
                hiddenNames.Add("Roads");
            if (filters.Stones.HasAnyImportance)
                hiddenNames.Add("Stones");
            if (filters.MapFeatures.HasAnyImportance)
                hiddenNames.Add("Map Features");
            if (filters.AdjacentBiomes.HasAnyImportance)
                hiddenNames.Add("Adjacent Biomes");
            if (filters.Stockpiles.HasAnyImportance)
                hiddenNames.Add("Stockpiles");

            // Biome lock
            if (filters.LockedBiome != null)
                hiddenNames.Add("Biome Lock");

            // Forageable food (requires food type selection)
            if (filters.ForageableFoodImportance != FilterImportance.Ignored && !string.IsNullOrEmpty(filters.ForageableFoodDefName))
                hiddenNames.Add("Forageable Food");

            // Hilliness (if restricted - not all 4 types allowed)
            if (filters.AllowedHilliness.Count < 4)
                hiddenNames.Add("Hilliness");

            return (hiddenNames.Count, hiddenNames);
        }

        /// <summary>
        /// Switches to workspace mode directly.
        /// Container filters (Rivers, Roads, Stones, etc.) can't be edited in Workspace mode,
        /// but they still function correctly in search. Users can toggle between modes freely.
        /// </summary>
        private static void TrySwitchToWorkspace(FilterSettings filters)
        {
            // Switch directly without warning - users can toggle between modes freely
            // Container filters configured in Classic mode remain active in Workspace mode
            _useWorkspaceMode = true;
            ResetWorkspace();
        }

        /// <summary>
        /// Called after a preset or import is applied.
        /// Previously auto-switched to Classic if containers were present, but this was disruptive.
        /// Container filters work correctly in Workspace mode (they're just not editable there).
        /// Users can switch to Classic mode manually if they need to edit containers.
        /// </summary>
        /// <param name="filters">The filters that were just applied.</param>
        public static void EnsureHiddenFiltersVisible(FilterSettings filters)
        {
            // No longer auto-switches - users can toggle between modes freely
            // Container filters configured elsewhere remain active in Workspace mode
        }

        /// <summary>
        /// Draws the mode toggle at the top of the Advanced UI.
        /// </summary>
        private static void DrawModeToggle(Rect rect)
        {
            // Background
            Widgets.DrawBoxSolid(rect, new Color(0.12f, 0.12f, 0.12f));

            float buttonWidth = 120f;
            float x = rect.x + 8f;

            // Workspace button
            var workspaceRect = new Rect(x, rect.y + 2f, buttonWidth, rect.height - 4f);
            Color workspaceBg = _useWorkspaceMode ? new Color(0.25f, 0.3f, 0.35f) : new Color(0.15f, 0.15f, 0.15f);
            Widgets.DrawBoxSolid(workspaceRect, workspaceBg);
            if (Widgets.ButtonText(workspaceRect, "LandingZone_Workspace_BucketView".Translate(), drawBackground: false))
            {
                // Get current filters to check for hidden active filters
                var filters = LandingZoneContext.State?.Preferences?.GetActiveFilters();
                if (filters != null)
                {
                    TrySwitchToWorkspace(filters);
                }
                else
                {
                    _useWorkspaceMode = true;
                    ResetWorkspace();
                }
            }
            if (_useWorkspaceMode)
            {
                GUI.color = new Color(0.4f, 0.7f, 0.9f);
                Widgets.DrawBox(workspaceRect);
                GUI.color = Color.white;
            }
            TooltipHandler.TipRegion(workspaceRect, "LandingZone_Workspace_BucketViewTooltip".Translate());

            x += buttonWidth + 8f;

            // Classic button
            var classicRect = new Rect(x, rect.y + 2f, buttonWidth, rect.height - 4f);
            Color classicBg = !_useWorkspaceMode ? new Color(0.25f, 0.3f, 0.35f) : new Color(0.15f, 0.15f, 0.15f);
            Widgets.DrawBoxSolid(classicRect, classicBg);
            if (Widgets.ButtonText(classicRect, "LandingZone_Workspace_ClassicView".Translate(), drawBackground: false))
            {
                _useWorkspaceMode = false;
            }
            if (!_useWorkspaceMode)
            {
                GUI.color = new Color(0.4f, 0.7f, 0.9f);
                Widgets.DrawBox(classicRect);
                GUI.color = Color.white;
            }
            TooltipHandler.TipRegion(classicRect, "LandingZone_Workspace_ClassicViewTooltip".Translate());
        }

        /// <summary>
        /// Renders the classic tabbed Advanced mode UI.
        /// </summary>
        private static float DrawClassicContent(Rect inRect, UserPreferences preferences)
        {
            // Two-column layout: left = filters, right = live preview
            float leftColumnWidth = inRect.width - RightPanelWidth - ColumnGap;
            Rect leftColumn = new Rect(inRect.x, inRect.y, leftColumnWidth, inRect.height);
            Rect rightColumn = new Rect(inRect.x + leftColumnWidth + ColumnGap, inRect.y, RightPanelWidth, inRect.height);

            // LEFT COLUMN: Tabs, Search, Filters (with scroll view)
            // Fixed header area for tabs and search
            const float headerHeight = TabHeight + 12f + 2f + 10f + SearchBoxHeight + 8f + 2f + 10f; // tabs + gaps + search + gaps
            var headerRect = new Rect(leftColumn.x, leftColumn.y, leftColumn.width, headerHeight);
            var scrollableRect = new Rect(leftColumn.x, leftColumn.y + headerHeight, leftColumn.width, leftColumn.height - headerHeight);

            // Draw header (tabs + search) - fixed, not scrolled
            var headerListing = new Listing_Standard { ColumnWidth = headerRect.width };
            headerListing.Begin(headerRect);

            // Show general conflicts (not filter-specific) at the top
            var generalConflicts = _activeConflicts.Where(c => c.FilterId == "general").ToList();
            if (generalConflicts.Any())
            {
                foreach (var conflict in generalConflicts)
                {
                    UIHelpers.DrawConflictWarning(headerListing, conflict);
                }
                headerListing.Gap(8f);
            }

            // Tab navigation (Tier 3)
            var tabRect = headerListing.GetRect(TabHeight);
            DrawTabs(tabRect, preferences.GetActiveFilters());
            headerListing.Gap(12f);
            headerListing.GapLine(); // Visual separator after tabs
            headerListing.Gap(10f);

            // Search box for filtering visible controls
            var searchRect = headerListing.GetRect(SearchBoxHeight);
            DrawSearchBox(searchRect);
            headerListing.Gap(8f);
            headerListing.GapLine(); // Visual separator after search
            headerListing.Gap(10f);

            headerListing.End();

            // Scrollable filter content - use cached height from previous frame
            var groups = GetFilterGroupsForTab(_selectedTab);
            float viewHeight = Mathf.Max(_cachedFilterContentHeight, scrollableRect.height);
            var viewRect = new Rect(0f, 0f, scrollableRect.width - 16f, viewHeight);

            Widgets.BeginScrollView(scrollableRect, ref _scrollPosition, viewRect);

            var listing = new Listing_Standard { ColumnWidth = viewRect.width };
            listing.Begin(viewRect);

            // Grouped filters (collapsible sections) - filtered by selected tab
            DrawFilterGroups(listing, groups, preferences);

            // Cache actual content height for next frame (dynamic sizing)
            _cachedFilterContentHeight = listing.CurHeight + 50f;

            listing.End();
            Widgets.EndScrollView();

            // RIGHT COLUMN: Live Preview Panel (Tier 3)
            DrawLivePreviewPanel(rightColumn, preferences);

            return listing.CurHeight;
        }

        private static void DrawSearchBox(Rect rect)
        {
            _searchText = UIHelpers.DrawSearchBox(new Listing_Standard { ColumnWidth = rect.width }, _searchText, "LandingZone_SearchFiltersPlaceholder");
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

                // Tab label with active count badge (except Results tab)
                string label = GetTabLabel(tab);
                if (tab != AdvancedTab.Results && tabCounts.TryGetValue(tab, out int count) && count > 0)
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
                AdvancedTab.Climate => "LandingZone_TabClimate".Translate(),
                AdvancedTab.Geography => "LandingZone_TabGeography".Translate(),
                AdvancedTab.Resources => "LandingZone_TabResources".Translate(),
                AdvancedTab.Features => "LandingZone_TabFeatures".Translate(),
                AdvancedTab.Results => "LandingZone_TabResults".Translate(),
                _ => tab.ToString()
            };
        }

        private static string GetTabTooltip(AdvancedTab tab)
        {
            return tab switch
            {
                AdvancedTab.Climate => "LandingZone_TabClimateTooltip".Translate(),
                AdvancedTab.Geography => "LandingZone_TabGeographyTooltip".Translate(),
                AdvancedTab.Resources => "LandingZone_TabResourcesTooltip".Translate(),
                AdvancedTab.Features => "LandingZone_TabFeaturesTooltip".Translate(),
                AdvancedTab.Results => "LandingZone_TabResultsTooltip".Translate(),
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

            // Full-height scrollable area (no footer - buttons moved to top)
            var scrollableRect = new Rect(contentRect.x, contentRect.y, contentRect.width, contentRect.height);

            // Use cached content height, ensuring it's at least as tall as the scrollable area
            // Content height is measured during drawing and cached for next frame
            float contentHeight = Mathf.Max(_cachedLivePreviewContentHeight, scrollableRect.height);
            var viewRect = new Rect(0f, 0f, scrollableRect.width - 16f, contentHeight);

            Widgets.BeginScrollView(scrollableRect, ref _rightPanelScrollPosition, viewRect);

            var listing = new Listing_Standard { ColumnWidth = viewRect.width };
            listing.Begin(viewRect);

            // Save Preset button
            if (listing.ButtonText("LandingZone_SavePresetFromAdvanced".Translate()))
            {
                Find.WindowStack.Add(new Dialog_SavePreset(filters, preferences.ActivePreset));
            }
            listing.Gap(4f);

            // Reset All button
            if (listing.ButtonText("LandingZone_Advanced_ResetAll".Translate()))
            {
                // Clear all filters to Ignored
                filters.ClearAll();
                Messages.Message("LandingZone_Advanced_ResetAll_Message".Translate(), MessageTypeDefOf.NeutralEvent, false);
            }
            listing.Gap(12f);

            // Header
            Text.Font = GameFont.Medium;
            listing.Label("LandingZone_LiveCoveragePreview".Translate());
            Text.Font = GameFont.Small;
            listing.GapLine();
            listing.Gap(8f);

            // Build filter lists first (needed for both display and selectivity checks)
            var allGroups = GetUserIntentGroups();
            var criticalFilters = new List<string>();
            var preferredFilters = new List<string>();

            // Grouping labels that don't correspond to actual filter IDs (just UI containers for sub-filters)
            var groupingLabels = new HashSet<string>
            {
                "Resource Modifiers",
                "Special Sites",
                "Life & Wildlife Modifiers",
                "Climate & Weather Modifiers"
            };

            foreach (var group in allGroups)
            {
                foreach (var filter in group.Filters)
                {
                    var (isActive, importance) = filter.IsActiveFunc(filters);
                    if (isActive)
                    {
                        // Skip grouping labels - they're UI containers, not actual filters
                        if (groupingLabels.Contains(filter.Label))
                            continue;

                        if (importance == FilterImportance.Critical)
                            criticalFilters.Add(filter.Label);
                        else if (importance == FilterImportance.Preferred)
                            preferredFilters.Add(filter.Label);
                    }
                }
            }

            // Live tile count estimates - always available using lightweight estimator
            _selectivityEstimator.Initialize();
            int totalSettleable = _selectivityEstimator.GetSettleableTiles();

            // Show baseline
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            listing.Label("LandingZone_BaselineTiles".Translate());
            Text.Font = GameFont.Medium;
            GUI.color = Color.white;
            listing.Label($"{totalSettleable:N0} tiles");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            listing.Gap(8f);

            // Compute lightweight selectivities for active critical filters
            var criticalSelectivities = ComputeLightweightSelectivities(filters);

            // Show estimate if we have critical filters
            if (criticalSelectivities.Any())
            {
                // Estimate combined selectivity (product of individual ratios)
                float combinedRatio = 1.0f;
                foreach (var s in criticalSelectivities)
                {
                    combinedRatio *= s.Ratio; // FilterSelectivity has Ratio property
                }

                int estimatedMatches = (int)(combinedRatio * totalSettleable);
                float percentage = combinedRatio * 100f;

                // Show estimated matches after applying critical filters
                Text.Font = GameFont.Small;
                listing.Label("LandingZone_AfterApplyingFilters".Translate());
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
                    listing.Label("LandingZone_VeryRestrictiveWarning".Translate());
                    GUI.color = Color.white;
                }
                else if (estimatedMatches < 100)
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = new Color(1f, 0.8f, 0.3f);
                    listing.Label("LandingZone_HighlyRestrictiveWarning".Translate());
                    GUI.color = Color.white;
                }

                Text.Font = GameFont.Small;
                listing.Gap(12f);
            }

            // Critical Filters
            if (criticalFilters.Any())
            {
                listing.GapLine(); // Visual separator before filter lists
                listing.Gap(8f);

                Text.Font = GameFont.Small;
                GUI.color = new Color(1f, 0.7f, 0.7f);
                listing.Label("LandingZone_CriticalFiltersLabel".Translate(criticalFilters.Count));
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
                listing.Label("LandingZone_PreferredFiltersLabel".Translate(preferredFilters.Count));
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
                DrawFallbackTierPreview(listing, filters, criticalSelectivities);
            }

            // Warnings
            if (_activeConflicts.Any())
            {
                listing.GapLine();
                listing.Gap(8f);
                GUI.color = new Color(1f, 0.7f, 0.3f);
                Text.Font = GameFont.Small;
                listing.Label("LandingZone_WarningsLabel".Translate(_activeConflicts.Count));
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

            listing.End();

            // Cache the measured content height for next frame (add padding for footer visibility)
            _cachedLivePreviewContentHeight = listing.CurHeight + 20f;

            Widgets.EndScrollView();
        }

        /// <summary>
        /// Draws a compact fallback tier preview in the Live Preview sidebar.
        /// Shows current strictness and top 1-2 alternative tiers with click-to-apply.
        /// </summary>
        private static void DrawFallbackTierPreview(Listing_Standard listing, FilterSettings filters, List<Filtering.FilterSelectivity> criticalSelectivities)
        {
            // No criticals? Skip
            if (criticalSelectivities == null || criticalSelectivities.Count == 0)
                return;

            try
            {

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
                listing.Label("LandingZone_FallbackTiers".Translate());
                GUI.color = Color.white;
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                listing.Label("LandingZone_RelaxStrictness".Translate());
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
                    "LandingZone_CurrentStrictness".Translate(currentLikelihood.GetUserMessage())
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
                                "LandingZone_AppliedFallback".Translate(suggestion.Description, suggestion.Strictness.ToString("P0")),
                                MessageTypeDefOf.NeutralEvent,
                                false
                            );
                        }

                        TooltipHandler.TipRegion(suggestionRect, "LandingZone_FallbackTooltip".Translate(suggestion.Description, suggestion.Strictness.ToString("P0")));
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

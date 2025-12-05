#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using LandingZone.Core.Filtering;
using LandingZone.Core.Filtering.Filters;
using LandingZone.Data;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LandingZone.Core.UI
{
    /// <summary>
    /// Bucket workspace renderer for Advanced mode.
    /// Provides a visual drag-and-drop interface for filter configuration with OR grouping.
    /// </summary>
    public static partial class AdvancedModeUI
    {
        // Workspace constants
        private const float BucketMinHeight = 80f;
        private const float ChipHeight = 26f;
        private const float ChipPadding = 4f;
        private const float BucketPadding = 6f;
        private const float LegendHeight = 50f;
        private const float LogicSummaryHeight = 40f;
        private const float WarningBadgeWidth = 24f;
        private const float BucketToolbarHeight = 22f;
        private const float ClauseHeaderHeight = 20f;
        private const float ClauseMinHeight = 40f;
        private const float ClausePadding = 4f;
        private const float WorkspaceToolbarHeight = 28f;
        private const float CategoryHeaderHeight = 22f;

        // Workspace state
        private static BucketWorkspace? _workspace;

        // Drag state
        private static string? _draggedChipId;
        private static int? _draggedOrGroupId;
        private static FilterImportance? _dragSourceBucket;
        private static int? _dragSourceClauseId;
        private static bool _isDragging;
        private static Vector2 _dragStartPos;
        private static Vector2 _dragCurrentPos;

        // Drop target state
        private static FilterImportance? _dropTargetBucket;
        private static int? _dropTargetClauseId;
        private static bool _isValidDropTarget;

        // Selection state - for multi-select and OR grouping
        private static HashSet<string> _selectedChipIds = new();
        private static int? _selectedOrGroupId;
        private static FilterImportance? _selectionBucket;
        private static int? _selectionClauseId;

        // Scroll positions
        private static Vector2 _paletteScrollPosition = Vector2.zero;
        private static Vector2 _bucketScrollPosition = Vector2.zero;
        private static Vector2 _containerPopupScrollPosition = Vector2.zero;
        private static Dictionary<FilterImportance, Vector2> _bucketScrollPositions = new();

        // Hybrid bucket height constants
        private const float BucketMinCollapsedHeight = 48f;  // Minimal height for empty buckets
        private const float BucketMaxExpandedHeight = 240f;  // Max height before scrolling - fits 2 rows of chips
        private const float BucketHeaderHeight = 34f;        // Header + subtitle
        private const float BucketOverhead = 10f;            // Bottom padding

        // Collapsed category state
        private static HashSet<string> _collapsedCategories = new();

        // Collapsed sub-group state (key format: "CategoryKey:SubGroup")
        private static HashSet<string> _collapsedSubGroups = new();

        // Search state
        private static string _paletteSearchQuery = "";
        private static bool _showSearchDropdown = false;
        private static string? _highlightedFilterId = null;
        private static float _highlightTimer = 0f;
        private const float HighlightDuration = 1.5f;  // Seconds to show highlight

        // Performance warnings cache
        private static readonly HashSet<string> HeavyFilterIds = new()
        {
            "growing_days", "graze", "forageable_food"
        };

        // Tile estimate cache
        private static string _cachedTileEstimate = "";
        private static int _lastEstimateFrame = -1;

        // Range editor state - tracks which chip is showing its range editor
        private static string? _rangeEditorChipId;
        private static FilterImportance? _rangeEditorBucket;
        private static Rect _rangeEditorAnchorRect;

        // Container popup state - tracks which chip is showing its container popup
        private static string? _containerPopupChipId;
        private static string? _lastContainerPopupChipId; // Track changes to reset scroll position
        private static FilterImportance? _containerPopupBucket;
        private static Rect _containerPopupAnchorRect;

        // Bucket scroll view state - needed to convert chip rects to window coords for popups
        private static Rect _bucketAreaRect;
        private static Vector2 _lastBucketScrollPosition;

        // Detailed tooltip popup state - tracks which filter is showing its detailed tooltip
        private static string? _detailedTooltipFilterId;
        private static Rect _detailedTooltipAnchorRect;

        // Palette filter lookup cache for quick access by filter ID
        private static Dictionary<string, PaletteFilter>? _paletteFilterLookup;
        private const float RangeEditorHeight = 50f;
        private const float ContainerPopupWidth = 200f;
        private const float ContainerPopupItemHeight = 24f;
        private const float ContainerPopupSectionHeight = 20f;  // Height for mod section headers

        /// <summary>
        /// Forces the workspace to re-synchronize from the current FilterSettings.
        /// Call this after programmatically modifying AdvancedFilters (e.g., Remix, Load Preset).
        /// </summary>
        public static void InvalidateWorkspace()
        {
            _workspace = null; // Will be recreated on next DrawBucketWorkspace call
        }

        /// <summary>
        /// Draws the bucket workspace interface.
        /// </summary>
        public static void DrawBucketWorkspace(Rect inRect, UserPreferences preferences)
        {
            var filters = preferences.GetActiveFilters();

            // Initialize workspace if needed
            if (_workspace == null)
            {
                _workspace = new BucketWorkspace();
                SyncWorkspaceFromSettings(filters);
            }

            // Initialize selectivity estimator (shared with parent partial class)
            _selectivityEstimator.Initialize();

            // Handle global mouse events for drag/drop
            HandleDragEvents(inRect);

            // Layout: Toolbar at top, then Left palette (38%) | Right buckets (60%)
            var toolbarRect = new Rect(inRect.x, inRect.y, inRect.width, WorkspaceToolbarHeight);
            DrawWorkspaceToolbar(toolbarRect, filters, preferences);

            float contentY = inRect.y + WorkspaceToolbarHeight + 4f;
            float contentHeight = inRect.height - WorkspaceToolbarHeight - 4f;

            float paletteWidth = inRect.width * 0.38f;
            float bucketWidth = inRect.width * 0.60f;
            float gap = inRect.width * 0.02f;

            Rect paletteRect = new Rect(inRect.x, contentY, paletteWidth, contentHeight);
            Rect bucketsRect = new Rect(inRect.x + paletteWidth + gap, contentY, bucketWidth, contentHeight);

            // Draw source palette (filter categories)
            DrawSourcePalette(paletteRect, filters);

            // Draw bucket workspace
            DrawBuckets(bucketsRect, filters);

            // Draw drag ghost if dragging
            if (_isDragging && (_draggedChipId != null || _draggedOrGroupId != null))
            {
                DrawDragGhost();
            }

            // Draw popups as overlays (rendered last, on top of everything)
            DrawRangeEditorPopup(filters);
            DrawContainerPopup(filters);
            DrawDetailedTooltipPopup();

            // Sync workspace changes back to settings
            SyncSettingsFromWorkspace(filters);
        }

        /// <summary>
        /// Draws the workspace toolbar with live tile estimate, Clear All, Save to Preset, and Classic View toggle.
        /// </summary>
        private static void DrawWorkspaceToolbar(Rect rect, FilterSettings filters, UserPreferences preferences)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.12f, 0.12f, 0.15f, 0.95f));

            float x = rect.x + 6f;
            float buttonHeight = rect.height - 4f;
            float buttonY = rect.y + 2f;

            // Clear All button
            var clearAllRect = new Rect(x, buttonY, 70f, buttonHeight);
            if (Widgets.ButtonText(clearAllRect, "LandingZone_Workspace_ClearAll".Translate()))
            {
                // Confirm before clearing
                Find.WindowStack.Add(new Dialog_MessageBox(
                    "LandingZone_Workspace_ClearAllConfirm".Translate(),
                    "LandingZone_Confirm".Translate(),
                    () => ClearAllBuckets(filters),
                    "LandingZone_Cancel".Translate(),
                    null,
                    null,
                    false,
                    null,
                    null
                ));
            }
            TooltipHandler.TipRegion(clearAllRect, "LandingZone_Workspace_ClearAllTooltip".Translate());
            x += 74f;

            // Save to Preset button
            var savePresetRect = new Rect(x, buttonY, 100f, buttonHeight);
            if (Widgets.ButtonText(savePresetRect, "LandingZone_Workspace_SavePreset".Translate()))
            {
                ShowSavePresetDialog(filters, preferences);
            }
            TooltipHandler.TipRegion(savePresetRect, "LandingZone_Workspace_SavePresetTooltip".Translate());
            x += 104f;

            // Separator
            var sepRect = new Rect(x, rect.y + 4f, 1f, rect.height - 8f);
            Widgets.DrawBoxSolid(sepRect, new Color(0.4f, 0.4f, 0.4f, 0.5f));
            x += 8f;

            // Live tile estimate
            string tileEstimate = GetLiveTileEstimate(filters);
            var estimateRect = new Rect(x, buttonY, 200f, buttonHeight);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.7f, 0.9f, 0.7f);
            Widgets.Label(estimateRect, tileEstimate);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(estimateRect, "LandingZone_Workspace_TileEstimateTooltip".Translate());
        }

        /// <summary>
        /// Gets a live tile estimate based on current workspace configuration.
        /// </summary>
        private static string GetLiveTileEstimate(FilterSettings filters)
        {
            // Cache the estimate to avoid recalculating every frame
            int currentFrame = Time.frameCount;
            if (currentFrame - _lastEstimateFrame < 30 && !string.IsNullOrEmpty(_cachedTileEstimate))
            {
                return _cachedTileEstimate;
            }
            _lastEstimateFrame = currentFrame;

            if (_workspace == null)
            {
                _cachedTileEstimate = "LandingZone_Workspace_EstimateNA".Translate();
                return _cachedTileEstimate;
            }

            // Count active filters in critical buckets
            var mustHaveChips = _workspace.GetChipsInBucket(FilterImportance.MustHave);
            var mustNotChips = _workspace.GetChipsInBucket(FilterImportance.MustNotHave);

            if (mustHaveChips.Count == 0 && mustNotChips.Count == 0)
            {
                int totalTiles = _selectivityEstimator.GetSettleableTiles();
                _cachedTileEstimate = "LandingZone_Workspace_EstimateAll".Translate(FormatTileCount(totalTiles));
                return _cachedTileEstimate;
            }

            // Estimate based on critical filters (multiply selectivities)
            float combinedSelectivity = 1.0f;
            int totalSettleable = _selectivityEstimator.GetSettleableTiles();

            foreach (var chip in mustHaveChips)
            {
                var estimate = EstimateChipSelectivity(chip.FilterId, filters);
                if (estimate.HasValue)
                {
                    combinedSelectivity *= estimate.Value;
                }
            }

            // Must NOT filters reduce by their inverse
            foreach (var chip in mustNotChips)
            {
                var estimate = EstimateChipSelectivity(chip.FilterId, filters);
                if (estimate.HasValue)
                {
                    combinedSelectivity *= (1.0f - estimate.Value);
                }
            }

            int estimatedMatches = (int)(totalSettleable * combinedSelectivity);
            _cachedTileEstimate = "LandingZone_Workspace_EstimateMatches".Translate(
                FormatTileCount(estimatedMatches),
                FormatTileCount(totalSettleable));
            return _cachedTileEstimate;
        }

        /// <summary>
        /// Estimates selectivity for a specific filter chip.
        /// </summary>
        private static float? EstimateChipSelectivity(string filterId, FilterSettings filters)
        {
            return filterId switch
            {
                "average_temperature" => _selectivityEstimator.EstimateTemperatureRange(filters.AverageTemperatureRange, FilterImportance.Critical).Selectivity,
                "minimum_temperature" => _selectivityEstimator.EstimateTemperatureRange(filters.MinimumTemperatureRange, FilterImportance.Critical).Selectivity,
                "maximum_temperature" => _selectivityEstimator.EstimateTemperatureRange(filters.MaximumTemperatureRange, FilterImportance.Critical).Selectivity,
                "rainfall" => _selectivityEstimator.EstimateRainfallRange(filters.RainfallRange, FilterImportance.Critical).Selectivity,
                "growing_days" => _selectivityEstimator.EstimateGrowingDaysRange(filters.GrowingDaysRange, FilterImportance.Critical).Selectivity,
                "pollution" => _selectivityEstimator.EstimatePollutionRange(filters.PollutionRange, FilterImportance.Critical).Selectivity,
                "coastal" => _selectivityEstimator.EstimateCoastal(FilterImportance.Critical).Selectivity,
                "coastal_lake" => 0.10f, // Approximate
                "water_access" => _selectivityEstimator.EstimateWaterAccess(FilterImportance.Critical).Selectivity,
                "elevation" => _selectivityEstimator.EstimateElevationRange(filters.ElevationRange, FilterImportance.Critical).Selectivity,
                "movement_difficulty" => _selectivityEstimator.EstimateMovementDifficultyRange(filters.MovementDifficultyRange, FilterImportance.Critical).Selectivity,
                "swampiness" => _selectivityEstimator.EstimateSwampinessRange(filters.SwampinessRange, FilterImportance.Critical).Selectivity,
                "forageability" => _selectivityEstimator.EstimateForageabilityRange(filters.ForageabilityRange, FilterImportance.Critical).Selectivity,
                "graze" => 0.50f, // Approximate for grazeable
                "animal_density" => _selectivityEstimator.EstimateAnimalDensityRange(filters.AnimalDensityRange, FilterImportance.Critical).Selectivity,
                "fish_population" => _selectivityEstimator.EstimateFishPopulationRange(filters.FishPopulationRange, FilterImportance.Critical).Selectivity,
                "plant_density" => _selectivityEstimator.EstimatePlantDensityRange(filters.PlantDensityRange, FilterImportance.Critical).Selectivity,
                "landmark" => 0.05f, // Named locations are rare
                "hilliness" => 0.70f, // Depends on selection
                "river" => 0.25f, // Approximate
                "road" => 0.15f, // Approximate
                _ => 0.50f // Default assumption
            };
        }

        private static string FormatTileCount(int count)
        {
            if (count >= 1000000)
                return $"{count / 1000000.0:F1}M";
            if (count >= 1000)
                return $"{count / 1000.0:F0}k";
            return count.ToString();
        }

        /// <summary>
        /// Clears all filters from all buckets.
        /// Uses FilterSettings.ClearAll() to ensure complete reset of all filter state,
        /// including containers (rivers, roads, stones, etc.), ranges, and biome lock.
        /// </summary>
        private static void ClearAllBuckets(FilterSettings filters)
        {
            // Use the comprehensive ClearAll() method from FilterSettings
            // This resets ALL filter state: importances, containers, ranges, biome lock, etc.
            filters.ClearAll();

            // Reset workspace UI state
            _workspace = null;
            _cachedTileEstimate = "";

            Messages.Message("LandingZone_Workspace_Cleared".Translate(), MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>
        /// Shows the save preset dialog.
        /// </summary>
        private static void ShowSavePresetDialog(FilterSettings filters, UserPreferences preferences)
        {
            // Use existing dialog with current active preset for reference
            Find.WindowStack.Add(new Dialog_SavePreset(filters, preferences.ActivePreset));
        }

        /// <summary>
        /// Called when user modifies filters in Advanced mode.
        /// Clears ActivePreset so search button shows "Search (Advanced)" instead of preset name.
        /// </summary>
        private static void OnFiltersModified()
        {
            var preferences = LandingZoneContext.State?.Preferences;
            if (preferences?.ActivePreset != null)
            {
                preferences.ActivePreset = null;
            }
            _cachedTileEstimate = ""; // Invalidate estimate
        }

        /// <summary>
        /// Handles global drag events.
        /// </summary>
        private static void HandleDragEvents(Rect inRect)
        {
            Event evt = Event.current;

            // Track mouse position during drag
            if (_isDragging)
            {
                _dragCurrentPos = evt.mousePosition;
            }

            // Handle mouse up to complete drag
            if (evt.type == EventType.MouseUp && evt.button == 0 && _isDragging)
            {
                CompleteDrag();
                evt.Use();
            }

            // Cancel drag on escape
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape && _isDragging)
            {
                CancelDrag();
                evt.Use();
            }

            // Click outside chips to clear selection
            if (evt.type == EventType.MouseDown && evt.button == 0 && !_isDragging)
            {
                // Selection is cleared in chip click handlers if not multi-select
            }
        }

        /// <summary>
        /// Starts dragging a chip.
        /// </summary>
        private static void StartDragChip(string chipId, FilterImportance sourceBucket, int sourceClauseId, Vector2 mousePos)
        {
            _draggedChipId = chipId;
            _draggedOrGroupId = null;
            _dragSourceBucket = sourceBucket;
            _dragSourceClauseId = sourceClauseId;
            _isDragging = true;
            _dragStartPos = mousePos;
            _dragCurrentPos = mousePos;
        }

        /// <summary>
        /// Starts dragging an OR group.
        /// </summary>
        private static void StartDragOrGroup(int groupId, FilterImportance sourceBucket, int sourceClauseId, Vector2 mousePos)
        {
            _draggedOrGroupId = groupId;
            _draggedChipId = null;
            _dragSourceBucket = sourceBucket;
            _dragSourceClauseId = sourceClauseId;
            _isDragging = true;
            _dragStartPos = mousePos;
            _dragCurrentPos = mousePos;
        }

        /// <summary>
        /// Completes a drag operation.
        /// </summary>
        private static void CompleteDrag()
        {
            if (_workspace == null || !_isDragging) return;

            // Check if we have a valid drop target
            if (_dropTargetBucket.HasValue && _isValidDropTarget)
            {
                var targetBucket = _dropTargetBucket.Value;
                var targetClauseId = _dropTargetClauseId;

                if (_draggedChipId != null && _dragSourceBucket.HasValue)
                {
                    // Determine if we're moving to a different bucket or clause
                    bool differentBucket = _dragSourceBucket.Value != targetBucket;
                    bool differentClause = _dragSourceClauseId != targetClauseId;

                    if (differentBucket)
                    {
                        // Move to different bucket (first clause)
                        _workspace.MoveChip(_draggedChipId, targetBucket);

                        // Sync container importance to FilterSettings if this is a container chip
                        SyncContainerChipToFilterSettings(_draggedChipId, targetBucket);

                        OnFiltersModified(); // User changed filters - clear preset tracking
                    }
                    else if (differentClause && targetClauseId.HasValue)
                    {
                        // Move to different clause within same bucket
                        _workspace.MoveChipToClause(_draggedChipId, targetClauseId.Value);

                        OnFiltersModified(); // User changed filters - clear preset tracking
                    }

                    // Clear selection since we moved the chip
                    _selectedChipIds.Clear();
                    _selectionBucket = null;
                    _selectionClauseId = null;
                }
                else if (_draggedOrGroupId.HasValue && _dragSourceBucket.HasValue)
                {
                    bool differentBucket = _dragSourceBucket.Value != targetBucket;
                    bool differentClause = _dragSourceClauseId != targetClauseId;

                    if (differentBucket)
                    {
                        // Move entire OR group to new bucket
                        var group = _workspace.GetOrGroup(_draggedOrGroupId.Value);
                        if (group != null)
                        {
                            foreach (var chip in group.Chips.ToList())
                            {
                                _workspace.MoveChip(chip.FilterId, targetBucket);

                                // Sync container importance for any container chips in the group
                                SyncContainerChipToFilterSettings(chip.FilterId, targetBucket);
                            }
                            OnFiltersModified(); // User changed filters - clear preset tracking
                        }
                    }
                    else if (differentClause && targetClauseId.HasValue)
                    {
                        // Move entire OR group to different clause
                        var group = _workspace.GetOrGroup(_draggedOrGroupId.Value);
                        if (group != null)
                        {
                            foreach (var chip in group.Chips.ToList())
                            {
                                _workspace.MoveChipToClause(chip.FilterId, targetClauseId.Value);
                            }
                            OnFiltersModified(); // User changed filters - clear preset tracking
                        }
                    }
                }
            }

            CancelDrag();
        }

        /// <summary>
        /// Cancels the current drag operation.
        /// </summary>
        private static void CancelDrag()
        {
            _draggedChipId = null;
            _draggedOrGroupId = null;
            _dragSourceBucket = null;
            _dragSourceClauseId = null;
            _isDragging = false;
            _dropTargetBucket = null;
            _dropTargetClauseId = null;
            _isValidDropTarget = false;
        }

        /// <summary>
        /// Draws a ghost of the dragged item at the cursor.
        /// </summary>
        private static void DrawDragGhost()
        {
            if (_workspace == null) return;

            string label = "";
            Color color = Color.gray;

            if (_draggedChipId != null)
            {
                foreach (var bucket in BucketWorkspace.AllBuckets)
                {
                    var chips = _workspace.GetChipsInBucket(bucket.Importance);
                    var chip = chips.FirstOrDefault(c => c.FilterId == _draggedChipId);
                    if (chip != null)
                    {
                        label = chip.Label;
                        color = bucket.Color;
                        break;
                    }
                }
            }
            else if (_draggedOrGroupId.HasValue)
            {
                var group = _workspace.GetOrGroup(_draggedOrGroupId.Value);
                if (group != null)
                {
                    label = group.GetDisplayLabel();
                    if (_dragSourceBucket.HasValue)
                    {
                        var bucket = BucketWorkspace.AllBuckets.FirstOrDefault(b => b.Importance == _dragSourceBucket.Value);
                        color = bucket?.Color ?? Color.gray;
                    }
                }
            }

            if (string.IsNullOrEmpty(label)) return;

            float width = Text.CalcSize(label).x + 20f;
            var ghostRect = new Rect(_dragCurrentPos.x - width / 2f, _dragCurrentPos.y - ChipHeight / 2f, width, ChipHeight);

            // Semi-transparent background
            GUI.color = new Color(color.r, color.g, color.b, 0.7f);
            Widgets.DrawBoxSolid(ghostRect, new Color(color.r * 0.3f, color.g * 0.3f, color.b * 0.3f, 0.8f));
            Widgets.DrawBox(ghostRect);
            GUI.color = Color.white;

            // Label
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(ghostRect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        /// <summary>
        /// Handles chip selection with multi-select support.
        /// </summary>
        private static void HandleChipSelection(string chipId, FilterImportance bucket, int clauseId, Event evt)
        {
            bool isMultiSelect = evt.control || evt.command;

            if (isMultiSelect)
            {
                // Multi-select: toggle selection, but only within same bucket AND clause
                if (_selectionBucket.HasValue && _selectionBucket.Value != bucket)
                {
                    // Different bucket - clear and start fresh
                    _selectedChipIds.Clear();
                    _selectedOrGroupId = null;
                }
                else if (_selectionClauseId.HasValue && _selectionClauseId.Value != clauseId)
                {
                    // Different clause - clear and start fresh
                    _selectedChipIds.Clear();
                    _selectedOrGroupId = null;
                }

                _selectionBucket = bucket;
                _selectionClauseId = clauseId;
                _selectedOrGroupId = null; // Deselect any OR group

                if (_selectedChipIds.Contains(chipId))
                {
                    _selectedChipIds.Remove(chipId);
                }
                else
                {
                    _selectedChipIds.Add(chipId);
                }
            }
            else
            {
                // Single select
                _selectedChipIds.Clear();
                _selectedOrGroupId = null;
                _selectedChipIds.Add(chipId);
                _selectionBucket = bucket;
                _selectionClauseId = clauseId;
            }
        }

        /// <summary>
        /// Handles OR group selection.
        /// </summary>
        private static void HandleOrGroupSelection(int groupId, FilterImportance bucket, int clauseId)
        {
            _selectedChipIds.Clear();
            _selectedOrGroupId = groupId;
            _selectionBucket = bucket;
            _selectionClauseId = clauseId;
        }

        /// <summary>
        /// Creates an OR group from the currently selected chips.
        /// </summary>
        private static void GroupSelectedAsOr()
        {
            if (_workspace == null || _selectedChipIds.Count < 2 || !_selectionBucket.HasValue) return;

            // Verify all selected chips are in the same bucket and not already in groups
            var bucket = _selectionBucket.Value;
            var validIds = new List<string>();

            foreach (var chipId in _selectedChipIds)
            {
                var importance = _workspace.GetChipImportance(chipId);
                if (importance == bucket)
                {
                    // Check if already in an OR group
                    var chips = _workspace.GetChipsInBucket(bucket);
                    var chip = chips.FirstOrDefault(c => c.FilterId == chipId);
                    if (chip != null && !chip.OrGroupId.HasValue)
                    {
                        validIds.Add(chipId);
                    }
                }
            }

            if (validIds.Count >= 2)
            {
                _workspace.CreateOrGroup(validIds);
                _selectedChipIds.Clear();
                _selectionBucket = null;
            }
        }

        /// <summary>
        /// Ungroups the selected OR group.
        /// </summary>
        private static void UngroupSelected()
        {
            if (_workspace == null || !_selectedOrGroupId.HasValue) return;

            var group = _workspace.GetOrGroup(_selectedOrGroupId.Value);
            if (group != null)
            {
                // Remove all chips from the group (this will dissolve it)
                foreach (var chip in group.Chips.ToList())
                {
                    _workspace.RemoveFromOrGroup(chip.FilterId);
                }
            }

            _selectedOrGroupId = null;
            _selectionBucket = null;
        }

        /// <summary>
        /// Calculates the actual content height needed for the palette based on expanded/collapsed categories.
        /// Must exactly mirror the drawing logic in DrawPaletteCategory and DrawSubGroupSeparator.
        /// Uses class-level constants to ensure consistency with drawing code.
        /// </summary>
        private static float CalculatePaletteContentHeight()
        {
            // Use class constants - these MUST match the drawing code!
            // CategoryHeaderHeight = 22f (class constant)
            // ChipHeight = 26f (class constant)
            // SubGroupSeparatorHeight = 18f (in DrawSubGroupSeparator)
            const float SubGroupSeparatorHeight = 18f;
            const float PostHeaderGap = 2f;       // listing.Gap(2f) after header
            const float CollapsedCategoryGap = 4f; // listing.Gap(4f) when collapsed
            const float PostFilterGap = 2f;       // listing.Gap(2f) after each filter
            const float CategoryEndGap = 6f;      // listing.Gap(6f) at end of expanded category

            float totalHeight = 0f;

            // Filter each category by runtime availability (hide mutators from unloaded mods)
            var categories = new List<(string key, List<PaletteFilter> filters)>
            {
                ("Climate", FilterByRuntime(GetClimatePaletteFilters())),
                ("Geography_Natural", FilterByRuntime(GetGeographyNaturalPaletteFilters())),
                ("Geography_Resources", FilterByRuntime(GetGeographyResourcesPaletteFilters())),
                ("Geography_Artificial", FilterByRuntime(GetGeographyArtificialPaletteFilters())),
            };

            // Add Mod_Filters only if there are mod filters to show
            var modFilters = GetModFiltersPaletteFilters(); // Already runtime-based
            if (modFilters.Count > 0)
            {
                categories.Add(("Mod_Filters", modFilters));
            }

            foreach (var (key, filters) in categories)
            {
                // Category header (uses class constant CategoryHeaderHeight = 22f)
                totalHeight += CategoryHeaderHeight;
                totalHeight += PostHeaderGap;

                if (_collapsedCategories.Contains(key))
                {
                    // Collapsed category: just the gap
                    totalHeight += CollapsedCategoryGap;
                }
                else
                {
                    // Expanded category: track sub-groups properly
                    string? lastSubGroup = null;
                    foreach (var filter in filters)
                    {
                        // Check if sub-group changed
                        bool subGroupChanged = filter.SubGroup != lastSubGroup && filter.SubGroup != null;
                        bool exitingSubGroup = filter.SubGroup == null && lastSubGroup != null;

                        if (subGroupChanged)
                        {
                            // Add sub-group separator
                            totalHeight += SubGroupSeparatorHeight;
                            lastSubGroup = filter.SubGroup;
                        }
                        else if (exitingSubGroup)
                        {
                            lastSubGroup = null;
                        }

                        // Check if filter is visible (sub-group not collapsed)
                        if (filter.SubGroup != null)
                        {
                            string collapseKey = $"{key}:{filter.SubGroup}";
                            if (_collapsedSubGroups.Contains(collapseKey))
                                continue; // Skip - sub-group is collapsed
                        }

                        // Filter row + gap after (ChipHeight + PostFilterGap)
                        totalHeight += ChipHeight + PostFilterGap;
                    }

                    // Gap at end of expanded category
                    totalHeight += CategoryEndGap;
                }
            }

            return totalHeight + 40f; // Buffer for padding at bottom
        }

        /// <summary>
        /// Draws the source palette with available filters organized by category.
        /// </summary>
        private static void DrawSourcePalette(Rect rect, FilterSettings filters)
        {
            // Background
            Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.1f, 0.8f));
            Widgets.DrawBox(rect);

            var contentRect = rect.ContractedBy(8f);

            // Header
            var headerRect = new Rect(contentRect.x, contentRect.y, contentRect.width, 28f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(headerRect, "LandingZone_Workspace_PaletteHeader".Translate());
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // Hint text
            var hintRect = new Rect(contentRect.x, contentRect.y + 30f, contentRect.width, 20f);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(hintRect, "LandingZone_Workspace_PaletteHint".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // Search bar
            float searchY = contentRect.y + 52f;
            var searchRect = new Rect(contentRect.x, searchY, contentRect.width, 24f);
            DrawPaletteSearchBar(searchRect);

            // Scrollable filter list (starts after search bar)
            float scrollStartY = searchY + 28f;
            var scrollRect = new Rect(contentRect.x, scrollStartY, contentRect.width, contentRect.height - (scrollStartY - contentRect.y));
            float contentHeight = CalculatePaletteContentHeight();
            var viewRect = new Rect(0f, 0f, scrollRect.width - 16f, contentHeight);

            Widgets.BeginScrollView(scrollRect, ref _paletteScrollPosition, viewRect);

            var listing = new Listing_Standard { ColumnWidth = viewRect.width };
            listing.Begin(viewRect);

            // Draw each category (filtered by runtime availability to hide mutators from unloaded mods)
            DrawPaletteCategory(listing, filters, "Climate", FilterByRuntime(GetClimatePaletteFilters()));
            DrawPaletteCategory(listing, filters, "Geography_Natural", FilterByRuntime(GetGeographyNaturalPaletteFilters()));
            DrawPaletteCategory(listing, filters, "Geography_Resources", FilterByRuntime(GetGeographyResourcesPaletteFilters()));
            DrawPaletteCategory(listing, filters, "Geography_Artificial", FilterByRuntime(GetGeographyArtificialPaletteFilters()));

            // Only show Mod_Filters if there are any mod-added filters
            var modFilters = GetModFiltersPaletteFilters(); // Already runtime-based
            if (modFilters.Count > 0)
            {
                DrawPaletteCategory(listing, filters, "Mod_Filters", modFilters);
            }

            listing.End();
            Widgets.EndScrollView();

            // Draw search dropdown as overlay (after scroll view)
            if (_showSearchDropdown && !string.IsNullOrEmpty(_paletteSearchQuery))
            {
                DrawSearchDropdown(searchRect, scrollRect, filters);
            }

            // Update highlight timer
            if (_highlightedFilterId != null)
            {
                _highlightTimer -= Time.deltaTime;
                if (_highlightTimer <= 0f)
                {
                    _highlightedFilterId = null;
                }
            }
        }

        /// <summary>
        /// Draws the search bar with text field and clear button.
        /// </summary>
        private static void DrawPaletteSearchBar(Rect rect)
        {
            // Background
            Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.15f));

            // Search icon (🔍)
            var iconRect = new Rect(rect.x + 4f, rect.y + 2f, 20f, rect.height - 4f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            Widgets.Label(iconRect, "🔍");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // Text field
            float clearWidth = string.IsNullOrEmpty(_paletteSearchQuery) ? 0f : 20f;
            var textRect = new Rect(rect.x + 26f, rect.y + 2f, rect.width - 30f - clearWidth, rect.height - 4f);

            string oldQuery = _paletteSearchQuery;
            _paletteSearchQuery = Widgets.TextField(textRect, _paletteSearchQuery);

            // Show/hide dropdown based on focus and query
            if (_paletteSearchQuery != oldQuery)
            {
                _showSearchDropdown = !string.IsNullOrEmpty(_paletteSearchQuery);
            }

            // Show placeholder when empty
            if (string.IsNullOrEmpty(_paletteSearchQuery))
            {
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(textRect, "LandingZone_Workspace_SearchPlaceholder".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }

            // Clear button (×) when text present
            if (!string.IsNullOrEmpty(_paletteSearchQuery))
            {
                var clearRect = new Rect(rect.xMax - 22f, rect.y + 2f, 20f, rect.height - 4f);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                if (Widgets.ButtonText(clearRect, "×", drawBackground: false))
                {
                    _paletteSearchQuery = "";
                    _showSearchDropdown = false;
                }
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        /// <summary>
        /// Gets all palette filters across all categories for search.
        /// Filters out filters not available in current runtime (mod/DLC not loaded, mutator not present).
        /// </summary>
        private static List<PaletteFilter> GetAllPaletteFilters()
        {
            var all = new List<PaletteFilter>();
            all.AddRange(GetClimatePaletteFilters());
            all.AddRange(GetGeographyNaturalPaletteFilters());
            all.AddRange(GetGeographyResourcesPaletteFilters());
            all.AddRange(GetGeographyArtificialPaletteFilters());
            all.AddRange(GetModFiltersPaletteFilters());

            // Filter out filters not available in current runtime
            return all.Where(f => IsFilterAvailable(f)).ToList();
        }

        /// <summary>
        /// Draws the search dropdown with matching filters and container items.
        /// </summary>
        private static void DrawSearchDropdown(Rect searchRect, Rect scrollRect, FilterSettings filters)
        {
            var allFilters = GetAllPaletteFilters();
            string query = _paletteSearchQuery.ToLowerInvariant();

            // Build combined search results
            var results = new List<SearchResult>();

            // 1. Direct filter matches (palette filters) - check label and search aliases
            foreach (var filter in allFilters)
            {
                bool isStartsWith = filter.Label.ToLowerInvariant().StartsWith(query);
                bool isContains = filter.Label.ToLowerInvariant().Contains(query);

                // Check search aliases from MutatorOverlays
                if (!isStartsWith && !isContains && filter.Kind == FilterKind.Mutator && !string.IsNullOrEmpty(filter.MutatorDefName))
                {
                    if (MutatorOverlays.TryGetValue(filter.MutatorDefName!, out var overlay) && overlay.SearchAliases != null)
                    {
                        foreach (var alias in overlay.SearchAliases)
                        {
                            if (alias.ToLowerInvariant().StartsWith(query))
                            {
                                isStartsWith = true;
                                break;
                            }
                            if (alias.ToLowerInvariant().Contains(query))
                            {
                                isContains = true;
                                break;
                            }
                        }
                    }
                }

                if (isStartsWith || isContains)
                {
                    results.Add(new SearchResult(filter, isStartsWith));
                }
            }

            // 2. Container item matches (stones, roads, etc.)
            var containerItemResults = GetSearchableContainerItems(query);
            results.AddRange(containerItemResults);

            // Sort: starts-with first, then alphabetically by label
            var sortedResults = results
                .OrderByDescending(r => r.IsStartsWith)
                .ThenBy(r => r.Label)
                .Take(8)  // Show up to 8 results
                .ToList();

            if (sortedResults.Count == 0)
            {
                _showSearchDropdown = false;
                return;
            }

            // Dropdown rect
            float dropdownHeight = sortedResults.Count * 24f + 4f;
            var dropdownRect = new Rect(searchRect.x, searchRect.yMax + 2f, searchRect.width, dropdownHeight);

            // Keep dropdown within scroll area bounds
            if (dropdownRect.yMax > scrollRect.yMax)
            {
                dropdownRect.height = scrollRect.yMax - dropdownRect.y;
            }

            // Draw dropdown background
            Widgets.DrawBoxSolid(dropdownRect, new Color(0.12f, 0.12f, 0.14f, 0.98f));
            Widgets.DrawBox(dropdownRect);

            // Draw matches
            float y = dropdownRect.y + 2f;
            foreach (var result in sortedResults)
            {
                var itemRect = new Rect(dropdownRect.x + 4f, y, dropdownRect.width - 8f, 22f);

                // Hover highlight
                if (Mouse.IsOver(itemRect))
                {
                    Widgets.DrawBoxSolid(itemRect, new Color(0.25f, 0.25f, 0.3f));
                }

                // "▶" indicator for starts-with matches, "↳" for container items
                string prefix = result.IsStartsWith ? "▶ " : (result.IsContainerItem ? "↳ " : "   ");

                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(itemRect, $"{prefix}{result.Label}");

                // Category hint on right
                var categoryRect = new Rect(itemRect.xMax - 80f, y, 76f, 22f);
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(categoryRect, result.CategoryHint);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;

                // Click to select
                if (Widgets.ButtonInvisible(itemRect))
                {
                    if (result.IsContainerItem)
                    {
                        SelectContainerItemSearchResult(result, filters);
                    }
                    else if (result.Filter != null)
                    {
                        SelectSearchResult(result.Filter);
                    }
                }

                y += 24f;
            }

            // Click outside to close
            if (Event.current.type == EventType.MouseDown && !dropdownRect.Contains(Event.current.mousePosition) && !searchRect.Contains(Event.current.mousePosition))
            {
                _showSearchDropdown = false;
                Event.current.Use();
            }
        }

        /// <summary>
        /// Formats category name for display in search results.
        /// </summary>
        private static string FormatCategoryName(string category)
        {
            return category switch
            {
                "Geography_Natural" => "Natural",
                "Geography_Resources" => "Resources",
                "Geography_Artificial" => "Artificial",
                "Mod_Filters" => "Mod",
                _ => category
            };
        }

        /// <summary>
        /// Represents a search result - either a direct filter match or a container item match.
        /// </summary>
        private class SearchResult
        {
            public string Label { get; }
            public string CategoryHint { get; }
            public bool IsStartsWith { get; set; }

            // For direct filter matches
            public PaletteFilter? Filter { get; }

            // For container item matches
            public string? ContainerFilterId { get; }
            public string? ItemDefName { get; }
            public ContainerType? ContainerKind { get; }

            // Direct filter match
            public SearchResult(PaletteFilter filter, bool isStartsWith = false)
            {
                Filter = filter;
                Label = filter.Label;
                CategoryHint = FormatCategoryName(filter.Category);
                IsStartsWith = isStartsWith;
            }

            // Container item match - show item with parent hint
            public SearchResult(string itemLabel, string containerLabel, string containerFilterId, ContainerType containerKind, string itemDefName, bool isStartsWith = false)
            {
                Label = $"{itemLabel} (in {containerLabel})";
                CategoryHint = containerLabel;
                ContainerFilterId = containerFilterId;
                ItemDefName = itemDefName;
                ContainerKind = containerKind;
                IsStartsWith = isStartsWith;
            }

            public bool IsContainerItem => ContainerFilterId != null;
        }

        /// <summary>
        /// Gets all searchable container items from all container types.
        /// </summary>
        private static IEnumerable<SearchResult> GetSearchableContainerItems(string query)
        {
            var results = new List<SearchResult>();

            // Hilliness container (enum values)
            var hillinessOptions = new[] {
                (Hilliness.Flat, "Flat"),
                (Hilliness.SmallHills, "Small Hills"),
                (Hilliness.LargeHills, "Large Hills"),
                (Hilliness.Mountainous, "Mountainous")
            };
            foreach (var (hilliness, label) in hillinessOptions)
            {
                bool isStartsWith = label.ToLowerInvariant().StartsWith(query);
                bool isContains = label.ToLowerInvariant().Contains(query);
                if (isStartsWith || isContains)
                {
                    results.Add(new SearchResult(label, "Hilliness", "hilliness", ContainerType.Hilliness, hilliness.ToString(), isStartsWith));
                }
            }

            // Rivers container
            foreach (var riverDef in Filtering.Filters.RiverFilter.GetAllRiverTypes())
            {
                string label = riverDef.label.CapitalizeFirst();
                bool isStartsWith = label.ToLowerInvariant().StartsWith(query);
                bool isContains = label.ToLowerInvariant().Contains(query);
                if (isStartsWith || isContains)
                {
                    results.Add(new SearchResult(label, "Rivers", "rivers", ContainerType.Rivers, riverDef.defName, isStartsWith));
                }
            }

            // Biomes container
            foreach (var biomeDef in DefDatabase<BiomeDef>.AllDefsListForReading.Where(b => b.canBuildBase))
            {
                string label = biomeDef.label.CapitalizeFirst();
                bool isStartsWith = label.ToLowerInvariant().StartsWith(query);
                bool isContains = label.ToLowerInvariant().Contains(query);
                if (isStartsWith || isContains)
                {
                    results.Add(new SearchResult(label, "Biomes", "biomes", ContainerType.Biomes, biomeDef.defName, isStartsWith));
                }
            }

            // Stones container
            foreach (var (defName, label, _) in GetStoneTypesWithMod())
            {
                bool isStartsWith = label.ToLowerInvariant().StartsWith(query);
                bool isContains = label.ToLowerInvariant().Contains(query);
                if (isStartsWith || isContains)
                {
                    results.Add(new SearchResult(label, "Natural Stones", "stones", ContainerType.Stones, defName, isStartsWith));
                }
            }

            // Roads container
            foreach (var (defName, label, _) in GetRoadTypesWithMod())
            {
                bool isStartsWith = label.ToLowerInvariant().StartsWith(query);
                bool isContains = label.ToLowerInvariant().Contains(query);
                if (isStartsWith || isContains)
                {
                    results.Add(new SearchResult(label, "Roads", "roads", ContainerType.Roads, defName, isStartsWith));
                }
            }

            // Stockpiles container
            foreach (var (defName, label, _) in GetStockpileTypes())
            {
                bool isStartsWith = label.ToLowerInvariant().StartsWith(query);
                bool isContains = label.ToLowerInvariant().Contains(query);
                if (isStartsWith || isContains)
                {
                    results.Add(new SearchResult(label, "Stockpiles", "stockpiles", ContainerType.Stockpiles, defName, isStartsWith));
                }
            }

            // Plant grove container
            foreach (var (defName, label, _) in GetPlantGroveTypesWithMod())
            {
                bool isStartsWith = label.ToLowerInvariant().StartsWith(query);
                bool isContains = label.ToLowerInvariant().Contains(query);
                if (isStartsWith || isContains)
                {
                    results.Add(new SearchResult(label, "Plant Grove", "plant_grove", ContainerType.PlantGrove, defName, isStartsWith));
                }
            }

            // Animal habitat container
            foreach (var (defName, label, _) in GetAnimalHabitatTypesWithMod())
            {
                bool isStartsWith = label.ToLowerInvariant().StartsWith(query);
                bool isContains = label.ToLowerInvariant().Contains(query);
                if (isStartsWith || isContains)
                {
                    results.Add(new SearchResult(label, "Animal Habitat", "animal_habitat", ContainerType.AnimalHabitat, defName, isStartsWith));
                }
            }

            // Mineral ores container
            foreach (var (defName, label, _) in GetMineralOreTypesWithMod())
            {
                bool isStartsWith = label.ToLowerInvariant().StartsWith(query);
                bool isContains = label.ToLowerInvariant().Contains(query);
                if (isStartsWith || isContains)
                {
                    results.Add(new SearchResult(label, "Mineral Rich", "mineral_rich", ContainerType.MineralRich, defName, isStartsWith));
                }
            }

            return results;
        }

        /// <summary>
        /// Handles selection of a search result - expands category, scrolls to filter, highlights.
        /// </summary>
        private static void SelectSearchResult(PaletteFilter filter)
        {
            // Close dropdown
            _showSearchDropdown = false;
            _paletteSearchQuery = "";

            // Expand parent category if collapsed
            if (_collapsedCategories.Contains(filter.Category))
            {
                _collapsedCategories.Remove(filter.Category);
            }

            // Set highlight
            _highlightedFilterId = filter.Id;
            _highlightTimer = HighlightDuration;

            // Calculate scroll position to show filter
            float targetY = CalculateFilterScrollPosition(filter);
            _paletteScrollPosition.y = targetY;
        }

        /// <summary>
        /// Handles selection of a container item search result - adds container to workspace if needed,
        /// scrolls to container, and opens the popup.
        /// </summary>
        private static void SelectContainerItemSearchResult(SearchResult result, FilterSettings filters)
        {
            if (result.ContainerFilterId == null || !result.ContainerKind.HasValue) return;

            // Close dropdown
            _showSearchDropdown = false;
            _paletteSearchQuery = "";

            // Find the parent container filter
            var allFilters = GetAllPaletteFilters();
            var containerFilter = allFilters.FirstOrDefault(f => f.Id == result.ContainerFilterId);
            if (containerFilter == null) return;

            // Expand parent category if collapsed
            if (_collapsedCategories.Contains(containerFilter.Category))
            {
                _collapsedCategories.Remove(containerFilter.Category);
            }

            // Check if container is already in workspace
            var (existingChip, _, importance) = _workspace?.FindChip(result.ContainerFilterId) ?? (null, null, null);
            if (existingChip == null)
            {
                // Add container to workspace at Preferred importance
                var newChip = new BucketWorkspace.FilterChip(
                    result.ContainerFilterId,
                    containerFilter.Label,
                    containerFilter.IsHeavy,
                    containerFilter.Category
                );
                _workspace?.AddChip(newChip, FilterImportance.Preferred);
                OnFiltersModified(); // User changed filters - clear preset tracking
            }

            // Set highlight on the container
            _highlightedFilterId = result.ContainerFilterId;
            _highlightTimer = HighlightDuration;

            // Calculate scroll position to show container
            float targetY = CalculateFilterScrollPosition(containerFilter);
            _paletteScrollPosition.y = targetY;

            // Note: Opening the popup directly from search is complex because we need the chip rect.
            // For now, we just scroll to and highlight the container - user can click to open popup.
        }

        /// <summary>
        /// Calculates the scroll position to show a specific filter.
        /// </summary>
        private static float CalculateFilterScrollPosition(PaletteFilter targetFilter)
        {
            const float SubGroupSeparatorHeight = 18f;
            float y = 0f;
            // Filter each category by runtime availability (hide mutators from unloaded mods)
            var categories = new (string key, List<PaletteFilter> filters)[]
            {
                ("Climate", FilterByRuntime(GetClimatePaletteFilters())),
                ("Geography_Natural", FilterByRuntime(GetGeographyNaturalPaletteFilters())),
                ("Geography_Resources", FilterByRuntime(GetGeographyResourcesPaletteFilters())),
                ("Geography_Artificial", FilterByRuntime(GetGeographyArtificialPaletteFilters())),
                ("Mod_Filters", GetModFiltersPaletteFilters()) // Already runtime-based
            };

            foreach (var (key, filters) in categories)
            {
                y += CategoryHeaderHeight;  // Category header

                if (!_collapsedCategories.Contains(key))
                {
                    string? lastSubGroup = null;
                    foreach (var filter in filters)
                    {
                        // Account for sub-group separator
                        if (filter.SubGroup != lastSubGroup && filter.SubGroup != null)
                        {
                            y += SubGroupSeparatorHeight;
                            lastSubGroup = filter.SubGroup;
                        }

                        if (filter.Id == targetFilter.Id)
                        {
                            // Return position with some padding above
                            return Math.Max(0f, y - 20f);
                        }

                        // Skip collapsed sub-group filters in height calculation
                        if (filter.SubGroup != null)
                        {
                            string collapseKey = $"{key}:{filter.SubGroup}";
                            if (_collapsedSubGroups.Contains(collapseKey))
                                continue;
                        }

                        y += 24f;  // FilterItemHeight
                    }
                }

                y += 8f;  // CategorySpacing
            }

            return 0f;
        }

        /// <summary>
        /// Draws a category of filters in the palette with collapse/expand support.
        /// </summary>
        private static void DrawPaletteCategory(Listing_Standard listing, FilterSettings filters, string categoryKey, List<PaletteFilter> paletteFilters)
        {
            // Count active filters in this category
            int activeCount = paletteFilters.Count(pf => _workspace?.GetChipImportance(pf.Id).HasValue ?? false);
            bool isCollapsed = _collapsedCategories.Contains(categoryKey);

            // Category header - clickable to toggle collapse
            var headerRect = listing.GetRect(CategoryHeaderHeight);
            Widgets.DrawBoxSolid(headerRect, new Color(0.15f, 0.15f, 0.2f));

            // Collapse arrow
            string arrow = isCollapsed ? "▶" : "▼";
            var arrowRect = new Rect(headerRect.x + 4f, headerRect.y, 16f, headerRect.height);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(arrowRect, arrow);

            // Category name with count
            string categoryLabel = $"LandingZone_Workspace_Category_{categoryKey}".Translate();
            string displayLabel = activeCount > 0
                ? $"{categoryLabel} [{activeCount}]"
                : categoryLabel;

            var labelRect = new Rect(headerRect.x + 20f, headerRect.y, headerRect.width - 24f, headerRect.height);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;

            // Highlight category if has active filters
            if (activeCount > 0)
            {
                GUI.color = new Color(0.7f, 0.9f, 0.7f);
            }
            Widgets.Label(labelRect, displayLabel);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            // Click to toggle collapse
            if (Widgets.ButtonInvisible(headerRect))
            {
                if (isCollapsed)
                    _collapsedCategories.Remove(categoryKey);
                else
                    _collapsedCategories.Add(categoryKey);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }

            listing.Gap(2f);

            // Skip filters if collapsed
            if (isCollapsed)
            {
                listing.Gap(4f);
                return;
            }

            // Draw each filter (with sub-group separators)
            string? lastSubGroup = null;
            foreach (var pf in paletteFilters)
            {
                // Draw sub-group separator when sub-group changes
                if (pf.SubGroup != lastSubGroup && pf.SubGroup != null)
                {
                    DrawSubGroupSeparator(listing, pf.SubGroup, categoryKey);
                    lastSubGroup = pf.SubGroup;
                }
                else if (pf.SubGroup == null && lastSubGroup != null)
                {
                    // Transitioning from a sub-group back to ungrouped
                    lastSubGroup = null;
                }

                // Skip filter if its sub-group is collapsed
                if (pf.SubGroup != null)
                {
                    string collapseKey = $"{categoryKey}:{pf.SubGroup}";
                    if (_collapsedSubGroups.Contains(collapseKey))
                        continue;
                }

                var importance = _workspace?.GetChipImportance(pf.Id);
                bool isInWorkspace = importance.HasValue;

                var filterRect = listing.GetRect(ChipHeight);

                // Background color based on state
                Color bgColor;
                if (isInWorkspace)
                {
                    var bucket = BucketWorkspace.AllBuckets.FirstOrDefault(b => b.Importance == importance);
                    bgColor = bucket?.Color ?? Color.gray;
                    bgColor.a = 0.3f;
                }
                else
                {
                    bgColor = new Color(0.2f, 0.2f, 0.2f);
                }

                Widgets.DrawBoxSolid(filterRect, bgColor);

                // Highlight effect from search
                if (_highlightedFilterId == pf.Id && _highlightTimer > 0f)
                {
                    // Pulsing highlight that fades out
                    float alpha = _highlightTimer / HighlightDuration;
                    float pulse = 0.3f + 0.2f * Mathf.Sin(Time.realtimeSinceStartup * 6f);
                    Color highlightColor = new Color(0.4f, 0.7f, 1f, alpha * pulse);
                    Widgets.DrawBoxSolid(filterRect, highlightColor);
                    Widgets.DrawBox(filterRect, 2, Texture2D.whiteTexture);
                }

                // Filter label - shrink to make room for source indicator and (i) icon
                // Always reserve space for (i) icon to keep MOD badges aligned consistently
                bool hasTooltip = !string.IsNullOrEmpty(pf.TooltipBrief) || !string.IsNullOrEmpty(pf.TooltipDetailed);
                float infoIconWidth = 18f; // Always reserve space for alignment

                // Source detection for DLC/MOD badges
                string? sourceBadge = null;
                string? sourceTooltip = null;
                if (pf.Kind == FilterKind.Mutator && !string.IsNullOrEmpty(pf.MutatorDefName))
                {
                    // Priority 1: Static DLC annotation (explicit, curated)
                    if (!string.IsNullOrEmpty(pf.RequiredDLC))
                    {
                        sourceBadge = "DLC";
                        sourceTooltip = $"{"LandingZone_RequiresDLC".Translate()} {pf.RequiredDLC}";
                    }
                    // Priority 2: Static Mod annotation (explicit, curated)
                    else if (!string.IsNullOrEmpty(pf.RequiredMod))
                    {
                        sourceBadge = "MOD";
                        sourceTooltip = $"{"LandingZone_AddedByMod".Translate()} {pf.RequiredMod}";
                    }
                    // Priority 3: Dynamic detection (runtime, for uncurated mod mutators)
                    else
                    {
                        var sourceInfo = MapFeatureFilter.GetMutatorSource(pf.MutatorDefName!);
                        if (sourceInfo.Type == MapFeatureFilter.MutatorSourceType.DLC)
                        {
                            sourceBadge = "DLC";
                            sourceTooltip = $"{"LandingZone_RequiresDLC".Translate()} {sourceInfo.SourceName}";
                        }
                        else if (sourceInfo.Type == MapFeatureFilter.MutatorSourceType.Mod)
                        {
                            sourceBadge = "MOD";
                            sourceTooltip = $"{"LandingZone_AddedByMod".Translate()} {sourceInfo.SourceName}";
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(pf.RequiredDLC))
                {
                    // Static DLC requirement for non-mutator filters
                    sourceBadge = "DLC";
                    sourceTooltip = $"{"LandingZone_RequiresDLC".Translate()} {pf.RequiredDLC}";
                }

                bool hasSourceIndicator = sourceBadge != null;
                float sourceIndicatorWidth = hasSourceIndicator ? 28f : 0f;
                var filterLabelRect = new Rect(filterRect.x + 4f, filterRect.y, filterRect.width - 90f - infoIconWidth - sourceIndicatorWidth, filterRect.height);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;

                string filterDisplayLabel = pf.Label;
                if (pf.IsHeavy)
                {
                    GUI.color = new Color(1f, 0.8f, 0.4f);
                    filterDisplayLabel = "⚠ " + filterDisplayLabel;
                }

                Widgets.Label(filterLabelRect, filterDisplayLabel);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;

                // Source indicator (DLC/MOD label) between filter name and (i) icon
                if (hasSourceIndicator)
                {
                    var sourceRect = new Rect(filterLabelRect.xMax + 2f, filterRect.y, sourceIndicatorWidth - 2f, filterRect.height);
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    // DLC = gray, MOD = blue-ish
                    GUI.color = sourceBadge == "MOD"
                        ? new Color(0.45f, 0.55f, 0.65f)
                        : new Color(0.55f, 0.55f, 0.55f);
                    Widgets.Label(sourceRect, sourceBadge);
                    GUI.color = Color.white;
                    Text.Anchor = TextAnchor.UpperLeft;
                    Text.Font = GameFont.Small;
                    if (sourceTooltip != null)
                        TooltipHandler.TipRegion(sourceRect, sourceTooltip);
                }

                // (i) info icon for detailed tooltip - position after source indicator if present
                if (hasTooltip && !string.IsNullOrEmpty(pf.TooltipDetailed))
                {
                    float infoIconX = filterLabelRect.xMax + sourceIndicatorWidth + 2f;
                    var infoIconRect = new Rect(infoIconX, filterRect.y + 2f, 14f, filterRect.height - 4f);
                    bool isDetailOpen = _detailedTooltipFilterId == pf.Id;

                    // Draw (i) icon
                    GUI.color = isDetailOpen ? new Color(0.5f, 0.8f, 1f) : new Color(0.6f, 0.6f, 0.6f);
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(infoIconRect, "ⓘ");
                    Text.Anchor = TextAnchor.UpperLeft;
                    Text.Font = GameFont.Small;
                    GUI.color = Color.white;

                    // Handle click on info icon
                    if (Widgets.ButtonInvisible(infoIconRect))
                    {
                        if (isDetailOpen)
                        {
                            _detailedTooltipFilterId = null;
                        }
                        else
                        {
                            _detailedTooltipFilterId = pf.Id;
                            _detailedTooltipAnchorRect = infoIconRect;
                        }
                    }

                    TooltipHandler.TipRegion(infoIconRect, "LandingZone_Workspace_ClickForDetails".Translate());
                }

                // Brief tooltip on hover over label
                if (!string.IsNullOrEmpty(pf.TooltipBrief) || !string.IsNullOrEmpty(pf.RequiredDLC))
                {
                    var tooltip = "";
                    if (!string.IsNullOrEmpty(pf.RequiredDLC))
                    {
                        tooltip = $"[{pf.RequiredDLC}] ";
                    }
                    if (!string.IsNullOrEmpty(pf.TooltipBrief))
                    {
                        tooltip += pf.TooltipBrief.Translate();
                    }
                    TooltipHandler.TipRegion(filterLabelRect, tooltip.Trim());
                }

                // "Add to..." button or current bucket indicator
                var buttonRect = new Rect(filterRect.xMax - 80f, filterRect.y + 2f, 76f, filterRect.height - 4f);

                if (isInWorkspace)
                {
                    // Show current bucket and allow click to remove
                    var bucket = BucketWorkspace.AllBuckets.FirstOrDefault(b => b.Importance == importance);
                    GUI.color = bucket?.Color ?? Color.white;
                    if (Widgets.ButtonText(buttonRect, bucket?.Label ?? "?", drawBackground: true))
                    {
                        // Click to remove from workspace
                        ClearChipFromFilterSettings(pf.Id);  // Clear from FilterSettings to prevent ghost filters
                        _workspace?.RemoveChip(pf.Id);
                        OnFiltersModified(); // User changed filters - clear preset tracking
                    }
                    GUI.color = Color.white;
                    TooltipHandler.TipRegion(buttonRect, "LandingZone_Workspace_RemoveTooltip".Translate());
                }
                else
                {
                    // Show "Add" button that opens bucket menu
                    if (Widgets.ButtonText(buttonRect, "LandingZone_Workspace_AddButton".Translate()))
                    {
                        ShowAddToBucketMenu(pf, filters);
                    }
                }

                // Heavy filter warning tooltip
                if (pf.IsHeavy)
                {
                    TooltipHandler.TipRegion(filterRect, "LandingZone_Workspace_HeavyFilterWarning".Translate());
                }

                listing.Gap(2f);
            }

            listing.Gap(6f);
        }

        /// <summary>
        /// Draws a visual separator for sub-groups within a category.
        /// Format: "── Sub Group Name ──"
        /// </summary>
        private static void DrawSubGroupSeparator(Listing_Standard listing, string subGroup, string categoryKey)
        {
            const float SeparatorHeight = 18f;
            var rect = listing.GetRect(SeparatorHeight);

            // Check if sub-group is collapsed
            string collapseKey = $"{categoryKey}:{subGroup}";
            bool isCollapsed = _collapsedSubGroups.Contains(collapseKey);

            // Draw subtle divider lines on each side of label
            float lineY = rect.y + rect.height / 2f;
            float labelWidth = Text.CalcSize(subGroup).x + 16f;  // Extra padding
            float lineSpace = 6f;

            // Collapse indicator
            string collapseArrow = isCollapsed ? "▸" : "";
            float arrowWidth = isCollapsed ? 12f : 0f;

            // Calculate line and label positions
            float totalLabelWidth = arrowWidth + labelWidth;
            float leftLineEnd = rect.x + (rect.width - totalLabelWidth) / 2f - lineSpace;
            float labelStart = leftLineEnd + lineSpace;
            float rightLineStart = labelStart + totalLabelWidth + lineSpace;

            // Draw left line
            GUI.color = new Color(0.4f, 0.4f, 0.4f);
            Widgets.DrawLineHorizontal(rect.x + 4f, lineY, leftLineEnd - rect.x - 8f);

            // Draw right line
            Widgets.DrawLineHorizontal(rightLineStart, lineY, rect.xMax - rightLineStart - 4f);
            GUI.color = Color.white;

            // Draw collapse arrow if collapsed
            if (isCollapsed)
            {
                var arrowRect = new Rect(labelStart, rect.y, arrowWidth, rect.height);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                Widgets.Label(arrowRect, collapseArrow);
            }

            // Draw sub-group label
            var labelRect = new Rect(labelStart + arrowWidth, rect.y, labelWidth, rect.height);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = new Color(0.6f, 0.6f, 0.65f);
            Widgets.Label(labelRect, subGroup);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // Click to toggle collapse
            if (Widgets.ButtonInvisible(rect))
            {
                if (isCollapsed)
                    _collapsedSubGroups.Remove(collapseKey);
                else
                    _collapsedSubGroups.Add(collapseKey);
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
        }

        /// <summary>
        /// Shows a float menu to select which bucket to add a filter to.
        /// </summary>
        private static void ShowAddToBucketMenu(PaletteFilter pf, FilterSettings filters)
        {
            var options = new List<FloatMenuOption>();

            foreach (var bucket in BucketWorkspace.AllBuckets)
            {
                var importance = bucket.Importance;
                string label = bucket.Label;

                // Note: Heavy filters can now be set to MustHave/MustNot - warning dialog will show at search time
                if (pf.IsHeavy && importance.IsHardGate())
                {
                    label += " ⚠";  // Just a warning indicator, no auto-demotion
                }

                options.Add(new FloatMenuOption(label, () =>
                {
                    var chip = new BucketWorkspace.FilterChip(
                        pf.Id,
                        pf.Label,
                        pf.IsHeavy,
                        pf.Category,
                        pf.ValueDisplay
                    );
                    _workspace?.AddChip(chip, importance);
                    OnFiltersModified(); // User changed filters - clear preset tracking

                    // Show warning for heavy filters in hard gates
                    if (pf.IsHeavy && importance.IsHardGate())
                    {
                        Messages.Message(
                            "LandingZone_Workspace_HeavyDemoteWarning".Translate(pf.Label),
                            MessageTypeDefOf.CautionInput,
                            false
                        );
                    }
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        /// <summary>
        /// Calculates the rendered width of a chip, including formatted label and all indicators.
        /// This is the single source of truth for chip width calculation, used by both
        /// CalculateBucketContentHeight and DrawClauseChips to ensure consistent wrapping.
        /// </summary>
        private static float CalculateChipWidth(BucketWorkspace.FilterChip chip)
        {
            var lookup = GetPaletteFilterLookup();
            bool hasRange = lookup.TryGetValue(chip.FilterId, out var paletteFilter) && paletteFilter.HasRange;
            bool isContainer = paletteFilter?.Kind == FilterKind.Container && paletteFilter.ContainerKind != null;

            // Build display label with range value or container state (matches DrawSingleChip logic)
            string displayLabel = chip.Label;
            var state = LandingZoneContext.State;
            if (state?.Preferences != null)
            {
                var filters = state.Preferences.GetActiveFilters();
                if (isContainer && paletteFilter != null)
                {
                    displayLabel = FormatContainerChipLabel(chip.Label, paletteFilter.ContainerKind!.Value, filters);
                }
                else if (hasRange && paletteFilter != null)
                {
                    var range = GetFilterRange(chip.FilterId, filters);
                    displayLabel = FormatChipLabelWithRange(chip.Label, range, paletteFilter);
                }
            }

            // Calculate width based on formatted label (matches DrawSingleChip lines 2344-2348)
            float textWidth = Text.CalcSize(displayLabel).x;
            float chipWidth = textWidth + 24f;
            if (chip.IsHeavy) chipWidth += 20f;
            if (hasRange || isContainer) chipWidth += 20f; // Space for range/container indicator

            return chipWidth;
        }

        /// <summary>
        /// Calculates the rendered width of an OR group chip.
        /// </summary>
        private static float CalculateOrGroupWidth(BucketWorkspace.OrGroup group)
        {
            string label = group.GetDisplayLabel();
            return Text.CalcSize(label).x + 32f; // OR indicator + padding
        }

        /// <summary>
        /// Calculates the content height needed for a bucket based on its chips.
        /// Uses CalculateChipWidth to ensure consistent wrapping with actual rendering.
        /// </summary>
        private static float CalculateBucketContentHeight(BucketWorkspace.ImportanceBucket bucket, float availableWidth)
        {
            if (_workspace == null) return 0f;

            var clauses = _workspace.GetClausesInBucket(bucket.Importance).ToList();
            if (clauses.Count == 0 || (clauses.Count == 1 && clauses[0].IsEmpty))
            {
                return ChipHeight; // One row for empty state
            }

            // Calculate rows based on chips in clauses
            float totalContentHeight = 0f;
            float effectiveWidth = availableWidth - 16f; // Account for bucket padding

            foreach (var clause in clauses)
            {
                var items = _workspace.GetRenderableItemsInClause(clause.ClauseId).ToList();
                if (items.Count == 0) continue;

                float x = 0f;
                int rows = 1;

                foreach (var item in items)
                {
                    float itemWidth;
                    if (item is BucketWorkspace.OrGroup group)
                    {
                        itemWidth = CalculateOrGroupWidth(group);
                    }
                    else if (item is BucketWorkspace.FilterChip chip)
                    {
                        itemWidth = CalculateChipWidth(chip);
                    }
                    else
                    {
                        itemWidth = 60f;
                    }

                    // Account for AND connector between items
                    float andWidth = items.IndexOf(item) > 0 ? 36f : 0f;

                    if (x + andWidth + itemWidth > effectiveWidth && x > 0f)
                    {
                        rows++;
                        x = 0f;
                    }
                    x += andWidth + itemWidth + ChipPadding;
                }

                totalContentHeight += rows * (ChipHeight + ChipPadding);
            }

            // Add space for clause headers if multiple clauses
            if (clauses.Count > 1)
            {
                totalContentHeight += clauses.Count * ClauseHeaderHeight;
                totalContentHeight += (clauses.Count - 1) * ClausePadding; // OR separators
            }

            return totalContentHeight;
        }

        /// <summary>
        /// Draws the four importance buckets with dynamic heights and single outer scroll.
        /// Each bucket expands to fit its content - no per-bucket scrollbars.
        /// </summary>
        private static void DrawBuckets(Rect rect, FilterSettings filters)
        {
            var contentRect = rect;

            // Legend at top (fixed, outside scroll)
            var legendRect = new Rect(contentRect.x, contentRect.y, contentRect.width, LegendHeight);
            DrawLegend(legendRect);

            // Logic summary at bottom (fixed, outside scroll)
            var summaryRect = new Rect(contentRect.x, contentRect.yMax - LogicSummaryHeight - 8f, contentRect.width, LogicSummaryHeight);
            DrawLogicSummary(summaryRect);

            // Scrollable bucket area between legend and summary
            float bucketsY = legendRect.yMax + 8f;
            float bucketAreaHeight = summaryRect.y - bucketsY - 8f;
            var bucketAreaRect = new Rect(contentRect.x, bucketsY, contentRect.width, bucketAreaHeight);

            // Store for popup coordinate conversion (chip rects are in scroll-content space)
            _bucketAreaRect = bucketAreaRect;
            _lastBucketScrollPosition = _bucketScrollPosition;

            // Phase 1: Calculate natural heights for each bucket
            var buckets = BucketWorkspace.AllBuckets.ToList();
            var naturalHeights = new Dictionary<FilterImportance, float>();
            const float gapBetweenBuckets = 6f;

            foreach (var bucket in buckets)
            {
                float contentHeight = CalculateBucketContentHeight(bucket, contentRect.width - 20f); // Account for scroll bar
                // Natural height = header + toolbar + content + padding, minimum for empty buckets
                float natural = BucketHeaderHeight + BucketToolbarHeight + contentHeight + BucketOverhead;
                natural = Mathf.Max(natural, BucketMinCollapsedHeight);
                naturalHeights[bucket.Importance] = natural;
            }

            // Phase 2: Calculate total content height for scroll view
            float totalContentHeight = naturalHeights.Values.Sum() + (buckets.Count - 1) * gapBetweenBuckets;

            // Phase 3: Draw buckets in single scroll view
            var viewRect = new Rect(0f, 0f, bucketAreaRect.width - 16f, totalContentHeight);
            _bucketScrollPosition = GUI.BeginScrollView(bucketAreaRect, _bucketScrollPosition, viewRect);

            float y = 0f;
            foreach (var bucket in buckets)
            {
                float bucketHeight = naturalHeights[bucket.Importance];
                var bucketRect = new Rect(0f, y, viewRect.width, bucketHeight);
                DrawBucket(bucketRect, bucket, filters);
                y += bucketHeight + gapBetweenBuckets;
            }

            GUI.EndScrollView();
        }

        /// <summary>
        /// Draws the legend explaining bucket semantics.
        /// </summary>
        private static void DrawLegend(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.12f, 0.12f, 0.15f));
            Widgets.DrawBox(rect);

            var innerRect = rect.ContractedBy(6f);

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);

            string legendText =
                "LandingZone_Workspace_LegendLine1".Translate() + "\n" +
                "LandingZone_Workspace_LegendLine2".Translate();

            Widgets.Label(innerRect, legendText);

            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        /// <summary>
        /// Draws a single importance bucket at its natural height (no per-bucket scroll).
        /// </summary>
        private static void DrawBucket(Rect rect, BucketWorkspace.ImportanceBucket bucket, FilterSettings filters)
        {
            // Drop target detection for bucket-level drops
            bool isDropTarget = false;
            if (_isDragging && Mouse.IsOver(rect))
            {
                _dropTargetBucket = bucket.Importance;
                if (!_dropTargetClauseId.HasValue)
                {
                    _isValidDropTarget = true;
                }
                isDropTarget = true;
            }

            // Background
            Color bgColor = new Color(bucket.Color.r * 0.15f, bucket.Color.g * 0.15f, bucket.Color.b * 0.15f, 0.9f);
            if (isDropTarget && !_dropTargetClauseId.HasValue)
            {
                // Highlight as drop target (only if no specific clause is targeted)
                bgColor = new Color(bucket.Color.r * 0.35f, bucket.Color.g * 0.35f, bucket.Color.b * 0.35f, 0.95f);
            }

            Widgets.DrawBoxSolid(rect, bgColor);

            // Colored left border (thicker when drop target)
            float borderWidth = (isDropTarget && !_dropTargetClauseId.HasValue) ? 6f : 4f;
            var borderRect = new Rect(rect.x, rect.y, borderWidth, rect.height);
            Widgets.DrawBoxSolid(borderRect, bucket.Color);

            // Drop target indicator border
            if (isDropTarget && !_dropTargetClauseId.HasValue)
            {
                GUI.color = bucket.Color;
                Widgets.DrawBox(rect, 2);
                GUI.color = Color.white;
            }

            // Header
            var headerRect = new Rect(rect.x + 10f, rect.y + 4f, rect.width - 20f, 18f);
            Text.Font = GameFont.Small;
            GUI.color = bucket.Color;
            Widgets.Label(headerRect, bucket.Label);
            GUI.color = Color.white;

            // Subtitle / inline hint - show different hint based on clause count
            var clauses = _workspace?.GetClausesInBucket(bucket.Importance).ToList() ?? new List<BucketWorkspace.Clause>();
            bool hasMultipleClauses = clauses.Count > 1;

            var subtitleRect = new Rect(rect.x + 10f, rect.y + 20f, rect.width - 120f, 14f);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.55f, 0.55f, 0.55f);
            string hint = hasMultipleClauses
                ? "LandingZone_Workspace_ClauseHint".Translate()
                : "LandingZone_Workspace_BucketInlineHint".Translate();
            Widgets.Label(subtitleRect, hint);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // Toolbar row with Group/Ungroup buttons
            var toolbarRect = new Rect(rect.x + 8f, rect.y + 34f, rect.width - 16f, BucketToolbarHeight);
            DrawBucketToolbar(toolbarRect, bucket.Importance);

            // Clauses area - draw directly at natural height (outer scroll handles overflow)
            var clausesRect = new Rect(rect.x + 8f, rect.y + 34f + BucketToolbarHeight + 2f, rect.width - 16f, rect.height - 34f - BucketToolbarHeight - 10f);
            DrawBucketClauses(clausesRect, bucket);

            // Tooltip
            TooltipHandler.TipRegion(rect, bucket.Tooltip);
        }

        /// <summary>
        /// Draws all clauses within a bucket.
        /// Clause UI is opt-in: only shown when there's more than one clause.
        /// </summary>
        private static void DrawBucketClauses(Rect rect, BucketWorkspace.ImportanceBucket bucket)
        {
            if (_workspace == null) return;

            var clauses = _workspace.GetClausesInBucket(bucket.Importance).ToList();
            if (clauses.Count == 0) return;

            // If only one clause and it's empty, show traditional empty bucket message
            if (clauses.Count == 1 && clauses[0].IsEmpty)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.4f, 0.4f, 0.4f);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, "LandingZone_Workspace_EmptyBucket".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                Text.Font = GameFont.Small;

                // Still need to handle drops on empty bucket
                if (_isDragging && Mouse.IsOver(rect))
                {
                    _dropTargetClauseId = clauses[0].ClauseId;
                    _isValidDropTarget = true;
                }
                return;
            }

            // OPT-IN: If only one clause with content, show chips directly (no clause UI)
            if (clauses.Count == 1)
            {
                var singleClause = clauses[0];
                DrawClauseChips(rect, singleClause, bucket.Importance);

                // Handle drops on this single clause
                if (_isDragging && Mouse.IsOver(rect))
                {
                    _dropTargetClauseId = singleClause.ClauseId;
                    _dropTargetBucket = bucket.Importance;
                    _isValidDropTarget = true;
                }
                return;
            }

            // Multiple clauses: show full clause UI with headers and OR separators
            // Calculate height per clause
            float totalAvailableHeight = rect.height;
            float clauseCount = clauses.Count;
            float clauseHeight = Math.Max(ClauseMinHeight, (totalAvailableHeight - (clauseCount - 1) * ClausePadding) / clauseCount);

            float y = rect.y;
            int clauseNumber = 1;
            foreach (var clause in clauses)
            {
                var clauseRect = new Rect(rect.x, y, rect.width, clauseHeight);
                DrawClause(clauseRect, clause, bucket, clauseNumber, clauses.Count > 1);
                y += clauseHeight + ClausePadding;
                clauseNumber++;
            }
        }

        /// <summary>
        /// Draws a single clause within a bucket.
        /// </summary>
        private static void DrawClause(Rect rect, BucketWorkspace.Clause clause, BucketWorkspace.ImportanceBucket bucket, int clauseNumber, bool showHeader)
        {
            if (_workspace == null) return;

            // Check if this clause is a drop target
            bool isDropTarget = false;
            if (_isDragging && Mouse.IsOver(rect))
            {
                _dropTargetClauseId = clause.ClauseId;
                _dropTargetBucket = bucket.Importance;
                _isValidDropTarget = true;
                isDropTarget = true;
            }

            // Background
            Color bgColor = isDropTarget
                ? new Color(bucket.Color.r * 0.3f, bucket.Color.g * 0.3f, bucket.Color.b * 0.3f, 0.85f)
                : new Color(bucket.Color.r * 0.08f, bucket.Color.g * 0.08f, bucket.Color.b * 0.08f, 0.6f);
            Widgets.DrawBoxSolid(rect, bgColor);

            // Border - dashed/highlight if drop target
            if (isDropTarget)
            {
                GUI.color = bucket.Color;
                Widgets.DrawBox(rect, 2);
                GUI.color = Color.white;
            }
            else if (showHeader)
            {
                GUI.color = new Color(bucket.Color.r * 0.5f, bucket.Color.g * 0.5f, bucket.Color.b * 0.5f, 0.5f);
                Widgets.DrawBox(rect, 1);
                GUI.color = Color.white;
            }

            float chipsY = rect.y;

            // Clause header (only show if multiple clauses)
            if (showHeader)
            {
                var headerRect = new Rect(rect.x + 4f, rect.y + 2f, rect.width - 28f, ClauseHeaderHeight - 4f);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.75f, 0.75f, 0.85f);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(headerRect, clause.GetDisplayLabel(clauseNumber));
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                Text.Font = GameFont.Small;

                // Remove clause button (X)
                var removeRect = new Rect(rect.xMax - 22f, rect.y + 2f, 18f, 18f);
                GUI.color = new Color(0.8f, 0.4f, 0.4f);
                if (Widgets.ButtonText(removeRect, "×", drawBackground: false))
                {
                    _workspace.RemoveClause(clause.ClauseId);
                }
                GUI.color = Color.white;
                TooltipHandler.TipRegion(removeRect, "LandingZone_Workspace_RemoveClauseTooltip".Translate());

                chipsY += ClauseHeaderHeight;
            }

            // Chips area
            var chipsRect = new Rect(rect.x + 4f, chipsY + 2f, rect.width - 8f, rect.height - (chipsY - rect.y) - 4f);
            DrawClauseChips(chipsRect, clause, bucket.Importance);

            // Show OR hint between clauses
            if (showHeader && clauseNumber > 1)
            {
                // Draw "OR" label above this clause
                var orLabelRect = new Rect(rect.x + rect.width / 2f - 15f, rect.y - ClausePadding - 2f, 30f, ClausePadding + 4f);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.6f, 0.5f, 0.8f);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(orLabelRect, "OR");
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }
        }

        /// <summary>
        /// Draws chips within a specific clause.
        /// </summary>
        private static void DrawClauseChips(Rect rect, BucketWorkspace.Clause clause, FilterImportance importance)
        {
            if (_workspace == null) return;

            var items = _workspace.GetRenderableItemsInClause(clause.ClauseId).ToList();

            if (items.Count == 0)
            {
                // Empty clause state
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.4f, 0.4f, 0.4f);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, "LandingZone_Workspace_EmptyClause".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                return;
            }

            // Draw chips/groups horizontally with wrapping
            float x = rect.x;
            float y = rect.y;
            const float andWidth = 36f;  // AND label width with padding on both sides
            const float andPadding = 4f; // Padding on each side of AND
            int itemIndex = 0;

            foreach (var item in items)
            {
                // Calculate item width BEFORE drawing to check if we need to wrap
                // Uses helper functions to ensure consistent width calculation with CalculateBucketContentHeight
                float itemWidth;
                if (item is BucketWorkspace.OrGroup grp)
                {
                    itemWidth = CalculateOrGroupWidth(grp);
                }
                else if (item is BucketWorkspace.FilterChip chp)
                {
                    itemWidth = CalculateChipWidth(chp);
                }
                else
                {
                    itemWidth = 60f;
                }

                // For items after first: check if AND + item will fit, wrap if needed
                if (itemIndex > 0)
                {
                    float neededWidth = andWidth + itemWidth;
                    if (x + neededWidth > rect.xMax - 10f)
                    {
                        // Wrap to next line
                        x = rect.x;
                        y += ChipHeight + ChipPadding;
                    }

                    // Draw clickable "AND" connector (always between items)
                    var andRect = new Rect(x + andPadding, y, andWidth - andPadding * 2, ChipHeight);
                    var prevItem = items[itemIndex - 1];

                    // Check for hover - show visual feedback
                    bool isHovered = Mouse.IsOver(andRect);
                    if (isHovered)
                    {
                        Widgets.DrawBoxSolid(andRect, new Color(0.3f, 0.3f, 0.4f, 0.4f));
                    }

                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = isHovered ? new Color(0.7f, 0.7f, 0.8f, 0.9f) : new Color(0.5f, 0.5f, 0.55f, 0.7f);
                    Widgets.Label(andRect, "AND");
                    GUI.color = Color.white;
                    Text.Anchor = TextAnchor.UpperLeft;
                    Text.Font = GameFont.Small;

                    // Handle click on AND connector - select both adjacent chips
                    Event evt = Event.current;
                    if (isHovered && evt.type == EventType.MouseDown && evt.button == 0)
                    {
                        _selectedChipIds.Clear();
                        _selectedOrGroupId = null;

                        if (prevItem is BucketWorkspace.FilterChip prevChip && prevChip.OrGroupId == null)
                        {
                            _selectedChipIds.Add(prevChip.FilterId);
                        }
                        if (item is BucketWorkspace.FilterChip currentChip && currentChip.OrGroupId == null)
                        {
                            _selectedChipIds.Add(currentChip.FilterId);
                        }

                        _selectionBucket = importance;
                        _selectionClauseId = clause.ClauseId;
                        evt.Use();
                    }

                    TooltipHandler.TipRegion(andRect, "LandingZone_Workspace_AndClickTooltip".Translate());
                    x += andWidth;
                }
                else
                {
                    // First item: check if it fits, wrap if needed (shouldn't happen but safety check)
                    if (x + itemWidth > rect.xMax - 10f && x > rect.x)
                    {
                        x = rect.x;
                        y += ChipHeight + ChipPadding;
                    }
                }

                // Draw the item at current position and use ACTUAL width returned
                if (item is BucketWorkspace.OrGroup group)
                {
                    float actualWidth = DrawOrGroupChip(new Rect(x, y, 0, ChipHeight), group, importance, clause.ClauseId);
                    x += actualWidth + ChipPadding;
                }
                else if (item is BucketWorkspace.FilterChip chip)
                {
                    float actualWidth = DrawSingleChip(new Rect(x, y, 0, ChipHeight), chip, importance, clause.ClauseId);
                    x += actualWidth + ChipPadding;
                }

                itemIndex++;
            }
            // Range editor is drawn as an overlay popup in DrawRangeEditorPopup()
        }

        /// <summary>
        /// Draws the toolbar for a bucket with Group/Ungroup buttons.
        /// </summary>
        private static void DrawBucketToolbar(Rect rect, FilterImportance importance)
        {
            float buttonWidth = 85f;
            float x = rect.x;

            // Check if we can group (2+ chips selected in this bucket, none already grouped)
            bool canGroup = _selectedChipIds.Count >= 2 &&
                           _selectionBucket == importance &&
                           _selectedOrGroupId == null;

            if (canGroup && _workspace != null)
            {
                // Verify none are already in groups
                var chips = _workspace.GetChipsInBucket(importance);
                foreach (var chipId in _selectedChipIds)
                {
                    var chip = chips.FirstOrDefault(c => c.FilterId == chipId);
                    if (chip?.OrGroupId != null)
                    {
                        canGroup = false;
                        break;
                    }
                }
            }

            // Check if we can ungroup (an OR group is selected in this bucket)
            bool canUngroup = _selectedOrGroupId.HasValue && _selectionBucket == importance;

            // "Group as OR" button
            var groupRect = new Rect(x, rect.y, buttonWidth, rect.height);
            GUI.enabled = canGroup;
            if (Widgets.ButtonText(groupRect, "LandingZone_Workspace_GroupAsOr".Translate(), drawBackground: true, doMouseoverSound: true, active: canGroup))
            {
                GroupSelectedAsOr();
            }
            GUI.enabled = true;
            if (canGroup)
            {
                TooltipHandler.TipRegion(groupRect, "LandingZone_Workspace_GroupAsOrTooltip".Translate(_selectedChipIds.Count));
            }

            x += buttonWidth + 4f;

            // "Ungroup" button
            var ungroupRect = new Rect(x, rect.y, buttonWidth, rect.height);
            GUI.enabled = canUngroup;
            if (Widgets.ButtonText(ungroupRect, "LandingZone_Workspace_Ungroup".Translate(), drawBackground: true, doMouseoverSound: true, active: canUngroup))
            {
                UngroupSelected();
            }
            GUI.enabled = true;
            if (canUngroup)
            {
                TooltipHandler.TipRegion(ungroupRect, "LandingZone_Workspace_UngroupTooltip".Translate());
            }

            // Selection count indicator
            if (_selectionBucket == importance && (_selectedChipIds.Count > 0 || _selectedOrGroupId.HasValue))
            {
                x += buttonWidth + 8f;
                var infoRect = new Rect(x, rect.y, rect.width - x + rect.x, rect.height);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.8f, 0.9f);
                Text.Anchor = TextAnchor.MiddleLeft;
                if (_selectedOrGroupId.HasValue)
                {
                    Widgets.Label(infoRect, "LandingZone_Workspace_OrGroupSelected".Translate());
                }
                else
                {
                    Widgets.Label(infoRect, "LandingZone_Workspace_ChipsSelected".Translate(_selectedChipIds.Count));
                }
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }
        }

        /// <summary>
        /// Draws a single filter chip. Returns the width used.
        /// </summary>
        private static float DrawSingleChip(Rect rect, BucketWorkspace.FilterChip chip, FilterImportance importance, int clauseId)
        {
            var bucket = BucketWorkspace.AllBuckets.FirstOrDefault(b => b.Importance == importance);
            var chipColor = bucket?.Color ?? Color.gray;

            // Check if this chip is selected
            bool isSelected = _selectedChipIds.Contains(chip.FilterId) && _selectionBucket == importance;

            // Check if this filter has a range or is a container
            var lookup = GetPaletteFilterLookup();
            bool hasRange = lookup.TryGetValue(chip.FilterId, out var paletteFilter) && paletteFilter.HasRange;
            bool isContainer = paletteFilter?.Kind == FilterKind.Container && paletteFilter.ContainerKind != null;
            bool isRangeEditorOpen = _rangeEditorChipId == chip.FilterId && _rangeEditorBucket == importance;
            bool isContainerPopupOpen = _containerPopupChipId == chip.FilterId && _containerPopupBucket == importance;

            // Build display label with range value or container state
            string displayLabel = chip.Label;
            var state = LandingZoneContext.State;
            if (state?.Preferences != null)
            {
                var filters = state.Preferences.GetActiveFilters();
                if (isContainer && paletteFilter != null)
                {
                    displayLabel = FormatContainerChipLabel(chip.Label, paletteFilter.ContainerKind!.Value, filters);
                }
                else if (hasRange && paletteFilter != null)
                {
                    var range = GetFilterRange(chip.FilterId, filters);
                    displayLabel = FormatChipLabelWithRange(chip.Label, range, paletteFilter);
                }
            }

            // Calculate width based on label
            float textWidth = Text.CalcSize(displayLabel).x;
            float chipWidth = textWidth + 24f;
            if (chip.IsHeavy) chipWidth += 20f;
            if (hasRange || isContainer) chipWidth += 20f; // Space for range/container indicator

            var chipRect = new Rect(rect.x, rect.y, chipWidth, ChipHeight);

            // Background - brighter if selected
            Color bgColor = isSelected
                ? new Color(chipColor.r * 0.6f, chipColor.g * 0.6f, chipColor.b * 0.6f, 0.95f)
                : new Color(chipColor.r * 0.4f, chipColor.g * 0.4f, chipColor.b * 0.4f, 0.9f);
            Widgets.DrawBoxSolid(chipRect, bgColor);

            // Border - thicker if selected
            GUI.color = chipColor;
            Widgets.DrawBox(chipRect, isSelected ? 2 : 1);
            GUI.color = Color.white;

            // Selection indicator glow
            if (isSelected)
            {
                GUI.color = new Color(chipColor.r, chipColor.g, chipColor.b, 0.3f);
                Widgets.DrawBox(chipRect.ExpandedBy(2f));
                GUI.color = Color.white;
            }

            // Content
            var contentRect = chipRect.ContractedBy(4f, 2f);

            // Heavy warning icon
            if (chip.IsHeavy)
            {
                var warningRect = new Rect(contentRect.x, contentRect.y, 16f, contentRect.height);
                GUI.color = new Color(1f, 0.7f, 0.3f);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(warningRect, "!");
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                contentRect.x += 18f;
                contentRect.width -= 18f;
            }

            // Label
            bool hasIndicator = hasRange || isContainer;
            var labelRect = hasIndicator ? new Rect(contentRect.x, contentRect.y, contentRect.width - 18f, contentRect.height) : contentRect;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, displayLabel);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // Range indicator button (slider icon) or Container indicator (dropdown arrow)
            if (hasIndicator)
            {
                var indicatorButtonRect = new Rect(contentRect.xMax - 16f, contentRect.y + 2f, 16f, contentRect.height - 4f);
                bool isOpen = isContainer ? isContainerPopupOpen : isRangeEditorOpen;

                // Highlight if popup/editor is open
                if (isOpen)
                {
                    Widgets.DrawBoxSolid(indicatorButtonRect, new Color(0.4f, 0.6f, 0.4f, 0.6f));
                }

                // Draw indicator icon: ▼ for containers, ≡ for ranges
                GUI.color = isOpen ? Color.white : new Color(0.8f, 0.8f, 0.8f);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(indicatorButtonRect, isContainer ? "▼" : "≡");
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                GUI.color = Color.white;

                string tooltipKey = isContainer ? "LandingZone_Workspace_EditContainer" : "LandingZone_Workspace_EditRange";
                TooltipHandler.TipRegion(indicatorButtonRect, tooltipKey.Translate());
            }

            // Handle mouse interactions
            Event evt = Event.current;
            if (Mouse.IsOver(chipRect))
            {
                // Mouse down - start drag or handle click
                if (evt.type == EventType.MouseDown && evt.button == 0)
                {
                    // Check if click is on indicator button (range or container)
                    if (hasIndicator)
                    {
                        var indicatorButtonRect = new Rect(contentRect.xMax - 16f, contentRect.y + 2f, 16f, contentRect.height - 4f);
                        if (indicatorButtonRect.Contains(evt.mousePosition))
                        {
                            if (isContainer)
                            {
                                // Toggle container popup
                                if (isContainerPopupOpen)
                                {
                                    _containerPopupChipId = null;
                                    _containerPopupBucket = null;
                                }
                                else
                                {
                                    _containerPopupChipId = chip.FilterId;
                                    _containerPopupBucket = importance;
                                    // Convert chip rect from scroll-content space to window space
                                    _containerPopupAnchorRect = new Rect(
                                        _bucketAreaRect.x + chipRect.x,
                                        _bucketAreaRect.y + chipRect.y - _lastBucketScrollPosition.y,
                                        chipRect.width,
                                        chipRect.height);
                                    // Close range editor if open
                                    _rangeEditorChipId = null;
                                    _rangeEditorBucket = null;
                                }
                            }
                            else
                            {
                                // Toggle range editor
                                if (isRangeEditorOpen)
                                {
                                    _rangeEditorChipId = null;
                                    _rangeEditorBucket = null;
                                }
                                else
                                {
                                    _rangeEditorChipId = chip.FilterId;
                                    _rangeEditorBucket = importance;
                                    // Convert chip rect from scroll-content space to window space
                                    _rangeEditorAnchorRect = new Rect(
                                        _bucketAreaRect.x + chipRect.x,
                                        _bucketAreaRect.y + chipRect.y - _lastBucketScrollPosition.y,
                                        chipRect.width,
                                        chipRect.height);
                                    // Close container popup if open
                                    _containerPopupChipId = null;
                                    _containerPopupBucket = null;
                                }
                            }
                            evt.Use();
                            return chipWidth;
                        }
                    }

                    // Check for drag start (need to move mouse to confirm drag)
                    _dragStartPos = evt.mousePosition;

                    // Handle selection
                    HandleChipSelection(chip.FilterId, importance, clauseId, evt);

                    evt.Use();
                }

                // Check for drag initiation (mouse moved while button held)
                if (evt.type == EventType.MouseDrag && evt.button == 0 && !_isDragging)
                {
                    float dragDistance = Vector2.Distance(_dragStartPos, evt.mousePosition);
                    if (dragDistance > 5f)
                    {
                        StartDragChip(chip.FilterId, importance, clauseId, evt.mousePosition);
                    }
                }

                // Right-click to remove
                if (evt.type == EventType.MouseDown && evt.button == 1)
                {
                    ClearChipFromFilterSettings(chip.FilterId);  // Clear from FilterSettings to prevent ghost filters
                    _workspace?.RemoveChip(chip.FilterId);
                    _selectedChipIds.Remove(chip.FilterId);
                    OnFiltersModified(); // User changed filters - clear preset tracking
                    evt.Use();
                }
            }

            // Tooltip
            string tooltip = "LandingZone_Workspace_ChipDragTooltip".Translate(chip.Label);
            if (chip.IsHeavy)
            {
                tooltip += "\n" + "LandingZone_Workspace_ChipHeavyNote".Translate();
            }
            TooltipHandler.TipRegion(chipRect, tooltip);

            return chipWidth;
        }

        /// <summary>
        /// Draws an OR group pill. Returns the width used.
        /// </summary>
        private static float DrawOrGroupChip(Rect rect, BucketWorkspace.OrGroup group, FilterImportance importance, int clauseId)
        {
            var bucket = BucketWorkspace.AllBuckets.FirstOrDefault(b => b.Importance == importance);
            var chipColor = bucket?.Color ?? Color.gray;

            // Check if this OR group is selected
            bool isSelected = _selectedOrGroupId == group.GroupId && _selectionBucket == importance;

            string label = group.GetDisplayLabel();
            float textWidth = Text.CalcSize(label).x;
            float chipWidth = textWidth + 32f; // Extra space for OR indicator

            var chipRect = new Rect(rect.x, rect.y, chipWidth, ChipHeight);

            // Background with OR indicator - brighter if selected
            Color bgColor = isSelected
                ? new Color(chipColor.r * 0.45f, chipColor.g * 0.45f, chipColor.b * 0.6f, 0.95f)
                : new Color(chipColor.r * 0.3f, chipColor.g * 0.3f, chipColor.b * 0.5f, 0.9f);
            Widgets.DrawBoxSolid(chipRect, bgColor);

            // Double border for OR groups - thicker if selected
            GUI.color = chipColor;
            Widgets.DrawBox(chipRect, isSelected ? 2 : 1);
            Widgets.DrawBox(chipRect.ContractedBy(3f), 1);
            GUI.color = Color.white;

            // Selection glow
            if (isSelected)
            {
                GUI.color = new Color(chipColor.r, chipColor.g, chipColor.b, 0.35f);
                Widgets.DrawBox(chipRect.ExpandedBy(3f));
                GUI.color = Color.white;
            }

            // "OR" indicator on the left
            var orRect = new Rect(chipRect.x + 2f, chipRect.y + 2f, 18f, chipRect.height - 4f);
            Widgets.DrawBoxSolid(orRect, new Color(0.6f, 0.4f, 0.8f, 0.8f));
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.white;
            Widgets.Label(orRect, "OR");
            Text.Anchor = TextAnchor.UpperLeft;

            // Content
            var contentRect = new Rect(chipRect.x + 22f, chipRect.y + 2f, chipRect.width - 26f, chipRect.height - 4f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(contentRect, string.Join(" | ", group.Chips.Select(c => c.Label)));
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // Handle mouse interactions
            Event evt = Event.current;
            if (Mouse.IsOver(chipRect))
            {
                // Left-click to ungroup immediately
                if (evt.type == EventType.MouseDown && evt.button == 0)
                {
                    // Ungroup the OR group back to individual chips
                    if (_workspace != null)
                    {
                        foreach (var chip in group.Chips.ToList())
                        {
                            _workspace.RemoveFromOrGroup(chip.FilterId);
                        }
                    }
                    _cachedTileEstimate = ""; // Invalidate estimate
                    // Clear selection state
                    _selectedOrGroupId = null;
                    _selectedChipIds.Clear();
                    _selectionBucket = null;
                    evt.Use();
                }

                // Right-click also ungroups (same behavior for consistency)
                if (evt.type == EventType.MouseDown && evt.button == 1)
                {
                    if (_workspace != null)
                    {
                        foreach (var chip in group.Chips.ToList())
                        {
                            _workspace.RemoveFromOrGroup(chip.FilterId);
                        }
                    }
                    _cachedTileEstimate = "";
                    _selectedOrGroupId = null;
                    _selectedChipIds.Clear();
                    _selectionBucket = null;
                    evt.Use();
                }
            }

            // Tooltip - inform user about click-to-ungroup
            TooltipHandler.TipRegion(chipRect, "LandingZone_Workspace_ClickToUngroup".Translate());

            return chipWidth;
        }

        /// <summary>
        /// Draws the logic summary at the bottom.
        /// </summary>
        private static void DrawLogicSummary(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.08f, 0.1f));
            Widgets.DrawBox(rect);

            var innerRect = rect.ContractedBy(6f);

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.8f, 0.7f);

            string summary = _workspace?.GetLogicSummary() ?? "No filters configured";
            Widgets.Label(innerRect, summary);

            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        /// <summary>
        /// Syncs workspace state from FilterSettings.
        /// </summary>
        private static void SyncWorkspaceFromSettings(FilterSettings filters)
        {
            // Map current filter importances to workspace chips
            // This creates chips for filters that have non-Ignored importance

            // Climate filters
            SyncFilterToWorkspace(filters, "average_temperature", "Avg Temp", filters.AverageTemperatureImportance, false, "Climate");
            SyncFilterToWorkspace(filters, "minimum_temperature", "Min Temp", filters.MinimumTemperatureImportance, false, "Climate");
            SyncFilterToWorkspace(filters, "maximum_temperature", "Max Temp", filters.MaximumTemperatureImportance, false, "Climate");
            SyncFilterToWorkspace(filters, "rainfall", "Rainfall", filters.RainfallImportance, false, "Climate");
            SyncFilterToWorkspace(filters, "growing_days", "Growing Days", filters.GrowingDaysImportance, true, "Climate");
            SyncFilterToWorkspace(filters, "pollution", "Pollution", filters.PollutionImportance, false, "Climate");

            // Climate mutators (MapFeature-based)
            SyncMutatorToWorkspace(filters, "mutator_sunny", "Sunny", "SunnyMutator", "Climate");
            SyncMutatorToWorkspace(filters, "mutator_foggy", "Foggy", "FoggyMutator", "Climate");
            SyncMutatorToWorkspace(filters, "mutator_windy", "Windy", "WindyMutator", "Climate");
            SyncMutatorToWorkspace(filters, "mutator_wet", "Wet Climate", "WetClimate", "Climate");
            SyncMutatorToWorkspace(filters, "mutator_pollution", "Pollution Increased", "Pollution_Increased", "Climate");

            // Geography filters - biomes
            SyncContainerToWorkspace(filters, "biomes", "Biomes", ContainerType.Biomes, "Geography");

            // Geography filters - terrain
            SyncContainerToWorkspace(filters, "hilliness", "Hilliness", ContainerType.Hilliness, "Geography");
            SyncFilterToWorkspace(filters, "elevation", "Elevation", filters.ElevationImportance, false, "Geography");
            SyncFilterToWorkspace(filters, "swampiness", "Swampiness", filters.SwampinessImportance, false, "Geography");
            SyncFilterToWorkspace(filters, "movement_difficulty", "Move Difficulty", filters.MovementDifficultyImportance, false, "Geography");

            // Geography filters - water access
            SyncFilterToWorkspace(filters, "coastal", "Ocean Coastal", filters.CoastalImportance, false, "Geography");
            SyncFilterToWorkspace(filters, "coastal_lake", "Lake Coastal", filters.CoastalLakeImportance, false, "Geography");
            SyncFilterToWorkspace(filters, "water_access", "Water Access", filters.WaterAccessImportance, false, "Geography");
            SyncContainerToWorkspace(filters, "rivers", "Rivers", ContainerType.Rivers, "Geography");
            SyncContainerToWorkspace(filters, "roads", "Roads", ContainerType.Roads, "Geography");

            // Geography mutators - water features
            SyncMutatorToWorkspace(filters, "mutator_river", "River", "River", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_river_delta", "River Delta", "RiverDelta", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_river_confluence", "Confluence", "RiverConfluence", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_river_island", "River Island", "RiverIsland", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_headwater", "Headwater", "Headwater", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_lake", "Lake", "Lake", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_lake_island", "Lake w/ Island", "LakeWithIsland", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_lake_islands", "Lake w/ Islands", "LakeWithIslands", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_lakeshore", "Lakeshore", "Lakeshore", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_pond", "Pond", "Pond", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_fjord", "Fjord", "Fjord", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_bay", "Bay", "Bay", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_coast", "Coast", "Coast", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_harbor", "Harbor", "Harbor", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_cove", "Cove", "Cove", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_peninsula", "Peninsula", "Peninsula", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_archipelago", "Archipelago", "Archipelago", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_coastal_atoll", "Coastal Atoll", "CoastalAtoll", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_coastal_island", "Coastal Island", "CoastalIsland", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_iceberg", "Iceberg", "Iceberg", "Geography");

            // Geography mutators - elevation features
            SyncMutatorToWorkspace(filters, "mutator_mountain", "Mountain", "Mountain", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_valley", "Valley", "Valley", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_basin", "Basin", "Basin", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_plateau", "Plateau", "Plateau", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_hollow", "Hollow", "Hollow", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_caves", "Caves", "Caves", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_cavern", "Cavern", "Cavern", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_cave_lakes", "Cave Lakes", "CaveLakes", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_lava_caves", "Lava Caves", "LavaCaves", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_cliffs", "Cliffs", "Cliffs", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_chasm", "Chasm", "Chasm", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_crevasse", "Crevasse", "Crevasse", "Geography");

            // Geography mutators - ground conditions
            SyncMutatorToWorkspace(filters, "mutator_sandy", "Sandy", "Sandy", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_muddy", "Muddy", "Muddy", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_marshy", "Marshy", "Marshy", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_wetland", "Wetland", "Wetland", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_dunes", "Dunes", "Dunes", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_oasis", "Oasis", "Oasis", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_dry_ground", "Dry Ground", "DryGround", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_dry_lake", "Dry Lake", "DryLake", "Geography");

            // Geography mutators - special/hazards
            SyncMutatorToWorkspace(filters, "mutator_toxic_lake", "Toxic Lake", "ToxicLake", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_lava_flow", "Lava Flow", "LavaFlow", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_lava_crater", "Lava Crater", "LavaCrater", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_lava_lake", "Lava Lake", "LavaLake", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_hot_springs", "Hot Springs", "HotSprings", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_mixed_biome", "Mixed Biome", "MixedBiome", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_obsidian", "Obsidian Deposits", "ObsidianDeposits", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_ice_caves", "Ice Caves", "IceCaves", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_ice_dunes", "Ice Dunes", "IceDunes", "Geography");

            // Geography mutators - Geological Landforms mod (GL_)
            SyncMutatorToWorkspace(filters, "mutator_gl_atoll", "Atoll", "GL_Atoll", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_gl_island", "Island", "GL_Island", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_gl_skerry", "Skerry", "GL_Skerry", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_gl_tombolo", "Tombolo", "GL_Tombolo", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_gl_landbridge", "Land Bridge", "GL_Landbridge", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_gl_cove_island", "Cove with Island", "GL_CoveWithIsland", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_gl_secluded_cove", "Secluded Cove", "GL_SecludedCove", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_gl_river_source", "River Source", "GL_RiverSource", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_gl_canyon", "Canyon", "GL_Canyon", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_gl_gorge", "Gorge", "GL_Gorge", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_gl_rift", "Rift", "GL_Rift", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_gl_caldera", "Caldera", "GL_Caldera", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_gl_crater", "Crater", "GL_Crater", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_gl_cirque", "Cirque", "GL_Cirque", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_gl_glacier", "Glacier", "GL_Glacier", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_gl_lone_mountain", "Lone Mountain", "GL_LoneMountain", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_gl_secluded_valley", "Secluded Valley", "GL_SecludedValley", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_gl_sinkhole", "Sinkhole", "GL_Sinkhole", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_gl_cave_entrance", "Cave Entrance", "GL_CaveEntrance", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_gl_surface_cave", "Surface Cave", "GL_SurfaceCave", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_gl_biome_transitions", "Biome Transitions", "GL_BiomeTransitions", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_gl_badlands", "Badlands", "GL_Badlands", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_gl_desert_plateau", "Desert Plateau", "GL_DesertPlateau", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_gl_swamp_hill", "Swamp Hill", "GL_SwampHill", "Geography");

            // Geography mutators - Alpha Biomes mod (AB_)
            SyncMutatorToWorkspace(filters, "mutator_ab_tar_lakes", "Tar Lakes", "AB_TarLakes", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_ab_propane_lakes", "Propane Lakes", "AB_PropaneLakes", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_ab_quicksand", "Quicksand Pits", "AB_QuicksandPits", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_ab_magma_vents", "Magma Vents", "AB_MagmaVents", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_ab_magmatic_quagmire", "Magmatic Quagmire", "AB_MagmaticQuagmire", "Geography");
            SyncMutatorToWorkspace(filters, "mutator_ab_sterile_ground", "Sterile Ground", "AB_SterileGround", "Geography");

            // Resources filters
            SyncContainerToWorkspace(filters, "stones", "Natural Stones", ContainerType.Stones, "Resources");
            SyncFilterToWorkspace(filters, "forageability", "Forageability", filters.ForageImportance, false, "Resources");
            SyncFilterToWorkspace(filters, "graze", "Grazeable", filters.GrazeImportance, true, "Resources");
            SyncFilterToWorkspace(filters, "animal_density", "Animal Density", filters.AnimalDensityImportance, false, "Resources");
            SyncFilterToWorkspace(filters, "fish_population", "Fish Population", filters.FishPopulationImportance, false, "Resources");
            SyncFilterToWorkspace(filters, "plant_density", "Plant Density", filters.PlantDensityImportance, false, "Resources");
            SyncContainerToWorkspace(filters, "plant_grove", "Plant Grove", ContainerType.PlantGrove, "Resources");
            SyncContainerToWorkspace(filters, "animal_habitat", "Animal Habitat", ContainerType.AnimalHabitat, "Resources");
            SyncContainerToWorkspace(filters, "mineral_ores", "Mineral Rich", ContainerType.MineralRich, "Resources");

            // Resource mutators (previously missing - caused Fertile Soil and others to not show in workspace)
            SyncMutatorToWorkspace(filters, "mutator_fertile", "Fertile Soil", "Fertile", "Resources");
            SyncMutatorToWorkspace(filters, "mutator_steam_geysers", "Steam Geysers+", "SteamGeysers_Increased", "Resources");
            SyncMutatorToWorkspace(filters, "mutator_animal_life_up", "Animal Life+", "AnimalLife_Increased", "Resources");
            SyncMutatorToWorkspace(filters, "mutator_animal_life_down", "Animal Life-", "AnimalLife_Decreased", "Resources");
            SyncMutatorToWorkspace(filters, "mutator_plant_life_up", "Plant Life+", "PlantLife_Increased", "Resources");
            SyncMutatorToWorkspace(filters, "mutator_plant_life_down", "Plant Life-", "PlantLife_Decreased", "Resources");
            SyncMutatorToWorkspace(filters, "mutator_fish_up", "Fish+", "Fish_Increased", "Resources");
            SyncMutatorToWorkspace(filters, "mutator_fish_down", "Fish-", "Fish_Decreased", "Resources");
            SyncMutatorToWorkspace(filters, "mutator_wild_plants", "Wild Plants", "WildPlants", "Resources");
            SyncMutatorToWorkspace(filters, "mutator_wild_tropical", "Wild Tropical Plants", "WildTropicalPlants", "Resources");
            SyncMutatorToWorkspace(filters, "mutator_archean_trees", "Archean Trees", "ArcheanTrees", "Resources");

            // Alpha Biomes mod mutators
            SyncMutatorToWorkspace(filters, "mutator_ab_golden_trees", "Golden Trees", "AB_GoldenTrees", "Resources");
            SyncMutatorToWorkspace(filters, "mutator_ab_luminescent_trees", "Luminescent Trees", "AB_LuminescentTrees", "Resources");
            SyncMutatorToWorkspace(filters, "mutator_ab_techno_trees", "Techno Trees", "AB_TechnoTrees", "Resources");
            SyncMutatorToWorkspace(filters, "mutator_ab_flesh_trees", "Flesh Trees", "AB_FleshTrees", "Resources");
            SyncMutatorToWorkspace(filters, "mutator_ab_healing_springs", "Healing Springs", "AB_HealingSprings", "Resources");
            SyncMutatorToWorkspace(filters, "mutator_ab_mutagenic_springs", "Mutagenic Springs", "AB_MutagenicSprings", "Resources");
            SyncMutatorToWorkspace(filters, "mutator_ab_geothermal_hotspots", "Geothermal Hotspots", "AB_GeothermalHotspots", "Resources");

            // Features filters
            SyncContainerToWorkspace(filters, "stockpiles", "Stockpiles", ContainerType.Stockpiles, "Features");
            SyncFilterToWorkspace(filters, "landmark", "Landmarks", filters.LandmarkImportance, false, "Features");

            // Features mutators - Salvage
            SyncMutatorToWorkspace(filters, "mutator_junkyard", "Junkyard", "Junkyard", "Features");
            SyncMutatorToWorkspace(filters, "mutator_ancient_ruins", "Ancient Ruins", "AncientRuins", "Features");
            SyncMutatorToWorkspace(filters, "mutator_ancient_ruins_frozen", "Ancient Ruins (Frozen)", "AncientRuins_Frozen", "Features");
            SyncMutatorToWorkspace(filters, "mutator_ancient_warehouse", "Ancient Warehouse", "AncientWarehouse", "Features");

            // Features mutators - Structures
            SyncMutatorToWorkspace(filters, "mutator_ancient_garrison", "Ancient Garrison", "AncientGarrison", "Features");
            SyncMutatorToWorkspace(filters, "mutator_ancient_quarry", "Ancient Quarry", "AncientQuarry", "Features");
            SyncMutatorToWorkspace(filters, "mutator_ancient_refinery", "Chemfuel Refinery", "AncientChemfuelRefinery", "Features");
            SyncMutatorToWorkspace(filters, "mutator_ancient_launch_site", "Launch Site", "AncientLaunchSite", "Features");
            SyncMutatorToWorkspace(filters, "mutator_ancient_uplink", "Ancient Uplink", "AncientUplink", "Features");

            // Features mutators - Settlements
            SyncMutatorToWorkspace(filters, "mutator_abandoned_outlander", "Abandoned (Outlander)", "AbandonedColonyOutlander", "Features");
            SyncMutatorToWorkspace(filters, "mutator_abandoned_tribal", "Abandoned (Tribal)", "AbandonedColonyTribal", "Features");
            SyncMutatorToWorkspace(filters, "mutator_ancient_infested", "Infested Settlement", "AncientInfestedSettlement", "Features");

            // Features mutators - Environmental
            SyncMutatorToWorkspace(filters, "mutator_ancient_heat_vent", "Heat Vent", "AncientHeatVent", "Features");
            SyncMutatorToWorkspace(filters, "mutator_ancient_smoke_vent", "Smoke Vent", "AncientSmokeVent", "Features");
            SyncMutatorToWorkspace(filters, "mutator_ancient_tox_vent", "Toxic Vent", "AncientToxVent", "Features");
            SyncMutatorToWorkspace(filters, "mutator_terraforming_scar", "Terraforming Scar", "TerraformingScar", "Features");
            SyncMutatorToWorkspace(filters, "mutator_insect_megahive", "Insect Megahive", "InsectMegahive", "Features");

            // Features mutators - Alpha Biomes mod
            SyncMutatorToWorkspace(filters, "mutator_ab_giant_fossils", "Giant Fossils", "AB_GiantFossils", "Features");
            SyncMutatorToWorkspace(filters, "mutator_ab_derelict_archonexus", "Derelict Archonexus", "AB_DerelictArchonexus", "Features");
            SyncMutatorToWorkspace(filters, "mutator_ab_derelict_clusters", "Derelict Clusters", "AB_DerelictClusters", "Features");
        }

        private static void SyncFilterToWorkspace(FilterSettings filters, string id, string label, FilterImportance importance, bool isHeavy, string category)
        {
            if (_workspace == null) return;

            bool inWorkspace = _workspace.ContainsChip(id);
            var currentImportance = _workspace.GetChipImportance(id);

            if (importance != FilterImportance.Ignored)
            {
                if (!inWorkspace)
                {
                    var chip = new BucketWorkspace.FilterChip(id, label, isHeavy, category);
                    _workspace.AddChip(chip, importance);
                }
                else if (currentImportance != importance)
                {
                    _workspace.MoveChip(id, importance);
                }
            }
            else if (inWorkspace)
            {
                _workspace.RemoveChip(id);
            }
        }

        /// <summary>
        /// Syncs a mutator filter from MapFeatures container to workspace.
        /// </summary>
        private static void SyncMutatorToWorkspace(FilterSettings filters, string chipId, string label, string mutatorDefName, string category)
        {
            if (_workspace == null) return;

            var importance = filters.MapFeatures.GetImportance(mutatorDefName);
            bool inWorkspace = _workspace.ContainsChip(chipId);
            var currentImportance = _workspace.GetChipImportance(chipId);

            if (importance != FilterImportance.Ignored)
            {
                if (!inWorkspace)
                {
                    var chip = new BucketWorkspace.FilterChip(chipId, label, false, category);
                    _workspace.AddChip(chip, importance);
                }
                else if (currentImportance != importance)
                {
                    _workspace.MoveChip(chipId, importance);
                }
            }
            else if (inWorkspace)
            {
                _workspace.RemoveChip(chipId);
            }
        }

        /// <summary>
        /// Syncs a container filter to the workspace.
        /// Container chips appear if the underlying data has any configuration.
        /// The chip's bucket position is determined by the highest importance in the container.
        /// </summary>
        private static void SyncContainerToWorkspace(FilterSettings filters, string chipId, string label, ContainerType containerType, string category)
        {
            if (_workspace == null) return;

            bool hasConfig = containerType switch
            {
                // Rivers: Show chip if any river has non-Ignored importance
                ContainerType.Rivers => filters.Rivers.HasAnyImportance,
                // Roads: Show chip if any road has non-Ignored importance
                ContainerType.Roads => filters.Roads.HasAnyImportance,
                // Hilliness: Show chip if selection differs from "all types" (less than 4 selected)
                ContainerType.Hilliness => filters.AllowedHilliness.Count < 4,
                // Stones: Show chip if any stone has non-Ignored importance
                ContainerType.Stones => filters.Stones.HasAnyImportance,
                // Stockpiles: Show chip if any stockpile type has non-Ignored importance
                ContainerType.Stockpiles => filters.Stockpiles.HasAnyImportance,
                // PlantGrove: Show chip if any plant has non-Ignored importance
                ContainerType.PlantGrove => filters.PlantGrove.HasAnyImportance,
                // AnimalHabitat: Show chip if any animal has non-Ignored importance
                ContainerType.AnimalHabitat => filters.AnimalHabitat.HasAnyImportance,
                // MineralRich: Show chip if any mineral ore has non-Ignored importance
                ContainerType.MineralRich => filters.MineralOres.HasAnyImportance,
                // Biomes: Show chip if any biome has non-Ignored importance
                ContainerType.Biomes => filters.Biomes.HasAnyImportance,
                _ => false
            };

            bool inWorkspace = _workspace.ContainsChip(chipId);

            if (hasConfig)
            {
                // Get the highest importance from the container to determine bucket placement
                var importance = GetHighestContainerImportance(containerType, filters);

                if (!inWorkspace)
                {
                    var chip = new BucketWorkspace.FilterChip(chipId, label, false, category);
                    _workspace.AddChip(chip, importance);
                }
                else
                {
                    // Update bucket if importance changed
                    var currentImportance = _workspace.GetChipImportance(chipId);
                    if (currentImportance != importance)
                    {
                        _workspace.MoveChip(chipId, importance);
                    }
                }
            }
            else if (inWorkspace)
            {
                _workspace.RemoveChip(chipId);
            }
        }

        /// <summary>
        /// Gets the highest importance level from any item in a container.
        /// Priority order: MustHave > MustNotHave > Priority > Preferred
        /// </summary>
        private static FilterImportance GetHighestContainerImportance(ContainerType containerType, FilterSettings filters)
        {
            // For Hilliness, we use MustHave since restricting hilliness is a hard requirement
            if (containerType == ContainerType.Hilliness)
            {
                return FilterImportance.MustHave;
            }

            // Get the container based on type
            var container = containerType switch
            {
                ContainerType.Rivers => filters.Rivers,
                ContainerType.Roads => filters.Roads,
                ContainerType.Stones => filters.Stones,
                ContainerType.Stockpiles => filters.Stockpiles,
                ContainerType.PlantGrove => filters.PlantGrove,
                ContainerType.AnimalHabitat => filters.AnimalHabitat,
                ContainerType.MineralRich => filters.MineralOres,
                ContainerType.Biomes => filters.Biomes,
                _ => null
            };

            if (container == null)
                return FilterImportance.Preferred;

            // Check for importance levels in priority order
            if (container.HasMustHave)
                return FilterImportance.MustHave;
            if (container.HasMustNotHave)
                return FilterImportance.MustNotHave;
            if (container.HasPriority)
                return FilterImportance.Priority;
            if (container.HasPreferred)
                return FilterImportance.Preferred;

            return FilterImportance.Preferred;
        }

        /// <summary>
        /// Maps container chip IDs to their ContainerType for reverse lookup.
        /// </summary>
        private static readonly Dictionary<string, ContainerType> ContainerChipMapping = new()
        {
            { "biomes", ContainerType.Biomes },
            { "hilliness", ContainerType.Hilliness },
            { "rivers", ContainerType.Rivers },   // Must match PaletteFilter ID "rivers" (not "river")
            { "roads", ContainerType.Roads },     // Must match PaletteFilter ID "roads" (not "road")
            { "stones", ContainerType.Stones },   // Must match PaletteFilter ID "stones"
            { "plant_grove", ContainerType.PlantGrove },
            { "animal_habitat", ContainerType.AnimalHabitat },
            { "mineral_ores", ContainerType.MineralRich },
            { "stockpiles", ContainerType.Stockpiles }  // Must match PaletteFilter ID
        };

        /// <summary>
        /// Gets all available item defNames for a container type.
        /// Used when syncing container chips that have no items explicitly configured.
        /// </summary>
        private static IEnumerable<string> GetAllContainerItemDefNames(ContainerType containerType)
        {
            return containerType switch
            {
                ContainerType.Rivers => Filtering.Filters.RiverFilter.GetAllRiverTypes().Select(r => r.defName),
                ContainerType.Roads => GetRoadTypesWithMod().Select(r => r.defName),
                ContainerType.Stones => GetStoneTypesWithMod().Select(s => s.defName),
                ContainerType.Stockpiles => GetStockpileTypes().Select(s => s.defName),
                ContainerType.PlantGrove => GetPlantGroveTypesWithMod().Select(p => p.defName),
                ContainerType.AnimalHabitat => GetAnimalHabitatTypesWithMod().Select(a => a.defName),
                ContainerType.MineralRich => GetMineralOreTypesWithMod().Select(m => m.defName),
                ContainerType.Biomes => GetBiomeTypesWithMod().Select(b => b.defName),
                _ => Enumerable.Empty<string>()
            };
        }

        /// <summary>
        /// Syncs container chip importance to the underlying FilterSettings.
        /// When a container chip (Rivers, Stones, Biomes, etc.) is dragged to a new bucket,
        /// this updates ALL configured items in that container to the new importance.
        ///
        /// KEY FIX: If the container has NO items configured (showing "(None)"), we now
        /// interpret this as "apply bucket importance to ALL items". This matches user intent
        /// when dragging an empty container chip - they want "any river" or "any stone" etc.
        /// to have MustHave/MustNotHave importance.
        /// </summary>
        private static void SyncContainerChipToFilterSettings(string chipId, FilterImportance newImportance)
        {
            if (!ContainerChipMapping.TryGetValue(chipId, out var containerType))
                return;  // Not a container chip, nothing to sync

            var filters = LandingZoneContext.State?.Preferences?.GetActiveFilters();
            if (filters == null) return;

            // Special handling for Hilliness (HashSet-based, not IndividualImportanceContainer)
            if (containerType == ContainerType.Hilliness)
            {
                // When dragged to MustHave/MustNotHave with all 4 types, show notice
                // Having all 4 types means "no filter" which defeats the purpose
                if ((newImportance == FilterImportance.MustHave || newImportance == FilterImportance.MustNotHave)
                    && filters.AllowedHilliness.Count == 4)
                {
                    Messages.Message("LandingZone_Hilliness_AllSelected".Translate(), MessageTypeDefOf.RejectInput, false);
                }
                return;
            }

            // Get the container and update all non-Ignored items
            var container = containerType switch
            {
                ContainerType.Rivers => filters.Rivers,
                ContainerType.Roads => filters.Roads,
                ContainerType.Stones => filters.Stones,
                ContainerType.Stockpiles => filters.Stockpiles,
                ContainerType.PlantGrove => filters.PlantGrove,
                ContainerType.AnimalHabitat => filters.AnimalHabitat,
                ContainerType.MineralRich => filters.MineralOres,
                ContainerType.Biomes => filters.Biomes,
                _ => null
            };

            if (container == null) return;

            // Collect items to update (all items with non-Ignored importance)
            var itemsToUpdate = container.ItemImportance
                .Where(kvp => kvp.Value != FilterImportance.Ignored)
                .Select(kvp => kvp.Key)
                .ToList();

            // KEY FIX: If no items are configured yet, sync ALL available items
            // This handles the case where user drags a container chip showing "(None)" to a bucket
            // User intent: "I want any river/stone/etc to be required" (or excluded)
            if (itemsToUpdate.Count == 0)
            {
                itemsToUpdate = GetAllContainerItemDefNames(containerType).ToList();
                Log.Message($"[LandingZone] Container '{chipId}' has no items configured - syncing ALL {itemsToUpdate.Count} available items to {newImportance}");
            }

            if (itemsToUpdate.Count == 0) return;  // No items available at all (shouldn't happen)

            // Apply the new importance to all items
            foreach (var item in itemsToUpdate)
            {
                container.SetImportance(item, newImportance);
            }

            Log.Message($"[LandingZone] Container '{chipId}' moved to {newImportance}: updated {itemsToUpdate.Count} item(s)");
        }

        /// <summary>
        /// Clears a chip's filter from FilterSettings when the chip is removed from workspace.
        /// This ensures FilterSettings stays in sync with visible workspace state.
        /// Fixes bug where right-click chip removal left "ghost filters" still active.
        /// </summary>
        private static void ClearChipFromFilterSettings(string chipId)
        {
            var filters = LandingZoneContext.State?.Preferences?.GetActiveFilters();
            if (filters == null) return;

            // Check if this is a container chip (Rivers, Roads, Stones, etc.)
            if (ContainerChipMapping.TryGetValue(chipId, out var containerType))
            {
                ClearContainerFilter(filters, containerType);
                return;
            }

            // Check if this is a mutator chip via PaletteFilter lookup
            var lookup = GetPaletteFilterLookup();
            if (lookup.TryGetValue(chipId, out var paletteFilter) &&
                paletteFilter.Kind == FilterKind.Mutator &&
                !string.IsNullOrEmpty(paletteFilter.MutatorDefName))
            {
                filters.MapFeatures.SetImportance(paletteFilter.MutatorDefName!, FilterImportance.Ignored);
                return;
            }

            // Simple filter - set importance to Ignored
            switch (chipId)
            {
                // Climate
                case "average_temperature": filters.AverageTemperatureImportance = FilterImportance.Ignored; break;
                case "minimum_temperature": filters.MinimumTemperatureImportance = FilterImportance.Ignored; break;
                case "maximum_temperature": filters.MaximumTemperatureImportance = FilterImportance.Ignored; break;
                case "rainfall": filters.RainfallImportance = FilterImportance.Ignored; break;
                case "growing_days": filters.GrowingDaysImportance = FilterImportance.Ignored; break;
                case "pollution": filters.PollutionImportance = FilterImportance.Ignored; break;

                // Geography - terrain
                case "elevation": filters.ElevationImportance = FilterImportance.Ignored; break;
                case "swampiness": filters.SwampinessImportance = FilterImportance.Ignored; break;
                case "movement_difficulty": filters.MovementDifficultyImportance = FilterImportance.Ignored; break;

                // Geography - water access
                case "coastal": filters.CoastalImportance = FilterImportance.Ignored; break;
                case "coastal_lake": filters.CoastalLakeImportance = FilterImportance.Ignored; break;
                case "water_access": filters.WaterAccessImportance = FilterImportance.Ignored; break;

                // Resources
                case "forageability": filters.ForageImportance = FilterImportance.Ignored; break;
                case "graze": filters.GrazeImportance = FilterImportance.Ignored; break;
                case "animal_density": filters.AnimalDensityImportance = FilterImportance.Ignored; break;
                case "fish_population": filters.FishPopulationImportance = FilterImportance.Ignored; break;
                case "plant_density": filters.PlantDensityImportance = FilterImportance.Ignored; break;

                // Features
                case "landmark": filters.LandmarkImportance = FilterImportance.Ignored; break;
                case "adjacent_biomes": filters.AdjacentBiomes.ItemImportance.Clear(); break;
            }
        }

        /// <summary>
        /// Clears all items in a container filter to Ignored.
        /// </summary>
        private static void ClearContainerFilter(FilterSettings filters, ContainerType containerType)
        {
            // Special handling for Hilliness (HashSet-based, not IndividualImportanceContainer)
            if (containerType == ContainerType.Hilliness)
            {
                filters.AllowedHilliness.Clear();
                return;
            }

            var container = containerType switch
            {
                ContainerType.Rivers => filters.Rivers,
                ContainerType.Roads => filters.Roads,
                ContainerType.Stones => filters.Stones,
                ContainerType.Stockpiles => filters.Stockpiles,
                ContainerType.PlantGrove => filters.PlantGrove,
                ContainerType.AnimalHabitat => filters.AnimalHabitat,
                ContainerType.MineralRich => filters.MineralOres,
                ContainerType.Biomes => filters.Biomes,
                _ => null
            };

            // IndividualImportanceContainer treats missing items as Ignored
            // So clearing the dictionary effectively sets all items to Ignored
            container?.ItemImportance.Clear();
        }

        /// <summary>
        /// Syncs FilterSettings from workspace state.
        /// </summary>
        private static void SyncSettingsFromWorkspace(FilterSettings filters)
        {
            if (_workspace == null) return;

            // Climate filters
            filters.AverageTemperatureImportance = _workspace.GetChipImportance("average_temperature") ?? FilterImportance.Ignored;
            filters.MinimumTemperatureImportance = _workspace.GetChipImportance("minimum_temperature") ?? FilterImportance.Ignored;
            filters.MaximumTemperatureImportance = _workspace.GetChipImportance("maximum_temperature") ?? FilterImportance.Ignored;
            filters.RainfallImportance = _workspace.GetChipImportance("rainfall") ?? FilterImportance.Ignored;
            filters.GrowingDaysImportance = _workspace.GetChipImportance("growing_days") ?? FilterImportance.Ignored;
            filters.PollutionImportance = _workspace.GetChipImportance("pollution") ?? FilterImportance.Ignored;

            // Climate mutators (MapFeature-based)
            SyncMutatorFromWorkspace(filters, "mutator_sunny", "SunnyMutator");
            SyncMutatorFromWorkspace(filters, "mutator_foggy", "FoggyMutator");
            SyncMutatorFromWorkspace(filters, "mutator_windy", "WindyMutator");
            SyncMutatorFromWorkspace(filters, "mutator_wet", "WetClimate");
            SyncMutatorFromWorkspace(filters, "mutator_pollution", "Pollution_Increased");

            // Geography filters - terrain
            // Note: hilliness and rivers containers are synced via popup (direct FilterSettings modification)
            filters.ElevationImportance = _workspace.GetChipImportance("elevation") ?? FilterImportance.Ignored;
            filters.SwampinessImportance = _workspace.GetChipImportance("swampiness") ?? FilterImportance.Ignored;
            filters.MovementDifficultyImportance = _workspace.GetChipImportance("movement_difficulty") ?? FilterImportance.Ignored;

            // Geography filters - water access
            filters.CoastalImportance = _workspace.GetChipImportance("coastal") ?? FilterImportance.Ignored;
            filters.CoastalLakeImportance = _workspace.GetChipImportance("coastal_lake") ?? FilterImportance.Ignored;
            filters.WaterAccessImportance = _workspace.GetChipImportance("water_access") ?? FilterImportance.Ignored;

            // Geography mutators - water features
            SyncMutatorFromWorkspace(filters, "mutator_river", "River");
            SyncMutatorFromWorkspace(filters, "mutator_river_delta", "RiverDelta");
            SyncMutatorFromWorkspace(filters, "mutator_river_confluence", "RiverConfluence");
            SyncMutatorFromWorkspace(filters, "mutator_river_island", "RiverIsland");
            SyncMutatorFromWorkspace(filters, "mutator_headwater", "Headwater");
            SyncMutatorFromWorkspace(filters, "mutator_lake", "Lake");
            SyncMutatorFromWorkspace(filters, "mutator_lake_island", "LakeWithIsland");
            SyncMutatorFromWorkspace(filters, "mutator_lake_islands", "LakeWithIslands");
            SyncMutatorFromWorkspace(filters, "mutator_lakeshore", "Lakeshore");
            SyncMutatorFromWorkspace(filters, "mutator_pond", "Pond");
            SyncMutatorFromWorkspace(filters, "mutator_fjord", "Fjord");
            SyncMutatorFromWorkspace(filters, "mutator_bay", "Bay");
            SyncMutatorFromWorkspace(filters, "mutator_coast", "Coast");
            SyncMutatorFromWorkspace(filters, "mutator_harbor", "Harbor");
            SyncMutatorFromWorkspace(filters, "mutator_cove", "Cove");
            SyncMutatorFromWorkspace(filters, "mutator_peninsula", "Peninsula");
            SyncMutatorFromWorkspace(filters, "mutator_archipelago", "Archipelago");
            SyncMutatorFromWorkspace(filters, "mutator_coastal_atoll", "CoastalAtoll");
            SyncMutatorFromWorkspace(filters, "mutator_coastal_island", "CoastalIsland");
            SyncMutatorFromWorkspace(filters, "mutator_iceberg", "Iceberg");

            // Geography mutators - elevation features
            SyncMutatorFromWorkspace(filters, "mutator_mountain", "Mountain");
            SyncMutatorFromWorkspace(filters, "mutator_valley", "Valley");
            SyncMutatorFromWorkspace(filters, "mutator_basin", "Basin");
            SyncMutatorFromWorkspace(filters, "mutator_plateau", "Plateau");
            SyncMutatorFromWorkspace(filters, "mutator_hollow", "Hollow");
            SyncMutatorFromWorkspace(filters, "mutator_caves", "Caves");
            SyncMutatorFromWorkspace(filters, "mutator_cavern", "Cavern");
            SyncMutatorFromWorkspace(filters, "mutator_cave_lakes", "CaveLakes");
            SyncMutatorFromWorkspace(filters, "mutator_lava_caves", "LavaCaves");
            SyncMutatorFromWorkspace(filters, "mutator_cliffs", "Cliffs");
            SyncMutatorFromWorkspace(filters, "mutator_chasm", "Chasm");
            SyncMutatorFromWorkspace(filters, "mutator_crevasse", "Crevasse");

            // Geography mutators - ground conditions
            SyncMutatorFromWorkspace(filters, "mutator_sandy", "Sandy");
            SyncMutatorFromWorkspace(filters, "mutator_muddy", "Muddy");
            SyncMutatorFromWorkspace(filters, "mutator_marshy", "Marshy");
            SyncMutatorFromWorkspace(filters, "mutator_wetland", "Wetland");
            SyncMutatorFromWorkspace(filters, "mutator_dunes", "Dunes");
            SyncMutatorFromWorkspace(filters, "mutator_oasis", "Oasis");
            SyncMutatorFromWorkspace(filters, "mutator_dry_ground", "DryGround");
            SyncMutatorFromWorkspace(filters, "mutator_dry_lake", "DryLake");

            // Geography mutators - special/hazards
            SyncMutatorFromWorkspace(filters, "mutator_toxic_lake", "ToxicLake");
            SyncMutatorFromWorkspace(filters, "mutator_lava_flow", "LavaFlow");
            SyncMutatorFromWorkspace(filters, "mutator_lava_crater", "LavaCrater");
            SyncMutatorFromWorkspace(filters, "mutator_lava_lake", "LavaLake");
            SyncMutatorFromWorkspace(filters, "mutator_hot_springs", "HotSprings");
            SyncMutatorFromWorkspace(filters, "mutator_mixed_biome", "MixedBiome");
            SyncMutatorFromWorkspace(filters, "mutator_obsidian", "ObsidianDeposits");
            SyncMutatorFromWorkspace(filters, "mutator_ice_caves", "IceCaves");
            SyncMutatorFromWorkspace(filters, "mutator_ice_dunes", "IceDunes");

            // Geography mutators - Geological Landforms mod (GL_)
            SyncMutatorFromWorkspace(filters, "mutator_gl_atoll", "GL_Atoll");
            SyncMutatorFromWorkspace(filters, "mutator_gl_island", "GL_Island");
            SyncMutatorFromWorkspace(filters, "mutator_gl_skerry", "GL_Skerry");
            SyncMutatorFromWorkspace(filters, "mutator_gl_tombolo", "GL_Tombolo");
            SyncMutatorFromWorkspace(filters, "mutator_gl_landbridge", "GL_Landbridge");
            SyncMutatorFromWorkspace(filters, "mutator_gl_cove_island", "GL_CoveWithIsland");
            SyncMutatorFromWorkspace(filters, "mutator_gl_secluded_cove", "GL_SecludedCove");
            SyncMutatorFromWorkspace(filters, "mutator_gl_river_source", "GL_RiverSource");
            SyncMutatorFromWorkspace(filters, "mutator_gl_canyon", "GL_Canyon");
            SyncMutatorFromWorkspace(filters, "mutator_gl_gorge", "GL_Gorge");
            SyncMutatorFromWorkspace(filters, "mutator_gl_rift", "GL_Rift");
            SyncMutatorFromWorkspace(filters, "mutator_gl_caldera", "GL_Caldera");
            SyncMutatorFromWorkspace(filters, "mutator_gl_crater", "GL_Crater");
            SyncMutatorFromWorkspace(filters, "mutator_gl_cirque", "GL_Cirque");
            SyncMutatorFromWorkspace(filters, "mutator_gl_glacier", "GL_Glacier");
            SyncMutatorFromWorkspace(filters, "mutator_gl_lone_mountain", "GL_LoneMountain");
            SyncMutatorFromWorkspace(filters, "mutator_gl_secluded_valley", "GL_SecludedValley");
            SyncMutatorFromWorkspace(filters, "mutator_gl_sinkhole", "GL_Sinkhole");
            SyncMutatorFromWorkspace(filters, "mutator_gl_cave_entrance", "GL_CaveEntrance");
            SyncMutatorFromWorkspace(filters, "mutator_gl_surface_cave", "GL_SurfaceCave");
            SyncMutatorFromWorkspace(filters, "mutator_gl_biome_transitions", "GL_BiomeTransitions");
            SyncMutatorFromWorkspace(filters, "mutator_gl_badlands", "GL_Badlands");
            SyncMutatorFromWorkspace(filters, "mutator_gl_desert_plateau", "GL_DesertPlateau");
            SyncMutatorFromWorkspace(filters, "mutator_gl_swamp_hill", "GL_SwampHill");

            // Geography mutators - Alpha Biomes mod (AB_)
            SyncMutatorFromWorkspace(filters, "mutator_ab_tar_lakes", "AB_TarLakes");
            SyncMutatorFromWorkspace(filters, "mutator_ab_propane_lakes", "AB_PropaneLakes");
            SyncMutatorFromWorkspace(filters, "mutator_ab_quicksand", "AB_QuicksandPits");
            SyncMutatorFromWorkspace(filters, "mutator_ab_magma_vents", "AB_MagmaVents");
            SyncMutatorFromWorkspace(filters, "mutator_ab_magmatic_quagmire", "AB_MagmaticQuagmire");
            SyncMutatorFromWorkspace(filters, "mutator_ab_sterile_ground", "AB_SterileGround");

            // Resources filters
            filters.ForageImportance = _workspace.GetChipImportance("forageability") ?? FilterImportance.Ignored;
            filters.GrazeImportance = _workspace.GetChipImportance("graze") ?? FilterImportance.Ignored;
            filters.AnimalDensityImportance = _workspace.GetChipImportance("animal_density") ?? FilterImportance.Ignored;
            filters.FishPopulationImportance = _workspace.GetChipImportance("fish_population") ?? FilterImportance.Ignored;
            filters.PlantDensityImportance = _workspace.GetChipImportance("plant_density") ?? FilterImportance.Ignored;

            // Resource mutators (previously missing - caused Fertile Soil and others to not sync)
            SyncMutatorFromWorkspace(filters, "mutator_fertile", "Fertile");
            SyncMutatorFromWorkspace(filters, "mutator_steam_geysers", "SteamGeysers_Increased");
            SyncMutatorFromWorkspace(filters, "mutator_animal_life_up", "AnimalLife_Increased");
            SyncMutatorFromWorkspace(filters, "mutator_animal_life_down", "AnimalLife_Decreased");
            SyncMutatorFromWorkspace(filters, "mutator_plant_life_up", "PlantLife_Increased");
            SyncMutatorFromWorkspace(filters, "mutator_plant_life_down", "PlantLife_Decreased");
            SyncMutatorFromWorkspace(filters, "mutator_fish_up", "Fish_Increased");
            SyncMutatorFromWorkspace(filters, "mutator_fish_down", "Fish_Decreased");
            SyncMutatorFromWorkspace(filters, "mutator_wild_plants", "WildPlants");
            SyncMutatorFromWorkspace(filters, "mutator_wild_tropical", "WildTropicalPlants");
            SyncMutatorFromWorkspace(filters, "mutator_archean_trees", "ArcheanTrees");

            // Alpha Biomes mod mutators
            SyncMutatorFromWorkspace(filters, "mutator_ab_golden_trees", "AB_GoldenTrees");
            SyncMutatorFromWorkspace(filters, "mutator_ab_luminescent_trees", "AB_LuminescentTrees");
            SyncMutatorFromWorkspace(filters, "mutator_ab_techno_trees", "AB_TechnoTrees");
            SyncMutatorFromWorkspace(filters, "mutator_ab_flesh_trees", "AB_FleshTrees");
            SyncMutatorFromWorkspace(filters, "mutator_ab_healing_springs", "AB_HealingSprings");
            SyncMutatorFromWorkspace(filters, "mutator_ab_mutagenic_springs", "AB_MutagenicSprings");
            SyncMutatorFromWorkspace(filters, "mutator_ab_geothermal_hotspots", "AB_GeothermalHotspots");

            // Features filters
            filters.LandmarkImportance = _workspace.GetChipImportance("landmark") ?? FilterImportance.Ignored;

            // Features mutators - Salvage
            SyncMutatorFromWorkspace(filters, "mutator_junkyard", "Junkyard");
            SyncMutatorFromWorkspace(filters, "mutator_ancient_ruins", "AncientRuins");
            SyncMutatorFromWorkspace(filters, "mutator_ancient_ruins_frozen", "AncientRuins_Frozen");
            SyncMutatorFromWorkspace(filters, "mutator_ancient_warehouse", "AncientWarehouse");

            // Features mutators - Structures
            SyncMutatorFromWorkspace(filters, "mutator_ancient_garrison", "AncientGarrison");
            SyncMutatorFromWorkspace(filters, "mutator_ancient_quarry", "AncientQuarry");
            SyncMutatorFromWorkspace(filters, "mutator_ancient_refinery", "AncientChemfuelRefinery");
            SyncMutatorFromWorkspace(filters, "mutator_ancient_launch_site", "AncientLaunchSite");
            SyncMutatorFromWorkspace(filters, "mutator_ancient_uplink", "AncientUplink");

            // Features mutators - Settlements
            SyncMutatorFromWorkspace(filters, "mutator_abandoned_outlander", "AbandonedColonyOutlander");
            SyncMutatorFromWorkspace(filters, "mutator_abandoned_tribal", "AbandonedColonyTribal");
            SyncMutatorFromWorkspace(filters, "mutator_ancient_infested", "AncientInfestedSettlement");

            // Features mutators - Environmental
            SyncMutatorFromWorkspace(filters, "mutator_ancient_heat_vent", "AncientHeatVent");
            SyncMutatorFromWorkspace(filters, "mutator_ancient_smoke_vent", "AncientSmokeVent");
            SyncMutatorFromWorkspace(filters, "mutator_ancient_tox_vent", "AncientToxVent");
            SyncMutatorFromWorkspace(filters, "mutator_terraforming_scar", "TerraformingScar");
            SyncMutatorFromWorkspace(filters, "mutator_insect_megahive", "InsectMegahive");

            // Features mutators - Alpha Biomes mod
            SyncMutatorFromWorkspace(filters, "mutator_ab_giant_fossils", "AB_GiantFossils");
            SyncMutatorFromWorkspace(filters, "mutator_ab_derelict_archonexus", "AB_DerelictArchonexus");
            SyncMutatorFromWorkspace(filters, "mutator_ab_derelict_clusters", "AB_DerelictClusters");
        }

        /// <summary>
        /// Syncs a mutator from workspace back to MapFeatures container.
        /// </summary>
        private static void SyncMutatorFromWorkspace(FilterSettings filters, string chipId, string mutatorDefName)
        {
            if (_workspace == null) return;

            var importance = _workspace.GetChipImportance(chipId) ?? FilterImportance.Ignored;
            filters.MapFeatures.SetImportance(mutatorDefName, importance);
        }

        // ============================================================================
        // RANGE EDITOR SUPPORT
        // ============================================================================

        /// <summary>
        /// Gets or builds the palette filter lookup cache.
        /// </summary>
        private static Dictionary<string, PaletteFilter> GetPaletteFilterLookup()
        {
            if (_paletteFilterLookup != null) return _paletteFilterLookup;

            _paletteFilterLookup = new Dictionary<string, PaletteFilter>();
            foreach (var filter in GetClimatePaletteFilters())
                _paletteFilterLookup[filter.Id] = filter;
            foreach (var filter in GetGeographyNaturalPaletteFilters())
                _paletteFilterLookup[filter.Id] = filter;
            foreach (var filter in GetGeographyResourcesPaletteFilters())
                _paletteFilterLookup[filter.Id] = filter;
            foreach (var filter in GetGeographyArtificialPaletteFilters())
                _paletteFilterLookup[filter.Id] = filter;
            foreach (var filter in GetModFiltersPaletteFilters())
                _paletteFilterLookup[filter.Id] = filter;

            return _paletteFilterLookup;
        }

        /// <summary>
        /// Formats a chip label to include the current range value.
        /// Examples: "Growing Days (35-60)", "Rainfall (1000-2200)", "Avg Temp (10-32°C)"
        /// </summary>
        private static string FormatChipLabelWithRange(string baseLabel, FloatRange range, PaletteFilter filter)
        {
            string rangeStr;

            // Special handling for Growing Days - show "Full-Year" for 60
            if (filter.Id == "growing_days")
            {
                string minStr = range.min >= 60 ? "Full" : $"{range.min:F0}";
                string maxStr = range.max >= 60 ? "Full" : $"{range.max:F0}";

                if (minStr == maxStr)
                    rangeStr = minStr == "Full" ? "(Full-Year)" : $"({minStr})";
                else
                    rangeStr = $"({minStr}-{maxStr})";
            }
            // Percentage values (0-1 range shown as %)
            else if (filter.Unit == "%")
            {
                int minPct = (int)(range.min * 100);
                int maxPct = (int)(range.max * 100);

                if (minPct == maxPct)
                    rangeStr = $"({minPct}%)";
                else
                    rangeStr = $"({minPct}-{maxPct}%)";
            }
            // Temperature with unit
            else if (filter.Unit == "°C")
            {
                if (Mathf.Approximately(range.min, range.max))
                    rangeStr = $"({range.min:F0}°)";
                else
                    rangeStr = $"({range.min:F0}-{range.max:F0}°)";
            }
            // Other numeric ranges
            else
            {
                if (Mathf.Approximately(range.min, range.max))
                    rangeStr = $"({range.min:F0})";
                else
                    rangeStr = $"({range.min:F0}-{range.max:F0})";
            }

            return $"{baseLabel} {rangeStr}";
        }

        /// <summary>
        /// Gets the current range value from FilterSettings for a given filter ID.
        /// </summary>
        private static FloatRange GetFilterRange(string filterId, FilterSettings filters)
        {
            return filterId switch
            {
                "average_temperature" => filters.AverageTemperatureRange,
                "minimum_temperature" => filters.MinimumTemperatureRange,
                "maximum_temperature" => filters.MaximumTemperatureRange,
                "rainfall" => filters.RainfallRange,
                "growing_days" => filters.GrowingDaysRange,
                "pollution" => filters.PollutionRange,
                "elevation" => filters.ElevationRange,
                "movement_difficulty" => filters.MovementDifficultyRange,
                "swampiness" => filters.SwampinessRange,
                "forageability" => filters.ForageabilityRange,
                "animal_density" => filters.AnimalDensityRange,
                "fish_population" => filters.FishPopulationRange,
                "plant_density" => filters.PlantDensityRange,
                _ => new FloatRange(0f, 1f)
            };
        }

        /// <summary>
        /// Sets the range value in FilterSettings for a given filter ID.
        /// </summary>
        private static void SetFilterRange(string filterId, FilterSettings filters, FloatRange range)
        {
            switch (filterId)
            {
                case "average_temperature": filters.AverageTemperatureRange = range; break;
                case "minimum_temperature": filters.MinimumTemperatureRange = range; break;
                case "maximum_temperature": filters.MaximumTemperatureRange = range; break;
                case "rainfall": filters.RainfallRange = range; break;
                case "growing_days": filters.GrowingDaysRange = range; break;
                case "pollution": filters.PollutionRange = range; break;
                case "elevation": filters.ElevationRange = range; break;
                case "movement_difficulty": filters.MovementDifficultyRange = range; break;
                case "swampiness": filters.SwampinessRange = range; break;
                case "forageability": filters.ForageabilityRange = range; break;
                case "animal_density": filters.AnimalDensityRange = range; break;
                case "fish_population": filters.FishPopulationRange = range; break;
                case "plant_density": filters.PlantDensityRange = range; break;
            }

            OnFiltersModified(); // User changed filters - clear preset tracking
        }

        /// <summary>
        /// Draws the inline range editor for a chip with a range-based filter.
        /// Returns the height used by the editor.
        /// </summary>
        private static float DrawInlineRangeEditor(Rect rect, string filterId, FilterSettings filters)
        {
            var lookup = GetPaletteFilterLookup();
            if (!lookup.TryGetValue(filterId, out var paletteFilter) || !paletteFilter.HasRange)
                return 0f;

            // Background
            var editorRect = new Rect(rect.x, rect.y, rect.width, RangeEditorHeight);
            Widgets.DrawBoxSolid(editorRect, new Color(0.15f, 0.15f, 0.2f, 0.95f));

            var innerRect = editorRect.ContractedBy(4f);

            // Get current range
            var currentRange = GetFilterRange(filterId, filters);

            // Label with current values
            var labelRect = new Rect(innerRect.x, innerRect.y, innerRect.width, 16f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;

            string rangeLabel;
            if (paletteFilter.RangeKind == RangeType.Discrete && filterId == "growing_days")
            {
                // Special label for Growing Days - show "Full-Year" for 60
                string minLabel = currentRange.min >= 60 ? "Full-Year" : $"{currentRange.min:F0}";
                string maxLabel = currentRange.max >= 60 ? "Full-Year" : $"{currentRange.max:F0}";
                rangeLabel = $"{paletteFilter.Label}: {minLabel} - {maxLabel} {paletteFilter.Unit ?? ""}";
            }
            else if (paletteFilter.Unit == "%")
            {
                rangeLabel = $"{paletteFilter.Label}: {currentRange.min * 100:F0}% - {currentRange.max * 100:F0}%";
            }
            else
            {
                rangeLabel = $"{paletteFilter.Label}: {currentRange.min:F0} - {currentRange.max:F0} {paletteFilter.Unit ?? ""}";
            }
            Widgets.Label(labelRect, rangeLabel);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // Range slider area
            var sliderRect = new Rect(innerRect.x, innerRect.y + 18f, innerRect.width - 30f, 24f);

            // Draw min/max range slider
            float minVal = currentRange.min;
            float maxVal = currentRange.max;

            if (paletteFilter.RangeKind == RangeType.Discrete && paletteFilter.DiscreteSteps != null)
            {
                // Discrete slider using snap-to-step
                DrawDiscreteRangeSlider(sliderRect, ref minVal, ref maxVal, paletteFilter);
            }
            else
            {
                // Continuous min-max slider
                DrawContinuousRangeSlider(sliderRect, ref minVal, ref maxVal, paletteFilter);
            }

            // Apply changes if values changed
            if (!Mathf.Approximately(minVal, currentRange.min) || !Mathf.Approximately(maxVal, currentRange.max))
            {
                SetFilterRange(filterId, filters, new FloatRange(minVal, maxVal));
            }

            // Close button
            var closeRect = new Rect(innerRect.xMax - 22f, innerRect.y, 22f, 22f);
            if (Widgets.ButtonText(closeRect, "×", drawBackground: false))
            {
                _rangeEditorChipId = null;
                _rangeEditorBucket = null;
            }

            return RangeEditorHeight;
        }

        /// <summary>
        /// Draws a continuous min-max range slider.
        /// </summary>
        private static void DrawContinuousRangeSlider(Rect rect, ref float minVal, ref float maxVal, PaletteFilter filter)
        {
            // Draw track background
            var trackRect = new Rect(rect.x + 4f, rect.y + 10f, rect.width - 8f, 4f);
            Widgets.DrawBoxSolid(trackRect, new Color(0.3f, 0.3f, 0.3f));

            // Calculate positions
            float range = filter.RangeMax - filter.RangeMin;
            float minPos = (minVal - filter.RangeMin) / range;
            float maxPos = (maxVal - filter.RangeMin) / range;

            // Draw selected range
            var selectedRect = new Rect(
                trackRect.x + trackRect.width * minPos,
                trackRect.y,
                trackRect.width * (maxPos - minPos),
                trackRect.height);
            Widgets.DrawBoxSolid(selectedRect, new Color(0.4f, 0.7f, 0.4f));

            // Min handle
            var minHandleRect = new Rect(trackRect.x + trackRect.width * minPos - 6f, rect.y + 4f, 12f, 16f);
            Widgets.DrawBoxSolid(minHandleRect, new Color(0.6f, 0.8f, 0.6f));
            Widgets.DrawBox(minHandleRect);

            // Max handle
            var maxHandleRect = new Rect(trackRect.x + trackRect.width * maxPos - 6f, rect.y + 4f, 12f, 16f);
            Widgets.DrawBoxSolid(maxHandleRect, new Color(0.6f, 0.8f, 0.6f));
            Widgets.DrawBox(maxHandleRect);

            // Handle dragging
            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                if (minHandleRect.Contains(evt.mousePosition))
                {
                    _draggedHandle = "min";
                    evt.Use();
                }
                else if (maxHandleRect.Contains(evt.mousePosition))
                {
                    _draggedHandle = "max";
                    evt.Use();
                }
                else if (trackRect.Contains(evt.mousePosition))
                {
                    // Click on track - move nearest handle
                    float clickPos = (evt.mousePosition.x - trackRect.x) / trackRect.width;
                    float clickVal = filter.RangeMin + clickPos * range;

                    if (Mathf.Abs(clickVal - minVal) < Mathf.Abs(clickVal - maxVal))
                    {
                        minVal = Mathf.Clamp(clickVal, filter.RangeMin, maxVal - 0.01f);
                        _draggedHandle = "min";
                    }
                    else
                    {
                        maxVal = Mathf.Clamp(clickVal, minVal + 0.01f, filter.RangeMax);
                        _draggedHandle = "max";
                    }
                    evt.Use();
                }
            }
            else if (evt.type == EventType.MouseDrag && _draggedHandle != null)
            {
                float dragPos = (evt.mousePosition.x - trackRect.x) / trackRect.width;
                float dragVal = filter.RangeMin + Mathf.Clamp01(dragPos) * range;

                if (_draggedHandle == "min")
                {
                    minVal = Mathf.Clamp(dragVal, filter.RangeMin, maxVal - 0.01f);
                }
                else if (_draggedHandle == "max")
                {
                    maxVal = Mathf.Clamp(dragVal, minVal + 0.01f, filter.RangeMax);
                }
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp)
            {
                _draggedHandle = null;
            }
        }

        /// <summary>
        /// Draws a discrete range slider with snap-to-step behavior.
        /// </summary>
        private static void DrawDiscreteRangeSlider(Rect rect, ref float minVal, ref float maxVal, PaletteFilter filter)
        {
            if (filter.DiscreteSteps == null || filter.DiscreteSteps.Length == 0) return;

            var steps = filter.DiscreteSteps;
            int stepCount = steps.Length;

            // Draw track background
            var trackRect = new Rect(rect.x + 4f, rect.y + 10f, rect.width - 8f, 4f);
            Widgets.DrawBoxSolid(trackRect, new Color(0.3f, 0.3f, 0.3f));

            // Draw step markers
            for (int i = 0; i < stepCount; i++)
            {
                float pos = (float)i / (stepCount - 1);
                var markerRect = new Rect(trackRect.x + trackRect.width * pos - 1f, trackRect.y - 2f, 2f, 8f);
                Widgets.DrawBoxSolid(markerRect, new Color(0.5f, 0.5f, 0.5f));
            }

            // Find current step indices
            int minStepIdx = FindNearestStepIndex(minVal, steps);
            int maxStepIdx = FindNearestStepIndex(maxVal, steps);

            float minPos = (float)minStepIdx / (stepCount - 1);
            float maxPos = (float)maxStepIdx / (stepCount - 1);

            // Draw selected range
            var selectedRect = new Rect(
                trackRect.x + trackRect.width * minPos,
                trackRect.y,
                trackRect.width * (maxPos - minPos),
                trackRect.height);
            Widgets.DrawBoxSolid(selectedRect, new Color(0.4f, 0.7f, 0.4f));

            // Min handle
            var minHandleRect = new Rect(trackRect.x + trackRect.width * minPos - 6f, rect.y + 4f, 12f, 16f);
            Widgets.DrawBoxSolid(minHandleRect, new Color(0.6f, 0.8f, 0.6f));
            Widgets.DrawBox(minHandleRect);

            // Max handle
            var maxHandleRect = new Rect(trackRect.x + trackRect.width * maxPos - 6f, rect.y + 4f, 12f, 16f);
            Widgets.DrawBoxSolid(maxHandleRect, new Color(0.6f, 0.8f, 0.6f));
            Widgets.DrawBox(maxHandleRect);

            // Handle dragging
            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                if (minHandleRect.Contains(evt.mousePosition))
                {
                    _draggedHandle = "min";
                    evt.Use();
                }
                else if (maxHandleRect.Contains(evt.mousePosition))
                {
                    _draggedHandle = "max";
                    evt.Use();
                }
                else if (trackRect.Contains(evt.mousePosition))
                {
                    float clickPos = (evt.mousePosition.x - trackRect.x) / trackRect.width;
                    int clickIdx = Mathf.RoundToInt(clickPos * (stepCount - 1));

                    if (Mathf.Abs(clickIdx - minStepIdx) < Mathf.Abs(clickIdx - maxStepIdx))
                    {
                        minStepIdx = Mathf.Clamp(clickIdx, 0, maxStepIdx);
                        minVal = steps[minStepIdx];
                        _draggedHandle = "min";
                    }
                    else
                    {
                        maxStepIdx = Mathf.Clamp(clickIdx, minStepIdx, stepCount - 1);
                        maxVal = steps[maxStepIdx];
                        _draggedHandle = "max";
                    }
                    evt.Use();
                }
            }
            else if (evt.type == EventType.MouseDrag && _draggedHandle != null)
            {
                float dragPos = (evt.mousePosition.x - trackRect.x) / trackRect.width;
                int dragIdx = Mathf.RoundToInt(Mathf.Clamp01(dragPos) * (stepCount - 1));

                if (_draggedHandle == "min")
                {
                    minStepIdx = Mathf.Clamp(dragIdx, 0, maxStepIdx);
                    minVal = steps[minStepIdx];
                }
                else if (_draggedHandle == "max")
                {
                    maxStepIdx = Mathf.Clamp(dragIdx, minStepIdx, stepCount - 1);
                    maxVal = steps[maxStepIdx];
                }
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp)
            {
                _draggedHandle = null;
            }
        }

        /// <summary>
        /// Finds the nearest step index for a given value.
        /// </summary>
        private static int FindNearestStepIndex(float value, float[] steps)
        {
            int nearest = 0;
            float minDist = float.MaxValue;

            for (int i = 0; i < steps.Length; i++)
            {
                float dist = Mathf.Abs(steps[i] - value);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = i;
                }
            }

            return nearest;
        }

        // Drag handle tracking for range slider
        private static string? _draggedHandle;

        // ============================================================================
        // RANGE EDITOR POPUP SUPPORT
        // ============================================================================

        /// <summary>
        /// Draws the range editor popup as an overlay (on top of all other elements).
        /// </summary>
        private static void DrawRangeEditorPopup(FilterSettings filters)
        {
            if (_rangeEditorChipId == null || _rangeEditorBucket == null) return;

            var lookup = GetPaletteFilterLookup();
            if (!lookup.TryGetValue(_rangeEditorChipId, out var paletteFilter) || !paletteFilter.HasRange)
                return;

            // Calculate popup position below the chip
            float popupWidth = 280f;
            float popupHeight = RangeEditorHeight + 10f;

            var popupRect = new Rect(
                _rangeEditorAnchorRect.x,
                _rangeEditorAnchorRect.yMax + 2f,
                popupWidth,
                popupHeight);

            // Ensure popup stays within screen bounds
            if (popupRect.xMax > Verse.UI.screenWidth - 10f)
            {
                popupRect.x = Verse.UI.screenWidth - 10f - popupWidth;
            }
            if (popupRect.yMax > Verse.UI.screenHeight - 10f)
            {
                // Show above the chip instead
                popupRect.y = _rangeEditorAnchorRect.y - popupHeight - 2f;
            }

            // Draw popup background
            Widgets.DrawBoxSolid(popupRect, new Color(0.1f, 0.1f, 0.12f, 0.98f));
            Widgets.DrawBox(popupRect);

            var innerRect = popupRect.ContractedBy(5f);
            DrawInlineRangeEditor(innerRect, _rangeEditorChipId, filters);

            // Handle click outside popup to close
            // NOTE: We do NOT use GUI.Button as click-blocker for range popups because
            // it would consume slider drag events. The slider controls need mouse events.
            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && !popupRect.Contains(evt.mousePosition))
            {
                _rangeEditorChipId = null;
                _rangeEditorBucket = null;
                evt.Use();
            }
        }

        // ============================================================================
        // CONTAINER POPUP SUPPORT
        // ============================================================================

        /// <summary>
        /// Draws the container popup for Rivers or Hilliness selection.
        /// </summary>
        private static void DrawContainerPopup(FilterSettings filters)
        {
            if (_containerPopupChipId == null || _containerPopupBucket == null) return;

            var lookup = GetPaletteFilterLookup();
            if (!lookup.TryGetValue(_containerPopupChipId, out var paletteFilter) ||
                paletteFilter.Kind != FilterKind.Container ||
                paletteFilter.ContainerKind == null) return;

            var containerType = paletteFilter.ContainerKind.Value;

            // Calculate popup height accounting for mod section headers
            float popupHeight = containerType switch
            {
                ContainerType.Rivers => CalculateRiversPopupHeight(),
                ContainerType.Roads => CalculateModGroupedPopupHeight(GetRoadTypesWithMod()),
                ContainerType.Hilliness => 4 * ContainerPopupItemHeight + 40f, // 4 hilliness + buttons (no mod grouping)
                ContainerType.Stones => CalculateModGroupedPopupHeight(GetStoneTypesWithMod()),
                ContainerType.Stockpiles => GetStockpileTypes().Count * ContainerPopupItemHeight + 40f, // stockpiles are hardcoded
                ContainerType.PlantGrove => Mathf.Max(CalculateModGroupedPopupHeight(GetPlantGroveTypesWithMod()), 60f),
                ContainerType.AnimalHabitat => Mathf.Max(CalculateModGroupedPopupHeight(GetAnimalHabitatTypesWithMod()), 60f),
                ContainerType.MineralRich => Mathf.Max(CalculateModGroupedPopupHeight(GetMineralOreTypesWithMod()), 60f),
                ContainerType.Biomes => CalculateModGroupedPopupHeight(GetBiomeTypesWithMod()),
                _ => 100f
            };

            // Calculate available screen space and clamp popup height
            const float screenMargin = 10f;
            const float headerHeight = 26f; // Header + close button area (outside scroll)
            float contentHeight = popupHeight;
            float availableBelow = Verse.UI.screenHeight - _containerPopupAnchorRect.yMax - 2f - screenMargin;
            float availableAbove = _containerPopupAnchorRect.y - screenMargin;

            // Determine if popup should appear above or below anchor
            bool flipAbove = availableBelow < 200f && availableAbove > availableBelow;
            float maxAvailableHeight = flipAbove ? availableAbove : availableBelow;

            // Clamp popup height to available space (minimum 150 to be usable)
            float clampedHeight = Mathf.Clamp(popupHeight, 100f, maxAvailableHeight);
            bool needsScroll = popupHeight > clampedHeight;

            // Reset scroll position when popup changes
            if (_containerPopupChipId != _lastContainerPopupChipId)
            {
                _containerPopupScrollPosition = Vector2.zero;
                _lastContainerPopupChipId = _containerPopupChipId;
            }

            var popupRect = new Rect(
                _containerPopupAnchorRect.x,
                flipAbove ? _containerPopupAnchorRect.y - clampedHeight - 2f : _containerPopupAnchorRect.yMax + 2f,
                ContainerPopupWidth,
                clampedHeight);

            // Clamp horizontal position to screen bounds
            if (popupRect.xMax > Verse.UI.screenWidth - screenMargin)
            {
                popupRect.x = Verse.UI.screenWidth - screenMargin - popupRect.width;
            }
            if (popupRect.x < screenMargin)
            {
                popupRect.x = screenMargin;
            }

            // Draw popup background with click-blocker
            // Draw an invisible button first to consume mouse events and prevent click-through
            if (GUI.Button(popupRect, "", GUI.skin.label))
            {
                // Button clicked on popup background - do nothing but consume the event
            }
            Widgets.DrawBoxSolid(popupRect, new Color(0.12f, 0.12f, 0.15f, 0.98f));
            Widgets.DrawBox(popupRect);

            var innerRect = popupRect.ContractedBy(6f);
            float y = innerRect.y;

            // Header with close button (always visible, outside scroll area)
            var headerRect = new Rect(innerRect.x, y, innerRect.width - 24f, 18f);
            var closeRect = new Rect(innerRect.xMax - 20f, y, 20f, 18f);

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.Label(headerRect, paletteFilter.Label);
            GUI.color = Color.white;

            if (Widgets.ButtonText(closeRect, "×", drawBackground: false))
            {
                _containerPopupChipId = null;
                _containerPopupBucket = null;
            }
            y += 20f;

            // Scroll view for content if needed
            float scrollAreaHeight = innerRect.height - headerHeight;
            var scrollOuterRect = new Rect(innerRect.x, y, innerRect.width, scrollAreaHeight);
            var scrollViewRect = new Rect(0f, 0f, innerRect.width - (needsScroll ? 16f : 0f), contentHeight - headerHeight);

            if (needsScroll)
            {
                Widgets.BeginScrollView(scrollOuterRect, ref _containerPopupScrollPosition, scrollViewRect);
                y = 0f; // Reset y for scroll view local coordinates
            }
            float contentX = needsScroll ? 0f : innerRect.x;
            float contentWidth = needsScroll ? scrollViewRect.width : innerRect.width;

            // Draw checkboxes based on container type (use scroll-aware coordinates)
            if (containerType == ContainerType.Rivers)
            {
                y = DrawRiversPopupContent(contentX, y, contentWidth, filters);
            }
            else if (containerType == ContainerType.Roads)
            {
                y = DrawRoadsPopupContent(contentX, y, contentWidth, filters);
            }
            else if (containerType == ContainerType.Hilliness)
            {
                y = DrawHillinessPopupContent(contentX, y, contentWidth, filters);
            }
            else if (containerType == ContainerType.Stones)
            {
                y = DrawStonesPopupContent(contentX, y, contentWidth, filters);
            }
            else if (containerType == ContainerType.Stockpiles)
            {
                y = DrawStockpilesPopupContent(contentX, y, contentWidth, filters);
            }
            else if (containerType == ContainerType.PlantGrove)
            {
                y = DrawPlantGrovePopupContent(contentX, y, contentWidth, filters);
            }
            else if (containerType == ContainerType.AnimalHabitat)
            {
                y = DrawAnimalHabitatPopupContent(contentX, y, contentWidth, filters);
            }
            else if (containerType == ContainerType.MineralRich)
            {
                y = DrawMineralOresPopupContent(contentX, y, contentWidth, filters);
            }
            else if (containerType == ContainerType.Biomes)
            {
                y = DrawBiomesPopupContent(contentX, y, contentWidth, filters);
            }

            // End scroll view if we started one
            if (needsScroll)
            {
                Widgets.EndScrollView();
            }

            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // Handle click outside popup to close
            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && !popupRect.Contains(evt.mousePosition))
            {
                _containerPopupChipId = null;
                _containerPopupBucket = null;
                evt.Use();
            }
        }

        /// <summary>
        /// Draws the detailed tooltip popup for a filter.
        /// </summary>
        private static void DrawDetailedTooltipPopup()
        {
            if (_detailedTooltipFilterId == null) return;

            var lookup = GetPaletteFilterLookup();
            if (!lookup.TryGetValue(_detailedTooltipFilterId, out var paletteFilter) ||
                string.IsNullOrEmpty(paletteFilter.TooltipDetailed)) return;

            // Get translated tooltip text with DLC info prefix
            string detailedText = "";
            if (!string.IsNullOrEmpty(paletteFilter.RequiredDLC))
            {
                detailedText = $"[{paletteFilter.RequiredDLC}]\n\n";
            }
            detailedText += paletteFilter.TooltipDetailed!.Translate();

            // Calculate popup size based on text
            Text.Font = GameFont.Tiny;
            float textWidth = 220f;
            float textHeight = Text.CalcHeight(detailedText, textWidth);
            float popupWidth = textWidth + 16f;
            float popupHeight = textHeight + 32f; // Header + padding

            // Position popup to the right of the anchor, or left if not enough space
            var popupRect = new Rect(
                _detailedTooltipAnchorRect.xMax + 4f,
                _detailedTooltipAnchorRect.y - 10f,
                popupWidth,
                popupHeight);

            // Clamp to screen bounds (all four edges)
            if (popupRect.xMax > Verse.UI.screenWidth)
            {
                popupRect.x = _detailedTooltipAnchorRect.x - popupWidth - 4f;
            }
            if (popupRect.x < 0)
            {
                popupRect.x = 4f;
            }
            if (popupRect.yMax > Verse.UI.screenHeight)
            {
                popupRect.y = Verse.UI.screenHeight - popupHeight - 10f;
            }
            if (popupRect.y < 0)
            {
                popupRect.y = 4f;
            }

            // Draw popup background
            Widgets.DrawBoxSolid(popupRect, new Color(0.1f, 0.1f, 0.13f, 0.98f));
            Widgets.DrawBox(popupRect);

            var innerRect = popupRect.ContractedBy(6f);
            float y = innerRect.y;

            // Header with filter name and close button
            var headerRect = new Rect(innerRect.x, y, innerRect.width - 18f, 16f);
            var closeRect = new Rect(innerRect.xMax - 14f, y, 14f, 16f);

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.9f, 0.9f, 0.9f);
            Widgets.Label(headerRect, paletteFilter.Label);
            GUI.color = Color.white;

            // Close button
            if (Widgets.ButtonText(closeRect, "×", drawBackground: false))
            {
                _detailedTooltipFilterId = null;
            }
            y += 18f;

            // Detailed text
            var textRect = new Rect(innerRect.x, y, textWidth, textHeight);
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.Label(textRect, detailedText);
            GUI.color = Color.white;

            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // Handle mouse events for popup
            Event evt = Event.current;
            if (evt.type == EventType.MouseDown)
            {
                if (!popupRect.Contains(evt.mousePosition))
                {
                    // Click outside popup - close it
                    _detailedTooltipFilterId = null;
                    evt.Use();
                }
                else if (!closeRect.Contains(evt.mousePosition))
                {
                    // Click inside popup but not on close button - consume event to prevent click-through
                    evt.Use();
                }
                // Click on close button is handled by Widgets.ButtonText above
            }
        }

        /// <summary>
        /// Draws the Rivers popup content (checkboxes, OR-only mode).
        /// </summary>
        private static float DrawRiversPopupContent(float x, float y, float width, FilterSettings filters)
        {
            // Get river types dynamically and group by mod
            var riverTypes = Filtering.Filters.RiverFilter.GetAllRiverTypes().ToList();
            var groupedRivers = riverTypes
                .Select(r => (def: r, modName: GetModDisplayName(r)))
                .GroupBy(t => t.modName)
                .OrderBy(g => GetModSortOrder(g.Key))
                .ThenBy(g => g.Key)
                .Select(g => (g.Key, g.Select(t => t.def).ToList()))
                .ToList();

            // Ensure Rivers is always OR (a tile can only have one river, AND makes no sense)
            filters.Rivers.Operator = Data.ImportanceOperator.OR;

            // Note: Rivers are OR-only (a tile can only have one river, AND makes no sense)
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            var noteRect = new Rect(x, y, width, 14f);
            Widgets.Label(noteRect, "Mode: Any selected (OR)");
            GUI.color = Color.white;
            y += 16f;

            foreach (var (modName, rivers) in groupedRivers)
            {
                // Draw mod section header
                y = DrawModSectionHeader(x, y, width, modName);

                foreach (var riverDef in rivers)
                {
                    var itemRect = new Rect(x, y, width, ContainerPopupItemHeight - 2f);
                    var importance = filters.Rivers.GetImportance(riverDef.defName);
                    bool isSelected = importance != FilterImportance.Ignored;

                    // Checkbox
                    bool newSelected = isSelected;
                    Widgets.CheckboxLabeled(itemRect, GenText.ToTitleCaseSmart(riverDef.label), ref newSelected);

                    if (newSelected != isSelected)
                    {
                        // When toggling, use the bucket's importance level
                        var bucketImportance = _containerPopupBucket ?? FilterImportance.Preferred;
                        filters.Rivers.SetImportance(riverDef.defName,
                            newSelected ? bucketImportance : FilterImportance.Ignored);
                        _cachedTileEstimate = ""; // Invalidate estimate
                    }

                    y += ContainerPopupItemHeight;
                }
            }

            y += 4f;

            // Select All / Clear buttons
            var selectAllRect = new Rect(x, y, width / 2 - 2f, 20f);
            var clearRect = new Rect(x + width / 2 + 2f, y, width / 2 - 2f, 20f);

            if (Widgets.ButtonText(selectAllRect, "All"))
            {
                var bucketImportance = _containerPopupBucket ?? FilterImportance.Preferred;
                foreach (var riverDef in riverTypes)
                {
                    filters.Rivers.SetImportance(riverDef.defName, bucketImportance);
                }
                _cachedTileEstimate = "";
            }

            if (Widgets.ButtonText(clearRect, "None"))
            {
                foreach (var riverDef in riverTypes)
                {
                    filters.Rivers.SetImportance(riverDef.defName, FilterImportance.Ignored);
                }
                _cachedTileEstimate = "";
            }

            return y + 24f;
        }

        /// <summary>
        /// Draws the Roads popup content (checkboxes, OR-only mode).
        /// </summary>
        private static float DrawRoadsPopupContent(float x, float y, float width, FilterSettings filters)
        {
            var roadTypes = GetRoadTypesWithMod();
            var groupedRoads = GroupByMod(roadTypes, t => t.modName);

            // Ensure Roads is always OR (tiles can connect to multiple roads but we want any match)
            filters.Roads.Operator = Data.ImportanceOperator.OR;

            // Note: Roads are OR-only (any selected road type qualifies)
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            var noteRect = new Rect(x, y, width, 14f);
            Widgets.Label(noteRect, "Mode: Any selected (OR)");
            GUI.color = Color.white;
            y += 16f;

            foreach (var (modName, items) in groupedRoads)
            {
                // Draw mod section header
                y = DrawModSectionHeader(x, y, width, modName);

                foreach (var (defName, label, _) in items)
                {
                    var itemRect = new Rect(x, y, width, ContainerPopupItemHeight - 2f);
                    var importance = filters.Roads.GetImportance(defName);
                    bool isSelected = importance != FilterImportance.Ignored;

                    bool newSelected = isSelected;
                    Widgets.CheckboxLabeled(itemRect, label, ref newSelected);

                    if (newSelected != isSelected)
                    {
                        var bucketImportance = _containerPopupBucket ?? FilterImportance.Preferred;
                        filters.Roads.SetImportance(defName, newSelected ? bucketImportance : FilterImportance.Ignored);
                        _cachedTileEstimate = "";
                    }

                    y += ContainerPopupItemHeight;
                }
            }

            y += 4f;

            // Select All / Clear buttons
            var selectAllRect = new Rect(x, y, width / 2 - 2f, 20f);
            var clearRect = new Rect(x + width / 2 + 2f, y, width / 2 - 2f, 20f);

            if (Widgets.ButtonText(selectAllRect, "All"))
            {
                var bucketImportance = _containerPopupBucket ?? FilterImportance.Preferred;
                foreach (var (defName, _, _) in roadTypes)
                {
                    filters.Roads.SetImportance(defName, bucketImportance);
                }
                _cachedTileEstimate = "";
            }

            if (Widgets.ButtonText(clearRect, "None"))
            {
                foreach (var (defName, _, _) in roadTypes)
                {
                    filters.Roads.SetImportance(defName, FilterImportance.Ignored);
                }
                _cachedTileEstimate = "";
            }

            return y + 24f;
        }

        /// <summary>
        /// Draws the Hilliness popup content (checkboxes).
        /// </summary>
        private static float DrawHillinessPopupContent(float x, float y, float width, FilterSettings filters)
        {
            var hillinessTypes = new[] {
                (RimWorld.Planet.Hilliness.Flat, "Flat"),
                (RimWorld.Planet.Hilliness.SmallHills, "Small Hills"),
                (RimWorld.Planet.Hilliness.LargeHills, "Large Hills"),
                (RimWorld.Planet.Hilliness.Mountainous, "Mountainous")
            };

            Text.Font = GameFont.Tiny;
            foreach (var (hilliness, label) in hillinessTypes)
            {
                var itemRect = new Rect(x, y, width, ContainerPopupItemHeight - 2f);
                bool isSelected = filters.AllowedHilliness.Contains(hilliness);

                bool newSelected = isSelected;
                Widgets.CheckboxLabeled(itemRect, label, ref newSelected);

                if (newSelected != isSelected)
                {
                    if (newSelected)
                        filters.AllowedHilliness.Add(hilliness);
                    else
                        filters.AllowedHilliness.Remove(hilliness);
                    _cachedTileEstimate = "";
                }

                y += ContainerPopupItemHeight;
            }

            y += 4f;

            // Select All / Clear buttons
            var selectAllRect = new Rect(x, y, width / 2 - 2f, 20f);
            var clearRect = new Rect(x + width / 2 + 2f, y, width / 2 - 2f, 20f);

            if (Widgets.ButtonText(selectAllRect, "All"))
            {
                foreach (var (hilliness, _) in hillinessTypes)
                {
                    filters.AllowedHilliness.Add(hilliness);
                }
                _cachedTileEstimate = "";
            }

            if (Widgets.ButtonText(clearRect, "None"))
            {
                filters.AllowedHilliness.Clear();
                _cachedTileEstimate = "";
            }

            return y + 24f;
        }

        #region Mod Grouping Helpers

        /// <summary>
        /// Gets the display name for a mod from a Def's modContentPack.
        /// Returns "Core" for base game, official DLC names, or the mod's name for other mods.
        /// </summary>
        private static string GetModDisplayName(Def? def)
        {
            if (def?.modContentPack == null)
                return "Core";

            var packageId = def.modContentPack.PackageId ?? "";
            var modName = def.modContentPack.Name ?? packageId;

            // Check for official DLCs
            if (packageId.IndexOf("ludeon.rimworld", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (packageId.IndexOf("royalty", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Royalty";
                if (packageId.IndexOf("ideology", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Ideology";
                if (packageId.IndexOf("biotech", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Biotech";
                if (packageId.IndexOf("anomaly", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Anomaly";
                return "Core";
            }

            // For other mods, use their display name
            return modName;
        }

        /// <summary>
        /// Gets the sort order for a mod group (Core first, then DLCs, then mods alphabetically).
        /// </summary>
        private static int GetModSortOrder(string modName)
        {
            return modName switch
            {
                "Core" => 0,
                "Royalty" => 1,
                "Ideology" => 2,
                "Biotech" => 3,
                "Anomaly" => 4,
                _ => 10  // Other mods come after DLCs
            };
        }

        /// <summary>
        /// Groups items by their source mod and returns them in sorted order.
        /// Core and official DLCs come first, then other mods alphabetically.
        /// </summary>
        private static List<(string modName, List<T> items)> GroupByMod<T>(
            IEnumerable<T> items,
            Func<T, string> getModName)
        {
            return items
                .GroupBy(getModName)
                .OrderBy(g => GetModSortOrder(g.Key))
                .ThenBy(g => g.Key)
                .Select(g => (g.Key, g.ToList()))
                .ToList();
        }

        /// <summary>
        /// Calculates the popup height for a mod-grouped container.
        /// Accounts for section headers for each mod group.
        /// </summary>
        private static float CalculateModGroupedPopupHeight(List<(string defName, string label, string modName)> items)
        {
            var groupCount = items.Select(i => i.modName).Distinct().Count();
            return items.Count * ContainerPopupItemHeight
                   + groupCount * ContainerPopupSectionHeight
                   + 56f; // mode note + buttons
        }

        /// <summary>
        /// Calculates the popup height for Rivers (special case - uses RiverDef directly).
        /// </summary>
        private static float CalculateRiversPopupHeight()
        {
            var riverTypes = Filtering.Filters.RiverFilter.GetAllRiverTypes().ToList();
            var groupCount = riverTypes.Select(r => GetModDisplayName(r)).Distinct().Count();
            return riverTypes.Count * ContainerPopupItemHeight
                   + groupCount * ContainerPopupSectionHeight
                   + 56f; // mode note + buttons
        }

        /// <summary>
        /// Draws a mod section header in a container popup.
        /// </summary>
        private static float DrawModSectionHeader(float x, float y, float width, string modName)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.5f, 0.5f, 0.5f);

            // Draw separator line at top of section (above the label)
            var lineRect = new Rect(x, y + 2f, width, 1f);
            Widgets.DrawLineHorizontal(lineRect.x, lineRect.y, lineRect.width);

            // Draw mod name
            var labelRect = new Rect(x + 4f, y, width - 8f, ContainerPopupSectionHeight);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, modName);
            Text.Anchor = TextAnchor.UpperLeft;

            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            return y + ContainerPopupSectionHeight;
        }

        #endregion

        /// <summary>
        /// Gets all natural stone types available in the current game, with mod info.
        /// </summary>
        private static List<(string defName, string label, string modName)> GetStoneTypesWithMod()
        {
            // Cache stone types since they don't change during gameplay
            if (_cachedStoneTypesWithMod == null)
            {
                _cachedStoneTypesWithMod = DefDatabase<ThingDef>.AllDefs
                    .Where(d => d.IsNonResourceNaturalRock)
                    .Select(d => (d.defName, d.LabelCap.ToString(), GetModDisplayName(d)))
                    .OrderBy(t => GetModSortOrder(t.Item3))
                    .ThenBy(t => t.Item3)
                    .ThenBy(t => t.Item2)
                    .ToList();
            }
            return _cachedStoneTypesWithMod;
        }

        private static List<(string defName, string label, string modName)>? _cachedStoneTypesWithMod = null;

        /// <summary>
        /// Gets all road types available in the current game, with mod info.
        /// </summary>
        private static List<(string defName, string label, string modName)> GetRoadTypesWithMod()
        {
            // Cache road types since they don't change during gameplay
            if (_cachedRoadTypesWithMod == null)
            {
                _cachedRoadTypesWithMod = Filtering.Filters.RoadFilter.GetAllRoadTypes()
                    .Select(r => (r.defName, GenText.ToTitleCaseSmart(r.label), GetModDisplayName(r)))
                    .OrderBy(t => GetModSortOrder(t.Item3))
                    .ThenBy(t => t.Item3)
                    .ThenBy(t => t.Item2)
                    .ToList();
            }
            return _cachedRoadTypesWithMod;
        }

        private static List<(string defName, string label, string modName)>? _cachedRoadTypesWithMod = null;

        /// <summary>
        /// Gets all stockpile types available in the game.
        /// </summary>
        private static List<(string defName, string label, string? dlc)> GetStockpileTypes()
        {
            // Known stockpile types from TileMutatorWorker_Stockpile.StockpileType enum
            return new List<(string defName, string label, string? dlc)>
            {
                ("Gravcore", "Compacted Gravcore", "Anomaly"),
                ("Weapons", "Weapons Cache", null),
                ("Medicine", "Medical Supplies", null),
                ("Chemfuel", "Chemfuel Stockpile", null),
                ("Component", "Components & Parts", null),
                ("Drugs", "Drug Stockpile", null)
            };
        }

        /// <summary>
        /// Gets all mineral ore types available in the game, with mod info.
        /// These are mineable resources that can appear on MineralRich tiles.
        /// </summary>
        private static List<(string defName, string label, string modName)> GetMineralOreTypesWithMod()
        {
            if (_cachedMineralOresWithMod != null) return _cachedMineralOresWithMod;

            // Query ThingDef for mineable ores that yield resources
            // These are the building category items that can be mined
            _cachedMineralOresWithMod = DefDatabase<ThingDef>.AllDefs
                .Where(d =>
                    d.mineable &&
                    d.building?.mineableThing != null &&
                    d.building.mineableThing.IsStuff &&
                    !d.IsNonResourceNaturalRock)  // Exclude plain rocks (covered by Stones filter)
                .Select(d => (
                    d.defName,
                    d.building.mineableThing.LabelCap.ToString(),  // Use the yielded resource name, not the rock name
                    GetModDisplayName(d)
                ))
                .GroupBy(t => t.Item1)  // Remove duplicates by defName
                .Select(g => g.First())
                .OrderBy(t => GetModSortOrder(t.Item3))
                .ThenBy(t => t.Item3)
                .ThenBy(t => t.Item2)
                .ToList();

            return _cachedMineralOresWithMod;
        }

        private static List<(string defName, string label, string modName)>? _cachedMineralOresWithMod = null;

        /// <summary>
        /// Gets all known plant grove species from the cache, with mod info.
        /// Returns unique plant species found across all tiles with PlantGrove mutator.
        /// </summary>
        private static List<(string defName, string label, string modName)> GetPlantGroveTypesWithMod()
        {
            if (_cachedPlantTypesWithMod != null) return _cachedPlantTypesWithMod;

            var state = LandingZoneContext.State;
            if (state == null)
            {
                _cachedPlantTypesWithMod = new List<(string defName, string label, string modName)>();
                return _cachedPlantTypesWithMod;
            }

            // Get unique plant species from cache
            var plantSet = new HashSet<string>();
            var world = Find.World;
            if (world != null)
            {
                for (int tileId = 0; tileId < world.grid.TilesCount; tileId++)
                {
                    var plants = state.MineralStockpileCache.GetPlantSpecies(tileId);
                    if (plants != null)
                    {
                        foreach (var plant in plants)
                        {
                            plantSet.Add(plant);
                        }
                    }
                }
            }

            // Convert to list with labels and mod info
            _cachedPlantTypesWithMod = plantSet
                .Select(p =>
                {
                    var def = DefDatabase<ThingDef>.GetNamedSilentFail(p);
                    var label = def?.LabelCap.ToString() ?? p.Replace("Plant_", "");
                    var modName = GetModDisplayName(def);
                    return (p, label, modName);
                })
                .OrderBy(t => GetModSortOrder(t.modName))
                .ThenBy(t => t.modName)
                .ThenBy(t => t.label)
                .ToList();

            return _cachedPlantTypesWithMod;
        }

        private static List<(string defName, string label, string modName)>? _cachedPlantTypesWithMod = null;

        /// <summary>
        /// Gets all known animal habitat species from the cache, with mod info.
        /// Returns unique animal species found across all tiles with AnimalHabitat mutator.
        /// </summary>
        private static List<(string defName, string label, string modName)> GetAnimalHabitatTypesWithMod()
        {
            if (_cachedAnimalTypesWithMod != null) return _cachedAnimalTypesWithMod;

            var state = LandingZoneContext.State;
            if (state == null)
            {
                _cachedAnimalTypesWithMod = new List<(string defName, string label, string modName)>();
                return _cachedAnimalTypesWithMod;
            }

            // Get unique animal species from cache
            var animalSet = new HashSet<string>();
            var world = Find.World;
            if (world != null)
            {
                for (int tileId = 0; tileId < world.grid.TilesCount; tileId++)
                {
                    var animals = state.MineralStockpileCache.GetAnimalSpecies(tileId);
                    if (animals != null)
                    {
                        foreach (var animal in animals)
                        {
                            animalSet.Add(animal);
                        }
                    }
                }
            }

            // Convert to list with labels and mod info
            _cachedAnimalTypesWithMod = animalSet
                .Select(a =>
                {
                    var def = DefDatabase<ThingDef>.GetNamedSilentFail(a);
                    var label = def?.LabelCap.ToString() ?? a;
                    var modName = GetModDisplayName(def);
                    return (a, label, modName);
                })
                .OrderBy(t => GetModSortOrder(t.modName))
                .ThenBy(t => t.modName)
                .ThenBy(t => t.label)
                .ToList();

            return _cachedAnimalTypesWithMod;
        }

        private static List<(string defName, string label, string modName)>? _cachedAnimalTypesWithMod = null;

        /// <summary>
        /// Gets all biome types available in the game, with mod info.
        /// </summary>
        private static List<(string defName, string label, string modName)> GetBiomeTypesWithMod()
        {
            // Cache biome types since they don't change during gameplay
            if (_cachedBiomeTypesWithMod == null)
            {
                _cachedBiomeTypesWithMod = Filtering.Filters.BiomeFilter.GetAllBiomeTypes()
                    .Select(b => (b.defName, GenText.ToTitleCaseSmart(b.label), GetModDisplayName(b)))
                    .OrderBy(t => GetModSortOrder(t.Item3))
                    .ThenBy(t => t.Item3)
                    .ThenBy(t => t.Item2)
                    .ToList();
            }
            return _cachedBiomeTypesWithMod;
        }

        private static List<(string defName, string label, string modName)>? _cachedBiomeTypesWithMod = null;

        /// <summary>
        /// Draws the Biomes popup content (checkboxes, OR-only mode, grouped by mod).
        /// </summary>
        private static float DrawBiomesPopupContent(float x, float y, float width, FilterSettings filters)
        {
            var biomeTypes = GetBiomeTypesWithMod();
            var groupedBiomes = GroupByMod(biomeTypes, t => t.modName);

            // Ensure Biomes is always OR (a tile can only have one biome, AND makes no sense)
            filters.Biomes.Operator = Data.ImportanceOperator.OR;

            // Note: Biomes are OR-only (a tile can only have one biome, AND makes no sense)
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            var noteRect = new Rect(x, y, width, 14f);
            Widgets.Label(noteRect, "Mode: Any selected (OR)");
            GUI.color = Color.white;
            y += 16f;

            foreach (var (modName, items) in groupedBiomes)
            {
                // Draw mod section header
                y = DrawModSectionHeader(x, y, width, modName);

                foreach (var (defName, label, _) in items)
                {
                    var itemRect = new Rect(x, y, width, ContainerPopupItemHeight - 2f);
                    var currentImportance = filters.Biomes.GetImportance(defName);
                    bool isSelected = currentImportance != FilterImportance.Ignored;

                    bool newSelected = isSelected;
                    Widgets.CheckboxLabeled(itemRect, label, ref newSelected);

                    if (newSelected != isSelected)
                    {
                        var bucketImportance = _containerPopupBucket ?? FilterImportance.Preferred;
                        filters.Biomes.SetImportance(defName, newSelected ? bucketImportance : FilterImportance.Ignored);
                        _cachedTileEstimate = "";
                    }

                    y += ContainerPopupItemHeight;
                }
            }

            y += 4f;

            // Select All / Clear buttons
            var selectAllRect = new Rect(x, y, width / 2 - 2f, 20f);
            var clearRect = new Rect(x + width / 2 + 2f, y, width / 2 - 2f, 20f);

            if (Widgets.ButtonText(selectAllRect, "All"))
            {
                var bucketImportance = _containerPopupBucket ?? FilterImportance.Preferred;
                foreach (var (defName, _, _) in biomeTypes)
                {
                    filters.Biomes.SetImportance(defName, bucketImportance);
                }
                _cachedTileEstimate = "";
            }

            if (Widgets.ButtonText(clearRect, "None"))
            {
                foreach (var (defName, _, _) in biomeTypes)
                {
                    filters.Biomes.SetImportance(defName, FilterImportance.Ignored);
                }
                _cachedTileEstimate = "";
            }

            return y + 24f;
        }

        /// <summary>
        /// Draws the Stones popup content (checkboxes, OR-only mode, grouped by mod).
        /// </summary>
        private static float DrawStonesPopupContent(float x, float y, float width, FilterSettings filters)
        {
            var stoneTypes = GetStoneTypesWithMod();
            var groupedStones = GroupByMod(stoneTypes, t => t.modName);

            // Note: Stones are OR-only (a tile can have 1-4 stone types, AND makes no sense)
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            var noteRect = new Rect(x, y, width, 14f);
            Widgets.Label(noteRect, "Mode: Any selected (OR)");
            GUI.color = Color.white;
            y += 16f;

            foreach (var (modName, items) in groupedStones)
            {
                // Draw mod section header
                y = DrawModSectionHeader(x, y, width, modName);

                foreach (var (defName, label, _) in items)
                {
                    var itemRect = new Rect(x, y, width, ContainerPopupItemHeight - 2f);
                    var currentImportance = filters.Stones.GetImportance(defName);
                    bool isSelected = currentImportance != FilterImportance.Ignored;

                    bool newSelected = isSelected;
                    Widgets.CheckboxLabeled(itemRect, label, ref newSelected);

                    if (newSelected != isSelected)
                    {
                        var bucketImportance = _containerPopupBucket ?? FilterImportance.Preferred;
                        filters.Stones.SetImportance(defName, newSelected ? bucketImportance : FilterImportance.Ignored);
                        _cachedTileEstimate = "";
                    }

                    y += ContainerPopupItemHeight;
                }
            }

            y += 4f;

            // Select All / Clear buttons
            var selectAllRect = new Rect(x, y, width / 2 - 2f, 20f);
            var clearRect = new Rect(x + width / 2 + 2f, y, width / 2 - 2f, 20f);

            if (Widgets.ButtonText(selectAllRect, "All"))
            {
                var bucketImportance = _containerPopupBucket ?? FilterImportance.Preferred;
                foreach (var (defName, _, _) in stoneTypes)
                {
                    filters.Stones.SetImportance(defName, bucketImportance);
                }
                _cachedTileEstimate = "";
            }

            if (Widgets.ButtonText(clearRect, "None"))
            {
                foreach (var (defName, _, _) in stoneTypes)
                {
                    filters.Stones.SetImportance(defName, FilterImportance.Ignored);
                }
                _cachedTileEstimate = "";
            }

            return y + 24f;
        }

        /// <summary>
        /// Draws the Stockpiles popup content (checkboxes with DLC checks).
        /// </summary>
        private static float DrawStockpilesPopupContent(float x, float y, float width, FilterSettings filters)
        {
            var stockpileTypes = GetStockpileTypes();

            foreach (var (defName, label, dlc) in stockpileTypes)
            {
                var itemRect = new Rect(x, y, width, ContainerPopupItemHeight - 2f);
                var currentImportance = filters.Stockpiles.GetImportance(defName);
                bool isSelected = currentImportance != FilterImportance.Ignored;

                // Check DLC requirement
                bool isDLCAvailable = string.IsNullOrEmpty(dlc) || DLCDetectionService.IsDLCAvailable(dlc!);

                if (!isDLCAvailable)
                {
                    // Show disabled item with DLC requirement
                    GUI.color = new Color(0.5f, 0.5f, 0.5f);
                    Widgets.Label(itemRect, $"☐ {label} ({dlc})");
                    GUI.color = Color.white;
                }
                else
                {
                    bool newSelected = isSelected;
                    Widgets.CheckboxLabeled(itemRect, label, ref newSelected);

                    if (newSelected != isSelected)
                    {
                        var bucketImportance = _containerPopupBucket ?? FilterImportance.Preferred;
                        filters.Stockpiles.SetImportance(defName, newSelected ? bucketImportance : FilterImportance.Ignored);
                        _cachedTileEstimate = "";
                    }
                }

                y += ContainerPopupItemHeight;
            }

            y += 4f;

            // Select All / Clear buttons
            var selectAllRect = new Rect(x, y, width / 2 - 2f, 20f);
            var clearRect = new Rect(x + width / 2 + 2f, y, width / 2 - 2f, 20f);

            if (Widgets.ButtonText(selectAllRect, "All"))
            {
                var bucketImportance = _containerPopupBucket ?? FilterImportance.Preferred;
                foreach (var (defName, _, dlc) in stockpileTypes)
                {
                    bool isDLCAvailable = string.IsNullOrEmpty(dlc) || DLCDetectionService.IsDLCAvailable(dlc!);
                    if (isDLCAvailable)
                    {
                        filters.Stockpiles.SetImportance(defName, bucketImportance);
                    }
                }
                _cachedTileEstimate = "";
            }

            if (Widgets.ButtonText(clearRect, "None"))
            {
                foreach (var (defName, _, _) in stockpileTypes)
                {
                    filters.Stockpiles.SetImportance(defName, FilterImportance.Ignored);
                }
                _cachedTileEstimate = "";
            }

            return y + 24f;
        }

        /// <summary>
        /// Draws the Plant Grove popup content (checkboxes, OR-only mode).
        /// </summary>
        private static float DrawPlantGrovePopupContent(float x, float y, float width, FilterSettings filters)
        {
            var plantTypes = GetPlantGroveTypesWithMod();

            if (plantTypes.Count == 0)
            {
                // No plants found in world cache yet
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                Widgets.Label(new Rect(x, y, width, 30f), "No plant groves detected.\nRun a search first to build cache.");
                GUI.color = Color.white;
                return y + 34f;
            }

            var groupedPlants = GroupByMod(plantTypes, t => t.modName);

            // Note: Plant Grove is OR-only
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            var noteRect = new Rect(x, y, width, 14f);
            Widgets.Label(noteRect, "Mode: Any selected (OR)");
            GUI.color = Color.white;
            y += 16f;

            foreach (var (modName, items) in groupedPlants)
            {
                // Draw mod section header
                y = DrawModSectionHeader(x, y, width, modName);

                foreach (var (defName, label, _) in items)
                {
                    var itemRect = new Rect(x, y, width, ContainerPopupItemHeight - 2f);
                    var currentImportance = filters.PlantGrove.GetImportance(defName);
                    bool isSelected = currentImportance != FilterImportance.Ignored;

                    bool newSelected = isSelected;
                    Widgets.CheckboxLabeled(itemRect, label, ref newSelected);

                    if (newSelected != isSelected)
                    {
                        var bucketImportance = _containerPopupBucket ?? FilterImportance.Preferred;
                        filters.PlantGrove.SetImportance(defName, newSelected ? bucketImportance : FilterImportance.Ignored);
                        _cachedTileEstimate = "";
                    }

                    y += ContainerPopupItemHeight;
                }
            }

            y += 4f;

            // Select All / Clear buttons
            var selectAllRect = new Rect(x, y, width / 2 - 2f, 20f);
            var clearRect = new Rect(x + width / 2 + 2f, y, width / 2 - 2f, 20f);

            if (Widgets.ButtonText(selectAllRect, "All"))
            {
                var bucketImportance = _containerPopupBucket ?? FilterImportance.Preferred;
                foreach (var (defName, _, _) in plantTypes)
                {
                    filters.PlantGrove.SetImportance(defName, bucketImportance);
                }
                _cachedTileEstimate = "";
            }

            if (Widgets.ButtonText(clearRect, "None"))
            {
                foreach (var (defName, _, _) in plantTypes)
                {
                    filters.PlantGrove.SetImportance(defName, FilterImportance.Ignored);
                }
                _cachedTileEstimate = "";
            }

            return y + 24f;
        }

        /// <summary>
        /// Draws the Animal Habitat popup content (checkboxes, OR-only mode).
        /// </summary>
        private static float DrawAnimalHabitatPopupContent(float x, float y, float width, FilterSettings filters)
        {
            var animalTypes = GetAnimalHabitatTypesWithMod();

            if (animalTypes.Count == 0)
            {
                // No animals found in world cache yet
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                Widgets.Label(new Rect(x, y, width, 30f), "No animal habitats detected.\nRun a search first to build cache.");
                GUI.color = Color.white;
                return y + 34f;
            }

            var groupedAnimals = GroupByMod(animalTypes, t => t.modName);

            // Note: Animal Habitat is OR-only
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            var noteRect = new Rect(x, y, width, 14f);
            Widgets.Label(noteRect, "Mode: Any selected (OR)");
            GUI.color = Color.white;
            y += 16f;

            foreach (var (modName, items) in groupedAnimals)
            {
                // Draw mod section header
                y = DrawModSectionHeader(x, y, width, modName);

                foreach (var (defName, label, _) in items)
                {
                    var itemRect = new Rect(x, y, width, ContainerPopupItemHeight - 2f);
                    var currentImportance = filters.AnimalHabitat.GetImportance(defName);
                    bool isSelected = currentImportance != FilterImportance.Ignored;

                    bool newSelected = isSelected;
                    Widgets.CheckboxLabeled(itemRect, label, ref newSelected);

                    if (newSelected != isSelected)
                    {
                        var bucketImportance = _containerPopupBucket ?? FilterImportance.Preferred;
                        filters.AnimalHabitat.SetImportance(defName, newSelected ? bucketImportance : FilterImportance.Ignored);
                        _cachedTileEstimate = "";
                    }

                    y += ContainerPopupItemHeight;
                }
            }

            y += 4f;

            // Select All / Clear buttons
            var selectAllRect = new Rect(x, y, width / 2 - 2f, 20f);
            var clearRect = new Rect(x + width / 2 + 2f, y, width / 2 - 2f, 20f);

            if (Widgets.ButtonText(selectAllRect, "All"))
            {
                var bucketImportance = _containerPopupBucket ?? FilterImportance.Preferred;
                foreach (var (defName, _, _) in animalTypes)
                {
                    filters.AnimalHabitat.SetImportance(defName, bucketImportance);
                }
                _cachedTileEstimate = "";
            }

            if (Widgets.ButtonText(clearRect, "None"))
            {
                foreach (var (defName, _, _) in animalTypes)
                {
                    filters.AnimalHabitat.SetImportance(defName, FilterImportance.Ignored);
                }
                _cachedTileEstimate = "";
            }

            return y + 24f;
        }

        /// <summary>
        /// Draws the Mineral Ores popup content (checkboxes, OR-only mode).
        /// </summary>
        private static float DrawMineralOresPopupContent(float x, float y, float width, FilterSettings filters)
        {
            var oreTypes = GetMineralOreTypesWithMod();

            if (oreTypes.Count == 0)
            {
                // No ores found
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                Widgets.Label(new Rect(x, y, width, 30f), "No mineable ores detected.");
                GUI.color = Color.white;
                return y + 34f;
            }

            var groupedOres = GroupByMod(oreTypes, t => t.modName);

            // Note: Mineral Ores is OR-only
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            var noteRect = new Rect(x, y, width, 14f);
            Widgets.Label(noteRect, "Mode: Any selected (OR)");
            GUI.color = Color.white;
            y += 16f;

            foreach (var (modName, items) in groupedOres)
            {
                // Draw mod section header
                y = DrawModSectionHeader(x, y, width, modName);

                foreach (var (defName, label, _) in items)
                {
                    var itemRect = new Rect(x, y, width, ContainerPopupItemHeight - 2f);
                    var currentImportance = filters.MineralOres.GetImportance(defName);
                    bool isSelected = currentImportance != FilterImportance.Ignored;

                    bool newSelected = isSelected;
                    Widgets.CheckboxLabeled(itemRect, label, ref newSelected);

                    if (newSelected != isSelected)
                    {
                        var bucketImportance = _containerPopupBucket ?? FilterImportance.Preferred;
                        filters.MineralOres.SetImportance(defName, newSelected ? bucketImportance : FilterImportance.Ignored);
                        _cachedTileEstimate = "";
                    }

                    y += ContainerPopupItemHeight;
                }
            }

            y += 4f;

            // Select All / Clear buttons
            var selectAllRect = new Rect(x, y, width / 2 - 2f, 20f);
            var clearRect = new Rect(x + width / 2 + 2f, y, width / 2 - 2f, 20f);

            if (Widgets.ButtonText(selectAllRect, "All"))
            {
                var bucketImportance = _containerPopupBucket ?? FilterImportance.Preferred;
                foreach (var (defName, _, _) in oreTypes)
                {
                    filters.MineralOres.SetImportance(defName, bucketImportance);
                }
                _cachedTileEstimate = "";
            }

            if (Widgets.ButtonText(clearRect, "None"))
            {
                foreach (var (defName, _, _) in oreTypes)
                {
                    filters.MineralOres.SetImportance(defName, FilterImportance.Ignored);
                }
                _cachedTileEstimate = "";
            }

            return y + 24f;
        }

        /// <summary>
        /// Formats a container chip label to show selection state.
        /// Examples: "Rivers (All)", "Rivers (2)", "Rivers (Creek|Huge)", "Hilliness (3)"
        /// </summary>
        private static string FormatContainerChipLabel(string baseLabel, ContainerType type, FilterSettings filters)
        {
            if (type == ContainerType.Rivers)
            {
                var riverTypes = Filtering.Filters.RiverFilter.GetAllRiverTypes().ToList();
                var selectedRivers = riverTypes
                    .Where(r => filters.Rivers.GetImportance(r.defName) != FilterImportance.Ignored)
                    .ToList();

                int total = riverTypes.Count;
                int selected = selectedRivers.Count;

                if (selected == 0)
                    return $"{baseLabel} (None)";
                if (selected == total)
                    return $"{baseLabel} (All)";
                if (selected <= 2)
                {
                    var names = selectedRivers.Select(r => GenText.ToTitleCaseSmart(r.label)).ToList();
                    return $"{baseLabel} ({string.Join("|", names)})";
                }
                return $"{baseLabel} ({selected})";
            }
            else if (type == ContainerType.Roads)
            {
                var roadTypes = GetRoadTypesWithMod();
                var selectedRoads = roadTypes
                    .Where(r => filters.Roads.GetImportance(r.defName) != FilterImportance.Ignored)
                    .ToList();

                int total = roadTypes.Count;
                int selected = selectedRoads.Count;

                if (selected == 0)
                    return $"{baseLabel} (None)";
                if (selected == total)
                    return $"{baseLabel} (All)";
                if (selected <= 2)
                {
                    var names = selectedRoads.Select(r => r.label).ToList();
                    return $"{baseLabel} ({string.Join("|", names)})";
                }
                return $"{baseLabel} ({selected})";
            }
            else if (type == ContainerType.Hilliness)
            {
                int selected = filters.AllowedHilliness.Count;
                if (selected == 0)
                    return $"{baseLabel} (None)";
                if (selected == 4)
                    return $"{baseLabel} (All)";
                if (selected <= 2)
                {
                    var names = filters.AllowedHilliness
                        .OrderBy(h => (int)h)
                        .Select(h => h switch
                        {
                            RimWorld.Planet.Hilliness.Flat => "Flat",
                            RimWorld.Planet.Hilliness.SmallHills => "Small",
                            RimWorld.Planet.Hilliness.LargeHills => "Large",
                            RimWorld.Planet.Hilliness.Mountainous => "Mtn",
                            _ => h.ToString()
                        })
                        .ToList();
                    return $"{baseLabel} ({string.Join("|", names)})";
                }
                return $"{baseLabel} ({selected})";
            }
            else if (type == ContainerType.Stones)
            {
                var stoneTypes = GetStoneTypesWithMod();
                var selectedStones = stoneTypes
                    .Where(s => filters.Stones.GetImportance(s.defName) != FilterImportance.Ignored)
                    .ToList();

                int total = stoneTypes.Count;
                int selected = selectedStones.Count;

                if (selected == 0)
                    return $"{baseLabel} (None)";
                if (selected == total)
                    return $"{baseLabel} (All)";
                if (selected <= 2)
                {
                    var names = selectedStones.Select(s => s.label).ToList();
                    return $"{baseLabel} ({string.Join("|", names)})";
                }
                return $"{baseLabel} ({selected})";
            }
            else if (type == ContainerType.Stockpiles)
            {
                var stockpileTypes = GetStockpileTypes();
                var selectedStockpiles = stockpileTypes
                    .Where(s => filters.Stockpiles.GetImportance(s.defName) != FilterImportance.Ignored)
                    .ToList();

                int total = stockpileTypes.Count;
                int selected = selectedStockpiles.Count;

                if (selected == 0)
                    return $"{baseLabel} (None)";
                if (selected == total)
                    return $"{baseLabel} (All)";
                if (selected <= 2)
                {
                    var names = selectedStockpiles.Select(s => s.label).ToList();
                    return $"{baseLabel} ({string.Join("|", names)})";
                }
                return $"{baseLabel} ({selected})";
            }
            else if (type == ContainerType.PlantGrove)
            {
                var plantTypes = GetPlantGroveTypesWithMod();
                var selectedPlants = plantTypes
                    .Where(p => filters.PlantGrove.GetImportance(p.defName) != FilterImportance.Ignored)
                    .ToList();

                int total = plantTypes.Count;
                int selected = selectedPlants.Count;

                if (selected == 0)
                    return $"{baseLabel} (None)";
                if (total > 0 && selected == total)
                    return $"{baseLabel} (All)";
                if (selected <= 2)
                {
                    var names = selectedPlants.Select(p => p.label).ToList();
                    return $"{baseLabel} ({string.Join("|", names)})";
                }
                return $"{baseLabel} ({selected})";
            }
            else if (type == ContainerType.AnimalHabitat)
            {
                var animalTypes = GetAnimalHabitatTypesWithMod();
                var selectedAnimals = animalTypes
                    .Where(a => filters.AnimalHabitat.GetImportance(a.defName) != FilterImportance.Ignored)
                    .ToList();

                int total = animalTypes.Count;
                int selected = selectedAnimals.Count;

                if (selected == 0)
                    return $"{baseLabel} (None)";
                if (total > 0 && selected == total)
                    return $"{baseLabel} (All)";
                if (selected <= 2)
                {
                    var names = selectedAnimals.Select(a => a.label).ToList();
                    return $"{baseLabel} ({string.Join("|", names)})";
                }
                return $"{baseLabel} ({selected})";
            }
            else if (type == ContainerType.MineralRich)
            {
                var oreTypes = GetMineralOreTypesWithMod();
                var selectedOres = oreTypes
                    .Where(o => filters.MineralOres.GetImportance(o.defName) != FilterImportance.Ignored)
                    .ToList();

                int total = oreTypes.Count;
                int selected = selectedOres.Count;

                if (selected == 0)
                    return $"{baseLabel} (None)";
                if (total > 0 && selected == total)
                    return $"{baseLabel} (All)";
                if (selected <= 2)
                {
                    var names = selectedOres.Select(o => o.label).ToList();
                    return $"{baseLabel} ({string.Join("|", names)})";
                }
                return $"{baseLabel} ({selected})";
            }
            else if (type == ContainerType.Biomes)
            {
                var biomeTypes = GetBiomeTypesWithMod();
                var selectedBiomes = biomeTypes
                    .Where(b => filters.Biomes.GetImportance(b.defName) != FilterImportance.Ignored)
                    .ToList();

                int total = biomeTypes.Count;
                int selected = selectedBiomes.Count;

                if (selected == 0)
                    return $"{baseLabel} (None)";
                if (total > 0 && selected == total)
                    return $"{baseLabel} (All)";
                if (selected <= 2)
                {
                    var names = selectedBiomes.Select(b => b.label).ToList();
                    return $"{baseLabel} ({string.Join("|", names)})";
                }
                return $"{baseLabel} ({selected})";
            }

            return baseLabel;
        }

        // ============================================================================
        // PALETTE FILTER DEFINITIONS
        // ============================================================================

        private class PaletteFilter
        {
            public string Id { get; }
            public string Label { get; }
            public string Category { get; }
            public string? SubGroup { get; }  // For visual separators within categories
            public bool IsHeavy { get; }
            public string? ValueDisplay { get; }

            // Range metadata for inline range editor
            public bool HasRange { get; }
            public RangeType RangeKind { get; }
            public float RangeMin { get; }
            public float RangeMax { get; }
            public float[]? DiscreteSteps { get; }  // For discrete ranges like Growing Days
            public string? Unit { get; }  // e.g., "°C", "mm", "m"

            // Mutator support - for MapFeature-based filters
            public FilterKind Kind { get; }
            public string? MutatorDefName { get; }  // The actual defName in MapFeatures container

            // Tooltip support - brief shown on hover, detailed shown via (i) click
            public string? TooltipBrief { get; }    // Short description, e.g., "World tile has river crossing"
            public string? TooltipDetailed { get; } // Longer explanation with examples

            // DLC requirement (e.g., "Anomaly", "Biotech", "Ideology")
            public string? RequiredDLC { get; }

            // Mod requirement (e.g., "Geological Landforms")
            public string? RequiredMod { get; }

            // Search aliases for discoverability (e.g., "Biome Transition" for MixedBiome)
            public string[]? SearchAliases { get; }

            // Regular importance-based filter
            public PaletteFilter(string id, string label, string category, bool isHeavy = false, string? valueDisplay = null,
                string? tooltipBrief = null, string? tooltipDetailed = null, string? subGroup = null,
                string? requiredDLC = null, string? requiredMod = null, string[]? searchAliases = null)
            {
                Id = id;
                Label = label;
                Category = category;
                SubGroup = subGroup;
                IsHeavy = isHeavy;
                ValueDisplay = valueDisplay;
                HasRange = false;
                Kind = FilterKind.Importance;
                TooltipBrief = tooltipBrief;
                TooltipDetailed = tooltipDetailed;
                RequiredDLC = requiredDLC;
                RequiredMod = requiredMod;
                SearchAliases = searchAliases;
            }

            // Range-based filter
            public PaletteFilter(string id, string label, string category, float rangeMin, float rangeMax,
                string? unit = null, bool isHeavy = false, float[]? discreteSteps = null,
                string? tooltipBrief = null, string? tooltipDetailed = null, string? subGroup = null,
                string? requiredDLC = null, string? requiredMod = null, string[]? searchAliases = null)
            {
                Id = id;
                Label = label;
                Category = category;
                SubGroup = subGroup;
                IsHeavy = isHeavy;
                HasRange = true;
                RangeMin = rangeMin;
                RangeMax = rangeMax;
                Unit = unit;
                DiscreteSteps = discreteSteps;
                RangeKind = discreteSteps != null ? RangeType.Discrete : RangeType.Continuous;
                Kind = FilterKind.Importance;
                TooltipBrief = tooltipBrief;
                TooltipDetailed = tooltipDetailed;
                RequiredDLC = requiredDLC;
                RequiredMod = requiredMod;
                SearchAliases = searchAliases;
            }

            // Mutator filter (MapFeature-based)
            public PaletteFilter(string id, string label, string category, string mutatorDefName, bool isHeavy = false,
                string? tooltipBrief = null, string? tooltipDetailed = null, string? subGroup = null,
                string? requiredDLC = null, string? requiredMod = null, string[]? searchAliases = null)
            {
                Id = id;
                Label = label;
                Category = category;
                SubGroup = subGroup;
                IsHeavy = isHeavy;
                HasRange = false;
                Kind = FilterKind.Mutator;
                MutatorDefName = mutatorDefName;
                TooltipBrief = tooltipBrief;
                TooltipDetailed = tooltipDetailed;
                RequiredDLC = requiredDLC;
                RequiredMod = requiredMod;
                SearchAliases = searchAliases;
            }

            // Container filter (Rivers, Hilliness with popup selector)
            public PaletteFilter(string id, string label, string category, ContainerType containerKind, bool isHeavy = false,
                string? tooltipBrief = null, string? tooltipDetailed = null, string? subGroup = null,
                string? requiredDLC = null, string? requiredMod = null, string[]? searchAliases = null)
            {
                Id = id;
                Label = label;
                Category = category;
                SubGroup = subGroup;
                IsHeavy = isHeavy;
                HasRange = false;
                Kind = FilterKind.Container;
                ContainerKind = containerKind;
                TooltipBrief = tooltipBrief;
                TooltipDetailed = tooltipDetailed;
                RequiredDLC = requiredDLC;
                RequiredMod = requiredMod;
                SearchAliases = searchAliases;
            }

            // Container type for Container kind filters
            public ContainerType? ContainerKind { get; }
        }

        private enum RangeType
        {
            Continuous,
            Discrete
        }

        private enum FilterKind
        {
            Importance,  // Regular importance-based filter (has dedicated FilterSettings property)
            Mutator,     // MapFeature mutator (stored in FilterSettings.MapFeatures container)
            Container    // Container filter with popup selector (Rivers, Hilliness)
        }

        private enum ContainerType
        {
            Rivers,       // IndividualImportanceContainer<string> with river defNames (OR-only)
            Roads,        // IndividualImportanceContainer<string> with road defNames (OR-only)
            Hilliness,    // HashSet<Hilliness> with terrain types
            Stones,       // IndividualImportanceContainer<string> with stone defNames (OR-only)
            Stockpiles,   // IndividualImportanceContainer<string> with stockpile types (Weapons, Medicine, etc.)
            PlantGrove,   // IndividualImportanceContainer<string> with plant species (OR-only)
            AnimalHabitat,// IndividualImportanceContainer<string> with animal species (OR-only)
            MineralRich,  // IndividualImportanceContainer<string> with ore defNames (OR-only)
            Biomes        // IndividualImportanceContainer<string> with biome defNames (OR-only)
        }

        // ============================================================================
        // MUTATOR OVERLAY SYSTEM
        // Provides curated metadata for runtime-discovered mutators
        // ============================================================================

        /// <summary>
        /// Curated metadata for mutators. Applied on top of runtime-discovered mutators.
        /// Provides friendly labels, category grouping, tooltips, and search aliases.
        /// </summary>
        private readonly struct MutatorOverlay
        {
            public string Label { get; }
            public string SubGroup { get; }
            public string? TooltipBrief { get; }
            public string[]? SearchAliases { get; }

            public MutatorOverlay(string label, string subGroup, string? tooltipBrief = null, string[]? searchAliases = null)
            {
                Label = label;
                SubGroup = subGroup;
                TooltipBrief = tooltipBrief;
                SearchAliases = searchAliases;
            }
        }

        /// <summary>
        /// Curated overlays for known mutators. Key = defName, Value = display metadata.
        /// Mutators not in this dictionary will use auto-generated labels and go to "Other" group.
        /// </summary>
        private static readonly Dictionary<string, MutatorOverlay> MutatorOverlays = new()
        {
            // ==================== CLIMATE ====================
            { "SunnyMutator", new MutatorOverlay("Sunny", "Climate", "Increased solar panel efficiency") },
            { "FoggyMutator", new MutatorOverlay("Foggy", "Climate", "Reduced visibility and solar efficiency") },
            { "WindyMutator", new MutatorOverlay("Windy", "Climate", "Increased wind turbine efficiency") },
            { "WetClimate", new MutatorOverlay("Wet Climate", "Climate", "Higher humidity and rainfall") },
            { "Pollution_Increased", new MutatorOverlay("Pollution+", "Climate", "Higher pollution levels") },

            // ==================== WATER FEATURES ====================
            { "River", new MutatorOverlay("River", "Water Features", "River crossing the map") },
            { "RiverDelta", new MutatorOverlay("River Delta", "Water Features", "River mouth with delta formation") },
            { "RiverConfluence", new MutatorOverlay("Confluence", "Water Features", "Multiple rivers meeting") },
            { "RiverIsland", new MutatorOverlay("River Island", "Water Features", "Island in river") },
            { "Headwater", new MutatorOverlay("Headwater", "Water Features", "River source/spring") },
            { "Lake", new MutatorOverlay("Lake", "Water Features", "Large body of fresh water") },
            { "LakeWithIsland", new MutatorOverlay("Lake w/ Island", "Water Features") },
            { "LakeWithIslands", new MutatorOverlay("Lake w/ Islands", "Water Features") },
            { "Lakeshore", new MutatorOverlay("Lakeshore", "Water Features") },
            { "Pond", new MutatorOverlay("Pond", "Water Features", "Small body of water") },
            { "Fjord", new MutatorOverlay("Fjord", "Water Features", "Narrow coastal inlet") },
            { "Bay", new MutatorOverlay("Bay", "Water Features", "Coastal bay") },
            { "Coast", new MutatorOverlay("Coast", "Water Features", "Ocean coastline") },
            { "Harbor", new MutatorOverlay("Harbor", "Water Features", "Natural harbor formation") },
            { "Cove", new MutatorOverlay("Cove", "Water Features", "Sheltered coastal area") },
            { "Peninsula", new MutatorOverlay("Peninsula", "Water Features") },
            { "Archipelago", new MutatorOverlay("Archipelago", "Water Features", "Chain of islands") },
            { "CoastalAtoll", new MutatorOverlay("Coastal Atoll", "Water Features") },
            { "CoastalIsland", new MutatorOverlay("Coastal Island", "Water Features") },
            { "Iceberg", new MutatorOverlay("Iceberg", "Water Features") },

            // ==================== ELEVATION ====================
            { "Mountain", new MutatorOverlay("Mountain", "Elevation", "Mountainous terrain") },
            { "Valley", new MutatorOverlay("Valley", "Elevation", "Valley between mountains") },
            { "Basin", new MutatorOverlay("Basin", "Elevation", "Low-lying area") },
            { "Plateau", new MutatorOverlay("Plateau", "Elevation", "Elevated flat terrain") },
            { "Hollow", new MutatorOverlay("Hollow", "Elevation", "Depression in terrain") },
            { "Caves", new MutatorOverlay("Caves", "Elevation", "Underground caves for defense/storage") },
            { "Cavern", new MutatorOverlay("Cavern", "Elevation", "Large underground chamber") },
            { "CaveLakes", new MutatorOverlay("Cave Lakes", "Elevation", "Underground lakes") },
            { "LavaCaves", new MutatorOverlay("Lava Caves", "Elevation", "Caves with lava") },
            { "IceCaves", new MutatorOverlay("Ice Caves", "Elevation", "Frozen underground caves") },
            { "Cliffs", new MutatorOverlay("Cliffs", "Elevation", "Steep cliff faces") },
            { "Chasm", new MutatorOverlay("Chasm", "Elevation", "Deep fissure in ground") },
            { "Crevasse", new MutatorOverlay("Crevasse", "Elevation", "Ice/rock crack") },

            // ==================== GROUND ====================
            { "Sandy", new MutatorOverlay("Sandy", "Ground", "Sandy terrain") },
            { "Muddy", new MutatorOverlay("Muddy", "Ground", "Muddy terrain") },
            { "Marshy", new MutatorOverlay("Marshy", "Ground", "Marsh/bog terrain") },
            { "Wetland", new MutatorOverlay("Wetland", "Ground", "Wetland ecosystem") },
            { "Dunes", new MutatorOverlay("Dunes", "Ground", "Sand dunes") },
            { "IceDunes", new MutatorOverlay("Ice Dunes", "Ground", "Frozen sand formations") },
            { "Oasis", new MutatorOverlay("Oasis", "Ground", "Desert oasis with water") },
            { "DryGround", new MutatorOverlay("Dry Ground", "Ground", "Arid terrain") },
            { "DryLake", new MutatorOverlay("Dry Lake", "Ground", "Dried lake bed") },

            // ==================== SPECIAL ====================
            { "ToxicLake", new MutatorOverlay("Toxic Lake", "Special", "Polluted water body") },
            { "LavaFlow", new MutatorOverlay("Lava Flow", "Special", "Active lava stream") },
            { "LavaCrater", new MutatorOverlay("Lava Crater", "Special", "Volcanic crater") },
            { "LavaLake", new MutatorOverlay("Lava Lake", "Special", "Lake of molten lava") },
            { "HotSprings", new MutatorOverlay("Hot Springs", "Special", "Natural hot springs for heating") },
            { "MixedBiome", new MutatorOverlay("Mixed Biome", "Special", "Tile at biome boundary", new[] { "Biome Transition", "Biome Edge" }) },
            { "ObsidianDeposits", new MutatorOverlay("Obsidian Deposits", "Special", "Volcanic glass deposits") },
            { "TerraformingScar", new MutatorOverlay("Terraforming Scar", "Special", "Ancient terraforming remnant") },

            // ==================== RESOURCES ====================
            { "Fertile", new MutatorOverlay("Fertile Soil", "Resources", "Better soil for farming") },
            { "SteamGeysers_Increased", new MutatorOverlay("Steam Geysers+", "Resources", "Extra geothermal power sources") },
            { "AnimalLife_Increased", new MutatorOverlay("Animal Life+", "Resources", "More wildlife") },
            { "AnimalLife_Decreased", new MutatorOverlay("Animal Life-", "Resources", "Less wildlife") },
            { "PlantLife_Increased", new MutatorOverlay("Plant Life+", "Resources", "More vegetation") },
            { "PlantLife_Decreased", new MutatorOverlay("Plant Life-", "Resources", "Less vegetation") },
            { "Fish_Increased", new MutatorOverlay("Fish+", "Resources", "More fish available") },
            { "Fish_Decreased", new MutatorOverlay("Fish-", "Resources", "Less fish available") },
            { "WildPlants", new MutatorOverlay("Wild Plants", "Resources", "Extra wild plants for foraging") },
            { "WildTropicalPlants", new MutatorOverlay("Wild Tropical Plants", "Resources", "Tropical plant species") },
            { "AnimalHabitat", new MutatorOverlay("Animal Habitat", "Resources", "Rich wildlife habitat") },
            { "PlantGrove", new MutatorOverlay("Plant Grove", "Resources", "Dense plant growth") },
            { "MineralRich", new MutatorOverlay("Mineral Rich", "Resources", "Extra ore deposits") },
            { "ArcheanTrees", new MutatorOverlay("Archean Trees", "Resources", "Ancient tree species") },

            // ==================== SALVAGE ====================
            { "Junkyard", new MutatorOverlay("Junkyard", "Salvage", "Salvageable materials") },
            { "Stockpile", new MutatorOverlay("Stockpile", "Salvage", "Abandoned supply cache") },
            { "AncientRuins", new MutatorOverlay("Ancient Ruins", "Salvage", "Pre-war ruins with loot") },
            { "AncientRuins_Frozen", new MutatorOverlay("Ancient Ruins (Frozen)", "Salvage", "Ice-preserved ruins") },
            { "AncientWarehouse", new MutatorOverlay("Ancient Warehouse", "Salvage", "Large supply cache") },

            // ==================== STRUCTURES ====================
            { "AncientGarrison", new MutatorOverlay("Ancient Garrison", "Structures", "Military installation") },
            { "AncientQuarry", new MutatorOverlay("Ancient Quarry", "Structures", "Mining operation") },
            { "AncientChemfuelRefinery", new MutatorOverlay("Chemfuel Refinery", "Structures", "Fuel processing plant") },
            { "AncientLaunchSite", new MutatorOverlay("Launch Site", "Structures", "Rocket launch facility") },
            { "AncientUplink", new MutatorOverlay("Ancient Uplink", "Structures", "Communication array") },

            // ==================== SETTLEMENTS ====================
            { "AbandonedColonyOutlander", new MutatorOverlay("Abandoned (Outlander)", "Settlements", "Abandoned outlander colony") },
            { "AbandonedColonyTribal", new MutatorOverlay("Abandoned (Tribal)", "Settlements", "Abandoned tribal settlement") },
            { "AncientInfestedSettlement", new MutatorOverlay("Infested Settlement", "Settlements", "Bug-infested ruins") },

            // ==================== ENVIRONMENTAL ====================
            { "AncientHeatVent", new MutatorOverlay("Heat Vent", "Environmental", "Geothermal heat source") },
            { "AncientSmokeVent", new MutatorOverlay("Smoke Vent", "Environmental", "Volcanic smoke emission") },
            { "AncientToxVent", new MutatorOverlay("Toxic Vent", "Environmental", "Toxic gas emission") },
            { "InsectMegahive", new MutatorOverlay("Insect Megahive", "Environmental", "Massive insect colony") },
        };

        /// <summary>
        /// Category display order for mutator groups in palette.
        /// Groups not in this list appear at the end alphabetically.
        /// </summary>
        private static readonly string[] MutatorGroupOrder = new[]
        {
            "Climate", "Water Features", "Elevation", "Ground", "Special",
            "Resources", "Salvage", "Structures", "Settlements", "Environmental", "Other"
        };

        // Growing days discrete steps: 0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60 (Full-Year)
        private static readonly float[] GrowingDaysSteps = { 0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60 };

        private static List<PaletteFilter> GetClimatePaletteFilters() => new()
        {
            // Core climate filters (range-based)
            new PaletteFilter("average_temperature", "Average Temperature", "Climate", -50f, 50f, "°C"),
            new PaletteFilter("minimum_temperature", "Minimum Temperature", "Climate", -100f, 30f, "°C"),
            new PaletteFilter("maximum_temperature", "Maximum Temperature", "Climate", 0f, 80f, "°C"),
            new PaletteFilter("rainfall", "Rainfall", "Climate", 0f, 5000f, "mm"),
            new PaletteFilter("growing_days", "Growing Days", "Climate", 0f, 60f, "days", isHeavy: true, discreteSteps: GrowingDaysSteps),
            new PaletteFilter("pollution", "Pollution", "Climate", 0f, 1f, "%", requiredDLC: "Biotech"),

            // Climate mutators (MapFeature-based)
            new PaletteFilter("mutator_sunny", "Sunny", "Climate", "SunnyMutator"),
            new PaletteFilter("mutator_foggy", "Foggy", "Climate", "FoggyMutator"),
            new PaletteFilter("mutator_windy", "Windy", "Climate", "WindyMutator"),
            new PaletteFilter("mutator_wet", "Wet Climate", "Climate", "WetClimate"),
            new PaletteFilter("mutator_pollution", "Pollution Increased", "Climate", "Pollution_Increased", requiredDLC: "Biotech"),
        };

        private static List<PaletteFilter> GetGeographyNaturalPaletteFilters() => new()
        {
            // Biomes - multi-select container (first in Geography)
            new PaletteFilter("biomes", "Biomes", "Geography_Natural", ContainerType.Biomes,
                tooltipBrief: "LandingZone_Tooltip_Biomes", tooltipDetailed: "LandingZone_TooltipDetail_Biomes", subGroup: "Biome"),

            // Terrain - container chips and ranges
            new PaletteFilter("hilliness", "Hilliness", "Geography_Natural", ContainerType.Hilliness,
                tooltipBrief: "LandingZone_Tooltip_Hilliness", tooltipDetailed: "LandingZone_TooltipDetail_Hilliness", subGroup: "Terrain"),
            new PaletteFilter("elevation", "Elevation", "Geography_Natural", 0f, 5000f, "m",
                tooltipBrief: "LandingZone_Tooltip_Elevation", subGroup: "Terrain"),
            new PaletteFilter("swampiness", "Swampiness", "Geography_Natural", 0f, 1f, "%",
                tooltipBrief: "LandingZone_Tooltip_Swampiness", subGroup: "Terrain"),
            new PaletteFilter("movement_difficulty", "Movement Difficulty", "Geography_Natural", 0f, 2f, "×",
                tooltipBrief: "LandingZone_Tooltip_MoveDifficulty", subGroup: "Terrain"),

            // Water Access - booleans and container
            new PaletteFilter("coastal", "Ocean Coastal", "Geography_Natural",
                tooltipBrief: "LandingZone_Tooltip_Coastal", tooltipDetailed: "LandingZone_TooltipDetail_Coastal", subGroup: "Water Access"),
            new PaletteFilter("coastal_lake", "Lake Coastal", "Geography_Natural",
                tooltipBrief: "LandingZone_Tooltip_CoastalLake", tooltipDetailed: "LandingZone_TooltipDetail_CoastalLake", subGroup: "Water Access"),
            new PaletteFilter("water_access", "Water Access", "Geography_Natural",
                tooltipBrief: "LandingZone_Tooltip_WaterAccess", tooltipDetailed: "LandingZone_TooltipDetail_WaterAccess", subGroup: "Water Access"),
            new PaletteFilter("rivers", "Rivers", "Geography_Natural", ContainerType.Rivers,
                tooltipBrief: "LandingZone_Tooltip_Rivers", tooltipDetailed: "LandingZone_TooltipDetail_Rivers", subGroup: "Water Access"),

            // Water Features (mutators) - These are Geological Landforms map generation features
            new PaletteFilter("mutator_river", "River (Landform)", "Geography_Natural", "River",
                tooltipBrief: "LandingZone_Tooltip_River", tooltipDetailed: "LandingZone_TooltipDetail_River", subGroup: "Water Features", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_river_delta", "River Delta", "Geography_Natural", "RiverDelta", subGroup: "Water Features", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_river_confluence", "Confluence", "Geography_Natural", "RiverConfluence", subGroup: "Water Features", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_river_island", "River Island", "Geography_Natural", "RiverIsland", subGroup: "Water Features", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_headwater", "Headwater", "Geography_Natural", "Headwater", subGroup: "Water Features", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_lake", "Lake", "Geography_Natural", "Lake", subGroup: "Water Features", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_lake_island", "Lake w/ Island", "Geography_Natural", "LakeWithIsland", subGroup: "Water Features", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_lake_islands", "Lake w/ Islands", "Geography_Natural", "LakeWithIslands", subGroup: "Water Features", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_lakeshore", "Lakeshore", "Geography_Natural", "Lakeshore", subGroup: "Water Features", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_pond", "Pond", "Geography_Natural", "Pond", subGroup: "Water Features", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_fjord", "Fjord", "Geography_Natural", "Fjord", subGroup: "Water Features", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_bay", "Bay", "Geography_Natural", "Bay", subGroup: "Water Features", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_coast", "Coast", "Geography_Natural", "Coast", subGroup: "Water Features", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_harbor", "Harbor", "Geography_Natural", "Harbor", subGroup: "Water Features", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_cove", "Cove", "Geography_Natural", "Cove", subGroup: "Water Features", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_peninsula", "Peninsula", "Geography_Natural", "Peninsula", subGroup: "Water Features", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_archipelago", "Archipelago", "Geography_Natural", "Archipelago", subGroup: "Water Features", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_coastal_atoll", "Coastal Atoll", "Geography_Natural", "CoastalAtoll", subGroup: "Water Features", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_coastal_island", "Coastal Island", "Geography_Natural", "CoastalIsland", subGroup: "Water Features", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_iceberg", "Iceberg", "Geography_Natural", "Iceberg", subGroup: "Water Features", requiredMod: "Geological Landforms"),
            // Geological Landforms - Water Features
            new PaletteFilter("mutator_gl_atoll", "Atoll", "Geography_Natural", "GL_Atoll",
                subGroup: "Water Features", requiredMod: "Geological Landforms", tooltipBrief: "Ring-shaped coral reef"),
            new PaletteFilter("mutator_gl_island", "Island", "Geography_Natural", "GL_Island",
                subGroup: "Water Features", requiredMod: "Geological Landforms", tooltipBrief: "Isolated landmass"),
            new PaletteFilter("mutator_gl_skerry", "Skerry", "Geography_Natural", "GL_Skerry",
                subGroup: "Water Features", requiredMod: "Geological Landforms", tooltipBrief: "Small rocky island"),
            new PaletteFilter("mutator_gl_tombolo", "Tombolo", "Geography_Natural", "GL_Tombolo",
                subGroup: "Water Features", requiredMod: "Geological Landforms", tooltipBrief: "Sand bar connecting island"),
            new PaletteFilter("mutator_gl_landbridge", "Land Bridge", "Geography_Natural", "GL_Landbridge",
                subGroup: "Water Features", requiredMod: "Geological Landforms", tooltipBrief: "Natural land connection"),
            new PaletteFilter("mutator_gl_cove_island", "Cove with Island", "Geography_Natural", "GL_CoveWithIsland",
                subGroup: "Water Features", requiredMod: "Geological Landforms", tooltipBrief: "Sheltered cove with island"),
            new PaletteFilter("mutator_gl_secluded_cove", "Secluded Cove", "Geography_Natural", "GL_SecludedCove",
                subGroup: "Water Features", requiredMod: "Geological Landforms", tooltipBrief: "Hidden coastal inlet"),
            new PaletteFilter("mutator_gl_river_source", "River Source", "Geography_Natural", "GL_RiverSource",
                subGroup: "Water Features", requiredMod: "Geological Landforms", tooltipBrief: "Origin point of river"),

            // Elevation Features (mutators) - These are Geological Landforms map generation features
            new PaletteFilter("mutator_mountain", "Mountain (Landform)", "Geography_Natural", "Mountain",
                tooltipBrief: "LandingZone_Tooltip_Mountain", tooltipDetailed: "LandingZone_TooltipDetail_Mountain", subGroup: "Elevation", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_valley", "Valley", "Geography_Natural", "Valley",
                tooltipBrief: "LandingZone_Tooltip_Mutator", subGroup: "Elevation", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_basin", "Basin", "Geography_Natural", "Basin",
                tooltipBrief: "LandingZone_Tooltip_Mutator", subGroup: "Elevation", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_plateau", "Plateau", "Geography_Natural", "Plateau",
                tooltipBrief: "LandingZone_Tooltip_Mutator", subGroup: "Elevation", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_hollow", "Hollow", "Geography_Natural", "Hollow",
                tooltipBrief: "LandingZone_Tooltip_Mutator", subGroup: "Elevation", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_caves", "Caves", "Geography_Natural", "Caves",
                tooltipBrief: "LandingZone_Tooltip_Caves", tooltipDetailed: "LandingZone_TooltipDetail_Caves", subGroup: "Elevation", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_cavern", "Cavern", "Geography_Natural", "Cavern", subGroup: "Elevation", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_cave_lakes", "Cave Lakes", "Geography_Natural", "CaveLakes", subGroup: "Elevation", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_lava_caves", "Lava Caves", "Geography_Natural", "LavaCaves", subGroup: "Elevation", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_ice_caves", "Ice Caves", "Geography_Natural", "IceCaves", subGroup: "Elevation", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_cliffs", "Cliffs", "Geography_Natural", "Cliffs", subGroup: "Elevation", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_chasm", "Chasm", "Geography_Natural", "Chasm", subGroup: "Elevation", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_crevasse", "Crevasse", "Geography_Natural", "Crevasse", subGroup: "Elevation", requiredMod: "Geological Landforms"),
            // Geological Landforms - Elevation
            new PaletteFilter("mutator_gl_canyon", "Canyon", "Geography_Natural", "GL_Canyon",
                subGroup: "Elevation", requiredMod: "Geological Landforms", tooltipBrief: "Deep river-carved gorge"),
            new PaletteFilter("mutator_gl_gorge", "Gorge", "Geography_Natural", "GL_Gorge",
                subGroup: "Elevation", requiredMod: "Geological Landforms", tooltipBrief: "Narrow steep valley"),
            new PaletteFilter("mutator_gl_rift", "Rift", "Geography_Natural", "GL_Rift",
                subGroup: "Elevation", requiredMod: "Geological Landforms", tooltipBrief: "Tectonic fissure"),
            new PaletteFilter("mutator_gl_caldera", "Caldera", "Geography_Natural", "GL_Caldera",
                subGroup: "Elevation", requiredMod: "Geological Landforms", tooltipBrief: "Volcanic crater basin"),
            new PaletteFilter("mutator_gl_crater", "Crater", "Geography_Natural", "GL_Crater",
                subGroup: "Elevation", requiredMod: "Geological Landforms", tooltipBrief: "Impact crater"),
            new PaletteFilter("mutator_gl_cirque", "Cirque", "Geography_Natural", "GL_Cirque",
                subGroup: "Elevation", requiredMod: "Geological Landforms", tooltipBrief: "Glacial amphitheater"),
            new PaletteFilter("mutator_gl_glacier", "Glacier", "Geography_Natural", "GL_Glacier",
                subGroup: "Elevation", requiredMod: "Geological Landforms", tooltipBrief: "Massive ice formation"),
            new PaletteFilter("mutator_gl_lone_mountain", "Lone Mountain", "Geography_Natural", "GL_LoneMountain",
                subGroup: "Elevation", requiredMod: "Geological Landforms", tooltipBrief: "Isolated peak"),
            new PaletteFilter("mutator_gl_secluded_valley", "Secluded Valley", "Geography_Natural", "GL_SecludedValley",
                subGroup: "Elevation", requiredMod: "Geological Landforms", tooltipBrief: "Hidden valley"),
            new PaletteFilter("mutator_gl_sinkhole", "Sinkhole", "Geography_Natural", "GL_Sinkhole",
                subGroup: "Elevation", requiredMod: "Geological Landforms", tooltipBrief: "Ground collapse depression"),
            new PaletteFilter("mutator_gl_cave_entrance", "Cave Entrance", "Geography_Natural", "GL_CaveEntrance",
                subGroup: "Elevation", requiredMod: "Geological Landforms", tooltipBrief: "Surface access to caves"),
            new PaletteFilter("mutator_gl_surface_cave", "Surface Cave", "Geography_Natural", "GL_SurfaceCave",
                subGroup: "Elevation", requiredMod: "Geological Landforms", tooltipBrief: "Exposed cave system"),

            // Ground Conditions (mutators) - These are Geological Landforms map generation features
            new PaletteFilter("mutator_sandy", "Sandy", "Geography_Natural", "Sandy", subGroup: "Ground", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_muddy", "Muddy", "Geography_Natural", "Muddy", subGroup: "Ground", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_marshy", "Marshy", "Geography_Natural", "Marshy", subGroup: "Ground", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_wetland", "Wetland", "Geography_Natural", "Wetland", subGroup: "Ground", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_dunes", "Dunes", "Geography_Natural", "Dunes", subGroup: "Ground", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_ice_dunes", "Ice Dunes", "Geography_Natural", "IceDunes", subGroup: "Ground", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_oasis", "Oasis", "Geography_Natural", "Oasis", subGroup: "Ground", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_dry_ground", "Dry Ground", "Geography_Natural", "DryGround", subGroup: "Ground", requiredMod: "Geological Landforms"),
            new PaletteFilter("mutator_dry_lake", "Dry Lake", "Geography_Natural", "DryLake", subGroup: "Ground", requiredMod: "Geological Landforms"),

            // Special/Hazards (mutators)
            new PaletteFilter("mutator_toxic_lake", "Toxic Lake", "Geography_Natural", "ToxicLake", subGroup: "Special"),
            new PaletteFilter("mutator_lava_flow", "Lava Flow", "Geography_Natural", "LavaFlow", subGroup: "Special"),
            new PaletteFilter("mutator_lava_crater", "Lava Crater", "Geography_Natural", "LavaCrater", subGroup: "Special"),
            new PaletteFilter("mutator_lava_lake", "Lava Lake", "Geography_Natural", "LavaLake", subGroup: "Special"),
            new PaletteFilter("mutator_hot_springs", "Hot Springs", "Geography_Natural", "HotSprings", subGroup: "Special"),
            new PaletteFilter("mutator_mixed_biome", "Mixed Biome", "Geography_Natural", "MixedBiome", subGroup: "Special"),
            new PaletteFilter("mutator_obsidian", "Obsidian Deposits", "Geography_Natural", "ObsidianDeposits", subGroup: "Special"),
            // Geological Landforms - Special
            new PaletteFilter("mutator_gl_biome_transitions", "Biome Transitions", "Geography_Natural", "GL_BiomeTransitions",
                subGroup: "Special", requiredMod: "Geological Landforms", tooltipBrief: "Multiple biomes on single tile",
                searchAliases: new[] { "Biome Edge", "Multi-Biome" }),
            new PaletteFilter("mutator_gl_badlands", "Badlands", "Geography_Natural", "GL_Badlands",
                subGroup: "Special", requiredMod: "Geological Landforms", tooltipBrief: "Eroded rocky terrain"),
            new PaletteFilter("mutator_gl_desert_plateau", "Desert Plateau", "Geography_Natural", "GL_DesertPlateau",
                subGroup: "Special", requiredMod: "Geological Landforms", tooltipBrief: "Elevated desert terrain"),
            new PaletteFilter("mutator_gl_swamp_hill", "Swamp Hill", "Geography_Natural", "GL_SwampHill",
                subGroup: "Special", requiredMod: "Geological Landforms", tooltipBrief: "Elevated wetland"),

            // Alpha Biomes - Ground/Hazards
            new PaletteFilter("mutator_ab_tar_lakes", "Tar Lakes", "Geography_Natural", "AB_TarLakes",
                subGroup: "Special", requiredMod: "Alpha Biomes", tooltipBrief: "Lakes of tar"),
            new PaletteFilter("mutator_ab_propane_lakes", "Propane Lakes", "Geography_Natural", "AB_PropaneLakes",
                subGroup: "Special", requiredMod: "Alpha Biomes", tooltipBrief: "Flammable propane pools"),
            new PaletteFilter("mutator_ab_quicksand", "Quicksand Pits", "Geography_Natural", "AB_QuicksandPits",
                subGroup: "Special", requiredMod: "Alpha Biomes", tooltipBrief: "Dangerous quicksand areas"),
            new PaletteFilter("mutator_ab_magma_vents", "Magma Vents", "Geography_Natural", "AB_MagmaVents",
                subGroup: "Special", requiredMod: "Alpha Biomes", tooltipBrief: "Active magma vents"),
            new PaletteFilter("mutator_ab_magmatic_quagmire", "Magmatic Quagmire", "Geography_Natural", "AB_MagmaticQuagmire",
                subGroup: "Special", requiredMod: "Alpha Biomes", tooltipBrief: "Volcanic mud terrain"),
            new PaletteFilter("mutator_ab_sterile_ground", "Sterile Ground", "Geography_Natural", "AB_SterileGround",
                subGroup: "Ground", requiredMod: "Alpha Biomes", tooltipBrief: "Infertile terrain"),
        };

        private static List<PaletteFilter> GetGeographyResourcesPaletteFilters() => new()
        {
            // Materials - Stones and mineral deposits
            new PaletteFilter("stones", "Natural Stones", "Geography_Resources", ContainerType.Stones, subGroup: "Materials"),
            new PaletteFilter("mineral_rich", "Mineral Rich", "Geography_Resources", ContainerType.MineralRich, subGroup: "Materials"),

            // Life & Wildlife - Base resource metrics
            new PaletteFilter("forageability", "Forageability", "Geography_Resources", 0f, 1f, "%", subGroup: "Life & Wildlife"),
            new PaletteFilter("graze", "Grazeable", "Geography_Resources", subGroup: "Life & Wildlife"),
            new PaletteFilter("animal_density", "Animal Density", "Geography_Resources", 0f, 6.5f, subGroup: "Life & Wildlife"),
            new PaletteFilter("fish_population", "Fish Population", "Geography_Resources", 0f, 900f, subGroup: "Life & Wildlife"),
            new PaletteFilter("plant_density", "Plant Density", "Geography_Resources", 0f, 1.3f, subGroup: "Life & Wildlife"),

            // Resource Modifiers - Mutators that boost resources
            new PaletteFilter("mutator_fertile", "Fertile Soil", "Geography_Resources", "Fertile", subGroup: "Modifiers"),
            new PaletteFilter("mutator_steam_geysers", "Steam Geysers+", "Geography_Resources", "SteamGeysers_Increased", subGroup: "Modifiers"),
            new PaletteFilter("mutator_animal_life_up", "Animal Life+", "Geography_Resources", "AnimalLife_Increased", subGroup: "Modifiers"),
            new PaletteFilter("mutator_animal_life_down", "Animal Life-", "Geography_Resources", "AnimalLife_Decreased", subGroup: "Modifiers"),
            new PaletteFilter("mutator_plant_life_up", "Plant Life+", "Geography_Resources", "PlantLife_Increased", subGroup: "Modifiers"),
            new PaletteFilter("mutator_plant_life_down", "Plant Life-", "Geography_Resources", "PlantLife_Decreased", subGroup: "Modifiers"),
            new PaletteFilter("mutator_fish_up", "Fish+", "Geography_Resources", "Fish_Increased", subGroup: "Modifiers"),
            new PaletteFilter("mutator_fish_down", "Fish-", "Geography_Resources", "Fish_Decreased", subGroup: "Modifiers"),
            new PaletteFilter("mutator_wild_plants", "Wild Plants", "Geography_Resources", "WildPlants", subGroup: "Modifiers"),
            new PaletteFilter("mutator_wild_tropical", "Wild Tropical Plants", "Geography_Resources", "WildTropicalPlants", subGroup: "Modifiers"),
            new PaletteFilter("plant_grove", "Plant Grove", "Geography_Resources", ContainerType.PlantGrove, subGroup: "Modifiers"),
            new PaletteFilter("animal_habitat", "Animal Habitat", "Geography_Resources", ContainerType.AnimalHabitat, subGroup: "Modifiers"),
            new PaletteFilter("mutator_archean_trees", "Archean Trees", "Geography_Resources", "ArcheanTrees", subGroup: "Modifiers", requiredDLC: "Anomaly"),

            // Alpha Biomes - Resource Modifiers (requiredMod filters only show when mod is active)
            new PaletteFilter("mutator_ab_golden_trees", "Golden Trees", "Geography_Resources", "AB_GoldenTrees",
                subGroup: "Modifiers", requiredMod: "Alpha Biomes", tooltipBrief: "Trees that produce gold"),
            new PaletteFilter("mutator_ab_luminescent_trees", "Luminescent Trees", "Geography_Resources", "AB_LuminescentTrees",
                subGroup: "Modifiers", requiredMod: "Alpha Biomes", tooltipBrief: "Glowing trees"),
            new PaletteFilter("mutator_ab_techno_trees", "Techno Trees", "Geography_Resources", "AB_TechnoTrees",
                subGroup: "Modifiers", requiredMod: "Alpha Biomes", tooltipBrief: "Technology-infused trees"),
            new PaletteFilter("mutator_ab_flesh_trees", "Flesh Trees", "Geography_Resources", "AB_FleshTrees",
                subGroup: "Modifiers", requiredMod: "Alpha Biomes", tooltipBrief: "Organic horror trees"),
            new PaletteFilter("mutator_ab_healing_springs", "Healing Springs", "Geography_Resources", "AB_HealingSprings",
                subGroup: "Modifiers", requiredMod: "Alpha Biomes", tooltipBrief: "Springs with healing properties"),
            new PaletteFilter("mutator_ab_mutagenic_springs", "Mutagenic Springs", "Geography_Resources", "AB_MutagenicSprings",
                subGroup: "Modifiers", requiredMod: "Alpha Biomes", tooltipBrief: "Springs with mutagenic effects"),
            new PaletteFilter("mutator_ab_geothermal_hotspots", "Geothermal Hotspots", "Geography_Resources", "AB_GeothermalHotspots",
                subGroup: "Modifiers", requiredMod: "Alpha Biomes", tooltipBrief: "Extra geothermal activity"),
        };

        private static List<PaletteFilter> GetGeographyArtificialPaletteFilters() => new()
        {
            // Infrastructure - Roads
            new PaletteFilter("roads", "Roads", "Geography_Artificial", ContainerType.Roads,
                tooltipBrief: "LandingZone_Tooltip_Roads", tooltipDetailed: "LandingZone_TooltipDetail_Roads", subGroup: "Infrastructure"),

            // Landmarks - Settlement proximity
            new PaletteFilter("landmark", "Landmarks", "Geography_Artificial", subGroup: "Landmarks"),

            // Salvage - Lootable locations
            new PaletteFilter("mutator_junkyard", "Junkyard", "Geography_Artificial", "Junkyard", subGroup: "Salvage"),
            new PaletteFilter("stockpiles", "Stockpiles", "Geography_Artificial", ContainerType.Stockpiles, subGroup: "Salvage"),
            new PaletteFilter("mutator_ancient_ruins", "Ancient Ruins", "Geography_Artificial", "AncientRuins", subGroup: "Salvage"),
            new PaletteFilter("mutator_ancient_ruins_frozen", "Ancient Ruins (Frozen)", "Geography_Artificial", "AncientRuins_Frozen", subGroup: "Salvage"),
            new PaletteFilter("mutator_ancient_warehouse", "Ancient Warehouse", "Geography_Artificial", "AncientWarehouse", subGroup: "Salvage"),
            // Alpha Biomes - Salvage
            new PaletteFilter("mutator_ab_giant_fossils", "Giant Fossils", "Geography_Artificial", "AB_GiantFossils",
                subGroup: "Salvage", requiredMod: "Alpha Biomes", tooltipBrief: "Ancient creature remains"),
            new PaletteFilter("mutator_ab_derelict_archonexus", "Derelict Archonexus", "Geography_Artificial", "AB_DerelictArchonexus",
                subGroup: "Salvage", requiredMod: "Alpha Biomes", tooltipBrief: "Abandoned archotech structure"),
            new PaletteFilter("mutator_ab_derelict_clusters", "Derelict Clusters", "Geography_Artificial", "AB_DerelictClusters",
                subGroup: "Salvage", requiredMod: "Alpha Biomes", tooltipBrief: "Clusters of abandoned tech"),

            // Structures - Ancient installations
            new PaletteFilter("mutator_ancient_garrison", "Ancient Garrison", "Geography_Artificial", "AncientGarrison", subGroup: "Structures"),
            new PaletteFilter("mutator_ancient_quarry", "Ancient Quarry", "Geography_Artificial", "AncientQuarry", subGroup: "Structures"),
            new PaletteFilter("mutator_ancient_refinery", "Chemfuel Refinery", "Geography_Artificial", "AncientChemfuelRefinery", subGroup: "Structures"),
            new PaletteFilter("mutator_ancient_launch_site", "Launch Site", "Geography_Artificial", "AncientLaunchSite", subGroup: "Structures"),
            new PaletteFilter("mutator_ancient_uplink", "Ancient Uplink", "Geography_Artificial", "AncientUplink", subGroup: "Structures", requiredDLC: "Anomaly"),

            // Settlements - Abandoned colonies
            new PaletteFilter("mutator_abandoned_outlander", "Abandoned (Outlander)", "Geography_Artificial", "AbandonedColonyOutlander", subGroup: "Settlements"),
            new PaletteFilter("mutator_abandoned_tribal", "Abandoned (Tribal)", "Geography_Artificial", "AbandonedColonyTribal", subGroup: "Settlements"),
            new PaletteFilter("mutator_ancient_infested", "Infested Settlement", "Geography_Artificial", "AncientInfestedSettlement", subGroup: "Settlements", requiredDLC: "Anomaly"),

            // Environmental - Terraforming and hazards
            new PaletteFilter("mutator_ancient_heat_vent", "Heat Vent", "Geography_Artificial", "AncientHeatVent", subGroup: "Environmental"),
            new PaletteFilter("mutator_ancient_smoke_vent", "Smoke Vent", "Geography_Artificial", "AncientSmokeVent", subGroup: "Environmental"),
            new PaletteFilter("mutator_ancient_tox_vent", "Toxic Vent", "Geography_Artificial", "AncientToxVent", subGroup: "Environmental", requiredDLC: "Biotech"),
            new PaletteFilter("mutator_terraforming_scar", "Terraforming Scar", "Geography_Artificial", "TerraformingScar", subGroup: "Environmental"),
            new PaletteFilter("mutator_insect_megahive", "Insect Megahive", "Geography_Artificial", "InsectMegahive", subGroup: "Environmental", requiredDLC: "Anomaly"),
        };

        /// <summary>
        /// Dynamically generates palette filters for mod-added mutators not in static definitions.
        /// Discovers mutators from runtime world scan that aren't covered by static palette.
        /// </summary>
        private static List<PaletteFilter> GetModFiltersPaletteFilters()
        {
            var result = new List<PaletteFilter>();

            // Get all mutators present in the current world
            var runtimeMutators = MapFeatureFilter.GetRuntimeMutators().ToHashSet();
            if (runtimeMutators.Count == 0)
                return result; // No world loaded yet

            // Collect all mutator defNames already defined in static palette
            var staticMutators = new HashSet<string>();
            foreach (var filter in GetClimatePaletteFilters().Concat(GetGeographyNaturalPaletteFilters())
                .Concat(GetGeographyResourcesPaletteFilters()).Concat(GetGeographyArtificialPaletteFilters()))
            {
                if (filter.Kind == FilterKind.Mutator && !string.IsNullOrEmpty(filter.MutatorDefName))
                    staticMutators.Add(filter.MutatorDefName!);
            }

            // Also exclude mutators that are represented by Container filters (popup UI)
            // These have their own UI and shouldn't appear as separate chips in Mod_Filters
            var containerRepresentedMutators = new HashSet<string>
            {
                "AnimalHabitat",  // Represented by ContainerType.AnimalHabitat
                "PlantGrove",     // Represented by ContainerType.PlantGrove
                "MineralRich",    // Represented by ContainerType.MineralRich
                "Stockpile",      // Represented by ContainerType.Stockpiles
            };

            // Find runtime mutators not in static palette and not Container-represented
            var modMutators = runtimeMutators
                .Except(staticMutators)
                .Except(containerRepresentedMutators)
                .OrderBy(m => m).ToList();

            foreach (var defName in modMutators)
            {
                // Use overlay if available, otherwise generate from defName
                string label;
                string subGroup;
                string? tooltipBrief = null;

                if (MutatorOverlays.TryGetValue(defName, out var overlay))
                {
                    label = overlay.Label;
                    subGroup = overlay.SubGroup;
                    tooltipBrief = overlay.TooltipBrief;
                }
                else
                {
                    // Auto-generate label from defName: "GL_RiverDelta" → "GL River Delta"
                    label = System.Text.RegularExpressions.Regex.Replace(defName, "([a-z])([A-Z])", "$1 $2")
                        .Replace("_", " ");
                    subGroup = "Other";
                }

                // Create mutator palette filter
                var paletteId = $"mutator_{defName.ToLowerInvariant()}";
                result.Add(new PaletteFilter(paletteId, label, "Mod_Filters", defName,
                    tooltipBrief: tooltipBrief, subGroup: subGroup));
            }

            return result;
        }

        /// <summary>
        /// Checks if a mutator is available in the current runtime (world loaded, mod provides it).
        /// Used to hide unavailable filters from palette.
        /// </summary>
        private static bool IsMutatorAvailable(string? mutatorDefName)
        {
            if (string.IsNullOrEmpty(mutatorDefName))
                return true; // Not a mutator filter

            return MapFeatureFilter.IsRuntimeMutator(mutatorDefName!);
        }

        /// <summary>
        /// Filters a list of palette filters, removing filters not available in current runtime.
        /// Checks: (1) mutator availability, (2) required DLC, (3) required mod.
        /// </summary>
        private static List<PaletteFilter> FilterByRuntime(List<PaletteFilter> filters)
        {
            return filters.Where(f => IsFilterAvailable(f)).ToList();
        }

        /// <summary>
        /// Checks if a filter is available in the current runtime.
        /// Validates: mutator existence, DLC requirements, and mod requirements.
        /// </summary>
        private static bool IsFilterAvailable(PaletteFilter filter)
        {
            // Check DLC requirement
            if (!string.IsNullOrEmpty(filter.RequiredDLC))
            {
                if (!DLCDetectionService.IsDLCAvailable(filter.RequiredDLC!))
                    return false;
            }

            // Check mod requirement
            if (!string.IsNullOrEmpty(filter.RequiredMod))
            {
                if (!DLCDetectionService.IsModActive(filter.RequiredMod!))
                    return false;
            }

            // Check mutator availability (for mutator-type filters only)
            if (filter.Kind == FilterKind.Mutator)
            {
                if (!IsMutatorAvailable(filter.MutatorDefName))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Resets the workspace to reload from settings.
        /// </summary>
        public static void ResetWorkspace()
        {
            _workspace = null;
            _paletteFilterLookup = null;
        }
    }
}

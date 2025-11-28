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

        // Collapsed category state
        private static HashSet<string> _collapsedCategories = new();

        // Performance warnings cache
        private static readonly HashSet<string> HeavyFilterIds = new()
        {
            "growing_days", "graze", "forageable_food"
        };

        // Tile estimate cache
        private static string _cachedTileEstimate = "";
        private static int _lastEstimateFrame = -1;

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

            // Classic View toggle (right side)
            var classicViewRect = new Rect(rect.xMax - 100f, buttonY, 94f, buttonHeight);
            if (Widgets.ButtonText(classicViewRect, "LandingZone_Workspace_ClassicView".Translate()))
            {
                // Switch to classic view (uses shared state with parent partial class)
                _useWorkspaceMode = false;
            }
            TooltipHandler.TipRegion(classicViewRect, "LandingZone_Workspace_ClassicViewTooltip".Translate());
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
                    }
                    else if (differentClause && targetClauseId.HasValue)
                    {
                        // Move to different clause within same bucket
                        _workspace.MoveChipToClause(_draggedChipId, targetClauseId.Value);
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
                            }
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

            // Scrollable filter list
            var scrollRect = new Rect(contentRect.x, contentRect.y + 55f, contentRect.width, contentRect.height - 55f);
            float contentHeight = 800f; // Approximate - calculated based on filters
            var viewRect = new Rect(0f, 0f, scrollRect.width - 16f, contentHeight);

            Widgets.BeginScrollView(scrollRect, ref _paletteScrollPosition, viewRect);

            var listing = new Listing_Standard { ColumnWidth = viewRect.width };
            listing.Begin(viewRect);

            // Draw each category
            DrawPaletteCategory(listing, filters, "Climate", GetClimatePaletteFilters());
            DrawPaletteCategory(listing, filters, "Geography", GetGeographyPaletteFilters());
            DrawPaletteCategory(listing, filters, "Resources", GetResourcesPaletteFilters());
            DrawPaletteCategory(listing, filters, "Features", GetFeaturesPaletteFilters());

            listing.End();
            Widgets.EndScrollView();
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

            // Draw each filter
            foreach (var pf in paletteFilters)
            {
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

                // Filter label
                var filterLabelRect = new Rect(filterRect.x + 4f, filterRect.y, filterRect.width - 80f, filterRect.height);
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

                // "Add to..." button or current bucket indicator
                var buttonRect = new Rect(filterRect.xMax - 70f, filterRect.y + 2f, 66f, filterRect.height - 4f);

                if (isInWorkspace)
                {
                    // Show current bucket and allow click to remove
                    var bucket = BucketWorkspace.AllBuckets.FirstOrDefault(b => b.Importance == importance);
                    GUI.color = bucket?.Color ?? Color.white;
                    if (Widgets.ButtonText(buttonRect, bucket?.Label ?? "?", drawBackground: true))
                    {
                        // Click to remove from workspace
                        _workspace?.RemoveChip(pf.Id);
                        _cachedTileEstimate = ""; // Invalidate estimate
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
        /// Draws the four importance buckets.
        /// </summary>
        private static void DrawBuckets(Rect rect, FilterSettings filters)
        {
            var contentRect = rect;

            // Legend at top
            var legendRect = new Rect(contentRect.x, contentRect.y, contentRect.width, LegendHeight);
            DrawLegend(legendRect);

            // Buckets
            float bucketsY = legendRect.yMax + 8f;
            float bucketAreaHeight = contentRect.height - LegendHeight - LogicSummaryHeight - 24f;
            float singleBucketHeight = (bucketAreaHeight - 24f) / 4f; // 4 buckets with gaps

            float y = bucketsY;
            foreach (var bucket in BucketWorkspace.AllBuckets)
            {
                var bucketRect = new Rect(contentRect.x, y, contentRect.width, singleBucketHeight);
                DrawBucket(bucketRect, bucket, filters);
                y += singleBucketHeight + 6f;
            }

            // Logic summary at bottom
            var summaryRect = new Rect(contentRect.x, contentRect.yMax - LogicSummaryHeight - 8f, contentRect.width, LogicSummaryHeight);
            DrawLogicSummary(summaryRect);
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
        /// Draws a single importance bucket.
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

            // "+ Add OR condition" button (small, unobtrusive) - only show if bucket has content
            bool bucketHasContent = clauses.Any(c => !c.IsEmpty);
            if (bucketHasContent)
            {
                var addOrRect = new Rect(rect.xMax - 110f, rect.y + 18f, 100f, 16f);
                Text.Font = GameFont.Tiny;
                if (Widgets.ButtonText(addOrRect, "LandingZone_Workspace_AddOrCondition".Translate(), drawBackground: true))
                {
                    _workspace?.AddClause(bucket.Importance);
                    _cachedTileEstimate = ""; // Invalidate estimate
                }
                Text.Font = GameFont.Small;
                TooltipHandler.TipRegion(addOrRect, "LandingZone_Workspace_AddOrConditionTooltip".Translate());
            }

            // Toolbar row with Group/Ungroup buttons
            var toolbarRect = new Rect(rect.x + 8f, rect.y + 34f, rect.width - 16f, BucketToolbarHeight);
            DrawBucketToolbar(toolbarRect, bucket.Importance);

            // Clauses area
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
            float maxWidth = rect.width;

            foreach (var item in items)
            {
                if (item is BucketWorkspace.OrGroup group)
                {
                    float groupWidth = DrawOrGroupChip(new Rect(x, y, 0, ChipHeight), group, importance, clause.ClauseId);

                    x += groupWidth + ChipPadding;
                    if (x > rect.xMax - 50f)
                    {
                        x = rect.x;
                        y += ChipHeight + ChipPadding;
                    }
                }
                else if (item is BucketWorkspace.FilterChip chip)
                {
                    float chipWidth = DrawSingleChip(new Rect(x, y, 0, ChipHeight), chip, importance, clause.ClauseId);

                    x += chipWidth + ChipPadding;
                    if (x > rect.xMax - 50f)
                    {
                        x = rect.x;
                        y += ChipHeight + ChipPadding;
                    }
                }
            }
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

            // Calculate width based on label
            float textWidth = Text.CalcSize(chip.Label).x;
            float chipWidth = textWidth + 24f;
            if (chip.IsHeavy) chipWidth += 20f;

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
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(contentRect, chip.Label);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // Handle mouse interactions
            Event evt = Event.current;
            if (Mouse.IsOver(chipRect))
            {
                // Mouse down - start drag or handle click
                if (evt.type == EventType.MouseDown && evt.button == 0)
                {
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
                    _workspace?.RemoveChip(chip.FilterId);
                    _selectedChipIds.Remove(chip.FilterId);
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
                // Mouse down - select or start drag
                if (evt.type == EventType.MouseDown && evt.button == 0)
                {
                    _dragStartPos = evt.mousePosition;
                    HandleOrGroupSelection(group.GroupId, importance, clauseId);
                    evt.Use();
                }

                // Check for drag initiation
                if (evt.type == EventType.MouseDrag && evt.button == 0 && !_isDragging)
                {
                    float dragDistance = Vector2.Distance(_dragStartPos, evt.mousePosition);
                    if (dragDistance > 5f)
                    {
                        StartDragOrGroup(group.GroupId, importance, clauseId, evt.mousePosition);
                    }
                }

                // Right-click to ungroup
                if (evt.type == EventType.MouseDown && evt.button == 1)
                {
                    HandleOrGroupSelection(group.GroupId, importance, clauseId);
                    UngroupSelected();
                    evt.Use();
                }
            }

            // Tooltip
            TooltipHandler.TipRegion(chipRect, "LandingZone_Workspace_OrGroup_DragTooltip".Translate());

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

            SyncFilterToWorkspace(filters, "average_temperature", "Avg Temp", filters.AverageTemperatureImportance, false, "Climate");
            SyncFilterToWorkspace(filters, "minimum_temperature", "Min Temp", filters.MinimumTemperatureImportance, false, "Climate");
            SyncFilterToWorkspace(filters, "maximum_temperature", "Max Temp", filters.MaximumTemperatureImportance, false, "Climate");
            SyncFilterToWorkspace(filters, "rainfall", "Rainfall", filters.RainfallImportance, false, "Climate");
            SyncFilterToWorkspace(filters, "growing_days", "Growing Days", filters.GrowingDaysImportance, true, "Climate");
            SyncFilterToWorkspace(filters, "pollution", "Pollution", filters.PollutionImportance, false, "Climate");

            SyncFilterToWorkspace(filters, "coastal", "Ocean Coastal", filters.CoastalImportance, false, "Geography");
            SyncFilterToWorkspace(filters, "coastal_lake", "Lake Coastal", filters.CoastalLakeImportance, false, "Geography");
            SyncFilterToWorkspace(filters, "elevation", "Elevation", filters.ElevationImportance, false, "Geography");
            SyncFilterToWorkspace(filters, "movement_difficulty", "Move Difficulty", filters.MovementDifficultyImportance, false, "Geography");
            SyncFilterToWorkspace(filters, "swampiness", "Swampiness", filters.SwampinessImportance, false, "Geography");

            SyncFilterToWorkspace(filters, "forageability", "Forageability", filters.ForageImportance, false, "Resources");
            SyncFilterToWorkspace(filters, "graze", "Grazeable", filters.GrazeImportance, true, "Resources");
            SyncFilterToWorkspace(filters, "animal_density", "Animal Density", filters.AnimalDensityImportance, false, "Resources");
            SyncFilterToWorkspace(filters, "fish_population", "Fish Population", filters.FishPopulationImportance, false, "Resources");
            SyncFilterToWorkspace(filters, "plant_density", "Plant Density", filters.PlantDensityImportance, false, "Resources");

            SyncFilterToWorkspace(filters, "landmark", "Landmarks", filters.LandmarkImportance, false, "Features");
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
        /// Syncs FilterSettings from workspace state.
        /// </summary>
        private static void SyncSettingsFromWorkspace(FilterSettings filters)
        {
            if (_workspace == null) return;

            // Update each filter's importance based on workspace state
            filters.AverageTemperatureImportance = _workspace.GetChipImportance("average_temperature") ?? FilterImportance.Ignored;
            filters.MinimumTemperatureImportance = _workspace.GetChipImportance("minimum_temperature") ?? FilterImportance.Ignored;
            filters.MaximumTemperatureImportance = _workspace.GetChipImportance("maximum_temperature") ?? FilterImportance.Ignored;
            filters.RainfallImportance = _workspace.GetChipImportance("rainfall") ?? FilterImportance.Ignored;
            filters.GrowingDaysImportance = _workspace.GetChipImportance("growing_days") ?? FilterImportance.Ignored;
            filters.PollutionImportance = _workspace.GetChipImportance("pollution") ?? FilterImportance.Ignored;

            filters.CoastalImportance = _workspace.GetChipImportance("coastal") ?? FilterImportance.Ignored;
            filters.CoastalLakeImportance = _workspace.GetChipImportance("coastal_lake") ?? FilterImportance.Ignored;
            filters.ElevationImportance = _workspace.GetChipImportance("elevation") ?? FilterImportance.Ignored;
            filters.MovementDifficultyImportance = _workspace.GetChipImportance("movement_difficulty") ?? FilterImportance.Ignored;
            filters.SwampinessImportance = _workspace.GetChipImportance("swampiness") ?? FilterImportance.Ignored;

            filters.ForageImportance = _workspace.GetChipImportance("forageability") ?? FilterImportance.Ignored;
            filters.GrazeImportance = _workspace.GetChipImportance("graze") ?? FilterImportance.Ignored;
            filters.AnimalDensityImportance = _workspace.GetChipImportance("animal_density") ?? FilterImportance.Ignored;
            filters.FishPopulationImportance = _workspace.GetChipImportance("fish_population") ?? FilterImportance.Ignored;
            filters.PlantDensityImportance = _workspace.GetChipImportance("plant_density") ?? FilterImportance.Ignored;

            filters.LandmarkImportance = _workspace.GetChipImportance("landmark") ?? FilterImportance.Ignored;
        }

        // ============================================================================
        // PALETTE FILTER DEFINITIONS
        // ============================================================================

        private class PaletteFilter
        {
            public string Id { get; }
            public string Label { get; }
            public string Category { get; }
            public bool IsHeavy { get; }
            public string? ValueDisplay { get; }

            public PaletteFilter(string id, string label, string category, bool isHeavy = false, string? valueDisplay = null)
            {
                Id = id;
                Label = label;
                Category = category;
                IsHeavy = isHeavy;
                ValueDisplay = valueDisplay;
            }
        }

        private static List<PaletteFilter> GetClimatePaletteFilters() => new()
        {
            new PaletteFilter("average_temperature", "Average Temperature", "Climate"),
            new PaletteFilter("minimum_temperature", "Minimum Temperature", "Climate"),
            new PaletteFilter("maximum_temperature", "Maximum Temperature", "Climate"),
            new PaletteFilter("rainfall", "Rainfall", "Climate"),
            new PaletteFilter("growing_days", "Growing Days", "Climate", isHeavy: true),
            new PaletteFilter("pollution", "Pollution", "Climate"),
        };

        private static List<PaletteFilter> GetGeographyPaletteFilters() => new()
        {
            // Note: hilliness is multi-select (HashSet), not importance-based - use Classic view
            new PaletteFilter("coastal", "Ocean Coastal", "Geography"),
            new PaletteFilter("coastal_lake", "Lake Coastal", "Geography"),
            new PaletteFilter("water_access", "Water Access", "Geography"),
            new PaletteFilter("elevation", "Elevation", "Geography"),
            new PaletteFilter("movement_difficulty", "Movement Difficulty", "Geography"),
            new PaletteFilter("swampiness", "Swampiness", "Geography"),
        };

        private static List<PaletteFilter> GetResourcesPaletteFilters() => new()
        {
            new PaletteFilter("forageability", "Forageability", "Resources"),
            // Note: forageable_food requires food type selection - use Classic view
            new PaletteFilter("graze", "Grazeable", "Resources", isHeavy: true),
            new PaletteFilter("animal_density", "Animal Density", "Resources"),
            new PaletteFilter("fish_population", "Fish Population", "Resources"),
            new PaletteFilter("plant_density", "Plant Density", "Resources"),
        };

        private static List<PaletteFilter> GetFeaturesPaletteFilters() => new()
        {
            new PaletteFilter("landmark", "Landmarks", "Features"),
            // Note: caves/mountain are MapFeature mutators - use Classic view for full feature control
        };

        /// <summary>
        /// Resets the workspace to reload from settings.
        /// </summary>
        public static void ResetWorkspace()
        {
            _workspace = null;
        }
    }
}

#nullable enable
using HarmonyLib;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LandingZone.Core.Diagnostics;
using LandingZone.Core.Filtering;
using LandingZone.Data;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LandingZone.Core
{
    /// <summary>
    /// Central access point for runtime services (state, harmony, logging).
    /// </summary>
    public static class LandingZoneContext
    {
        private static readonly object Gate = new object();

        public static GameState State { get; private set; } = null!;
        public static Harmony HarmonyInstance { get; private set; } = null!;
        public static FilterService Filters { get; private set; } = null!;
        public static Highlighting.HighlightService Highlighter { get; private set; } = null!;
        public static Highlighting.HighlightState HighlightState => Highlighter?.State ?? _fallbackState;
        private static readonly Highlighting.HighlightState _fallbackState = new Highlighting.HighlightState();

        private static FilterSelectivityEstimator? _selectivityEstimator;
        public static FilterSelectivityEstimator SelectivityEstimator
        {
            get
            {
                if (_selectivityEstimator == null)
                {
                    _selectivityEstimator = new FilterSelectivityEstimator();
                    _selectivityEstimator.Initialize();
                }
                return _selectivityEstimator;
            }
        }

        public static void Initialize(GameState state, Harmony harmony)
        {
            lock (Gate)
            {
                State = state;
                HarmonyInstance = harmony;
                Filters = FilterService.CreateDefault();
                Highlighter = Highlighting.HighlightService.Create(Filters);
            }
        }

        private static FilterService.FilterEvaluationJob? _activeJob;
        private static bool _focusAfterEvaluation;
        private static int _currentMatchIndex;
        private static string _lastMessage = string.Empty;
        private static readonly Dictionary<int, MatchBreakdown> BreakdownCache = new();
        private static readonly Dictionary<int, int> RankCache = new();
        public static IReadOnlyList<TileScore> LatestResults { get; private set; } = System.Array.Empty<TileScore>();
        public static float EvaluationProgress { get; private set; }

        /// <summary>
        /// True if the last evaluation returned 0 results (gates blocked all tiles).
        /// User can choose to run a relaxed search.
        /// </summary>
        public static bool LastSearchWasEmpty { get; private set; }

        /// <summary>
        /// True if the current results are from a relaxed search (MustHave demoted to Priority).
        /// </summary>
        public static bool IsRelaxedSearchResult { get; private set; }

        /// <summary>
        /// Original MustHave/MustNotHave requirements captured before relaxed search.
        /// Used to show which requirements each tile satisfies in the results.
        /// </summary>
        public static List<Data.OriginalRequirement>? OriginalRequirements { get; private set; }

        /// <summary>
        /// Cache of relaxed match info per tile (computed on demand).
        /// Key is tile ID, value is the match analysis.
        /// </summary>
        private static Dictionary<int, Data.RelaxedMatchInfo> _relaxedMatchCache = new Dictionary<int, Data.RelaxedMatchInfo>();

        /// <summary>
        /// Gets relaxed match info for a specific tile.
        /// Returns null if not a relaxed search or if requirements can't be evaluated.
        /// </summary>
        public static Data.RelaxedMatchInfo? GetRelaxedMatchInfo(int tileId)
        {
            if (!IsRelaxedSearchResult || OriginalRequirements == null || OriginalRequirements.Count == 0)
                return null;

            if (_relaxedMatchCache.TryGetValue(tileId, out var cached))
                return cached;

            // Compute and cache
            var matchInfo = EvaluateTileAgainstOriginalRequirements(tileId);
            _relaxedMatchCache[tileId] = matchInfo;
            return matchInfo;
        }

        /// <summary>
        /// Evaluates a tile against the original requirements to determine which are satisfied.
        /// </summary>
        private static Data.RelaxedMatchInfo EvaluateTileAgainstOriginalRequirements(int tileId)
        {
            var requirements = OriginalRequirements ?? new List<Data.OriginalRequirement>();
            var matchInfo = new Data.RelaxedMatchInfo(tileId, requirements);

            foreach (var req in requirements)
            {
                bool tileHasFeature = CheckTileHasFeature(tileId, req.FilterId);

                // For MustHave: tile should HAVE the feature
                // For MustNotHave: tile should NOT HAVE the feature
                bool satisfied = req.IsMustNotHave ? !tileHasFeature : tileHasFeature;

                if (satisfied)
                    matchInfo.SatisfiedRequirements.Add(req);
                else
                    matchInfo.ViolatedRequirements.Add(req);
            }

            return matchInfo;
        }

        /// <summary>
        /// Checks if a tile has a specific feature based on filter ID.
        /// </summary>
        private static bool CheckTileHasFeature(int tileId, string filterId)
        {
            var worldGrid = Find.WorldGrid;
            if (worldGrid == null) return false;

            var tile = worldGrid[tileId];
            if (tile == null) return false;

            // Handle container items (format: "type:itemName")
            if (filterId.Contains(":"))
            {
                var parts = filterId.Split(':');
                var filterType = parts[0];
                var itemName = parts[1];

                return filterType switch
                {
                    "river" => CheckTileHasRiver(tileId, itemName),
                    "road" => CheckTileHasRoad(tileId, itemName),
                    "stone" => CheckTileHasStone(tileId, itemName),
                    "feature" => CheckTileHasMapFeature(tileId, itemName),
                    "grove" => CheckTileHasPlantGrove(tileId, itemName),
                    "habitat" => CheckTileHasAnimalHabitat(tileId, itemName),
                    "ore" => CheckTileHasOre(tileId, itemName),
                    "stockpile" => CheckTileHasStockpile(tileId, itemName),
                    "adjacent" => CheckTileHasAdjacentBiome(tileId, itemName),
                    _ => false
                };
            }

            // Handle single-value filters
            return CheckSingleValueFilter(tileId, filterId);
        }

        private static bool CheckTileHasRiver(int tileId, string riverName)
        {
            var tile = Find.WorldGrid[tileId];
            var rivers = tile?.Rivers;
            if (rivers == null) return false;
            return rivers.Any(r => r.river?.defName == riverName || r.river?.label == riverName);
        }

        private static bool CheckTileHasRoad(int tileId, string roadName)
        {
            var tile = Find.WorldGrid[tileId];
            var roads = tile?.Roads;
            if (roads == null) return false;
            return roads.Any(r => r.road?.defName == roadName || r.road?.label == roadName);
        }

        private static bool CheckTileHasStone(int tileId, string stoneName)
        {
            var stones = Find.World?.NaturalRockTypesIn(tileId);
            if (stones == null) return false;
            return stones.Any(s => s.defName == stoneName || s.label == stoneName);
        }

        private static bool CheckTileHasMapFeature(int tileId, string featureName)
        {
            // Use MapFeatureFilter to get tile's mutators/features
            var features = Filtering.Filters.MapFeatureFilter.GetTileMapFeatures(tileId);
            return features.Contains(featureName);
        }

        private static bool CheckTileHasPlantGrove(int tileId, string groveName)
        {
            // Plant groves are stored as mutators
            var features = Filtering.Filters.MapFeatureFilter.GetTileMapFeatures(tileId);
            return features.Contains(groveName);
        }

        private static bool CheckTileHasAnimalHabitat(int tileId, string habitatName)
        {
            // Animal habitats are stored as mutators
            var features = Filtering.Filters.MapFeatureFilter.GetTileMapFeatures(tileId);
            return features.Contains(habitatName);
        }

        private static bool CheckTileHasOre(int tileId, string oreName)
        {
            if (State?.MineralStockpileCache != null)
            {
                var ores = State.MineralStockpileCache.GetMineralTypes(tileId);
                return ores?.Contains(oreName) ?? false;
            }
            return false;
        }

        private static bool CheckTileHasStockpile(int tileId, string stockpileType)
        {
            if (State?.MineralStockpileCache != null)
            {
                var stockpiles = State.MineralStockpileCache.GetStockpileTypes(tileId);
                return stockpiles?.Contains(stockpileType) ?? false;
            }
            return false;
        }

        private static bool CheckTileHasAdjacentBiome(int tileId, string biomeName)
        {
            // For adjacent biomes, we'd need to check neighbor tiles
            // This is a simplified check - actual implementation would iterate neighbors
            return false; // TODO: Implement proper neighbor biome check
        }

        private static bool CheckSingleValueFilter(int tileId, string filterId)
        {
            // For single-value filters, we need to check if tile matches the filter criteria
            // The actual range/threshold values are in the original FilterSettings
            // For relaxed search badge display, we'll consider the filter "matched" if
            // the tile would have passed the MustHave gate (which we know it didn't, hence relaxed)
            // This is a simplification - we show it as "violated" for single-value MustHave requirements

            // For MustHave filters that were relaxed, the tile likely doesn't meet the requirement
            // Return false for most single-value filters in relaxed context
            var tile = Find.WorldGrid[tileId];
            return filterId switch
            {
                "coastal" => tile?.PrimaryBiome?.impassable == false && Find.World.CoastDirectionAt(tileId).IsValid,
                "water_access" => Find.World.CoastDirectionAt(tileId).IsValid || (tile?.Rivers?.Any() ?? false),
                _ => false // Most single-value requirements were violated (that's why we relaxed)
            };
        }

        public static bool RefreshWorldCache(bool force = false)
        {
            if (State == null)
                return false;

            var world = Find.World;
            if (world == null)
                return false;

            var grid = world.grid;
            if (grid == null)
                return false;

            var info = world.info;
            var seed = info?.seedString ?? string.Empty;

            // Clear tile cache when world changes
            if (Filters != null)
            {
                Filters.TileCache.ResetIfWorldChanged(seed);
            }

            // Reset selectivity estimator for new world
            _selectivityEstimator = null;

            // Count settleable tiles from game cache
            int settleable = 0;
            for (int i = 0; i < grid.TilesCount; i++)
            {
                var tile = grid[i];
                var biome = tile?.PrimaryBiome;
                if (biome != null && !biome.impassable && !world.Impassable(i))
                    settleable++;
            }

            LogMessage($"World cache ready - tiles: {grid.TilesCount}, settleable: {settleable}");

            return true;
        }

        public static void RefreshDefinitions()
        {
            State?.DefCache.Refresh();
        }

        private static void EnsureCacheReady()
        {
            // No longer needed - game's cache is always ready
            // Just ensure Filters TileCache is cleared if world changed
            if (Filters != null && Find.World?.info != null)
            {
                Filters.TileCache.ResetIfWorldChanged(Find.World.info.seedString ?? string.Empty);
            }
        }

        public static bool IsEvaluating { get; private set; }
        public static float LastEvaluationMs { get; private set; }
        public static int LastEvaluationCount { get; private set; }
        public static string CurrentPhaseDescription { get; private set; } = "";

        // Tile cache precomputation
        public static bool IsTileCachePrecomputing { get; private set; }
        public static float TileCacheProgress { get; private set; }
        public static int TileCacheTotalTiles { get; private set; }
        public static int TileCacheProcessedTiles { get; private set; }

        public static void LogMessage(string message)
        {
            Log.Message($"[LandingZone] {message}");
            _lastMessage = message;
        }

        public static IReadOnlyList<TileScore> EvaluateFilters()
        {
            RequestEvaluation(EvaluationRequestSource.Legacy, focusOnComplete: false);
            return LatestResults;
        }

        /// <summary>
        /// Requests evaluation (heavy filter warning removed - users understand the tradeoffs).
        /// </summary>
        public static void RequestEvaluationWithWarning(EvaluationRequestSource source, bool focusOnComplete)
        {
            // Heavy filter warning dialog removed per user feedback - proceed directly
            RequestEvaluation(source, focusOnComplete);
        }

        public static bool RequestEvaluation(EvaluationRequestSource source, bool focusOnComplete)
        {
            if (Filters == null || State == null)
                return false;

            // Diagnostic: Log evaluation request start
            string contextType = Current.ProgramState == ProgramState.Playing ? "in-game" : "world-gen";
            if (DevDiagnostics.PhaseADiagnosticsEnabled)
            {
                Log.Message($"[LZ][DIAG] RequestEvaluation START: source={source}, context={contextType}, focus={focusOnComplete}, diagEnabled=true, timestamp={System.DateTime.Now:HH:mm:ss.fff}");
            }

            EnsureEvaluationComponent();
            EnsureCacheReady();
            if (_activeJob != null)
            {
                _focusAfterEvaluation |= focusOnComplete;
                LogMessage($"Search already running; queueing focus={focusOnComplete} (source {source}).");
                return true;
            }

            try
            {
                _activeJob = Filters.CreateJob(State);
            }
            catch (System.Exception ex)
            {
                Log.Error($"[LandingZone] Failed to start evaluation: {ex}");
                if (DevDiagnostics.PhaseADiagnosticsEnabled)
                {
                    Log.Error($"[LZ][DIAG] RequestEvaluation FAILED: {ex.Message}");
                }
                _activeJob = null;
                return false;
            }

            _focusAfterEvaluation = focusOnComplete;
            IsEvaluating = true;
            LatestResults = System.Array.Empty<TileScore>();
            LastEvaluationCount = 0;
            EvaluationProgress = 0f;

            // Reset relaxed search flag unless this is a relaxed search
            if (source != EvaluationRequestSource.RelaxedSearch)
            {
                IsRelaxedSearchResult = false;
                OriginalRequirements = null;
                _relaxedMatchCache.Clear();
            }

            LogMessage("Best site search started.");

            // Immediately kick the job once to start scoring phase
            // (tick-driven stepping continues via StepEvaluation calls)
            StepEvaluation();

            return true;
        }

        /// <summary>
        /// Cancels the currently running evaluation job.
        /// </summary>
        public static void CancelEvaluation()
        {
            if (_activeJob != null)
            {
                LogMessage($"Search canceled by user at {(EvaluationProgress * 100f):F0}% progress.");
                _activeJob = null;
                IsEvaluating = false;
                EvaluationProgress = 0f;
                CurrentPhaseDescription = "";
            }
        }

        /// <summary>
        /// Requests a relaxed search that demotes MustHave gates to Priority.
        /// Use when a normal search returns 0 results.
        /// Results will show [RELAXED] badge to indicate they violate original requirements.
        /// IMPORTANT: This method creates a COPY of filters and relaxes the copy.
        /// The user's original filter settings are NEVER mutated.
        /// </summary>
        public static bool RequestRelaxedSearch(bool focusOnComplete)
        {
            if (Filters == null || State == null)
                return false;

            // Clone the user's filters and relax the COPY - never mutate user's settings
            var originalFilters = State.Preferences.GetActiveFilters();

            // Capture original requirements BEFORE relaxing (for [X/Y] badge display)
            OriginalRequirements = originalFilters.GetOriginalRequirements();
            _relaxedMatchCache.Clear(); // Clear cache for new search

            var relaxedFilters = originalFilters.Clone();
            relaxedFilters.RelaxMustHaveGates();

            IsRelaxedSearchResult = true;
            LogMessage($"Starting relaxed search (MustHave demoted to Priority on temporary copy). Tracking {OriginalRequirements.Count} original requirements...");

            // Pass the relaxed copy to evaluation - original filters untouched
            return RequestEvaluationWithFilters(EvaluationRequestSource.RelaxedSearch, focusOnComplete, relaxedFilters);
        }

        /// <summary>
        /// Internal method to request evaluation with explicit filter settings.
        /// Used by RequestRelaxedSearch to avoid mutating user preferences.
        /// </summary>
        private static bool RequestEvaluationWithFilters(EvaluationRequestSource source, bool focusOnComplete, Data.FilterSettings overrideFilters)
        {
            if (Filters == null || State == null)
                return false;

            // Diagnostic: Log evaluation request start
            string contextType = Current.ProgramState == ProgramState.Playing ? "in-game" : "world-gen";
            if (DevDiagnostics.PhaseADiagnosticsEnabled)
            {
                Log.Message($"[LZ][DIAG] RequestEvaluationWithFilters START: source={source}, context={contextType}, focus={focusOnComplete}, usingOverride=true, timestamp={System.DateTime.Now:HH:mm:ss.fff}");
            }

            EnsureEvaluationComponent();
            EnsureCacheReady();
            if (_activeJob != null)
            {
                _focusAfterEvaluation |= focusOnComplete;
                LogMessage($"Search already running; queueing focus={focusOnComplete} (source {source}).");
                return true;
            }

            try
            {
                // Pass override filters to CreateJob - these will be used instead of State.Preferences
                _activeJob = Filters.CreateJob(State, overrideFilters);
            }
            catch (System.Exception ex)
            {
                Log.Error($"[LandingZone] Failed to start evaluation: {ex}");
                if (DevDiagnostics.PhaseADiagnosticsEnabled)
                {
                    Log.Error($"[LZ][DIAG] RequestEvaluationWithFilters FAILED: {ex.Message}");
                }
                _activeJob = null;
                return false;
            }

            _focusAfterEvaluation = focusOnComplete;
            IsEvaluating = true;
            LatestResults = System.Array.Empty<TileScore>();
            LastEvaluationCount = 0;
            EvaluationProgress = 0f;

            // Don't reset relaxed search flag - caller handles this
            LogMessage("Best site search started (with override filters).");

            // Immediately kick the job once to start scoring phase
            StepEvaluation();

            return true;
        }

        internal static void StepEvaluation()
        {
            if (_activeJob == null || Filters == null || State == null)
                return;

            // Diagnostic breadcrumb: log step tick when diagnostics enabled
            if (DevDiagnostics.PhaseADiagnosticsEnabled)
            {
                Log.Message($"[LZ][DIAG] Step tick: cursor={_activeJob.ProcessedTiles}/{_activeJob.TotalTiles}, progress={_activeJob.Progress:P0}");
            }

            int iterations = LandingZoneMod.Instance?.Settings.EvaluationChunkSize ?? 250;
            iterations = Mathf.Max(50, iterations);
            if (_activeJob.Step(iterations))
            {
                CompleteEvaluation(_activeJob);
                _activeJob = null;
            }
            else
            {
                EvaluationProgress = _activeJob.Progress;
                CurrentPhaseDescription = _activeJob.CurrentPhaseDescription;

                // Progress spam only in verbose mode; default logs stay concise
                if (LandingZoneLogger.IsVerbose && Find.TickManager.TicksGame % 500 == 0)
                {
                    LogMessage($"Search progress {(EvaluationProgress * 100f):F1}% - {CurrentPhaseDescription}");
                }
            }
        }

        private static void CompleteEvaluation(FilterService.FilterEvaluationJob job)
        {
            var list = job.Results.ToList();
            LatestResults = list;
            LastEvaluationCount = list.Count;
            LastEvaluationMs = job.ElapsedMs;
            UpdateBreakdowns(list);

            // Diagnostic: Log evaluation completion
            if (DevDiagnostics.PhaseADiagnosticsEnabled)
            {
                Log.Message($"[LZ][DIAG] RequestEvaluation END: results={list.Count}, elapsed_ms={job.ElapsedMs:F0}, timestamp={System.DateTime.Now:HH:mm:ss.fff}");
            }

            // Concise completion summary for diagnostics
            var preset = State?.Preferences?.ActivePreset;
            var modeLabel = State?.Preferences?.Options?.PreferencesUIMode.ToString() ?? "Unknown";
            var presetLabel = preset != null
                ? $"{preset.Id} ({preset.Name})"
                : (State?.Preferences?.Options?.PreferencesUIMode == UIMode.Simple ? "custom_simple" : "advanced");
            LandingZoneLogger.LogStandard(
                $"[LandingZone] Search complete: preset={presetLabel}, mode={modeLabel}, results={list.Count}, best={(list.Count > 0 ? list[0].Score.ToString("F2") : "n/a")}, duration_ms={job.ElapsedMs:F0}"
            );
            // Track empty results for car-builder fallback UI
            LastSearchWasEmpty = list.Count == 0;

            if (list.Count > 0)
            {
                _currentMatchIndex = Mathf.Clamp(_currentMatchIndex, 0, list.Count - 1);
                LogMessage($"Best site score: {list[0].Score:F2} (tile {list[0].TileId})");
            }
            else
            {
                _currentMatchIndex = -1;
                LogMessage("Best site search finished with no matching tiles.");
            }

            if (State != null)
            {
                Highlighter?.Update(State, list);
            }
            IsEvaluating = false;
            EvaluationProgress = 1f;

            if (_focusAfterEvaluation && list.Count > 0)
            {
                FocusTopResult(list);
            }
            _focusAfterEvaluation = false;
        }

        public static void FocusTile(int tileId)
        {
            if (tileId < 0)
                return;

            var worldInterface = Find.WorldInterface;
            var grid = Find.WorldGrid;
            var camera = Find.WorldCameraDriver;
            if (worldInterface == null || grid == null || camera == null)
                return;

            if (tileId >= grid.TilesCount)
                return;

            worldInterface.SelectedTile = tileId;
            camera.JumpTo(grid.GetTileCenter(tileId));
        }

        public static string LastMessage => _lastMessage;

        public static void FocusTopResult(IReadOnlyList<TileScore> results)
        {
            if (results == null || results.Count == 0)
                return;

            var tileId = results[0].TileId;
            _currentMatchIndex = 0;
            FocusTile(tileId);
        }

        public static int CurrentMatchIndex => _currentMatchIndex;

        private static void UpdateBreakdowns(IReadOnlyList<TileScore> results)
        {
            BreakdownCache.Clear();
            RankCache.Clear();
            for (int i = 0; i < results.Count; i++)
            {
                var score = results[i];
                BreakdownCache[score.TileId] = score.Breakdown;
                RankCache[score.TileId] = i;
            }
        }

        public static bool TryGetBreakdown(int tileId, out MatchBreakdown breakdown)
        {
            return BreakdownCache.TryGetValue(tileId, out breakdown);
        }

        public static bool TryGetMatchRank(int tileId, out int rank)
        {
            return RankCache.TryGetValue(tileId, out rank);
        }

        public static bool TryGetBreakdown(RimWorld.Planet.PlanetTile tile, out MatchBreakdown breakdown)
        {
            return TryGetBreakdown((int)tile, out breakdown);
        }

        public static void FocusNextMatch(int direction)
        {
            var matches = LatestResults;
            if (matches == null || matches.Count == 0)
                return;

            _currentMatchIndex = (_currentMatchIndex + direction + matches.Count) % matches.Count;
            var target = matches[_currentMatchIndex];
            FocusTile(target.TileId);
        }

        public static bool HasMatches => LatestResults.Count > 0;
        public static int HighlightedMatchCount => LatestResults.Count;

        public static bool TryGetCurrentMatch(out int rank, out TileScore score)
        {
            var matches = LatestResults;
            if (matches != null && matches.Count > 0)
            {
                _currentMatchIndex = Mathf.Clamp(_currentMatchIndex, 0, matches.Count - 1);
                rank = _currentMatchIndex;
                score = matches[_currentMatchIndex];
                return true;
            }

            rank = -1;
            score = default;
            return false;
        }

        private static void EnsureEvaluationComponent()
        {
            var game = Current.Game;
            if (game == null)
                return;

            if (game.GetComponent<LandingZoneEvaluationComponent>() == null)
            {
                game.components.Add(new LandingZoneEvaluationComponent(game));
            }
        }

        /// <summary>
        /// Starts tile cache precomputation for all tiles in the world.
        /// This is called automatically on world load.
        /// </summary>
        public static void StartTileCachePrecomputation()
        {
            var game = Current.Game;
            if (game == null || Filters == null)
                return;

            // Get or create the precomputation component
            var component = game.GetComponent<TileCachePrecomputationComponent>();
            if (component == null)
            {
                component = new TileCachePrecomputationComponent(game);
                game.components.Add(component);
            }

            // Start precomputation
            component.StartPrecomputation(Filters.TileCache);
            IsTileCachePrecomputing = true;
            TileCacheTotalTiles = Find.WorldGrid?.TilesCount ?? 0;
            LogMessage($"Started background tile cache precomputation for {TileCacheTotalTiles} tiles");
        }

        /// <summary>
        /// Updates tile cache precomputation status.
        /// Called by TileCachePrecomputationComponent.
        /// </summary>
        internal static void UpdateTileCacheStatus(float progress, int processedTiles, bool completed)
        {
            TileCacheProgress = progress;
            TileCacheProcessedTiles = processedTiles;

            if (completed && IsTileCachePrecomputing)
            {
                IsTileCachePrecomputing = false;
                LogMessage($"Tile cache precomputation complete: {processedTiles} tiles cached");
            }
        }
    }
}

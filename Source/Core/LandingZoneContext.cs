using HarmonyLib;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

        public static bool RequestEvaluation(EvaluationRequestSource source, bool focusOnComplete)
        {
            if (Filters == null || State == null)
                return false;

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
                _activeJob = null;
                return false;
            }

            _focusAfterEvaluation = focusOnComplete;
            IsEvaluating = true;
            LatestResults = System.Array.Empty<TileScore>();
            LastEvaluationCount = 0;
            EvaluationProgress = 0f;
            LogMessage("Best site search started.");
            return true;
        }

        internal static void StepEvaluation()
        {
            if (_activeJob == null || Filters == null || State == null)
                return;

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
                if (Find.TickManager.TicksGame % 500 == 0)
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

            Highlighter?.Update(State, list);
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

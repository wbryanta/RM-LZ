using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LandingZone.Data;
using UnityEngine;
using Verse;

namespace LandingZone.Core.Filtering
{
    public sealed class FilterService
    {
        private readonly SiteFilterRegistry _registry = new SiteFilterRegistry();
        private readonly List<TileScore> _lastScores = new();
        private readonly TileDataCache _tileCache = new TileDataCache();

        private FilterService()
        {
        }

        public static FilterService CreateDefault()
        {
            var service = new FilterService();
            service.RegisterDefaultFilters();
            return service;
        }

        /// <summary>
        /// Gets the tile data cache for accessing expensive tile properties.
        /// </summary>
        public TileDataCache TileCache => _tileCache;

        private void RegisterDefaultFilters()
        {
            _registry.Register(new Filters.BiomeFilter());
            _registry.Register(new Filters.TemperatureFilter());
            _registry.Register(new Filters.RainfallFilter());
            _registry.Register(new Filters.CoastalFilter());
            _registry.Register(new Filters.RiverFilter());
        }

        public FilterEvaluationJob CreateJob(GameState state)
        {
            return new FilterEvaluationJob(this, state);
        }

        public IReadOnlyList<TileScore> Evaluate(GameState state)
        {
            var grid = Find.World?.grid;
            if (grid == null)
            {
                _lastScores.Clear();
                return _lastScores;
            }

            // Reset cache if world changed
            _tileCache.ResetIfWorldChanged(state.WorldSnapshot.SeedString);

            var tiles = _registry.ApplyAll(state, grid.TilesCount).ToList();
            _lastScores.Clear();

            var profile = state.BestSiteProfile;
            var filters = state.Preferences.Filters;
            profile.RequireCoastal = filters.CoastalImportance == FilterImportance.Critical;
            profile.PreferCoastal = filters.CoastalImportance == FilterImportance.Preferred;
            profile.RequireRiver = filters.RiverImportance == FilterImportance.Critical;
            profile.PreferRiver = filters.RiverImportance == FilterImportance.Preferred;
            profile.RequireFeature = !string.IsNullOrEmpty(filters.RequiredFeatureDefName) && filters.FeatureImportance == FilterImportance.Critical;
            profile.PreferredTemperature = filters.TemperatureRange;
            profile.PreferredRainfall = filters.RainfallRange;
            var maxResults = Mathf.Clamp(filters.MaxResults, 1, FilterSettings.MaxResultsLimit);
            foreach (var tileId in tiles)
            {
                if (!state.WorldSnapshot.TryGetInfo(tileId, out var info))
                    continue;

                var result = BuildTileScore(info, profile, filters, tileId, _tileCache);
                if (result.Score <= 0f)
                    continue;

                InsertTopResult(_lastScores, result, maxResults);
            }

            _lastScores.Sort((a, b) => b.Score.CompareTo(a.Score));
            LandingZoneContext.LogMessage($"Filter evaluation produced {_lastScores.Count} tiles (cache entries: {_tileCache.CachedCount}).");
            return _lastScores;
        }

        private static TileScore BuildTileScore(WorldSnapshot.TileInfo info, BestSiteProfile profile, FilterSettings filters, int tileId, TileDataCache cache)
        {
            // PHASE 1: Apply cheap filters first (from basic TileInfo)
            var score = 1f;
            if (!ApplyRangeConstraint(info.Temperature, filters.TemperatureRange, filters.TemperatureImportance, 5f, 500f, 0.5f, ref score, out var tempScore)) return default;
            if (!ApplyRangeConstraint(info.Rainfall, filters.RainfallRange, filters.RainfallImportance, 200f, 500f, 0.5f, ref score, out var rainScore)) return default;
            if (!ApplyBooleanPreference(info.IsCoastal, filters.CoastalImportance, 0.4f, ref score, out var hasCoastal)) return default;
            if (!ApplyBooleanPreference(info.HasRiver, filters.RiverImportance, 0.2f, ref score, out var hasRiver)) return default;
            if (!filters.AllowedHilliness.Contains(info.Hilliness)) return default;

            bool featureConfigured = !string.IsNullOrEmpty(filters.RequiredFeatureDefName);
            bool featureMatched = featureConfigured && info.FeatureDef != null && info.FeatureDef.defName == filters.RequiredFeatureDefName;
            if (featureConfigured)
            {
                if (!ApplyDiscretePreference(featureMatched, filters.FeatureImportance, 0.1f, ref score))
                    return default;
            }

            // PHASE 2: Fetch expensive properties ONLY for tiles that passed cheap filters
            // This is the key optimization - we only compute expensive data for ~5-10% of tiles
            var extended = cache.GetOrCompute(tileId);

            if (!ApplyRangeConstraint(extended.GrowingDays, filters.GrowingDaysRange, filters.GrowingDaysImportance, 2f, 20f, 1f, ref score, out var growScore)) return default;
            if (!ApplyRangeConstraint(extended.Pollution, filters.PollutionRange, filters.PollutionImportance, 0.02f, 0.05f, 2f, ref score, out var pollutionScore)) return default;
            if (!ApplyRangeConstraint(extended.Forageability, filters.ForageabilityRange, filters.ForageImportance, 0.02f, 0.1f, 2f, ref score, out var forageScore)) return default;
            if (!ApplyRangeConstraint(extended.MovementDifficulty, filters.MovementDifficultyRange, filters.MovementDifficultyImportance, 0.2f, 1f, 0.5f, ref score, out var movementScore)) return default;
            if (!ApplyBooleanPreference(extended.CanGrazeNow, filters.GrazeImportance, 0.15f, ref score, out var canGraze)) return default;

            int stoneMatches = 0;
            int requiredStones = filters.RequiredStoneDefNames.Count;
            if (requiredStones > 0)
            {
                if (extended.StoneDefNames != null)
                {
                    foreach (var requiredStone in filters.RequiredStoneDefNames)
                    {
                        if (extended.StoneDefNames.Contains(requiredStone))
                        {
                            stoneMatches++;
                        }
                    }
                }

                int missing = requiredStones - stoneMatches;
                if (filters.StoneImportance == FilterImportance.Critical && missing > 0)
                    return default;
                if (filters.StoneImportance == FilterImportance.Preferred && missing > 0)
                {
                    score -= missing * 0.05f;
                }
            }

            bool featureRequired = featureConfigured;
            bool hillinessAllowed = filters.AllowedHilliness.Contains(info.Hilliness);

            var finalScore = Mathf.Max(score, 0f);
            var breakdown = new MatchBreakdown(
                filters.TemperatureImportance != FilterImportance.Ignored,
                tempScore,
                filters.RainfallImportance != FilterImportance.Ignored,
                rainScore,
                filters.GrowingDaysImportance != FilterImportance.Ignored,
                growScore,
                filters.PollutionImportance != FilterImportance.Ignored,
                pollutionScore,
                filters.ForageImportance != FilterImportance.Ignored,
                forageScore,
                filters.MovementDifficultyImportance != FilterImportance.Ignored,
                movementScore,
                filters.CoastalImportance,
                hasCoastal,
                filters.RiverImportance,
                hasRiver,
                filters.FeatureImportance,
                featureMatched,
                filters.RequiredFeatureDefName,
                filters.GrazeImportance,
                canGraze,
                filters.StoneImportance,
                requiredStones,
                stoneMatches,
                hillinessAllowed,
                finalScore);

            return new TileScore(tileId, finalScore, breakdown);
        }

        private static bool ApplyRangeConstraint(float value, FloatRange range, FilterImportance importance, float tolerance, float penaltyScale, float breakdownScale, ref float score, out float normalizedScore)
        {
            normalizedScore = 1f;
            if (importance == FilterImportance.Ignored)
                return true;

            bool within = IsWithinRange(value, range, tolerance);
            if (importance == FilterImportance.Critical && !within)
                return false;

            var penalty = DistancePenalty(value, range, penaltyScale);
            score -= penalty;
            normalizedScore = Mathf.Clamp01(1f - penalty * breakdownScale);
            return true;
        }

        private static bool ApplyBooleanPreference(bool actual, FilterImportance importance, float penalty, ref float score, out bool hasValue)
        {
            hasValue = actual;
            if (importance == FilterImportance.Ignored)
                return true;

            if (importance == FilterImportance.Critical && !actual)
                return false;

            if (importance == FilterImportance.Preferred && !actual)
            {
                score -= penalty;
            }

            return true;
        }

        private static bool ApplyDiscretePreference(bool satisfied, FilterImportance importance, float penalty, ref float score)
        {
            if (importance == FilterImportance.Ignored)
                return true;

            if (importance == FilterImportance.Critical && !satisfied)
                return false;

            if (importance == FilterImportance.Preferred && !satisfied)
            {
                score -= penalty;
            }

            return true;
        }

        private static float DistancePenalty(float value, FloatRange range, float scale)
        {
            if (value >= range.min && value <= range.max)
                return 0f;

            var delta = value < range.min ? range.min - value : value - range.max;
            return delta / Mathf.Max(scale, 0.0001f);
        }

        private static bool IsWithinRange(float value, FloatRange range, float tolerance)
        {
            return value >= range.min - tolerance && value <= range.max + tolerance;
        }

        private static void InsertTopResult(List<TileScore> list, TileScore candidate, int limit)
        {
            if (list.Count < limit)
            {
                list.Add(candidate);
                return;
            }

            int worstIndex = 0;
            float worstScore = list[0].Score;
            for (int i = 1; i < list.Count; i++)
            {
                if (list[i].Score < worstScore)
                {
                    worstScore = list[i].Score;
                    worstIndex = i;
                }
            }

            if (candidate.Score > worstScore)
            {
                list[worstIndex] = candidate;
            }
            else if (Mathf.Approximately(candidate.Score, worstScore))
            {
                // keep both by replacing any earlier entry sharing the score to ensure diversity
                list[worstIndex] = candidate;
            }
        }
        public sealed class FilterEvaluationJob
        {
            private readonly FilterService _owner;
            private readonly GameState _state;
            private readonly FilterSettings _filters;
            private readonly BestSiteProfile _profile;
            private readonly List<int> _tiles;
            private readonly List<TileScore> _best = new();
            private readonly List<TileScore> _results = new();
            private readonly Stopwatch _stopwatch = new Stopwatch();
            private readonly int _maxResults;
            private int _cursor;
            private bool _completed;

            internal FilterEvaluationJob(FilterService owner, GameState state)
            {
                _owner = owner;
                _state = state;
                _filters = state.Preferences.Filters;
                _profile = state.BestSiteProfile;
                var grid = Find.World?.grid ?? throw new System.InvalidOperationException("World grid unavailable.");

                // Reset cache if world changed
                _owner._tileCache.ResetIfWorldChanged(state.WorldSnapshot.SeedString);

                _tiles = owner._registry.ApplyAll(state, grid.TilesCount).ToList();
                _profile.RequireCoastal = _filters.CoastalImportance == FilterImportance.Critical;
                _profile.PreferCoastal = _filters.CoastalImportance == FilterImportance.Preferred;
                _profile.RequireRiver = _filters.RiverImportance == FilterImportance.Critical;
                _profile.PreferRiver = _filters.RiverImportance == FilterImportance.Preferred;
                _profile.RequireFeature = !string.IsNullOrEmpty(_filters.RequiredFeatureDefName) && _filters.FeatureImportance == FilterImportance.Critical;
                _profile.PreferredTemperature = _filters.TemperatureRange;
                _profile.PreferredRainfall = _filters.RainfallRange;
                _maxResults = Mathf.Clamp(_filters.MaxResults, 1, FilterSettings.MaxResultsLimit);
                _stopwatch.Start();
            }

            public bool Step(int iterations)
            {
                if (_completed)
                    return true;

                iterations = Mathf.Max(1, iterations);
                for (int i = 0; i < iterations && _cursor < _tiles.Count; i++)
                {
                    var tileId = _tiles[_cursor++];
                    if (!_state.WorldSnapshot.TryGetInfo(tileId, out var info))
                        continue;

                    var result = BuildTileScore(info, _profile, _filters, tileId, _owner._tileCache);
                    if (result.Score <= 0f)
                        continue;

                    InsertTopResult(_best, result, _maxResults);
                }

                if (_cursor >= _tiles.Count)
                {
                    _best.Sort((a, b) => b.Score.CompareTo(a.Score));
                    _results.Clear();
                    _results.AddRange(_best);
                    _stopwatch.Stop();
                    _completed = true;

                    LandingZoneContext.LogMessage($"Evaluation complete: {_results.Count} matches, {_owner._tileCache.CachedCount} tiles cached.");
                    return true;
                }

                return false;
            }

            public float Progress => _tiles.Count == 0 ? 1f : Mathf.Clamp01(_cursor / (float)_tiles.Count);
            public bool Completed => _completed;
            public IReadOnlyList<TileScore> Results => _results;
            public float ElapsedMs => (float)_stopwatch.Elapsed.TotalMilliseconds;
            public int TotalTiles => _tiles.Count;
            public int ProcessedTiles => _cursor;
        }
    }

    public readonly struct TileScore
    {
        public TileScore(int tileId, float score, MatchBreakdown breakdown)
        {
            TileId = tileId;
            Score = score;
            Breakdown = breakdown;
        }

        public int TileId { get; }
        public float Score { get; }
        public MatchBreakdown Breakdown { get; }
    }
}

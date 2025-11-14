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
        private readonly FilterSelectivityAnalyzer _selectivityAnalyzer = new FilterSelectivityAnalyzer();

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
            // Core filters
            _registry.Register(new Filters.BiomeFilter());

            // Temperature filters
            _registry.Register(new Filters.AverageTemperatureFilter());
            _registry.Register(new Filters.MinimumTemperatureFilter());
            _registry.Register(new Filters.MaximumTemperatureFilter());

            // Climate filters
            _registry.Register(new Filters.RainfallFilter());

            // Geography filters
            _registry.Register(new Filters.CoastalFilter());
            _registry.Register(new Filters.RiverFilter());
            _registry.Register(new Filters.RoadFilter());
            _registry.Register(new Filters.ElevationFilter());
            _registry.Register(new Filters.CoastalLakeFilter());

            // Resource filters
            _registry.Register(new Filters.GrazeFilter());
            _registry.Register(new Filters.ForageableFoodFilter());

            // World features and landmarks (1.6+)
            _registry.Register(new Filters.WorldFeatureFilter());
            _registry.Register(new Filters.LandmarkFilter());
            _registry.Register(new Filters.MapFeatureFilter());
            _registry.Register(new Filters.AdjacentBiomesFilter());
        }

        public FilterEvaluationJob CreateJob(GameState state)
        {
            return new FilterEvaluationJob(this, state);
        }

        /// <summary>
        /// Estimates search complexity and returns warnings for expensive filter combinations.
        /// Returns (estimatedSeconds, warningMessage, shouldWarn).
        /// </summary>
        public (float estimatedSeconds, string warningMessage, bool shouldWarn) EstimateSearchComplexity(GameState state)
        {
            var filters = state.Preferences.Filters;
            var (cheapPredicates, heavyPredicates) = _registry.GetAllPredicates(state);

            int heavyCriticals = heavyPredicates.Count(p => p.Importance == FilterImportance.Critical);
            int heavyPreferreds = heavyPredicates.Count(p => p.Importance == FilterImportance.Preferred);
            int totalHeavy = heavyCriticals + heavyPreferreds;

            // Estimate processing time
            // Base: ~0.1s for cheap filters
            // Each heavy filter adds ~20-60 seconds depending on tile count
            float estimatedSeconds = 0.1f;

            if (totalHeavy > 0)
            {
                // Estimate based on 500-tile chunks taking ~2 seconds each
                // For a typical world (~300k tiles), that's ~600 chunks = ~1200 seconds total
                // But we only process heavy predicates, so: time = (heavyCount * 1200s)
                // This is conservative; actual time depends on filter complexity
                int worldTileCount = Find.World?.grid?.TilesCount ?? 300000;
                int chunkCount = Mathf.CeilToInt(worldTileCount / 500f);

                // Heavy filters are slower (~0.5s per 500-tile chunk)
                estimatedSeconds += chunkCount * 0.5f * totalHeavy;
            }

            // Build warning message
            string warningMessage = "";
            bool shouldWarn = false;

            if (estimatedSeconds > 30f)
            {
                shouldWarn = true;
                int minutes = Mathf.FloorToInt(estimatedSeconds / 60f);
                int seconds = Mathf.FloorToInt(estimatedSeconds % 60f);

                warningMessage = $"This search may take ";
                if (minutes > 0)
                    warningMessage += $"{minutes}m {seconds}s";
                else
                    warningMessage += $"{seconds}s";

                if (totalHeavy > 2)
                {
                    warningMessage += $"\n\n{totalHeavy} expensive filters are enabled.";
                    warningMessage += "\nConsider reducing Critical filters for faster searches.";
                }

                warningMessage += "\n\nContinue anyway?";
            }

            return (estimatedSeconds, warningMessage, shouldWarn);
        }

        // NOTE: Synchronous Evaluate() method removed - dead code path never called in production.
        // Production uses FilterEvaluationJob (async incremental evaluation) instead.
        // See forensic analysis 2025-11-13 for details.

        // NOTE: BuildTileScore method removed - dead code from old scoring system before k-of-n architecture
        // NOTE: Deprecated helper methods removed (ApplyRangeConstraint, ApplyBooleanPreference,
        //       ApplyDiscretePreference, DistancePenalty, IsWithinRange) - only used by deleted Evaluate() method.
        //       See forensic analysis 2025-11-13 for details.

        /// <summary>
        /// Inserts a candidate into top-N results list, maintaining heap invariant.
        /// Used by FilterEvaluationJob for incremental result collection.
        /// </summary>
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

        /// <summary>
        /// Analyzes critical filter selectivity and estimates match likelihood.
        /// Provides warnings and suggestions if the combination is very restrictive.
        /// </summary>
        private void PerformLikelihoodAnalysis(
            List<IFilterPredicate> cheapPredicates,
            List<IFilterPredicate> heavyPredicates,
            GameState state,
            int totalTiles,
            float strictness)
        {
            var context = new FilterContext(state, _tileCache);

            // Combine all critical predicates (cheap + heavy)
            var allCriticals = cheapPredicates
                .Concat(heavyPredicates)
                .Where(p => p.Importance == FilterImportance.Critical)
                .ToList();

            if (allCriticals.Count == 0)
                return;

            Log.Message($"[LandingZone] === Match Likelihood Analysis ===");

            // Analyze selectivity of each critical filter
            var selectivities = _selectivityAnalyzer.AnalyzeCriticals(
                cheapPredicates.Concat(heavyPredicates).ToList(),
                context,
                totalTiles
            );

            // Estimate match likelihood at current strictness
            var likelihood = strictness >= 1.0f
                ? MatchLikelihoodEstimator.EstimateAllCriticals(selectivities)
                : MatchLikelihoodEstimator.EstimateRelaxedCriticals(selectivities, strictness);

            Log.Message($"[LandingZone] {likelihood.GetUserMessage()}");
            Log.Message($"[LandingZone] Details: {likelihood.Description}");

            // Warn if very restrictive
            if (likelihood.Category <= LikelihoodCategory.Low)
            {
                Log.Warning($"[LandingZone] ⚠️ Your {allCriticals.Count} critical filters are very restrictive!");

                // Provide suggestions for different strictness levels
                var suggestions = MatchLikelihoodEstimator.SuggestStrictness(selectivities);

                Log.Message("[LandingZone] Suggestions:");
                foreach (var suggestion in suggestions)
                {
                    if (suggestion.Strictness < strictness && suggestion.Category >= LikelihoodCategory.Medium)
                    {
                        Log.Message($"[LandingZone]   {suggestion.GetDisplayText()}");
                    }
                }
            }

            // If impossible, strongly recommend relaxing
            if (likelihood.Category == LikelihoodCategory.Impossible)
            {
                Log.Error("[LandingZone] 🚨 Your filter combination appears impossible!");
                Log.Error("[LandingZone] 🚨 Consider reducing strictness or moving some filters to Preferred.");
            }

            Log.Message("[LandingZone] ===================================");
        }

        public sealed class FilterEvaluationJob
        {
            private readonly FilterService _owner;
            private readonly GameState _state;
            private readonly List<CandidateTile> _candidates;
            private readonly List<IFilterPredicate> _heavyCriticals;
            private readonly List<IFilterPredicate> _heavyPreferreds;
            private readonly int _totalCriticals;
            private readonly int _totalPreferreds;
            private readonly float _kappa;
            private readonly float _strictness;
            private readonly FilterContext _context;
            private readonly PrecomputedBitsetCache _heavyBitsets = new PrecomputedBitsetCache();
            private readonly int _tileCount;
            private readonly List<TileScore> _best = new();
            private readonly List<TileScore> _results = new();
            private readonly Stopwatch _stopwatch = new Stopwatch();
            private readonly int _maxResults;
            private readonly int _heavyPredicateCount;
            private int _cursor;
            private bool _completed;
            private bool _precomputationComplete;
            private float _minInHeap;
            private float _currentStrictness; // Mutable strictness for auto-relax

            // For membership scoring
            private readonly List<ISiteFilter> _criticalFilters;
            private readonly List<ISiteFilter> _preferredFilters;
            private readonly float[] _criticalWeights;
            private readonly float[] _preferredWeights;

            internal FilterEvaluationJob(FilterService owner, GameState state)
            {
                _owner = owner;
                _state = state;
                var filters = state.Preferences.Filters;
                _strictness = filters.CriticalStrictness;
                _currentStrictness = _strictness; // Initialize current strictness
                _maxResults = Mathf.Clamp(filters.MaxResults, 1, FilterSettings.MaxResultsLimit);

                var grid = Find.World?.grid ?? throw new System.InvalidOperationException("World grid unavailable.");
                _tileCount = grid.TilesCount;

                // Reset cache if world changed
                var worldSeed = Find.World?.info?.seedString ?? string.Empty;
                _owner._tileCache.ResetIfWorldChanged(worldSeed);

                // Log active filter configuration
                LogActiveFilters(filters);

                // STAGE A: Cheap aggregate gate (synchronous - fast enough)
                var (cheapPredicates, heavyPredicates) = owner._registry.GetAllPredicates(state);

                _heavyCriticals = heavyPredicates.Where(p => p.Importance == FilterImportance.Critical).ToList();
                _heavyPreferreds = heavyPredicates.Where(p => p.Importance == FilterImportance.Preferred).ToList();

                int cheapCriticals = cheapPredicates.Count(p => p.Importance == FilterImportance.Critical);
                int cheapPreferreds = cheapPredicates.Count(p => p.Importance == FilterImportance.Preferred);

                _totalCriticals = cheapCriticals + _heavyCriticals.Count;
                _totalPreferreds = cheapPreferreds + _heavyPreferreds.Count;
                _kappa = ScoringWeights.ComputeKappa(_totalCriticals, _totalPreferreds);
                _context = new FilterContext(state, owner._tileCache);

                // Initialize membership scoring data structures
                _criticalFilters = new List<ISiteFilter>();
                _preferredFilters = new List<ISiteFilter>();

                foreach (var filter in owner._registry.Filters)
                {
                    var importance = SiteFilterRegistry.GetFilterImportance(filter.Id, filters);

                    if (importance == FilterImportance.Critical)
                        _criticalFilters.Add(filter);
                    else if (importance == FilterImportance.Preferred)
                        _preferredFilters.Add(filter);
                }

                // For now, use equal weights (rank-based weighting comes in future UI)
                _criticalWeights = Enumerable.Repeat(1f, _criticalFilters.Count).ToArray();
                _preferredWeights = Enumerable.Repeat(1f, _preferredFilters.Count).ToArray();

                Log.Message($"[LandingZone] Membership scoring: {_criticalFilters.Count} critical filters, {_preferredFilters.Count} preferred filters");

                var aggregator = new BitsetAggregator(
                    cheapPredicates,
                    _heavyCriticals.Count,
                    _heavyPreferreds.Count,
                    _context,
                    _tileCount
                );

                _candidates = aggregator.GetCandidates(_strictness, maxCandidates: 25000);

                Log.Message($"[LandingZone] FilterEvaluationJob: Stage A complete, {_candidates.Count} candidates");

                // Start incremental precomputation for all heavy predicates
                _heavyPredicateCount = _heavyCriticals.Count + _heavyPreferreds.Count;
                if (_heavyPredicateCount > 0)
                {
                    Log.Message($"[LandingZone] Starting precomputation for {_heavyPredicateCount} heavy predicates...");
                    foreach (var predicate in _heavyCriticals.Concat(_heavyPreferreds))
                    {
                        _heavyBitsets.StartPrecomputation(predicate, _context, _tileCount);
                    }
                    _precomputationComplete = false;
                }
                else
                {
                    _precomputationComplete = true;
                }

                _stopwatch.Start();
            }

            private static void LogActiveFilters(FilterSettings filters)
            {
                Log.Message($"[LandingZone] === Active Filter Configuration ===");
                Log.Message($"[LandingZone] Strictness: {filters.CriticalStrictness:F2} (1.0 = all criticals required)");
                Log.Message($"[LandingZone] MaxResults: {filters.MaxResults}");

                // Critical filters
                var criticals = new List<string>();
                if (filters.AverageTemperatureImportance == FilterImportance.Critical) criticals.Add($"AvgTemp {filters.AverageTemperatureRange}");
                if (filters.MinimumTemperatureImportance == FilterImportance.Critical) criticals.Add($"MinTemp {filters.MinimumTemperatureRange}");
                if (filters.MaximumTemperatureImportance == FilterImportance.Critical) criticals.Add($"MaxTemp {filters.MaximumTemperatureRange}");
                if (filters.RainfallImportance == FilterImportance.Critical) criticals.Add($"Rainfall {filters.RainfallRange}");
                if (filters.GrowingDaysImportance == FilterImportance.Critical) criticals.Add($"GrowingDays {filters.GrowingDaysRange}");
                if (filters.CoastalImportance == FilterImportance.Critical) criticals.Add("Coastal");
                if (filters.LandmarkImportance == FilterImportance.Critical) criticals.Add("Landmark");
                if (filters.GrazeImportance == FilterImportance.Critical) criticals.Add("Graze");

                if (criticals.Count > 0)
                    Log.Message($"[LandingZone] Critical filters: {string.Join(", ", criticals)}");
                else
                    Log.Message($"[LandingZone] Critical filters: (none)");

                // Preferred filters
                var preferreds = new List<string>();
                if (filters.AverageTemperatureImportance == FilterImportance.Preferred) preferreds.Add("AvgTemp");
                if (filters.MinimumTemperatureImportance == FilterImportance.Preferred) preferreds.Add("MinTemp");
                if (filters.MaximumTemperatureImportance == FilterImportance.Preferred) preferreds.Add("MaxTemp");
                if (filters.RainfallImportance == FilterImportance.Preferred) preferreds.Add("Rainfall");
                if (filters.GrowingDaysImportance == FilterImportance.Preferred) preferreds.Add("GrowingDays");
                if (filters.PollutionImportance == FilterImportance.Preferred) preferreds.Add("Pollution");
                if (filters.ForageImportance == FilterImportance.Preferred) preferreds.Add("Forage");
                if (filters.ForageableFoodImportance == FilterImportance.Preferred) preferreds.Add("ForageableFood");
                if (filters.MovementDifficultyImportance == FilterImportance.Preferred) preferreds.Add("Movement");
                if (filters.ElevationImportance == FilterImportance.Preferred) preferreds.Add("Elevation");
                if (filters.CoastalImportance == FilterImportance.Preferred) preferreds.Add("Coastal");
                if (filters.CoastalLakeImportance == FilterImportance.Preferred) preferreds.Add("CoastalLake");
                if (filters.LandmarkImportance == FilterImportance.Preferred) preferreds.Add("Landmark");
                if (filters.GrazeImportance == FilterImportance.Preferred) preferreds.Add("Graze");
                if (filters.FeatureImportance == FilterImportance.Preferred) preferreds.Add("Feature");

                if (preferreds.Count > 0)
                    Log.Message($"[LandingZone] Preferred filters: {string.Join(", ", preferreds)}");
                else
                    Log.Message($"[LandingZone] Preferred filters: (none)");

                Log.Message($"[LandingZone] =====================================");
            }

            public bool Step(int iterations)
            {
                if (_completed)
                    return true;

                iterations = Mathf.Max(1, iterations);

                // PHASE 1: Precomputation of heavy predicates (if not complete)
                if (!_precomputationComplete)
                {
                    // Process chunks of tiles for each heavy predicate
                    // Reduced from 5000 to 500 to keep UI responsive during expensive operations
                    const int tilesPerChunk = 500;
                    _precomputationComplete = _heavyBitsets.StepPrecomputations(tilesPerChunk);

                    return false; // Not done yet, keep stepping
                }

                // PHASE 2: Heavy evaluation with branch-and-bound pruning
                for (int i = 0; i < iterations && _cursor < _candidates.Count; i++)
                {
                    var candidate = _candidates[_cursor++];

                    // Prune: If upper bound can't beat worst in heap, skip
                    if (_best.Count >= _maxResults && candidate.UpperBound <= _minInHeap)
                        continue;

                    float critScore, prefScore, penalty, finalScore;

                    // Check feature flag for scoring system
                    if (_state.Preferences.Options.UseNewScoring)
                    {
                        // NEW: Membership-based scoring
                        (critScore, prefScore, penalty, finalScore) = ComputeMembershipScoreForTile(candidate.TileId);

                        // Strictness check: critical score must meet threshold
                        if (critScore < _currentStrictness)
                        {
                            Log.Message($"[LandingZone] Tile {candidate.TileId}: [MEMBERSHIP] Failed strictness check " +
                                       $"(critScore={critScore:F2} < {_currentStrictness:F2}, penalty={penalty:F2})");
                            continue;
                        }

                        Log.Message($"[LandingZone] Tile {candidate.TileId}: [MEMBERSHIP] critScore={critScore:F2}, " +
                                   $"prefScore={prefScore:F2}, penalty={penalty:F2}, finalScore={finalScore:F2}");
                    }
                    else
                    {
                        // OLD: k-of-n binary scoring (legacy path)
                        int critHeavyMatches = CountMatches(_heavyCriticals, candidate.TileId);
                        int prefHeavyMatches = CountMatches(_heavyPreferreds, candidate.TileId);

                        int totalCritMatches = candidate.CritCheapMatches + critHeavyMatches;
                        int totalPrefMatches = candidate.PrefCheapMatches + prefHeavyMatches;

                        critScore = ScoringWeights.NormalizeCriticalScore(totalCritMatches, _totalCriticals);
                        prefScore = ScoringWeights.NormalizePreferredScore(totalPrefMatches, _totalPreferreds);

                        // Strictness check
                        if (critScore < _currentStrictness)
                        {
                            Log.Message($"[LandingZone] Tile {candidate.TileId}: [K-OF-N] Failed strictness check " +
                                       $"(crit {totalCritMatches}/{_totalCriticals} = {critScore:F2} < {_currentStrictness:F2})");
                            continue;
                        }

                        finalScore = ScoringWeights.ComputeFinalScore(critScore, prefScore, _kappa);
                        penalty = 1.0f; // No penalty in k-of-n

                        Log.Message($"[LandingZone] Tile {candidate.TileId}: [K-OF-N] critScore={critScore:F2}, prefScore={prefScore:F2}, finalScore={finalScore:F2}");
                    }

                    // Simplified breakdown for now
                    var breakdown = new MatchBreakdown(
                        false, 0f, false, 0f, false, 0f, false, 0f, false, 0f, false, 0f,
                        FilterImportance.Ignored, false,
                        FilterImportance.Ignored, false,
                        FilterImportance.Ignored, false, null,
                        FilterImportance.Ignored, false,
                        FilterImportance.Ignored, 0, 0,
                        true, finalScore
                    );

                    var tileScore = new TileScore(candidate.TileId, finalScore, breakdown);

                    // Insert into Top-N heap
                    InsertTopResult(_best, tileScore, _maxResults);

                    // Update min heap value
                    if (_best.Count >= _maxResults)
                        _minInHeap = _best.Min(r => r.Score);
                }

                // Check if done
                if (_cursor >= _candidates.Count)
                {
                    // Auto-relax: If 0 results and strictness is 1.0, retry with relaxed strictness
                    if (_best.Count == 0 && _currentStrictness >= 1.0f && _totalCriticals >= 2)
                    {
                        Log.Warning("[LandingZone] FilterEvaluationJob: 0 results at strictness 1.0");
                        Log.Warning($"[LandingZone] Auto-relaxing to {_totalCriticals - 1} of {_totalCriticals} criticals and retrying...");

                        float relaxedStrictness = (_totalCriticals - 1) / (float)_totalCriticals;

                        // Reset and retry with relaxed strictness
                        _currentStrictness = relaxedStrictness;
                        _cursor = 0;
                        _best.Clear();
                        _minInHeap = 0f;
                        // Don't clear _heavyBitsets - we want to reuse the cached precomputed bitsets

                        // Continue processing (don't set _completed = true)
                        Log.Message($"[LandingZone] Retrying with relaxed strictness {relaxedStrictness:F2}");
                        return false; // Not done yet, continue processing
                    }

                    _best.Sort((a, b) => b.Score.CompareTo(a.Score));
                    _results.Clear();
                    _results.AddRange(_best);
                    _stopwatch.Stop();
                    _completed = true;

                    if (_results.Count > 0 && _currentStrictness < 1.0f)
                    {
                        int requiredMatches = Mathf.CeilToInt(_totalCriticals * _currentStrictness);
                        Log.Message($"[LandingZone] 💡 No tiles matched all {_totalCriticals} criticals. Showing tiles matching {requiredMatches} of {_totalCriticals}.");
                    }

                    LandingZoneContext.LogMessage($"K-of-N evaluation: {_results.Count} results, {_owner._tileCache.CachedCount} cached");
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Counts how many predicates match a specific tile using cached bitsets.
            /// </summary>
            private int CountMatches(List<IFilterPredicate> predicates, int tileId)
            {
                int matches = 0;
                foreach (var predicate in predicates)
                {
                    // Use cached bitset - evaluates ALL tiles once, then O(1) lookups
                    if (_heavyBitsets.Matches(predicate, tileId, _context, _tileCount))
                        matches++;
                }
                return matches;
            }

            /// <summary>
            /// Computes membership-based score for a tile using continuous fuzzy logic.
            /// </summary>
            private (float critScore, float prefScore, float penalty, float finalScore) ComputeMembershipScoreForTile(int tileId)
            {
                // Collect membership scores
                float[] critMemberships = new float[_criticalFilters.Count];
                for (int i = 0; i < _criticalFilters.Count; i++)
                {
                    critMemberships[i] = _criticalFilters[i].Membership(tileId, _context);
                }

                float[] prefMemberships = new float[_preferredFilters.Count];
                for (int i = 0; i < _preferredFilters.Count; i++)
                {
                    prefMemberships[i] = _preferredFilters[i].Membership(tileId, _context);
                }

                // Compute group scores (weighted averages)
                float critScore = ScoringWeights.ComputeGroupScore(critMemberships, _criticalWeights);
                float prefScore = ScoringWeights.ComputeGroupScore(prefMemberships, _preferredWeights);

                // Compute worst critical for penalty
                float worstCrit = ScoringWeights.ComputeWorstCritical(critMemberships);

                // Default penalty parameters (will be exposed in mod settings later)
                const float alphaPen = 0.1f;  // 10% floor
                const float gammaPen = 2.0f;  // Quadratic punishment
                float penalty = ScoringWeights.ComputePenalty(worstCrit, alphaPen, gammaPen);

                // Compute global weights
                const float critBase = 4.0f;
                const float prefBase = 1.0f;
                const float mutatorWeight = 0.0f; // Mutator scoring comes in LZ-SCORING-005
                var (lambdaC, lambdaP, lambdaMut) = ScoringWeights.ComputeGlobalWeights(
                    _criticalFilters.Count,
                    _preferredFilters.Count,
                    critBase,
                    prefBase,
                    mutatorWeight
                );

                // Compute final membership score
                float mutatorScore = 0.5f; // Neutral baseline until LZ-SCORING-005
                float finalScore = ScoringWeights.ComputeMembershipScore(
                    critScore,
                    prefScore,
                    mutatorScore,
                    penalty,
                    lambdaC,
                    lambdaP,
                    lambdaMut
                );

                return (critScore, prefScore, penalty, finalScore);
            }

            public float Progress
            {
                get
                {
                    if (_candidates.Count == 0)
                        return 1f;

                    // Two-phase progress:
                    // Phase 1 (precomputation): 0% to 50%
                    // Phase 2 (evaluation): 50% to 100%
                    if (!_precomputationComplete)
                    {
                        float precompProgress = _heavyBitsets.GetPrecomputationProgress();
                        return precompProgress * 0.5f; // Map 0-1 to 0-0.5
                    }
                    else
                    {
                        float evalProgress = _cursor / (float)_candidates.Count;
                        return 0.5f + (evalProgress * 0.5f); // Map 0-1 to 0.5-1.0
                    }
                }
            }

            /// <summary>
            /// Gets a human-readable description of the current processing phase.
            /// </summary>
            public string CurrentPhaseDescription
            {
                get
                {
                    if (_completed)
                        return "Complete";

                    if (!_precomputationComplete)
                    {
                        // Phase 1: Precomputing expensive filters
                        var currentFilterId = _heavyBitsets.CurrentPredicateId;
                        if (!string.IsNullOrEmpty(currentFilterId))
                        {
                            // Convert filter ID to user-friendly name
                            string filterName = currentFilterId switch
                            {
                                "SpecificStone" => "Stone types",
                                "StoneCount" => "Stone count",
                                "Cave" => "Cave systems",
                                "Landmark" => "Landmarks",
                                "WorldFeature" => "World features",
                                "MapFeature" => "Map features",
                                "AdjacentBiomes" => "Adjacent biomes",
                                "ForageableFood" => "Forageable food",
                                _ => currentFilterId
                            };
                            return $"Processing {filterName}...";
                        }
                        return "Processing filters...";
                    }
                    else
                    {
                        // Phase 2: Evaluating candidates
                        return $"Evaluating {_candidates.Count} candidates...";
                    }
                }
            }

            public bool Completed => _completed;
            public IReadOnlyList<TileScore> Results => _results;
            public float ElapsedMs => (float)_stopwatch.Elapsed.TotalMilliseconds;
            public int TotalTiles => _candidates.Count;
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

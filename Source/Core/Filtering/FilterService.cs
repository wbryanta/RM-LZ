using System;
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
            _registry.Register(new Filters.CoastalLakeFilter());
            _registry.Register(new Filters.WaterAccessFilter()); // Coastal OR river helper for symmetric water requirements
            _registry.Register(new Filters.RiverFilter());
            _registry.Register(new Filters.RoadFilter());
            _registry.Register(new Filters.ElevationFilter());

            // Resource filters
            _registry.Register(new Filters.GrazeFilter());
            _registry.Register(new Filters.ForageableFoodFilter());
            _registry.Register(new Filters.StoneFilter());
            _registry.Register(new Filters.StockpileFilter());

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
            var filters = state.Preferences.GetActiveFilters();
            var (cheapPredicates, heavyPredicates) = _registry.GetAllPredicates(state);

            int cheapCriticals = cheapPredicates.Count(p => p.Importance == FilterImportance.Critical);
            int heavyCriticals = heavyPredicates.Count(p => p.Importance == FilterImportance.Critical);
            int heavyPreferreds = heavyPredicates.Count(p => p.Importance == FilterImportance.Preferred);
            int totalHeavy = heavyCriticals + heavyPreferreds;

            // Recalibrated estimates based on two-phase filtering architecture:
            // Phase 1 (Apply): Light filters reduce candidates by ~90-95% instantly using game cache
            // Phase 2 (Score): Heavy filters only process survivors (~5-10% of original tiles)

            // Base: Light filters (instant game cache access)
            float estimatedSeconds = 0.5f;

            // Light Critical filters add minimal time (game cache is pre-computed)
            if (cheapCriticals > 0)
            {
                estimatedSeconds += cheapCriticals * 0.2f;
            }

            // Heavy Critical filters process reduced candidate set after Light filtering
            // Typical scenario: 300k tiles → 15k-30k candidates after Light filters
            // Heavy computation: ~3-8 seconds per filter on reduced set
            if (heavyCriticals > 0)
            {
                // Estimate based on typical 95% reduction from Light filters
                int worldTileCount = Find.World?.grid?.TilesCount ?? 300000;
                float estimatedCandidates = worldTileCount * 0.05f; // 5% survive Light filters

                // Each heavy Critical filter adds ~5s for typical worlds (calibrated from real usage)
                float heavyCriticalTime = heavyCriticals * 5.0f;

                // Adjust for world size (larger worlds = more candidates after filtering)
                if (worldTileCount > 400000)
                    heavyCriticalTime *= 1.5f; // +50% for huge worlds
                else if (worldTileCount < 200000)
                    heavyCriticalTime *= 0.7f; // -30% for small worlds

                estimatedSeconds += heavyCriticalTime;
            }

            // Heavy Preferred filters only affect scoring phase (faster than Critical)
            if (heavyPreferreds > 0)
            {
                // Preferred filters score survivors, not filter them
                // Typically 2-3s per Preferred heavy filter
                estimatedSeconds += heavyPreferreds * 2.5f;
            }

            // Penalty for multiple heavy filters (cache misses, compound complexity)
            if (totalHeavy > 3)
            {
                float complexityPenalty = (totalHeavy - 3) * 1.5f;
                estimatedSeconds += complexityPenalty;
            }

            // Build warning message
            string warningMessage = "";
            bool shouldWarn = false;

            // Warn at 15+ seconds (users notice delays beyond this)
            if (estimatedSeconds > 15f)
            {
                shouldWarn = true;
                int minutes = Mathf.FloorToInt(estimatedSeconds / 60f);
                int seconds = Mathf.FloorToInt(estimatedSeconds % 60f);

                warningMessage = $"This search may take ";
                if (minutes > 0)
                    warningMessage += $"{minutes}m {seconds}s";
                else
                    warningMessage += $"{seconds}s";

                if (totalHeavy > 3)
                {
                    warningMessage += $"\n\n{totalHeavy} expensive filters are enabled.";
                    warningMessage += "\nConsider reducing Critical filters for faster results.";
                }
                else if (heavyCriticals > 0)
                {
                    warningMessage += $"\n\n{heavyCriticals} expensive filter(s) need to scan many tiles.";
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

            // Only show likelihood analysis in Standard or Verbose mode
            if (!LandingZoneLogger.IsStandardOrVerbose)
                return;

            LandingZoneLogger.LogStandard($"[LandingZone] === Match Likelihood Analysis ===");

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

            LandingZoneLogger.LogStandard($"[LandingZone] {likelihood.GetUserMessage()}");
            LandingZoneLogger.LogStandard($"[LandingZone] Details: {likelihood.Description}");

            // Warn if very restrictive
            if (likelihood.Category <= LikelihoodCategory.Low)
            {
                LandingZoneLogger.LogWarning($"[LandingZone] ⚠️ Your {allCriticals.Count} critical filters are very restrictive!");

                // Provide suggestions for different strictness levels
                var suggestions = MatchLikelihoodEstimator.SuggestStrictness(selectivities);

                LandingZoneLogger.LogStandard("[LandingZone] Suggestions:");
                foreach (var suggestion in suggestions)
                {
                    if (suggestion.Strictness < strictness && suggestion.Category >= LikelihoodCategory.Medium)
                    {
                        LandingZoneLogger.LogStandard($"[LandingZone]   {suggestion.GetDisplayText()}");
                    }
                }
            }

            // If impossible, strongly recommend relaxing
            if (likelihood.Category == LikelihoodCategory.Impossible)
            {
                LandingZoneLogger.LogError("[LandingZone] 🚨 Your filter combination appears impossible!");
                LandingZoneLogger.LogError("[LandingZone] 🚨 Consider reducing strictness or moving some filters to Preferred.");
            }

            LandingZoneLogger.LogStandard("[LandingZone] ===================================");
        }

        /// <summary>
        /// Gets selectivity data for a specific filter by ID.
        /// Returns null if filter is ignored or not found.
        /// Used by Advanced mode UI to show live feedback.
        /// </summary>
        public FilterSelectivity? GetFilterSelectivity(string filterId, GameState state)
        {
            var (cheap, heavy) = _registry.GetAllPredicates(state);
            var allPredicates = cheap.Concat(heavy);
            var predicate = allPredicates.FirstOrDefault(p => p.Id == filterId);

            if (predicate == null || predicate.Importance == FilterImportance.Ignored)
                return null;

            int totalTiles = Find.WorldGrid?.TilesCount ?? 0;
            if (totalTiles == 0)
                return null;

            var context = new FilterContext(state, _tileCache);
            return _selectivityAnalyzer.AnalyzePredicate(predicate, context, totalTiles);
        }

        /// <summary>
        /// Gets selectivity data for all active (non-ignored) filters.
        /// Used by Advanced mode UI for live feedback panel.
        /// </summary>
        public List<FilterSelectivity> GetAllSelectivities(GameState state)
        {
            var (cheap, heavy) = _registry.GetAllPredicates(state);
            var allPredicates = cheap.Concat(heavy).ToList();
            var results = new List<FilterSelectivity>();

            int totalTiles = Find.WorldGrid?.TilesCount ?? 0;
            if (totalTiles == 0)
                return results;

            var context = new FilterContext(state, _tileCache);

            foreach (var predicate in allPredicates)
            {
                if (predicate.Importance != FilterImportance.Ignored)
                {
                    results.Add(_selectivityAnalyzer.AnalyzePredicate(predicate, context, totalTiles));
                }
            }

            return results;
        }

        public sealed class FilterEvaluationJob
        {
            private readonly FilterService _owner;
            private readonly GameState _state;
            private readonly Data.Preset? _preset; // Track active preset for fallback logic
            private List<CandidateTile> _candidates;
            private readonly List<IFilterPredicate> _heavyCriticals;
            private readonly List<IFilterPredicate> _heavyPreferreds;
            private int _totalCriticals; // Not readonly - updated when fallback tiers activate
            private int _totalPreferreds; // Not readonly - updated when fallback tiers activate
            private float _kappa; // Not readonly - updated when fallback tiers activate
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
            private int _fallbackTierUsed = 0; // 0 = primary, 1+ = fallback tier index

            // For membership scoring
            private readonly List<ISiteFilter> _criticalFilters;
            private readonly List<ISiteFilter> _preferredFilters;
            private float[] _criticalWeights; // Not readonly - updated when fallback tiers activate
            private float[] _preferredWeights; // Not readonly - updated when fallback tiers activate

            internal FilterEvaluationJob(FilterService owner, GameState state)
            {
                _owner = owner;
                _state = state;
                _preset = state.Preferences.ActivePreset; // Store preset for fallback logic
                var filters = state.Preferences.GetActiveFilters();
                _strictness = filters.CriticalStrictness;

                // Apply preset's minimum strictness override if set
                if (_preset?.MinimumStrictness != null)
                {
                    _strictness = Mathf.Max(_strictness, _preset.MinimumStrictness.Value);
                    LandingZoneLogger.LogStandard($"[LandingZone] Preset '{_preset.Id}' enforces MinimumStrictness={_preset.MinimumStrictness:F2}");
                }

                _currentStrictness = _strictness; // Initialize current strictness
                _maxResults = Mathf.Clamp(filters.MaxResults, 1, FilterSettings.MaxResultsLimit);
                var preset = state.Preferences.ActivePreset;
                var modeLabel = state.Preferences.Options.PreferencesUIMode.ToString();
                var presetLabel = preset != null
                    ? $"{preset.Id} ({preset.Name})"
                    : (state.Preferences.Options.PreferencesUIMode == UIMode.Simple ? "custom_simple" : "advanced");

                // Minimal/Standard/Verbose: Always log search start with key parameters
                LandingZoneLogger.LogMinimal(
                    $"[LandingZone] Search start: preset={presetLabel}, mode={modeLabel}, strictness={_strictness:F2}, maxResults={_maxResults}"
                );

                var grid = Find.World?.grid ?? throw new System.InvalidOperationException("World grid unavailable.");
                _tileCount = grid.TilesCount;

                // Reset cache if world changed
                var worldSeed = Find.World?.info?.seedString ?? string.Empty;
                _owner._tileCache.ResetIfWorldChanged(worldSeed);

                // Standard/Verbose: Log filter configuration (Verbose gets detailed dump, Standard gets summary)
                LogActiveFilters(filters);
                LogActiveFiltersSummary(filters);

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

                // Eager-build MineralStockpileCache if StoneFilter is active (prevents lazy build during tile evaluation)
                if (filters.Stones.HasAnyImportance)
                {
                    LandingZoneLogger.LogStandard("[LandingZone] Pre-building MineralStockpileCache for StoneFilter...");
                    state.MineralStockpileCache.EnsureBuilt();
                }

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

                LandingZoneLogger.LogStandard($"[LandingZone] Membership scoring: {_criticalFilters.Count} critical filters, {_preferredFilters.Count} preferred filters");

                var aggregator = new BitsetAggregator(
                    cheapPredicates,
                    _heavyCriticals.Count,
                    _heavyPreferreds.Count,
                    _context,
                    _tileCount
                );

                // Get max candidates from mod settings
                int maxCandidates = LandingZoneSettings.MaxCandidates.GetValue();
                _candidates = aggregator.GetCandidates(_strictness, maxCandidates);

                LandingZoneLogger.LogStandard($"[LandingZone] FilterEvaluationJob: Stage A complete, {_candidates.Count} candidates");

                // FALLBACK LOGIC: If zero candidates and preset has fallback tiers, try them
                if (_candidates.Count == 0 && _preset?.FallbackTiers != null && _preset.FallbackTiers.Count > 0)
                {
                    LandingZoneLogger.LogStandard($"[LandingZone] ⚠️ Primary filters yielded zero results. Trying fallback tiers...");

                    // Special warning for Exotic preset - ArcheanTrees might be missing
                    if (_preset.Id == "exotic")
                    {
                        LandingZoneLogger.LogStandard($"[LandingZone] ⚠️ Exotic preset: ArcheanTrees anchor yielded zero results.");
                        LandingZoneLogger.LogStandard($"[LandingZone]    → Possible causes: Biotech DLC not installed, or ArcheanTrees def missing from mod loadout.");
                    }

                    foreach (var (tier, index) in _preset.FallbackTiers.Select((t, i) => (t, i)))
                    {
                        LandingZoneLogger.LogStandard($"[LandingZone] Attempting Tier {index + 2}: {tier.Name}");

                        // Create temporary state with fallback filters
                        var tempPrefs = new UserPreferences();
                        tempPrefs.SimpleFilters.CopyFrom(tier.Filters);
                        var tempState = new GameState(_state.DefCache, tempPrefs, _state.BestSiteProfile);

                        // Get predicates for fallback filters
                        var (cheapFallback, heavyFallback) = owner._registry.GetAllPredicates(tempState);
                        int fallbackTotalCrits = cheapFallback.Count(p => p.Importance == FilterImportance.Critical) +
                                                  heavyFallback.Count(p => p.Importance == FilterImportance.Critical);
                        int fallbackTotalPrefs = cheapFallback.Count(p => p.Importance == FilterImportance.Preferred) +
                                                  heavyFallback.Count(p => p.Importance == FilterImportance.Preferred);
                        int fallbackHeavyCrits = heavyFallback.Count(p => p.Importance == FilterImportance.Critical);
                        int fallbackHeavyPrefs = heavyFallback.Count(p => p.Importance == FilterImportance.Preferred);

                        // Re-run aggregator with fallback predicates
                        var fallbackAggregator = new BitsetAggregator(
                            cheapFallback,
                            fallbackHeavyCrits,
                            fallbackHeavyPrefs,
                            _context,
                            _tileCount
                        );

                        _candidates = fallbackAggregator.GetCandidates(_strictness, maxCandidates);

                        if (_candidates.Count > 0)
                        {
                            _fallbackTierUsed = index + 1; // 1-indexed (Tier 2 = index 0 in list)
                            LandingZoneLogger.LogStandard($"[LandingZone] ✓ Tier {index + 2} ({tier.Name}) yielded {_candidates.Count} candidates");

                            // CRITICAL: Update scoring context to use fallback tier's filters
                            // Otherwise, scoring phase still checks against original preset's Critical requirements
                            _state.Preferences.SimpleFilters.CopyFrom(tier.Filters);

                            // Re-initialize scoring filters from fallback tier
                            _criticalFilters.Clear();
                            _preferredFilters.Clear();
                            var fallbackFilters = tier.Filters;

                            foreach (var filter in owner._registry.Filters)
                            {
                                var importance = SiteFilterRegistry.GetFilterImportance(filter.Id, fallbackFilters);

                                if (importance == FilterImportance.Critical)
                                    _criticalFilters.Add(filter);
                                else if (importance == FilterImportance.Preferred)
                                    _preferredFilters.Add(filter);
                            }

                            // Update weights (equal weights for now)
                            Array.Resize(ref _criticalWeights, _criticalFilters.Count);
                            Array.Resize(ref _preferredWeights, _preferredFilters.Count);
                            for (int i = 0; i < _criticalWeights.Length; i++) _criticalWeights[i] = 1f;
                            for (int i = 0; i < _preferredWeights.Length; i++) _preferredWeights[i] = 1f;

                            // Update totals
                            _totalCriticals = fallbackTotalCrits;
                            _totalPreferreds = fallbackTotalPrefs;
                            _kappa = ScoringWeights.ComputeKappa(_totalCriticals, _totalPreferreds);

                            LandingZoneLogger.LogStandard($"[LandingZone] Updated scoring context: {_criticalFilters.Count} critical filters, {_preferredFilters.Count} preferred filters (from Tier {index + 2})");

                            break; // Stop trying fallbacks once we have results
                        }
                        else
                        {
                            LandingZoneLogger.LogStandard($"[LandingZone] ✗ Tier {index + 2} ({tier.Name}) also yielded zero results");
                        }
                    }

                    if (_candidates.Count == 0)
                    {
                        LandingZoneLogger.LogStandard($"[LandingZone] ⚠️ All fallback tiers exhausted. No results found.");
                    }
                }

                // Start incremental precomputation for all heavy predicates
                _heavyPredicateCount = _heavyCriticals.Count + _heavyPreferreds.Count;
                if (_heavyPredicateCount > 0)
                {
                    LandingZoneLogger.LogStandard($"[LandingZone] Starting precomputation for {_heavyPredicateCount} heavy predicates...");
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
                // Only log detailed filter configuration in Verbose mode
                if (!LandingZoneLogger.IsVerbose)
                    return;

                LandingZoneLogger.LogVerbose($"[LandingZone] === Active Filter Configuration ===");
                LandingZoneLogger.LogVerbose($"[LandingZone] Strictness: {filters.CriticalStrictness:F2} (1.0 = all criticals required)");
                LandingZoneLogger.LogVerbose($"[LandingZone] MaxResults: {filters.MaxResults}");

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
                    LandingZoneLogger.LogVerbose($"[LandingZone] Critical filters: {string.Join(", ", criticals)}");
                else
                    LandingZoneLogger.LogVerbose($"[LandingZone] Critical filters: (none)");

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
                    LandingZoneLogger.LogVerbose($"[LandingZone] Preferred filters: {string.Join(", ", preferreds)}");
                else
                    LandingZoneLogger.LogVerbose($"[LandingZone] Preferred filters: (none)");

                LandingZoneLogger.LogVerbose($"[LandingZone] =====================================");
            }

            /// <summary>
            /// Standard mode: Log concise filter summary (one block, no stack traces)
            /// </summary>
            private static void LogActiveFiltersSummary(FilterSettings filters)
            {
                // Only in Standard mode (not Minimal, not Verbose - Verbose has its own detailed dump)
                if (!LandingZoneLogger.IsStandardOrVerbose || LandingZoneLogger.IsVerbose)
                    return;

                var criticals = new List<string>();
                var preferreds = new List<string>();

                // Climate & Environment
                if (filters.AverageTemperatureImportance == FilterImportance.Critical) criticals.Add($"AvgTemp[{filters.AverageTemperatureRange.min:F0}-{filters.AverageTemperatureRange.max:F0}°C]");
                else if (filters.AverageTemperatureImportance == FilterImportance.Preferred) preferreds.Add("AvgTemp");

                if (filters.RainfallImportance == FilterImportance.Critical) criticals.Add($"Rain[{filters.RainfallRange.min:F0}-{filters.RainfallRange.max:F0}mm]");
                else if (filters.RainfallImportance == FilterImportance.Preferred) preferreds.Add("Rain");

                if (filters.GrowingDaysImportance == FilterImportance.Critical) criticals.Add($"Growing[{filters.GrowingDaysRange.min:F0}-{filters.GrowingDaysRange.max:F0}d]");
                else if (filters.GrowingDaysImportance == FilterImportance.Preferred) preferreds.Add("Growing");

                // Water
                if (filters.WaterAccessImportance == FilterImportance.Critical) criticals.Add("WaterAccess");
                else if (filters.CoastalImportance == FilterImportance.Critical) criticals.Add("Coastal");
                else if (filters.CoastalImportance == FilterImportance.Preferred) preferreds.Add("Coastal");

                // Map features - show specific features if small set (<=3 Critical)
                if (filters.MapFeatures.HasCritical)
                {
                    var critFeatures = filters.MapFeatures.ItemImportance
                        .Where(kvp => kvp.Value == FilterImportance.Critical)
                        .Select(kvp => kvp.Key)
                        .ToList();

                    if (critFeatures.Count <= 3)
                        criticals.Add($"MapFeatures[{string.Join("+", critFeatures)}]({filters.MapFeatures.Operator})");
                    else
                        criticals.Add($"MapFeatures({filters.MapFeatures.Operator}, {critFeatures.Count} critical)");
                }
                else if (filters.MapFeatures.HasPreferred) preferreds.Add("MapFeatures");

                // Biome lock
                if (filters.LockedBiome != null) criticals.Add($"Biome[{filters.LockedBiome.label}]");

                // Rivers
                if (filters.Rivers.HasCritical) criticals.Add($"Rivers({filters.Rivers.Operator})");
                else if (filters.Rivers.HasPreferred) preferreds.Add("Rivers");

                // Landmark
                if (filters.LandmarkImportance == FilterImportance.Critical) criticals.Add("Landmark");
                else if (filters.LandmarkImportance == FilterImportance.Preferred) preferreds.Add("Landmark");

                // Stones
                if (filters.Stones.HasCritical) criticals.Add($"Stones({filters.Stones.Operator})");
                else if (filters.Stones.HasPreferred) preferreds.Add("Stones");

                // Build summary
                string critSummary = criticals.Count > 0 ? string.Join(", ", criticals) : "(none)";
                string prefSummary = preferreds.Count > 0 ? string.Join(", ", preferreds) : "(none)";

                LandingZoneLogger.LogStandard($"[LandingZone] Active filters - Critical: {critSummary}");
                if (preferreds.Count > 0)
                    LandingZoneLogger.LogStandard($"[LandingZone] Active filters - Preferred: {prefSummary}");
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
                    MatchBreakdownV2? detailedBreakdown = null;

                    // Check feature flag for scoring system
                    if (_state.Preferences.Options.UseNewScoring)
                    {
                        // NEW: Membership-based scoring
                        (critScore, prefScore, penalty, finalScore, detailedBreakdown) = ComputeMembershipScoreForTile(candidate.TileId);

                        // Strictness check: critical score must meet threshold
                        if (critScore < _currentStrictness)
                        {
                            LandingZoneLogger.LogVerbose($"[LandingZone] Tile {candidate.TileId}: [MEMBERSHIP] Failed strictness check " +
                                           $"(critScore={critScore:F2} < {_currentStrictness:F2}, penalty={penalty:F2})");
                            continue;
                        }

                        LandingZoneLogger.LogVerbose($"[LandingZone] Tile {candidate.TileId}: [MEMBERSHIP] critScore={critScore:F2}, " +
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
                            LandingZoneLogger.LogVerbose($"[LandingZone] Tile {candidate.TileId}: [K-OF-N] Failed strictness check " +
                                           $"(crit {totalCritMatches}/{_totalCriticals} = {critScore:F2} < {_currentStrictness:F2})");
                            continue;
                        }

                        finalScore = ScoringWeights.ComputeFinalScore(critScore, prefScore, _kappa);
                        penalty = 1.0f; // No penalty in k-of-n

                        LandingZoneLogger.LogVerbose($"[LandingZone] Tile {candidate.TileId}: [K-OF-N] critScore={critScore:F2}, prefScore={prefScore:F2}, finalScore={finalScore:F2}");
                    }

                    // Simplified breakdown for now
                    var breakdown = new MatchBreakdown(
                        false, 0f, false, 0f, false, 0f, false, 0f, false, 0f, false, 0f,
                        FilterImportance.Ignored, false,
                        FilterImportance.Ignored, false,
                        FilterImportance.Ignored, false, null,
                        FilterImportance.Ignored, false,
                        FilterImportance.Ignored, 0, 0,
                        true, 0f, null, finalScore
                    );

                    var tileScore = new TileScore(candidate.TileId, finalScore, breakdown, detailedBreakdown);

                    // Insert into Top-N heap
                    InsertTopResult(_best, tileScore, _maxResults);

                    // Update min heap value
                    if (_best.Count >= _maxResults)
                        _minInHeap = _best.Min(r => r.Score);
                }

                // Check if done
                if (_cursor >= _candidates.Count)
                {
                    // Log Stage B completion telemetry
                    LandingZoneLogger.LogStandard($"[LandingZone] Stage B processed {_cursor}/{_candidates.Count} candidates in {_stopwatch.ElapsedMilliseconds}ms");

                    // Auto-relax: If 0 results and strictness is 1.0, retry with relaxed strictness
                    if (_best.Count == 0 && _currentStrictness >= 1.0f && _totalCriticals >= 2)
                    {
                        LandingZoneLogger.LogWarning("[LandingZone] FilterEvaluationJob: 0 results at strictness 1.0");
                        LandingZoneLogger.LogWarning($"[LandingZone] Auto-relaxing to {_totalCriticals - 1} of {_totalCriticals} criticals and retrying...");

                        float relaxedStrictness = (_totalCriticals - 1) / (float)_totalCriticals;

                        // Reset and retry with relaxed strictness
                        _currentStrictness = relaxedStrictness;
                        _cursor = 0;
                        _best.Clear();
                        _minInHeap = 0f;
                        // Don't clear _heavyBitsets - we want to reuse the cached precomputed bitsets

                        // Continue processing (don't set _completed = true)
                        LandingZoneLogger.LogStandard($"[LandingZone] Retrying with relaxed strictness {relaxedStrictness:F2}");
                        return false; // Not done yet, continue processing
                    }

                    _best.Sort((a, b) => b.Score.CompareTo(a.Score));
                    _results.Clear();
                    _results.AddRange(_best);
                    _stopwatch.Stop();
                    _completed = true;

                    // Compute best score (top result's score, or 0 if no results)
                    float bestScore = _results.Count > 0 ? _results[0].Score : 0f;

                    // Get preset label for completion line
                    var preset = _state.Preferences.ActivePreset;
                    var presetLabel = preset != null
                        ? $"{preset.Id} ({preset.Name})"
                        : (_state.Preferences.Options.PreferencesUIMode == UIMode.Simple ? "custom_simple" : "advanced");
                    var modeLabel = _state.Preferences.Options.PreferencesUIMode.ToString();

                    // CRITICAL=0 WARNING: If we have zero critical filters, warn user
                    if (_totalCriticals == 0)
                    {
                        LandingZoneLogger.LogWarning($"[LandingZone] ⚠️ No Critical filters active (preset={presetLabel}). Results are unfiltered!");
                    }

                    // Minimal/Standard/Verbose: Always log completion with key metrics
                    string tierSuffix = _fallbackTierUsed > 0 ? $", tier={_fallbackTierUsed + 1}" : "";
                    LandingZoneLogger.LogMinimal(
                        $"[LandingZone] Search complete: preset={presetLabel}, mode={modeLabel}, results={_results.Count}, " +
                        $"bestScore={bestScore:F4}, strictness={_currentStrictness:F2}, duration={_stopwatch.ElapsedMilliseconds}ms{tierSuffix}"
                    );

                    // Standard/Verbose: Additional context about relaxed strictness
                    if (_results.Count > 0 && _currentStrictness < 1.0f)
                    {
                        int requiredMatches = Mathf.CeilToInt(_totalCriticals * _currentStrictness);
                        LandingZoneLogger.LogStandard($"[LandingZone] 💡 No tiles matched all {_totalCriticals} criticals. Showing tiles matching {requiredMatches} of {_totalCriticals}.");
                    }

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
            /// Returns tuple: (critScore, prefScore, penalty, finalScore, detailedBreakdown)
            /// </summary>
            private (float critScore, float prefScore, float penalty, float finalScore, MatchBreakdownV2 breakdown) ComputeMembershipScoreForTile(int tileId)
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

                // Compute global weights from mod settings
                var (critBase, prefBase, mutatorWeight) = LandingZoneSettings.GetWeightValues();
                var (lambdaC, lambdaP, lambdaMut) = ScoringWeights.ComputeGlobalWeights(
                    _criticalFilters.Count,
                    _preferredFilters.Count,
                    critBase,
                    prefBase,
                    mutatorWeight
                );

                // Compute mutator quality score (with preset-specific overrides if active)
                var tileMutators = Filters.MapFeatureFilter.GetTileMapFeatures(tileId);
                var activePreset = _context.State.Preferences.ActivePreset;
                float mutatorScore = MutatorQualityRatings.ComputeMutatorScore(tileMutators, 0.25f, activePreset);

                // Compute final membership score
                float finalScore = ScoringWeights.ComputeMembershipScore(
                    critScore,
                    prefScore,
                    mutatorScore,
                    penalty,
                    lambdaC,
                    lambdaP,
                    lambdaMut
                );

                // Build detailed breakdown
                var breakdown = BuildDetailedBreakdown(
                    tileId,
                    critMemberships,
                    prefMemberships,
                    critScore,
                    prefScore,
                    mutatorScore,
                    penalty,
                    finalScore
                );

                return (critScore, prefScore, penalty, finalScore, breakdown);
            }

            /// <summary>
            /// Builds detailed breakdown with per-filter match info and mutator contributions.
            /// </summary>
            private MatchBreakdownV2 BuildDetailedBreakdown(
                int tileId,
                float[] critMemberships,
                float[] prefMemberships,
                float critScore,
                float prefScore,
                float mutatorScore,
                float penalty,
                float finalScore)
            {
                var matched = new List<FilterMatchInfo>();
                var missed = new List<FilterMatchInfo>();

                // Process critical filters
                for (int i = 0; i < _criticalFilters.Count; i++)
                {
                    var filter = _criticalFilters[i];
                    float membership = critMemberships[i];
                    bool isMatched = membership >= 0.9f; // Match threshold
                    bool isRangeFilter = IsRangeFilter(filter);
                    float filterPenalty = isMatched ? 0f : (1f - membership) * (1f - membership);

                    var info = new FilterMatchInfo(
                        filter.Id,
                        FilterImportance.Critical,
                        membership,
                        isMatched,
                        isRangeFilter,
                        filterPenalty
                    );

                    if (isMatched)
                        matched.Add(info);
                    else
                        missed.Add(info);
                }

                // Process preferred filters
                for (int i = 0; i < _preferredFilters.Count; i++)
                {
                    var filter = _preferredFilters[i];
                    float membership = prefMemberships[i];
                    bool isMatched = membership >= 0.9f;
                    bool isRangeFilter = IsRangeFilter(filter);
                    float filterPenalty = isMatched ? 0f : (1f - membership) * 0.5f; // Preferred penalty is softer

                    var info = new FilterMatchInfo(
                        filter.Id,
                        FilterImportance.Preferred,
                        membership,
                        isMatched,
                        isRangeFilter,
                        filterPenalty
                    );

                    if (isMatched)
                        matched.Add(info);
                    else
                        missed.Add(info);
                }

                // Collect mutator contributions (excluding explicitly selected Critical/Preferred)
                var tileMutators = Filters.MapFeatureFilter.GetTileMapFeatures(tileId);
                var mutatorContributions = new List<MutatorContribution>();
                var mapFeaturesFilter = _state.Preferences.GetActiveFilters().MapFeatures;
                var activePreset = _state.Preferences.ActivePreset; // Get active preset for quality overrides

                if (tileMutators != null)
                {
                    foreach (var mutator in tileMutators)
                    {
                        // Skip mutators that were explicitly marked as Critical or Preferred
                        // These are already counted in the matched/missed sections, not as "bonus" modifiers
                        var importance = mapFeaturesFilter.GetImportance(mutator);
                        if (importance == FilterImportance.Critical || importance == FilterImportance.Preferred)
                            continue;

                        // Use preset-specific quality overrides if active preset exists
                        int quality = MutatorQualityRatings.GetQuality(mutator, activePreset);
                        if (quality != 0) // Only include non-neutral
                        {
                            // Approximate contribution (actual is more complex)
                            float contribution = quality * 0.01f; // Rough estimate
                            mutatorContributions.Add(new MutatorContribution(mutator, quality, contribution));
                        }
                    }
                }

                // Collect stone/mineral contributions (from MineralStockpileCache)
                var stonesFilter = _state.Preferences.GetActiveFilters().Stones;
                var tileStones = _state.MineralStockpileCache.GetMineralTypes(tileId);
                if (tileStones != null && tileStones.Count > 0)
                {
                    foreach (var stone in tileStones)
                    {
                        // Show stones that matched user preferences (Critical or Preferred)
                        var importance = stonesFilter.GetImportance(stone);
                        if (importance == FilterImportance.Critical || importance == FilterImportance.Preferred)
                        {
                            // Stones matched user preferences - give positive contribution
                            // Use quality rating: Plasteel/Uranium = +8, Gold/Jade = +6, Silver/Components/Steel = +4
                            int quality = stone switch
                            {
                                "MineablePlasteel" => 8,
                                "MineableUranium" => 8,
                                "MineableGold" => 6,
                                "MineableJade" => 6,
                                "MineableSilver" => 4,
                                "MineableSteel" => 4,         // Core construction resource
                                "MineableComponentsIndustrial" => 4,
                                _ => 2 // Unknown ores get small bonus
                            };

                            float contribution = quality * 0.01f;
                            mutatorContributions.Add(new MutatorContribution(stone, quality, contribution));
                        }
                    }
                }

                // Collect stockpile contributions (from MineralStockpileCache)
                var stockpilesFilter = _state.Preferences.GetActiveFilters().Stockpiles;
                var tileStockpiles = _state.MineralStockpileCache.GetStockpileTypes(tileId);
                if (tileStockpiles != null && tileStockpiles.Count > 0)
                {
                    foreach (var stockpile in tileStockpiles)
                    {
                        // Show stockpiles that matched user preferences (Critical or Preferred)
                        var importance = stockpilesFilter.GetImportance(stockpile);
                        if (importance == FilterImportance.Critical || importance == FilterImportance.Preferred)
                        {
                            // Stockpiles matched user preferences - give positive contribution
                            // Use StockpileFilter.GetStockpileQuality for consistent quality ratings
                            int quality = Filters.StockpileFilter.GetStockpileQuality(stockpile);
                            float contribution = quality * 0.01f;
                            mutatorContributions.Add(new MutatorContribution(stockpile, quality, contribution));
                        }
                    }
                }

                // Collect animal species contributions (from MineralStockpileCache)
                var tileAnimals = _state.MineralStockpileCache.GetAnimalSpecies(tileId);
                if (tileAnimals != null && tileAnimals.Count > 0)
                {
                    foreach (var animal in tileAnimals)
                    {
                        // Always show flagship animals (no filter exists yet, but provide quality bonus)
                        // Quality ratings based on rarity/value: Thrumbo +10, Megasloth +8, Elephant +7, etc.
                        int quality = GetAnimalQuality(animal);
                        float contribution = quality * 0.01f;
                        mutatorContributions.Add(new MutatorContribution(animal, quality, contribution));
                    }
                }

                // Collect plant species contributions (from MineralStockpileCache)
                var tilePlants = _state.MineralStockpileCache.GetPlantSpecies(tileId);
                if (tilePlants != null && tilePlants.Count > 0)
                {
                    foreach (var plant in tilePlants)
                    {
                        // Always show flagship plants (no filter exists yet, but provide quality bonus)
                        // Quality ratings based on value: Ambrosia +9, Devilstrand +8, Healroot +7, etc.
                        int quality = GetPlantQuality(plant);
                        float contribution = quality * 0.01f;
                        mutatorContributions.Add(new MutatorContribution(plant, quality, contribution));
                    }
                }

                return new MatchBreakdownV2(
                    matched,
                    missed,
                    mutatorContributions,
                    critScore,
                    prefScore,
                    mutatorScore,
                    penalty,
                    finalScore
                );
            }

            /// <summary>
            /// Determines if a filter uses range-based matching (vs boolean).
            /// </summary>
            private static bool IsRangeFilter(ISiteFilter filter)
            {
                // Range filters have continuous membership scores
                string id = filter.Id.ToLowerInvariant();
                return id.Contains("temperature") ||
                       id.Contains("rainfall") ||
                       id.Contains("growing") ||
                       id.Contains("forage") ||
                       id.Contains("pollution") ||
                       id.Contains("movement");
            }

            /// <summary>
            /// Gets quality rating for animal species.
            /// Based on rarity, value, and strategic importance.
            /// </summary>
            private static int GetAnimalQuality(string animalDefName)
            {
                return animalDefName switch
                {
                    // Ultra-rare legendary animals
                    "Thrumbo" => 10,             // Ultra-rare, high value, legendary

                    // Rare valuable animals
                    "Megasloth" => 8,            // Rare, wool, meat, tanky
                    "Elephant" => 7,             // Large, tanky, valuable tusks
                    "Rhinoceros" => 7,           // Tanky, good for caravans
                    "Grizzly_Bear" => 6,         // Dangerous but valuable
                    "Polar_Bear" => 6,           // Cold biome apex predator

                    // Useful domestic animals
                    "Muffalo" => 5,              // Common but essential for caravans, wool, milk
                    "Alpaca" => 5,               // Wool producer, pack animal
                    "Dromedary" => 5,            // Desert specialist, pack animal
                    "Cow" => 4,                  // Milk, leather, meat
                    "Pig" => 4,                  // Fast breeding, meat

                    // Common food animals
                    "Deer" => 3,                 // Common hunting target
                    "Elk" => 3,                  // Good meat yield
                    "Turkey" => 3,               // Food, eggs
                    "Chicken" => 2,              // Very common, eggs
                    "Hare" => 2,                 // Common, fast breeding

                    // Default for unknown species
                    _ => 3                       // Generic animal bonus
                };
            }

            /// <summary>
            /// Gets quality rating for plant species.
            /// Based on value, rarity, and utility (medicine, drugs, textiles, food).
            /// </summary>
            private static int GetPlantQuality(string plantDefName)
            {
                return plantDefName switch
                {
                    // Ultra-rare valuable plants
                    "Plant_Ambrosia" => 9,       // Ultra-rare psychite drug source, high recreation value

                    // Rare luxury/specialist plants
                    "Plant_Devilstrand" => 8,    // Luxury textile, slow-growing, high value
                    "Plant_Healroot" => 7,       // Medicine source, cannot craft early-game

                    // Valuable cash crops
                    "Plant_Smokeleaf" => 5,      // Drug, recreation, trading commodity
                    "Plant_Psychoid" => 5,       // Psychite drug source (tea, flake, yayo)

                    // Useful agricultural plants
                    "Plant_Cotton" => 4,         // Textile production
                    "Plant_Corn" => 4,           // High food yield
                    "Plant_Rice" => 4,           // Fast-growing food
                    "Plant_Potato" => 3,         // Reliable food source

                    // Common foraging plants
                    "Plant_Berry" => 3,          // Wild food source
                    "Plant_Strawberry" => 3,     // Wild food source
                    "Plant_TreeOak" => 2,        // Wood source (common)

                    // Default for unknown species
                    _ => 3                       // Generic plant bonus
                };
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
                                "Caves" => "Cave systems",
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
        public TileScore(int tileId, float score, MatchBreakdown breakdown, MatchBreakdownV2? breakdownV2 = null)
        {
            TileId = tileId;
            Score = score;
            Breakdown = breakdown;
            BreakdownV2 = breakdownV2;
        }

        public int TileId { get; }
        public float Score { get; }
        public MatchBreakdown Breakdown { get; } // Legacy
        public MatchBreakdownV2? BreakdownV2 { get; } // New detailed breakdown
    }
}

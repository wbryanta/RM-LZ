using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LandingZone.Data;
using UnityEngine;
using Verse;

namespace LandingZone.Core.Filtering
{
    /// <summary>
    /// Caches precomputed bitsets for heavy predicates to avoid repeated expensive evaluations.
    /// Heavy predicates are evaluated ONCE upfront (incrementally to avoid UI freeze), then bitsets are reused for all candidate checks.
    /// Cache keys include predicate parameters to detect when filters change.
    /// </summary>
    public sealed class PrecomputedBitsetCache
    {
        private readonly Dictionary<string, BitArray> _cache = new Dictionary<string, BitArray>();
        private readonly Dictionary<string, IncrementalPrecomputation> _inProgress = new Dictionary<string, IncrementalPrecomputation>();
        private readonly Dictionary<string, int> _predicateParamHashes = new Dictionary<string, int>(); // Track param hashes for invalidation

        /// <summary>
        /// Represents an in-progress incremental precomputation of a heavy predicate.
        /// </summary>
        private class IncrementalPrecomputation
        {
            public IFilterPredicate Predicate;
            public FilterContext Context;
            public int TileCount;
            public BitArray Result;
            public int ProcessedCount;
            public ISiteFilter SiteFilter;

            public IncrementalPrecomputation(IFilterPredicate predicate, FilterContext context, int tileCount)
            {
                Predicate = predicate;
                Context = context;
                TileCount = tileCount;
                Result = new BitArray(tileCount);
                ProcessedCount = 0;

                // Extract the underlying ISiteFilter if this is a FilterPredicateAdapter
                if (predicate is FilterPredicateAdapter adapter)
                {
                    var filterField = typeof(FilterPredicateAdapter).GetField("_filter",
                        BindingFlags.NonPublic | BindingFlags.Instance);

                    if (filterField != null && filterField.GetValue(adapter) is ISiteFilter siteFilter)
                    {
                        SiteFilter = siteFilter;
                    }
                }
            }

            /// <summary>
            /// Processes a chunk of tiles. Returns true if complete.
            /// Processes tiles in batches to avoid UI freeze.
            /// </summary>
            public bool ProcessChunk(int chunkSize)
            {
                if (ProcessedCount >= TileCount)
                    return true;

                if (SiteFilter == null)
                {
                    // Fallback: synchronous evaluation if we couldn't extract the filter
                    Log.Warning($"[LandingZone] No ISiteFilter for incremental processing, falling back to synchronous");
                    Result = Predicate.Evaluate(Context, TileCount);
                    ProcessedCount = TileCount;
                    return true;
                }

                // Process a chunk of tiles (e.g., tiles 0-4999, then 5000-9999, etc.)
                int endTile = Mathf.Min(ProcessedCount + chunkSize, TileCount);
                var chunkTileIds = Enumerable.Range(ProcessedCount, endTile - ProcessedCount);

                // Call Apply() on just this chunk
                var matchingTiles = SiteFilter.Apply(Context, chunkTileIds);

                // Set bits for matching tiles
                foreach (var tileId in matchingTiles)
                {
                    if (tileId >= 0 && tileId < TileCount)
                    {
                        Result[tileId] = true;
                    }
                }

                ProcessedCount = endTile;
                return ProcessedCount >= TileCount;
            }

            public float Progress => TileCount == 0 ? 1f : ProcessedCount / (float)TileCount;
        }

        /// <summary>
        /// Computes a hash of predicate parameters to detect changes.
        /// </summary>
        private int ComputePredicateParamHash(IFilterPredicate predicate, FilterContext context)
        {
            // Use filter settings to compute hash based on predicate's specific parameters
            var filters = context.Filters;
            unchecked
            {
                int hash = 17;

                // Hash based on predicate ID-specific parameters
                // Only Heavy filters need param hashing (Light filters don't use PrecomputedBitsetCache)
                switch (predicate.Id)
                {
                    case "growing_days":
                        hash = hash * 31 + filters.GrowingDaysRange.GetHashCode();
                        hash = hash * 31 + filters.GrowingDaysImportance.GetHashCode();
                        break;
                    case "minimum_temperature":
                        hash = hash * 31 + filters.MinimumTemperatureRange.GetHashCode();
                        hash = hash * 31 + filters.MinimumTemperatureImportance.GetHashCode();
                        break;
                    case "maximum_temperature":
                        hash = hash * 31 + filters.MaximumTemperatureRange.GetHashCode();
                        hash = hash * 31 + filters.MaximumTemperatureImportance.GetHashCode();
                        break;
                    // Note: pollution, forageability, swampiness, animal_density, fish_population,
                    // plant_density, elevation are all Light filters and don't use this cache
                    default:
                        // For unknown predicates, use predicate ID as hash
                        hash = hash * 31 + predicate.Id.GetHashCode();
                        break;
                }

                return hash;
            }
        }

        /// <summary>
        /// Starts incremental precomputation for a predicate if not already cached/in-progress.
        /// Checks parameter hash to detect if cached bitset is stale.
        /// </summary>
        public void StartPrecomputation(IFilterPredicate predicate, FilterContext context, int tileCount)
        {
            int currentParamHash = ComputePredicateParamHash(predicate, context);

            // Check if we have a cached bitset with matching parameters
            if (_cache.ContainsKey(predicate.Id))
            {
                if (_predicateParamHashes.TryGetValue(predicate.Id, out int cachedHash))
                {
                    if (cachedHash == currentParamHash)
                    {
                        // Cache hit! Reusing precomputed bitset
                        if (LandingZoneSettings.LogLevel >= LoggingLevel.Standard)
                            Log.Message($"[LandingZone] [CACHE HIT] {predicate.Id}: hash {currentParamHash} matches cached");
                        return;
                    }
                    else
                    {
                        // Parameters changed, invalidate cache
                        if (LandingZoneSettings.LogLevel >= LoggingLevel.Standard)
                            Log.Message($"[LandingZone] [CACHE MISS] {predicate.Id}: hash changed from {cachedHash} to {currentParamHash}, recomputing");
                        _cache.Remove(predicate.Id);
                        _predicateParamHashes.Remove(predicate.Id);
                    }
                }
                else
                {
                    // Cache exists but no hash (shouldn't happen, but handle it)
                    if (LandingZoneSettings.LogLevel >= LoggingLevel.Standard)
                        Log.Message($"[LandingZone] [CACHE MISS] {predicate.Id}: cache exists but no param hash, recomputing");
                    _cache.Remove(predicate.Id);
                }
            }
            else if (_predicateParamHashes.ContainsKey(predicate.Id))
            {
                // Hash exists but no cache (shouldn't happen, clean up)
                if (LandingZoneSettings.LogLevel >= LoggingLevel.Standard)
                    Log.Message($"[LandingZone] [CACHE MISS] {predicate.Id}: orphaned param hash, clearing");
                _predicateParamHashes.Remove(predicate.Id);
            }

            // Already in progress with same params?
            if (_inProgress.ContainsKey(predicate.Id))
            {
                if (LandingZoneSettings.LogLevel >= LoggingLevel.Standard)
                    Log.Message($"[LandingZone] {predicate.Id}: precomputation already in progress, skipping");
                return;
            }

            if (LandingZoneSettings.LogLevel >= LoggingLevel.Standard)
                Log.Message($"[LandingZone] Starting incremental precomputation: {predicate.Id} (hash: {currentParamHash})");
            _inProgress[predicate.Id] = new IncrementalPrecomputation(predicate, context, tileCount);
            _predicateParamHashes[predicate.Id] = currentParamHash; // Store hash for future validation
        }

        /// <summary>
        /// Processes a chunk of precomputations. Returns true if all in-progress precomputations are complete.
        /// </summary>
        public bool StepPrecomputations(int tilesPerChunk)
        {
            if (_inProgress.Count == 0)
                return true;

            var completedKeys = new List<string>();

            foreach (var kvp in _inProgress)
            {
                var precomp = kvp.Value;
                CurrentPredicateId = kvp.Key; // Track which filter is currently processing
                bool complete = precomp.ProcessChunk(tilesPerChunk);

                if (complete)
                {
                    _cache[kvp.Key] = precomp.Result;
                    completedKeys.Add(kvp.Key);
                    Log.Message($"[LandingZone] Completed precomputation: {kvp.Key}");
                }
            }

            foreach (var key in completedKeys)
            {
                _inProgress.Remove(key);
            }

            if (_inProgress.Count == 0)
                CurrentPredicateId = null;

            return _inProgress.Count == 0;
        }

        /// <summary>
        /// Gets the ID of the predicate currently being precomputed (for progress display).
        /// </summary>
        public string CurrentPredicateId { get; private set; }

        /// <summary>
        /// Gets the overall precomputation progress (0.0 to 1.0).
        /// </summary>
        public float GetPrecomputationProgress()
        {
            if (_inProgress.Count == 0)
                return 1f;

            float totalProgress = 0f;
            foreach (var precomp in _inProgress.Values)
            {
                totalProgress += precomp.Progress;
            }
            return totalProgress / _inProgress.Count;
        }

        /// <summary>
        /// Checks if there are any precomputations in progress.
        /// </summary>
        public bool HasPendingPrecomputations => _inProgress.Count > 0;

        /// <summary>
        /// Gets or computes the bitset for a predicate.
        /// If not cached and not in progress, starts synchronous computation (should be avoided).
        /// </summary>
        public BitArray GetOrCompute(IFilterPredicate predicate, FilterContext context, int tileCount)
        {
            if (_cache.TryGetValue(predicate.Id, out var cached))
                return cached;

            // This path should not be hit if StartPrecomputation was called properly
            Log.Warning($"[LandingZone] Synchronous fallback for predicate: {predicate.Id}");
            var bitset = predicate.Evaluate(context, tileCount);
            _cache[predicate.Id] = bitset;

            return bitset;
        }

        /// <summary>
        /// Checks if a specific tile matches a predicate (using cached bitset).
        /// Requires the predicate to be fully precomputed.
        /// </summary>
        public bool Matches(IFilterPredicate predicate, int tileId, FilterContext context, int tileCount)
        {
            var bitset = GetOrCompute(predicate, context, tileCount);
            return tileId < bitset.Length && bitset[tileId];
        }

        public void Clear()
        {
            _cache.Clear();
            _inProgress.Clear();
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using UnityEngine;
using Verse;

namespace LandingZone.Core.Filtering
{
    /// <summary>
    /// Stage A: Cheap aggregate gate using bitset operations.
    /// Evaluates cheap predicates (Critical + Preferred) and computes per-tile match counts.
    /// Gates candidates by upper bound scoring to keep dataset bounded.
    /// </summary>
    public sealed class BitsetAggregator
    {
        private readonly List<IFilterPredicate> _cheapCriticals;
        private readonly List<IFilterPredicate> _cheapPreferreds;
        private readonly int _heavyCriticalCount;
        private readonly int _heavyPreferredCount;
        private readonly int _tileCount;
        private readonly FilterContext _context;

        // Per-tile counts of cheap matches
        private int[] _critCheapCount;
        private int[] _prefCheapCount;

        public BitsetAggregator(
            List<IFilterPredicate> cheapPredicates,
            int heavyCriticalCount,
            int heavyPreferredCount,
            FilterContext context,
            int tileCount)
        {
            _cheapCriticals = cheapPredicates.Where(p => p.Importance == FilterImportance.Critical).ToList();
            _cheapPreferreds = cheapPredicates.Where(p => p.Importance == FilterImportance.Preferred).ToList();
            _heavyCriticalCount = heavyCriticalCount;
            _heavyPreferredCount = heavyPreferredCount;
            _context = context;
            _tileCount = tileCount;

            _critCheapCount = new int[tileCount];
            _prefCheapCount = new int[tileCount];
        }

        /// <summary>
        /// Stored cheap counts for later use in Stage B.
        /// </summary>
        public (int[] critical, int[] preferred) CheapCounts => (_critCheapCount, _prefCheapCount);

        /// <summary>
        /// Gets candidate tiles that pass the cheap aggregate gate.
        /// </summary>
        /// <param name="strictness">Critical strictness threshold [0,1]</param>
        /// <param name="maxCandidates">Maximum candidates to return (adaptive tightening)</param>
        /// <returns>List of candidates with their upper bound scores</returns>
        public List<CandidateTile> GetCandidates(float strictness, int maxCandidates)
        {
            // Step 1: Evaluate all cheap predicates and accumulate counts
            EvaluateAndAccumulate();

            // Step 2: Compute total critical and preferred counts
            int totalCriticals = _cheapCriticals.Count + _heavyCriticalCount;
            int totalPreferreds = _cheapPreferreds.Count + _heavyPreferredCount;

            // Step 3: Compute kappa for score weighting
            float kappa = ScoringWeights.ComputeKappa(totalCriticals, totalPreferreds);

            Log.Message($"[LandingZone] BitsetAggregator: {_cheapCriticals.Count} cheap crits, " +
                       $"{_cheapPreferreds.Count} cheap prefs, " +
                       $"{_heavyCriticalCount} heavy crits, " +
                       $"{_heavyPreferredCount} heavy prefs, κ={kappa:F3}");

            // Step 4: ADAPTIVE K-OF-N FALLBACK
            // Instead of using strictness directly, find the best k-of-n threshold
            // that produces a reasonable candidate count for Heavy filter processing.
            // Example: "Impossible" preset has 6 cheap criticals:
            //   - 6/6 match: 0 tiles (too strict)
            //   - 5/6 match: 42 tiles (too few)
            //   - 4/6 match: 2,847 tiles (GOOD - use this!)
            //   - This avoids processing 104k tiles with Growing Days

            int minReasonableCandidates = LandingZoneSettings.MaxCandidatesForHeavyFilters;
            int maxReasonableCandidates = minReasonableCandidates * 10; // Allow 10x for flexibility

            int effectiveMinCriticals = DetermineAdaptiveThreshold(
                minReasonableCandidates,
                maxReasonableCandidates,
                strictness);

            LandingZoneLogger.LogStandard($"[LandingZone] BitsetAggregator: Adaptive threshold = {effectiveMinCriticals}/{_cheapCriticals.Count} cheap criticals");

            // Step 5: Compute upper bounds and create candidates
            // Use reasonable initial capacity - don't pre-allocate for int.MaxValue!
            int initialCapacity = Mathf.Min(maxCandidates, 100000);
            var candidates = new List<CandidateTile>(initialCapacity);
            var world = Find.World;
            var worldGrid = world?.grid;

            for (int tileId = 0; tileId < _tileCount; tileId++)
            {
                // Filter out unsettleable tiles (ocean, impassable biomes)
                if (worldGrid != null)
                {
                    var tile = worldGrid[tileId];
                    var biome = tile?.PrimaryBiome;
                    if (biome == null || biome.impassable || world.Impassable(tileId))
                        continue;
                }

                // ADAPTIVE K-OF-N GATE: Tile must match at least effectiveMinCriticals cheap criticals
                // This replaces strictness-based filtering with intelligent k-of-n matching
                if (_cheapCriticals.Count > 0 && _critCheapCount[tileId] < effectiveMinCriticals)
                {
                    // Tile matched too few cheap criticals → eliminate
                    continue;
                }

                // Upper bound: assume this tile matches ALL heavy predicates
                float critScoreUB = (totalCriticals == 0) ? 1f :
                    (_critCheapCount[tileId] + _heavyCriticalCount) / (float)totalCriticals;

                float prefScoreUB = (totalPreferreds == 0) ? 0f :
                    (_prefCheapCount[tileId] + _heavyPreferredCount) / (float)totalPreferreds;

                float upperBound = ScoringWeights.ComputeFinalScore(critScoreUB, prefScoreUB, kappa);

                candidates.Add(new CandidateTile(
                    tileId,
                    upperBound,
                    _critCheapCount[tileId],
                    _prefCheapCount[tileId]
                ));
            }

            LandingZoneLogger.LogStandard($"[LandingZone] BitsetAggregator: {candidates.Count} candidates pass strictness {strictness:F2}");

            // Step 5: Adaptive tightening if too many candidates
            bool capHit = candidates.Count > maxCandidates;
            if (capHit)
            {
                int originalCount = candidates.Count;
                LandingZoneLogger.LogStandard($"[LandingZone] ⚠️ Candidate cap hit! {originalCount} candidates exceed limit of {maxCandidates}. " +
                                              $"Tightening to top {maxCandidates} by score. Consider using stricter filters or increasing Max Candidate Tiles in settings.");

                // Sort by upper bound and take top N
                candidates.Sort((a, b) => b.UpperBound.CompareTo(a.UpperBound));
                candidates = candidates.Take(maxCandidates).ToList();
            }

            // Step 6: Sort by upper bound descending for branch-and-bound
            candidates.Sort((a, b) => b.UpperBound.CompareTo(a.UpperBound));

            // Final summary
            if (maxCandidates == int.MaxValue)
            {
                LandingZoneLogger.LogStandard($"[LandingZone] BitsetAggregator: Returning {candidates.Count} candidates (Unlimited mode)");
            }
            else
            {
                string capStatus = capHit ? "CAP HIT" : $"{candidates.Count}/{maxCandidates}";
                LandingZoneLogger.LogStandard($"[LandingZone] BitsetAggregator: Returning {candidates.Count} candidates ({capStatus})");
            }

            return candidates;
        }

        private void EvaluateAndAccumulate()
        {
            // Evaluate cheap critical predicates
            foreach (var predicate in _cheapCriticals)
            {
                var bitset = predicate.Evaluate(_context, _tileCount);
                AccumulateCounts(bitset, _critCheapCount);
            }

            // Evaluate cheap preferred predicates
            foreach (var predicate in _cheapPreferreds)
            {
                var bitset = predicate.Evaluate(_context, _tileCount);
                AccumulateCounts(bitset, _prefCheapCount);
            }
        }

        private static void AccumulateCounts(BitArray bitset, int[] counts)
        {
            for (int i = 0; i < bitset.Length; i++)
            {
                if (bitset[i])
                    counts[i]++;
            }
        }

        /// <summary>
        /// Determines the minimum number of cheap criticals a tile must match to pass Stage A.
        /// Uses adaptive k-of-n fallback to ensure a reasonable candidate count for Heavy filter processing.
        /// </summary>
        /// <param name="minCandidates">Minimum desired candidates (e.g., 1000)</param>
        /// <param name="maxCandidates">Maximum desired candidates (e.g., 10000)</param>
        /// <param name="strictness">User's strictness setting (informational)</param>
        /// <returns>Minimum cheap criticals required (k in k-of-n)</returns>
        private int DetermineAdaptiveThreshold(int minCandidates, int maxCandidates, float strictness)
        {
            // If no cheap criticals, no filtering possible
            if (_cheapCriticals.Count == 0)
                return 0;

            // Count tiles matching k cheap criticals for each k
            int[] tileCounts = new int[_cheapCriticals.Count + 1]; // tileCounts[k] = # tiles matching ≥k criticals
            var world = Find.World;
            var worldGrid = world?.grid;

            for (int tileId = 0; tileId < _tileCount; tileId++)
            {
                // Skip unsettleable tiles
                if (worldGrid != null)
                {
                    var tile = worldGrid[tileId];
                    var biome = tile?.PrimaryBiome;
                    if (biome == null || biome.impassable || world.Impassable(tileId))
                        continue;
                }

                int matchCount = _critCheapCount[tileId];
                for (int k = 0; k <= matchCount; k++)
                {
                    tileCounts[k]++;
                }
            }

            // Log tile distribution for transparency
            LandingZoneLogger.LogStandard($"[LandingZone] BitsetAggregator: k-of-n distribution:");
            for (int k = _cheapCriticals.Count; k >= 0; k--)
            {
                LandingZoneLogger.LogStandard($"  {k}/{_cheapCriticals.Count}: {tileCounts[k]} tiles");
            }

            // Find the best k (highest match requirement) that produces a reasonable candidate count
            // Start from strictest (all criticals) and relax until we have enough candidates
            for (int k = _cheapCriticals.Count; k >= 0; k--)
            {
                int candidateCount = tileCounts[k];

                // Perfect: Within target range
                if (candidateCount >= minCandidates && candidateCount <= maxCandidates)
                {
                    LandingZoneLogger.LogStandard($"[LandingZone] BitsetAggregator: Selected {k}/{_cheapCriticals.Count} (produces {candidateCount} candidates, target: {minCandidates}-{maxCandidates})");
                    return k;
                }

                // Good enough: Exceeds minimum
                if (candidateCount >= minCandidates)
                {
                    LandingZoneLogger.LogStandard($"[LandingZone] BitsetAggregator: Selected {k}/{_cheapCriticals.Count} (produces {candidateCount} candidates, exceeds minimum {minCandidates})");
                    return k;
                }
            }

            // Fallback: No k produces enough candidates, use 0 (accept all)
            LandingZoneLogger.LogWarning($"[LandingZone] BitsetAggregator: No k-of-n threshold produces {minCandidates}+ candidates. Using 0/{_cheapCriticals.Count} (all tiles).");
            return 0;
        }
    }

    /// <summary>
    /// Candidate tile with upper bound score and cheap match counts.
    /// </summary>
    public readonly struct CandidateTile
    {
        public CandidateTile(int tileId, float upperBound, int critCheapMatches, int prefCheapMatches)
        {
            TileId = tileId;
            UpperBound = upperBound;
            CritCheapMatches = critCheapMatches;
            PrefCheapMatches = prefCheapMatches;
        }

        public int TileId { get; }
        public float UpperBound { get; }
        public int CritCheapMatches { get; }
        public int PrefCheapMatches { get; }
    }
}

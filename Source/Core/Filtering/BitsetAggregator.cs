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

            // Step 4: Compute upper bounds and create candidates
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

                // Upper bound: assume this tile matches ALL heavy predicates
                float critScoreUB = (totalCriticals == 0) ? 1f :
                    (_critCheapCount[tileId] + _heavyCriticalCount) / (float)totalCriticals;

                // Gate by strictness - can this tile possibly meet the threshold?
                if (critScoreUB < strictness)
                    continue; // Cannot meet strictness even if all heavy match

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

            Log.Message($"[LandingZone] BitsetAggregator: {candidates.Count} candidates pass strictness {strictness:F2}");

            // Step 5: Adaptive tightening if too many candidates
            if (candidates.Count > maxCandidates)
            {
                Log.Message($"[LandingZone] BitsetAggregator: Too many candidates ({candidates.Count}), " +
                           $"tightening to {maxCandidates}");

                // Sort by upper bound and take top N
                candidates.Sort((a, b) => b.UpperBound.CompareTo(a.UpperBound));
                candidates = candidates.Take(maxCandidates).ToList();
            }

            // Step 6: Sort by upper bound descending for branch-and-bound
            candidates.Sort((a, b) => b.UpperBound.CompareTo(a.UpperBound));

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

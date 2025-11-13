using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using UnityEngine;
using Verse;

namespace LandingZone.Core.Filtering
{
    /// <summary>
    /// Stage B: Branch-and-bound heavy evaluation with Top-N heap.
    /// Evaluates heavy predicates lazily and prunes candidates that cannot beat current Top-N.
    /// </summary>
    public sealed class BranchAndBoundScorer
    {
        private readonly List<IFilterPredicate> _heavyCriticals;
        private readonly List<IFilterPredicate> _heavyPreferreds;
        private readonly int _totalCriticals;
        private readonly int _totalPreferreds;
        private readonly float _kappa;
        private readonly float _strictness;
        private readonly FilterContext _context;
        private readonly int _maxResults;
        private readonly int _maxHeavyEvals;

        public BranchAndBoundScorer(
            List<IFilterPredicate> heavyPredicates,
            int totalCriticals,
            int totalPreferreds,
            float kappa,
            float strictness,
            FilterContext context,
            int maxResults,
            int maxHeavyEvals = 2000)
        {
            _heavyCriticals = heavyPredicates.Where(p => p.Importance == FilterImportance.Critical).ToList();
            _heavyPreferreds = heavyPredicates.Where(p => p.Importance == FilterImportance.Preferred).ToList();
            _totalCriticals = totalCriticals;
            _totalPreferreds = totalPreferreds;
            _kappa = kappa;
            _strictness = strictness;
            _context = context;
            _maxResults = maxResults;
            _maxHeavyEvals = maxHeavyEvals;
        }

        /// <summary>
        /// Scores candidates using branch-and-bound with upper bound pruning.
        /// </summary>
        public List<TileScore> Score(List<CandidateTile> candidates)
        {
            var results = new List<TileScore>(_maxResults);
            float minInHeap = 0f;
            int heavyEvalsUsed = 0;

            Log.Message($"[LandingZone] BranchAndBoundScorer: Processing {candidates.Count} candidates, " +
                       $"maxResults={_maxResults}, budget={_maxHeavyEvals}");

            foreach (var candidate in candidates)
            {
                // Prune: If this candidate's upper bound can't beat worst in heap, skip
                if (results.Count >= _maxResults && candidate.UpperBound <= minInHeap)
                {
                    // All remaining candidates have lower upper bounds (sorted), so we're done
                    break;
                }

                // Budget limit: Stop if we've evaluated too many tiles
                if (heavyEvalsUsed >= _maxHeavyEvals)
                {
                    Log.Warning($"[LandingZone] Branch-and-bound hit budget limit ({_maxHeavyEvals} evals)");
                    break;
                }

                // Evaluate heavy predicates for this tile
                int critHeavyMatches = EvaluateHeavyPredicates(_heavyCriticals, candidate.TileId);
                int prefHeavyMatches = EvaluateHeavyPredicates(_heavyPreferreds, candidate.TileId);
                heavyEvalsUsed++;

                // Compute final scores
                int totalCritMatches = candidate.CritCheapMatches + critHeavyMatches;
                int totalPrefMatches = candidate.PrefCheapMatches + prefHeavyMatches;

                float critScore = ScoringWeights.NormalizeCriticalScore(totalCritMatches, _totalCriticals);
                float prefScore = ScoringWeights.NormalizePreferredScore(totalPrefMatches, _totalPreferreds);

                // Check strictness threshold
                if (critScore < _strictness)
                    continue; // Doesn't meet critical strictness

                float finalScore = ScoringWeights.ComputeFinalScore(critScore, prefScore, _kappa);

                // TODO: Build proper MatchBreakdown (simplified placeholder for now)
                var breakdown = new MatchBreakdown(
                    false, 0f,  // temperature
                    false, 0f,  // rainfall
                    false, 0f,  // growing
                    false, 0f,  // pollution
                    false, 0f,  // forage
                    false, 0f,  // movement
                    FilterImportance.Ignored, false,  // coastal
                    FilterImportance.Ignored, false,  // river
                    FilterImportance.Ignored, false, null,  // feature
                    FilterImportance.Ignored, false,  // graze
                    FilterImportance.Ignored, 0, 0,  // stone
                    true,  // hilliness
                    finalScore
                );

                var tileScore = new TileScore(candidate.TileId, finalScore, breakdown);

                // Insert into Top-N heap
                if (results.Count < _maxResults)
                {
                    results.Add(tileScore);
                    if (results.Count == _maxResults)
                    {
                        // Heap is now full, compute min
                        minInHeap = results.Min(r => r.Score);
                    }
                }
                else
                {
                    // Replace worst if this is better
                    int worstIndex = 0;
                    float worstScore = results[0].Score;
                    for (int i = 1; i < results.Count; i++)
                    {
                        if (results[i].Score < worstScore)
                        {
                            worstScore = results[i].Score;
                            worstIndex = i;
                        }
                    }

                    if (finalScore > worstScore)
                    {
                        results[worstIndex] = tileScore;
                        minInHeap = results.Min(r => r.Score);
                    }
                }
            }

            // Sort results by score descending
            results.Sort((a, b) => b.Score.CompareTo(a.Score));

            Log.Message($"[LandingZone] BranchAndBoundScorer: {results.Count} results, " +
                       $"{heavyEvalsUsed} heavy evaluations used");

            return results;
        }

        private int EvaluateHeavyPredicates(List<IFilterPredicate> predicates, int tileId)
        {
            int matches = 0;
            foreach (var predicate in predicates)
            {
                // Evaluate just this one tile
                // Note: BitArray is inefficient for single-tile evaluation
                // TODO: Add IFilterPredicate.MatchesSingle(tileId) for efficiency
                var bitset = predicate.Evaluate(_context, tileId + 1);
                if (bitset[tileId])
                    matches++;
            }
            return matches;
        }
    }
}

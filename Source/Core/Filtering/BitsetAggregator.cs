#nullable enable
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LandingZone.Data;
using LandingZone.Core.Diagnostics;
using UnityEngine;
using Verse;

namespace LandingZone.Core.Filtering
{
    /// <summary>
    /// Stage A: Cheap aggregate gate using bitset operations.
    /// Evaluates cheap predicates and computes per-tile match counts.
    /// - MustHave: Hard include gates (k-of-n matching)
    /// - MustNotHave: Hard exclude gates (eliminate tiles)
    /// - Priority/Preferred: Scoring predicates (weighted)
    /// Gates candidates by upper bound scoring to keep dataset bounded.
    /// </summary>
    public sealed class BitsetAggregator
    {
        private readonly List<IFilterPredicate> _cheapMustHave;
        private readonly List<IFilterPredicate> _cheapMustNotHave;
        private readonly List<IFilterPredicate> _cheapPriority;
        private readonly List<IFilterPredicate> _cheapPreferred;
        private readonly int _heavyMustHaveCount;
        private readonly int _heavyPriorityCount;
        private readonly int _heavyPreferredCount;
        private readonly int _tileCount;
        private readonly FilterContext _context;

        // Per-tile counts of cheap matches
        private int[] _mustHaveCheapCount;
        private int[] _priorityCheapCount;
        private int[] _preferredCheapCount;
        // Per-tile exclusion flags for MustNotHave
        private bool[] _excludedByMustNotHave;

        public BitsetAggregator(
            List<IFilterPredicate> cheapPredicates,
            int heavyMustHaveCount,
            int heavyPriorityCount,
            int heavyPreferredCount,
            FilterContext context,
            int tileCount)
        {
            _cheapMustHave = cheapPredicates.Where(p => p.Importance == FilterImportance.MustHave).ToList();
            _cheapMustNotHave = cheapPredicates.Where(p => p.Importance == FilterImportance.MustNotHave).ToList();
            _cheapPriority = cheapPredicates.Where(p => p.Importance == FilterImportance.Priority).ToList();
            _cheapPreferred = cheapPredicates.Where(p => p.Importance == FilterImportance.Preferred).ToList();
            _heavyMustHaveCount = heavyMustHaveCount;
            _heavyPriorityCount = heavyPriorityCount;
            _heavyPreferredCount = heavyPreferredCount;
            _context = context;
            _tileCount = tileCount;

            _mustHaveCheapCount = new int[tileCount];
            _priorityCheapCount = new int[tileCount];
            _preferredCheapCount = new int[tileCount];
            _excludedByMustNotHave = new bool[tileCount];
        }

        /// <summary>
        /// Stored cheap counts for later use in Stage B.
        /// Returns (mustHave, priority, preferred) counts per tile.
        /// </summary>
        public (int[] mustHave, int[] priority, int[] preferred) CheapCounts =>
            (_mustHaveCheapCount, _priorityCheapCount, _preferredCheapCount);

        // Legacy compatibility
        /// <summary>
        /// [LEGACY] Returns (critical, preferred) counts. Critical = MustHave, Preferred = Priority + Preferred combined.
        /// </summary>
        public (int[] critical, int[] preferred) LegacyCheapCounts
        {
            get
            {
                // Combine priority + preferred for backwards compatibility
                var combinedPreferred = new int[_tileCount];
                for (int i = 0; i < _tileCount; i++)
                {
                    combinedPreferred[i] = _priorityCheapCount[i] + _preferredCheapCount[i];
                }
                return (_mustHaveCheapCount, combinedPreferred);
            }
        }

        /// <summary>
        /// Gets candidate tiles that pass the cheap aggregate gate.
        /// </summary>
        /// <param name="strictness">MustHave strictness threshold [0,1]</param>
        /// <param name="maxCandidates">Maximum candidates to return (adaptive tightening)</param>
        /// <returns>List of candidates with their upper bound scores</returns>
        public List<CandidateTile> GetCandidates(float strictness, int maxCandidates)
        {
            Stopwatch? phaseASw = null;
            if (DevDiagnostics.PhaseADiagnosticsEnabled)
            {
                phaseASw = Stopwatch.StartNew();
                Log.Message($"[LZ][DIAG] Phase A START: maxCandidates={maxCandidates}, strictness={strictness:F2}, timestamp={System.DateTime.Now:HH:mm:ss.fff}");
            }

            // Step 1: Evaluate all cheap predicates and accumulate counts
            EvaluateAndAccumulate();

            // Step 2: Compute totals for scoring
            // MustHave gates are binary (pass/fail), scoring uses Priority + Preferred
            int totalMustHave = _cheapMustHave.Count + _heavyMustHaveCount;
            int totalPriority = _cheapPriority.Count + _heavyPriorityCount;
            int totalPreferred = _cheapPreferred.Count + _heavyPreferredCount;
            int totalScoring = totalPriority + totalPreferred;

            // Step 3: Compute kappa for score weighting (gates don't contribute to kappa)
            float kappa = ScoringWeights.ComputeKappa(totalMustHave, totalScoring);

            LandingZoneLogger.LogStandard($"[LandingZone] BitsetAggregator: {_cheapMustHave.Count} cheap mustHave, " +
                       $"{_cheapMustNotHave.Count} cheap mustNotHave, " +
                       $"{_cheapPriority.Count} cheap priority, {_cheapPreferred.Count} cheap preferred, " +
                       $"{_heavyMustHaveCount} heavy mustHave, {_heavyPriorityCount} heavy priority, " +
                       $"{_heavyPreferredCount} heavy preferred, κ={kappa:F3}");

            // Step 4: ADAPTIVE K-OF-N FALLBACK for MustHave gates
            int minReasonableCandidates = LandingZoneSettings.MaxCandidatesForHeavyFilters;
            int maxReasonableCandidates = minReasonableCandidates * 10;

            int effectiveMinMustHave = DetermineAdaptiveThreshold(
                minReasonableCandidates,
                maxReasonableCandidates,
                strictness);

            LandingZoneLogger.LogStandard($"[LandingZone] BitsetAggregator: Adaptive threshold = {effectiveMinMustHave}/{_cheapMustHave.Count} cheap MustHave");

            // Step 5: Compute upper bounds and create candidates
            int initialCapacity = Mathf.Min(maxCandidates, 100000);
            var candidates = new List<CandidateTile>(initialCapacity);
            var world = Find.World;
            var worldGrid = world?.grid;

            if (DevDiagnostics.PhaseADiagnosticsEnabled)
            {
                LandingZoneLogger.LogStandard($"[LZ][DIAG] Phase A: tileCount={_tileCount}, cheapMustHave={_cheapMustHave.Count}, " +
                    $"cheapMustNotHave={_cheapMustNotHave.Count}, cheapPriority={_cheapPriority.Count}, " +
                    $"cheapPreferred={_cheapPreferred.Count}, strictness={strictness:F2}");
            }

            int excludedByMustNotHave = 0;
            for (int tileId = 0; tileId < _tileCount; tileId++)
            {
                // Filter out unsettleable tiles (ocean, impassable biomes)
                if (worldGrid != null && world != null)
                {
                    var tile = worldGrid[tileId];
                    var biome = tile?.PrimaryBiome;
                    if (biome == null || biome.impassable || world.Impassable(tileId))
                        continue;
                }

                // HARD GATE: MustNotHave - exclude any tile that matches a MustNotHave predicate
                if (_excludedByMustNotHave[tileId])
                {
                    excludedByMustNotHave++;
                    continue;
                }

                // ADAPTIVE K-OF-N GATE: Tile must match at least effectiveMinMustHave cheap MustHave predicates
                if (_cheapMustHave.Count > 0 && _mustHaveCheapCount[tileId] < effectiveMinMustHave)
                {
                    continue;
                }

                // Upper bound scoring: assume tile matches ALL heavy predicates
                // MustHave gates contribute to binary satisfaction (not weighted)
                float mustHaveScoreUB = (totalMustHave == 0) ? 1f :
                    (_mustHaveCheapCount[tileId] + _heavyMustHaveCount) / (float)totalMustHave;

                // Scoring: Priority has 2x weight, Preferred has 1x weight
                float priorityScore = (totalPriority == 0) ? 0f :
                    (_priorityCheapCount[tileId] + _heavyPriorityCount) / (float)totalPriority;
                float preferredScore = (totalPreferred == 0) ? 0f :
                    (_preferredCheapCount[tileId] + _heavyPreferredCount) / (float)totalPreferred;

                // Combine Priority (2x) and Preferred (1x) with normalized weights
                float scoringScoreUB = (totalScoring == 0) ? 0f :
                    (priorityScore * 2f + preferredScore) / 3f; // Weighted average

                float upperBound = ScoringWeights.ComputeFinalScore(mustHaveScoreUB, scoringScoreUB, kappa);

                // For backwards compatibility, store combined counts
                int combinedScoringCount = _priorityCheapCount[tileId] + _preferredCheapCount[tileId];
                candidates.Add(new CandidateTile(
                    tileId,
                    upperBound,
                    _mustHaveCheapCount[tileId],
                    combinedScoringCount
                ));
            }

            if (excludedByMustNotHave > 0)
            {
                LandingZoneLogger.LogStandard($"[LandingZone] BitsetAggregator: {excludedByMustNotHave} tiles excluded by MustNotHave gates");
            }

            LandingZoneLogger.LogStandard($"[LandingZone] BitsetAggregator: {candidates.Count} candidates pass gates");

            bool capHit = candidates.Count > maxCandidates;
            if (DevDiagnostics.PhaseADiagnosticsEnabled)
            {
                LandingZoneLogger.LogStandard($"[LZ][DIAG] Phase A candidates: {candidates.Count} (cap {maxCandidates}, capHit={capHit})");
            }

            // Step 6: Adaptive tightening if too many candidates
            if (capHit)
            {
                int originalCount = candidates.Count;
                LandingZoneLogger.LogStandard($"[LandingZone] Candidate cap hit! {originalCount} candidates exceed limit of {maxCandidates}. " +
                                              $"Tightening to top {maxCandidates} by score. Consider using stricter filters or increasing Max Candidate Tiles in settings.");

                candidates.Sort((a, b) => b.UpperBound.CompareTo(a.UpperBound));
                candidates = candidates.Take(maxCandidates).ToList();
            }

            // Step 7: Sort by upper bound descending for branch-and-bound
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

            if (DevDiagnostics.PhaseADiagnosticsEnabled)
            {
                LandingZoneLogger.LogStandard($"[LZ][DIAG] Phase A candidates: {candidates.Count} (cap {maxCandidates}, capHit={capHit})");
                phaseASw?.Stop();
                Log.Message($"[LZ][DIAG] Phase A END: candidates={candidates.Count}, elapsed_ms={phaseASw?.ElapsedMilliseconds ?? 0}");
            }

            return candidates;
        }

        private void EvaluateAndAccumulate()
        {
            // Evaluate cheap MustHave predicates (hard include gates)
            foreach (var predicate in _cheapMustHave)
            {
                var bitset = predicate.Evaluate(_context, _tileCount);
                if (DevDiagnostics.PhaseADiagnosticsEnabled)
                {
                    int matches = CountBits(bitset);
                    LandingZoneLogger.LogStandard($"[LZ][DIAG] CheapMustHave {predicate.Id ?? predicate.GetType().Name}: matches={matches}");
                }
                AccumulateCounts(bitset, _mustHaveCheapCount);
            }

            // Evaluate cheap MustNotHave predicates (hard exclude gates)
            // Any tile that matches a MustNotHave predicate is excluded
            foreach (var predicate in _cheapMustNotHave)
            {
                var bitset = predicate.Evaluate(_context, _tileCount);
                if (DevDiagnostics.PhaseADiagnosticsEnabled)
                {
                    int matches = CountBits(bitset);
                    LandingZoneLogger.LogStandard($"[LZ][DIAG] CheapMustNotHave {predicate.Id ?? predicate.GetType().Name}: matches={matches} (will EXCLUDE)");
                }
                // For MustNotHave, matching tiles are EXCLUDED
                for (int i = 0; i < bitset.Length; i++)
                {
                    if (bitset[i])
                        _excludedByMustNotHave[i] = true;
                }
            }

            // Evaluate cheap Priority predicates (high-weight scoring)
            foreach (var predicate in _cheapPriority)
            {
                var bitset = predicate.Evaluate(_context, _tileCount);
                if (DevDiagnostics.PhaseADiagnosticsEnabled)
                {
                    int matches = CountBits(bitset);
                    LandingZoneLogger.LogStandard($"[LZ][DIAG] CheapPriority {predicate.Id ?? predicate.GetType().Name}: matches={matches}");
                }
                AccumulateCounts(bitset, _priorityCheapCount);
            }

            // Evaluate cheap Preferred predicates (normal-weight scoring)
            foreach (var predicate in _cheapPreferred)
            {
                var bitset = predicate.Evaluate(_context, _tileCount);
                if (DevDiagnostics.PhaseADiagnosticsEnabled)
                {
                    int matches = CountBits(bitset);
                    LandingZoneLogger.LogStandard($"[LZ][DIAG] CheapPreferred {predicate.Id ?? predicate.GetType().Name}: matches={matches}");
                }
                AccumulateCounts(bitset, _preferredCheapCount);
            }
        }

        private static int CountBits(BitArray bitset)
        {
            int count = 0;
            for (int i = 0; i < bitset.Length; i++)
            {
                if (bitset[i]) count++;
            }
            return count;
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
        /// Determines the minimum number of cheap MustHave predicates a tile must match to pass Stage A.
        /// Uses adaptive k-of-n fallback to ensure a reasonable candidate count for Heavy filter processing.
        /// </summary>
        /// <param name="minCandidates">Minimum desired candidates (e.g., 1000)</param>
        /// <param name="maxCandidates">Maximum desired candidates (e.g., 10000)</param>
        /// <param name="strictness">User's strictness setting (informational)</param>
        /// <returns>Minimum cheap MustHave matches required (k in k-of-n)</returns>
        private int DetermineAdaptiveThreshold(int minCandidates, int maxCandidates, float strictness)
        {
            // If no cheap MustHave predicates, no filtering possible
            if (_cheapMustHave.Count == 0)
                return 0;

            // Count tiles matching k cheap MustHave predicates for each k
            int[] tileCounts = new int[_cheapMustHave.Count + 1]; // tileCounts[k] = # tiles matching ≥k MustHave
            var world = Find.World;
            var worldGrid = world?.grid;

            for (int tileId = 0; tileId < _tileCount; tileId++)
            {
                // Skip unsettleable tiles
                if (worldGrid != null && world != null)
                {
                    var tile = worldGrid[tileId];
                    var biome = tile?.PrimaryBiome;
                    if (biome == null || biome.impassable || world.Impassable(tileId))
                        continue;
                }

                // Also skip tiles excluded by MustNotHave
                if (_excludedByMustNotHave[tileId])
                    continue;

                int matchCount = _mustHaveCheapCount[tileId];
                for (int k = 0; k <= matchCount; k++)
                {
                    tileCounts[k]++;
                }
            }

            // Log tile distribution for transparency
            LandingZoneLogger.LogStandard($"[LandingZone] BitsetAggregator: k-of-n distribution:");
            for (int k = _cheapMustHave.Count; k >= 0; k--)
            {
                LandingZoneLogger.LogStandard($"  {k}/{_cheapMustHave.Count}: {tileCounts[k]} tiles");
            }

            // Find the best k (highest match requirement) that produces a reasonable candidate count
            // Start from strictest (all MustHave) and relax until we have enough candidates
            for (int k = _cheapMustHave.Count; k >= 0; k--)
            {
                int candidateCount = tileCounts[k];

                // Perfect: Within target range
                if (candidateCount >= minCandidates && candidateCount <= maxCandidates)
                {
                    LandingZoneLogger.LogStandard($"[LandingZone] BitsetAggregator: Selected {k}/{_cheapMustHave.Count} (produces {candidateCount} candidates, target: {minCandidates}-{maxCandidates})");
                    return k;
                }

                // Good enough: Exceeds minimum
                if (candidateCount >= minCandidates)
                {
                    LandingZoneLogger.LogStandard($"[LandingZone] BitsetAggregator: Selected {k}/{_cheapMustHave.Count} (produces {candidateCount} candidates, exceeds minimum {minCandidates})");
                    return k;
                }
            }

            // Fallback: No k produces enough candidates, use 0 (accept all)
            LandingZoneLogger.LogWarning($"[LandingZone] BitsetAggregator: No k-of-n threshold produces {minCandidates}+ candidates. Using 0/{_cheapMustHave.Count} (all tiles).");
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

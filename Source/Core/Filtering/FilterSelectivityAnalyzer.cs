using System;
using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using Verse;

namespace LandingZone.Core.Filtering
{
    /// <summary>
    /// Analyzes filter selectivity to predict match likelihood.
    /// Selectivity = (tiles that pass filter) / (total tiles)
    /// Used to warn users about overly restrictive filter combinations.
    /// </summary>
    public sealed class FilterSelectivityAnalyzer
    {
        private readonly Dictionary<string, FilterSelectivity> _cache = new Dictionary<string, FilterSelectivity>();
        private string _lastWorldSeed = string.Empty;

        /// <summary>
        /// Analyzes a predicate's selectivity by evaluating it on all tiles.
        /// Results are cached per world seed for performance.
        /// </summary>
        public FilterSelectivity AnalyzePredicate(IFilterPredicate predicate, FilterContext context, int totalTiles)
        {
            // Check if world changed
            string currentSeed = Find.World?.info?.seedString ?? string.Empty;
            if (currentSeed != _lastWorldSeed)
            {
                _cache.Clear();
                _lastWorldSeed = currentSeed;
            }

            // Return cached result if available
            if (_cache.TryGetValue(predicate.Id, out var cached))
                return cached;

            // Evaluate predicate on all tiles
            var bitset = predicate.Evaluate(context, totalTiles);
            int matchCount = 0;

            for (int i = 0; i < bitset.Length; i++)
            {
                if (bitset[i])
                    matchCount++;
            }

            var selectivity = new FilterSelectivity(
                predicate.Id,
                predicate.Importance,
                matchCount,
                totalTiles,
                predicate.IsHeavy
            );

            _cache[predicate.Id] = selectivity;

            Log.Message($"[LandingZone] Selectivity: {predicate.Id} = {selectivity.Ratio:P2} " +
                       $"({matchCount:N0}/{totalTiles:N0} tiles, {(predicate.IsHeavy ? "heavy" : "cheap")})");

            return selectivity;
        }

        /// <summary>
        /// Analyzes all critical predicates and returns their selectivities.
        /// This is fast for cheap predicates (~100ms) but may be slow for multiple heavy predicates.
        /// </summary>
        public List<FilterSelectivity> AnalyzeCriticals(
            List<IFilterPredicate> predicates,
            FilterContext context,
            int totalTiles)
        {
            var criticals = predicates.Where(p => p.Importance == FilterImportance.Critical).ToList();
            var results = new List<FilterSelectivity>(criticals.Count);

            foreach (var predicate in criticals)
            {
                results.Add(AnalyzePredicate(predicate, context, totalTiles));
            }

            return results;
        }

        public void Clear()
        {
            _cache.Clear();
            _lastWorldSeed = string.Empty;
        }
    }

    /// <summary>
    /// Result of analyzing a filter's selectivity.
    /// </summary>
    public readonly struct FilterSelectivity
    {
        public FilterSelectivity(string filterId, FilterImportance importance, int matchCount, int totalTiles, bool isHeavy)
        {
            FilterId = filterId;
            Importance = importance;
            MatchCount = matchCount;
            TotalTiles = totalTiles;
            IsHeavy = isHeavy;
        }

        public string FilterId { get; }
        public FilterImportance Importance { get; }
        public int MatchCount { get; }
        public int TotalTiles { get; }
        public bool IsHeavy { get; }

        /// <summary>
        /// Selectivity ratio: 0.0 (no tiles match) to 1.0 (all tiles match)
        /// </summary>
        public float Ratio => TotalTiles == 0 ? 0f : (float)MatchCount / TotalTiles;

        /// <summary>
        /// Categorizes how restrictive this filter is
        /// </summary>
        public SelectivityCategory Category
        {
            get
            {
                if (Ratio >= 0.50f) return SelectivityCategory.VeryCommon;    // 50%+
                if (Ratio >= 0.20f) return SelectivityCategory.Common;         // 20-50%
                if (Ratio >= 0.05f) return SelectivityCategory.Uncommon;       // 5-20%
                if (Ratio >= 0.01f) return SelectivityCategory.Rare;           // 1-5%
                if (Ratio >= 0.001f) return SelectivityCategory.VeryRare;      // 0.1-1%
                return SelectivityCategory.ExtremelyRare;                      // <0.1%
            }
        }
    }

    public enum SelectivityCategory
    {
        VeryCommon,      // 50%+ of tiles (e.g., flat/hills, moderate temp)
        Common,          // 20-50% of tiles (e.g., granite, good growing)
        Uncommon,        // 5-20% of tiles (e.g., specific biome, river)
        Rare,            // 1-5% of tiles (e.g., coastal + river)
        VeryRare,        // 0.1-1% of tiles (e.g., road, specific feature)
        ExtremelyRare    // <0.1% of tiles (e.g., landmarks, road+river)
    }
}

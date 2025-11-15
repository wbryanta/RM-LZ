using System.Collections.Generic;

namespace LandingZone.Data
{
    /// <summary>
    /// Information about a single filter's match status for a tile.
    /// </summary>
    public readonly struct FilterMatchInfo
    {
        public FilterMatchInfo(
            string filterName,
            FilterImportance importance,
            float membership,
            bool isMatched,
            bool isRangeFilter,
            float penalty)
        {
            FilterName = filterName;
            Importance = importance;
            Membership = membership;
            IsMatched = isMatched;
            IsRangeFilter = isRangeFilter;
            Penalty = penalty;
        }

        /// <summary>
        /// Display name of the filter (e.g., "Temperature", "Rainfall", "Caves")
        /// </summary>
        public string FilterName { get; }

        /// <summary>
        /// Filter importance level
        /// </summary>
        public FilterImportance Importance { get; }

        /// <summary>
        /// Membership score [0,1] for range filters, or 1.0/0.0 for boolean
        /// </summary>
        public float Membership { get; }

        /// <summary>
        /// True if filter requirement was met (membership above threshold)
        /// </summary>
        public bool IsMatched { get; }

        /// <summary>
        /// True if this is a range filter (can have degrees of miss), false for boolean
        /// </summary>
        public bool IsRangeFilter { get; }

        /// <summary>
        /// Penalty applied to final score if missed (0 if matched)
        /// </summary>
        public float Penalty { get; }

        /// <summary>
        /// True if this is a "near miss" - ONLY for range filters where delta is small.
        /// Boolean filters cannot be near misses (either matched or missed).
        /// </summary>
        public bool IsNearMiss => !IsMatched && IsRangeFilter && Membership >= 0.85f;

        /// <summary>
        /// True if this is a critical filter
        /// </summary>
        public bool IsCritical => Importance == FilterImportance.Critical;
    }

    /// <summary>
    /// Information about a mutator's contribution to the final score.
    /// </summary>
    public readonly struct MutatorContribution
    {
        public MutatorContribution(string mutatorName, int qualityRating, float contribution)
        {
            MutatorName = mutatorName;
            QualityRating = qualityRating;
            Contribution = contribution;
        }

        /// <summary>
        /// Mutator def name (e.g., "Fish_Increased", "Fertile")
        /// </summary>
        public string MutatorName { get; }

        /// <summary>
        /// Quality rating from -10 to +10
        /// </summary>
        public int QualityRating { get; }

        /// <summary>
        /// Actual contribution to final score (can be positive or negative)
        /// </summary>
        public float Contribution { get; }

        /// <summary>
        /// True if this mutator has a positive effect
        /// </summary>
        public bool IsPositive => Contribution > 0f;

        /// <summary>
        /// True if this mutator has a negative effect
        /// </summary>
        public bool IsNegative => Contribution < 0f;
    }

    /// <summary>
    /// Complete breakdown of how a tile's score was calculated.
    /// Contains per-filter match status and mutator contributions.
    /// </summary>
    public readonly struct MatchBreakdownV2
    {
        public MatchBreakdownV2(
            List<FilterMatchInfo> matchedFilters,
            List<FilterMatchInfo> missedFilters,
            List<MutatorContribution> mutators,
            float criticalScore,
            float preferredScore,
            float mutatorScore,
            float penalty,
            float finalScore)
        {
            MatchedFilters = matchedFilters;
            MissedFilters = missedFilters;
            Mutators = mutators;
            CriticalScore = criticalScore;
            PreferredScore = preferredScore;
            MutatorScore = mutatorScore;
            Penalty = penalty;
            FinalScore = finalScore;
        }

        /// <summary>
        /// Filters that the tile successfully matched
        /// </summary>
        public IReadOnlyList<FilterMatchInfo> MatchedFilters { get; }

        /// <summary>
        /// Filters that the tile failed to match
        /// </summary>
        public IReadOnlyList<FilterMatchInfo> MissedFilters { get; }

        /// <summary>
        /// Mutator contributions (positive and negative)
        /// </summary>
        public IReadOnlyList<MutatorContribution> Mutators { get; }

        /// <summary>
        /// Aggregated critical filter score [0,1]
        /// </summary>
        public float CriticalScore { get; }

        /// <summary>
        /// Aggregated preferred filter score [0,1]
        /// </summary>
        public float PreferredScore { get; }

        /// <summary>
        /// Mutator quality score [0,1]
        /// </summary>
        public float MutatorScore { get; }

        /// <summary>
        /// Total penalty applied from missed filters
        /// </summary>
        public float Penalty { get; }

        /// <summary>
        /// Final composite score after all factors
        /// </summary>
        public float FinalScore { get; }

        /// <summary>
        /// Total number of critical filters that were missed
        /// </summary>
        public int CriticalMissCount
        {
            get
            {
                int count = 0;
                foreach (var missed in MissedFilters)
                {
                    if (missed.IsCritical)
                        count++;
                }
                return count;
            }
        }

        /// <summary>
        /// True if this is a perfect match (all criticals matched with 1.0 membership)
        /// </summary>
        public bool IsPerfectMatch
        {
            get
            {
                // Must have no critical misses
                if (CriticalMissCount > 0) return false;

                // All critical filters must have 1.0 membership
                foreach (var matched in MatchedFilters)
                {
                    if (matched.IsCritical && matched.Membership < 1.0f)
                        return false;
                }

                return true;
            }
        }

        /// <summary>
        /// True if this is a perfect+ match (perfect match + positive modifiers pushing score > 1.0)
        /// </summary>
        public bool IsPerfectPlus => IsPerfectMatch && FinalScore > 1.0f;
    }
}

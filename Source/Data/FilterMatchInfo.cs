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
        /// True if this is a Priority filter (scored at 2x weight).
        /// In 5-state model: Priority filters contribute more to final score.
        /// </summary>
        public bool IsPriority => Importance == FilterImportance.Priority;
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
    ///
    /// 5-State Scoring Model:
    /// - MustHave/MustNotHave: Gates only (pass/fail in Apply phase, not in breakdown)
    /// - Priority: Scored at 2x weight (in CriticalScore for backward compat)
    /// - Preferred: Scored at 1x weight (in PreferredScore)
    /// - Mutators: Ambient bonus (in MutatorScore)
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
        /// Filters that the tile successfully matched (Priority + Preferred only)
        /// </summary>
        public IReadOnlyList<FilterMatchInfo> MatchedFilters { get; }

        /// <summary>
        /// Filters that the tile failed to match (Priority + Preferred only)
        /// </summary>
        public IReadOnlyList<FilterMatchInfo> MissedFilters { get; }

        /// <summary>
        /// Mutator contributions (positive and negative)
        /// </summary>
        public IReadOnlyList<MutatorContribution> Mutators { get; }

        /// <summary>
        /// Aggregated Priority filter score [0,1] (historically called "CriticalScore" for backward compat)
        /// In the 5-state model, this is the weighted average of Priority filter memberships.
        /// </summary>
        public float CriticalScore { get; }

        /// <summary>
        /// Aggregated Preferred filter score [0,1]
        /// </summary>
        public float PreferredScore { get; }

        /// <summary>
        /// Mutator quality score [0,1]
        /// </summary>
        public float MutatorScore { get; }

        /// <summary>
        /// Penalty term [0,1]. In 5-state model, always 1.0 (no penalty - gates are binary).
        /// Kept for backward compatibility with UI.
        /// </summary>
        public float Penalty { get; }

        /// <summary>
        /// Final composite score after all factors
        /// </summary>
        public float FinalScore { get; }

        /// <summary>
        /// Count of Priority filters that were missed.
        /// (Historically called CriticalMissCount for backward compat)
        /// In 5-state model, MustHave gates don't appear here - they filter in Apply phase.
        /// </summary>
        public int CriticalMissCount
        {
            get
            {
                int count = 0;
                foreach (var missed in MissedFilters)
                {
                    // Priority filters are the "high importance" scoring filters
                    if (missed.Importance == FilterImportance.Priority)
                        count++;
                }
                return count;
            }
        }

        /// <summary>
        /// True if all Priority filters matched with high membership.
        /// (Historically called IsPerfectMatch for backward compat)
        /// In 5-state model, this means all Priority filters have membership >= 0.9.
        /// </summary>
        public bool IsPerfectMatch
        {
            get
            {
                // Must have no Priority misses
                if (CriticalMissCount > 0) return false;

                // All Priority filters must have high membership
                foreach (var matched in MatchedFilters)
                {
                    if (matched.Importance == FilterImportance.Priority && matched.Membership < 0.9f)
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

    /// <summary>
    /// Represents a single original requirement from the user's filter settings.
    /// Used to track which MustHave/MustNotHave requirements a relaxed result satisfies.
    /// </summary>
    public readonly struct OriginalRequirement
    {
        public OriginalRequirement(string filterId, string displayName, bool isMustNotHave)
        {
            FilterId = filterId;
            DisplayName = displayName;
            IsMustNotHave = isMustNotHave;
        }

        /// <summary>
        /// Internal filter identifier (e.g., "coastal", "rivers", "caves")
        /// </summary>
        public string FilterId { get; }

        /// <summary>
        /// Human-readable display name (e.g., "Coastal", "Huge River", "Caves")
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// True if this was a MustNotHave requirement (exclusion), false if MustHave (inclusion)
        /// </summary>
        public bool IsMustNotHave { get; }
    }

    /// <summary>
    /// Tracks how well a tile satisfies the original requirements from before relaxation.
    /// </summary>
    public class RelaxedMatchInfo
    {
        public RelaxedMatchInfo(int tileId, List<OriginalRequirement> allRequirements)
        {
            TileId = tileId;
            AllRequirements = allRequirements ?? new List<OriginalRequirement>();
            SatisfiedRequirements = new List<OriginalRequirement>();
            ViolatedRequirements = new List<OriginalRequirement>();
        }

        public int TileId { get; }

        /// <summary>
        /// All original MustHave/MustNotHave requirements.
        /// </summary>
        public List<OriginalRequirement> AllRequirements { get; }

        /// <summary>
        /// Requirements that this tile satisfies.
        /// </summary>
        public List<OriginalRequirement> SatisfiedRequirements { get; }

        /// <summary>
        /// Requirements that this tile violates.
        /// </summary>
        public List<OriginalRequirement> ViolatedRequirements { get; }

        /// <summary>
        /// Count of satisfied requirements.
        /// </summary>
        public int SatisfiedCount => SatisfiedRequirements.Count;

        /// <summary>
        /// Total requirement count.
        /// </summary>
        public int TotalCount => AllRequirements.Count;

        /// <summary>
        /// Badge text like "[2/3]"
        /// </summary>
        public string BadgeText => $"[{SatisfiedCount}/{TotalCount}]";
    }
}

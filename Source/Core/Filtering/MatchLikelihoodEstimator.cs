using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LandingZone.Core.Filtering
{
    /// <summary>
    /// Estimates the likelihood of finding matches given multiple critical filters.
    /// Uses filter selectivity ratios to predict how many tiles will pass all criticals.
    /// </summary>
    public static class MatchLikelihoodEstimator
    {
        /// <summary>
        /// Estimates match likelihood for all criticals at full strictness (must match all).
        /// Uses independent probability assumption: P(A AND B) ≈ P(A) × P(B)
        /// This is an upper bound estimate (reality may have correlations).
        /// </summary>
        public static MatchLikelihood EstimateAllCriticals(List<FilterSelectivity> criticals)
        {
            if (criticals.Count == 0)
            {
                return new MatchLikelihood(
                    1.0f,
                    criticals[0].TotalTiles,
                    LikelihoodCategory.Guaranteed,
                    "No critical filters"
                );
            }

            // Multiply selectivity ratios (independent probability assumption)
            float combinedRatio = 1.0f;
            foreach (var crit in criticals)
            {
                combinedRatio *= crit.Ratio;
            }

            int estimatedMatches = (int)(criticals[0].TotalTiles * combinedRatio);
            var category = CategorizeByCount(estimatedMatches);
            string description = BuildDescription(criticals, combinedRatio, estimatedMatches);

            return new MatchLikelihood(combinedRatio, estimatedMatches, category, description);
        }

        /// <summary>
        /// Estimates match likelihood when using k-of-n strictness (e.g., "3 of 4 required").
        /// This is more complex because a tile can match in multiple ways.
        /// We use a simplified approximation based on binomial probability.
        /// </summary>
        public static MatchLikelihood EstimateRelaxedCriticals(
            List<FilterSelectivity> criticals,
            float strictness)
        {
            if (criticals.Count == 0)
            {
                return EstimateAllCriticals(criticals);
            }

            int requiredMatches = (int)Math.Ceiling(criticals.Count * strictness);

            // If requiring all, use exact calculation
            if (requiredMatches >= criticals.Count)
            {
                return EstimateAllCriticals(criticals);
            }

            // Approximate: tiles that match k-of-n have probability much higher than matching all
            // Simple heuristic: Use the product of the k most restrictive filters
            var sortedByRatio = criticals.OrderBy(c => c.Ratio).ToList();
            var mostRestrictive = sortedByRatio.Take(requiredMatches).ToList();

            float combinedRatio = 1.0f;
            foreach (var crit in mostRestrictive)
            {
                combinedRatio *= crit.Ratio;
            }

            int estimatedMatches = (int)(criticals[0].TotalTiles * combinedRatio);
            var category = CategorizeByCount(estimatedMatches);
            string description = $"{requiredMatches} of {criticals.Count} criticals required: " +
                               $"~{estimatedMatches:N0} tiles estimated";

            return new MatchLikelihood(combinedRatio, estimatedMatches, category, description);
        }

        /// <summary>
        /// Suggests optimal strictness levels with estimated match counts.
        /// Returns suggestions ordered from most to least restrictive.
        /// </summary>
        public static List<StrictnessSuggestion> SuggestStrictness(List<FilterSelectivity> criticals)
        {
            if (criticals.Count == 0)
                return new List<StrictnessSuggestion>();

            var suggestions = new List<StrictnessSuggestion>();

            // Full strictness (1.0)
            var full = EstimateAllCriticals(criticals);
            suggestions.Add(new StrictnessSuggestion(
                1.0f,
                criticals.Count,
                criticals.Count,
                full.EstimatedMatches,
                "All criticals required",
                full.Category
            ));

            // Relaxed options (if 3+ criticals)
            if (criticals.Count >= 3)
            {
                for (int required = criticals.Count - 1; required >= Math.Max(1, criticals.Count - 2); required--)
                {
                    float strictness = (float)required / criticals.Count;
                    var estimate = EstimateRelaxedCriticals(criticals, strictness);

                    suggestions.Add(new StrictnessSuggestion(
                        strictness,
                        required,
                        criticals.Count,
                        estimate.EstimatedMatches,
                        $"{required} of {criticals.Count} criticals",
                        estimate.Category
                    ));
                }
            }

            return suggestions;
        }

        private static LikelihoodCategory CategorizeByCount(int estimatedMatches)
        {
            if (estimatedMatches >= 1000) return LikelihoodCategory.VeryHigh;    // 1000+ tiles
            if (estimatedMatches >= 100) return LikelihoodCategory.High;         // 100-1000 tiles
            if (estimatedMatches >= 10) return LikelihoodCategory.Medium;        // 10-100 tiles
            if (estimatedMatches >= 1) return LikelihoodCategory.Low;            // 1-10 tiles
            if (estimatedMatches == 0) return LikelihoodCategory.Impossible;     // 0 tiles
            return LikelihoodCategory.VeryLow;                                   // <1 tile (0.1-0.9)
        }

        private static string BuildDescription(List<FilterSelectivity> criticals, float combinedRatio, int estimatedMatches)
        {
            var sb = new StringBuilder();
            sb.Append($"{criticals.Count} critical filters: ");
            sb.Append(string.Join(" + ", criticals.Select(c => c.FilterId)));
            sb.Append($" ≈ {combinedRatio:P3} of world ({estimatedMatches:N0} tiles)");

            // Add warning for extremely rare combinations
            if (estimatedMatches < 10)
            {
                sb.Append(" ⚠️ Very restrictive!");
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Result of estimating match likelihood
    /// </summary>
    public readonly struct MatchLikelihood
    {
        public MatchLikelihood(float probability, int estimatedMatches, LikelihoodCategory category, string description)
        {
            Probability = probability;
            EstimatedMatches = estimatedMatches;
            Category = category;
            Description = description;
        }

        /// <summary>
        /// Estimated probability (0.0 to 1.0) that a random tile passes all filters
        /// </summary>
        public float Probability { get; }

        /// <summary>
        /// Estimated number of tiles that will pass all filters
        /// </summary>
        public int EstimatedMatches { get; }

        /// <summary>
        /// Categorical assessment of likelihood
        /// </summary>
        public LikelihoodCategory Category { get; }

        /// <summary>
        /// Human-readable description
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Returns a user-friendly message for this likelihood
        /// </summary>
        public string GetUserMessage()
        {
            return Category switch
            {
                LikelihoodCategory.Guaranteed => "✓ Plenty of matches expected",
                LikelihoodCategory.VeryHigh => $"✓ Good match likelihood (~{EstimatedMatches:N0} tiles)",
                LikelihoodCategory.High => $"✓ Decent match likelihood (~{EstimatedMatches:N0} tiles)",
                LikelihoodCategory.Medium => $"⚠️ Limited matches expected (~{EstimatedMatches:N0} tiles)",
                LikelihoodCategory.Low => $"⚠️ Very few matches expected (~{EstimatedMatches:N0} tiles)",
                LikelihoodCategory.VeryLow => "🚨 Extremely unlikely to find matches (< 1 tile estimated)",
                LikelihoodCategory.Impossible => "🚨 No matches expected (impossible combination)",
                _ => Description
            };
        }
    }

    public enum LikelihoodCategory
    {
        Guaranteed,    // No filters or very permissive
        VeryHigh,      // 1000+ expected matches
        High,          // 100-1000 expected matches
        Medium,        // 10-100 expected matches
        Low,           // 1-10 expected matches
        VeryLow,       // < 1 expected match
        Impossible     // 0 expected matches
    }

    /// <summary>
    /// Suggestion for a specific strictness level with estimated results
    /// </summary>
    public readonly struct StrictnessSuggestion
    {
        public StrictnessSuggestion(
            float strictness,
            int requiredMatches,
            int totalCriticals,
            int estimatedResults,
            string description,
            LikelihoodCategory category)
        {
            Strictness = strictness;
            RequiredMatches = requiredMatches;
            TotalCriticals = totalCriticals;
            EstimatedResults = estimatedResults;
            Description = description;
            Category = category;
        }

        public float Strictness { get; }
        public int RequiredMatches { get; }
        public int TotalCriticals { get; }
        public int EstimatedResults { get; }
        public string Description { get; }
        public LikelihoodCategory Category { get; }

        public string GetDisplayText()
        {
            string icon = Category switch
            {
                LikelihoodCategory.Guaranteed => "✓",
                LikelihoodCategory.VeryHigh => "✓",
                LikelihoodCategory.High => "✓",
                LikelihoodCategory.Medium => "⚠️",
                LikelihoodCategory.Low => "⚠️",
                _ => "🚨"
            };

            return $"{icon} {Description}: ~{EstimatedResults:N0} tiles";
        }
    }
}

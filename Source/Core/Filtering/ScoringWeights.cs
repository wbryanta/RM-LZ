using UnityEngine;

namespace LandingZone.Core.Filtering
{
    /// <summary>
    /// Utilities for computing dynamic scoring weights in k-of-n evaluation.
    /// </summary>
    public static class ScoringWeights
    {
        /// <summary>
        /// Computes κ (kappa): the weight given to critical filters in final score.
        /// Formula: κ = 0.5 + 0.5 · |C|^p / (|C|^p + |P|^p), where p=1 by default.
        ///
        /// Examples:
        ///   - 5 critical, 2 preferred → κ ≈ 0.86 (critical-dominant)
        ///   - 3 critical, 5 preferred → κ ≈ 0.69 (balanced)
        ///   - 1 critical, 8 preferred → κ ≈ 0.56 (preferred-dominant)
        ///   - 0 critical, N preferred → κ = 0 (only preferred matter)
        ///   - N critical, 0 preferred → κ = 1 (only critical matter)
        /// </summary>
        /// <param name="criticalCount">Number of critical filters</param>
        /// <param name="preferredCount">Number of preferred filters</param>
        /// <param name="exponent">Power exponent (default 1.0 for linear)</param>
        /// <returns>Critical weight κ ∈ [0, 1]</returns>
        public static float ComputeKappa(int criticalCount, int preferredCount, float exponent = 1.0f)
        {
            // Edge cases
            if (criticalCount == 0 && preferredCount == 0)
                return 0.5f; // No filters - arbitrary

            if (criticalCount == 0)
                return 0f; // Only preferred filters matter

            if (preferredCount == 0)
                return 1f; // Only critical filters matter

            // Apply power exponent
            float critPowered = Mathf.Pow(criticalCount, exponent);
            float prefPowered = Mathf.Pow(preferredCount, exponent);

            // κ = 0.5 + 0.5 · C^p / (C^p + P^p)
            float ratio = critPowered / (critPowered + prefPowered);
            return 0.5f + 0.5f * ratio;
        }

        /// <summary>
        /// Computes normalized critical score: S_C = matches / total.
        /// </summary>
        public static float NormalizeCriticalScore(int matches, int total)
        {
            if (total == 0) return 1f; // No criticals = perfect score
            return (float)matches / total;
        }

        /// <summary>
        /// Computes normalized preferred score: S_P = matches / total.
        /// </summary>
        public static float NormalizePreferredScore(int matches, int total)
        {
            if (total == 0) return 0f; // No preferreds = no bonus
            return (float)matches / total;
        }

        /// <summary>
        /// Computes final combined score: S = κ·S_C + (1-κ)·S_P.
        /// </summary>
        public static float ComputeFinalScore(
            float criticalScore,
            float preferredScore,
            float kappa)
        {
            return kappa * criticalScore + (1f - kappa) * preferredScore;
        }
    }
}

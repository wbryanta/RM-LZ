using UnityEngine;

namespace LandingZone.Core.Filtering
{
    /// <summary>
    /// Utilities for computing dynamic scoring weights.
    ///
    /// ARCHITECTURE NOTE (v0.4.1+):
    /// The primary scoring model is the 5-STATE SCORING MODEL:
    /// - MustHave/MustNotHave = Binary gates (pass/fail in Apply phase)
    /// - Priority = Scored at 2x weight
    /// - Preferred = Scored at 1x weight
    /// - Mutators = Ambient bonus (10%)
    ///
    /// Use ComputeScoringWeights() and ComputeScoringScore() for new code.
    ///
    /// The legacy KAPPA-BASED methods (ComputeKappa, ComputeFinalScore) are still used
    /// for upper-bound calculation in BitsetAggregator.
    /// </summary>
    public static class ScoringWeights
    {
        // ============================================================
        // LEGACY KAPPA-BASED SCORING
        // Used by: BitsetAggregator (upper bound calc)
        // ============================================================

        /// <summary>
        /// [LEGACY] Computes κ (kappa): weight for critical vs preferred in final score.
        /// Used by BitsetAggregator for candidate upper bound calculation.
        /// Formula: κ = 0.5 + 0.5 · |C|^p / (|C|^p + |P|^p), where p=1 by default.
        /// </summary>
        /// <param name="criticalCount">Number of critical filters (MustHave in new model)</param>
        /// <param name="preferredCount">Number of scoring filters (Priority + Preferred in new model)</param>
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
        /// [LEGACY] Computes normalized critical score: S_C = matches / total.
        /// Used by legacy K-OF-N fallback path.
        /// </summary>
        public static float NormalizeCriticalScore(int matches, int total)
        {
            if (total == 0) return 1f; // No criticals = perfect score
            return (float)matches / total;
        }

        /// <summary>
        /// [LEGACY] Computes normalized preferred score: S_P = matches / total.
        /// Used by legacy K-OF-N fallback path.
        /// </summary>
        public static float NormalizePreferredScore(int matches, int total)
        {
            if (total == 0) return 0f; // No preferreds = no bonus
            return (float)matches / total;
        }

        /// <summary>
        /// [LEGACY] Computes final combined score: S = κ·S_C + (1-κ)·S_P.
        /// Used by BitsetAggregator for upper bound calculation and legacy K-OF-N fallback.
        /// </summary>
        public static float ComputeFinalScore(
            float criticalScore,
            float preferredScore,
            float kappa)
        {
            return kappa * criticalScore + (1f - kappa) * preferredScore;
        }

        // ============================================================
        // MEMBERSHIP-BASED SCORING (CURRENT SYSTEM)
        // ============================================================

        /// <summary>
        /// Computes weighted group score from membership values.
        /// S_group = Σ(w_i·μ_i) / Σ(w_i)
        ///
        /// Used for both Priority and Preferred aggregation in 5-state model.
        /// </summary>
        /// <param name="memberships">Per-filter membership scores [0,1]</param>
        /// <param name="weights">Per-filter weights (from ranking or importance)</param>
        /// <returns>Weighted average membership [0,1]</returns>
        public static float ComputeGroupScore(float[] memberships, float[] weights)
        {
            if (memberships == null || memberships.Length == 0)
                return 0f;

            if (weights == null || weights.Length == 0)
                return 0f;

            if (memberships.Length != weights.Length)
            {
                Verse.Log.Warning($"[LandingZone] Membership/weight length mismatch: {memberships.Length} vs {weights.Length}");
                return 0f;
            }

            float weightedSum = 0f;
            float weightSum = 0f;

            for (int i = 0; i < memberships.Length; i++)
            {
                weightedSum += weights[i] * memberships[i];
                weightSum += weights[i];
            }

            return weightSum > 0 ? weightedSum / weightSum : 0f;
        }

        // ============================================================
        // 5-STATE SCORING MODEL (PRIMARY)
        // ============================================================
        // MustHave/MustNotHave = Gates only (binary pass/fail in Apply)
        // Priority = Scored at 2x weight
        // Preferred = Scored at 1x weight
        // Mutators = Ambient bonus (10%)
        // ============================================================

        /// <summary>
        /// Computes global weights for the 5-state scoring model.
        /// Only Priority, Preferred, and Mutators contribute to score.
        /// MustHave/MustNotHave are gates (binary) and don't affect scoring.
        ///
        /// Formula:
        ///   α = 2 · n_priority (Priority gets 2x weight)
        ///   β = n_preferred   (Preferred gets 1x weight)
        ///   λ_prio = (1 - λ_mut) · α / (α + β)
        ///   λ_pref = (1 - λ_mut) · β / (α + β)
        ///
        /// Example (3 Priority, 5 Preferred, λ_mut=0.1):
        ///   α = 2·3 = 6, β = 5
        ///   λ_prio ≈ 0.49, λ_pref ≈ 0.41, λ_mut = 0.1
        ///
        /// SPECIAL CASE: When NO scoring filters are active (only MustHave/MustNotHave gates),
        /// returns special sentinel (-1, -1, -1) to indicate "gates only" mode.
        /// Callers should interpret this as: tiles that pass all gates get score=1.0.
        /// </summary>
        /// <param name="priorityCount">Number of Priority filters</param>
        /// <param name="preferredCount">Number of Preferred filters</param>
        /// <param name="mutatorWeight">Weight reserved for mutator score (default 0.1)</param>
        /// <returns>(λ_prio, λ_pref, λ_mut) where sum = 1.0, or (-1,-1,-1) for gates-only mode</returns>
        public static (float lambdaPrio, float lambdaPref, float lambdaMut) ComputeScoringWeights(
            int priorityCount,
            int preferredCount,
            float mutatorWeight = 0.1f)
        {
            mutatorWeight = Mathf.Clamp01(mutatorWeight);

            // SPECIAL CASE: No scoring filters (only MustHave/MustNotHave gates)
            // Return sentinel to indicate "gates only" mode - tiles passing all gates are perfect
            if (priorityCount == 0 && preferredCount == 0)
                return (-1f, -1f, -1f); // Sentinel for gates-only mode

            // Priority gets 2x weight per filter
            float alpha = 2f * priorityCount;
            float beta = preferredCount;
            float sum = alpha + beta;

            if (sum == 0)
                return (-1f, -1f, -1f); // Sentinel for gates-only mode

            // Normalize and reserve space for mutators
            float lambdaPrio = (1f - mutatorWeight) * alpha / sum;
            float lambdaPref = (1f - mutatorWeight) * beta / sum;

            return (lambdaPrio, lambdaPref, mutatorWeight);
        }

        /// <summary>
        /// Computes final score using the 5-state model.
        /// No penalty term - MustHave/MustNotHave are gates (handled in Apply phase).
        ///
        /// Formula:
        ///   S_final = λ_prio·S_prio + λ_pref·S_pref + λ_mut·S_mut
        ///
        /// Where:
        ///   S_prio = weighted average Priority membership
        ///   S_pref = weighted average Preferred membership
        ///   S_mut = mutator quality score
        /// </summary>
        /// <param name="priorityScore">Weighted Priority membership [0,1]</param>
        /// <param name="preferredScore">Weighted Preferred membership [0,1]</param>
        /// <param name="mutatorScore">Mutator quality score [0,1]</param>
        /// <param name="lambdaPrio">Priority group weight</param>
        /// <param name="lambdaPref">Preferred group weight</param>
        /// <param name="lambdaMut">Mutator group weight</param>
        /// <returns>Final score [0,1]</returns>
        public static float ComputeScoringScore(
            float priorityScore,
            float preferredScore,
            float mutatorScore,
            float lambdaPrio,
            float lambdaPref,
            float lambdaMut)
        {
            return lambdaPrio * priorityScore + lambdaPref * preferredScore + lambdaMut * mutatorScore;
        }
    }
}

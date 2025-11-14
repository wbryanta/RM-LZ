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

        // ============================================================
        // MEMBERSHIP-BASED SCORING (NEW SYSTEM)
        // ============================================================

        /// <summary>
        /// Computes weighted group score from membership values.
        /// S_group = Σ(w_i·μ_i) / Σ(w_i)
        ///
        /// Used for both critical and preferred aggregation.
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

        /// <summary>
        /// Finds worst (minimum) membership score among criticals.
        /// W_C = min{μ_i | i ∈ Critical}
        ///
        /// Used for penalty calculation - punish tiles with bad critical misses.
        /// </summary>
        /// <param name="criticalMemberships">Membership scores for critical filters</param>
        /// <returns>Worst critical membership [0,1], or 1.0 if no criticals</returns>
        public static float ComputeWorstCritical(float[] criticalMemberships)
        {
            if (criticalMemberships == null || criticalMemberships.Length == 0)
                return 1f; // No criticals = perfect

            float worst = 1f;
            foreach (float mu in criticalMemberships)
            {
                if (mu < worst)
                    worst = mu;
            }

            return worst;
        }

        /// <summary>
        /// Computes penalty term based on worst critical miss.
        /// P_C = α_pen + (1 - α_pen)·W_C^γ_pen
        ///
        /// - α_pen: minimum score fraction that survives (penalty floor)
        /// - γ_pen: penalty curve sharpness (higher = harsher punishment)
        /// - W_C: worst critical membership
        ///
        /// Examples (α_pen=0.1, γ_pen=2.0):
        ///   W_C = 1.0 → P_C = 1.0 (no penalty)
        ///   W_C = 0.7 → P_C ≈ 0.54
        ///   W_C = 0.3 → P_C ≈ 0.18
        ///   W_C = 0.0 → P_C = 0.1 (floor)
        /// </summary>
        public static float ComputePenalty(float worstCritical, float alphaPenalty = 0.1f, float gammaPenalty = 2.0f)
        {
            worstCritical = Mathf.Clamp01(worstCritical);
            float powered = Mathf.Pow(worstCritical, gammaPenalty);
            return alphaPenalty + (1f - alphaPenalty) * powered;
        }

        /// <summary>
        /// Computes global weights for critical, preferred, and mutator groups.
        ///
        /// Formula:
        ///   α = critBase · n_C
        ///   β = prefBase · n_P
        ///   λ_C = (1 - λ_mut) · α / (α + β)
        ///   λ_P = (1 - λ_mut) · β / (α + β)
        ///
        /// Default parameters:
        ///   critBase = 4 (one critical = 4× one preferred)
        ///   prefBase = 1
        ///   λ_mut = 0.1 (10% weight reserved for mutators)
        ///
        /// Example (4 critical, 12 preferred):
        ///   α = 4·4 = 16, β = 1·12 = 12
        ///   λ_C ≈ 0.514, λ_P ≈ 0.386, λ_mut = 0.1
        /// </summary>
        /// <param name="criticalCount">Number of critical filters</param>
        /// <param name="preferredCount">Number of preferred filters</param>
        /// <param name="critBase">Per-filter importance of criticals (default 4.0)</param>
        /// <param name="prefBase">Per-filter importance of preferreds (default 1.0)</param>
        /// <param name="mutatorWeight">Weight reserved for mutator score (default 0.1)</param>
        /// <returns>(λ_C, λ_P, λ_mut) where sum = 1.0</returns>
        public static (float lambdaC, float lambdaP, float lambdaMut) ComputeGlobalWeights(
            int criticalCount,
            int preferredCount,
            float critBase = 4.0f,
            float prefBase = 1.0f,
            float mutatorWeight = 0.1f)
        {
            mutatorWeight = Mathf.Clamp01(mutatorWeight);

            // Edge cases
            if (criticalCount == 0 && preferredCount == 0)
                return (0f, 0f, 1f); // Only mutators

            float alpha = critBase * criticalCount;
            float beta = prefBase * preferredCount;
            float sum = alpha + beta;

            if (sum == 0)
                return (0f, 0f, 1f); // Only mutators

            // Normalize and reserve space for mutators
            float lambdaC = (1f - mutatorWeight) * alpha / sum;
            float lambdaP = (1f - mutatorWeight) * beta / sum;

            return (lambdaC, lambdaP, mutatorWeight);
        }

        /// <summary>
        /// Computes final membership-based score using penalty term approach.
        ///
        /// Formula (from user-modifying-mathed-math.md):
        ///   S_base = λ_C·S_C + λ_P·S_P + λ_mut·S_mut
        ///   S_final = P_C · S_base
        ///
        /// Where:
        ///   S_C = weighted average critical membership
        ///   S_P = weighted average preferred membership
        ///   S_mut = mutator quality score
        ///   P_C = penalty based on worst critical
        /// </summary>
        /// <param name="criticalScore">Weighted critical membership [0,1]</param>
        /// <param name="preferredScore">Weighted preferred membership [0,1]</param>
        /// <param name="mutatorScore">Mutator quality score [0,1]</param>
        /// <param name="penalty">Penalty multiplier from worst critical [0,1]</param>
        /// <param name="lambdaC">Critical group weight</param>
        /// <param name="lambdaP">Preferred group weight</param>
        /// <param name="lambdaMut">Mutator group weight</param>
        /// <returns>Final score [0,1]</returns>
        public static float ComputeMembershipScore(
            float criticalScore,
            float preferredScore,
            float mutatorScore,
            float penalty,
            float lambdaC,
            float lambdaP,
            float lambdaMut)
        {
            float baseScore = lambdaC * criticalScore + lambdaP * preferredScore + lambdaMut * mutatorScore;
            return penalty * baseScore;
        }
    }
}

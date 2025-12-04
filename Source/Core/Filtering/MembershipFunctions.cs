using System;

namespace LandingZone.Core.Filtering
{
    /// <summary>
    /// Utility functions for computing continuous membership scores [0,1].
    /// Implements trapezoid falloff, distance-based decay, and other fuzzy logic patterns.
    /// </summary>
    public static class MembershipFunctions
    {
        /// <summary>
        /// Default fuzzy margin multiplier. Can be tuned via mod settings.
        /// Default: 0.3 (margins are 30% of user's range width).
        /// </summary>
        public static float DefaultMarginMultiplier = 0.3f;

        /// <summary>
        /// Trapezoid membership for numeric ranges with soft margins.
        ///
        /// Perfect (1.0) inside [min, max].
        /// Linear falloff outside range based on margin width.
        /// Zero beyond margin.
        ///
        /// Formula:
        ///   μ(x) = 1.0                      if min ≤ x ≤ max
        ///        = 1.0 - (min - x)/margin   if min - margin < x < min
        ///        = 1.0 - (x - max)/margin   if max < x < max + margin
        ///        = 0.0                      otherwise
        /// </summary>
        /// <param name="value">Tile's actual value</param>
        /// <param name="min">User's minimum acceptable value</param>
        /// <param name="max">User's maximum acceptable value</param>
        /// <param name="marginMultiplier">Margin size as fraction of range width (default 0.3)</param>
        public static float Trapezoid(float value, float min, float max, float marginMultiplier = -1f)
        {
            if (marginMultiplier < 0) marginMultiplier = DefaultMarginMultiplier;

            // Inside perfect range
            if (value >= min && value <= max)
                return 1.0f;

            float rangeWidth = max - min;
            float margin = rangeWidth * marginMultiplier;

            // Guard against division by zero when min == max (exact value match)
            // Use a small epsilon margin for graceful falloff
            const float MinMargin = 0.001f;
            if (margin < MinMargin) margin = MinMargin;

            // Below minimum
            if (value < min)
            {
                float distance = min - value;
                if (distance >= margin) return 0.0f;
                return 1.0f - (distance / margin);
            }

            // Above maximum
            if (value > max)
            {
                float distance = value - max;
                if (distance >= margin) return 0.0f;
                return 1.0f - (distance / margin);
            }

            return 0.0f; // Should never reach here
        }

        /// <summary>
        /// Distance-based membership with smooth decay.
        ///
        /// Perfect (1.0) at distance 0.
        /// Smooth falloff using 1/(1+d^p) curve.
        ///
        /// Formula:
        ///   μ(d) = 1 / (1 + d^p)
        ///
        /// where d is normalized distance and p controls falloff sharpness (default 2).
        /// </summary>
        /// <param name="distance">Normalized distance from ideal (0 = perfect)</param>
        /// <param name="power">Falloff sharpness (default 2 for quadratic decay)</param>
        public static float DistanceDecay(float distance, float power = 2f)
        {
            if (distance <= 0) return 1.0f;
            return 1.0f / (1.0f + (float)Math.Pow(distance, power));
        }

        /// <summary>
        /// Binary membership: either perfect match (1.0) or complete miss (0.0).
        /// Used for boolean filters like Coastal, HasCave, etc.
        /// </summary>
        public static float Binary(bool matches)
        {
            return matches ? 1.0f : 0.0f;
        }

        /// <summary>
        /// Ordinal category membership based on integer distance from allowed set.
        ///
        /// Perfect (1.0) if value is in allowed set.
        /// Linear decay based on steps away from nearest allowed value.
        /// Zero beyond maxDistance steps.
        ///
        /// Example: Hilliness with allowed={SmallHills, LargeHills}
        ///   Flat(0) → SmallHills(1) is 1 step → μ = 1 - 1/2 = 0.5
        ///   Mountainous(3) → LargeHills(2) is 1 step → μ = 1 - 1/2 = 0.5
        ///   Impassable(4) → LargeHills(2) is 2 steps → μ = 0.0
        /// </summary>
        /// <param name="actualCode">Tile's integer code</param>
        /// <param name="allowedCodes">Set of acceptable codes</param>
        /// <param name="maxDistance">Maximum steps before score drops to 0 (default 2)</param>
        public static float OrdinalDistance(int actualCode, int[] allowedCodes, int maxDistance = 2)
        {
            if (allowedCodes == null || allowedCodes.Length == 0)
                return 0.0f;

            // Find minimum distance to any allowed code
            int minDist = int.MaxValue;
            foreach (int allowed in allowedCodes)
            {
                int dist = Math.Abs(actualCode - allowed);
                if (dist < minDist) minDist = dist;
            }

            // Perfect match
            if (minDist == 0) return 1.0f;

            // Beyond tolerance
            if (minDist >= maxDistance) return 0.0f;

            // Linear decay
            return 1.0f - ((float)minDist / maxDistance);
        }

        /// <summary>
        /// Clamp membership score to [0,1] range (safety check).
        /// </summary>
        public static float Clamp01(float value)
        {
            if (value < 0.0f) return 0.0f;
            if (value > 1.0f) return 1.0f;
            return value;
        }
    }
}

namespace LandingZone.Data
{
    /// <summary>
    /// Tile rarity tiers based on cumulative probability of features/biome combination.
    /// Uses log10 scale of combined probabilities from canonical world data.
    /// </summary>
    public enum TileRarity : byte
    {
        /// <summary>
        /// Very common (>10% of tiles) - P > 0.1
        /// </summary>
        Common = 0,

        /// <summary>
        /// Somewhat common (1-10% of tiles) - 0.01 < P <= 0.1
        /// </summary>
        Uncommon = 1,

        /// <summary>
        /// Rare (0.1-1% of tiles) - 0.001 < P <= 0.01
        /// </summary>
        Rare = 2,

        /// <summary>
        /// Very rare (0.01-0.1% of tiles) - 0.0001 < P <= 0.001
        /// </summary>
        VeryRare = 3,

        /// <summary>
        /// Epic (0.001-0.01% of tiles) - 0.00001 < P <= 0.0001
        /// </summary>
        Epic = 4,

        /// <summary>
        /// Legendary (0.0001-0.001% of tiles) - 0.000001 < P <= 0.00001
        /// </summary>
        Legendary = 5,

        /// <summary>
        /// Mythic (< 0.0001% of tiles) - P <= 0.000001
        /// </summary>
        Mythic = 6
    }

    /// <summary>
    /// Helper methods for TileRarity enum
    /// </summary>
    public static class TileRarityExtensions
    {
        /// <summary>
        /// Converts rarity tier to user-friendly display label (full text)
        /// </summary>
        public static string ToLabel(this TileRarity rarity)
        {
            return rarity switch
            {
                TileRarity.Common => "Common",
                TileRarity.Uncommon => "Uncommon",
                TileRarity.Rare => "Rare",
                TileRarity.VeryRare => "Very Rare",
                TileRarity.Epic => "Epic",
                TileRarity.Legendary => "Legendary",
                TileRarity.Mythic => "Mythic",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Converts rarity tier to compact badge label (max 5 chars, no wrapping)
        /// </summary>
        public static string ToBadgeLabel(this TileRarity rarity)
        {
            return rarity switch
            {
                TileRarity.Common => "Comm",         // 4 chars - fits single line
                TileRarity.Uncommon => "Uncm",       // 4 chars - fits single line
                TileRarity.Rare => "Rare",           // 4 chars - perfect
                TileRarity.VeryRare => "V.Rar",      // 5 chars - max allowed
                TileRarity.Epic => "Epic",           // 4 chars - perfect
                TileRarity.Legendary => "Legnd",     // 5 chars - max allowed
                TileRarity.Mythic => "Myth",         // 4 chars - clear
                _ => "???"                           // 3 chars - error state
            };
        }

        /// <summary>
        /// Gets color for rarity tier (for UI badges)
        /// </summary>
        public static UnityEngine.Color ToColor(this TileRarity rarity)
        {
            return rarity switch
            {
                TileRarity.Common => new UnityEngine.Color(0.7f, 0.7f, 0.7f),      // Gray
                TileRarity.Uncommon => new UnityEngine.Color(0.3f, 0.9f, 0.3f),    // Green
                TileRarity.Rare => new UnityEngine.Color(0.3f, 0.6f, 1.0f),        // Blue
                TileRarity.VeryRare => new UnityEngine.Color(0.7f, 0.3f, 1.0f),    // Purple
                TileRarity.Epic => new UnityEngine.Color(1.0f, 0.4f, 0.8f),        // Pink
                TileRarity.Legendary => new UnityEngine.Color(1.0f, 0.6f, 0.0f),   // Orange
                TileRarity.Mythic => new UnityEngine.Color(1.0f, 0.85f, 0.0f),     // Gold
                _ => UnityEngine.Color.white
            };
        }

        /// <summary>
        /// Computes rarity tier from probability (0-1 range)
        /// </summary>
        public static TileRarity FromProbability(float probability)
        {
            if (probability > 0.1f) return TileRarity.Common;
            if (probability > 0.01f) return TileRarity.Uncommon;
            if (probability > 0.001f) return TileRarity.Rare;
            if (probability > 0.0001f) return TileRarity.VeryRare;
            if (probability > 0.00001f) return TileRarity.Epic;
            if (probability > 0.000001f) return TileRarity.Legendary;
            return TileRarity.Mythic;
        }
    }
}

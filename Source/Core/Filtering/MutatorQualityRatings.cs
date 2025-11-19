using System;
using System.Collections.Generic;

namespace LandingZone.Core.Filtering
{
    /// <summary>
    /// Quality ratings for all 83 map mutators from -10 (very bad) to +10 (very good).
    ///
    /// Ratings based on gameplay impact:
    /// - Positive: Resources, power, defense, mood bonuses
    /// - Negative: Dangers, hazards, penalties, ugly features
    /// - Neutral: Geographic features, minor modifiers
    ///
    /// Source: Canonical mutator list from game cache analysis (83 total).
    /// </summary>
    public static class MutatorQualityRatings
    {
        private static readonly Dictionary<string, int> _ratings = new Dictionary<string, int>
        {
            // ========================================
            // VERY GOOD (+10): Major gameplay benefits
            // ========================================
            { "AncientHeatVent", 10 },          // Geothermal power
            { "HotSprings", 10 },               // Geothermal + mood bonus
            { "Fertile", 10 },                  // Major farming bonus
            { "MineralRich", 10 },              // Major mining bonus
            { "SteamGeysers_Increased", 10 },   // More geothermal power

            // ========================================
            // GOOD (+5 to +8): Significant benefits
            // ========================================
            { "PlantLife_Increased", 8 },       // More harvestable plants
            { "Fish_Increased", 8 },            // More fishing food
            { "AnimalLife_Increased", 8 },      // More hunting/taming
            { "WetClimate", 7 },                // Farming bonus
            { "Wetland", 7 },                   // Farming bonus
            { "Muddy", 6 },                     // Farming bonus, defensible
            { "Marshy", 6 },                    // Defensible terrain
            { "Oasis", 7 },                     // Good in deserts
            { "WildPlants", 6 },                // Forageable resources
            { "WildTropicalPlants", 6 },        // Forageable resources
            { "PlantGrove", 5 },                // Wood resources
            { "AnimalHabitat", 5 },             // More animals nearby
            { "River", 5 },                     // Water, trade, fishing
            { "Caves", 5 },                     // Shelter, defense, mining
            { "Cavern", 5 },                    // Shelter
            { "Lake", 4 },                      // Water, fishing
            { "LakeWithIsland", 4 },            // Unique defensible feature
            { "LakeWithIslands", 4 },           // Unique feature
            { "CaveLakes", 4 },                 // Water source underground

            // ========================================
            // SLIGHTLY POSITIVE (+1 to +3)
            // ========================================
            { "ObsidianDeposits", 3 },          // Crafting material
            { "ArcheanTrees", 3 },              // Unique wood source
            { "SunnyMutator", 2 },              // Mood bonus
            { "Pond", 2 },                      // Small water source
            { "RiverDelta", 2 },                // Multiple rivers

            // ========================================
            // NEUTRAL (0): Geographic features, minor
            // ========================================
            { "Basin", 0 },
            { "Bay", 0 },
            { "Chasm", 0 },
            { "Cliffs", 0 },
            { "Coast", 0 },
            { "CoastalAtoll", 0 },
            { "CoastalIsland", 0 },
            { "Cove", 0 },
            { "Crevasse", 0 },
            { "Dunes", 0 },
            { "Fjord", 0 },
            { "Harbor", 0 },
            { "Headwater", 0 },
            { "Hollow", 0 },
            { "Iceberg", 0 },
            { "Mountain", 0 },
            { "Peninsula", 0 },
            { "Plateau", 0 },
            { "Sandy", 0 },
            { "Valley", 0 },
            { "Lakeshore", 0 },
            { "RiverConfluence", 0 },
            { "RiverIsland", 0 },
            { "Archipelago", 0 },
            { "MixedBiome", 0 },
            { "FoggyMutator", 0 },
            { "WindyMutator", 0 },
            { "AncientQuarry", 0 },             // Resources but also danger
            { "AncientWarehouse", 0 },          // Loot but also danger
            { "AncientUplink", 0 },             // Minor loot
            { "Stockpile", 0 },                 // Minor loot
            { "AncientRuins", 0 },              // Loot but also danger
            { "AncientRuins_Frozen", 0 },       // Loot but also danger

            // ========================================
            // BAD (-5 to -8): Penalties and hazards
            // ========================================
            { "DryGround", -6 },                // Farming penalty
            { "DryLake", -5 },                  // No water
            { "PlantLife_Decreased", -7 },      // Resource penalty
            { "Fish_Decreased", -6 },           // Food penalty
            { "AnimalLife_Decreased", -6 },     // Food penalty
            { "Pollution_Increased", -8 },      // Health hazard, ugly
            { "AbandonedColonyOutlander", -5 }, // Raiders
            { "AbandonedColonyTribal", -5 },    // Raiders
            { "Junkyard", -5 },                 // Ugly, minimal loot
            { "TerraformingScar", -6 },         // Ugly terrain

            // ========================================
            // VERY BAD (-10): Major dangers/hazards
            // ========================================
            { "AncientInfestedSettlement", -10 }, // Insect hive infestation
            { "InsectMegahive", -10 },            // Massive insect threat
            { "ToxicLake", -10 },                 // Toxic hazard
            { "AncientToxVent", -10 },            // Toxic gas hazard
            { "AncientSmokeVent", -8 },           // Pollution hazard
            { "LavaCaves", -9 },                  // Lava danger
            { "LavaCrater", -10 },                // Major lava hazard
            { "LavaFlow", -9 },                   // Lava danger
            { "AncientChemfuelRefinery", -8 },    // Explosion hazard
            { "AncientGarrison", -8 },            // Mechanoid danger
            { "AncientLaunchSite", -8 },          // Mechanoid danger
        };

        /// <summary>
        /// Gets quality rating for a mutator.
        /// Returns 0 if mutator not found (treat as neutral).
        /// </summary>
        /// <param name="mutatorName">Mutator defName</param>
        /// <param name="activePreset">Optional preset - if provided, checks for preset-specific overrides first</param>
        /// <returns>Quality rating from -10 (very bad) to +10 (very good)</returns>
        public static int GetQuality(string mutatorName, LandingZone.Data.Preset? activePreset = null)
        {
            // Check preset-specific overrides first
            if (activePreset?.MutatorQualityOverrides != null
                && activePreset.MutatorQualityOverrides.TryGetValue(mutatorName, out int overrideQuality))
            {
                return overrideQuality;
            }

            // Fall back to global rating
            return _ratings.TryGetValue(mutatorName, out int quality) ? quality : 0;
        }

        /// <summary>
        /// Computes mutator quality score using tanh squashing function.
        ///
        /// Formula (from user-modifying-mathed-math.md):
        ///   Q_raw = Σ(quality_k) for all mutators on tile
        ///   S_mut = 0.5 × (1 + tanh(β × Q_raw))
        ///
        /// Where β ≈ 0.25 controls sensitivity.
        ///
        /// Result is in [0,1]:
        /// - 0.5 = neutral (no mutators or balanced good/bad)
        /// - 1.0 = excellent (multiple very good mutators)
        /// - 0.0 = terrible (multiple very bad mutators)
        ///
        /// Examples:
        ///   No mutators → Q_raw=0 → S_mut=0.5 (neutral)
        ///   Geothermal(+10) → Q_raw=10 → S_mut≈0.92 (excellent)
        ///   ToxicLake(-10) → Q_raw=-10 → S_mut≈0.08 (terrible)
        ///   Geothermal(+10) + ToxicLake(-10) → Q_raw=0 → S_mut=0.5 (neutral)
        /// </summary>
        /// <param name="mutators">List of mutator defNames on tile</param>
        /// <param name="beta">Sensitivity parameter (default 0.25)</param>
        /// <param name="activePreset">Optional preset for quality overrides</param>
        /// <returns>Mutator quality score [0,1]</returns>
        public static float ComputeMutatorScore(IEnumerable<string> mutators, float beta = 0.25f, LandingZone.Data.Preset? activePreset = null)
        {
            if (mutators == null)
                return 0.5f; // Neutral baseline

            // Sum quality ratings (using preset-specific overrides if provided)
            float qRaw = 0f;
            foreach (var mutator in mutators)
            {
                qRaw += GetQuality(mutator, activePreset);
            }

            // Squash via tanh: S_mut = 0.5 × (1 + tanh(β × Q_raw))
            // Using identity: tanh(x) = (e^2x - 1) / (e^2x + 1)
            double e2x = Math.Exp(2.0 * beta * qRaw);
            double tanhValue = (e2x - 1.0) / (e2x + 1.0);
            float sMut = 0.5f * (1f + (float)tanhValue);

            return Math.Max(0f, Math.Min(1f, sMut)); // Clamp to [0,1]
        }
    }
}

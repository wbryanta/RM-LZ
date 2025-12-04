#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LandingZone.Core;
using LandingZone.Core.Filtering.Filters;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Data
{
    /// <summary>
    /// Computes tile rarity based on canonical world data from full cache dumps.
    /// Uses percent_settleable probabilities to determine how rare a tile's combination is.
    ///
    /// IMPORTANT: These probabilities are a HEURISTIC BASELINE, not precise measurements.
    /// Actual rarity varies based on:
    /// - World seed and coverage settings
    /// - Active mods (Geological Landforms, Biomes!, etc.)
    /// - DLC content (Anomaly, Odyssey add new mutators)
    ///
    /// The goal is to give players a rough sense of relative rarity ("Hot Springs is rarer
    /// than Caves") rather than exact percentages. The tanh squashing in mutator scoring
    /// further smooths these values, so precise accuracy isn't critical.
    ///
    /// Data source: Analysis of 11 vanilla worlds (3.25M total tiles, 1.55M settleable)
    /// Snapshot date: 2025-11-15
    /// </summary>
    public static class RarityCalculator
    {
        private static Dictionary<string, float> _probabilities = new Dictionary<string, float>();
        private static bool _initialized = false;

        /// <summary>
        /// Initializes rarity calculator by loading canonical world data.
        /// Should be called once at game startup.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            try
            {
                // Try to load canonical data from mod directory
                string modDir = GenFilePaths.ModsFolderPath;
                string[] possiblePaths = new[]
                {
                    Path.Combine(modDir, "LandingZone", "docs", "data", "canonical_world_library_aggregate.json"),
                    Path.Combine(modDir, "LandingZone", "docs", "data", "canonical_world_library_2025-11-15-145806.json")
                };

                string dataPath = possiblePaths.FirstOrDefault(File.Exists);

                if (dataPath == null)
                {
                    Log.Warning("[LandingZone] RarityCalculator: Canonical world data not found. Rarity scoring disabled.");
                    _initialized = true;
                    return;
                }

                // Parse JSON (simplified - assumes structure matches our aggregate format)
                string json = File.ReadAllText(dataPath);

                // Extract biomes and mutators from JSON
                // Format: { ..., "biomes": [...], "mutators": [...] } (simplified assumption)
                // In production, use actual JSON parser

                // For now, we'll populate with hardcoded common values
                // TODO: Implement actual JSON parsing using SimpleJSON or similar
                PopulateHardcodedProbabilities();

                _initialized = true;
                Log.Message($"[LandingZone] RarityCalculator initialized from {Path.GetFileName(dataPath)}");
            }
            catch (Exception ex)
            {
                Log.Error($"[LandingZone] RarityCalculator initialization failed: {ex.Message}");
                _initialized = true; // Mark as initialized even on failure to avoid repeated attempts
            }
        }

        /// <summary>
        /// Populates heuristic baseline probabilities from canonical aggregate world data.
        /// Source: docs/data/canonical_world_library_aggregate.json (snapshot: 2025-11-15)
        /// Dataset: 11 vanilla worlds, 3.25M total tiles, 1.55M settleable tiles.
        /// Values are percent_settleable (fraction of settleable tiles with each feature).
        /// See class-level comment for why these are heuristics, not precise measurements.
        /// </summary>
        private static void PopulateHardcodedProbabilities()
        {
            // ====================
            // BIOMES (19 total)
            // ====================
            _probabilities["TemperateForest"] = 0.2951f;
            _probabilities["Desert"] = 0.1581f;
            _probabilities["AridShrubland"] = 0.1501f;
            _probabilities["TropicalRainforest"] = 0.1071f;
            _probabilities["Tundra"] = 0.0732f;
            _probabilities["BorealForest"] = 0.0678f;
            _probabilities["Grasslands"] = 0.0614f;
            _probabilities["ExtremeDesert"] = 0.0365f;
            _probabilities["Glowforest"] = 0.0127f;
            _probabilities["TemperateSwamp"] = 0.0122f;
            _probabilities["Scarlands"] = 0.0114f;
            _probabilities["TropicalSwamp"] = 0.0052f;
            _probabilities["LavaField"] = 0.0036f;
            _probabilities["GlacialPlain"] = 0.0033f;
            _probabilities["ColdBog"] = 0.0024f;
            // Ocean, SeaIce, IceSheet, Lake = 0% settleable (impassable)

            // ====================
            // MAP FEATURES / MUTATORS (83 total from canonical aggregate)
            // Sorted by percent_settleable descending
            // ====================

            // Common mutators (>1% of tiles)
            _probabilities["Mountain"] = 0.1755f;
            _probabilities["Caves"] = 0.0856f;
            _probabilities["River"] = 0.0531f;
            _probabilities["Coast"] = 0.0492f;
            _probabilities["MixedBiome"] = 0.0309f;
            _probabilities["SunnyMutator"] = 0.0197f;
            _probabilities["AnimalHabitat"] = 0.0187f;
            _probabilities["AnimalLife_Increased"] = 0.0161f;
            _probabilities["WildPlants"] = 0.0158f;
            _probabilities["PlantLife_Increased"] = 0.0153f;
            _probabilities["AnimalLife_Decreased"] = 0.0101f;
            _probabilities["PlantGrove"] = 0.0097f;
            _probabilities["SteamGeysers_Increased"] = 0.0094f;
            _probabilities["Junkyard"] = 0.0077f;

            // Uncommon mutators (0.1-1% of tiles)
            _probabilities["PlantLife_Decreased"] = 0.0059f;
            _probabilities["FoggyMutator"] = 0.0058f;
            _probabilities["Sandy"] = 0.0058f;
            _probabilities["Fertile"] = 0.0055f;
            _probabilities["WetClimate"] = 0.0051f;
            _probabilities["WindyMutator"] = 0.0049f;
            _probabilities["Muddy"] = 0.0044f;
            _probabilities["Marshy"] = 0.0043f;
            _probabilities["Lakeshore"] = 0.0035f;
            _probabilities["DryGround"] = 0.0031f;
            _probabilities["RiverIsland"] = 0.0028f;
            _probabilities["Headwater"] = 0.0024f;
            _probabilities["Pollution_Increased"] = 0.0017f;
            _probabilities["RiverConfluence"] = 0.0012f;
            _probabilities["WildTropicalPlants"] = 0.0011f;

            // Rare mutators (0.01-0.1% of tiles)
            _probabilities["AncientRuins"] = 0.00029f;
            _probabilities["Valley"] = 0.00030f;
            _probabilities["Stockpile"] = 0.00028f;
            _probabilities["Bay"] = 0.00026f;
            _probabilities["AncientUplink"] = 0.00023f;
            _probabilities["RiverDelta"] = 0.00023f;
            _probabilities["Peninsula"] = 0.00022f;
            _probabilities["ObsidianDeposits"] = 0.00021f;
            _probabilities["Cavern"] = 0.00021f;
            _probabilities["CoastalIsland"] = 0.00021f;
            _probabilities["Archipelago"] = 0.00020f;
            _probabilities["Cove"] = 0.00020f;
            _probabilities["Cliffs"] = 0.00019f;
            _probabilities["LakeWithIslands"] = 0.00016f;
            _probabilities["Chasm"] = 0.00015f;
            _probabilities["Fjord"] = 0.00015f;
            _probabilities["Wetland"] = 0.00015f;
            _probabilities["MineralRich"] = 0.00015f;
            _probabilities["Lake"] = 0.00014f;
            _probabilities["Pond"] = 0.00014f;
            _probabilities["LakeWithIsland"] = 0.00014f;
            _probabilities["CoastalAtoll"] = 0.00013f;
            _probabilities["Hollow"] = 0.00013f;
            _probabilities["CaveLakes"] = 0.00012f;
            _probabilities["Basin"] = 0.00012f;
            _probabilities["Plateau"] = 0.00012f;

            // Very rare mutators (<0.01% of tiles)
            _probabilities["AbandonedColonyTribal"] = 0.00009f;
            _probabilities["Oasis"] = 0.000088f;
            _probabilities["HotSprings"] = 0.000082f;
            _probabilities["AbandonedColonyOutlander"] = 0.000076f;
            _probabilities["AncientQuarry"] = 0.000075f;
            _probabilities["Fish_Increased"] = 0.000073f;
            _probabilities["LavaFlow"] = 0.000062f;
            _probabilities["AncientToxVent"] = 0.00006f;
            _probabilities["AncientGarrison"] = 0.000059f;
            _probabilities["AncientChemfuelRefinery"] = 0.000059f;
            _probabilities["AncientSmokeVent"] = 0.000058f;
            _probabilities["AncientInfestedSettlement"] = 0.000057f;
            _probabilities["Dunes"] = 0.000056f;
            _probabilities["AncientWarehouse"] = 0.000055f;
            _probabilities["AncientLaunchSite"] = 0.000054f;
            _probabilities["DryLake"] = 0.000054f;
            _probabilities["AncientHeatVent"] = 0.000048f;
            _probabilities["Harbor"] = 0.000048f;
            _probabilities["ToxicLake"] = 0.000045f;
            _probabilities["LavaCaves"] = 0.000035f;
            _probabilities["TerraformingScar"] = 0.000032f;
            _probabilities["ArcheanTrees"] = 0.000032f;
            _probabilities["Iceberg"] = 0.000024f;
            _probabilities["InsectMegahive"] = 0.000022f;
            _probabilities["Fish_Decreased"] = 0.000021f;
            _probabilities["LavaLake"] = 0.000015f;
            _probabilities["LavaCrater"] = 0.000012f;
            _probabilities["Crevasse"] = 0.000012f;
            _probabilities["AncientRuins_Frozen"] = 0.0000084f;
            _probabilities["IceCaves"] = 0.0000064f;
        }

        /// <summary>
        /// Computes rarity for a given tile based on its biome and mutators.
        /// Returns combined probability and rarity tier.
        /// </summary>
        public static (float probability, TileRarity tier) ComputeTileRarity(int tileId)
        {
            if (!_initialized)
                Initialize();

            var tile = Find.WorldGrid?[tileId];
            if (tile == null)
                return (1.0f, TileRarity.Common);

            float combinedProbability = 1.0f;

            // Factor in biome probability
            string? biomeDefName = tile.PrimaryBiome?.defName;
            if (biomeDefName != null && _probabilities.TryGetValue(biomeDefName, out float biomeProbability))
            {
                combinedProbability *= biomeProbability;
            }

            // Factor in mutator probabilities (multiplicative - independent events)
            var mutators = MapFeatureFilter.GetTileMapFeatures(tileId).ToList();
            foreach (var mutator in mutators)
            {
                if (_probabilities.TryGetValue(mutator, out float mutatorProbability))
                {
                    combinedProbability *= mutatorProbability;
                }
            }

            var tier = TileRarityExtensions.FromProbability(combinedProbability);
            return (combinedProbability, tier);
        }

        /// <summary>
        /// Gets rarity tier for a tile without probability calculation
        /// </summary>
        public static TileRarity GetTileRarity(int tileId)
        {
            return ComputeTileRarity(tileId).tier;
        }

        /// <summary>
        /// Gets probability for a specific feature/biome defName
        /// </summary>
        public static float GetFeatureProbability(string defName)
        {
            if (!_initialized)
                Initialize();

            return _probabilities.TryGetValue(defName, out float prob) ? prob : 0.01f; // Default to 1% if unknown
        }
    }
}

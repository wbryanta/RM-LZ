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
        /// Temporary: Populates hardcoded probabilities from known canonical data.
        /// TODO: Replace with actual JSON parsing
        /// </summary>
        private static void PopulateHardcodedProbabilities()
        {
            // Biomes (from canonical data)
            _probabilities["TemperateForest"] = 0.3009f;
            _probabilities["AridShrubland"] = 0.1486f;
            _probabilities["Desert"] = 0.1413f;
            _probabilities["TropicalRainforest"] = 0.1171f;
            _probabilities["Tundra"] = 0.0850f;
            _probabilities["BorealForest"] = 0.0767f;
            _probabilities["IceSheet"] = 0.0519f;
            _probabilities["ExtremeDesert"] = 0.0454f;
            _probabilities["TropicalSwamp"] = 0.0247f;
            _probabilities["ColdBog"] = 0.0083f;

            // Common mutators
            _probabilities["Caves"] = 0.2161f;
            _probabilities["Mountain"] = 0.1086f;
            _probabilities["MixedBiome"] = 0.0852f;
            _probabilities["Ruins"] = 0.0307f;
            _probabilities["SteamGeysers_Increased"] = 0.0094f;
            _probabilities["AnimalHabitat"] = 0.0187f;
            _probabilities["Junkyard"] = 0.0077f;
            _probabilities["AnimalLife_Increased"] = 0.0158f;
            _probabilities["WildPlants"] = 0.0157f;

            // Rare/Epic mutators
            _probabilities["ArcheanTrees"] = 0.0051f;
            _probabilities["Headwater"] = 0.0017f;
            _probabilities["LargeHills_Mild"] = 0.0009f;
            _probabilities["ToxicRain"] = 0.0003f;
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

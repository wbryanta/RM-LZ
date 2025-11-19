using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Data
{
    /// <summary>
    /// Lightweight cache for resolved MineralRich and Stockpile contents.
    /// Built lazily on first access, cached per world seed.
    /// </summary>
    public sealed class MineralStockpileCache
    {
        private Dictionary<int, TileDetail>? _cache;
        private string _worldSeed = string.Empty;
        private bool _isBuilding = false;

        /// <summary>
        /// Details about a tile's minerals, stockpiles, animals, and plants.
        /// </summary>
        public readonly struct TileDetail
        {
            public TileDetail(List<string> minerals, List<string> stockpiles, List<string> animals, List<string> plants)
            {
                Minerals = minerals;
                Stockpiles = stockpiles;
                Animals = animals;
                Plants = plants;
            }

            /// <summary>
            /// Ore/mineable defNames from BiomeDef.GetMineableThingDefForTile (e.g., "MineableGold", "MineableUranium", "MineableJade")
            /// </summary>
            public List<string> Minerals { get; }

            /// <summary>
            /// Stockpile contents from TileMutatorWorker_Stockpile.GetStockpileType (e.g., "Weapons", "Medicine", "Gravcore")
            /// </summary>
            public List<string> Stockpiles { get; }

            /// <summary>
            /// Flagship animal species from TileMutatorWorker_AnimalHabitat.GetAnimalKind (e.g., "Thrumbo", "Megasloth", "Elephant")
            /// </summary>
            public List<string> Animals { get; }

            /// <summary>
            /// Flagship plant species from TileMutatorWorker_PlantGrove/WildPlants.GetPlantKind (e.g., "Plant_Ambrosia", "Plant_Healroot")
            /// </summary>
            public List<string> Plants { get; }
        }

        /// <summary>
        /// Gets mineral types for a tile, or empty list if not cached.
        /// </summary>
        public List<string> GetMineralTypes(int tileId)
        {
            EnsureInitialized();
            return _cache != null && _cache.TryGetValue(tileId, out var detail)
                ? detail.Minerals
                : new List<string>();
        }

        /// <summary>
        /// Checks if a tile has a specific mineral type.
        /// </summary>
        public bool HasMineral(int tileId, string defName)
        {
            EnsureInitialized();
            return _cache != null
                && _cache.TryGetValue(tileId, out var detail)
                && detail.Minerals.Contains(defName);
        }

        /// <summary>
        /// Gets stockpile types for a tile, or empty list if not cached.
        /// </summary>
        public List<string> GetStockpileTypes(int tileId)
        {
            EnsureInitialized();
            return _cache != null && _cache.TryGetValue(tileId, out var detail)
                ? detail.Stockpiles
                : new List<string>();
        }

        /// <summary>
        /// Gets flagship animal species for a tile, or empty list if not cached.
        /// </summary>
        public List<string> GetAnimalSpecies(int tileId)
        {
            EnsureInitialized();
            return _cache != null && _cache.TryGetValue(tileId, out var detail)
                ? detail.Animals
                : new List<string>();
        }

        /// <summary>
        /// Checks if a tile has a specific animal species.
        /// </summary>
        public bool HasAnimal(int tileId, string defName)
        {
            EnsureInitialized();
            return _cache != null
                && _cache.TryGetValue(tileId, out var detail)
                && detail.Animals.Contains(defName);
        }

        /// <summary>
        /// Gets flagship plant species for a tile, or empty list if not cached.
        /// </summary>
        public List<string> GetPlantSpecies(int tileId)
        {
            EnsureInitialized();
            return _cache != null && _cache.TryGetValue(tileId, out var detail)
                ? detail.Plants
                : new List<string>();
        }

        /// <summary>
        /// Checks if a tile has a specific plant species.
        /// </summary>
        public bool HasPlant(int tileId, string defName)
        {
            EnsureInitialized();
            return _cache != null
                && _cache.TryGetValue(tileId, out var detail)
                && detail.Plants.Contains(defName);
        }

        /// <summary>
        /// Clears the cache.
        /// </summary>
        public void Clear()
        {
            _cache?.Clear();
            _cache = null;
            _worldSeed = string.Empty;
        }

        /// <summary>
        /// Gets cache statistics for debugging.
        /// </summary>
        public (int mineralTiles, int stockpileTiles, int animalTiles, int plantTiles) GetCacheStats()
        {
            if (_cache == null) return (0, 0, 0, 0);

            int mineralTiles = _cache.Count(kvp => kvp.Value.Minerals.Count > 0);
            int stockpileTiles = _cache.Count(kvp => kvp.Value.Stockpiles.Count > 0);
            int animalTiles = _cache.Count(kvp => kvp.Value.Animals.Count > 0);
            int plantTiles = _cache.Count(kvp => kvp.Value.Plants.Count > 0);
            return (mineralTiles, stockpileTiles, animalTiles, plantTiles);
        }

        /// <summary>
        /// Ensures the cache is built. Safe to call multiple times (idempotent).
        /// Call this once before search to avoid lazy builds during tile evaluation.
        /// </summary>
        public void EnsureBuilt()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            var world = Find.World;
            if (world == null)
            {
                Verse.Log.Warning("[LandingZone] MineralStockpileCache.EnsureInitialized: World not available");
                return;
            }

            var currentSeed = world.info.seedString;

            // Reset if world changed
            if (_worldSeed != currentSeed)
            {
                if (LandingZoneSettings.LogLevel >= LoggingLevel.Standard)
                    Verse.Log.Message($"[LandingZone] MineralStockpileCache: World seed changed from '{_worldSeed}' to '{currentSeed}', clearing cache");
                Clear();
                _worldSeed = currentSeed;
            }

            // Build cache if needed
            if (_cache == null && !_isBuilding)
            {
                BuildCache();
            }
        }

        private void BuildCache()
        {
            _isBuilding = true;

            try
            {
                var world = Find.World;
                if (world?.grid == null)
                {
                    _cache = new Dictionary<int, TileDetail>();
                    return;
                }

                var sw = Stopwatch.StartNew();
                _cache = new Dictionary<int, TileDetail>();

                int tileCount = world.grid.TilesCount;
                int mineralCount = 0;
                int stockpileCount = 0;
                int animalCount = 0;
                int plantCount = 0;

                // Whitelist of known valid ore defNames (based on canonical world data)
                var validOres = new HashSet<string>
                {
                    "MineableSilver",
                    "MineableSteel",        // Core construction resource (compacted steel)
                    "MineableUranium",
                    "MineablePlasteel",
                    "MineableGold",
                    "MineableComponentsIndustrial",
                    "MineableJade",
                    // "MineableObsidian", // Uncomment if validated in-game
                    // "MineableVacstone", // Add if discovered in testing
                };
                var unknownOres = new HashSet<string>(); // Track unknown ores for warning

                if (LandingZoneSettings.LogLevel >= LoggingLevel.Standard)
                {
                    Log.Message($"[LandingZone] MineralStockpileCache: Building cache for {tileCount} tiles...");
                }

                for (int tileId = 0; tileId < tileCount; tileId++)
                {
                    var tile = world.grid[tileId];
                    if (tile == null || tile.WaterCovered) continue;

                    var mutators = tile.Mutators;
                    if (mutators == null || mutators.Count == 0) continue;

                    var minerals = new List<string>();
                    var stockpiles = new List<string>();
                    var animals = new List<string>();
                    var plants = new List<string>();

                    // Resolve MineralRich to specific ore types (valuable minerals)
                    var mineralRichMutator = mutators.FirstOrDefault(m => m.defName == "MineralRich");
                    if (mineralRichMutator != null && mineralRichMutator.Worker != null)
                    {
                        var planetTile = new PlanetTile(tileId, world.grid.Surface);

                        // Use reflection to call GetMineableThingDefForTile (not publicly accessible)
                        var method = AccessTools.Method(mineralRichMutator.Worker.GetType(), "GetMineableThingDefForTile");
                        if (method != null)
                        {
                            var oreDef = method.Invoke(mineralRichMutator.Worker, new object[] { planetTile }) as ThingDef;
                            if (oreDef != null)
                            {
                                // Validate against whitelist
                                if (validOres.Contains(oreDef.defName))
                                {
                                    minerals.Add(oreDef.defName); // e.g., MineableGold, MineableUranium, etc.
                                    mineralCount++;
                                }
                                else
                                {
                                    // Track unknown ore for warning
                                    unknownOres.Add(oreDef.defName);
                                }
                            }
                        }
                    }

                    // Resolve Stockpile to specific loot types (Chemfuel, Component, Drugs, Gravcore, Medicine, Weapons)
                    var stockpileMutator = mutators.FirstOrDefault(m => m.defName == "Stockpile");
                    if (stockpileMutator != null && stockpileMutator.Worker != null)
                    {
                        var planetTile = new PlanetTile(tileId, world.grid.Surface);

                        // Use reflection to call private static GetStockpileType(PlanetTile)
                        // Returns TileMutatorWorker_Stockpile.StockpileType enum
                        var method = AccessTools.Method(stockpileMutator.Worker.GetType(), "GetStockpileType");
                        if (method != null)
                        {
                            try
                            {
                                var stockpileTypeEnum = method.Invoke(null, new object[] { planetTile }); // Static method
                                if (stockpileTypeEnum != null)
                                {
                                    string stockpileTypeName = stockpileTypeEnum.ToString(); // Enum to string: "Chemfuel", "Component", etc.
                                    stockpiles.Add(stockpileTypeName);
                                    stockpileCount++;
                                }
                            }
                            catch (System.Exception ex)
                            {
                                if (LandingZoneSettings.LogLevel >= LoggingLevel.Verbose)
                                    Log.Warning($"[LandingZone] MineralStockpileCache: Failed to resolve stockpile at tile {tileId}: {ex.Message}");
                            }
                        }
                    }

                    // Resolve AnimalHabitat to flagship species (Thrumbo, Megasloth, Elephant, etc.)
                    var animalHabitatMutator = mutators.FirstOrDefault(m => m.defName == "AnimalHabitat");
                    if (animalHabitatMutator != null && animalHabitatMutator.Worker != null)
                    {
                        var planetTile = new PlanetTile(tileId, world.grid.Surface);

                        // GetAnimalKind is public instance method
                        var method = AccessTools.Method(animalHabitatMutator.Worker.GetType(), "GetAnimalKind");
                        if (method != null)
                        {
                            try
                            {
                                var animalDef = method.Invoke(animalHabitatMutator.Worker, new object[] { planetTile }) as Verse.PawnKindDef;
                                if (animalDef != null)
                                {
                                    animals.Add(animalDef.defName); // e.g., "Thrumbo", "Megasloth", "Elephant"
                                    animalCount++;
                                }
                            }
                            catch (System.Exception ex)
                            {
                                if (LandingZoneSettings.LogLevel >= LoggingLevel.Verbose)
                                    Log.Warning($"[LandingZone] MineralStockpileCache: Failed to resolve animal at tile {tileId}: {ex.Message}");
                            }
                        }
                    }

                    // Resolve PlantGrove to flagship plant species (Ambrosia, Healroot, Devilstrand, etc.)
                    var plantGroveMutator = mutators.FirstOrDefault(m => m.defName == "PlantGrove");
                    if (plantGroveMutator != null && plantGroveMutator.Worker != null)
                    {
                        var planetTile = new PlanetTile(tileId, world.grid.Surface);

                        // GetPlantKind is private instance method
                        var method = AccessTools.Method(plantGroveMutator.Worker.GetType(), "GetPlantKind");
                        if (method != null)
                        {
                            try
                            {
                                var plantDef = method.Invoke(plantGroveMutator.Worker, new object[] { planetTile }) as Verse.ThingDef;
                                if (plantDef != null)
                                {
                                    plants.Add(plantDef.defName); // e.g., "Plant_Ambrosia", "Plant_Healroot"
                                    plantCount++;
                                }
                            }
                            catch (System.Exception ex)
                            {
                                if (LandingZoneSettings.LogLevel >= LoggingLevel.Verbose)
                                    Log.Warning($"[LandingZone] MineralStockpileCache: Failed to resolve plant grove at tile {tileId}: {ex.Message}");
                            }
                        }
                    }

                    // Resolve WildPlants to flagship plant species
                    var wildPlantsMutator = mutators.FirstOrDefault(m => m.defName == "WildPlants");
                    if (wildPlantsMutator != null && wildPlantsMutator.Worker != null)
                    {
                        var planetTile = new PlanetTile(tileId, world.grid.Surface);

                        // GetPlantKind is private instance method
                        var method = AccessTools.Method(wildPlantsMutator.Worker.GetType(), "GetPlantKind");
                        if (method != null)
                        {
                            try
                            {
                                var plantDef = method.Invoke(wildPlantsMutator.Worker, new object[] { planetTile }) as Verse.ThingDef;
                                if (plantDef != null)
                                {
                                    plants.Add(plantDef.defName); // e.g., "Plant_Berry", "Plant_Healroot"
                                    plantCount++;
                                }
                            }
                            catch (System.Exception ex)
                            {
                                if (LandingZoneSettings.LogLevel >= LoggingLevel.Verbose)
                                    Log.Warning($"[LandingZone] MineralStockpileCache: Failed to resolve wild plants at tile {tileId}: {ex.Message}");
                            }
                        }
                    }

                    // Only cache tiles that have something
                    if (minerals.Count > 0 || stockpiles.Count > 0 || animals.Count > 0 || plants.Count > 0)
                    {
                        _cache[tileId] = new TileDetail(minerals, stockpiles, animals, plants);
                    }
                }

                sw.Stop();

                if (LandingZoneSettings.LogLevel >= LoggingLevel.Standard)
                {
                    Log.Message($"[LandingZone] MineralStockpileCache: Built cache in {sw.ElapsedMilliseconds}ms " +
                               $"({mineralCount} MineralRich, {stockpileCount} Stockpile, {animalCount} AnimalHabitat, {plantCount} PlantGrove/WildPlants tiles cached)");

                    // Log mineral type distribution for debugging
                    var mineralTypes = new Dictionary<string, int>();
                    foreach (var detail in _cache.Values)
                    {
                        foreach (var mineral in detail.Minerals)
                        {
                            if (!mineralTypes.ContainsKey(mineral))
                                mineralTypes[mineral] = 0;
                            mineralTypes[mineral]++;
                        }
                    }

                    if (mineralTypes.Count > 0)
                    {
                        var summary = string.Join(", ", mineralTypes.OrderByDescending(kvp => kvp.Value).Select(kvp => $"{kvp.Key}:{kvp.Value}"));
                        Log.Message($"[LandingZone] MineralStockpileCache: Mineral distribution: {summary}");
                    }

                    // Log stockpile type distribution for debugging
                    var stockpileTypes = new Dictionary<string, int>();
                    foreach (var detail in _cache.Values)
                    {
                        foreach (var stockpile in detail.Stockpiles)
                        {
                            if (!stockpileTypes.ContainsKey(stockpile))
                                stockpileTypes[stockpile] = 0;
                            stockpileTypes[stockpile]++;
                        }
                    }

                    if (stockpileTypes.Count > 0)
                    {
                        var summary = string.Join(", ", stockpileTypes.OrderByDescending(kvp => kvp.Value).Select(kvp => $"{kvp.Key}:{kvp.Value}"));
                        Log.Message($"[LandingZone] MineralStockpileCache: Stockpile distribution: {summary}");
                    }

                    // Log animal species distribution for debugging
                    var animalSpecies = new Dictionary<string, int>();
                    foreach (var detail in _cache.Values)
                    {
                        foreach (var animal in detail.Animals)
                        {
                            if (!animalSpecies.ContainsKey(animal))
                                animalSpecies[animal] = 0;
                            animalSpecies[animal]++;
                        }
                    }

                    if (animalSpecies.Count > 0)
                    {
                        var summary = string.Join(", ", animalSpecies.OrderByDescending(kvp => kvp.Value).Select(kvp => $"{kvp.Key}:{kvp.Value}"));
                        Log.Message($"[LandingZone] MineralStockpileCache: Animal distribution: {summary}");
                    }

                    // Log plant species distribution for debugging
                    var plantSpecies = new Dictionary<string, int>();
                    foreach (var detail in _cache.Values)
                    {
                        foreach (var plant in detail.Plants)
                        {
                            if (!plantSpecies.ContainsKey(plant))
                                plantSpecies[plant] = 0;
                            plantSpecies[plant]++;
                        }
                    }

                    if (plantSpecies.Count > 0)
                    {
                        var summary = string.Join(", ", plantSpecies.OrderByDescending(kvp => kvp.Value).Select(kvp => $"{kvp.Key}:{kvp.Value}"));
                        Log.Message($"[LandingZone] MineralStockpileCache: Plant distribution: {summary}");
                    }

                    // Warn about unknown ores (not in whitelist)
                    if (unknownOres.Count > 0)
                    {
                        Log.Warning($"[LandingZone] MineralStockpileCache: Found {unknownOres.Count} unknown ore type(s): {string.Join(", ", unknownOres)}");
                        Log.Warning($"[LandingZone]   → These ores were filtered out. Add to whitelist in MineralStockpileCache.cs if they're valid vanilla ores.");
                    }
                }
                else if (sw.ElapsedMilliseconds > 1000)
                {
                    Log.Warning($"[LandingZone] MineralStockpileCache: Cache build took {sw.ElapsedMilliseconds}ms (longer than expected)");
                }
            }
            finally
            {
                _isBuilding = false;
            }
        }
    }
}

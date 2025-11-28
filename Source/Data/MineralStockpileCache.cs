using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using LandingZone.Core.Diagnostics;
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
        private static HashSet<string>? _validOresCache; // Dynamically built from DefDatabase

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
            if (DevDiagnostics.PhaseADiagnosticsEnabled)
            {
                Log.Message($"[LZ][DIAG] MineralStockpileCache.EnsureBuilt START, timestamp={System.DateTime.Now:HH:mm:ss.fff}");
            }
            var sw = Stopwatch.StartNew();
            EnsureInitialized();
            sw.Stop();
            if (DevDiagnostics.PhaseADiagnosticsEnabled)
            {
                Log.Message($"[LZ][DIAG] MineralStockpileCache.EnsureBuilt END, elapsed_ms={sw.ElapsedMilliseconds}");
            }
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

                // Build valid ore set dynamically from DefDatabase (mod-agnostic)
                var validOres = GetValidMineableOres();
                var newOresDiscovered = new HashSet<string>(); // Track newly discovered mod ores for logging

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
                                // Dynamic validation - accept all mineable defs discovered from DefDatabase
                            if (validOres.Contains(oreDef.defName))
                            {
                                minerals.Add(oreDef.defName); // e.g., MineableGold, MineableUranium, BVM_MineablePlatinum, etc.
                                mineralCount++;
                            }
                            else
                            {
                                // Log newly discovered ores not in our dynamic set (shouldn't happen often)
                                // But accept them anyway - they came from the game's ore system
                                minerals.Add(oreDef.defName);
                                mineralCount++;
                                newOresDiscovered.Add(oreDef.defName);
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

                    // Log newly discovered ores (not in DefDatabase at init time, but accepted anyway)
                    if (newOresDiscovered.Count > 0)
                    {
                        Log.Message($"[LandingZone] MineralStockpileCache: Accepted {newOresDiscovered.Count} ore(s) not in initial DefDatabase scan: {string.Join(", ", newOresDiscovered)}");
                    }

                    // Log valid ores set size for debugging
                    Log.Message($"[LandingZone] MineralStockpileCache: Valid ore set contains {validOres.Count} mineable defs (dynamic detection)");
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

        /// <summary>
        /// Dynamically builds a set of valid mineable ore defNames from DefDatabase.
        /// Uses strict heuristics to avoid grabbing non-ore defs (frames, furniture, etc.).
        /// </summary>
        private static HashSet<string> GetValidMineableOres()
        {
            // Return cached set if already built
            if (_validOresCache != null)
                return _validOresCache;

            _validOresCache = new HashSet<string>();

            // Known mod prefixes that produce mineable ores
            var knownModPrefixes = new[] { "AB_", "BVM_", "DankPyon_", "EM_", "GL_", "VFE_", "VPE_", "VCHE_" };

            try
            {
                // Scan all ThingDefs for mineable rocks/ores
                foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
                {
                    if (def == null || def.building == null) continue;

                    // Primary criteria: has mineable yield (produces resources when mined)
                    bool hasMinedYield = def.building.mineableThing != null;

                    // Secondary criteria: natural rock with scatter commonality (ore veins)
                    bool isMineableRock = def.building.isNaturalRock && def.building.mineableScatterCommonality > 0;

                    // Accept if it meets primary or secondary criteria
                    if (hasMinedYield || isMineableRock)
                    {
                        _validOresCache.Add(def.defName);
                        continue;
                    }

                    // Mod-specific handling: only accept known mod prefixes if they ALSO have mineable properties
                    // This catches odd mod naming conventions but requires actual mineable behavior
                    bool hasKnownModPrefix = knownModPrefixes.Any(p => def.defName.StartsWith(p));
                    if (hasKnownModPrefix && def.building.mineableYieldWasteable)
                    {
                        _validOresCache.Add(def.defName);
                    }
                }

                // Manual allowlist for edge cases that don't fit the heuristics
                // (Add specific defNames here if discovered in testing)
                var manualAllowlist = new[]
                {
                    // DankPyon golem rocks that spawn as map gen features
                    "DankPyon_GolemRock_Iron_MapGen",
                    "DankPyon_GolemRock_Silver_MapGen"
                };
                foreach (var defName in manualAllowlist)
                {
                    if (DefDatabase<ThingDef>.GetNamedSilentFail(defName) != null)
                    {
                        _validOresCache.Add(defName);
                    }
                }

                if (LandingZoneSettings.LogLevel >= LoggingLevel.Verbose)
                {
                    Log.Message($"[LandingZone] MineralStockpileCache: Built valid ores set with {_validOresCache.Count} entries from DefDatabase");
                    if (_validOresCache.Count > 0 && _validOresCache.Count <= 50)
                    {
                        Log.Message($"[LandingZone]   Valid ores: {string.Join(", ", _validOresCache.OrderBy(s => s))}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[LandingZone] MineralStockpileCache: Failed to build valid ores set: {ex.Message}");
                // Fallback to basic vanilla ores
                _validOresCache = new HashSet<string>
                {
                    "MineableSilver", "MineableSteel", "MineableUranium",
                    "MineablePlasteel", "MineableGold", "MineableComponentsIndustrial", "MineableJade"
                };
            }

            return _validOresCache;
        }

        /// <summary>
        /// Gets the list of all valid mineable ore defNames (for UI/diagnostics).
        /// </summary>
        public static IReadOnlyCollection<string> GetAllValidOres()
        {
            return GetValidMineableOres();
        }
    }
}

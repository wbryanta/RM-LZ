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
        /// Details about a tile's minerals and stockpiles.
        /// </summary>
        public readonly struct TileDetail
        {
            public TileDetail(List<string> minerals, List<string> stockpiles)
            {
                Minerals = minerals;
                Stockpiles = stockpiles;
            }

            /// <summary>
            /// Ore/mineable defNames from BiomeDef.GetMineableThingDefForTile (e.g., "MineableGold", "MineableUranium", "MineableJade")
            /// </summary>
            public List<string> Minerals { get; }

            /// <summary>
            /// Stockpile contents (reserved for future use)
            /// </summary>
            public List<string> Stockpiles { get; }
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
        public (int mineralTiles, int stockpileTiles) GetCacheStats()
        {
            if (_cache == null) return (0, 0);

            int mineralTiles = _cache.Count(kvp => kvp.Value.Minerals.Count > 0);
            int stockpileTiles = _cache.Count(kvp => kvp.Value.Stockpiles.Count > 0);
            return (mineralTiles, stockpileTiles);
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

                // Whitelist of known valid ore defNames (based on canonical world data)
                var validOres = new HashSet<string>
                {
                    "MineableSilver",
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

                    // TODO: Resolve Stockpile contents when we add that feature
                    if (mutators.Any(m => m.defName == "Stockpile"))
                    {
                        // Placeholder - we'd need to research how to resolve stockpile contents
                        stockpileCount++;
                    }

                    // Only cache tiles that have something
                    if (minerals.Count > 0 || stockpiles.Count > 0)
                    {
                        _cache[tileId] = new TileDetail(minerals, stockpiles);
                    }
                }

                sw.Stop();

                if (LandingZoneSettings.LogLevel >= LoggingLevel.Standard)
                {
                    Log.Message($"[LandingZone] MineralStockpileCache: Built cache in {sw.ElapsedMilliseconds}ms " +
                               $"({mineralCount} MineralRich, {stockpileCount} Stockpile tiles cached)");

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

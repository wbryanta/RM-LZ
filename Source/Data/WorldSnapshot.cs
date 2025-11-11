using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Data
{
    /// <summary>
    /// Lightweight, refreshable snapshot of world level data that the UI cares about.
    /// </summary>
    public sealed class WorldSnapshot
    {
        public string SeedString { get; private set; } = string.Empty;
        public float PlanetCoverage { get; private set; }
        public int TotalTileCount { get; private set; }
        public int SettleableTileCount { get; private set; }
        public IReadOnlyList<int> SettleableTiles => _settleableTiles;
        public IReadOnlyList<TileInfo> TileInfos => _tileInfos;

        private readonly List<int> _settleableTiles = new();
        private readonly List<TileInfo> _tileInfos = new();

        public void RefreshFromCurrentWorld()
        {
            var world = Find.World;
            if (world == null)
                return;

            var worldGrid = world.grid;
            if (worldGrid == null)
                return;

            SeedString = world.info?.seedString ?? string.Empty;
            PlanetCoverage = world.info?.planetCoverage ?? 0f;
            TotalTileCount = worldGrid.TilesCount;

            _settleableTiles.Clear();
            _tileInfos.Clear();

            for (var tileId = 0; tileId < worldGrid.TilesCount; tileId++)
            {
                var tile = worldGrid[tileId];
                if (tile == null)
                    continue;

                var info = BuildTileInfo(world, tile, tileId);
                _tileInfos.Add(info);

                var impassable = Find.World.Impassable(tileId);
                if (info.Biome != null && !info.Biome.impassable && !impassable)
                {
                    _settleableTiles.Add(tileId);
                }
            }

            SettleableTileCount = _settleableTiles.Count;
        }

        public bool TryGetInfo(int tileId, out TileInfo info)
        {
            if (tileId >= 0 && tileId < _tileInfos.Count)
            {
                info = _tileInfos[tileId];
                return true;
            }

            info = default;
            return false;
        }

        private static TileInfo BuildTileInfo(World world, Tile tileObj, int tileId)
        {
            // HYBRID APPROACH: Only compute CHEAP properties here.
            // Expensive calculations (growing days, stones, grazing, movement) are deferred to TileDataCache.

            var biome = tileObj.PrimaryBiome;
            var temperature = tileObj.temperature;
            var rainfall = tileObj.rainfall;
            var isCoastal = tileObj.IsCoastal;
            var hasRiver = tileObj is SurfaceTile surface && surface.Rivers != null && surface.Rivers.Count > 0;
            var featureDef = tileObj.feature?.def;
            var hilliness = tileObj.hilliness;

            return new TileInfo(biome, temperature, rainfall, isCoastal, hasRiver, featureDef, hilliness);
        }

        /// <summary>
        /// Basic tile information containing only CHEAP properties.
        /// Expensive properties (growing days, stones, grazing, movement) are computed lazily via TileDataCache.
        /// This enables fast snapshot initialization (&lt;1s vs ~120s for 100% coverage worlds).
        /// </summary>
        public readonly struct TileInfo
        {
            public TileInfo(BiomeDef? biome, float temperature, float rainfall, bool isCoastal, bool hasRiver,
                FeatureDef? featureDef, Hilliness hilliness)
            {
                Biome = biome;
                Temperature = temperature;
                Rainfall = rainfall;
                IsCoastal = isCoastal;
                HasRiver = hasRiver;
                FeatureDef = featureDef;
                Hilliness = hilliness;
            }

            // CHEAP properties - direct access from Tile object
            public BiomeDef? Biome { get; }
            public float Temperature { get; }
            public float Rainfall { get; }
            public bool IsCoastal { get; }
            public bool HasRiver { get; }
            public FeatureDef? FeatureDef { get; }
            public Hilliness Hilliness { get; }

            // EXPENSIVE properties moved to TileInfoExtended in TileDataCache:
            // - GrowingDays (GenTemperature.TwelfthsInAverageTemperatureRange - 2-3ms)
            // - StoneDefNames (World.NaturalRockTypesIn - 1-2ms)
            // - CanGrazeNow (VirtualPlantsUtility - 1-2ms)
            // - MovementDifficulty (WorldPathGrid.CalculatedMovementDifficultyAt - 1-2ms)
            // - Pollution (tile.pollution - actually cheap, moved for consistency)
            // - Forageability (biome.forageability - actually cheap, moved for consistency)
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Data
{
    /// <summary>
    /// Caches expensive tile calculations on-demand to avoid upfront computation cost.
    /// Only computes extended properties when requested, then memoizes the result.
    /// </summary>
    public sealed class TileDataCache
    {
        private readonly Dictionary<int, TileInfoExtended> _cache = new();
        private string _worldSeed = string.Empty;

        /// <summary>
        /// Gets extended tile info, computing it on first access and caching thereafter.
        /// </summary>
        public TileInfoExtended GetOrCompute(int tileId)
        {
            if (_cache.TryGetValue(tileId, out var cached))
                return cached;

            var extended = ComputeExtended(tileId);
            _cache[tileId] = extended;
            return extended;
        }

        /// <summary>
        /// Resets the cache if the world seed has changed.
        /// </summary>
        public void ResetIfWorldChanged(string newSeed)
        {
            if (_worldSeed != newSeed)
            {
                _cache.Clear();
                _worldSeed = newSeed;
            }
        }

        /// <summary>
        /// Clears all cached data.
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
            _worldSeed = string.Empty;
        }

        /// <summary>
        /// Gets the number of cached entries.
        /// </summary>
        public int CachedCount => _cache.Count;

        private static TileInfoExtended ComputeExtended(int tileId)
        {
            var world = Find.World;
            if (world == null || world.grid == null)
                return default;

            var tile = world.grid[tileId];
            if (tile == null)
                return default;

            var biome = tile.PrimaryBiome;
            var planetTile = new PlanetTile(tileId, world.grid.Surface);

            // EXPENSIVE: Growing days calculation (2-3ms per tile)
            var growingTwelfths = GenTemperature.TwelfthsInAverageTemperatureRange(planetTile, 6f, 42f);
            var daysPerTwelfth = GenDate.DaysPerYear / (float)GenDate.TwelfthsPerYear;
            var growingDays = (growingTwelfths?.Count ?? 0) * daysPerTwelfth;

            // EXPENSIVE: Stone types query (1-2ms per tile)
            var stoneDefNames = world.NaturalRockTypesIn(tileId)?
                .Where(t => t != null)
                .Select(t => t.defName)
                .ToArray() ?? System.Array.Empty<string>();

            // EXPENSIVE: Grazing check (1-2ms per tile)
            var canGrazeNow = VirtualPlantsUtility.EnvironmentAllowsEatingVirtualPlantsNowAt(planetTile);

            // EXPENSIVE: Movement difficulty calculation (1-2ms per tile)
            var movementDifficulty = WorldPathGrid.CalculatedMovementDifficultyAt(planetTile, perceivedStatic: true);

            // CHEAP: Direct property access
            var pollution = tile.pollution;
            var forageability = biome?.forageability ?? 0f;

            // EXPENSIVE: Min/max temperature calculation
            var minTemp = GenTemperature.MinTemperatureAtTile(tileId);
            var maxTemp = GenTemperature.MaxTemperatureAtTile(tileId);

            return new TileInfoExtended(
                growingDays,
                stoneDefNames,
                canGrazeNow,
                movementDifficulty,
                pollution,
                forageability,
                minTemp,
                maxTemp
            );
        }
    }

    /// <summary>
    /// Extended tile information requiring expensive calculations.
    /// Computed lazily via TileDataCache.
    /// </summary>
    public readonly struct TileInfoExtended
    {
        public TileInfoExtended(
            float growingDays,
            string[] stoneDefNames,
            bool canGrazeNow,
            float movementDifficulty,
            float pollution,
            float forageability,
            float minTemperature,
            float maxTemperature)
        {
            GrowingDays = growingDays;
            StoneDefNames = stoneDefNames;
            CanGrazeNow = canGrazeNow;
            MovementDifficulty = movementDifficulty;
            Pollution = pollution;
            Forageability = forageability;
            MinTemperature = minTemperature;
            MaxTemperature = maxTemperature;
        }

        public float GrowingDays { get; }
        public string[] StoneDefNames { get; }
        public bool CanGrazeNow { get; }
        public float MovementDifficulty { get; }
        public float Pollution { get; }
        public float Forageability { get; }
        public float MinTemperature { get; }
        public float MaxTemperature { get; }
    }
}

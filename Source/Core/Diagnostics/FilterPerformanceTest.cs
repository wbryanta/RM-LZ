using System;
using System.Diagnostics;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core.Diagnostics
{
    /// <summary>
    /// Performance testing for "heavy" vs "light" filter operations.
    /// Tests whether RimWorld caches the data we thought was expensive.
    /// </summary>
    public static class FilterPerformanceTest
    {
        /// <summary>
        /// Tests performance of supposedly "heavy" RimWorld API calls.
        /// Run this to determine if heavy/light distinction still matters.
        /// </summary>
        public static void RunPerformanceTest()
        {
            var world = Find.World;
            if (world?.grid == null)
            {
                Log.Error("[LandingZone] FilterPerformanceTest: No world loaded");
                return;
            }

            // Get settleable tiles directly from world (same pattern as WorldSnapshot)
            var settleable = new System.Collections.Generic.List<int>();
            var worldGrid = world.grid;

            for (int tileId = 0; tileId < worldGrid.TilesCount; tileId++)
            {
                var tile = worldGrid[tileId];
                var biome = tile.PrimaryBiome;
                if (biome != null && biome.canBuildBase)
                {
                    settleable.Add(tileId);
                    if (settleable.Count >= 10000) // Cap at 10k for performance
                        break;
                }
            }

            Log.Message($"[LandingZone] Performance Test: Testing {settleable.Count} settleable tiles");

            // Test 1: GenTemperature.MinTemperatureAtTile (supposed to be Heavy)
            TestOperation("MinTemperatureAtTile", settleable, id =>
            {
                return GenTemperature.MinTemperatureAtTile(id);
            });

            // Test 2: GenTemperature.MaxTemperatureAtTile (supposed to be Heavy)
            TestOperation("MaxTemperatureAtTile", settleable, id =>
            {
                return GenTemperature.MaxTemperatureAtTile(id);
            });

            // Test 3: world.NaturalRockTypesIn (supposed to be Heavy)
            TestOperation("NaturalRockTypesIn", settleable, id =>
            {
                var rocks = world.NaturalRockTypesIn(id);
                if (rocks == null) return 0;
                return rocks.Count();
            });

            // Test 4: VirtualPlantsUtility (supposed to be Heavy)
            TestOperation("VirtualPlants.CanGraze", settleable, id =>
            {
                var planetTile = new PlanetTile(id, world.grid.Surface);
                return VirtualPlantsUtility.EnvironmentAllowsEatingVirtualPlantsNowAt(planetTile) ? 1 : 0;
            });

            // Test 5: Baseline - simple property access (known to be Light)
            TestOperation("tile.PrimaryBiome access (baseline)", settleable, id =>
            {
                var tile = world.grid[id];
                return tile.PrimaryBiome != null ? 1 : 0;
            });

            Log.Message("[LandingZone] Performance Test: Complete. Check results above.");
        }

        private static void TestOperation<T>(string name, System.Collections.Generic.List<int> tiles, Func<int, T> operation)
        {
            var sw = Stopwatch.StartNew();

            foreach (var tileId in tiles)
            {
                var result = operation(tileId);
            }

            sw.Stop();

            double msPerTile = (double)sw.ElapsedMilliseconds / tiles.Count;
            double estimatedFullWorld = msPerTile * 295000; // Estimate for full world

            Log.Message($"[LandingZone] PerformanceTest: {name}");
            Log.Message($"  - {tiles.Count} tiles in {sw.ElapsedMilliseconds}ms ({msPerTile:F4}ms/tile)");
            Log.Message($"  - Estimated full world (295k): {estimatedFullWorld:F0}ms = {estimatedFullWorld/1000:F1}s");

            if (estimatedFullWorld > 2000)
            {
                Log.Warning($"[LandingZone] ⚠️ {name} is EXPENSIVE - needs optimization!");
            }
            else if (estimatedFullWorld > 500)
            {
                Log.Message($"[LandingZone] ⚠️ {name} is moderate - consider optimizing");
            }
            else
            {
                Log.Message($"[LandingZone] ✓ {name} is cheap - no optimization needed");
            }
        }
    }
}

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
                Log.Error("LandingZone_DevTools_PerfTestNoWorld".Translate());
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

            Log.Message("LandingZone_DevTools_PerfTestTesting".Translate(settleable.Count));

            // Test 1a: GenTemperature.MinTemperatureAtTile METHOD (expensive?)
            TestOperation("GenTemperature.MinTemperatureAtTile (method)", settleable, id =>
            {
                return GenTemperature.MinTemperatureAtTile(id);
            });

            // Test 1b: tile.MinTemperature PROPERTY (cached?)
            TestOperation("tile.MinTemperature (property)", settleable, id =>
            {
                var tile = world.grid[id];
                return tile.MinTemperature;
            });

            // Test 2a: GenTemperature.MaxTemperatureAtTile METHOD (expensive?)
            TestOperation("GenTemperature.MaxTemperatureAtTile (method)", settleable, id =>
            {
                return GenTemperature.MaxTemperatureAtTile(id);
            });

            // Test 2b: tile.MaxTemperature PROPERTY (cached?)
            TestOperation("tile.MaxTemperature (property)", settleable, id =>
            {
                var tile = world.grid[id];
                return tile.MaxTemperature;
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

            Log.Message("LandingZone_DevTools_PerfTestComplete".Translate());
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

            Log.Message("LandingZone_DevTools_PerfTestTiles".Translate(name));
            Log.Message("LandingZone_DevTools_PerfTestResult".Translate(tiles.Count, sw.ElapsedMilliseconds, msPerTile.ToString("F4")));
            Log.Message("LandingZone_DevTools_PerfTestEstimate".Translate(estimatedFullWorld.ToString("F0"), (estimatedFullWorld/1000).ToString("F1")));

            if (estimatedFullWorld > 2000)
            {
                Log.Warning("LandingZone_DevTools_PerfTestExpensive".Translate(name));
            }
            else if (estimatedFullWorld > 500)
            {
                Log.Message("LandingZone_DevTools_PerfTestModerate".Translate(name));
            }
            else
            {
                Log.Message("LandingZone_DevTools_PerfTestCheap".Translate(name));
            }
        }
    }
}

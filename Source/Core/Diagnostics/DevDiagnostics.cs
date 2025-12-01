#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using RimWorld;
using RimWorld.Planet;
using Verse;
using LandingZone.Core.Filtering.Filters;
using LandingZone.Core.UI;
using LandingZone.Data;
using UnityEngine;

namespace LandingZone.Core.Diagnostics
{
    /// <summary>
    /// Dev-mode diagnostics helpers to dump world-facing defs we care about for coverage checks.
    /// </summary>
    public static class DevDiagnostics
    {
        private static bool _phaseADiagnosticsEnabled;

        public static bool PhaseADiagnosticsEnabled
        {
            get => _phaseADiagnosticsEnabled;
            set => _phaseADiagnosticsEnabled = value;
        }

        public static void DumpWorldDefinitions()
        {
            DumpBiomes();
            DumpMutators();
            DumpWorldObjects();
        }

        /// <summary>
        /// Dumps detailed information about the top N search results to Player.log.
        /// Shows tile ID, score, biome, coordinates, and all filter match details.
        /// </summary>
        /// <param name="count">Number of top results to dump (default 10)</param>
        public static void DumpTopResults(int count = 10)
        {
            var results = LandingZoneContext.LatestResults;

            if (results == null || results.Count == 0)
            {
                Log.Warning("[LandingZone][DEV] DumpTopResults: No search results available. Run a search first.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[LandingZone][DEV] ===== TOP {System.Math.Min(count, results.Count)} SEARCH RESULTS =====");
            sb.AppendLine($"Total results: {results.Count}");

            var world = Find.World;
            var surfaceGrid = world?.grid;  // SurfaceTile grid with PrimaryBiome, pollution, etc.
            var worldGrid = Find.WorldGrid; // Tile grid with Rivers, Roads, etc.

            int displayed = 0;
            foreach (var result in results.Take(count))
            {
                displayed++;
                sb.AppendLine();
                sb.AppendLine($"--- Result #{displayed}: Tile {result.TileId} ---");
                sb.AppendLine($"  Score: {result.Score:F4}");

                // Get tile info from SurfaceTile (biome, temperature, etc.)
                if (surfaceGrid != null && result.TileId >= 0 && result.TileId < surfaceGrid.TilesCount)
                {
                    var surfaceTile = surfaceGrid[result.TileId];
                    if (surfaceTile != null)
                    {
                        var biome = surfaceTile.PrimaryBiome?.label ?? "unknown";
                        sb.AppendLine($"  Biome: {biome}");
                        sb.AppendLine($"  Hilliness: {surfaceTile.hilliness}");
                        sb.AppendLine($"  Elevation: {surfaceTile.elevation:F0}m");
                        sb.AppendLine($"  Temperature: {surfaceTile.temperature:F1}°C");
                        sb.AppendLine($"  Rainfall: {surfaceTile.rainfall:F0}mm");
                    }
                }

                // Get position from WorldGrid
                if (worldGrid != null && result.TileId >= 0 && result.TileId < worldGrid.TilesCount)
                {
                    var pos = worldGrid.GetTileCenter(result.TileId);
                    sb.AppendLine($"  World Position: ({pos.x:F2}, {pos.y:F2}, {pos.z:F2})");

                    // Get Rivers and Roads from WorldGrid Tile
                    var tile = worldGrid[result.TileId];
                    if (tile != null)
                    {
                        if (tile.Rivers != null && tile.Rivers.Any())
                        {
                            var rivers = tile.Rivers.Select(r => r.river?.defName ?? "unknown").Distinct();
                            sb.AppendLine($"  Rivers: {string.Join(", ", rivers)}");
                        }

                        if (tile.Roads != null && tile.Roads.Any())
                        {
                            var roads = tile.Roads.Select(r => r.road?.defName ?? "unknown").Distinct();
                            sb.AppendLine($"  Roads: {string.Join(", ", roads)}");
                        }
                    }
                }

                // Mutators/features
                var features = MapFeatureFilter.GetTileMapFeatures(result.TileId).ToList();
                if (features.Any())
                {
                    sb.AppendLine($"  Map Features: {string.Join(", ", features)}");
                }

                // Match breakdown from BreakdownV2 if available
                if (result.BreakdownV2 != null)
                {
                    var breakdown = result.BreakdownV2.Value;
                    var allFilters = new List<Data.FilterMatchInfo>();
                    if (breakdown.MatchedFilters != null) allFilters.AddRange(breakdown.MatchedFilters);
                    if (breakdown.MissedFilters != null) allFilters.AddRange(breakdown.MissedFilters);

                    if (allFilters.Any())
                    {
                        sb.AppendLine($"  Match Breakdown ({allFilters.Count} filters):");
                        foreach (var match in allFilters.OrderByDescending(m => m.IsPriority).ThenBy(m => m.FilterName))
                        {
                            string prefix = match.IsPriority ? "[PRIO]" : "[PREF]";
                            string status = match.IsMatched ? "PASS" : "FAIL";
                            sb.AppendLine($"    {prefix} {match.FilterName}: {status} (membership={match.Membership:F3})");
                        }
                    }

                    // Mutator contributions
                    if (breakdown.Mutators != null && breakdown.Mutators.Any())
                    {
                        sb.AppendLine($"  Mutator Contributions ({breakdown.Mutators.Count}):");
                        foreach (var mut in breakdown.Mutators.OrderByDescending(m => m.Contribution))
                        {
                            string sign = mut.Contribution >= 0 ? "+" : "";
                            sb.AppendLine($"    {mut.MutatorName}: {sign}{mut.Contribution:F3}");
                        }
                    }

                    // Score components
                    sb.AppendLine($"  Score Components: Crit={breakdown.CriticalScore:F3}, Pref={breakdown.PreferredScore:F3}, Mut={breakdown.MutatorScore:F3}, Penalty={breakdown.Penalty:F3}");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"[LandingZone][DEV] ===== END TOP RESULTS DUMP =====");

            Log.Message(sb.ToString());
        }

        /// <summary>
        /// Dumps valid mineable ores detected from DefDatabase (for debugging mod ore support).
        /// Groups output by prefix for readability.
        /// </summary>
        public static void DumpValidOres()
        {
            var ores = MineralStockpileCache.GetAllValidOres();
            var sb = new StringBuilder();
            sb.AppendLine($"[LandingZone][DEV] ===== VALID MINEABLE ORES ({ores.Count} total) =====");

            // Group by prefix (mod source)
            var grouped = ores
                .OrderBy(s => s)
                .GroupBy(s => GetOrePrefix(s))
                .OrderBy(g => g.Key == "Vanilla" ? "AAA" : g.Key); // Vanilla first

            foreach (var group in grouped)
            {
                sb.AppendLine($"  [{group.Key}] ({group.Count()}):");
                sb.AppendLine($"    {string.Join(", ", group)}");
            }

            sb.AppendLine($"[LandingZone][DEV] ===== END VALID ORES =====");
            Log.Message(sb.ToString());
        }

        /// <summary>
        /// Writes a small text snapshot of the current world to Config/, sampling a limited number of tiles.
        /// Includes world size, seed, mod list hash, distinct biomes, and sampled tiles with biome/hilliness/features.
        /// </summary>
        public static void DumpMiniWorldSnapshot(int sampleSize = 200)
        {
            var world = Find.World;
            if (world == null)
            {
                Log.Warning("[LandingZone][DEV] DumpMiniWorldSnapshot: World unavailable.");
                return;
            }
            var worldGrid = world?.grid;
            if (worldGrid == null)
            {
                Log.Warning("[LandingZone][DEV] DumpMiniWorldSnapshot: World grid unavailable.");
                return;
            }

            int tileCount = worldGrid.TilesCount;
            var seed = world!.info?.seedString ?? "unknown";

            // Build a simple mod hash for reference
            var modIds = LoadedModManager.RunningModsListForReading.Select(m => m.PackageId).OrderBy(id => id).ToList();
            string modHash = GenText.StableStringHash(string.Join(";", modIds)).ToString("X");

            var sb = new StringBuilder();
            sb.AppendLine("[LandingZone][DEV] ===== MINI WORLD SNAPSHOT =====");
            sb.AppendLine($"World seed: {seed}");
            sb.AppendLine($"Tiles: {tileCount}");
            sb.AppendLine($"Mod count: {modIds.Count} (hash {modHash})");

            // Distinct biomes present (sampled via tiles)
            var biomes = new HashSet<string>();
            for (int i = 0; i < Mathf.Min(tileCount, 5000); i++)
            {
                var tile = worldGrid[i];
                var biomeLabel = tile?.PrimaryBiome?.defName ?? "unknown";
                biomes.Add(biomeLabel);
            }
            sb.AppendLine($"Biomes observed (sampled): {string.Join(", ", biomes.OrderBy(b => b))}");

            sb.AppendLine();
            sb.AppendLine($"Sampled tiles (first {Mathf.Min(sampleSize, tileCount)}):");

            int sampleCount = Mathf.Min(sampleSize, tileCount);
            for (int i = 0; i < sampleCount; i++)
            {
                var tile = worldGrid[i];
                if (tile == null) continue;
                var biome = tile.PrimaryBiome?.defName ?? "unknown";
                var rivers = tile.Rivers != null && tile.Rivers.Any()
                    ? string.Join(",", tile.Rivers.Select(r => r.river?.defName ?? "unknown").Distinct())
                    : "none";
                var roads = tile.Roads != null && tile.Roads.Any()
                    ? string.Join(",", tile.Roads.Select(r => r.road?.defName ?? "unknown").Distinct())
                    : "none";
                var features = MapFeatureFilter.GetTileMapFeatures(i).ToList();
                string featureStr = features.Any() ? string.Join(",", features) : "none";

                sb.AppendLine($"Tile {i}: biome={biome}, hilliness={tile.hilliness}, elev={tile.elevation:F0}, rivers={rivers}, roads={roads}, features={featureStr}");
            }

            sb.AppendLine("[LandingZone][DEV] ===== END MINI WORLD SNAPSHOT =====");

            var path = Path.Combine(GenFilePaths.ConfigFolderPath, $"LZ_MiniWorldSnapshot_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt");
            File.WriteAllText(path, sb.ToString());
            Log.Message($"[LandingZone][DEV] Mini world snapshot written to {path}");
        }

        /// <summary>
        /// Extracts prefix from ore defName for grouping (e.g., "DankPyon_MineableGold" → "DankPyon").
        /// </summary>
        private static string GetOrePrefix(string defName)
        {
            // Known mod prefixes
            if (defName.StartsWith("AB_")) return "Alpha Biomes (AB_)";
            if (defName.StartsWith("BVM_")) return "BVM";
            if (defName.StartsWith("DankPyon_")) return "DankPyon";
            if (defName.StartsWith("EM_")) return "EM";
            if (defName.StartsWith("GL_")) return "Geological Landforms (GL_)";
            if (defName.StartsWith("VFE_")) return "Vanilla Expanded (VFE_)";
            if (defName.StartsWith("VPE_")) return "Vanilla Expanded (VPE_)";
            if (defName.StartsWith("VCHE_")) return "Vanilla Expanded (VCHE_)";

            // Standard vanilla ores start with "Mineable" without underscore prefix
            if (defName.StartsWith("Mineable") && !defName.Contains("_"))
                return "Vanilla";

            // Other patterns - extract prefix before underscore if present
            var underscoreIdx = defName.IndexOf('_');
            if (underscoreIdx > 0 && underscoreIdx < 15)
                return defName.Substring(0, underscoreIdx);

            return "Other";
        }

        /// <summary>
        /// Compare runtime mutators (tile scan) vs UI map features list and log the differences.
        /// </summary>
        public static void CompareMutatorCoverage()
        {
            var runtimeMutators = GetRuntimeMutators();
            // UI sources: curated buckets + full available types (DefDatabase/fallback scan)
            var uiFeatures = new HashSet<string>(AdvancedModeUI.GetCuratedMutatorsForDiagnostics());
            uiFeatures.UnionWith(MapFeatureFilter.GetAllMapFeatureTypes());

            var missingInUi = runtimeMutators.Except(uiFeatures).OrderBy(s => s).ToList();
            var uiOnly = uiFeatures.Except(runtimeMutators).OrderBy(s => s).ToList();

            Log.Message($"[LandingZone][DEV] Mutator coverage: runtime={runtimeMutators.Count}, uiFeatures={uiFeatures.Count}, missingInUI={missingInUi.Count}, uiOnly={uiOnly.Count}");
            if (missingInUi.Any())
            {
                Log.Message($"[LandingZone][DEV] Mutators present at runtime but NOT in UI: {string.Join(", ", missingInUi)}");
            }
            if (uiOnly.Any())
            {
                Log.Message($"[LandingZone][DEV] Mutators in UI list but NOT seen at runtime: {string.Join(", ", uiOnly)}");
            }
        }

        private static void DumpBiomes()
        {
            var biomes = DefDatabase<BiomeDef>.AllDefsListForReading
                .Select(b => $"{b.defName} (startable={b.canBuildBase})")
                .OrderBy(s => s)
                .ToList();
            Log.Message($"[LandingZone][DEV] Biomes ({biomes.Count}): {string.Join(", ", biomes)}");
        }

        private static void DumpMutators()
        {
            var mutators = GetRuntimeMutators();
            Log.Message($"[LandingZone][DEV] Runtime tile mutators ({mutators.Count}): {string.Join(", ", mutators.OrderBy(s => s))}");
        }

        private static void DumpWorldObjects()
        {
            var objs = DefDatabase<WorldObjectDef>.AllDefsListForReading
                .Select(o => o.defName)
                .OrderBy(s => s)
                .ToList();
            Log.Message($"[LandingZone][DEV] WorldObjectDefs ({objs.Count}): {string.Join(", ", objs)}");
        }

        private static HashSet<string> GetRuntimeMutators()
        {
            var mutators = new HashSet<string>();
            var grid = Find.World?.grid;
            if (grid == null) return mutators;

            int tileCount = grid.TilesCount;
            for (int i = 0; i < tileCount; i++)
            {
                foreach (var m in MapFeatureFilter.GetTileMapFeatures(i))
                {
                    if (!string.IsNullOrWhiteSpace(m))
                    {
                        mutators.Add(m);
                    }
                }
            }
            return mutators;
        }

        private static HashSet<string> GetUiMapFeatures()
        {
            // TODO: remove when a formal list is exposed; kept for reference
            return new HashSet<string>();
        }
    }
}

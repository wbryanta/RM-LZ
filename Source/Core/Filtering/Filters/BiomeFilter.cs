using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by biome type.
    /// Supports individual importance per biome type (Critical/Preferred/Ignored).
    /// </summary>
    public sealed class BiomeFilter : ISiteFilter
    {
        public string Id => "biome";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            var biomes = filters.Biomes;

            // Legacy support: if LockedBiome is set but Biomes container is empty, use legacy
            if (!biomes.HasAnyImportance && filters.LockedBiome != null)
            {
                var targetBiome = filters.LockedBiome;
                var grid = Find.World.grid;
                return inputTiles.Where(id =>
                {
                    var tile = grid[id];
                    return tile?.PrimaryBiome == targetBiome;
                });
            }

            // If no biomes configured, pass all tiles through
            if (!biomes.HasAnyImportance)
            {
                if (Prefs.DevMode)
                    Log.Message($"[LandingZone][BiomeFilter] No biome importance configured, passing all tiles");
                return inputTiles;
            }

            // Only hard gates (MustHave/MustNotHave) filter in Apply phase
            if (!biomes.HasHardGates)
            {
                if (Prefs.DevMode)
                    Log.Message($"[LandingZone][BiomeFilter] No hard gates (MustHave/MustNotHave), passing all tiles. HasPriority={biomes.HasPriority}, HasPreferred={biomes.HasPreferred}");
                return inputTiles;
            }

            // Debug: Log the biome filter configuration
            if (Prefs.DevMode)
            {
                var mustHaves = biomes.GetMustHaveItems().ToList();
                var mustNotHaves = biomes.GetMustNotHaveItems().ToList();
                Log.Message($"[LandingZone][BiomeFilter] Applying hard gates: MustHave=[{string.Join(", ", mustHaves)}] ({biomes.Operator}), MustNotHave=[{string.Join(", ", mustNotHaves)}]");
            }

            int inputCount = 0;
            int matchCount = 0;

            var worldGrid = Find.World.grid;

            // Filter for tiles that meet hard gate requirements (MustHave AND MustNotHave)
            var result = inputTiles.Where(id =>
            {
                inputCount++;
                var tile = worldGrid[id];
                if (tile?.PrimaryBiome == null) return false;

                var tileBiome = new List<string> { tile.PrimaryBiome.defName };
                bool meets = biomes.MeetsHardGateRequirements(tileBiome);

                // Log first few matches/non-matches in dev mode
                if (Prefs.DevMode && inputCount <= 5)
                {
                    Log.Message($"[LandingZone][BiomeFilter] Tile {id}: Biome=[{tile.PrimaryBiome.defName}], Meets={meets}");
                }

                if (meets) matchCount++;
                return meets;
            }).ToList();

            if (Prefs.DevMode)
                Log.Message($"[LandingZone][BiomeFilter] Result: {matchCount}/{inputCount} tiles passed biome filter");

            return result;
        }

        public string Describe(FilterContext context)
        {
            var filters = context.Filters;
            var biomes = filters.Biomes;

            // Legacy support
            if (!biomes.HasAnyImportance && filters.LockedBiome != null)
                return filters.LockedBiome.LabelCap;

            if (!biomes.HasAnyImportance)
                return "Any biome";

            var parts = new List<string>();
            var criticalCount = biomes.CountByImportance(FilterImportance.Critical);
            var preferredCount = biomes.CountByImportance(FilterImportance.Preferred);

            if (criticalCount > 0)
                parts.Add($"{criticalCount} required");
            if (preferredCount > 0)
                parts.Add($"{preferredCount} preferred");

            return $"Biomes: {string.Join(", ", parts)}";
        }

        /// <summary>
        /// Gets all available biome types in the game.
        /// Useful for UI dropdown population.
        /// </summary>
        public static IEnumerable<BiomeDef> GetAllBiomeTypes()
        {
            // Get only starting-location-compatible biomes
            // Filter out non-starting biomes (space, orbit, special event biomes)
            var nonStartingBiomes = new HashSet<string>
            {
                "BiomeSpace", "Space", // Space biomes (SOS2, etc.)
                "BiomeOrbit", "Orbit", // Orbital biomes
                "Labyrinth", // Labyrinth (Anomaly DLC) - special event map
                "MetalHell", "MechanoidBase", // Mechanoid bases - not player-selectable
            };

            return DefDatabase<BiomeDef>.AllDefsListForReading
                .Where(b => !b.impassable && !nonStartingBiomes.Contains(b.defName))
                .OrderBy(b => b.label);
        }

        public float Membership(int tileId, FilterContext context)
        {
            var filters = context.Filters;
            var biomes = filters.Biomes;

            // Legacy support
            if (!biomes.HasAnyImportance && filters.LockedBiome != null)
            {
                var tile = Find.World.grid[tileId];
                if (tile == null) return 0.0f;
                bool matches = tile.PrimaryBiome == filters.LockedBiome;
                return MembershipFunctions.Binary(matches);
            }

            // If no biomes configured, no membership constraint
            if (!biomes.HasAnyImportance)
                return 0.0f;

            var worldGrid = Find.World.grid;
            var tileData = worldGrid[tileId];
            if (tileData?.PrimaryBiome == null) return 0.0f;

            var tileBiome = new List<string> { tileData.PrimaryBiome.defName };

            // Align with Apply phase: prioritize Critical (MustHave) requirements
            if (biomes.HasCritical)
            {
                // Use GetCriticalSatisfaction to respect AND/OR operator
                float satisfaction = biomes.GetCriticalSatisfaction(tileBiome);
                return satisfaction;
            }

            // Priority OR Preferred: Use GetScoringScore for weighted scoring (Priority=2x, Preferred=1x)
            if (biomes.HasPriority || biomes.HasPreferred)
            {
                float score = biomes.GetScoringScore(tileBiome);
                // Normalize to [0,1] range based on maximum possible score
                int maxScore = biomes.CountByImportance(FilterImportance.Priority) * 2
                             + biomes.CountByImportance(FilterImportance.Preferred);
                float membership = maxScore > 0 ? UnityEngine.Mathf.Clamp01(score / maxScore) : 0f;
                return membership;
            }

            return 0.0f;
        }
    }
}

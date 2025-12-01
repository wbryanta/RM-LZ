#nullable enable
using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by specific forageable food type (e.g., berries, agave, etc.).
    /// Uses Preferred/Critical importance.
    /// </summary>
    public sealed class ForageableFoodFilter : ISiteFilter
    {
        public string Id => "forageable_food";
        public FilterHeaviness Heaviness => FilterHeaviness.Medium;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            var importance = filters.ForageableFoodImportance;

            // Only hard gates (MustHave/MustNotHave) filter in Apply phase
            if (!importance.IsHardGate())
                return inputTiles;

            var requiredFoodDef = filters.ForageableFoodDefName;
            if (string.IsNullOrEmpty(requiredFoodDef))
                return inputTiles;

            var worldGrid = Find.World.grid;

            return inputTiles.Where(id =>
            {
                var tile = worldGrid[id];
                if (tile == null) return false;
                var biome = tile.PrimaryBiome;
                if (biome == null) return false;

                bool hasFood = TileHasForageableFood(id, biome, requiredFoodDef!);
                return importance == FilterImportance.MustNotHave ? !hasFood : hasFood;
            });
        }

        public string Describe(FilterContext context)
        {
            var filters = context.Filters;
            if (filters.ForageableFoodImportance == FilterImportance.Ignored || string.IsNullOrEmpty(filters.ForageableFoodDefName))
                return "Any forageable food";

            string importanceLabel = filters.ForageableFoodImportance == FilterImportance.Critical ? " (required)" : " (preferred)";
            return $"Forageable: {filters.ForageableFoodDefName}{importanceLabel}";
        }

        /// <summary>
        /// Checks if a tile's biome produces a specific forageable food type.
        /// </summary>
        private static bool TileHasForageableFood(int tileId, BiomeDef biome, string foodDefName)
        {
            if (biome == null || string.IsNullOrEmpty(foodDefName))
                return false;

            // Check if this biome has the specified plant as a wild plant
            if (biome.wildPlants != null)
            {
                foreach (var plantRecord in biome.wildPlants)
                {
                    if (plantRecord?.plant == null)
                        continue;

                    // Check if this plant produces the desired forageable food
                    var plant = plantRecord.plant;

                    // Plants that produce forageable food have a harvestedThingDef
                    if (plant.plant?.harvestedThingDef != null)
                    {
                        if (plant.plant.harvestedThingDef.defName == foodDefName)
                            return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Gets all available forageable food types across all biomes.
        /// Useful for UI dropdown population.
        /// </summary>
        public static IEnumerable<ThingDef> GetAllForageableFoodTypes()
        {
            var foods = new HashSet<ThingDef>();

            foreach (var biome in DefDatabase<BiomeDef>.AllDefsListForReading)
            {
                if (biome.wildPlants == null)
                    continue;

                foreach (var plantRecord in biome.wildPlants)
                {
                    var plant = plantRecord?.plant;
                    if (plant?.plant?.harvestedThingDef != null &&
                        plant.plant.harvestedThingDef.IsNutritionGivingIngestible)
                    {
                        foods.Add(plant.plant.harvestedThingDef);
                    }
                }
            }

            return foods.OrderBy(f => f.label);
        }

        public float Membership(int tileId, FilterContext context)
        {
            var filters = context.Filters;
            var requiredFoodDef = filters.ForageableFoodDefName;

            // If no specific food required, use general forageability from cache
            if (string.IsNullOrEmpty(requiredFoodDef))
                return 0.0f;

            var tile = Find.World.grid[tileId];
            if (tile == null) return 0.0f;
            var biome = tile.PrimaryBiome;
            if (biome == null) return 0.0f;

            // Binary membership: 1.0 if tile has the required forageable food, 0.0 if not
            bool hasFood = TileHasForageableFood(tileId, biome, requiredFoodDef!);
            return MembershipFunctions.Binary(hasFood);
        }
    }
}

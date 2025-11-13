using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using Verse;

namespace LandingZone.Core.Filtering
{
    public sealed class SiteFilterRegistry
    {
        private readonly List<ISiteFilter> _filters = new List<ISiteFilter>();

        public void Register(ISiteFilter filter)
        {
            _filters.Add(filter);
            _filters.Sort((a, b) => a.Heaviness.CompareTo(b.Heaviness));
        }

        public IEnumerable<int> ApplyAll(GameState state, int totalTiles, TileDataCache tileCache)
        {
            var context = new FilterContext(state, tileCache);

            // Get settleable tiles directly from game's world grid
            var world = Find.World;
            var worldGrid = world?.grid;
            IEnumerable<int> current;
            if (worldGrid != null && worldGrid.TilesCount > 0)
            {
                current = Enumerable.Range(0, worldGrid.TilesCount)
                    .Where(id => {
                        var tile = worldGrid[id];
                        var biome = tile?.PrimaryBiome;
                        return biome != null && !biome.impassable && !world.Impassable(id);
                    });
            }
            else
            {
                current = Enumerable.Range(0, totalTiles);
            }

            foreach (var filter in _filters)
            {
                current = filter.Apply(context, current).ToList();
                if (!current.Any())
                    break;
            }

            return current;
        }

        public IReadOnlyList<ISiteFilter> Filters => _filters;

        /// <summary>
        /// Converts all registered filters to predicates and partitions by heaviness.
        /// Used for k-of-n symmetric evaluation.
        /// </summary>
        public (List<IFilterPredicate> Cheap, List<IFilterPredicate> Heavy) GetAllPredicates(GameState state)
        {
            var cheap = new List<IFilterPredicate>();
            var heavy = new List<IFilterPredicate>();

            var filters = state.Preferences.Filters;

            foreach (var filter in _filters)
            {
                // Determine importance for this filter based on its ID
                var importance = GetFilterImportance(filter.Id, filters);

                // Skip ignored filters
                if (importance == FilterImportance.Ignored)
                    continue;

                // Create predicate adapter
                var predicate = new FilterPredicateAdapter(filter, importance);

                // Partition by heaviness
                if (filter.Heaviness == FilterHeaviness.Heavy)
                    heavy.Add(predicate);
                else
                    cheap.Add(predicate); // Light and Medium are treated as cheap
            }

            return (cheap, heavy);
        }

        /// <summary>
        /// Maps filter IDs to their importance settings in FilterSettings.
        /// </summary>
        private static FilterImportance GetFilterImportance(string filterId, FilterSettings settings)
        {
            return filterId switch
            {
                // Temperature filters
                "temperature" => settings.AverageTemperatureImportance,
                "average_temperature" => settings.AverageTemperatureImportance,
                "minimum_temperature" => settings.MinimumTemperatureImportance,
                "maximum_temperature" => settings.MaximumTemperatureImportance,

                // Climate filters
                "rainfall" => settings.RainfallImportance,
                "growing_days" => settings.GrowingDaysImportance,
                "pollution" => settings.PollutionImportance,
                "forageability" => settings.ForageImportance,
                "forageable_food" => settings.ForageableFoodImportance,

                // Geography filters
                "elevation" => settings.ElevationImportance,
                "movement_difficulty" => settings.MovementDifficultyImportance,
                "coastal" => settings.CoastalImportance,
                "coastal_lake" => settings.CoastalLakeImportance,

                // Individual importance filters: Use max importance (Critical > Preferred > Ignored)
                "river" => settings.Rivers.HasCritical ? FilterImportance.Critical :
                           settings.Rivers.HasPreferred ? FilterImportance.Preferred :
                           FilterImportance.Ignored,
                "road" => settings.Roads.HasCritical ? FilterImportance.Critical :
                          settings.Roads.HasPreferred ? FilterImportance.Preferred :
                          FilterImportance.Ignored,

                // Resource filters
                "graze" => settings.GrazeImportance,
                "specific_stones" => settings.StoneImportance,
                "stone_count" => settings.UseStoneCount ? settings.StoneImportance : FilterImportance.Ignored,

                // World features
                "world_feature" => settings.FeatureImportance,
                "landmark" => settings.LandmarkImportance,
                "map_features" => settings.MapFeatures.HasCritical ? FilterImportance.Critical :
                                  settings.MapFeatures.HasPreferred ? FilterImportance.Preferred :
                                  FilterImportance.Ignored,
                "adjacent_biomes" => settings.AdjacentBiomes.HasCritical ? FilterImportance.Critical :
                                     settings.AdjacentBiomes.HasPreferred ? FilterImportance.Preferred :
                                     FilterImportance.Ignored,

                // Biome filter is always Critical if LockedBiome is set
                "biome" => settings.LockedBiome != null ? FilterImportance.Critical : FilterImportance.Ignored,

                _ => FilterImportance.Ignored
            };
        }
    }
}

#nullable enable
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
                        var tile = worldGrid?[id];
                        if (tile == null) return false;
                        var biome = tile.PrimaryBiome;
                        if (biome == null) return false;
                        return !biome.impassable && !world!.Impassable(id);
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
        /// NOTE: Heavy filters set to hard gates (MustHave/MustNotHave) are respected as-is.
        /// A user-facing warning dialog is shown before search if heavy+gate filters are detected,
        /// allowing the user to choose: Proceed, Demote to Priority, or Cancel.
        /// </summary>
        /// <param name="state">Game state containing preferences</param>
        /// <param name="overrideFilters">Optional filters to use instead of state.Preferences.GetActiveFilters() (e.g., for relaxed search)</param>
        public (List<IFilterPredicate> Cheap, List<IFilterPredicate> Heavy) GetAllPredicates(GameState state, FilterSettings? overrideFilters = null)
        {
            var cheap = new List<IFilterPredicate>();
            var heavy = new List<IFilterPredicate>();

            // Use override filters if provided (e.g., for relaxed search), otherwise use active filters
            var filters = overrideFilters ?? state.Preferences.GetActiveFilters();

            foreach (var filter in _filters)
            {
                // Determine importance for this filter based on its ID
                var importance = GetFilterImportance(filter.Id, filters);

                // Skip ignored filters
                if (!importance.IsActive())
                    continue;

                // Create predicate adapter - respect user intent, no auto-demotion
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
        /// Detects heavy filters that are set to hard gate importance levels (MustHave/MustNotHave).
        /// Used by UI to show a warning before search.
        /// </summary>
        /// <returns>List of (filterId, filterLabel, importance) for heavy+gate filters</returns>
        public List<(string Id, string Label, FilterImportance Importance)> GetHeavyGateFilters(FilterSettings filters)
        {
            var result = new List<(string, string, FilterImportance)>();

            foreach (var filter in _filters)
            {
                var importance = GetFilterImportance(filter.Id, filters);

                if (filter.Heaviness == FilterHeaviness.Heavy && importance.IsHardGate())
                {
                    string label = filter.Id switch
                    {
                        "growing_days" => "LandingZone_GrowingSeason".Translate(),
                        "graze" => "LandingZone_Grazing".Translate(),
                        "forageable_food" => "LandingZone_ForageableFood".Translate(),
                        _ => filter.Id
                    };
                    result.Add((filter.Id, label, importance));
                }
            }

            return result;
        }

        /// <summary>
        /// Maps filter IDs to their importance settings in FilterSettings.
        /// For container-based filters, returns the highest importance level present:
        /// MustHave > MustNotHave > Priority > Preferred > Ignored
        /// </summary>
        internal static FilterImportance GetFilterImportance(string filterId, FilterSettings settings)
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
                "swampiness" => settings.SwampinessImportance,

                // Wildlife & Ecology filters
                "animal_density" => settings.AnimalDensityImportance,
                "fish_population" => settings.FishPopulationImportance,
                "plant_density" => settings.PlantDensityImportance,

                // Geography filters
                "elevation" => settings.ElevationImportance,
                "movement_difficulty" => settings.MovementDifficultyImportance,
                "hilliness" => settings.AllowedHilliness.Count < 4 ? FilterImportance.MustHave : FilterImportance.Ignored,
                "coastal" => settings.CoastalImportance,
                "coastal_lake" => settings.CoastalLakeImportance,
                "water_access" => settings.WaterAccessImportance, // Coastal OR any river

                // Container-based filters: Use highest importance (MustHave > MustNotHave > Priority > Preferred > Ignored)
                "river" => GetContainerMaxImportance(settings.Rivers),
                "road" => GetContainerMaxImportance(settings.Roads),

                // Resource filters
                "graze" => settings.GrazeImportance,

                // World features
                "world_feature" => settings.FeatureImportance,
                "landmark" => settings.LandmarkImportance,
                "map_features" => GetContainerMaxImportance(settings.MapFeatures),
                "adjacent_biomes" => GetContainerMaxImportance(settings.AdjacentBiomes),
                "stone" => GetContainerMaxImportance(settings.Stones),
                "stockpile" => GetContainerMaxImportance(settings.Stockpiles),
                "mineral_ores" => GetContainerMaxImportance(settings.MineralOres),
                "plant_grove" => GetContainerMaxImportance(settings.PlantGrove),
                "animal_habitat" => GetContainerMaxImportance(settings.AnimalHabitat),

                // Biome filter: check container first, fall back to legacy LockedBiome
                "biome" => settings.Biomes.HasAnyImportance
                    ? GetContainerMaxImportance(settings.Biomes)
                    : (settings.LockedBiome != null ? FilterImportance.MustHave : FilterImportance.Ignored),

                _ => FilterImportance.Ignored
            };
        }

        /// <summary>
        /// Gets the highest importance level present in a container.
        /// Order: MustHave > MustNotHave > Priority > Preferred > Ignored
        /// </summary>
        private static FilterImportance GetContainerMaxImportance<T>(IndividualImportanceContainer<T> container) where T : notnull
        {
            if (container.HasMustHave) return FilterImportance.MustHave;
            if (container.HasMustNotHave) return FilterImportance.MustNotHave;
            if (container.HasPriority) return FilterImportance.Priority;
            if (container.HasPreferred) return FilterImportance.Preferred;
            return FilterImportance.Ignored;
        }
    }
}

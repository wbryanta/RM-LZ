using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LandingZone.Data
{
    /// <summary>
    /// Player controlled filter configuration (biomes, temperature, rainfall, etc.).
    /// Clean slate design for v0.1.0-beta - all legacy code removed.
    /// </summary>
    public sealed class FilterSettings : IExposable
    {
        public FilterSettings()
        {
            Reset();
        }

        // === CLIMATE & ENVIRONMENT FILTERS ===

        // Temperature filters (split into Average, Min, Max for more flexibility)
        public FloatRange AverageTemperatureRange { get; set; } = new FloatRange(10f, 32f);
        public FloatRange MinimumTemperatureRange { get; set; } = new FloatRange(-20f, 10f);
        public FloatRange MaximumTemperatureRange { get; set; } = new FloatRange(25f, 50f);
        public FilterImportance AverageTemperatureImportance { get; set; } = FilterImportance.Preferred;
        public FilterImportance MinimumTemperatureImportance { get; set; } = FilterImportance.Ignored;
        public FilterImportance MaximumTemperatureImportance { get; set; } = FilterImportance.Ignored;

        // Rainfall
        public FloatRange RainfallRange { get; set; } = new FloatRange(1000f, 2200f);
        public FilterImportance RainfallImportance { get; set; } = FilterImportance.Preferred;

        // Growing days
        public FloatRange GrowingDaysRange { get; set; } = new FloatRange(40f, 60f);
        public FilterImportance GrowingDaysImportance { get; set; } = FilterImportance.Preferred;

        // Pollution
        public FloatRange PollutionRange { get; set; } = new FloatRange(0f, 0.25f);
        public FilterImportance PollutionImportance { get; set; } = FilterImportance.Preferred;

        // Forageability
        public FloatRange ForageabilityRange { get; set; } = new FloatRange(0.5f, 1f);
        public FilterImportance ForageImportance { get; set; } = FilterImportance.Preferred;

        // Forageable food type (specific food selection)
        public string? ForageableFoodDefName { get; set; }
        public FilterImportance ForageableFoodImportance { get; set; } = FilterImportance.Ignored;

        // Elevation (in meters)
        public FloatRange ElevationRange { get; set; } = new FloatRange(0f, 5000f);
        public FilterImportance ElevationImportance { get; set; } = FilterImportance.Ignored;

        // Swampiness
        public FloatRange SwampinessRange { get; set; } = new FloatRange(0f, 1f);
        public FilterImportance SwampinessImportance { get; set; } = FilterImportance.Ignored;

        // Animals can graze now
        public FilterImportance GrazeImportance { get; set; } = FilterImportance.Ignored;

        // Animal density (0-6.5 range from cache dump)
        public FloatRange AnimalDensityRange { get; set; } = new FloatRange(0f, 6.5f);
        public FilterImportance AnimalDensityImportance { get; set; } = FilterImportance.Ignored;

        // Fish population (0-900 range from cache dump)
        public FloatRange FishPopulationRange { get; set; } = new FloatRange(0f, 900f);
        public FilterImportance FishPopulationImportance { get; set; } = FilterImportance.Ignored;

        // Plant density factor (0.0-1.3 range from cache dump)
        public FloatRange PlantDensityRange { get; set; } = new FloatRange(0f, 1.3f);
        public FilterImportance PlantDensityImportance { get; set; } = FilterImportance.Ignored;

        // === TERRAIN & GEOGRAPHY FILTERS ===

        // Movement difficulty
        public FloatRange MovementDifficultyRange { get; set; } = new FloatRange(0f, 2f);
        public FilterImportance MovementDifficultyImportance { get; set; } = FilterImportance.Preferred;

        // Coastal (ocean)
        public FilterImportance CoastalImportance { get; set; } = FilterImportance.Ignored;

        // Coastal (lake) - separate from ocean
        public FilterImportance CoastalLakeImportance { get; set; } = FilterImportance.Ignored;

        // Water access (coastal OR any river) - symmetric helper for water-themed presets
        public FilterImportance WaterAccessImportance { get; set; } = FilterImportance.Ignored;

        // Rivers (individual importance per river type)
        // Default to OR operator since a tile can only have one river; AND makes no sense
        public IndividualImportanceContainer<string> Rivers { get; set; } = new IndividualImportanceContainer<string> { Operator = ImportanceOperator.OR };

        // Roads (individual importance per road type)
        public IndividualImportanceContainer<string> Roads { get; set; } = new IndividualImportanceContainer<string>();

        // Stones (individual importance per stone type)
        // Default to OR operator since tiles typically have 1-2 mineral types; AND makes no sense for multiple critical stones
        public IndividualImportanceContainer<string> Stones { get; set; } = new IndividualImportanceContainer<string> { Operator = ImportanceOperator.OR };

        // Stockpiles (individual importance per stockpile type: Weapons, Medicine, Components, etc.)
        public IndividualImportanceContainer<string> Stockpiles { get; set; } = new IndividualImportanceContainer<string>();

        // Plant Grove (individual importance per plant species: Ambrosia, Healroot, etc.)
        // Default to OR operator since tiles typically have one grove type
        public IndividualImportanceContainer<string> PlantGrove { get; set; } = new IndividualImportanceContainer<string> { Operator = ImportanceOperator.OR };

        // Animal Habitat (individual importance per animal species: Thrumbo, Megasloth, etc.)
        // Default to OR operator since tiles typically have one flagship animal
        public IndividualImportanceContainer<string> AnimalHabitat { get; set; } = new IndividualImportanceContainer<string> { Operator = ImportanceOperator.OR };

        // Mineral Rich ores (individual importance per ore type: Steel, Plasteel, Uranium, etc.)
        // Default to OR operator since wanting any of the specified ores is typical
        public IndividualImportanceContainer<string> MineralOres { get; set; } = new IndividualImportanceContainer<string> { Operator = ImportanceOperator.OR };

        // Stone count filter ("any X stone types") - alternative mode
        public FloatRange StoneCountRange { get; set; } = new FloatRange(2f, 3f);
        public bool UseStoneCount { get; set; } = false;

        // Hilliness (multi-select)
        public HashSet<Hilliness> AllowedHilliness { get; } = new HashSet<Hilliness>
        {
            Hilliness.SmallHills,
            Hilliness.LargeHills,
            Hilliness.Mountainous
        };

        // === WORLD & FEATURES FILTERS ===

        // Biomes (individual importance per biome type)
        // Default to OR operator since a tile can only have one biome; AND makes no sense
        public IndividualImportanceContainer<string> Biomes { get; set; } = new IndividualImportanceContainer<string> { Operator = ImportanceOperator.OR };

        // Biome lock (legacy single selection - deprecated, use Biomes container instead)
        public BiomeDef? LockedBiome { get; set; }

        // Map features (individual importance per feature: Caves, Ruins, MixedBiome, etc.)
        public IndividualImportanceContainer<string> MapFeatures { get; set; } = new IndividualImportanceContainer<string>();

        // Adjacent biomes (individual importance per biome)
        public IndividualImportanceContainer<string> AdjacentBiomes { get; set; } = new IndividualImportanceContainer<string>();

        // World feature (legacy single selection - may remove in future)
        public string? RequiredFeatureDefName { get; set; }
        public FilterImportance FeatureImportance { get; set; } = FilterImportance.Ignored;

        // Has landmark (tiles with proper names)
        public FilterImportance LandmarkImportance { get; set; } = FilterImportance.Ignored;

        // === RESULTS CONTROL ===

        public const int DefaultMaxResults = 25;
        public const int MinMaxResults = 10;
        public const int MaxResultsLimit = 100;

        private int _maxResults = DefaultMaxResults;
        public int MaxResults
        {
            get => _maxResults;
            set => _maxResults = Mathf.Clamp(value, MinMaxResults, MaxResultsLimit);
        }

        // === ADVANCED MATCHING CONTROLS ===

        /// <summary>
        /// Critical strictness: fraction of critical filters a tile must match (k-of-n).
        /// 1.0 = all criticals required (strict)
        /// 0.8 = match 4 of 5 criticals (relaxed)
        /// 0.6 = match 3 of 5 criticals (very relaxed)
        /// Range: [0.0, 1.0]
        /// </summary>
        private float _criticalStrictness = 1.0f;
        public float CriticalStrictness
        {
            get => _criticalStrictness;
            set => _criticalStrictness = Mathf.Clamp01(value);
        }

        public void Reset()
        {
            // Climate & Environment
            AverageTemperatureRange = new FloatRange(10f, 32f);
            MinimumTemperatureRange = new FloatRange(-20f, 10f);
            MaximumTemperatureRange = new FloatRange(25f, 50f);
            AverageTemperatureImportance = FilterImportance.Preferred;
            MinimumTemperatureImportance = FilterImportance.Ignored;
            MaximumTemperatureImportance = FilterImportance.Ignored;

            RainfallRange = new FloatRange(1000f, 2200f);
            RainfallImportance = FilterImportance.Preferred;

            GrowingDaysRange = new FloatRange(40f, 60f);
            GrowingDaysImportance = FilterImportance.Preferred;

            PollutionRange = new FloatRange(0f, 0.25f);
            PollutionImportance = FilterImportance.Preferred;

            ForageabilityRange = new FloatRange(0.5f, 1f);
            ForageImportance = FilterImportance.Preferred;

            ForageableFoodDefName = null;
            ForageableFoodImportance = FilterImportance.Ignored;

            ElevationRange = new FloatRange(0f, 5000f);
            ElevationImportance = FilterImportance.Ignored;

            SwampinessRange = new FloatRange(0f, 1f);
            SwampinessImportance = FilterImportance.Ignored;

            GrazeImportance = FilterImportance.Ignored;

            AnimalDensityRange = new FloatRange(0f, 6.5f);
            AnimalDensityImportance = FilterImportance.Ignored;

            FishPopulationRange = new FloatRange(0f, 900f);
            FishPopulationImportance = FilterImportance.Ignored;

            PlantDensityRange = new FloatRange(0f, 1.3f);
            PlantDensityImportance = FilterImportance.Ignored;

            // Terrain & Geography
            MovementDifficultyRange = new FloatRange(0f, 2f);
            MovementDifficultyImportance = FilterImportance.Preferred;

            CoastalImportance = FilterImportance.Ignored;
            CoastalLakeImportance = FilterImportance.Ignored;
            WaterAccessImportance = FilterImportance.Ignored;

            Rivers.Reset();
            Roads.Reset();
            Stones.Reset();

            StoneCountRange = new FloatRange(2f, 3f);
            UseStoneCount = false;

            AllowedHilliness.Clear();
            AllowedHilliness.Add(Hilliness.SmallHills);
            AllowedHilliness.Add(Hilliness.LargeHills);
            AllowedHilliness.Add(Hilliness.Mountainous);

            // World & Features
            Biomes.Reset();
            LockedBiome = null;

            MapFeatures.Reset();
            AdjacentBiomes.Reset();

            RequiredFeatureDefName = null;
            FeatureImportance = FilterImportance.Ignored;

            LandmarkImportance = FilterImportance.Ignored;

            // Results
            MaxResults = DefaultMaxResults;

            // Advanced Matching
            // Note: With membership scoring, strictness should be 0.0 to allow fuzzy matching.
            // The scoring system ranks tiles continuously rather than binary pass/fail.
            // For k-of-n scoring (legacy), 1.0 means all critical filters must match.
            CriticalStrictness = 0.0f;
        }

        /// <summary>
        /// Clears all filters to Ignored (no filtering active).
        /// Unlike Reset(), this sets everything to Ignored rather than comfortable defaults.
        /// </summary>
        public void ClearAll()
        {
            // Climate & Environment - set all importance to Ignored
            AverageTemperatureImportance = FilterImportance.Ignored;
            MinimumTemperatureImportance = FilterImportance.Ignored;
            MaximumTemperatureImportance = FilterImportance.Ignored;
            RainfallImportance = FilterImportance.Ignored;
            GrowingDaysImportance = FilterImportance.Ignored;
            PollutionImportance = FilterImportance.Ignored;
            ForageImportance = FilterImportance.Ignored;
            ForageableFoodImportance = FilterImportance.Ignored;
            ElevationImportance = FilterImportance.Ignored;
            SwampinessImportance = FilterImportance.Ignored;
            GrazeImportance = FilterImportance.Ignored;
            AnimalDensityImportance = FilterImportance.Ignored;
            FishPopulationImportance = FilterImportance.Ignored;
            PlantDensityImportance = FilterImportance.Ignored;

            // Terrain & Geography
            MovementDifficultyImportance = FilterImportance.Ignored;
            CoastalImportance = FilterImportance.Ignored;
            CoastalLakeImportance = FilterImportance.Ignored;
            WaterAccessImportance = FilterImportance.Ignored;

            // Clear all individual importance containers
            Rivers.Reset();
            Roads.Reset();
            Stones.Reset();
            Stockpiles.Reset();
            PlantGrove.Reset();
            AnimalHabitat.Reset();
            MineralOres.Reset();
            Biomes.Reset();
            MapFeatures.Reset();
            AdjacentBiomes.Reset();

            // Reset hilliness to allow all types (no filtering)
            AllowedHilliness.Clear();
            AllowedHilliness.Add(Hilliness.Flat);
            AllowedHilliness.Add(Hilliness.SmallHills);
            AllowedHilliness.Add(Hilliness.LargeHills);
            AllowedHilliness.Add(Hilliness.Mountainous);

            // World & Features
            LockedBiome = null;
            RequiredFeatureDefName = null;
            FeatureImportance = FilterImportance.Ignored;
            LandmarkImportance = FilterImportance.Ignored;
            UseStoneCount = false;

            // Keep results settings and strictness at defaults
            MaxResults = DefaultMaxResults;
            CriticalStrictness = 0.0f;
        }

        /// <summary>
        /// Demotes all MustHave gates to Priority for relaxed search.
        /// MustNotHave gates remain unchanged (they exclude unwanted tiles).
        /// This allows the car-builder fallback pattern to show closest matches
        /// that don't meet all requirements.
        /// </summary>
        public void RelaxMustHaveGates()
        {
            // Demote single-value MustHave filters to Priority
            if (AverageTemperatureImportance == FilterImportance.MustHave)
                AverageTemperatureImportance = FilterImportance.Priority;
            if (MinimumTemperatureImportance == FilterImportance.MustHave)
                MinimumTemperatureImportance = FilterImportance.Priority;
            if (MaximumTemperatureImportance == FilterImportance.MustHave)
                MaximumTemperatureImportance = FilterImportance.Priority;
            if (GrowingDaysImportance == FilterImportance.MustHave)
                GrowingDaysImportance = FilterImportance.Priority;
            if (PollutionImportance == FilterImportance.MustHave)
                PollutionImportance = FilterImportance.Priority;
            if (ForageImportance == FilterImportance.MustHave)
                ForageImportance = FilterImportance.Priority;
            if (ForageableFoodImportance == FilterImportance.MustHave)
                ForageableFoodImportance = FilterImportance.Priority;
            if (ElevationImportance == FilterImportance.MustHave)
                ElevationImportance = FilterImportance.Priority;
            if (SwampinessImportance == FilterImportance.MustHave)
                SwampinessImportance = FilterImportance.Priority;
            if (GrazeImportance == FilterImportance.MustHave)
                GrazeImportance = FilterImportance.Priority;
            if (AnimalDensityImportance == FilterImportance.MustHave)
                AnimalDensityImportance = FilterImportance.Priority;
            if (FishPopulationImportance == FilterImportance.MustHave)
                FishPopulationImportance = FilterImportance.Priority;
            if (PlantDensityImportance == FilterImportance.MustHave)
                PlantDensityImportance = FilterImportance.Priority;
            if (MovementDifficultyImportance == FilterImportance.MustHave)
                MovementDifficultyImportance = FilterImportance.Priority;
            if (CoastalImportance == FilterImportance.MustHave)
                CoastalImportance = FilterImportance.Priority;
            if (CoastalLakeImportance == FilterImportance.MustHave)
                CoastalLakeImportance = FilterImportance.Priority;
            if (WaterAccessImportance == FilterImportance.MustHave)
                WaterAccessImportance = FilterImportance.Priority;
            if (FeatureImportance == FilterImportance.MustHave)
                FeatureImportance = FilterImportance.Priority;
            if (LandmarkImportance == FilterImportance.MustHave)
                LandmarkImportance = FilterImportance.Priority;

            // Demote MustHave items in container filters to Priority
            Rivers.RelaxMustHaveToPriority();
            Roads.RelaxMustHaveToPriority();
            Stones.RelaxMustHaveToPriority();
            Stockpiles.RelaxMustHaveToPriority();
            PlantGrove.RelaxMustHaveToPriority();
            AnimalHabitat.RelaxMustHaveToPriority();
            MineralOres.RelaxMustHaveToPriority();
            Biomes.RelaxMustHaveToPriority();
            MapFeatures.RelaxMustHaveToPriority();
            AdjacentBiomes.RelaxMustHaveToPriority();
        }

        /// <summary>
        /// Extracts all MustHave and MustNotHave requirements from the current filter settings.
        /// Used to snapshot original requirements before relaxed search.
        /// </summary>
        public List<OriginalRequirement> GetOriginalRequirements()
        {
            var requirements = new List<OriginalRequirement>();

            // Single-value filters - check for MustHave or MustNotHave
            void AddIfGate(FilterImportance importance, string filterId, string displayName)
            {
                if (importance == FilterImportance.MustHave)
                    requirements.Add(new OriginalRequirement(filterId, displayName, isMustNotHave: false));
                else if (importance == FilterImportance.MustNotHave)
                    requirements.Add(new OriginalRequirement(filterId, displayName, isMustNotHave: true));
            }

            AddIfGate(AverageTemperatureImportance, "avg_temp", "LandingZone_FilterAverageTemperature".Translate());
            AddIfGate(MinimumTemperatureImportance, "min_temp", "LandingZone_FilterMinimumTemperature".Translate());
            AddIfGate(MaximumTemperatureImportance, "max_temp", "LandingZone_FilterMaximumTemperature".Translate());
            AddIfGate(GrowingDaysImportance, "growing_days", "LandingZone_FilterGrowingDays".Translate());
            AddIfGate(PollutionImportance, "pollution", "LandingZone_FilterPollution".Translate());
            AddIfGate(ForageImportance, "forage", "LandingZone_FilterForageability".Translate());
            AddIfGate(ForageableFoodImportance, "forageable_food", "LandingZone_FilterForageableFood".Translate());
            AddIfGate(ElevationImportance, "elevation", "LandingZone_FilterElevation".Translate());
            AddIfGate(SwampinessImportance, "swampiness", "LandingZone_FilterSwampiness".Translate());
            AddIfGate(GrazeImportance, "graze", "LandingZone_FilterGrazeability".Translate());
            AddIfGate(AnimalDensityImportance, "animal_density", "LandingZone_FilterAnimalDensity".Translate());
            AddIfGate(FishPopulationImportance, "fish_population", "LandingZone_FilterFishPopulation".Translate());
            AddIfGate(PlantDensityImportance, "plant_density", "LandingZone_FilterPlantDensity".Translate());
            AddIfGate(MovementDifficultyImportance, "movement", "LandingZone_FilterMovementDifficulty".Translate());
            AddIfGate(CoastalImportance, "coastal", "LandingZone_FilterCoastal".Translate());
            AddIfGate(CoastalLakeImportance, "coastal_lake", "LandingZone_FilterCoastalLake".Translate());
            AddIfGate(WaterAccessImportance, "water_access", "LandingZone_FilterWaterAccess".Translate());
            AddIfGate(FeatureImportance, "feature", "LandingZone_FilterFeature".Translate());
            AddIfGate(LandmarkImportance, "landmark", "LandingZone_FilterLandmark".Translate());

            // Container filters - extract individual MustHave/MustNotHave items
            foreach (var item in Rivers.GetMustHaveItems())
                requirements.Add(new OriginalRequirement($"river:{item}", item, isMustNotHave: false));
            foreach (var item in Rivers.GetMustNotHaveItems())
                requirements.Add(new OriginalRequirement($"river:{item}", item, isMustNotHave: true));

            foreach (var item in Roads.GetMustHaveItems())
                requirements.Add(new OriginalRequirement($"road:{item}", item, isMustNotHave: false));
            foreach (var item in Roads.GetMustNotHaveItems())
                requirements.Add(new OriginalRequirement($"road:{item}", item, isMustNotHave: true));

            foreach (var item in Stones.GetMustHaveItems())
                requirements.Add(new OriginalRequirement($"stone:{item}", item, isMustNotHave: false));
            foreach (var item in Stones.GetMustNotHaveItems())
                requirements.Add(new OriginalRequirement($"stone:{item}", item, isMustNotHave: true));

            foreach (var item in MapFeatures.GetMustHaveItems())
                requirements.Add(new OriginalRequirement($"feature:{item}", item, isMustNotHave: false));
            foreach (var item in MapFeatures.GetMustNotHaveItems())
                requirements.Add(new OriginalRequirement($"feature:{item}", item, isMustNotHave: true));

            foreach (var item in PlantGrove.GetMustHaveItems())
                requirements.Add(new OriginalRequirement($"grove:{item}", item, isMustNotHave: false));
            foreach (var item in PlantGrove.GetMustNotHaveItems())
                requirements.Add(new OriginalRequirement($"grove:{item}", item, isMustNotHave: true));

            foreach (var item in AnimalHabitat.GetMustHaveItems())
                requirements.Add(new OriginalRequirement($"habitat:{item}", item, isMustNotHave: false));
            foreach (var item in AnimalHabitat.GetMustNotHaveItems())
                requirements.Add(new OriginalRequirement($"habitat:{item}", item, isMustNotHave: true));

            foreach (var item in MineralOres.GetMustHaveItems())
                requirements.Add(new OriginalRequirement($"ore:{item}", item, isMustNotHave: false));
            foreach (var item in MineralOres.GetMustNotHaveItems())
                requirements.Add(new OriginalRequirement($"ore:{item}", item, isMustNotHave: true));

            foreach (var item in Stockpiles.GetMustHaveItems())
                requirements.Add(new OriginalRequirement($"stockpile:{item}", item, isMustNotHave: false));
            foreach (var item in Stockpiles.GetMustNotHaveItems())
                requirements.Add(new OriginalRequirement($"stockpile:{item}", item, isMustNotHave: true));

            foreach (var item in AdjacentBiomes.GetMustHaveItems())
                requirements.Add(new OriginalRequirement($"adjacent:{item}", item, isMustNotHave: false));
            foreach (var item in AdjacentBiomes.GetMustNotHaveItems())
                requirements.Add(new OriginalRequirement($"adjacent:{item}", item, isMustNotHave: true));

            foreach (var item in Biomes.GetMustHaveItems())
                requirements.Add(new OriginalRequirement($"biome:{item}", item, isMustNotHave: false));
            foreach (var item in Biomes.GetMustNotHaveItems())
                requirements.Add(new OriginalRequirement($"biome:{item}", item, isMustNotHave: true));

            return requirements;
        }

        /// <summary>
        /// Creates a deep copy of these filter settings.
        /// Used by relaxed search to avoid mutating user's original filters.
        /// </summary>
        public FilterSettings Clone()
        {
            var clone = new FilterSettings();
            clone.CopyFrom(this);
            return clone;
        }

        /// <summary>
        /// Copies all filter settings from another FilterSettings instance.
        /// Performs deep copy of containers and collections.
        /// </summary>
        public void CopyFrom(FilterSettings source)
        {
            if (source == null) return;

            // Climate & Environment
            AverageTemperatureRange = source.AverageTemperatureRange;
            MinimumTemperatureRange = source.MinimumTemperatureRange;
            MaximumTemperatureRange = source.MaximumTemperatureRange;
            AverageTemperatureImportance = source.AverageTemperatureImportance;
            MinimumTemperatureImportance = source.MinimumTemperatureImportance;
            MaximumTemperatureImportance = source.MaximumTemperatureImportance;

            RainfallRange = source.RainfallRange;
            RainfallImportance = source.RainfallImportance;

            GrowingDaysRange = source.GrowingDaysRange;
            GrowingDaysImportance = source.GrowingDaysImportance;

            PollutionRange = source.PollutionRange;
            PollutionImportance = source.PollutionImportance;

            ForageabilityRange = source.ForageabilityRange;
            ForageImportance = source.ForageImportance;

            ForageableFoodDefName = source.ForageableFoodDefName;
            ForageableFoodImportance = source.ForageableFoodImportance;

            ElevationRange = source.ElevationRange;
            ElevationImportance = source.ElevationImportance;

            SwampinessRange = source.SwampinessRange;
            SwampinessImportance = source.SwampinessImportance;

            GrazeImportance = source.GrazeImportance;

            AnimalDensityRange = source.AnimalDensityRange;
            AnimalDensityImportance = source.AnimalDensityImportance;

            FishPopulationRange = source.FishPopulationRange;
            FishPopulationImportance = source.FishPopulationImportance;

            PlantDensityRange = source.PlantDensityRange;
            PlantDensityImportance = source.PlantDensityImportance;

            // Terrain & Geography
            MovementDifficultyRange = source.MovementDifficultyRange;
            MovementDifficultyImportance = source.MovementDifficultyImportance;

            CoastalImportance = source.CoastalImportance;
            CoastalLakeImportance = source.CoastalLakeImportance;
            WaterAccessImportance = source.WaterAccessImportance;

            // Deep copy containers
            Rivers = source.Rivers.Clone();
            Roads = source.Roads.Clone();
            Stones = source.Stones.Clone();
            Stockpiles = source.Stockpiles.Clone();
            PlantGrove = source.PlantGrove.Clone();
            AnimalHabitat = source.AnimalHabitat.Clone();
            MineralOres = source.MineralOres.Clone();
            Biomes = source.Biomes.Clone();
            MapFeatures = source.MapFeatures.Clone();
            AdjacentBiomes = source.AdjacentBiomes.Clone();

            StoneCountRange = source.StoneCountRange;
            UseStoneCount = source.UseStoneCount;

            // Deep copy hilliness set
            AllowedHilliness.Clear();
            foreach (var h in source.AllowedHilliness)
                AllowedHilliness.Add(h);

            // World & Features
            LockedBiome = source.LockedBiome;

            RequiredFeatureDefName = source.RequiredFeatureDefName;
            FeatureImportance = source.FeatureImportance;

            LandmarkImportance = source.LandmarkImportance;

            // Results Control
            MaxResults = source.MaxResults;

            // Advanced Matching
            CriticalStrictness = source.CriticalStrictness;
        }

        /// <summary>
        /// Serialization support for RimWorld save/load system.
        /// Serializes ALL 56 properties for complete save/load support.
        /// </summary>
        public void ExposeData()
        {
            // === CLIMATE & ENVIRONMENT FILTERS (27 properties) ===

            // Temperature filters (6 properties)
            var avgTempRange = AverageTemperatureRange;
            var minTempRange = MinimumTemperatureRange;
            var maxTempRange = MaximumTemperatureRange;
            var avgTempImp = AverageTemperatureImportance;
            var minTempImp = MinimumTemperatureImportance;
            var maxTempImp = MaximumTemperatureImportance;

            Scribe_Values.Look(ref avgTempRange, "averageTemperatureRange", new FloatRange(10f, 32f));
            Scribe_Values.Look(ref minTempRange, "minimumTemperatureRange", new FloatRange(-20f, 10f));
            Scribe_Values.Look(ref maxTempRange, "maximumTemperatureRange", new FloatRange(25f, 50f));
            Scribe_Values.Look(ref avgTempImp, "averageTemperatureImportance", FilterImportance.Preferred);
            Scribe_Values.Look(ref minTempImp, "minimumTemperatureImportance", FilterImportance.Ignored);
            Scribe_Values.Look(ref maxTempImp, "maximumTemperatureImportance", FilterImportance.Ignored);

            // Rainfall (2 properties)
            var rainfallRange = RainfallRange;
            var rainfallImp = RainfallImportance;

            Scribe_Values.Look(ref rainfallRange, "rainfallRange", new FloatRange(1000f, 2200f));
            Scribe_Values.Look(ref rainfallImp, "rainfallImportance", FilterImportance.Preferred);

            // Growing days (2 properties)
            var growingDaysRange = GrowingDaysRange;
            var growingDaysImp = GrowingDaysImportance;

            Scribe_Values.Look(ref growingDaysRange, "growingDaysRange", new FloatRange(40f, 60f));
            Scribe_Values.Look(ref growingDaysImp, "growingDaysImportance", FilterImportance.Preferred);

            // Pollution (2 properties)
            var pollutionRange = PollutionRange;
            var pollutionImp = PollutionImportance;

            Scribe_Values.Look(ref pollutionRange, "pollutionRange", new FloatRange(0f, 0.25f));
            Scribe_Values.Look(ref pollutionImp, "pollutionImportance", FilterImportance.Preferred);

            // Forageability (2 properties)
            var forageRange = ForageabilityRange;
            var forageImp = ForageImportance;

            Scribe_Values.Look(ref forageRange, "forageabilityRange", new FloatRange(0.5f, 1f));
            Scribe_Values.Look(ref forageImp, "forageImportance", FilterImportance.Preferred);

            // Forageable food (2 properties)
            var forageFood = ForageableFoodDefName;
            var forageFoodImp = ForageableFoodImportance;

            Scribe_Values.Look(ref forageFood, "forageableFoodDefName", null);
            Scribe_Values.Look(ref forageFoodImp, "forageableFoodImportance", FilterImportance.Ignored);

            // Elevation (2 properties)
            var elevationRange = ElevationRange;
            var elevationImp = ElevationImportance;

            Scribe_Values.Look(ref elevationRange, "elevationRange", new FloatRange(0f, 5000f));
            Scribe_Values.Look(ref elevationImp, "elevationImportance", FilterImportance.Ignored);

            // Swampiness (2 properties)
            var swampinessRange = SwampinessRange;
            var swampinessImp = SwampinessImportance;

            Scribe_Values.Look(ref swampinessRange, "swampinessRange", new FloatRange(0f, 1f));
            Scribe_Values.Look(ref swampinessImp, "swampinessImportance", FilterImportance.Ignored);

            // Graze (1 property)
            var grazeImp = GrazeImportance;
            Scribe_Values.Look(ref grazeImp, "grazeImportance", FilterImportance.Ignored);

            // Animal density (2 properties)
            var animalDensityRange = AnimalDensityRange;
            var animalDensityImp = AnimalDensityImportance;

            Scribe_Values.Look(ref animalDensityRange, "animalDensityRange", new FloatRange(0f, 6.5f));
            Scribe_Values.Look(ref animalDensityImp, "animalDensityImportance", FilterImportance.Ignored);

            // Fish population (2 properties)
            var fishPopRange = FishPopulationRange;
            var fishPopImp = FishPopulationImportance;

            Scribe_Values.Look(ref fishPopRange, "fishPopulationRange", new FloatRange(0f, 900f));
            Scribe_Values.Look(ref fishPopImp, "fishPopulationImportance", FilterImportance.Ignored);

            // Plant density (2 properties)
            var plantDensityRange = PlantDensityRange;
            var plantDensityImp = PlantDensityImportance;

            Scribe_Values.Look(ref plantDensityRange, "plantDensityRange", new FloatRange(0f, 1.3f));
            Scribe_Values.Look(ref plantDensityImp, "plantDensityImportance", FilterImportance.Ignored);

            // === TERRAIN & GEOGRAPHY FILTERS (14 properties) ===

            // Movement difficulty (2 properties)
            var movementRange = MovementDifficultyRange;
            var movementImp = MovementDifficultyImportance;

            Scribe_Values.Look(ref movementRange, "movementDifficultyRange", new FloatRange(0f, 2f));
            Scribe_Values.Look(ref movementImp, "movementDifficultyImportance", FilterImportance.Preferred);

            // Coastal (1 property)
            var coastalImp = CoastalImportance;
            Scribe_Values.Look(ref coastalImp, "coastalImportance", FilterImportance.Ignored);

            // Coastal lake (1 property)
            var coastalLakeImp = CoastalLakeImportance;
            Scribe_Values.Look(ref coastalLakeImp, "coastalLakeImportance", FilterImportance.Ignored);

            // Water access (1 property)
            var waterAccessImp = WaterAccessImportance;
            Scribe_Values.Look(ref waterAccessImp, "waterAccessImportance", FilterImportance.Ignored);

            // Rivers (1 property - IndividualImportanceContainer)
            var rivers = Rivers;
            Scribe_Deep.Look(ref rivers, "rivers");

            // Roads (1 property - IndividualImportanceContainer)
            var roads = Roads;
            Scribe_Deep.Look(ref roads, "roads");

            // Stones (1 property - IndividualImportanceContainer)
            var stones = Stones;
            Scribe_Deep.Look(ref stones, "stones");

            // Stockpiles (1 property - IndividualImportanceContainer)
            var stockpiles = Stockpiles;
            Scribe_Deep.Look(ref stockpiles, "stockpiles");

            // Plant Grove (1 property - IndividualImportanceContainer)
            var plantGrove = PlantGrove;
            Scribe_Deep.Look(ref plantGrove, "plantGrove");

            // Animal Habitat (1 property - IndividualImportanceContainer)
            var animalHabitat = AnimalHabitat;
            Scribe_Deep.Look(ref animalHabitat, "animalHabitat");

            // Mineral Ores (1 property - IndividualImportanceContainer)
            var mineralOres = MineralOres;
            Scribe_Deep.Look(ref mineralOres, "mineralOres");

            // Stone count (2 properties)
            var stoneCountRange = StoneCountRange;
            var useStoneCount = UseStoneCount;

            Scribe_Values.Look(ref stoneCountRange, "stoneCountRange", new FloatRange(2f, 3f));
            Scribe_Values.Look(ref useStoneCount, "useStoneCount", false);

            // Hilliness (1 property - HashSet<Hilliness>, needs special handling)
            List<Hilliness>? hillinessListForSave = null;
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                hillinessListForSave = AllowedHilliness.ToList();
            }

            Scribe_Collections.Look(ref hillinessListForSave, "allowedHilliness", LookMode.Value);

            // === WORLD & FEATURES FILTERS (9 properties) ===

            // Biomes (1 property - IndividualImportanceContainer)
            var biomes = Biomes;
            Scribe_Deep.Look(ref biomes, "biomes");

            // Locked biome (1 property - Def reference) - legacy, deprecated
            var lockedBiome = LockedBiome;
            Scribe_Defs.Look(ref lockedBiome, "lockedBiome");

            // Map features (1 property - IndividualImportanceContainer)
            var mapFeatures = MapFeatures;
            Scribe_Deep.Look(ref mapFeatures, "mapFeatures");

            // Adjacent biomes (1 property - IndividualImportanceContainer)
            var adjacentBiomes = AdjacentBiomes;
            Scribe_Deep.Look(ref adjacentBiomes, "adjacentBiomes");

            // Required feature (2 properties)
            var requiredFeature = RequiredFeatureDefName;
            var featureImp = FeatureImportance;

            Scribe_Values.Look(ref requiredFeature, "requiredFeatureDefName", null);
            Scribe_Values.Look(ref featureImp, "featureImportance", FilterImportance.Ignored);

            // Landmark (1 property)
            var landmarkImp = LandmarkImportance;
            Scribe_Values.Look(ref landmarkImp, "landmarkImportance", FilterImportance.Ignored);

            // === RESULTS CONTROL (1 property) ===

            // MaxResults (backing field _maxResults)
            int maxResults = _maxResults;
            Scribe_Values.Look(ref maxResults, "maxResults", DefaultMaxResults);

            // === ADVANCED MATCHING (1 property) ===

            // CriticalStrictness (backing field _criticalStrictness)
            float criticalStrictness = _criticalStrictness;
            Scribe_Values.Look(ref criticalStrictness, "criticalStrictness", 0.0f);

            // === WRITE BACK TO PROPERTIES AFTER LOADING ===

            if (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // Climate & Environment (27 properties)
                AverageTemperatureRange = avgTempRange;
                MinimumTemperatureRange = minTempRange;
                MaximumTemperatureRange = maxTempRange;
                AverageTemperatureImportance = avgTempImp;
                MinimumTemperatureImportance = minTempImp;
                MaximumTemperatureImportance = maxTempImp;

                RainfallRange = rainfallRange;
                RainfallImportance = rainfallImp;

                GrowingDaysRange = growingDaysRange;
                GrowingDaysImportance = growingDaysImp;

                PollutionRange = pollutionRange;
                PollutionImportance = pollutionImp;

                ForageabilityRange = forageRange;
                ForageImportance = forageImp;

                ForageableFoodDefName = forageFood;  // Can be null
                ForageableFoodImportance = forageFoodImp;

                ElevationRange = elevationRange;
                ElevationImportance = elevationImp;

                SwampinessRange = swampinessRange;
                SwampinessImportance = swampinessImp;

                GrazeImportance = grazeImp;

                AnimalDensityRange = animalDensityRange;
                AnimalDensityImportance = animalDensityImp;

                FishPopulationRange = fishPopRange;
                FishPopulationImportance = fishPopImp;

                PlantDensityRange = plantDensityRange;
                PlantDensityImportance = plantDensityImp;

                // Terrain & Geography (14 properties)
                MovementDifficultyRange = movementRange;
                MovementDifficultyImportance = movementImp;

                CoastalImportance = coastalImp;
                CoastalLakeImportance = coastalLakeImp;
                WaterAccessImportance = waterAccessImp;

                Rivers = rivers ?? new IndividualImportanceContainer<string>();
                Roads = roads ?? new IndividualImportanceContainer<string>();
                Stones = stones ?? new IndividualImportanceContainer<string>();
                Stockpiles = stockpiles ?? new IndividualImportanceContainer<string>();
                PlantGrove = plantGrove ?? new IndividualImportanceContainer<string>();
                AnimalHabitat = animalHabitat ?? new IndividualImportanceContainer<string>();
                MineralOres = mineralOres ?? new IndividualImportanceContainer<string>();

                StoneCountRange = stoneCountRange;
                UseStoneCount = useStoneCount;

                // Hilliness - restore from list
                AllowedHilliness.Clear();
                if (hillinessListForSave != null && hillinessListForSave.Count > 0)
                {
                    foreach (var h in hillinessListForSave)
                        AllowedHilliness.Add(h);
                }
                else
                {
                    // Default values from Reset()
                    AllowedHilliness.Add(Hilliness.SmallHills);
                    AllowedHilliness.Add(Hilliness.LargeHills);
                    AllowedHilliness.Add(Hilliness.Mountainous);
                }

                // World & Features (9 properties)
                Biomes = biomes ?? new IndividualImportanceContainer<string>();
                LockedBiome = lockedBiome;  // Can be null (legacy)

                MapFeatures = mapFeatures ?? new IndividualImportanceContainer<string>();
                AdjacentBiomes = adjacentBiomes ?? new IndividualImportanceContainer<string>();

                RequiredFeatureDefName = requiredFeature;  // Can be null
                FeatureImportance = featureImp;

                LandmarkImportance = landmarkImp;

                // Results Control (1 property) - use property setter for clamping
                _maxResults = Mathf.Clamp(maxResults, MinMaxResults, MaxResultsLimit);

                // Advanced Matching (1 property) - use property setter for clamping
                _criticalStrictness = Mathf.Clamp01(criticalStrictness);
            }
        }
    }

    /// <summary>
    /// 5-state importance model for filter configuration.
    /// - MustHave/MustNotHave: Hard gates (tile MUST/MUST NOT match) - Apply phase only
    /// - Priority: Higher scoring weight than Preferred
    /// - Preferred: Normal scoring weight
    /// - Ignored: Not evaluated
    /// </summary>
    public enum FilterImportance : byte
    {
        /// <summary>Not evaluated - filter is off.</summary>
        Ignored = 0,

        /// <summary>Normal scoring weight.</summary>
        Preferred = 1,

        /// <summary>Higher scoring weight than Preferred.</summary>
        Priority = 2,

        /// <summary>Hard gate: tile MUST match this filter to be considered.</summary>
        MustHave = 3,

        /// <summary>Hard gate: tile MUST NOT match this filter to be considered.</summary>
        MustNotHave = 4,

        /// <summary>[LEGACY] Alias for MustHave. Use MustHave for new code.</summary>
        Critical = MustHave
    }

    /// <summary>
    /// Extension methods for FilterImportance to simplify gate/scoring logic.
    /// </summary>
    public static class FilterImportanceExtensions
    {
        /// <summary>
        /// Returns true if this importance level is a hard gate (MustHave or MustNotHave).
        /// Hard gates are evaluated in the Apply phase and exclude/include tiles absolutely.
        /// </summary>
        public static bool IsHardGate(this FilterImportance importance)
        {
            return importance == FilterImportance.MustHave || importance == FilterImportance.MustNotHave;
        }

        /// <summary>
        /// Returns true if this importance level contributes to scoring (Priority or Preferred).
        /// Scoring filters are evaluated in the Score phase and affect tile ranking.
        /// </summary>
        public static bool IsScoring(this FilterImportance importance)
        {
            return importance == FilterImportance.Priority || importance == FilterImportance.Preferred;
        }

        /// <summary>
        /// Returns true if this importance level should be evaluated at all.
        /// </summary>
        public static bool IsActive(this FilterImportance importance)
        {
            return importance != FilterImportance.Ignored;
        }

        /// <summary>
        /// Migrates old 3-state importance values to new 5-state model.
        /// Critical (2) → MustHave (3)
        /// Preferred (1) → Preferred (1)
        /// Ignored (0) → Ignored (0)
        /// </summary>
        public static FilterImportance MigrateFromLegacy(byte legacyValue)
        {
            return legacyValue switch
            {
                2 => FilterImportance.MustHave,  // Critical → MustHave
                1 => FilterImportance.Preferred,
                _ => FilterImportance.Ignored
            };
        }
    }
}

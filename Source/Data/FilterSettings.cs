using System.Collections.Generic;
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
    public sealed class FilterSettings
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

        // Rivers (individual importance per river type)
        public IndividualImportanceContainer<string> Rivers { get; set; } = new IndividualImportanceContainer<string>();

        // Roads (individual importance per road type)
        public IndividualImportanceContainer<string> Roads { get; set; } = new IndividualImportanceContainer<string>();

        // Stones (individual importance per stone type)
        public IndividualImportanceContainer<string> Stones { get; set; } = new IndividualImportanceContainer<string>();

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

        // Biome lock
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

        public const int DefaultMaxResults = 20;
        public const int MaxResultsLimit = 25;

        private int _maxResults = DefaultMaxResults;
        public int MaxResults
        {
            get => _maxResults;
            set => _maxResults = Mathf.Clamp(value, 1, MaxResultsLimit);
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
    }

    public enum FilterImportance : byte
    {
        Ignored = 0,
        Preferred = 1,
        Critical = 2
    }
}

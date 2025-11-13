using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LandingZone.Data
{
    /// <summary>
    /// Player controlled filter configuration (biomes, temperature, rainfall, etc.).
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

        // Legacy temperature range - kept for backwards compatibility
        [System.Obsolete("Use AverageTemperatureRange instead")]
        public FloatRange TemperatureRange
        {
            get => AverageTemperatureRange;
            set => AverageTemperatureRange = value;
        }

        [System.Obsolete("Use AverageTemperatureImportance instead")]
        public FilterImportance TemperatureImportance
        {
            get => AverageTemperatureImportance;
            set => AverageTemperatureImportance = value;
        }

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

        // Animals can graze now
        public FilterImportance GrazeImportance { get; set; } = FilterImportance.Ignored;

        [System.Obsolete("Use GrazeImportance instead")]
        public FilterState GrazeState
        {
            get => GrazeImportance == FilterImportance.Critical ? FilterState.On : FilterState.Off;
            set => GrazeImportance = value == FilterState.On ? FilterImportance.Critical : FilterImportance.Ignored;
        }

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

        // Legacy river filters (kept for backward compatibility)
        [System.Obsolete("Use Rivers container with individual importance instead")]
        public MultiSelectFilterContainer<string> RiverTypes { get; set; } = new MultiSelectFilterContainer<string>();
        [System.Obsolete("Use Rivers container with individual importance instead")]
        public FilterImportance RiverImportance { get; set; } = FilterImportance.Critical;

        // Roads (individual importance per road type)
        public IndividualImportanceContainer<string> Roads { get; set; } = new IndividualImportanceContainer<string>();

        // Legacy road filters (kept for backward compatibility)
        [System.Obsolete("Use Roads container with individual importance instead")]
        public MultiSelectFilterContainer<string> RoadTypes { get; set; } = new MultiSelectFilterContainer<string>();
        [System.Obsolete("Use Roads container with individual importance instead")]
        public FilterImportance RoadImportance { get; set; } = FilterImportance.Ignored;

        // Individual stone filters (NEW - each stone has its own importance)
        public FilterImportance GraniteImportance { get; set; } = FilterImportance.Preferred;
        public FilterImportance MarbleImportance { get; set; } = FilterImportance.Ignored;
        public FilterImportance LimestoneImportance { get; set; } = FilterImportance.Preferred;
        public FilterImportance SlateImportance { get; set; } = FilterImportance.Ignored;
        public FilterImportance SandstoneImportance { get; set; } = FilterImportance.Ignored;

        // Legacy stone filters (kept for backward compatibility - will be migrated to individual filters)
        [System.Obsolete("Use individual stone importance properties (GraniteImportance, MarbleImportance, etc.) instead")]
        public HashSet<string> RequiredStoneDefNames { get; } = new HashSet<string>();

        [System.Obsolete("Use individual stone importance properties instead")]
        public FilterImportance StoneImportance { get; set; } = FilterImportance.Preferred;

        // Stone count filter ("any X stone types")
        public FloatRange StoneCountRange { get; set; } = new FloatRange(2f, 3f);
        public bool UseStoneCount { get; set; } = false;

        // Hilliness (existing multi-select)
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

        // Legacy map feature filter (kept for backward compatibility)
        [System.Obsolete("Use MapFeatures container with individual importance instead")]
        public FilterImportance MapFeatureImportance { get; set; } = FilterImportance.Ignored;

        // Adjacent biomes (individual importance per biome)
        public IndividualImportanceContainer<string> AdjacentBiomes { get; set; } = new IndividualImportanceContainer<string>();

        // Legacy adjacent biome filter (kept for backward compatibility)
        [System.Obsolete("Use AdjacentBiomes container with individual importance instead")]
        public FilterImportance AdjacentBiomeImportance { get; set; } = FilterImportance.Ignored;

        // World feature (legacy single selection)
        public string? RequiredFeatureDefName { get; set; }
        public FilterImportance FeatureImportance { get; set; } = FilterImportance.Ignored;

        // Has landmark (tiles with proper names)
        public FilterImportance LandmarkImportance { get; set; } = FilterImportance.Ignored;

        // === RESULTS CONTROL ===

        public const int DefaultMaxResults = 10;
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
        /// 1.0 = all criticals required (backward compatible)
        /// 0.8 = match 4 of 5 criticals
        /// 0.6 = match 3 of 5 criticals
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

            GrazeImportance = FilterImportance.Ignored;

            // Terrain & Geography
            MovementDifficultyRange = new FloatRange(0f, 2f);
            MovementDifficultyImportance = FilterImportance.Preferred;

            CoastalImportance = FilterImportance.Ignored;
            CoastalLakeImportance = FilterImportance.Ignored;

            // Reset new individual importance containers
            Rivers.Reset();
            Roads.Reset();

            // Reset legacy containers (for backward compatibility)
            RiverTypes.Reset();
            RiverImportance = FilterImportance.Critical;
            RoadTypes.Reset();
            RoadImportance = FilterImportance.Ignored;

            // Individual stone filters
            GraniteImportance = FilterImportance.Preferred;
            MarbleImportance = FilterImportance.Ignored;
            LimestoneImportance = FilterImportance.Preferred;
            SlateImportance = FilterImportance.Ignored;
            SandstoneImportance = FilterImportance.Ignored;

            // Legacy stone filters (for backward compatibility)
            RequiredStoneDefNames.Clear();
            StoneImportance = FilterImportance.Preferred;
            StoneCountRange = new FloatRange(2f, 3f);
            UseStoneCount = false;

            AllowedHilliness.Clear();
            AllowedHilliness.Add(Hilliness.SmallHills);
            AllowedHilliness.Add(Hilliness.LargeHills);
            AllowedHilliness.Add(Hilliness.Mountainous);

            // World & Features
            LockedBiome = null;

            // Reset new individual importance containers
            MapFeatures.Reset();
            AdjacentBiomes.Reset();

            // Reset legacy properties (for backward compatibility)
            MapFeatureImportance = FilterImportance.Ignored;
            AdjacentBiomeImportance = FilterImportance.Ignored;

            RequiredFeatureDefName = null;
            FeatureImportance = FilterImportance.Ignored;

            LandmarkImportance = FilterImportance.Ignored;

            // Results
            MaxResults = DefaultMaxResults;

            // Advanced Matching
            CriticalStrictness = 1.0f;
        }

        /// <summary>
        /// Migrates old stone settings to new individual stone importance properties.
        /// Called automatically when loading old save files.
        /// </summary>
        public void MigrateLegacyStoneSettings()
        {
            // If we have old stone selections but no new individual stone importances set,
            // migrate them to the new format
            if (RequiredStoneDefNames.Count > 0 &&
                GraniteImportance == FilterImportance.Ignored &&
                MarbleImportance == FilterImportance.Ignored &&
                LimestoneImportance == FilterImportance.Ignored &&
                SlateImportance == FilterImportance.Ignored &&
                SandstoneImportance == FilterImportance.Ignored)
            {
                // Migrate: all selected stones get the same importance as the old StoneImportance
                var targetImportance = StoneImportance;

                if (RequiredStoneDefNames.Contains("Granite"))
                    GraniteImportance = targetImportance;

                if (RequiredStoneDefNames.Contains("Marble"))
                    MarbleImportance = targetImportance;

                if (RequiredStoneDefNames.Contains("Limestone"))
                    LimestoneImportance = targetImportance;

                if (RequiredStoneDefNames.Contains("Slate"))
                    SlateImportance = targetImportance;

                if (RequiredStoneDefNames.Contains("Sandstone"))
                    SandstoneImportance = targetImportance;

                Log.Message($"[LandingZone] Migrated {RequiredStoneDefNames.Count} legacy stone settings to individual filters");

                // Clear legacy settings after migration
                RequiredStoneDefNames.Clear();
                StoneImportance = FilterImportance.Ignored;
            }
        }
    }

    public enum FilterImportance : byte
    {
        Ignored = 0,
        Preferred = 1,
        Critical = 2
    }
}

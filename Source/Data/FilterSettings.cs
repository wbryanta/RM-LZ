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
        public IndividualImportanceContainer<string> Rivers { get; set; } = new IndividualImportanceContainer<string>();

        // Roads (individual importance per road type)
        public IndividualImportanceContainer<string> Roads { get; set; } = new IndividualImportanceContainer<string>();

        // Stones (individual importance per stone type)
        // Default to OR operator since tiles typically have 1-2 mineral types; AND makes no sense for multiple critical stones
        public IndividualImportanceContainer<string> Stones { get; set; } = new IndividualImportanceContainer<string> { Operator = ImportanceOperator.OR };

        // Stockpiles (individual importance per stockpile type: Weapons, Medicine, Components, etc.)
        public IndividualImportanceContainer<string> Stockpiles { get; set; } = new IndividualImportanceContainer<string>();

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
        /// Copies all filter settings from another FilterSettings instance.
        /// Uses reflection to copy all public properties.
        /// </summary>
        public void CopyFrom(FilterSettings source)
        {
            if (source == null) return;

            var properties = typeof(FilterSettings).GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            foreach (var prop in properties)
            {
                if (prop.CanWrite && prop.CanRead)
                {
                    var value = prop.GetValue(source);

                    // Deep copy for IndividualImportanceContainer properties
                    if (value != null && prop.PropertyType.IsGenericType &&
                        prop.PropertyType.GetGenericTypeDefinition() == typeof(IndividualImportanceContainer<>))
                    {
                        var cloneMethod = prop.PropertyType.GetMethod("Clone");
                        if (cloneMethod != null)
                        {
                            value = cloneMethod.Invoke(value, null);
                        }
                    }

                    prop.SetValue(this, value);
                }
            }
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

            // === WORLD & FEATURES FILTERS (8 properties) ===

            // Locked biome (1 property - Def reference)
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

                // World & Features (8 properties)
                LockedBiome = lockedBiome;  // Can be null

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

    public enum FilterImportance : byte
    {
        Ignored = 0,
        Preferred = 1,
        Critical = 2
    }
}

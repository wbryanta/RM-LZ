using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Data
{
    /// <summary>
    /// Predefined filter configurations for common settlement preferences.
    /// Used by Default mode for quick selection.
    /// </summary>
    public static class FilterPresets
    {
        public static readonly PresetDefinition[] AllPresets = new[]
        {
            new PresetDefinition(
                "Temperate Paradise",
                "Mild climate, rich resources, easy living",
                ApplyTemperateParadise
            ),
            new PresetDefinition(
                "Arctic Challenge",
                "Frozen wasteland, harsh survival conditions",
                ApplyArcticChallenge
            ),
            new PresetDefinition(
                "Desert Oasis",
                "Hot and dry, scarce water and vegetation",
                ApplyDesertOasis
            ),
            new PresetDefinition(
                "Mountain Fortress",
                "Defensible highlands, stone-rich terrain",
                ApplyMountainFortress
            ),
            new PresetDefinition(
                "Coastal Trade Hub",
                "Ocean access, moderate climate, trading opportunities",
                ApplyCoastalTradeHub
            )
        };

        private static void ApplyTemperateParadise(FilterSettings settings)
        {
            settings.Reset();

            // Climate: Mild and comfortable
            settings.AverageTemperatureRange = new FloatRange(10f, 25f);
            settings.AverageTemperatureImportance = FilterImportance.Preferred;

            settings.RainfallRange = new FloatRange(600f, 1400f);
            settings.RainfallImportance = FilterImportance.Preferred;

            settings.GrowingDaysRange = new FloatRange(30f, 60f);
            settings.GrowingDaysImportance = FilterImportance.Preferred;

            // Terrain: Gentle hills
            settings.AllowedHilliness.Clear();
            settings.AllowedHilliness.Add(Hilliness.Flat);
            settings.AllowedHilliness.Add(Hilliness.SmallHills);
            settings.AllowedHilliness.Add(Hilliness.LargeHills);

            // Resources: Good variety
            settings.ForageImportance = FilterImportance.Preferred;
            settings.ForageabilityRange = new FloatRange(0.6f, 1f);

            // === HIDDEN ADVANCED FEATURES (Easy Access Advanced) ===
            // Uses canonical mutator names from world cache analysis

            // Favorable map features (not shown in Default UI, but improve quality)
            settings.MapFeatures.SetImportance("SteamGeysers_Increased", FilterImportance.Preferred);
            settings.MapFeatures.SetImportance("AnimalLife_Increased", FilterImportance.Preferred);
            settings.MapFeatures.SetImportance("PlantLife_Increased", FilterImportance.Preferred);
            settings.MapFeatures.SetImportance("WildPlants", FilterImportance.Preferred);
            settings.MapFeatures.SetImportance("Fertile", FilterImportance.Preferred);
            settings.MapFeatures.SetImportance("SunnyMutator", FilterImportance.Preferred);
            settings.MapFeatures.SetImportance("AncientRuins", FilterImportance.Preferred);
            settings.MapFeatures.SetImportance("Junkyard", FilterImportance.Preferred);

            // Preferred stones for construction
            settings.Stones.SetImportance("Granite", FilterImportance.Preferred);
            settings.Stones.SetImportance("Marble", FilterImportance.Preferred);
        }

        private static void ApplyArcticChallenge(FilterSettings settings)
        {
            settings.Reset();

            // Climate: Frozen
            settings.AverageTemperatureRange = new FloatRange(-50f, -10f);
            settings.AverageTemperatureImportance = FilterImportance.Critical;

            settings.GrowingDaysRange = new FloatRange(0f, 10f);
            settings.GrowingDaysImportance = FilterImportance.Critical;

            // Terrain: Any
            settings.AllowedHilliness.Clear();
            settings.AllowedHilliness.Add(Hilliness.Flat);
            settings.AllowedHilliness.Add(Hilliness.SmallHills);
            settings.AllowedHilliness.Add(Hilliness.LargeHills);
            settings.AllowedHilliness.Add(Hilliness.Mountainous);

            // === HIDDEN ADVANCED FEATURES (Easy Access Advanced) ===
            // Uses canonical mutator names from world cache analysis

            // Favorable features for arctic survival
            settings.MapFeatures.SetImportance("SteamGeysers_Increased", FilterImportance.Preferred);  // Critical for power/heat
            settings.MapFeatures.SetImportance("AnimalLife_Increased", FilterImportance.Preferred);  // More hunting
            settings.MapFeatures.SetImportance("HotSprings", FilterImportance.Preferred);  // Natural heating

            // Preferred stones
            settings.Stones.SetImportance("Granite", FilterImportance.Preferred);
        }

        private static void ApplyDesertOasis(FilterSettings settings)
        {
            settings.Reset();

            // Climate: Hot and dry
            settings.AverageTemperatureRange = new FloatRange(25f, 45f);
            settings.AverageTemperatureImportance = FilterImportance.Preferred;

            settings.RainfallRange = new FloatRange(0f, 600f);
            settings.RainfallImportance = FilterImportance.Preferred;

            settings.GrowingDaysRange = new FloatRange(20f, 50f);
            settings.GrowingDaysImportance = FilterImportance.Preferred;

            // Terrain: Flat desert
            settings.AllowedHilliness.Clear();
            settings.AllowedHilliness.Add(Hilliness.Flat);
            settings.AllowedHilliness.Add(Hilliness.SmallHills);

            // === HIDDEN ADVANCED FEATURES (Easy Access Advanced) ===
            // Uses canonical mutator names from world cache analysis

            // Favorable features for desert survival
            settings.MapFeatures.SetImportance("SteamGeysers_Increased", FilterImportance.Preferred);  // Power without fuel
            settings.MapFeatures.SetImportance("AnimalLife_Increased", FilterImportance.Preferred);
            settings.MapFeatures.SetImportance("Oasis", FilterImportance.Preferred);  // Water source
            settings.MapFeatures.SetImportance("SunnyMutator", FilterImportance.Preferred);  // Great for solar

            // Preferred stones
            settings.Stones.SetImportance("Granite", FilterImportance.Preferred);
            settings.Stones.SetImportance("Sandstone", FilterImportance.Preferred);  // Common in deserts
        }

        private static void ApplyMountainFortress(FilterSettings settings)
        {
            settings.Reset();

            // Climate: Cold but survivable
            settings.AverageTemperatureRange = new FloatRange(0f, 15f);
            settings.AverageTemperatureImportance = FilterImportance.Preferred;

            // Terrain: Mountainous
            settings.AllowedHilliness.Clear();
            settings.AllowedHilliness.Add(Hilliness.Mountainous);

            // Features: Caves for defense
            settings.MapFeatures.SetImportance("Caves", FilterImportance.Preferred);

            // === HIDDEN ADVANCED FEATURES (Easy Access Advanced) ===
            // Uses canonical mutator names from world cache analysis

            // Favorable features for mountain base
            settings.MapFeatures.SetImportance("SteamGeysers_Increased", FilterImportance.Preferred);  // Power in deep mountains
            settings.MapFeatures.SetImportance("MineralRich", FilterImportance.Preferred);  // Extra ore/stone
            settings.MapFeatures.SetImportance("Cavern", FilterImportance.Preferred);  // Large defensive space

            // Preferred stones for defense and construction
            settings.Stones.SetImportance("Granite", FilterImportance.Preferred);  // Highest HP
            settings.Stones.SetImportance("Marble", FilterImportance.Preferred);  // Beauty bonus
        }

        private static void ApplyCoastalTradeHub(FilterSettings settings)
        {
            settings.Reset();

            // Climate: Moderate
            settings.AverageTemperatureRange = new FloatRange(10f, 25f);
            settings.AverageTemperatureImportance = FilterImportance.Preferred;

            settings.RainfallRange = new FloatRange(600f, 1400f);
            settings.RainfallImportance = FilterImportance.Preferred;

            // Geography: Coastal
            settings.CoastalImportance = FilterImportance.Critical;

            // Terrain: Accessible
            settings.AllowedHilliness.Clear();
            settings.AllowedHilliness.Add(Hilliness.Flat);
            settings.AllowedHilliness.Add(Hilliness.SmallHills);

            // Features: Roads for trade
            settings.Roads.SetImportance("AncientAsphaltRoad", FilterImportance.Preferred);
            settings.Roads.SetImportance("AncientAsphaltHighway", FilterImportance.Preferred);

            // === HIDDEN ADVANCED FEATURES (Easy Access Advanced) ===
            // Uses canonical mutator names from world cache analysis

            // Favorable features for trade hub
            settings.MapFeatures.SetImportance("SteamGeysers_Increased", FilterImportance.Preferred);
            settings.MapFeatures.SetImportance("AnimalLife_Increased", FilterImportance.Preferred);
            settings.MapFeatures.SetImportance("PlantLife_Increased", FilterImportance.Preferred);
            settings.MapFeatures.SetImportance("Fertile", FilterImportance.Preferred);
            settings.MapFeatures.SetImportance("AncientRuins", FilterImportance.Preferred);  // Salvage for trade
            settings.MapFeatures.SetImportance("Junkyard", FilterImportance.Preferred);  // Salvage for trade

            // Preferred stones for construction
            settings.Stones.SetImportance("Granite", FilterImportance.Preferred);
            settings.Stones.SetImportance("Marble", FilterImportance.Preferred);  // Beauty for wealthy trade town
        }
    }

    public readonly struct PresetDefinition
    {
        public PresetDefinition(string name, string description, System.Action<FilterSettings> apply)
        {
            Name = name;
            Description = description;
            Apply = apply;
        }

        public string Name { get; }
        public string Description { get; }
        public System.Action<FilterSettings> Apply { get; }
    }
}

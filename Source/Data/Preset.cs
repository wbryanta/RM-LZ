using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Data
{
    /// <summary>
    /// A preset configuration bundle that can be applied to Simple mode.
    /// Contains filter settings, metadata, and rarity targeting information.
    /// </summary>
    public class Preset
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "User"; // "Curated", "Special", "User"
        public TileRarity? TargetRarity { get; set; } = null;

        /// <summary>
        /// Quick summary of key filters for display on preset card
        /// </summary>
        public string FilterSummary { get; set; } = "";

        /// <summary>
        /// Filter settings to apply when this preset is selected
        /// </summary>
        public FilterSettings Filters { get; set; } = new FilterSettings();

        /// <summary>
        /// Creates a deep copy of the filter settings
        /// </summary>
        public FilterSettings CloneFilters()
        {
            var clone = new FilterSettings();
            clone.CopyFrom(Filters);
            return clone;
        }

        /// <summary>
        /// Applies this preset's filters to the target FilterSettings
        /// </summary>
        public void ApplyTo(FilterSettings target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            target.CopyFrom(Filters);
        }
    }

    /// <summary>
    /// Library of curated and user-created presets
    /// </summary>
    public static class PresetLibrary
    {
        private static List<Preset> _curated = new List<Preset>();
        private static List<Preset> _userPresets = new List<Preset>();
        private static bool _initialized = false;

        /// <summary>
        /// Initializes preset library with curated bundles
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            _curated = new List<Preset>
            {
                // Special presets (Angel/Unicorn/Demon/Scorched)
                CreateAngelPreset(),
                CreateUnicornPreset(),
                CreateDemonPreset(),
                CreateScorchedHellPreset(),

                // Classic presets
                CreateTemperatePreset(),
                CreateArcticChallengePreset(),
                CreateDesertOasisPreset(),

                // New playstyle presets
                CreateDefensePreset(),
                CreateAgrarianPreset(),
                CreatePowerPreset(),
                CreateBayouPreset()
            };

            _initialized = true;
            Log.Message($"[LandingZone] PresetLibrary initialized with {_curated.Count} curated presets");
        }

        /// <summary>
        /// Gets all curated presets (Angel, Unicorn, Demon, and classic bundles)
        /// </summary>
        public static IReadOnlyList<Preset> GetCurated()
        {
            if (!_initialized) Initialize();
            return _curated;
        }

        /// <summary>
        /// Gets user-saved presets
        /// </summary>
        public static IReadOnlyList<Preset> GetUserPresets()
        {
            return _userPresets;
        }

        /// <summary>
        /// Saves current Simple mode filters as a user preset.
        /// Returns true if saved successfully, false if name already exists.
        /// </summary>
        public static bool SaveUserPreset(string name, FilterSettings filters)
        {
            // Check for duplicate names
            if (_userPresets.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                Log.Warning($"[LandingZone] User preset '{name}' already exists");
                return false;
            }

            var preset = new Preset
            {
                Id = $"user_{Guid.NewGuid():N}",
                Name = name,
                Description = "User-created preset from Simple mode",
                Category = "User",
                Filters = new FilterSettings()
            };
            preset.Filters.CopyFrom(filters);
            preset.FilterSummary = GenerateFilterSummary(filters);

            _userPresets.Add(preset);
            Log.Message($"[LandingZone] Saved user preset: {name}");
            return true;
        }

        /// <summary>
        /// Deletes a user preset by name
        /// </summary>
        public static bool DeleteUserPreset(string name)
        {
            var preset = _userPresets.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (preset != null)
            {
                _userPresets.Remove(preset);
                Log.Message($"[LandingZone] Deleted user preset: {name}");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Generates a quick summary of active filters for display
        /// </summary>
        private static string GenerateFilterSummary(FilterSettings filters)
        {
            var parts = new List<string>();

            if (filters.LockedBiome != null)
                parts.Add($"Biome: {filters.LockedBiome.LabelCap}");

            if (filters.AverageTemperatureImportance != FilterImportance.Ignored)
                parts.Add($"Temp: {filters.AverageTemperatureRange.min:F0}-{filters.AverageTemperatureRange.max:F0}°C");

            if (filters.CoastalImportance != FilterImportance.Ignored)
                parts.Add("Coastal");

            if (filters.Rivers.HasAnyImportance)
                parts.Add($"Rivers: {filters.Rivers.Operator}");

            if (filters.Stones.HasAnyImportance)
                parts.Add($"{filters.Stones.CountByImportance(FilterImportance.Critical)} stones");

            return string.Join(" | ", parts);
        }

        // ===== ANGEL PRESET: High quality of life =====
        private static Preset CreateAngelPreset()
        {
            var preset = new Preset
            {
                Id = "angel",
                Name = "Angel",
                Description = "Perfect quality of life - temperate, fertile, resources abundant. Targets rare combinations (0.1-1% of tiles).",
                Category = "Special",
                TargetRarity = TileRarity.Rare,
                FilterSummary = "Temperate Forest | Coastal | Rivers | All Resources"
            };

            var filters = preset.Filters;

            // Climate: Temperate with good rainfall
            filters.AverageTemperatureRange = new FloatRange(15f, 25f);
            filters.AverageTemperatureImportance = FilterImportance.Critical;
            filters.RainfallRange = new FloatRange(1200f, 2500f);
            filters.RainfallImportance = FilterImportance.Preferred;
            filters.GrowingDaysRange = new FloatRange(50f, 60f);
            filters.GrowingDaysImportance = FilterImportance.Preferred;

            // Geography: Coastal with rivers
            filters.CoastalImportance = FilterImportance.Preferred;
            filters.Rivers.SetImportance("HugeRiver", FilterImportance.Preferred);
            filters.Rivers.SetImportance("LargeRiver", FilterImportance.Preferred);
            filters.Rivers.Operator = ImportanceOperator.OR;

            // Resources: Abundant stones
            filters.Stones.SetImportance("Granite", FilterImportance.Preferred);
            filters.Stones.SetImportance("Limestone", FilterImportance.Preferred);
            filters.Stones.SetImportance("Marble", FilterImportance.Preferred);
            filters.Stones.Operator = ImportanceOperator.AND;

            // Features: Geothermal, caves
            filters.MapFeatures.SetImportance("SteamGeysers_Increased", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Caves", FilterImportance.Preferred);
            filters.MapFeatures.Operator = ImportanceOperator.OR;

            return preset;
        }

        // ===== UNICORN PRESET: Rare/mythic combinations =====
        private static Preset CreateUnicornPreset()
        {
            var preset = new Preset
            {
                Id = "unicorn",
                Name = "Unicorn",
                Description = "Extremely rare combinations - Archean trees, headwaters, multiple rare features. Targets epic/legendary tiles (<0.01% of tiles).",
                Category = "Special",
                TargetRarity = TileRarity.Epic,
                FilterSummary = "Rare Mutators | Ancient Features | Unique Geography"
            };

            var filters = preset.Filters;

            // Climate: Temperate (to ensure settleable)
            filters.AverageTemperatureRange = new FloatRange(10f, 30f);
            filters.AverageTemperatureImportance = FilterImportance.Preferred;

            // Features: Stack rare mutators
            filters.MapFeatures.SetImportance("ArcheanTrees", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("Headwater", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("SteamGeysers_Increased", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("AnimalHabitat", FilterImportance.Preferred);
            filters.MapFeatures.Operator = ImportanceOperator.AND; // Must have multiple

            // Geography: Unusual combinations
            filters.CoastalLakeImportance = FilterImportance.Preferred;
            filters.Rivers.SetImportance("HugeRiver", FilterImportance.Preferred);

            return preset;
        }

        // ===== DEMON PRESET: Extreme challenges =====
        private static Preset CreateDemonPreset()
        {
            var preset = new Preset
            {
                Id = "demon",
                Name = "Demon",
                Description = "Extreme survival challenge - ice sheets, extreme deserts, or hostile combinations. For masochists only.",
                Category = "Special",
                TargetRarity = TileRarity.VeryRare,
                FilterSummary = "Ice Sheet OR Extreme Desert | Hostile Features"
            };

            var filters = preset.Filters;

            // Climate: Extremes
            filters.AverageTemperatureRange = new FloatRange(-50f, -20f); // Ice sheet
            filters.AverageTemperatureImportance = FilterImportance.Critical;
            filters.GrowingDaysRange = new FloatRange(0f, 10f);
            filters.GrowingDaysImportance = FilterImportance.Preferred;

            // OR: Extreme heat
            // Note: This is simplified - actual implementation should support OR between temperature ranges

            // Features: Hostile
            filters.MapFeatures.SetImportance("ToxicRain", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Junkyard", FilterImportance.Preferred);

            return preset;
        }

        // ===== SCORCHED HELL PRESET: Extreme heat variant =====
        private static Preset CreateScorchedHellPreset()
        {
            var preset = new Preset
            {
                Id = "scorched_hell",
                Name = "Scorched Hell",
                Description = "Extreme heat survival - scorching deserts, volcanic activity, toxic features. Lava and fire everywhere.",
                Category = "Special",
                TargetRarity = TileRarity.VeryRare,
                FilterSummary = "Extreme Heat | Volcanic | Toxic Features"
            };

            var filters = preset.Filters;

            // Climate: Extreme heat
            filters.AverageTemperatureRange = new FloatRange(35f, 60f); // Scorching heat
            filters.AverageTemperatureImportance = FilterImportance.Critical;
            filters.RainfallRange = new FloatRange(0f, 400f); // Arid/desert
            filters.RainfallImportance = FilterImportance.Preferred;
            filters.GrowingDaysRange = new FloatRange(0f, 20f);
            filters.GrowingDaysImportance = FilterImportance.Preferred;

            // Features: Hostile/volcanic
            filters.MapFeatures.SetImportance("ToxicRain", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("SteamGeysers_Increased", FilterImportance.Preferred); // Volcanic activity
            filters.MapFeatures.SetImportance("Junkyard", FilterImportance.Preferred);

            return preset;
        }

        // ===== Classic curated presets from existing FilterPresets =====
        private static Preset CreateTemperatePreset()
        {
            var preset = new Preset
            {
                Id = "temperate",
                Name = "Temperate",
                Description = "Balanced temperate climate with good growing season and rainfall",
                Category = "Curated",
                FilterSummary = "10-32°C | 40-60 days grow | 1000-2200mm rain"
            };

            preset.Filters.AverageTemperatureRange = new FloatRange(10f, 32f);
            preset.Filters.AverageTemperatureImportance = FilterImportance.Preferred;
            preset.Filters.RainfallRange = new FloatRange(1000f, 2200f);
            preset.Filters.RainfallImportance = FilterImportance.Preferred;
            preset.Filters.GrowingDaysRange = new FloatRange(40f, 60f);
            preset.Filters.GrowingDaysImportance = FilterImportance.Preferred;

            return preset;
        }

        private static Preset CreateArcticChallengePreset()
        {
            var preset = new Preset
            {
                Id = "arctic_challenge",
                Name = "Arctic Challenge",
                Description = "Extreme cold survival test with minimal growing season",
                Category = "Curated",
                FilterSummary = "-50 to -10°C | 0-20 days grow"
            };

            preset.Filters.AverageTemperatureRange = new FloatRange(-50f, -10f);
            preset.Filters.AverageTemperatureImportance = FilterImportance.Critical;
            preset.Filters.GrowingDaysRange = new FloatRange(0f, 20f);
            preset.Filters.GrowingDaysImportance = FilterImportance.Preferred;

            return preset;
        }

        private static Preset CreateDesertOasisPreset()
        {
            var preset = new Preset
            {
                Id = "desert_oasis",
                Name = "Desert Oasis",
                Description = "Hot, dry climate with water access for survival",
                Category = "Curated",
                FilterSummary = "30-50°C | Coastal/Lake | River"
            };

            preset.Filters.AverageTemperatureRange = new FloatRange(30f, 50f);
            preset.Filters.AverageTemperatureImportance = FilterImportance.Preferred;
            preset.Filters.RainfallRange = new FloatRange(200f, 800f);
            preset.Filters.RainfallImportance = FilterImportance.Preferred;
            preset.Filters.CoastalImportance = FilterImportance.Preferred;
            preset.Filters.Rivers.SetImportance("LargeRiver", FilterImportance.Preferred);

            return preset;
        }

        // ===== NEW CURATED PRESETS: Diverse playstyles =====
        private static Preset CreateDefensePreset()
        {
            var preset = new Preset
            {
                Id = "defense",
                Name = "Defense",
                Description = "Defensible position - mountains, caves, chokepoints. Build an impenetrable fortress.",
                Category = "Curated",
                FilterSummary = "Mountainous | Caves | Stone Rich"
            };

            var filters = preset.Filters;

            // Terrain: Mountainous for defense
            filters.AllowedHilliness.Clear();
            filters.AllowedHilliness.Add(Hilliness.Mountainous);

            // Features: Caves and defensible terrain
            filters.MapFeatures.SetImportance("Caves", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Cavern", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Mountain", FilterImportance.Preferred);

            // Resources: Stone for building defenses
            filters.Stones.SetImportance("Granite", FilterImportance.Preferred); // Highest HP
            filters.Stones.SetImportance("Limestone", FilterImportance.Preferred);
            filters.Stones.Operator = ImportanceOperator.AND;

            // Climate: Survivable but not paradise
            filters.AverageTemperatureRange = new FloatRange(-10f, 20f);
            filters.AverageTemperatureImportance = FilterImportance.Preferred;

            return preset;
        }

        private static Preset CreateAgrarianPreset()
        {
            var preset = new Preset
            {
                Id = "agrarian",
                Name = "Agrarian",
                Description = "Perfect farming conditions - long growing season, fertile soil, abundant resources. Feed the world.",
                Category = "Curated",
                FilterSummary = "50-60 days grow | Fertile | WildPlants | Temperate"
            };

            var filters = preset.Filters;

            // Climate: Optimal for growing
            filters.GrowingDaysRange = new FloatRange(50f, 60f);
            filters.GrowingDaysImportance = FilterImportance.Critical;
            filters.AverageTemperatureRange = new FloatRange(15f, 30f);
            filters.AverageTemperatureImportance = FilterImportance.Preferred;
            filters.RainfallRange = new FloatRange(1000f, 2500f);
            filters.RainfallImportance = FilterImportance.Preferred;

            // Features: Fertile and plant-rich
            filters.MapFeatures.SetImportance("Fertile", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("WildPlants", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("PlantLife_Increased", FilterImportance.Preferred);
            filters.MapFeatures.Operator = ImportanceOperator.OR;

            // Terrain: Flat for farming
            filters.AllowedHilliness.Clear();
            filters.AllowedHilliness.Add(Hilliness.Flat);
            filters.AllowedHilliness.Add(Hilliness.SmallHills);

            return preset;
        }

        private static Preset CreatePowerPreset()
        {
            var preset = new Preset
            {
                Id = "power",
                Name = "Power",
                Description = "Energy independence - geothermal vents, rivers for hydro, sunny for solar. Unlimited electricity.",
                Category = "Curated",
                FilterSummary = "Geothermal | Rivers | Sunny"
            };

            var filters = preset.Filters;

            // Features: Power-generating features
            filters.MapFeatures.SetImportance("SteamGeysers_Increased", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("SunnyMutator", FilterImportance.Preferred);

            // Geography: Rivers for hydroelectric
            filters.Rivers.SetImportance("HugeRiver", FilterImportance.Preferred);
            filters.Rivers.SetImportance("LargeRiver", FilterImportance.Preferred);
            filters.Rivers.Operator = ImportanceOperator.OR;

            // Climate: Temperate for ease of access
            filters.AverageTemperatureRange = new FloatRange(10f, 30f);
            filters.AverageTemperatureImportance = FilterImportance.Preferred;

            return preset;
        }

        private static Preset CreateBayouPreset()
        {
            var preset = new Preset
            {
                Id = "bayou",
                Name = "Bayou",
                Description = "Swamplands - high rainfall, wetlands, rich biodiversity. Hunt, fish, and survive in the marsh.",
                Category = "Curated",
                FilterSummary = "Swamp | High Rain | Coastal | Warm"
            };

            var filters = preset.Filters;

            // Climate: Warm and wet
            filters.AverageTemperatureRange = new FloatRange(20f, 35f);
            filters.AverageTemperatureImportance = FilterImportance.Preferred;
            filters.RainfallRange = new FloatRange(2000f, 4000f);
            filters.RainfallImportance = FilterImportance.Critical;

            // Geography: Coastal/lakes + rivers
            filters.CoastalLakeImportance = FilterImportance.Preferred;
            filters.Rivers.SetImportance("LargeRiver", FilterImportance.Preferred);
            filters.Rivers.SetImportance("HugeRiver", FilterImportance.Preferred);

            // Features: Wildlife and plants
            filters.MapFeatures.SetImportance("AnimalLife_Increased", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("WildPlants", FilterImportance.Preferred);

            return preset;
        }
    }
}

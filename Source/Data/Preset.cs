using System;
using System.Collections.Generic;
using System.Linq;
using LandingZone.Core.Filtering.Filters;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Data
{
    /// <summary>
    /// Represents a fallback tier for presets targeting extremely rare features.
    /// If a preset's primary filters yield zero results, the system tries fallback tiers in sequence.
    /// </summary>
    public class FallbackTier
    {
        public string Name { get; set; } = "";
        public FilterSettings Filters { get; set; } = new FilterSettings();
    }

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
        /// Preset-specific mutator quality overrides.
        /// When scoring tiles for this preset, these values replace global MutatorQualityRatings.
        /// Allows presets to value "negative" mutators (e.g., Scorched wants lava features).
        /// Key: mutatorDefName, Value: quality override for THIS preset only
        /// </summary>
        public Dictionary<string, int> MutatorQualityOverrides { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Tiered fallback configurations for presets that target extremely rare features.
        /// If primary filters yield zero results, the system will try each fallback tier in sequence.
        /// Each tier should be progressively less restrictive than the previous.
        /// </summary>
        public List<FallbackTier>? FallbackTiers { get; set; } = null;

        /// <summary>
        /// Minimum strictness for this preset's Critical filters.
        /// null = use default (0.0 for fuzzy matching)
        /// 1.0 = require ALL Critical filters to match (hard enforcement)
        /// Use for presets with specific Critical requirements (e.g., Scorched temperature range)
        /// </summary>
        public float? MinimumStrictness { get; set; } = null;

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
                // Special presets (4-column row 1)
                CreateElysianPreset(),
                CreateExoticPreset(),
                CreateSubZeroPreset(),
                CreateScorchedPreset(),

                // Curated playstyle presets (4-column rows 2-3)
                CreateDesertOasisPreset(),
                CreateDefensePreset(),
                CreateAgrarianPreset(),
                CreatePowerPreset(),
                CreateBayouPreset(),
                CreateSavannahPreset(),
                CreateAquaticPreset(),
                CreateHomesteaderPreset(),
                // 8 curated + 4 specials = 12 total (perfect 3x4 grid)

                // TEMPORARY: Testing presets (4-column row 4)
                // TODO: Remove after stockpile scoring validation is complete
                CreateTestComponentStockpilePreset(),
                CreateTestWeaponsStockpilePreset(),
                CreateTestMedicineStockpilePreset(),
                CreateTestMineableSteelPreset()
            };

            _initialized = true;
            Log.Message($"[LandingZone] PresetLibrary initialized with {_curated.Count} curated presets");
        }

        /// <summary>
        /// Gets all curated presets (Elysian, Exotic, SubZero, Scorched + 8 curated playstyles)
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
        /// <param name="name">Name for the new preset</param>
        /// <param name="filters">Filter settings to save</param>
        /// <param name="sourcePreset">Optional source preset to copy quality overrides from</param>
        public static bool SaveUserPreset(string name, FilterSettings filters, Preset? sourcePreset = null)
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

            // Copy mutator quality overrides from source preset if provided
            // This preserves themed scoring when saving variants of curated presets (e.g., Scorched, Homesteader)
            if (sourcePreset?.MutatorQualityOverrides != null && sourcePreset.MutatorQualityOverrides.Count > 0)
            {
                preset.MutatorQualityOverrides = new Dictionary<string, int>(sourcePreset.MutatorQualityOverrides);
                Log.Message($"[LandingZone] Saved user preset '{name}' with {preset.MutatorQualityOverrides.Count} quality overrides");
            }

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

        // ===== ELYSIAN PRESET: Perfect everything - highest quality of life =====
        private static Preset CreateElysianPreset()
        {
            var preset = new Preset
            {
                Id = "elysian",
                Name = "Elysian",
                Description = "The easiest RimWorld experience - perfect climate, ideal biome, abundant resources, stacked +10 quality mutators. God-tier colonist paradise.",
                Category = "Special",
                TargetRarity = TileRarity.Epic,
                FilterSummary = "Perfect Climate | +10 Mutators | Best Resources"
            };

            var filters = preset.Filters;

            // Climate: Perfect comfort zone (Critical)
            filters.AverageTemperatureRange = new FloatRange(18f, 25f);
            filters.AverageTemperatureImportance = FilterImportance.Critical;
            filters.RainfallRange = new FloatRange(1400f, 2200f);
            filters.RainfallImportance = FilterImportance.Critical;
            filters.GrowingDaysRange = new FloatRange(55f, 60f);
            filters.GrowingDaysImportance = FilterImportance.Critical;
            filters.PollutionRange = new FloatRange(0f, 0.1f);
            filters.PollutionImportance = FilterImportance.Critical;

            // Biomes: Paradise biomes (Critical, OR)
            // Note: Would use multi-biome container if available
            // Target: TemperateForest (30%), TropicalRainforest (11.7%)

            // +10 Mutators (Critical, OR) - Stack as many as possible
            filters.MapFeatures.SetImportance("Fertile", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("MineralRich", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("SteamGeysers_Increased", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("AncientHeatVent", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("HotSprings", FilterImportance.Critical);
            filters.MapFeatures.Operator = ImportanceOperator.OR;

            // +8 Mutators (Preferred, OR) - Secondary bonuses
            filters.MapFeatures.SetImportance("PlantLife_Increased", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Fish_Increased", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("AnimalLife_Increased", FilterImportance.Preferred);

            // +7 Mutators (Preferred, OR) - Tertiary bonuses
            filters.MapFeatures.SetImportance("WetClimate", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Wetland", FilterImportance.Preferred);

            // +5/+6 Mutators (Preferred, OR)
            filters.MapFeatures.SetImportance("Caves", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("WildPlants", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Muddy", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("River", FilterImportance.Preferred);

            // Geography: Water access for trade/fishing (Preferred, OR)
            filters.CoastalImportance = FilterImportance.Preferred;
            filters.CoastalLakeImportance = FilterImportance.Preferred;

            // Rivers: Major water sources (Preferred, OR)
            filters.Rivers.SetImportance("HugeRiver", FilterImportance.Preferred);
            filters.Rivers.SetImportance("LargeRiver", FilterImportance.Preferred);
            filters.Rivers.Operator = ImportanceOperator.OR;

            // Hilliness: Varied terrain (avoid flat and extreme)
            filters.AllowedHilliness.Clear();
            filters.AllowedHilliness.Add(Hilliness.SmallHills);
            filters.AllowedHilliness.Add(Hilliness.LargeHills);

            // Movement: Not too easy, not too hard (Preferred)
            filters.MovementDifficultyRange = new FloatRange(0.8f, 1.5f);
            filters.MovementDifficultyImportance = FilterImportance.Preferred;

            // Resources: Valuable ores for construction and trading (Preferred, OR)
            filters.Stones.SetImportance("MineablePlasteel", FilterImportance.Preferred);  // Advanced construction
            filters.Stones.SetImportance("MineableGold", FilterImportance.Preferred);      // Trading wealth
            filters.Stones.Operator = ImportanceOperator.OR;  // Accept either (tiles only have 1 ore)

            // Resource ranges: Abundant everything (Preferred)
            filters.ForageabilityRange = new FloatRange(0.7f, 1.0f);
            filters.ForageImportance = FilterImportance.Preferred;
            filters.PlantDensityRange = new FloatRange(0.8f, 1.3f);
            filters.PlantDensityImportance = FilterImportance.Preferred;
            filters.AnimalDensityRange = new FloatRange(2.5f, 6.5f);
            filters.AnimalDensityImportance = FilterImportance.Preferred;
            filters.FishPopulationRange = new FloatRange(400f, 900f);
            filters.FishPopulationImportance = FilterImportance.Preferred;

            return preset;
        }

        // ===== EXOTIC PRESET: Ultra-rare feature hunter =====
        // DESIGN NOTE: This preset uses a "guaranteed anchor + bonus stacking" approach instead of staged fallback.
        // Critical: ArcheanTrees (0.0034%, ~24 tiles globally) - THE rarest mutator, guarantees exact result count
        // Preferred: Other rare features - bonus scoring for tiles that stack multiple rares
        //
        // This design INTENTIONALLY avoids fallback logic because:
        // 1. ArcheanTrees alone guarantees non-zero results (exactly 24 tiles)
        // 2. Preferred rares create natural scoring gradient (1-7 rares stacked)
        // 3. Top results will be tiles with ArcheanTrees + multiple other rares
        // 4. No risk of empty results or confusing "which tier matched" logging
        //
        // Expected Results: ~24 tiles (all ArcheanTrees locations), scored by rare feature stacking.
        private static Preset CreateExoticPreset()
        {
            var preset = new Preset
            {
                Id = "exotic",
                Name = "Exotic",
                Description = "Chase the rarest features in RimWorld - ArcheanTrees anchor (24 tiles globally), with bonus scoring for stacking additional rare mutators.",
                Category = "Special",
                TargetRarity = TileRarity.Epic,
                FilterSummary = "ArcheanTrees | Stack Rare Features"
            };

            var filters = preset.Filters;

            // Climate: Keep broad to maximize rare feature finds (Preferred)
            filters.AverageTemperatureRange = new FloatRange(5f, 35f);
            filters.AverageTemperatureImportance = FilterImportance.Preferred;

            // Ultra-Rare Anchor (Critical): ArcheanTrees is THE rarest mutator
            // This guarantees ~24 results (all ArcheanTrees tiles globally)
            filters.MapFeatures.SetImportance("ArcheanTrees", FilterImportance.Critical);
            filters.MapFeatures.Operator = ImportanceOperator.OR;

            // Stack More Rares (Preferred, OR) - Reward tiles with multiple rare features
            // Tiles with ArcheanTrees + multiple of these will score highest
            filters.MapFeatures.SetImportance("Cavern", FilterImportance.Preferred);        // 0.022%, 157 tiles
            filters.MapFeatures.SetImportance("Headwater", FilterImportance.Preferred);     // 0.25%, 1745 tiles
            filters.MapFeatures.SetImportance("HotSprings", FilterImportance.Preferred);    // 0.0094%, 66 tiles
            filters.MapFeatures.SetImportance("Oasis", FilterImportance.Preferred);         // 0.0094%, 66 tiles
            filters.MapFeatures.SetImportance("RiverDelta", FilterImportance.Preferred);    // 0.023%, 159 tiles
            filters.MapFeatures.SetImportance("Peninsula", FilterImportance.Preferred);     // 0.021%, 148 tiles
            filters.MapFeatures.SetImportance("MineralRich", FilterImportance.Preferred);   // 0.017%, 119 tiles

            // Geography: Unusual combinations (Preferred)
            filters.CoastalLakeImportance = FilterImportance.Preferred;
            filters.Rivers.SetImportance("HugeRiver", FilterImportance.Preferred);
            filters.Rivers.Operator = ImportanceOperator.OR;

            // Tiered fallback for when ArcheanTrees doesn't exist (missing Biotech DLC or unlucky seed)
            preset.FallbackTiers = new List<FallbackTier>();

            // Tier 2: Any ultra-rare feature (if ArcheanTrees yields zero)
            var tier2 = new FallbackTier
            {
                Name = "Ultra-Rares (any)",
                Filters = new FilterSettings()
            };
            tier2.Filters.CopyFrom(filters); // Copy base settings
            tier2.Filters.MapFeatures.Reset(); // Clear existing map features
            tier2.Filters.MapFeatures.SetImportance("Cavern", FilterImportance.Critical);       // 0.022%
            tier2.Filters.MapFeatures.SetImportance("HotSprings", FilterImportance.Critical);   // 0.0094%
            tier2.Filters.MapFeatures.SetImportance("Oasis", FilterImportance.Critical);        // 0.0094%
            tier2.Filters.MapFeatures.SetImportance("RiverDelta", FilterImportance.Critical);   // 0.023%
            tier2.Filters.MapFeatures.SetImportance("Peninsula", FilterImportance.Critical);    // 0.021%
            tier2.Filters.MapFeatures.SetImportance("MineralRich", FilterImportance.Critical);  // 0.017%
            tier2.Filters.MapFeatures.Operator = ImportanceOperator.OR; // Need at least one

            preset.FallbackTiers.Add(tier2);

            // Quality Overrides: Boost ultra-rare features for stacking bonuses
            preset.MutatorQualityOverrides["ArcheanTrees"] = 10;     // 3 → +10 (THE rarest, 0.0034% = 24 tiles)
            preset.MutatorQualityOverrides["Cavern"] = 8;            // 5 → +8  (0.022% = 157 tiles)
            preset.MutatorQualityOverrides["HotSprings"] = 10;       // Already 10, keep high
            preset.MutatorQualityOverrides["Oasis"] = 9;             // 7 → +9  (0.0094% = 66 tiles)
            preset.MutatorQualityOverrides["RiverDelta"] = 7;        // 2 → +7  (0.023% = 159 tiles)
            preset.MutatorQualityOverrides["Peninsula"] = 6;         // 0 → +6  (0.021% = 148 tiles)
            preset.MutatorQualityOverrides["MineralRich"] = 10;      // Already 10, keep high
            preset.MutatorQualityOverrides["Headwater"] = 5;         // Boost headwater bonus

            return preset;
        }

        // ===== SUBZERO PRESET: Frozen wasteland survival =====
        private static Preset CreateSubZeroPreset()
        {
            var preset = new Preset
            {
                Id = "subzero",
                Name = "SubZero",
                Description = "Extreme cold survival challenge - frozen tundra, ice sheets, barely any growing season. For masochists only.",
                Category = "Special",
                TargetRarity = TileRarity.VeryRare,
                FilterSummary = "Frozen | Tundra/Boreal | Ice Features"
            };

            var filters = preset.Filters;

            // Climate: Extreme cold (Critical)
            filters.AverageTemperatureRange = new FloatRange(-50f, -15f);
            filters.AverageTemperatureImportance = FilterImportance.Critical;
            filters.GrowingDaysRange = new FloatRange(0f, 15f);
            filters.GrowingDaysImportance = FilterImportance.Critical;

            // Biomes: Frozen biomes (Critical, OR)
            // Note: Would use multi-biome container if available
            // Target: Tundra (8.5%), BorealForest (5.6%), GlacialPlain (0.33%)

            // Desired Features (Preferred, OR): Survival aids in frozen environment
            filters.MapFeatures.SetImportance("Caves", FilterImportance.Preferred);                // Shelter from cold
            filters.MapFeatures.SetImportance("SteamGeysers_Increased", FilterImportance.Preferred); // Critical for heating
            filters.MapFeatures.SetImportance("MineralRich", FilterImportance.Preferred);          // Mining focus when can't farm
            filters.MapFeatures.Operator = ImportanceOperator.OR;

            // Geographic Features (Preferred, OR): Thematic ice features
            filters.MapFeatures.SetImportance("IceCaves", FilterImportance.Preferred);  // Very rare ice feature
            filters.MapFeatures.SetImportance("Crevasse", FilterImportance.Preferred);    // Dangerous ice terrain
            filters.MapFeatures.SetImportance("Iceberg", FilterImportance.Preferred);     // Visual flair

            return preset;
        }

        // ===== SCORCHED PRESET: Volcanic nightmare =====
        private static Preset CreateScorchedPreset()
        {
            var preset = new Preset
            {
                Id = "scorched",
                Name = "Scorched",
                Description = "Volcanic nightmare - extreme heat, lava flows, toxic atmosphere. Embrace the fire!",
                Category = "Special",
                TargetRarity = TileRarity.VeryRare,
                FilterSummary = "Lava | Extreme Heat | Toxic | Volcanic",
                MinimumStrictness = 1.0f  // Enforce ALL Critical filters (temp + rainfall + lava features)
            };

            var filters = preset.Filters;

            // Climate: Extreme heat (Critical)
            filters.AverageTemperatureRange = new FloatRange(35f, 60f);
            filters.AverageTemperatureImportance = FilterImportance.Critical;
            filters.RainfallRange = new FloatRange(0f, 400f);
            filters.RainfallImportance = FilterImportance.Critical;
            filters.GrowingDaysRange = new FloatRange(0f, 25f);
            filters.GrowingDaysImportance = FilterImportance.Critical;
            filters.PollutionRange = new FloatRange(0.3f, 1.0f);
            filters.PollutionImportance = FilterImportance.Preferred;

            // Biomes: Desert/volcanic (Critical, OR)
            // Note: Would use multi-biome container if available, for now document in description
            // Target: ExtremeDesert, Desert, LavaField

            // Lava Features (Critical, OR) - Core thematic elements
            filters.MapFeatures.SetImportance("LavaCaves", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("LavaFlow", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("LavaCrater", FilterImportance.Critical);
            filters.MapFeatures.Operator = ImportanceOperator.OR; // Need at least one lava feature

            // Toxic/Hostile Features (Preferred, OR) - Secondary thematic elements
            filters.MapFeatures.SetImportance("ToxicLake", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("AncientSmokeVent", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("AncientToxVent", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Pollution_Increased", FilterImportance.Preferred);

            // Supporting Features
            filters.MapFeatures.SetImportance("SteamGeysers_Increased", FilterImportance.Preferred); // Geothermal in volcanic regions
            filters.MapFeatures.SetImportance("DryGround", FilterImportance.Preferred); // Barren wasteland

            // Resources: Volcanic materials
            filters.MapFeatures.SetImportance("ObsidianDeposits", FilterImportance.Preferred);
            // Note: MineableObsidian may not exist in base game - needs validation via mineral cache logs
            // filters.Stones.SetImportance("MineableObsidian", FilterImportance.Preferred);

            // Quality Overrides: Make "dangerous" features valuable for this theme
            preset.MutatorQualityOverrides["LavaCaves"] = 8;           // -9 → +8
            preset.MutatorQualityOverrides["LavaFlow"] = 8;            // -9 → +8
            preset.MutatorQualityOverrides["LavaCrater"] = 7;          // -10 → +7
            preset.MutatorQualityOverrides["ToxicLake"] = 5;           // -10 → +5
            preset.MutatorQualityOverrides["AncientSmokeVent"] = 4;    // -8 → +4
            preset.MutatorQualityOverrides["AncientToxVent"] = 5;      // -10 → +5
            preset.MutatorQualityOverrides["Pollution_Increased"] = 4; // -8 → +4
            preset.MutatorQualityOverrides["DryGround"] = 3;           // -6 → +3

            // Fallback tiers for Scorched (lava features are ultra-rare and may not overlap with extreme heat)
            preset.FallbackTiers = new List<FallbackTier>();

            // Tier 2: Keep lava features Critical, relax temperature requirement
            var tier2 = new FallbackTier
            {
                Name = "Lava features (relaxed temp)",
                Filters = new FilterSettings()
            };
            tier2.Filters.CopyFrom(filters);
            tier2.Filters.AverageTemperatureRange = new FloatRange(15f, 80f); // Widen temp range
            tier2.Filters.AverageTemperatureImportance = FilterImportance.Preferred; // Downgrade from Critical
            preset.FallbackTiers.Add(tier2);

            // Tier 3: Drop lava requirement, focus on extreme heat/dry desert
            var tier3 = new FallbackTier
            {
                Name = "Extreme heat desert (no lava)",
                Filters = new FilterSettings()
            };
            tier3.Filters.AverageTemperatureRange = new FloatRange(35f, 60f);
            tier3.Filters.AverageTemperatureImportance = FilterImportance.Critical;
            tier3.Filters.RainfallRange = new FloatRange(0f, 400f);
            tier3.Filters.RainfallImportance = FilterImportance.Critical;
            tier3.Filters.GrowingDaysRange = new FloatRange(0f, 25f);
            tier3.Filters.GrowingDaysImportance = FilterImportance.Critical;
            tier3.Filters.PollutionRange = new FloatRange(0.3f, 1.0f);
            tier3.Filters.PollutionImportance = FilterImportance.Preferred;
            // No lava features - rely on temperature/rainfall/pollution for volcanic/hostile feel
            preset.FallbackTiers.Add(tier3);

            return preset;
        }

        // ===== SAVANNAH PRESET: Wildlife plains =====
        private static Preset CreateSavannahPreset()
        {
            var preset = new Preset
            {
                Id = "savannah",
                Name = "Savannah",
                Description = "Warm grasslands with abundant wildlife, wind, open terrain - perfect for hunting, herding, and wind power.",
                Category = "Curated",
                TargetRarity = TileRarity.Common,
                FilterSummary = "Wildlife | Warm | Grasslands | Grazing"
            };

            var filters = preset.Filters;

            // Climate: Warm savannah range (Critical)
            filters.AverageTemperatureRange = new FloatRange(22f, 38f);
            filters.AverageTemperatureImportance = FilterImportance.Critical;
            filters.RainfallRange = new FloatRange(500f, 1200f);
            filters.RainfallImportance = FilterImportance.Critical;
            filters.GrowingDaysRange = new FloatRange(45f, 60f);
            filters.GrowingDaysImportance = FilterImportance.Critical;

            // Biomes: Grassland types (Critical, OR)
            // Note: Would use multi-biome container if available
            // Target: Grasslands (5.9%), AridShrubland (14.9%), TemperateForest (30%)

            // Wildlife (Critical, OR): Abundant animals
            filters.MapFeatures.SetImportance("AnimalLife_Increased", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("AnimalHabitat", FilterImportance.Critical);
            filters.MapFeatures.Operator = ImportanceOperator.OR;

            // Animal Density: High animal population (Preferred)
            filters.AnimalDensityRange = new FloatRange(3.0f, 6.5f);
            filters.AnimalDensityImportance = FilterImportance.Preferred;

            // Grazing: Animals can graze (Preferred)
            filters.GrazeImportance = FilterImportance.Preferred;

            // Wind (Preferred): Wind turbines
            filters.MapFeatures.SetImportance("WindyMutator", FilterImportance.Preferred);

            // Terrain: Open plains (Preferred)
            filters.AllowedHilliness.Clear();
            filters.AllowedHilliness.Add(Hilliness.Flat);
            filters.AllowedHilliness.Add(Hilliness.SmallHills);

            filters.MovementDifficultyRange = new FloatRange(0.0f, 1.0f);
            filters.MovementDifficultyImportance = FilterImportance.Preferred;

            // Supporting Features (Preferred, OR): Water for animals
            filters.Rivers.SetImportance("HugeRiver", FilterImportance.Preferred);
            filters.Rivers.SetImportance("LargeRiver", FilterImportance.Preferred);
            filters.Rivers.SetImportance("River", FilterImportance.Preferred);
            filters.Rivers.Operator = ImportanceOperator.OR;

            filters.MapFeatures.SetImportance("WildPlants", FilterImportance.Preferred);

            return preset;
        }

        private static Preset CreateAquaticPreset()
        {
            var preset = new Preset
            {
                Id = "aquatic",
                Name = "Aquatic",
                Description = "Maximum water access - coastal tiles with rivers, lakes, headwaters. Fish, trade, and naval supremacy.",
                Category = "Curated",
                TargetRarity = TileRarity.Rare,
                FilterSummary = "Coastal | Rivers | Lakes | Fish"
            };

            var filters = preset.Filters;

            // Major Water Source: Coastal OR rivers (Critical) - symmetric water requirement
            filters.WaterAccessImportance = FilterImportance.Critical;

            // Additional Water (Preferred): Bonus scoring for specific water features
            filters.CoastalLakeImportance = FilterImportance.Preferred; // Lakes
            filters.Rivers.SetImportance("HugeRiver", FilterImportance.Preferred); // Big rivers bonus
            filters.Rivers.SetImportance("LargeRiver", FilterImportance.Preferred);
            filters.Rivers.Operator = ImportanceOperator.OR;

            // Aquatic Mutators (Preferred, OR): Water features everywhere
            filters.MapFeatures.SetImportance("Headwater", FilterImportance.Preferred);        // River source
            filters.MapFeatures.SetImportance("RiverDelta", FilterImportance.Preferred);       // Multiple rivers
            filters.MapFeatures.SetImportance("RiverConfluence", FilterImportance.Preferred);  // Rivers meet
            filters.MapFeatures.SetImportance("RiverIsland", FilterImportance.Preferred);      // Island in river
            filters.MapFeatures.SetImportance("Lake", FilterImportance.Preferred);             // Lakes
            filters.MapFeatures.SetImportance("LakeWithIsland", FilterImportance.Preferred);   // Lake features
            filters.MapFeatures.SetImportance("LakeWithIslands", FilterImportance.Preferred);  // Multiple islands
            filters.MapFeatures.SetImportance("Lakeshore", FilterImportance.Preferred);        // Lake borders
            filters.MapFeatures.SetImportance("Pond", FilterImportance.Preferred);             // Small water
            filters.MapFeatures.SetImportance("Bay", FilterImportance.Preferred);              // Coastal bay
            filters.MapFeatures.SetImportance("Cove", FilterImportance.Preferred);             // Coastal cove
            filters.MapFeatures.SetImportance("Harbor", FilterImportance.Preferred);           // Natural harbor
            filters.MapFeatures.SetImportance("Fjord", FilterImportance.Preferred);            // Coastal inlet
            filters.MapFeatures.Operator = ImportanceOperator.OR;

            // Fishing: Abundant fish (Preferred)
            filters.MapFeatures.SetImportance("Fish_Increased", FilterImportance.Preferred);
            filters.FishPopulationRange = new FloatRange(400f, 900f);
            filters.FishPopulationImportance = FilterImportance.Preferred;

            // Biomes: Near water (Preferred, OR)
            // Note: Would use multi-biome container if available
            // Target: TemperateForest, TropicalRainforest, Grasslands

            // Climate: Temperate for living (Preferred)
            filters.AverageTemperatureRange = new FloatRange(10f, 30f);
            filters.AverageTemperatureImportance = FilterImportance.Preferred;
            filters.RainfallRange = new FloatRange(1000f, 2500f);
            filters.RainfallImportance = FilterImportance.Preferred;

            return preset;
        }

        private static Preset CreateDesertOasisPreset()
        {
            var preset = new Preset
            {
                Id = "desert_oasis",
                Name = "Desert Oasis",
                Description = "Water in the wasteland - desert tiles with life-sustaining rivers. Coastal access is a bonus!",
                Category = "Curated",
                TargetRarity = TileRarity.Rare,
                FilterSummary = "Desert | Rivers | Hot"
            };

            var filters = preset.Filters;

            // Climate: Hot desert (Critical)
            filters.AverageTemperatureRange = new FloatRange(28f, 45f);
            filters.AverageTemperatureImportance = FilterImportance.Critical;
            filters.RainfallRange = new FloatRange(100f, 600f);
            filters.RainfallImportance = FilterImportance.Critical;

            // Biomes: Desert types (Critical, OR)
            // Note: Would use multi-biome container if available
            // Target: Desert (14.1%), ExtremeDesert (4.2%), AridShrubland (14.9%)

            // WATER FEATURES - This is what makes it an oasis!
            // Water access: Rivers OR coastal (Critical) - symmetric water requirement
            filters.WaterAccessImportance = FilterImportance.Critical;

            // Additional Water (Preferred): Bonus for specific oasis features
            filters.CoastalLakeImportance = FilterImportance.Preferred; // Lakes
            filters.Rivers.SetImportance("HugeRiver", FilterImportance.Preferred); // Big rivers bonus
            filters.Rivers.SetImportance("LargeRiver", FilterImportance.Preferred);
            filters.Rivers.Operator = ImportanceOperator.OR;

            // Oasis Features (Preferred, OR): Bonus for literal oasis features
            filters.MapFeatures.SetImportance("Oasis", FilterImportance.Preferred);       // Literal oasis mutator
            filters.MapFeatures.SetImportance("Lake", FilterImportance.Preferred);        // Water source
            filters.MapFeatures.SetImportance("Pond", FilterImportance.Preferred);        // Small water
            filters.MapFeatures.SetImportance("WetClimate", FilterImportance.Preferred);  // Moisture pocket
            filters.MapFeatures.Operator = ImportanceOperator.OR;

            // Positive Mutators (Preferred, OR): Make desert livable
            filters.MapFeatures.SetImportance("Fertile", FilterImportance.Preferred);     // Farming in desert
            filters.MapFeatures.SetImportance("WildPlants", FilterImportance.Preferred);  // Forage despite heat

            return preset;
        }

        // ===== DEFENSE PRESET: Mountain fortress =====
        private static Preset CreateDefensePreset()
        {
            var preset = new Preset
            {
                Id = "defense",
                Name = "Defense",
                Description = "Natural defensibility - mountains, caves, chokepoints, stone abundance. Build an impenetrable fortress.",
                Category = "Curated",
                TargetRarity = TileRarity.Uncommon,
                FilterSummary = "Mountainous | Caves | Granite+Slate | Chokepoints"
            };

            var filters = preset.Filters;

            // Terrain: Mountainous (Critical)
            filters.AllowedHilliness.Clear();
            filters.AllowedHilliness.Add(Hilliness.LargeHills);
            filters.AllowedHilliness.Add(Hilliness.Mountainous);

            // Fortification Features (Critical, OR): Mountain and underground
            filters.MapFeatures.SetImportance("Mountain", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("Caves", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("Cavern", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("Chasm", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("Cliffs", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("Valley", FilterImportance.Critical);
            filters.MapFeatures.Operator = ImportanceOperator.OR;

            // Resources: Advanced ores for fortifications (Critical, OR)
            filters.Stones.SetImportance("MineablePlasteel", FilterImportance.Critical);  // Advanced armor/walls
            filters.Stones.SetImportance("MineableComponentsIndustrial", FilterImportance.Critical); // Tech components
            filters.Stones.Operator = ImportanceOperator.OR;  // Accept either (tiles only have 1 ore)

            // Mining bonus (Preferred)
            filters.MapFeatures.SetImportance("MineralRich", FilterImportance.Preferred);

            // Additional Defense (Preferred, OR): Water-based defensibility
            filters.MapFeatures.SetImportance("Peninsula", FilterImportance.Preferred);     // 3 sides water
            filters.MapFeatures.SetImportance("RiverIsland", FilterImportance.Preferred);   // River moat
            filters.MapFeatures.SetImportance("CoastalIsland", FilterImportance.Preferred); // Ocean moat

            // Climate: Temperate for livability (Preferred)
            filters.AverageTemperatureRange = new FloatRange(10f, 25f);
            filters.AverageTemperatureImportance = FilterImportance.Preferred;
            filters.GrowingDaysRange = new FloatRange(30f, 60f);
            filters.GrowingDaysImportance = FilterImportance.Preferred;

            return preset;
        }

        private static Preset CreateAgrarianPreset()
        {
            var preset = new Preset
            {
                Id = "agrarian",
                Name = "Agrarian",
                Description = "Maximum agricultural potential - fertile soil, perfect climate, water abundance, year-round growing. Feed the world.",
                Category = "Curated",
                TargetRarity = TileRarity.Common,
                FilterSummary = "50-60 Grow Days | Fertile | High Rain | Flat"
            };

            var filters = preset.Filters;

            // Climate: Optimal crop conditions (Critical)
            filters.AverageTemperatureRange = new FloatRange(15f, 28f);
            filters.AverageTemperatureImportance = FilterImportance.Critical;
            filters.RainfallRange = new FloatRange(1200f, 2500f);
            filters.RainfallImportance = FilterImportance.Critical;
            filters.GrowingDaysRange = new FloatRange(50f, 60f);
            filters.GrowingDaysImportance = FilterImportance.Critical;

            // Biomes: Best farming biomes (Preferred, OR)
            // Note: Would use multi-biome container if available
            // Target: TemperateForest (30%), TropicalRainforest (11.7%), Grasslands (5.9%)

            // Farming Mutators (Preferred, OR): Agricultural bonuses
            filters.MapFeatures.SetImportance("Fertile", FilterImportance.Preferred);          // THE farming mutator
            filters.MapFeatures.SetImportance("WetClimate", FilterImportance.Preferred);       // Moisture for crops
            filters.MapFeatures.SetImportance("PlantLife_Increased", FilterImportance.Preferred); // More plants
            filters.MapFeatures.SetImportance("WildPlants", FilterImportance.Preferred);       // Forage backup
            filters.MapFeatures.SetImportance("Muddy", FilterImportance.Preferred);            // Farming bonus
            filters.MapFeatures.Operator = ImportanceOperator.OR;

            // Water (Preferred, OR): Irrigation and fishing
            filters.Rivers.SetImportance("HugeRiver", FilterImportance.Preferred);
            filters.Rivers.SetImportance("LargeRiver", FilterImportance.Preferred);
            filters.Rivers.SetImportance("River", FilterImportance.Preferred);
            filters.Rivers.Operator = ImportanceOperator.OR;
            filters.CoastalImportance = FilterImportance.Preferred;
            filters.CoastalLakeImportance = FilterImportance.Preferred;

            // Terrain: Easy to farm (Preferred)
            filters.AllowedHilliness.Clear();
            filters.AllowedHilliness.Add(Hilliness.Flat);
            filters.AllowedHilliness.Add(Hilliness.SmallHills);

            filters.MovementDifficultyRange = new FloatRange(0.0f, 1.2f);
            filters.MovementDifficultyImportance = FilterImportance.Preferred;

            return preset;
        }

        private static Preset CreatePowerPreset()
        {
            var preset = new Preset
            {
                Id = "power",
                Name = "Power",
                Description = "Maximum power generation - geothermal, hydro, wind, solar. Energy independence through diversified infrastructure.",
                Category = "Curated",
                TargetRarity = TileRarity.Rare,
                FilterSummary = "Geothermal | Rivers | Wind | Solar"
            };

            var filters = preset.Filters;

            // Geothermal (Critical, OR): Primary power source
            filters.MapFeatures.SetImportance("SteamGeysers_Increased", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("AncientHeatVent", FilterImportance.Critical);
            filters.MapFeatures.Operator = ImportanceOperator.OR;

            // Hydro (Preferred, OR): Watermill potential
            filters.Rivers.SetImportance("HugeRiver", FilterImportance.Preferred);
            filters.Rivers.SetImportance("LargeRiver", FilterImportance.Preferred);
            filters.Rivers.Operator = ImportanceOperator.OR;

            filters.MapFeatures.SetImportance("RiverDelta", FilterImportance.Preferred);      // Multiple rivers
            filters.MapFeatures.SetImportance("RiverConfluence", FilterImportance.Preferred); // Rivers meet
            filters.MapFeatures.SetImportance("Headwater", FilterImportance.Preferred);       // River source

            // Wind (Preferred): Wind turbines
            filters.MapFeatures.SetImportance("WindyMutator", FilterImportance.Preferred);
            filters.AllowedHilliness.Clear();
            filters.AllowedHilliness.Add(Hilliness.LargeHills);
            filters.AllowedHilliness.Add(Hilliness.Mountainous);

            // Solar (Preferred): Solar panels
            filters.MapFeatures.SetImportance("SunnyMutator", FilterImportance.Preferred);
            filters.RainfallRange = new FloatRange(400f, 1200f);
            filters.RainfallImportance = FilterImportance.Preferred;

            // Climate: Manageable heat/cold loads (Preferred)
            filters.AverageTemperatureRange = new FloatRange(10f, 30f);
            filters.AverageTemperatureImportance = FilterImportance.Preferred;

            // Geography: Tidal potential (future)
            filters.CoastalImportance = FilterImportance.Preferred;
            filters.CoastalLakeImportance = FilterImportance.Preferred;

            // Uranium: Nuclear power potential (Preferred)
            filters.Stones.SetImportance("MineableUranium", FilterImportance.Preferred);

            return preset;
        }

        private static Preset CreateBayouPreset()
        {
            var preset = new Preset
            {
                Id = "bayou",
                Name = "Bayou",
                Description = "Hot, wet, diseased marshlands - swamps, mud, difficult terrain, overgrown vegetation. Swamp horror survival.",
                Category = "Curated",
                TargetRarity = TileRarity.Uncommon,
                FilterSummary = "Swampy | Hot+Wet | Muddy | Overgrown"
            };

            var filters = preset.Filters;

            // Climate: Hot and humid (Critical)
            filters.AverageTemperatureRange = new FloatRange(25f, 40f);
            filters.AverageTemperatureImportance = FilterImportance.Critical;
            filters.RainfallRange = new FloatRange(1800f, 3000f);
            filters.RainfallImportance = FilterImportance.Critical;

            // Swampiness: THIS is the key stat for swamps! (Critical)
            filters.SwampinessRange = new FloatRange(0.4f, 1.0f);
            filters.SwampinessImportance = FilterImportance.Critical;

            // Biomes: Swamp biomes (Critical, OR)
            // Note: Would use multi-biome container if available
            // Target: TemperateSwamp (1.2%), TropicalSwamp (0.6%), ColdBog (0.11%)

            // Swamp Mutators (Preferred, OR): Swampy terrain
            filters.MapFeatures.SetImportance("Muddy", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Marshy", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Wetland", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("WetClimate", FilterImportance.Preferred);
            filters.MapFeatures.Operator = ImportanceOperator.OR;

            // Geographic Features (Preferred, OR): Swamps have water
            filters.Rivers.SetImportance("HugeRiver", FilterImportance.Preferred);
            filters.Rivers.SetImportance("LargeRiver", FilterImportance.Preferred);
            filters.Rivers.SetImportance("River", FilterImportance.Preferred);
            filters.Rivers.Operator = ImportanceOperator.OR;

            filters.MapFeatures.SetImportance("Lakeshore", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Pond", FilterImportance.Preferred);

            // Flora/Fauna (Preferred, OR): Overgrown swamp life
            filters.MapFeatures.SetImportance("PlantLife_Increased", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("WildTropicalPlants", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("AnimalLife_Increased", FilterImportance.Preferred);

            // Terrain: Low-lying swamps (Preferred)
            filters.AllowedHilliness.Clear();
            filters.AllowedHilliness.Add(Hilliness.Flat);
            filters.AllowedHilliness.Add(Hilliness.SmallHills);

            filters.MovementDifficultyRange = new FloatRange(1.2f, 2.0f);
            filters.MovementDifficultyImportance = FilterImportance.Preferred;

            return preset;
        }

        // ===== WILDCARD PRESET: Random chaos challenge =====
        private static Preset CreateHomesteaderPreset()
        {
            var preset = new Preset
            {
                Id = "homesteader",
                Name = "Homesteader",
                Description = "Move-in ready! Find abandoned settlements and ancient structures with existing buildings to salvage.",
                Category = "Curated",
                TargetRarity = TileRarity.VeryRare,
                FilterSummary = "Ruins | Abandoned Colonies | Ancient Sites"
            };

            var filters = preset.Filters;

            // Climate: Temperate (livable)
            filters.AverageTemperatureRange = new FloatRange(10f, 30f);
            filters.AverageTemperatureImportance = FilterImportance.Preferred;
            filters.GrowingDaysRange = new FloatRange(30f, 60f);
            filters.GrowingDaysImportance = FilterImportance.Preferred;

            // Abandoned settlements (Critical - must have one)
            filters.MapFeatures.SetImportance("AbandonedColonyTribal", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("AbandonedColonyOutlander", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("Stockpile", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("AncientRuins", FilterImportance.Critical);
            filters.MapFeatures.Operator = ImportanceOperator.OR; // Need at least one

            // Ancient structures (Preferred - bonus for having these too)
            filters.MapFeatures.SetImportance("AncientWarehouse", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("AncientQuarry", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("AncientGarrison", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("AncientLaunchSite", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("AncientUplink", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("AncientChemfuelRefinery", FilterImportance.Preferred);

            // Supporting: Roads (ancient sites often had access)
            filters.Roads.SetImportance("DirtRoad", FilterImportance.Preferred);
            filters.Roads.SetImportance("DirtPath", FilterImportance.Preferred);
            filters.Roads.SetImportance("StoneRoad", FilterImportance.Preferred);
            filters.Roads.SetImportance("AncientAsphaltRoad", FilterImportance.Preferred);
            filters.Roads.SetImportance("AncientAsphaltHighway", FilterImportance.Preferred);
            filters.Roads.Operator = ImportanceOperator.OR;

            // Quality overrides: Ancient sites are valuable despite dangers
            preset.MutatorQualityOverrides["AbandonedColonyTribal"] = 6;      // -5 → +6
            preset.MutatorQualityOverrides["AbandonedColonyOutlander"] = 7;   // -5 → +7
            preset.MutatorQualityOverrides["AncientGarrison"] = 6;            // -8 → +6
            preset.MutatorQualityOverrides["AncientLaunchSite"] = 6;          // -8 → +6
            preset.MutatorQualityOverrides["AncientChemfuelRefinery"] = 4;    // -8 → +4
            preset.MutatorQualityOverrides["AncientWarehouse"] = 5;           // 0 → +5
            preset.MutatorQualityOverrides["AncientQuarry"] = 5;              // 0 → +5

            // Industrial ruins theme: Junk, pollution, and toxicity are thematic and valuable
            preset.MutatorQualityOverrides["Junkyard"] = 4;                   // -5 → +4
            preset.MutatorQualityOverrides["Pollution_Increased"] = 3;        // -8 → +3
            preset.MutatorQualityOverrides["AncientToxVent"] = 4;             // -10 → +4

            // Resources: Plasteel for industrial salvage and construction (Preferred)
            filters.Stones.SetImportance("MineablePlasteel", FilterImportance.Preferred);

            return preset;
        }

        // ========== TESTING PRESETS (TEMPORARY) ==========
        // These presets are for validating stockpile scoring and mineral cache behavior.
        // They should be removed after testing is complete.

        /// <summary>
        /// TEST: Component stockpile detection and scoring
        /// </summary>
        private static Preset CreateTestComponentStockpilePreset()
        {
            var preset = new Preset
            {
                Id = "test_component_stockpile",
                Name = "[TEST] Component",
                Description = "Testing preset: Finds tiles with Component stockpiles (Critical). Expected: 5-10 tiles globally.",
                Category = "Testing",
                TargetRarity = TileRarity.VeryRare,
                FilterSummary = "Component Stockpile (Critical)"
            };

            var filters = preset.Filters;

            // Stockpile: Component (Critical)
            filters.Stockpiles.SetImportance("Component", FilterImportance.Critical);

            return preset;
        }

        /// <summary>
        /// TEST: Weapons stockpile detection and scoring
        /// </summary>
        private static Preset CreateTestWeaponsStockpilePreset()
        {
            var preset = new Preset
            {
                Id = "test_weapons_stockpile",
                Name = "[TEST] Weapons",
                Description = "Testing preset: Finds tiles with Weapons stockpiles (Critical). Expected: 10-15 tiles globally. Should show 'Weapons: Quality +8' in dumps.",
                Category = "Testing",
                TargetRarity = TileRarity.VeryRare,
                FilterSummary = "Weapons Stockpile (Critical)"
            };

            var filters = preset.Filters;

            // Stockpile: Weapons (Critical)
            filters.Stockpiles.SetImportance("Weapons", FilterImportance.Critical);

            return preset;
        }

        /// <summary>
        /// TEST: Medicine stockpile detection and scoring
        /// </summary>
        private static Preset CreateTestMedicineStockpilePreset()
        {
            var preset = new Preset
            {
                Id = "test_medicine_stockpile",
                Name = "[TEST] Medicine",
                Description = "Testing preset: Finds tiles with Medicine stockpiles (Critical). Expected: 8-12 tiles globally. Should show 'Medicine: Quality +7' in dumps.",
                Category = "Testing",
                TargetRarity = TileRarity.VeryRare,
                FilterSummary = "Medicine Stockpile (Critical)"
            };

            var filters = preset.Filters;

            // Stockpile: Medicine (Critical)
            filters.Stockpiles.SetImportance("Medicine", FilterImportance.Critical);

            return preset;
        }

        /// <summary>
        /// TEST: MineableSteel validation (now valid - core construction resource)
        /// This preset uses MineableSteel to verify it's correctly included in the whitelist.
        /// Expected behavior: Returns tiles with compacted steel (MineralRich → MineableSteel)
        /// If your world has no MineableSteel tiles, this will return 0 results (expected).
        /// </summary>
        private static Preset CreateTestMineableSteelPreset()
        {
            var preset = new Preset
            {
                Id = "test_mineable_steel",
                Name = "[TEST] Steel",
                Description = "Testing preset: REQUIRES compacted steel (MineableSteel). Returns 0 results if world has no steel tiles. Check Player.log for cache distribution.",
                Category = "Testing",
                TargetRarity = TileRarity.VeryRare,
                FilterSummary = "MineableSteel (Critical, Valid)"
            };

            var filters = preset.Filters;

            // Stone: MineableSteel (Critical) - core construction resource (compacted steel)
            // Using Critical so Apply phase filters out non-steel tiles
            // If this returns 0 results, your world has no MineableSteel (check cache logs)
            filters.Stones.SetImportance("MineableSteel", FilterImportance.Critical);

            return preset;
        }
    }
}

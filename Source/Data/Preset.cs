#nullable enable
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
    public class FallbackTier : IExposable
    {
        public string Name { get; set; } = "";
        public FilterSettings Filters { get; set; } = new FilterSettings();

        public void ExposeData()
        {
            string name = Name;
            FilterSettings filters = Filters;

            Scribe_Values.Look(ref name, "name", "");
            Scribe_Deep.Look(ref filters, "filters");

            if (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Name = name ?? "";
                Filters = filters ?? new FilterSettings();
            }
        }
    }

    /// <summary>
    /// A preset configuration bundle that can be applied to Simple mode.
    /// Contains filter settings, metadata, and rarity targeting information.
    /// </summary>
    public class Preset : IExposable
    {
        public string Id { get; set; } = "";

        /// <summary>
        /// Raw name - may be translation key (for curated) or literal string (for user presets)
        /// Use GetDisplayName() for UI display.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Raw description - may be translation key or literal string.
        /// Use GetDisplayDescription() for UI display.
        /// </summary>
        public string Description { get; set; } = "";

        public string Category { get; set; } = "User"; // "Curated", "Special", "User"
        public TileRarity? TargetRarity { get; set; } = null;

        /// <summary>
        /// Raw filter summary - may be translation key or literal string.
        /// Use GetDisplayFilterSummary() for UI display.
        /// </summary>
        public string FilterSummary { get; set; } = "";

        /// <summary>
        /// Gets the display-ready name, translating if this is a translation key.
        /// </summary>
        public string GetDisplayName()
        {
            if (string.IsNullOrEmpty(Name)) return "";
            if (Name.StartsWith("LandingZone_") && LanguageDatabase.activeLanguage != null)
            {
                return Name.Translate();
            }
            return Name;
        }

        /// <summary>
        /// Gets the display-ready description, translating if this is a translation key.
        /// </summary>
        public string GetDisplayDescription()
        {
            if (string.IsNullOrEmpty(Description)) return "";
            if (Description.StartsWith("LandingZone_") && LanguageDatabase.activeLanguage != null)
            {
                return Description.Translate();
            }
            return Description;
        }

        /// <summary>
        /// Gets the display-ready filter summary, translating if this is a translation key.
        /// </summary>
        public string GetDisplayFilterSummary()
        {
            if (string.IsNullOrEmpty(FilterSummary)) return "";
            if (FilterSummary.StartsWith("LandingZone_") && LanguageDatabase.activeLanguage != null)
            {
                return FilterSummary.Translate();
            }
            return FilterSummary;
        }

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
        /// Validates preset requirements against the current runtime.
        /// Returns info about missing mutators that won't be available.
        /// </summary>
        public RuntimeValidationResult ValidateRequirements()
        {
            var result = new RuntimeValidationResult();

            // Check MapFeatures for mutators not in runtime
            foreach (var (defName, importance) in Filters.MapFeatures.ItemImportance)
            {
                // Skip if mutator exists in runtime
                if (MapFeatureFilter.IsRuntimeMutator(defName))
                    continue;

                // Get source info to identify what mod provides this
                // For missing mutators, source detection may return Core - infer from naming patterns
                var sourceInfo = MapFeatureFilter.GetMutatorSource(defName);
                string sourceName = InferSourceName(defName, sourceInfo);

                // Critical/MustHave = blocking, Priority/Preferred = warning only
                bool isBlocking = importance == FilterImportance.Critical || importance == FilterImportance.MustHave;

                result.MissingMutators.Add(new MissingMutatorInfo
                {
                    DefName = defName,
                    SourceName = sourceName,
                    Importance = importance,
                    IsBlocking = isBlocking
                });

                if (isBlocking)
                    result.HasBlockingMissing = true;
                else
                    result.HasWarningMissing = true;
            }

            // Check MutatorQualityOverrides (these are scoring bonuses, not blocking)
            if (MutatorQualityOverrides != null)
            {
                foreach (var defName in MutatorQualityOverrides.Keys)
                {
                    // Skip if already checked in MapFeatures or if exists in runtime
                    if (result.MissingMutators.Any(m => m.DefName == defName))
                        continue;
                    if (MapFeatureFilter.IsRuntimeMutator(defName))
                        continue;

                    var sourceInfo = MapFeatureFilter.GetMutatorSource(defName);
                    string sourceName = InferSourceName(defName, sourceInfo);

                    // Quality overrides are always non-blocking (scoring only)
                    result.MissingMutators.Add(new MissingMutatorInfo
                    {
                        DefName = defName,
                        SourceName = sourceName,
                        Importance = FilterImportance.Preferred, // Treat as preferred for display
                        IsBlocking = false
                    });
                    result.HasWarningMissing = true;
                }
            }

            return result;
        }

        /// <summary>
        /// Infers a user-friendly source name for a mutator based on naming conventions.
        /// If GetMutatorSource returns a mod/DLC, use that. Otherwise infer from prefix patterns.
        /// </summary>
        private static string InferSourceName(string defName, MapFeatureFilter.MutatorSourceInfo sourceInfo)
        {
            // If we got a real source (Mod or DLC), use it
            if (sourceInfo.Type == MapFeatureFilter.MutatorSourceType.Mod)
                return sourceInfo.SourceName;
            if (sourceInfo.Type == MapFeatureFilter.MutatorSourceType.DLC)
                return sourceInfo.SourceName;

            // For Core/unknown, try to infer from naming conventions
            if (defName.StartsWith("GL_"))
                return "Geological Landforms";
            if (defName.StartsWith("AB_"))
                return "Alpha Biomes";
            if (defName.StartsWith("VE_"))
                return "Vanilla Expanded";
            if (defName.StartsWith("RF_"))
                return "ReGrowth Framework";

            // Unknown source
            return "unknown mod";
        }

        /// <summary>
        /// Applies this preset's filters to the target FilterSettings.
        /// Preserves the user's MaxResults setting (presets don't override result limits).
        /// </summary>
        public void ApplyTo(FilterSettings target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            // Preserve user's MaxResults setting - presets shouldn't override result limits
            int userMaxResults = target.MaxResults;

            target.CopyFrom(Filters);

            // Restore user's MaxResults
            target.MaxResults = userMaxResults;
        }

        /// <summary>
        /// Serializes/deserializes preset data for persistence
        /// </summary>
        public void ExposeData()
        {
            // Use local variables for properties (Scribe requires ref to variables)
            string id = Id;
            string name = Name;
            string description = Description;
            string category = Category;
            TileRarity? targetRarity = TargetRarity;
            string filterSummary = FilterSummary;
            float? minimumStrictness = MinimumStrictness;
            FilterSettings filters = Filters;
            Dictionary<string, int> mutatorOverrides = MutatorQualityOverrides;
            List<FallbackTier>? fallbackTiers = FallbackTiers;

            Scribe_Values.Look(ref id, "id", "");
            Scribe_Values.Look(ref name, "name", "");
            Scribe_Values.Look(ref description, "description", "");
            Scribe_Values.Look(ref category, "category", "User");
            Scribe_Values.Look(ref targetRarity, "targetRarity", null);
            Scribe_Values.Look(ref filterSummary, "filterSummary", "");
            Scribe_Values.Look(ref minimumStrictness, "minimumStrictness", null);

            Scribe_Deep.Look(ref filters, "filters");
            Scribe_Collections.Look(ref mutatorOverrides, "mutatorQualityOverrides", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref fallbackTiers, "fallbackTiers", LookMode.Deep);

            // Write back to properties after loading
            if (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Id = id ?? "";
                Name = name ?? "";
                Description = description ?? "";
                Category = category ?? "User";
                TargetRarity = targetRarity;
                FilterSummary = filterSummary ?? "";
                MinimumStrictness = minimumStrictness;
                Filters = filters ?? new FilterSettings();
                MutatorQualityOverrides = mutatorOverrides ?? new Dictionary<string, int>();
                FallbackTiers = fallbackTiers;
            }
        }
    }

    /// <summary>
    /// Library of curated and user-created presets
    /// </summary>
    public static class PresetLibrary
    {
        private static List<Preset> _curated = new List<Preset>();
        private static bool _initialized = false;

        /// <summary>
        /// Initializes preset library with curated bundles
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            _curated = new List<Preset>
            {
                // Row 1: Balanced + Special presets (5 columns)
                CreateBalancedPreset(),   // NEW: Easy start for new players
                CreateElysianPreset(),
                CreateExoticPreset(),
                CreateSubZeroPreset(),
                CreateScorchedPreset(),

                // Row 2: Themed presets (5 columns)
                CreateSavannahPreset(),
                CreateAquaticPreset(),
                CreateDesertOasisPreset(),
                CreateDefensePreset(),
                CreateAnomalyPreset(),    // NEW: Horror experience

                // Row 3: Playstyle presets (5 columns)
                CreateAgrarianPreset(),
                CreatePowerPreset(),
                CreateBayouPreset(),
                CreateHomesteadPreset(),
                CreateTradeEmpirePreset() // NEW: Coastal trade focus
                // 12 original + 3 new = 15 total (5x3 grid)
            };

            _initialized = true;
            // Use hardcoded English for startup log - language system may not be loaded yet
            Log.Message($"[LandingZone] Preset library initialized with {_curated.Count} curated presets");
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
        /// Gets user-saved presets (globally persisted in ModSettings)
        /// </summary>
        public static IReadOnlyList<Preset> GetUserPresets()
        {
            // User presets are stored globally in ModSettings and persist across all saves
            return LandingZoneMod.Instance?.Settings?.UserPresets ?? new List<Preset>();
        }

        /// <summary>
        /// Gets a preset by ID (searches both curated and user presets)
        /// </summary>
        public static Preset? GetById(string id)
        {
            if (!_initialized) Initialize();

            // Search curated presets first
            var preset = _curated.FirstOrDefault(p => p.Id == id);
            if (preset != null) return preset;

            // Then search user presets
            return GetUserPresets().FirstOrDefault(p => p.Id == id);
        }

        /// <summary>
        /// Saves a fully-formed preset (e.g., from token import) preserving all fields.
        /// Returns true if saved successfully, false if name already exists.
        /// </summary>
        /// <param name="preset">Complete preset to save</param>
        public static bool SaveUserPreset(Preset preset)
        {
            var userPresets = LandingZoneMod.Instance?.Settings?.UserPresets;
            if (userPresets == null)
            {
                Log.Error("[LandingZone] Cannot save preset - mod settings not available");
                return false;
            }

            // Check for duplicate names
            if (userPresets.Any(p => p.Name.Equals(preset.Name, StringComparison.OrdinalIgnoreCase)))
            {
                Log.Warning("LandingZone_PresetAlreadyExists".Translate(preset.Name));
                return false;
            }

            // Ensure category is User and ID is unique
            preset.Category = "User";
            if (string.IsNullOrEmpty(preset.Id) || !preset.Id.StartsWith("user_"))
                preset.Id = $"user_{Guid.NewGuid():N}";

            userPresets.Add(preset);
            Log.Message("LandingZone_PresetSavedLog".Translate(preset.Name));

            // Persist to disk immediately
            LandingZoneMod.Instance?.WriteSettings();

            return true;
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
            var userPresets = LandingZoneMod.Instance?.Settings?.UserPresets;
            if (userPresets == null)
            {
                Log.Error("[LandingZone] Cannot save preset - mod settings not available");
                return false;
            }

            // Check for duplicate names
            if (userPresets.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                Log.Warning("LandingZone_PresetAlreadyExists".Translate(name));
                return false;
            }

            var preset = new Preset
            {
                Id = $"user_{Guid.NewGuid():N}",
                Name = name,
                Description = "LandingZone_UserCreatedPresetDesc", // Translation key - use GetDisplayDescription()
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
                Log.Message("LandingZone_PresetSavedWithOverrides".Translate(name, preset.MutatorQualityOverrides.Count));
            }

            userPresets.Add(preset);
            Log.Message("LandingZone_PresetSavedLog".Translate(name));

            // Persist to disk immediately
            LandingZoneMod.Instance?.WriteSettings();

            return true;
        }

        /// <summary>
        /// Deletes a user preset by name
        /// </summary>
        public static bool DeleteUserPreset(string name)
        {
            var userPresets = LandingZoneMod.Instance?.Settings?.UserPresets;
            if (userPresets == null)
            {
                Log.Error("[LandingZone] Cannot delete preset - mod settings not available");
                return false;
            }

            var preset = userPresets.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (preset != null)
            {
                userPresets.Remove(preset);
                Log.Message("LandingZone_PresetDeleted".Translate(name));

                // Persist to disk immediately
                LandingZoneMod.Instance?.WriteSettings();

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

        // ===== BALANCED PRESET: Easy start for new players =====
        // DESIGN NOTE: Layered design - great with Core, deeper with DLC/mods
        // Purpose: Year-round growing, defensible terrain, water access, trade routes
        private static Preset CreateBalancedPreset()
        {
            var preset = new Preset
            {
                Id = "balanced",
                Name = "LandingZone_Preset_balanced_Name",
                Description = "LandingZone_Preset_balanced_Description",
                Category = "Curated",
                TargetRarity = TileRarity.Common,
                FilterSummary = "LandingZone_Preset_balanced_FilterSummary"
            };

            var filters = preset.Filters;

            // === CORE LAYER (Always Works) ===

            // Growing: Year-round farming guaranteed (Critical)
            filters.GrowingDaysRange = new FloatRange(55f, 60f);
            filters.GrowingDaysImportance = FilterImportance.Critical;

            // Terrain: Defensible hilliness - NOT flat (Critical via allowed hilliness)
            filters.AllowedHilliness.Clear();
            filters.AllowedHilliness.Add(Hilliness.SmallHills);
            filters.AllowedHilliness.Add(Hilliness.LargeHills);
            filters.AllowedHilliness.Add(Hilliness.Mountainous);

            // Water: Rivers for water access (Critical, OR)
            filters.Rivers.SetImportance("HugeRiver", FilterImportance.Critical);
            filters.Rivers.SetImportance("LargeRiver", FilterImportance.Critical);
            filters.Rivers.SetImportance("River", FilterImportance.Critical);
            filters.Rivers.Operator = ImportanceOperator.OR;

            // Defensible terrain feature (Critical, OR)
            filters.MapFeatures.SetImportance("Mountain", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("Caves", FilterImportance.Critical);
            filters.MapFeatures.Operator = ImportanceOperator.OR;

            // Climate: Comfortable temperature (Preferred)
            filters.AverageTemperatureRange = new FloatRange(10f, 25f);
            filters.AverageTemperatureImportance = FilterImportance.Preferred;

            // Rainfall: Good plant growth (Preferred)
            filters.RainfallRange = new FloatRange(800f, 1800f);
            filters.RainfallImportance = FilterImportance.Preferred;

            // Roads: Basic trade access (Preferred, OR)
            filters.Roads.SetImportance("DirtRoad", FilterImportance.Preferred);
            filters.Roads.SetImportance("StoneRoad", FilterImportance.Preferred);
            filters.Roads.SetImportance("AncientAsphaltRoad", FilterImportance.Preferred);
            filters.Roads.Operator = ImportanceOperator.OR;

            // Coastal: Fishing bonus (Preferred)
            filters.CoastalImportance = FilterImportance.Preferred;
            filters.CoastalLakeImportance = FilterImportance.Preferred;

            // === DLC LAYER (Enhanced with DLC) ===
            // Priority features - high scoring but not required

            // [Core/Anomaly] Geothermal power
            filters.MapFeatures.SetImportance("SteamGeysers_Increased", FilterImportance.Priority);

            // [Core] Fertile land for better farming
            filters.MapFeatures.SetImportance("Fertile", FilterImportance.Priority);

            // [Core/Ideology] Exploration opportunities
            filters.MapFeatures.SetImportance("AncientRuins", FilterImportance.Priority);

            // === MOD LAYER (Bonus with Mods) ===
            // [Geological Landforms] Terrain variety - Preferred
            filters.MapFeatures.SetImportance("Valley", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Basin", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Hollow", FilterImportance.Preferred);

            return preset;
        }

        // ===== ELYSIAN PRESET: Perfect everything - highest quality of life =====
        // DESIGN NOTE: Layered design - great with Core, paradise with DLC/mods
        // Philosophy: Year-round farming + comfortable temps = anchor. Everything else enriches.
        // Key Fix: Loosened Critical gates from 5 to 2 (was: temp + rain + grow + pollution + mutators)
        private static Preset CreateElysianPreset()
        {
            var preset = new Preset
            {
                Id = "elysian",
                Name = "LandingZone_Preset_elysian_Name",
                Description = "LandingZone_Preset_elysian_Description",
                Category = "Special",
                TargetRarity = TileRarity.Epic,
                FilterSummary = "LandingZone_Preset_elysian_FilterSummary"
            };

            var filters = preset.Filters;

            // === CORE LAYER (Critical Anchors - Always Works) ===

            // Year-round farming - THE defining feature (Critical)
            filters.GrowingDaysRange = new FloatRange(55f, 60f);
            filters.GrowingDaysImportance = FilterImportance.Critical;

            // Comfortable temperature (Critical)
            filters.AverageTemperatureRange = new FloatRange(18f, 25f);
            filters.AverageTemperatureImportance = FilterImportance.Critical;

            // === DLC LAYER (Priority - high scoring, not gates) ===

            // Perfect rainfall (downgraded from Critical)
            filters.RainfallRange = new FloatRange(1400f, 2200f);
            filters.RainfallImportance = FilterImportance.Priority;

            // Clean air (downgraded from Critical)
            filters.PollutionRange = new FloatRange(0f, 0.1f);
            filters.PollutionImportance = FilterImportance.Priority;

            // +10 Mutators (Priority, OR) - High value features but not required
            filters.MapFeatures.SetImportance("Fertile", FilterImportance.Priority);               // Best farming
            filters.MapFeatures.SetImportance("MineralRich", FilterImportance.Priority);           // Mining bonus
            filters.MapFeatures.SetImportance("SteamGeysers_Increased", FilterImportance.Priority); // Free power
            filters.MapFeatures.SetImportance("AncientHeatVent", FilterImportance.Priority);       // Heat + power
            filters.MapFeatures.SetImportance("HotSprings", FilterImportance.Priority);            // Unique feature
            filters.MapFeatures.Operator = ImportanceOperator.OR;

            // [DLC] Loot opportunities (Priority)
            filters.MapFeatures.SetImportance("AncientWarehouse", FilterImportance.Priority);      // Loot cache
            filters.MapFeatures.SetImportance("Stockpile", FilterImportance.Priority);             // Supplies

            // === MOD LAYER (Preferred - bonus scoring) ===

            // +8 Mutators (Preferred) - Secondary bonuses
            filters.MapFeatures.SetImportance("PlantLife_Increased", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Fish_Increased", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("AnimalLife_Increased", FilterImportance.Preferred);

            // +7 Mutators (Preferred)
            filters.MapFeatures.SetImportance("WetClimate", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Wetland", FilterImportance.Preferred);

            // +5/+6 Mutators (Preferred)
            filters.MapFeatures.SetImportance("Caves", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("WildPlants", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Muddy", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("River", FilterImportance.Preferred);

            // [Geological Landforms] Sheltered terrain (Preferred)
            filters.MapFeatures.SetImportance("Valley", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Basin", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Hollow", FilterImportance.Preferred);

            // [Geological Landforms] Scenic water (Preferred)
            filters.MapFeatures.SetImportance("Harbor", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Bay", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Cove", FilterImportance.Preferred);

            // [Alpha Biomes] Paradise biomes (Preferred)
            filters.MapFeatures.SetImportance("AB_IdyllicMeadows", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("AB_GallatrossGraveyard", FilterImportance.Preferred);

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
            filters.Stones.SetImportance("MineablePlasteel", FilterImportance.Preferred);
            filters.Stones.SetImportance("MineableGold", FilterImportance.Preferred);
            filters.Stones.Operator = ImportanceOperator.OR;

            // Resource ranges: Abundant everything (Preferred)
            filters.ForageabilityRange = new FloatRange(0.7f, 1.0f);
            filters.ForageImportance = FilterImportance.Preferred;
            filters.PlantDensityRange = new FloatRange(0.8f, 1.3f);
            filters.PlantDensityImportance = FilterImportance.Preferred;
            filters.AnimalDensityRange = new FloatRange(2.5f, 6.5f);
            filters.AnimalDensityImportance = FilterImportance.Preferred;
            filters.FishPopulationRange = new FloatRange(400f, 900f);
            filters.FishPopulationImportance = FilterImportance.Preferred;

            // Quality Overrides: Boost paradise features
            preset.MutatorQualityOverrides["AB_IdyllicMeadows"] = 10;    // Paradise biome
            preset.MutatorQualityOverrides["AB_GallatrossGraveyard"] = 7; // Unique fauna
            preset.MutatorQualityOverrides["Valley"] = 6;                // Sheltered
            preset.MutatorQualityOverrides["Basin"] = 5;                 // Sheltered
            preset.MutatorQualityOverrides["Harbor"] = 6;                // Scenic water
            preset.MutatorQualityOverrides["Bay"] = 5;                   // Scenic water

            return preset;
        }

        // ===== EXOTIC PRESET: Ultra-rare feature hunter =====
        // DESIGN NOTE: Layered rare hunting - works with Core, richer with DLC/mods
        //
        // Philosophy: Hunt for ANY ultra-rare feature. More DLCs/mods = more rare options = richer experience.
        // Key Design:
        // 1. NO Critical gates - all rare features are Priority with OR (guarantees results)
        // 2. Core rares (Cavern, HotSprings, Oasis, MineralRich) exist in every world
        // 3. DLC enriches: Biotech adds ArcheanTrees, Anomaly adds lava features
        // 4. Mods expand: GL adds geographic rares, AB adds biome-specific rares
        // 5. Stacking scores highest: quality overrides reward tiles with multiple rares
        private static Preset CreateExoticPreset()
        {
            var preset = new Preset
            {
                Id = "exotic",
                Name = "LandingZone_Preset_exotic_Name",
                Description = "LandingZone_Preset_exotic_Description",
                Category = "Special",
                TargetRarity = TileRarity.Epic,
                FilterSummary = "LandingZone_Preset_exotic_FilterSummary"
            };

            var filters = preset.Filters;

            // Climate: Keep broad to maximize rare feature finds (Preferred)
            filters.AverageTemperatureRange = new FloatRange(5f, 35f);
            filters.AverageTemperatureImportance = FilterImportance.Preferred;

            // === CORE LAYER (Always Works) ===
            // Core rare features - Priority (OR) - these exist in vanilla
            filters.MapFeatures.SetImportance("Cavern", FilterImportance.Priority);         // 0.022%, 157 tiles
            filters.MapFeatures.SetImportance("HotSprings", FilterImportance.Priority);     // 0.0094%, 66 tiles
            filters.MapFeatures.SetImportance("Oasis", FilterImportance.Priority);          // 0.0094%, 66 tiles
            filters.MapFeatures.SetImportance("MineralRich", FilterImportance.Priority);    // 0.017%, 119 tiles
            filters.MapFeatures.Operator = ImportanceOperator.OR;  // Any rare feature matches

            // === DLC LAYER (Enhanced with DLC) ===
            // [Biotech] Ultra-rare features - Priority (contribute to scoring, don't gate)
            filters.MapFeatures.SetImportance("ArcheanTrees", FilterImportance.Priority);   // 0.0034%, 24 tiles - THE rarest

            // [Anomaly] Volcanic rares - Priority
            filters.MapFeatures.SetImportance("LavaCaves", FilterImportance.Priority);      // Very rare
            filters.MapFeatures.SetImportance("TerraformingScar", FilterImportance.Priority); // Alien terrain

            // === MOD LAYER (Bonus with Mods) ===
            // [Geological Landforms] Geographic rares - Preferred (bonus scoring)
            filters.MapFeatures.SetImportance("RiverDelta", FilterImportance.Preferred);    // 0.023%, 159 tiles
            filters.MapFeatures.SetImportance("Peninsula", FilterImportance.Preferred);     // 0.021%, 148 tiles
            filters.MapFeatures.SetImportance("Headwater", FilterImportance.Preferred);     // 0.25%, 1745 tiles
            filters.MapFeatures.SetImportance("Fjord", FilterImportance.Preferred);         // Coastal inlet
            filters.MapFeatures.SetImportance("RiverConfluence", FilterImportance.Preferred); // Rivers meet

            // Geography: Unusual combinations (Preferred)
            filters.CoastalLakeImportance = FilterImportance.Preferred;
            filters.Rivers.SetImportance("HugeRiver", FilterImportance.Preferred);
            filters.Rivers.Operator = ImportanceOperator.OR;

            // No fallback tiers needed - Priority OR guarantees results from Core rares

            // Quality Overrides: Boost ultra-rare features for stacking bonuses
            preset.MutatorQualityOverrides["ArcheanTrees"] = 10;     // 3 → +10 (THE rarest, 0.0034% = 24 tiles)
            preset.MutatorQualityOverrides["Cavern"] = 8;            // 5 → +8  (0.022% = 157 tiles)
            preset.MutatorQualityOverrides["HotSprings"] = 10;       // Already 10, keep high
            preset.MutatorQualityOverrides["Oasis"] = 9;             // 7 → +9  (0.0094% = 66 tiles)
            preset.MutatorQualityOverrides["MineralRich"] = 10;      // Already 10, keep high
            preset.MutatorQualityOverrides["LavaCaves"] = 8;         // Volcanic rare
            preset.MutatorQualityOverrides["TerraformingScar"] = 7;  // Alien terrain
            preset.MutatorQualityOverrides["RiverDelta"] = 7;        // 2 → +7  (0.023% = 159 tiles)
            preset.MutatorQualityOverrides["Peninsula"] = 6;         // 0 → +6  (0.021% = 148 tiles)
            preset.MutatorQualityOverrides["Headwater"] = 5;         // Boost headwater bonus
            preset.MutatorQualityOverrides["Fjord"] = 6;             // Coastal rare
            preset.MutatorQualityOverrides["RiverConfluence"] = 5;   // River feature

            return preset;
        }

        // ===== SUBZERO PRESET: Frozen wasteland survival =====
        // DESIGN NOTE: Layered design - solid Core, enriched with DLC/mods
        // Key Addition: DLC layer for heat sources, GL ice features
        private static Preset CreateSubZeroPreset()
        {
            var preset = new Preset
            {
                Id = "subzero",
                Name = "LandingZone_Preset_subzero_Name",
                Description = "LandingZone_Preset_subzero_Description",
                Category = "Special",
                TargetRarity = TileRarity.VeryRare,
                FilterSummary = "LandingZone_Preset_subzero_FilterSummary"
            };

            var filters = preset.Filters;

            // === CORE LAYER (Critical Anchors - Always Works) ===

            // Climate: Extreme cold (Critical) - defines the preset
            filters.AverageTemperatureRange = new FloatRange(-50f, -15f);
            filters.AverageTemperatureImportance = FilterImportance.Critical;
            filters.GrowingDaysRange = new FloatRange(0f, 15f);
            filters.GrowingDaysImportance = FilterImportance.Critical;

            // === DLC LAYER (Priority - survival aids) ===

            // Heat sources - survival critical in frozen hellscape (Priority, OR)
            filters.MapFeatures.SetImportance("SteamGeysers_Increased", FilterImportance.Priority); // Geothermal heat
            filters.MapFeatures.SetImportance("AncientHeatVent", FilterImportance.Priority);        // DLC heat source
            filters.MapFeatures.SetImportance("HotSprings", FilterImportance.Priority);             // Rare heat source
            filters.MapFeatures.Operator = ImportanceOperator.OR;

            // [Anomaly] Volcanic warmth (Priority) - rare oasis of warmth
            filters.MapFeatures.SetImportance("LavaCaves", FilterImportance.Priority);

            // === MOD LAYER (Preferred - thematic features) ===

            // Shelter (Preferred)
            filters.MapFeatures.SetImportance("Caves", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Mountain", FilterImportance.Preferred);

            // Mining focus when can't farm (Preferred)
            filters.MapFeatures.SetImportance("MineralRich", FilterImportance.Preferred);

            // [Geological Landforms] Ice terrain (Preferred)
            filters.MapFeatures.SetImportance("Crevasse", FilterImportance.Preferred);    // Dangerous ice terrain
            filters.MapFeatures.SetImportance("Iceberg", FilterImportance.Preferred);     // Visual ice features
            filters.MapFeatures.SetImportance("Cliffs", FilterImportance.Preferred);      // Dramatic frozen cliffs
            filters.MapFeatures.SetImportance("Chasm", FilterImportance.Preferred);       // Ice chasms

            // [Geological Landforms] Water in frozen lands (Preferred)
            filters.MapFeatures.SetImportance("Headwater", FilterImportance.Preferred);   // Frozen river source
            filters.MapFeatures.SetImportance("Lake", FilterImportance.Preferred);        // Frozen lake

            // Quality Overrides: Boost survival and thematic features
            preset.MutatorQualityOverrides["SteamGeysers_Increased"] = 10; // CRITICAL for survival
            preset.MutatorQualityOverrides["AncientHeatVent"] = 10;        // CRITICAL for survival
            preset.MutatorQualityOverrides["HotSprings"] = 10;             // CRITICAL for survival
            preset.MutatorQualityOverrides["LavaCaves"] = 8;               // Warmth in hellscape (was -9)
            preset.MutatorQualityOverrides["Crevasse"] = 4;                // Thematic danger
            preset.MutatorQualityOverrides["Iceberg"] = 5;                 // Thematic ice
            preset.MutatorQualityOverrides["Cliffs"] = 4;                  // Dramatic terrain

            return preset;
        }

        // ===== SCORCHED PRESET: Volcanic nightmare =====
        // DESIGN NOTE: Already excellent with fallback tiers and quality overrides
        // Key Addition: Mod layer for volcanic terrain (GL/AB)
        private static Preset CreateScorchedPreset()
        {
            var preset = new Preset
            {
                Id = "scorched",
                Name = "LandingZone_Preset_scorched_Name",
                Description = "LandingZone_Preset_scorched_Description",
                Category = "Special",
                TargetRarity = TileRarity.VeryRare,
                FilterSummary = "LandingZone_Preset_scorched_FilterSummary",
                MinimumStrictness = 1.0f  // Enforce ALL Critical filters (temp + rainfall + lava features)
            };

            var filters = preset.Filters;

            // === CORE LAYER (Critical Anchors) ===

            // Climate: Extreme heat (Critical)
            filters.AverageTemperatureRange = new FloatRange(35f, 60f);
            filters.AverageTemperatureImportance = FilterImportance.Critical;
            filters.RainfallRange = new FloatRange(0f, 400f);
            filters.RainfallImportance = FilterImportance.Critical;
            filters.GrowingDaysRange = new FloatRange(0f, 25f);
            filters.GrowingDaysImportance = FilterImportance.Critical;

            // Lava Features (Critical, OR) - Core thematic elements
            filters.MapFeatures.SetImportance("LavaCaves", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("LavaFlow", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("LavaCrater", FilterImportance.Critical);
            filters.MapFeatures.Operator = ImportanceOperator.OR;

            // === DLC LAYER (Priority - hostile features) ===

            // Pollution (Priority - downgraded from Critical for flexibility)
            filters.PollutionRange = new FloatRange(0.3f, 1.0f);
            filters.PollutionImportance = FilterImportance.Priority;

            // Toxic/Hostile Features (Priority)
            filters.MapFeatures.SetImportance("ToxicLake", FilterImportance.Priority);
            filters.MapFeatures.SetImportance("AncientSmokeVent", FilterImportance.Priority);
            filters.MapFeatures.SetImportance("AncientToxVent", FilterImportance.Priority);
            filters.MapFeatures.SetImportance("Pollution_Increased", FilterImportance.Priority);

            // === MOD LAYER (Preferred - volcanic terrain) ===

            // [Geological Landforms] Volcanic terrain (Preferred)
            filters.MapFeatures.SetImportance("Chasm", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Cliffs", FilterImportance.Preferred);

            // Supporting Features (Preferred)
            filters.MapFeatures.SetImportance("SteamGeysers_Increased", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("DryGround", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("ObsidianDeposits", FilterImportance.Preferred);

            // Quality Overrides: Make "dangerous" features valuable for this theme
            preset.MutatorQualityOverrides["LavaCaves"] = 8;           // -9 → +8
            preset.MutatorQualityOverrides["LavaFlow"] = 8;            // -9 → +8
            preset.MutatorQualityOverrides["LavaCrater"] = 7;          // -10 → +7
            preset.MutatorQualityOverrides["ToxicLake"] = 5;           // -10 → +5
            preset.MutatorQualityOverrides["AncientSmokeVent"] = 4;    // -8 → +4
            preset.MutatorQualityOverrides["AncientToxVent"] = 5;      // -10 → +5
            preset.MutatorQualityOverrides["Pollution_Increased"] = 4; // -8 → +4
            preset.MutatorQualityOverrides["DryGround"] = 3;           // -6 → +3
            preset.MutatorQualityOverrides["Chasm"] = 4;               // Volcanic terrain
            preset.MutatorQualityOverrides["Cliffs"] = 3;              // Dramatic terrain

            // Fallback tiers for Scorched (lava features are ultra-rare)
            preset.FallbackTiers = new List<FallbackTier>();

            // Tier 2: Keep lava features Critical, relax temperature requirement
            var tier2 = new FallbackTier
            {
                Name = "LandingZone_Preset_scorched_FallbackTier2",
                Filters = new FilterSettings()
            };
            tier2.Filters.CopyFrom(filters);
            tier2.Filters.AverageTemperatureRange = new FloatRange(15f, 80f);
            tier2.Filters.AverageTemperatureImportance = FilterImportance.Preferred;
            preset.FallbackTiers.Add(tier2);

            // Tier 3: Drop lava requirement, focus on extreme heat/dry desert
            var tier3 = new FallbackTier
            {
                Name = "LandingZone_Preset_scorched_FallbackTier3",
                Filters = new FilterSettings()
            };
            tier3.Filters.AverageTemperatureRange = new FloatRange(35f, 60f);
            tier3.Filters.AverageTemperatureImportance = FilterImportance.Critical;
            tier3.Filters.RainfallRange = new FloatRange(0f, 400f);
            tier3.Filters.RainfallImportance = FilterImportance.Critical;
            tier3.Filters.GrowingDaysRange = new FloatRange(0f, 25f);
            tier3.Filters.GrowingDaysImportance = FilterImportance.Critical;
            tier3.Filters.PollutionRange = new FloatRange(0.3f, 1.0f);
            tier3.Filters.PollutionImportance = FilterImportance.Priority;
            preset.FallbackTiers.Add(tier3);

            return preset;
        }

        // ===== SAVANNAH PRESET: Wildlife plains =====
        // DESIGN NOTE: Layered design - works with Core climate, richer with DLC/mods
        // Key Fix: Changed AnimalLife_Increased/AnimalHabitat from Critical to Priority
        //          (these rare mutators may not exist, causing 0 results)
        private static Preset CreateSavannahPreset()
        {
            var preset = new Preset
            {
                Id = "savannah",
                Name = "LandingZone_Preset_savannah_Name",
                Description = "LandingZone_Preset_savannah_Description",
                Category = "Curated",
                TargetRarity = TileRarity.Common,
                FilterSummary = "LandingZone_Preset_savannah_FilterSummary"
            };

            var filters = preset.Filters;

            // === CORE LAYER (Critical Anchors - Always Works) ===

            // Climate: Warm savannah range (Critical) - defines the preset
            filters.AverageTemperatureRange = new FloatRange(22f, 38f);
            filters.AverageTemperatureImportance = FilterImportance.Critical;
            filters.RainfallRange = new FloatRange(500f, 1200f);
            filters.RainfallImportance = FilterImportance.Critical;
            filters.GrowingDaysRange = new FloatRange(45f, 60f);
            filters.GrowingDaysImportance = FilterImportance.Critical;

            // Terrain: Open plains (Critical - savannah is flat/rolling)
            filters.AllowedHilliness.Clear();
            filters.AllowedHilliness.Add(Hilliness.Flat);
            filters.AllowedHilliness.Add(Hilliness.SmallHills);

            // === DLC LAYER (Priority - high scoring, not gates) ===

            // Wildlife (Priority, OR) - downgraded from Critical (these are rare mutators)
            filters.MapFeatures.SetImportance("AnimalLife_Increased", FilterImportance.Priority);
            filters.MapFeatures.SetImportance("AnimalHabitat", FilterImportance.Priority);
            filters.MapFeatures.Operator = ImportanceOperator.OR;

            // Wind - open plains have wind (Priority)
            filters.MapFeatures.SetImportance("WindyMutator", FilterImportance.Priority);

            // Grazing fodder (Priority)
            filters.MapFeatures.SetImportance("WildPlants", FilterImportance.Priority);

            // === MOD LAYER (Preferred - bonus scoring) ===

            // [Geological Landforms] Open terrain (Preferred)
            filters.MapFeatures.SetImportance("Plateau", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Basin", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Valley", FilterImportance.Preferred);

            // Animal Density: High animal population (Preferred)
            filters.AnimalDensityRange = new FloatRange(3.0f, 6.5f);
            filters.AnimalDensityImportance = FilterImportance.Preferred;

            // Grazing: Animals can graze (Preferred)
            filters.GrazeImportance = FilterImportance.Preferred;

            // Movement: Easy to traverse (Preferred)
            filters.MovementDifficultyRange = new FloatRange(0.0f, 1.0f);
            filters.MovementDifficultyImportance = FilterImportance.Preferred;

            // Supporting Features (Preferred, OR): Water for animals
            filters.Rivers.SetImportance("HugeRiver", FilterImportance.Preferred);
            filters.Rivers.SetImportance("LargeRiver", FilterImportance.Preferred);
            filters.Rivers.SetImportance("River", FilterImportance.Preferred);
            filters.Rivers.Operator = ImportanceOperator.OR;

            // Quality Overrides: Boost wildlife-related features
            preset.MutatorQualityOverrides["AnimalLife_Increased"] = 8;  // THE wildlife mutator
            preset.MutatorQualityOverrides["AnimalHabitat"] = 7;         // Animal bonus
            preset.MutatorQualityOverrides["WindyMutator"] = 5;          // Open plains wind
            preset.MutatorQualityOverrides["Plateau"] = 4;               // Open terrain
            preset.MutatorQualityOverrides["Basin"] = 3;                 // Open terrain

            return preset;
        }

        // ===== AQUATIC PRESET: Water world =====
        // DESIGN NOTE: Layered design - already good, promoting key features to Priority
        // Key Addition: Quality overrides for port features, fishing economy
        private static Preset CreateAquaticPreset()
        {
            var preset = new Preset
            {
                Id = "aquatic",
                Name = "LandingZone_Preset_aquatic_Name",
                Description = "LandingZone_Preset_aquatic_Description",
                Category = "Curated",
                TargetRarity = TileRarity.Rare,
                FilterSummary = "LandingZone_Preset_aquatic_FilterSummary"
            };

            var filters = preset.Filters;

            // === CORE LAYER (Critical Anchor - Always Works) ===

            // Major Water Source: Coastal OR rivers (Critical)
            filters.WaterAccessImportance = FilterImportance.Critical;

            // === DLC LAYER (Priority - key port features) ===

            // [Geological Landforms] Key port features (Priority)
            filters.MapFeatures.SetImportance("Harbor", FilterImportance.Priority);           // Natural harbor
            filters.MapFeatures.SetImportance("Bay", FilterImportance.Priority);              // Coastal bay
            filters.MapFeatures.SetImportance("Fjord", FilterImportance.Priority);            // Coastal inlet
            filters.MapFeatures.Operator = ImportanceOperator.OR;

            // Major water confluences (Priority)
            filters.MapFeatures.SetImportance("RiverDelta", FilterImportance.Priority);       // Multiple rivers
            filters.MapFeatures.SetImportance("RiverConfluence", FilterImportance.Priority);  // Rivers meet

            // Fishing economy (Priority)
            filters.MapFeatures.SetImportance("Fish_Increased", FilterImportance.Priority);

            // Big rivers (Priority)
            filters.Rivers.SetImportance("HugeRiver", FilterImportance.Priority);
            filters.Rivers.SetImportance("LargeRiver", FilterImportance.Priority);
            filters.Rivers.Operator = ImportanceOperator.OR;

            // === MOD LAYER (Preferred - water features everywhere) ===

            // Lakes (Preferred)
            filters.CoastalLakeImportance = FilterImportance.Preferred;
            filters.MapFeatures.SetImportance("Lake", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("LakeWithIsland", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("LakeWithIslands", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Lakeshore", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Pond", FilterImportance.Preferred);

            // River features (Preferred)
            filters.MapFeatures.SetImportance("Headwater", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("RiverIsland", FilterImportance.Preferred);

            // Coastal features (Preferred)
            filters.MapFeatures.SetImportance("Cove", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Peninsula", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("CoastalIsland", FilterImportance.Preferred);

            // Fishing ranges (Preferred)
            filters.FishPopulationRange = new FloatRange(400f, 900f);
            filters.FishPopulationImportance = FilterImportance.Preferred;

            // Climate: Temperate for living (Preferred)
            filters.AverageTemperatureRange = new FloatRange(10f, 30f);
            filters.AverageTemperatureImportance = FilterImportance.Preferred;
            filters.RainfallRange = new FloatRange(1000f, 2500f);
            filters.RainfallImportance = FilterImportance.Preferred;

            // Quality Overrides: Boost aquatic features
            preset.MutatorQualityOverrides["Harbor"] = 8;            // Natural port
            preset.MutatorQualityOverrides["Fjord"] = 7;             // Deep water access
            preset.MutatorQualityOverrides["Bay"] = 6;               // Sheltered water
            preset.MutatorQualityOverrides["RiverDelta"] = 6;        // Multiple rivers
            preset.MutatorQualityOverrides["RiverConfluence"] = 5;   // Rivers meet
            preset.MutatorQualityOverrides["Fish_Increased"] = 6;    // Fishing economy
            preset.MutatorQualityOverrides["Peninsula"] = 5;         // Surrounded by water
            preset.MutatorQualityOverrides["CoastalIsland"] = 5;     // Island life

            return preset;
        }

        // ===== DESERT OASIS PRESET: Water in wasteland =====
        // DESIGN NOTE: Layered design - Core anchors, enriched with DLC/mods
        // Key Addition: Oasis mutator as Priority, quality overrides for thematic features
        private static Preset CreateDesertOasisPreset()
        {
            var preset = new Preset
            {
                Id = "desert_oasis",
                Name = "LandingZone_Preset_desert_oasis_Name",
                Description = "LandingZone_Preset_desert_oasis_Description",
                Category = "Curated",
                TargetRarity = TileRarity.Rare,
                FilterSummary = "LandingZone_Preset_desert_oasis_FilterSummary"
            };

            var filters = preset.Filters;

            // === CORE LAYER (Critical Anchors - Always Works) ===

            // Climate: Hot desert (Critical) - defines the preset
            filters.AverageTemperatureRange = new FloatRange(28f, 45f);
            filters.AverageTemperatureImportance = FilterImportance.Critical;
            filters.RainfallRange = new FloatRange(100f, 600f);
            filters.RainfallImportance = FilterImportance.Critical;

            // WATER FEATURES - This is what makes it an oasis!
            filters.WaterAccessImportance = FilterImportance.Critical;

            // === DLC LAYER (Priority - thematic oasis features) ===

            // THE oasis mutator (Priority) - the defining thematic feature
            filters.MapFeatures.SetImportance("Oasis", FilterImportance.Priority);
            filters.MapFeatures.Operator = ImportanceOperator.OR;

            // Desert water sources (Priority)
            filters.MapFeatures.SetImportance("HotSprings", FilterImportance.Priority);   // Hot water in desert
            filters.MapFeatures.SetImportance("Fish_Increased", FilterImportance.Priority); // Oasis fish

            // Rivers in desert (Priority)
            filters.Rivers.SetImportance("HugeRiver", FilterImportance.Priority);
            filters.Rivers.SetImportance("LargeRiver", FilterImportance.Priority);
            filters.Rivers.Operator = ImportanceOperator.OR;

            // === MOD LAYER (Preferred - water features) ===

            // Lakes (Preferred)
            filters.CoastalLakeImportance = FilterImportance.Preferred;
            filters.MapFeatures.SetImportance("Lake", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Pond", FilterImportance.Preferred);

            // Moisture pocket (Preferred)
            filters.MapFeatures.SetImportance("WetClimate", FilterImportance.Preferred);

            // Make desert livable (Preferred)
            filters.MapFeatures.SetImportance("Fertile", FilterImportance.Preferred);     // Farming in desert
            filters.MapFeatures.SetImportance("WildPlants", FilterImportance.Preferred);  // Forage despite heat

            // [Geological Landforms] Water terrain (Preferred)
            filters.MapFeatures.SetImportance("Basin", FilterImportance.Preferred);       // Water collects
            filters.MapFeatures.SetImportance("Hollow", FilterImportance.Preferred);      // Sheltered

            // Quality Overrides: Boost oasis-related features
            preset.MutatorQualityOverrides["Oasis"] = 10;            // THE oasis feature
            preset.MutatorQualityOverrides["HotSprings"] = 8;        // Desert water source
            preset.MutatorQualityOverrides["Fertile"] = 7;           // Rare in desert
            preset.MutatorQualityOverrides["Fish_Increased"] = 6;    // Oasis fish
            preset.MutatorQualityOverrides["WetClimate"] = 6;        // Moisture pocket
            preset.MutatorQualityOverrides["Basin"] = 4;             // Water collects

            return preset;
        }

        // ===== DEFENSE PRESET: Mountain fortress =====
        // DESIGN NOTE: Layered design - works with Core terrain, richer with DLC/mods
        // Key Fix: Changed MineablePlasteel/Components from Critical to Priority
        //          (these ores may not exist in world generation)
        private static Preset CreateDefensePreset()
        {
            var preset = new Preset
            {
                Id = "defense",
                Name = "LandingZone_Preset_defense_Name",
                Description = "LandingZone_Preset_defense_Description",
                Category = "Curated",
                TargetRarity = TileRarity.Uncommon,
                FilterSummary = "LandingZone_Preset_defense_FilterSummary"
            };

            var filters = preset.Filters;

            // === CORE LAYER (Critical Anchors - Always Works) ===

            // Terrain: Mountainous (Critical) - defines the preset
            filters.AllowedHilliness.Clear();
            filters.AllowedHilliness.Add(Hilliness.LargeHills);
            filters.AllowedHilliness.Add(Hilliness.Mountainous);

            // Fortification Features (Critical, OR): Mountain and underground
            filters.MapFeatures.SetImportance("Mountain", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("Caves", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("Cavern", FilterImportance.Critical);
            filters.MapFeatures.Operator = ImportanceOperator.OR;

            // === DLC LAYER (Priority - high scoring, not gates) ===

            // Resources: Advanced ores (downgraded from Critical - may not exist)
            filters.Stones.SetImportance("MineablePlasteel", FilterImportance.Priority);
            filters.Stones.SetImportance("MineableComponentsIndustrial", FilterImportance.Priority);
            filters.Stones.Operator = ImportanceOperator.OR;

            // [DLC] Military structures (Priority)
            filters.MapFeatures.SetImportance("AncientGarrison", FilterImportance.Priority);  // Military loot
            filters.MapFeatures.SetImportance("AncientQuarry", FilterImportance.Priority);    // Stone source

            // Mining bonus (Priority)
            filters.MapFeatures.SetImportance("MineralRich", FilterImportance.Priority);

            // === MOD LAYER (Preferred - bonus scoring) ===

            // [Geological Landforms] Natural barriers (Preferred)
            filters.MapFeatures.SetImportance("Chasm", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Cliffs", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Crevasse", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Valley", FilterImportance.Preferred);          // Killbox terrain

            // Water-based defensibility (Preferred)
            filters.MapFeatures.SetImportance("Peninsula", FilterImportance.Preferred);       // 3 sides water
            filters.MapFeatures.SetImportance("RiverIsland", FilterImportance.Preferred);     // River moat
            filters.MapFeatures.SetImportance("CoastalIsland", FilterImportance.Preferred);   // Ocean moat

            // Climate: Temperate for livability (Preferred)
            filters.AverageTemperatureRange = new FloatRange(10f, 25f);
            filters.AverageTemperatureImportance = FilterImportance.Preferred;
            filters.GrowingDaysRange = new FloatRange(30f, 60f);
            filters.GrowingDaysImportance = FilterImportance.Preferred;

            // Quality Overrides: Boost defense-related features
            preset.MutatorQualityOverrides["AncientGarrison"] = 6;        // Military loot (was -8)
            preset.MutatorQualityOverrides["Peninsula"] = 7;              // 3-sided water defense
            preset.MutatorQualityOverrides["RiverIsland"] = 6;            // Natural moat
            preset.MutatorQualityOverrides["CoastalIsland"] = 6;          // Ocean moat
            preset.MutatorQualityOverrides["Chasm"] = 5;                  // Natural barrier
            preset.MutatorQualityOverrides["Cliffs"] = 5;                 // Natural barrier
            preset.MutatorQualityOverrides["Valley"] = 6;                 // Killbox terrain

            return preset;
        }

        // ===== AGRARIAN PRESET: Farming paradise =====
        // DESIGN NOTE: Layered design - Core climate anchors, enriched with DLC/mods
        // Key Addition: Fertile/PlantLife promoted to Priority, quality overrides
        private static Preset CreateAgrarianPreset()
        {
            var preset = new Preset
            {
                Id = "agrarian",
                Name = "LandingZone_Preset_agrarian_Name",
                Description = "LandingZone_Preset_agrarian_Description",
                Category = "Curated",
                TargetRarity = TileRarity.Common,
                FilterSummary = "LandingZone_Preset_agrarian_FilterSummary"
            };

            var filters = preset.Filters;

            // === CORE LAYER (Critical Anchors - Always Works) ===

            // Climate: Optimal crop conditions (Critical) - defines the preset
            filters.AverageTemperatureRange = new FloatRange(15f, 28f);
            filters.AverageTemperatureImportance = FilterImportance.Critical;
            filters.RainfallRange = new FloatRange(1200f, 2500f);
            filters.RainfallImportance = FilterImportance.Critical;
            filters.GrowingDaysRange = new FloatRange(50f, 60f);
            filters.GrowingDaysImportance = FilterImportance.Critical;

            // Terrain: Easy to farm (Critical - flat for farming)
            filters.AllowedHilliness.Clear();
            filters.AllowedHilliness.Add(Hilliness.Flat);
            filters.AllowedHilliness.Add(Hilliness.SmallHills);

            // === DLC LAYER (Priority - key farming features) ===

            // THE farming mutator (Priority)
            filters.MapFeatures.SetImportance("Fertile", FilterImportance.Priority);
            filters.MapFeatures.Operator = ImportanceOperator.OR;

            // Plant life bonuses (Priority)
            filters.MapFeatures.SetImportance("PlantLife_Increased", FilterImportance.Priority);
            filters.MapFeatures.SetImportance("WetClimate", FilterImportance.Priority);

            // === MOD LAYER (Preferred - farming enrichment) ===

            // Secondary farming features (Preferred)
            filters.MapFeatures.SetImportance("WildPlants", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Muddy", FilterImportance.Preferred);

            // [Geological Landforms] Sheltered farmland (Preferred)
            filters.MapFeatures.SetImportance("Valley", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Basin", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Hollow", FilterImportance.Preferred);

            // [Geological Landforms] Irrigation (Preferred)
            filters.MapFeatures.SetImportance("RiverDelta", FilterImportance.Preferred);

            // [Alpha Biomes] Paradise farming (Preferred)
            filters.MapFeatures.SetImportance("AB_IdyllicMeadows", FilterImportance.Preferred);

            // Water (Preferred, OR): Irrigation and fishing
            filters.Rivers.SetImportance("HugeRiver", FilterImportance.Preferred);
            filters.Rivers.SetImportance("LargeRiver", FilterImportance.Preferred);
            filters.Rivers.SetImportance("River", FilterImportance.Preferred);
            filters.Rivers.Operator = ImportanceOperator.OR;
            filters.CoastalImportance = FilterImportance.Preferred;
            filters.CoastalLakeImportance = FilterImportance.Preferred;

            // Movement: Easy terrain (Preferred)
            filters.MovementDifficultyRange = new FloatRange(0.0f, 1.2f);
            filters.MovementDifficultyImportance = FilterImportance.Preferred;

            // Quality Overrides: Boost farming-related features
            preset.MutatorQualityOverrides["Fertile"] = 10;              // THE farming mutator
            preset.MutatorQualityOverrides["PlantLife_Increased"] = 8;   // More plants
            preset.MutatorQualityOverrides["WetClimate"] = 6;            // Moisture for crops
            preset.MutatorQualityOverrides["Valley"] = 5;                // Sheltered farmland
            preset.MutatorQualityOverrides["Basin"] = 4;                 // Water collects
            preset.MutatorQualityOverrides["RiverDelta"] = 5;            // Irrigation
            preset.MutatorQualityOverrides["AB_IdyllicMeadows"] = 8;     // Paradise farming

            return preset;
        }

        // ===== POWER PRESET: Energy independence =====
        // DESIGN NOTE: Layered design - multiple power sources, broader Critical gate
        // Key Fix: Added WindyMutator/SunnyMutator to Critical OR (alternative anchors)
        private static Preset CreatePowerPreset()
        {
            var preset = new Preset
            {
                Id = "power",
                Name = "LandingZone_Preset_power_Name",
                Description = "LandingZone_Preset_power_Description",
                Category = "Curated",
                TargetRarity = TileRarity.Rare,
                FilterSummary = "LandingZone_Preset_power_FilterSummary"
            };

            var filters = preset.Filters;

            // === CORE LAYER (Critical Anchors - Broadened) ===

            // Multiple power sources (Critical, OR) - broadened to include wind/solar
            filters.MapFeatures.SetImportance("SteamGeysers_Increased", FilterImportance.Critical); // Geothermal
            filters.MapFeatures.SetImportance("AncientHeatVent", FilterImportance.Critical);        // DLC geothermal
            filters.MapFeatures.SetImportance("WindyMutator", FilterImportance.Critical);           // Wind power
            filters.MapFeatures.SetImportance("SunnyMutator", FilterImportance.Critical);           // Solar power
            filters.MapFeatures.Operator = ImportanceOperator.OR;  // Any power source works

            // === DLC LAYER (Priority - high scoring, not gates) ===

            // Hydro power (Priority, OR): Watermill potential
            filters.Rivers.SetImportance("HugeRiver", FilterImportance.Priority);
            filters.Rivers.SetImportance("LargeRiver", FilterImportance.Priority);
            filters.Rivers.Operator = ImportanceOperator.OR;

            // Nuclear power potential (Priority)
            filters.Stones.SetImportance("MineableUranium", FilterImportance.Priority);

            // === MOD LAYER (Preferred - bonus scoring) ===

            // [Geological Landforms] Hydro sites (Preferred)
            filters.MapFeatures.SetImportance("RiverDelta", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("RiverConfluence", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Headwater", FilterImportance.Preferred);

            // [Geological Landforms] Wind exposure (Preferred)
            filters.MapFeatures.SetImportance("Plateau", FilterImportance.Preferred);

            // Terrain: Elevated for wind (Preferred)
            filters.AllowedHilliness.Clear();
            filters.AllowedHilliness.Add(Hilliness.SmallHills);
            filters.AllowedHilliness.Add(Hilliness.LargeHills);
            filters.AllowedHilliness.Add(Hilliness.Mountainous);

            // Solar optimization: Low rainfall = more sun (Preferred)
            filters.RainfallRange = new FloatRange(400f, 1200f);
            filters.RainfallImportance = FilterImportance.Preferred;

            // Climate: Manageable heat/cold loads (Preferred)
            filters.AverageTemperatureRange = new FloatRange(10f, 30f);
            filters.AverageTemperatureImportance = FilterImportance.Preferred;

            // Geography: Tidal potential (Preferred)
            filters.CoastalImportance = FilterImportance.Preferred;
            filters.CoastalLakeImportance = FilterImportance.Preferred;

            // Quality Overrides: Boost power-related features
            preset.MutatorQualityOverrides["SteamGeysers_Increased"] = 10; // THE geothermal mutator
            preset.MutatorQualityOverrides["AncientHeatVent"] = 9;         // DLC geothermal
            preset.MutatorQualityOverrides["WindyMutator"] = 7;            // Wind power
            preset.MutatorQualityOverrides["SunnyMutator"] = 6;            // Solar power
            preset.MutatorQualityOverrides["RiverDelta"] = 5;              // Hydro potential
            preset.MutatorQualityOverrides["RiverConfluence"] = 5;         // Hydro potential
            preset.MutatorQualityOverrides["Plateau"] = 4;                 // Wind exposure

            return preset;
        }

        // ===== BAYOU PRESET: Swamp horror =====
        // DESIGN NOTE: Layered design - swampiness anchor, enriched with DLC/mods
        // Key Addition: DLC layer for horror features, quality overrides
        private static Preset CreateBayouPreset()
        {
            var preset = new Preset
            {
                Id = "bayou",
                Name = "LandingZone_Preset_bayou_Name",
                Description = "LandingZone_Preset_bayou_Description",
                Category = "Curated",
                TargetRarity = TileRarity.Uncommon,
                FilterSummary = "LandingZone_Preset_bayou_FilterSummary"
            };

            var filters = preset.Filters;

            // === CORE LAYER (Critical Anchors - Always Works) ===

            // Climate: Hot and humid (Critical) - defines the preset
            filters.AverageTemperatureRange = new FloatRange(25f, 40f);
            filters.AverageTemperatureImportance = FilterImportance.Critical;
            filters.RainfallRange = new FloatRange(1800f, 3000f);
            filters.RainfallImportance = FilterImportance.Critical;

            // Swampiness: THE key stat for swamps! (Critical)
            filters.SwampinessRange = new FloatRange(0.4f, 1.0f);
            filters.SwampinessImportance = FilterImportance.Critical;

            // Terrain: Low-lying swamps (Critical)
            filters.AllowedHilliness.Clear();
            filters.AllowedHilliness.Add(Hilliness.Flat);
            filters.AllowedHilliness.Add(Hilliness.SmallHills);

            // === DLC LAYER (Priority - horror features) ===

            // [Anomaly] Swamp infestation (Priority)
            filters.MapFeatures.SetImportance("InsectMegahive", FilterImportance.Priority);
            filters.MapFeatures.Operator = ImportanceOperator.OR;

            // Murky waters (Priority)
            filters.MapFeatures.SetImportance("Pollution_Increased", FilterImportance.Priority);

            // Swamp Mutators (Priority)
            filters.MapFeatures.SetImportance("Muddy", FilterImportance.Priority);
            filters.MapFeatures.SetImportance("Marshy", FilterImportance.Priority);

            // === MOD LAYER (Preferred - swamp enrichment) ===

            // Swamp terrain (Preferred)
            filters.MapFeatures.SetImportance("Wetland", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("WetClimate", FilterImportance.Preferred);

            // [Alpha Biomes] Horror swamp (Preferred)
            filters.MapFeatures.SetImportance("AB_MiasmicMangrove", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("AB_TarPits", FilterImportance.Preferred);

            // Water features (Preferred)
            filters.Rivers.SetImportance("HugeRiver", FilterImportance.Preferred);
            filters.Rivers.SetImportance("LargeRiver", FilterImportance.Preferred);
            filters.Rivers.SetImportance("River", FilterImportance.Preferred);
            filters.Rivers.Operator = ImportanceOperator.OR;

            filters.MapFeatures.SetImportance("Lakeshore", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Pond", FilterImportance.Preferred);

            // Flora/Fauna (Preferred)
            filters.MapFeatures.SetImportance("PlantLife_Increased", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("WildTropicalPlants", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("AnimalLife_Increased", FilterImportance.Preferred);

            // Movement: Difficult terrain (Preferred)
            filters.MovementDifficultyRange = new FloatRange(1.2f, 2.0f);
            filters.MovementDifficultyImportance = FilterImportance.Preferred;

            // Quality Overrides: Boost swamp and horror features
            preset.MutatorQualityOverrides["Muddy"] = 6;                // Swamp terrain
            preset.MutatorQualityOverrides["Marshy"] = 6;               // Swamp terrain
            preset.MutatorQualityOverrides["InsectMegahive"] = 5;       // Thematic danger (was 0)
            preset.MutatorQualityOverrides["Pollution_Increased"] = 3;  // Murky waters (was -8)
            preset.MutatorQualityOverrides["AB_MiasmicMangrove"] = 7;   // Horror swamp
            preset.MutatorQualityOverrides["AB_TarPits"] = 5;           // Dangerous terrain

            return preset;
        }

        // ===== HOMESTEAD PRESET: Abandoned settlement salvage =====
        // DESIGN NOTE: Already excellent with quality overrides
        // Key Addition: Junkyard added to Critical OR, mod layer for terrain
        private static Preset CreateHomesteadPreset()
        {
            var preset = new Preset
            {
                Id = "homestead",
                Name = "LandingZone_Preset_homestead_Name",
                Description = "LandingZone_Preset_homestead_Description",
                Category = "Curated",
                TargetRarity = TileRarity.VeryRare,
                FilterSummary = "LandingZone_Preset_homestead_FilterSummary"
            };

            var filters = preset.Filters;

            // === CORE LAYER (Critical Anchor - salvage sites) ===

            // Abandoned settlements (Critical, OR) - must have one salvage site
            filters.MapFeatures.SetImportance("AbandonedColonyTribal", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("AbandonedColonyOutlander", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("Stockpile", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("AncientRuins", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("Junkyard", FilterImportance.Critical);  // Added: salvage site
            filters.MapFeatures.Operator = ImportanceOperator.OR;

            // === DLC LAYER (Priority - ancient structures) ===

            // Ancient structures (Priority) - high value salvage
            filters.MapFeatures.SetImportance("AncientWarehouse", FilterImportance.Priority);
            filters.MapFeatures.SetImportance("AncientQuarry", FilterImportance.Priority);
            filters.MapFeatures.SetImportance("AncientGarrison", FilterImportance.Priority);
            filters.MapFeatures.SetImportance("AncientLaunchSite", FilterImportance.Priority);
            filters.MapFeatures.SetImportance("AncientUplink", FilterImportance.Priority);
            filters.MapFeatures.SetImportance("AncientChemfuelRefinery", FilterImportance.Priority);

            // Resources: Plasteel for salvage (Priority)
            filters.Stones.SetImportance("MineablePlasteel", FilterImportance.Priority);

            // === MOD LAYER (Preferred - terrain and roads) ===

            // [Geological Landforms] Sheltered settlement (Preferred)
            filters.MapFeatures.SetImportance("Valley", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Basin", FilterImportance.Preferred);

            // Supporting: Roads (ancient sites had access)
            filters.Roads.SetImportance("DirtRoad", FilterImportance.Preferred);
            filters.Roads.SetImportance("DirtPath", FilterImportance.Preferred);
            filters.Roads.SetImportance("StoneRoad", FilterImportance.Preferred);
            filters.Roads.SetImportance("AncientAsphaltRoad", FilterImportance.Preferred);
            filters.Roads.SetImportance("AncientAsphaltHighway", FilterImportance.Preferred);
            filters.Roads.Operator = ImportanceOperator.OR;

            // Climate: Temperate (livable) - Preferred
            filters.AverageTemperatureRange = new FloatRange(10f, 30f);
            filters.AverageTemperatureImportance = FilterImportance.Preferred;
            filters.GrowingDaysRange = new FloatRange(30f, 60f);
            filters.GrowingDaysImportance = FilterImportance.Preferred;

            // Quality overrides: Ancient sites are valuable despite dangers
            preset.MutatorQualityOverrides["AbandonedColonyTribal"] = 6;      // -5 → +6
            preset.MutatorQualityOverrides["AbandonedColonyOutlander"] = 7;   // -5 → +7
            preset.MutatorQualityOverrides["Junkyard"] = 6;                   // -5 → +6 (salvage value)
            preset.MutatorQualityOverrides["AncientGarrison"] = 6;            // -8 → +6
            preset.MutatorQualityOverrides["AncientLaunchSite"] = 6;          // -8 → +6
            preset.MutatorQualityOverrides["AncientChemfuelRefinery"] = 4;    // -8 → +4
            preset.MutatorQualityOverrides["AncientWarehouse"] = 5;           // 0 → +5
            preset.MutatorQualityOverrides["AncientQuarry"] = 5;              // 0 → +5
            preset.MutatorQualityOverrides["Valley"] = 4;                     // Sheltered settlement
            preset.MutatorQualityOverrides["Basin"] = 3;                      // Sheltered settlement

            // Industrial ruins theme
            preset.MutatorQualityOverrides["Pollution_Increased"] = 3;        // -8 → +3
            preset.MutatorQualityOverrides["AncientToxVent"] = 4;             // -10 → +4

            return preset;
        }

        // ===== ANOMALY PRESET: Horror experience =====
        // DESIGN NOTE: Layered design - creepy with Core, terrifying with Anomaly DLC
        // Purpose: Underground horror, dramatic terrain, atmospheric dread
        private static Preset CreateAnomalyPreset()
        {
            var preset = new Preset
            {
                Id = "anomaly",
                Name = "LandingZone_Preset_anomaly_Name",
                Description = "LandingZone_Preset_anomaly_Description",
                Category = "Special",
                TargetRarity = TileRarity.Rare,
                FilterSummary = "LandingZone_Preset_anomaly_FilterSummary"
            };

            var filters = preset.Filters;

            // === CORE LAYER (Always Works) ===

            // Underground horror (Critical, OR) - caves exist in vanilla
            filters.MapFeatures.SetImportance("Caves", FilterImportance.Critical);
            filters.MapFeatures.SetImportance("Cavern", FilterImportance.Critical);
            filters.MapFeatures.Operator = ImportanceOperator.OR;

            // Dramatic terrain (Priority) - high scoring for mountainous
            filters.AllowedHilliness.Clear();
            filters.AllowedHilliness.Add(Hilliness.LargeHills);
            filters.AllowedHilliness.Add(Hilliness.Mountainous);

            // Climate: Wide range for biome variety (Preferred)
            filters.AverageTemperatureRange = new FloatRange(-10f, 35f);
            filters.AverageTemperatureImportance = FilterImportance.Preferred;

            // Atmospheric features (Preferred)
            filters.MapFeatures.SetImportance("FoggyMutator", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("AncientRuins", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Mountain", FilterImportance.Preferred);

            // === DLC LAYER (The Full Horror Experience) ===

            // [Anomaly] Volcanic horror - Priority (high scoring)
            filters.MapFeatures.SetImportance("LavaCaves", FilterImportance.Priority);
            filters.MapFeatures.SetImportance("LavaFlow", FilterImportance.Priority);
            filters.MapFeatures.SetImportance("LavaCrater", FilterImportance.Priority);
            filters.MapFeatures.SetImportance("TerraformingScar", FilterImportance.Priority);
            filters.MapFeatures.SetImportance("InsectMegahive", FilterImportance.Priority);

            // [Biotech] Toxic wasteland - Priority
            filters.MapFeatures.SetImportance("Pollution_Increased", FilterImportance.Priority);
            filters.MapFeatures.SetImportance("ToxicLake", FilterImportance.Priority);
            filters.MapFeatures.SetImportance("ArcheanTrees", FilterImportance.Priority);

            // === MOD LAYER (Deeper Horror) ===

            // [Geological Landforms] Dangerous terrain - Preferred
            filters.MapFeatures.SetImportance("Chasm", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Crevasse", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("Cliffs", FilterImportance.Preferred);

            // Quality Overrides: Flip negatives to positives for horror theme
            preset.MutatorQualityOverrides["FoggyMutator"] = 5;          // -3 → +5
            preset.MutatorQualityOverrides["Pollution_Increased"] = 4;   // -8 → +4
            preset.MutatorQualityOverrides["LavaCaves"] = 7;             // -9 → +7
            preset.MutatorQualityOverrides["LavaFlow"] = 6;              // -9 → +6
            preset.MutatorQualityOverrides["LavaCrater"] = 6;            // -10 → +6
            preset.MutatorQualityOverrides["ToxicLake"] = 5;             // -10 → +5
            preset.MutatorQualityOverrides["InsectMegahive"] = 6;        // 0 → +6
            preset.MutatorQualityOverrides["TerraformingScar"] = 5;      // Alien terrain
            preset.MutatorQualityOverrides["Chasm"] = 4;                 // Dangerous but thematic
            preset.MutatorQualityOverrides["Crevasse"] = 4;              // Ice/rock hazard

            return preset;
        }

        // ===== TRADE EMPIRE PRESET: Coastal trade focus =====
        // DESIGN NOTE: Layered design - trade routes with Core, magnate with DLC/mods
        // Purpose: Coastal access, roads, valuable resources, natural ports
        private static Preset CreateTradeEmpirePreset()
        {
            var preset = new Preset
            {
                Id = "trade_empire",
                Name = "LandingZone_Preset_trade_empire_Name",
                Description = "LandingZone_Preset_trade_empire_Description",
                Category = "Curated",
                TargetRarity = TileRarity.Uncommon,
                FilterSummary = "LandingZone_Preset_trade_empire_FilterSummary"
            };

            var filters = preset.Filters;

            // === CORE LAYER (Always Works) ===

            // Coastal: Sea trade routes (Critical)
            filters.CoastalImportance = FilterImportance.Critical;

            // Roads: Land trade routes (Critical, OR)
            filters.Roads.SetImportance("DirtRoad", FilterImportance.Critical);
            filters.Roads.SetImportance("StoneRoad", FilterImportance.Critical);
            filters.Roads.SetImportance("AncientAsphaltRoad", FilterImportance.Critical);
            filters.Roads.SetImportance("AncientAsphaltHighway", FilterImportance.Critical);
            filters.Roads.Operator = ImportanceOperator.OR;

            // Climate: Productive workers (Preferred)
            filters.AverageTemperatureRange = new FloatRange(10f, 28f);
            filters.AverageTemperatureImportance = FilterImportance.Preferred;

            // Growing: Food self-sufficiency (Preferred)
            filters.GrowingDaysRange = new FloatRange(40f, 60f);
            filters.GrowingDaysImportance = FilterImportance.Preferred;

            // Additional water access (Preferred)
            filters.CoastalLakeImportance = FilterImportance.Preferred;

            // === DLC LAYER (Trade Magnate) ===

            // Luxury trade goods - Priority (high scoring)
            filters.Stones.SetImportance("MineableGold", FilterImportance.Priority);
            filters.Stones.SetImportance("MineableJade", FilterImportance.Priority);
            filters.Stones.SetImportance("MineableSilver", FilterImportance.Priority);
            filters.Stones.Operator = ImportanceOperator.OR;

            // High-tech exports - Priority
            filters.MapFeatures.SetImportance("MineralRich", FilterImportance.Priority);

            // [Anomaly] Salvage for trade - Priority
            filters.MapFeatures.SetImportance("Junkyard", FilterImportance.Priority);
            filters.MapFeatures.SetImportance("Stockpile", FilterImportance.Priority);

            // [Ideology] Pre-built storage - Priority
            filters.MapFeatures.SetImportance("AncientWarehouse", FilterImportance.Priority);

            // Fishing industry (Preferred)
            filters.MapFeatures.SetImportance("Fish_Increased", FilterImportance.Preferred);
            filters.FishPopulationRange = new FloatRange(300f, 900f);
            filters.FishPopulationImportance = FilterImportance.Preferred;

            // === MOD LAYER (Trade Magnate+) ===

            // [Geological Landforms] Natural ports - Priority
            filters.MapFeatures.SetImportance("Harbor", FilterImportance.Priority);
            filters.MapFeatures.SetImportance("Bay", FilterImportance.Priority);
            filters.MapFeatures.SetImportance("Cove", FilterImportance.Priority);
            filters.MapFeatures.SetImportance("Fjord", FilterImportance.Priority);

            // River trade hubs - Priority
            filters.MapFeatures.SetImportance("RiverDelta", FilterImportance.Priority);
            filters.MapFeatures.SetImportance("RiverConfluence", FilterImportance.Priority);

            // Defensible ports - Preferred
            filters.MapFeatures.SetImportance("Peninsula", FilterImportance.Preferred);
            filters.MapFeatures.SetImportance("CoastalIsland", FilterImportance.Preferred);

            // Quality Overrides: Boost trade-related features
            preset.MutatorQualityOverrides["Harbor"] = 8;            // Natural port
            preset.MutatorQualityOverrides["Bay"] = 6;               // Sheltered water
            preset.MutatorQualityOverrides["Cove"] = 5;              // Small harbor
            preset.MutatorQualityOverrides["Fjord"] = 7;             // Deep water access
            preset.MutatorQualityOverrides["RiverDelta"] = 7;        // River trade hub
            preset.MutatorQualityOverrides["RiverConfluence"] = 6;   // Rivers meet
            preset.MutatorQualityOverrides["Junkyard"] = 5;          // Salvage value

            return preset;
        }
    }

    /// <summary>
    /// Information about a mutator that's missing from the current runtime.
    /// </summary>
    public class MissingMutatorInfo
    {
        public string DefName { get; set; } = "";
        public string SourceName { get; set; } = "";
        public FilterImportance Importance { get; set; }
        public bool IsBlocking { get; set; }
    }

    /// <summary>
    /// Result of validating a preset's requirements against the current runtime.
    /// Different from PresetValidationResult (in PresetTokenCodec) which validates token imports.
    /// </summary>
    public class RuntimeValidationResult
    {
        public List<MissingMutatorInfo> MissingMutators { get; } = new List<MissingMutatorInfo>();
        public bool HasBlockingMissing { get; set; }
        public bool HasWarningMissing { get; set; }

        public bool HasAnyMissing => HasBlockingMissing || HasWarningMissing;

        /// <summary>
        /// Gets a tooltip string describing missing requirements.
        /// </summary>
        public string GetTooltip()
        {
            if (!HasAnyMissing)
                return "";

            var sb = new System.Text.StringBuilder();

            if (HasBlockingMissing)
            {
                sb.AppendLine("⚠ " + "LandingZone_PresetMissingCritical".Translate());
                foreach (var m in MissingMutators.Where(m => m.IsBlocking))
                {
                    sb.AppendLine($"  • {m.DefName} ({m.SourceName})");
                }
            }

            if (HasWarningMissing)
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.AppendLine("LandingZone_PresetMissingOptional".Translate());
                foreach (var m in MissingMutators.Where(m => !m.IsBlocking).Take(5))
                {
                    sb.AppendLine($"  • {m.DefName} ({m.SourceName})");
                }
                var remaining = MissingMutators.Count(m => !m.IsBlocking) - 5;
                if (remaining > 0)
                {
                    sb.AppendLine($"  ... and {remaining} more");
                }
            }

            return sb.ToString().TrimEnd();
        }
    }
}

#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using LandingZone.Core.Filtering.Filters;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Data
{
    /// <summary>
    /// Represents a single missing or unresolved item from a preset.
    /// </summary>
    public readonly struct MissingPresetItem
    {
        public MissingPresetItem(string category, string defName, string? resolution)
        {
            Category = category;
            DefName = defName;
            Resolution = resolution;
        }

        /// <summary>
        /// Category of the missing item (e.g., "Mutator", "Stone", "Biome", "River")
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// The defName that couldn't be resolved
        /// </summary>
        public string DefName { get; }

        /// <summary>
        /// How LZ will handle this (e.g., "Will be skipped", "Resolved to GL_River")
        /// </summary>
        public string? Resolution { get; }

        /// <summary>
        /// True if this was resolved via alias (not truly missing)
        /// </summary>
        public bool IsResolved => Resolution != null && !Resolution.Contains("skipped");
    }

    /// <summary>
    /// Result of validating a preset's dependencies.
    /// </summary>
    public class PresetValidationResult
    {
        public List<MissingPresetItem> MissingItems { get; } = new List<MissingPresetItem>();
        public List<MissingPresetItem> ResolvedAliases { get; } = new List<MissingPresetItem>();

        /// <summary>
        /// True if there are items that will be skipped (truly missing, not just aliased)
        /// </summary>
        public bool HasMissingItems => MissingItems.Count > 0;

        /// <summary>
        /// True if some items were resolved via alias
        /// </summary>
        public bool HasResolvedAliases => ResolvedAliases.Count > 0;

        /// <summary>
        /// True if validation found any issues worth reporting
        /// </summary>
        public bool HasIssues => HasMissingItems || HasResolvedAliases;

        /// <summary>
        /// Count of items that will be skipped
        /// </summary>
        public int SkippedCount => MissingItems.Count;

        /// <summary>
        /// Generates a user-friendly summary of validation issues.
        /// </summary>
        public string GetSummary()
        {
            var parts = new List<string>();

            if (MissingItems.Count > 0)
            {
                var grouped = MissingItems.GroupBy(m => m.Category);
                foreach (var group in grouped)
                {
                    var names = string.Join(", ", group.Select(m => m.DefName));
                    parts.Add($"{group.Key}: {names}");
                }
            }

            return string.Join("\n", parts);
        }
    }

    /// <summary>
    /// Encodes/decodes presets to/from compact shareable tokens.
    /// Pipeline: Preset ↔ DTO ↔ JSON ↔ GZip ↔ Base64Url
    /// </summary>
    public static class PresetTokenCodec
    {
        private const int CURRENT_VERSION = 1;

        /// <summary>
        /// Exports a preset to a shareable token string.
        /// </summary>
        public static string EncodePreset(Preset preset)
        {
            try
            {
                // 1. Convert Preset → DTO (only non-default values)
                var dto = PresetToDTO(preset);

                // 2. Serialize to JSON (compact)
                byte[] jsonBytes;
                using (var ms = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(PresetTokenDTO));
                    serializer.WriteObject(ms, dto);
                    jsonBytes = ms.ToArray();
                }

                // 3. Compress with GZip (now using jsonBytes directly)
                var compressed = Compress(jsonBytes);

                // 4. Encode to Base64Url (URL-safe, no padding)
                var token = Base64UrlEncode(compressed);

                return token;
            }
            catch (Exception ex)
            {
                Log.Error($"[LandingZone] Failed to encode preset '{preset.Name}': {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Imports a preset from a token string.
        /// Returns (preset, errorMessage, validation) - errorMessage is null on success.
        /// Validation contains info about missing/aliased items even on success.
        /// </summary>
        public static (Preset? preset, string? error, PresetValidationResult? validation) DecodePreset(string token)
        {
            try
            {
                // 1. Decode from Base64Url
                var compressed = Base64UrlDecode(token);
                if (compressed == null)
                    return (null, "Invalid token format - could not decode base64url", null);

                // 2. Decompress with GZip
                var jsonBytes = Decompress(compressed);

                // 3. Deserialize from JSON
                PresetTokenDTO? dto;
                using (var ms = new MemoryStream(jsonBytes))
                {
                    var serializer = new DataContractJsonSerializer(typeof(PresetTokenDTO));
                    dto = serializer.ReadObject(ms) as PresetTokenDTO;
                }

                if (dto == null)
                    return (null, "Invalid token format - could not parse JSON", null);

                // 4. Validate version
                if (dto.Version > CURRENT_VERSION)
                    return (null, $"Token version {dto.Version} is newer than supported version {CURRENT_VERSION}. Please update the mod.", null);

                // 5. Convert DTO → Preset with validation
                var (preset, validationError) = DTOToPreset(dto);
                if (validationError != null)
                    return (null, validationError, null);

                // 6. Validate all defNames and collect missing/aliased items
                var validation = ValidatePresetDependencies(preset!);

                return (preset, null, validation);
            }
            catch (Exception ex)
            {
                return (null, $"Failed to import preset: {ex.Message}", null);
            }
        }

        /// <summary>
        /// Validates all defNames in a preset and returns information about missing/aliased items.
        /// </summary>
        public static PresetValidationResult ValidatePresetDependencies(Preset preset)
        {
            var result = new PresetValidationResult();

            // Validate MapFeatures (mutators)
            foreach (var kvp in preset.Filters.MapFeatures.ItemImportance)
            {
                if (kvp.Value == FilterImportance.Ignored) continue;

                var defName = kvp.Key;
                var resolved = MapFeatureFilter.ResolveToRuntimeMutator(defName);

                if (resolved == null)
                {
                    // Truly missing - will be skipped
                    result.MissingItems.Add(new MissingPresetItem("Map Feature", defName, "Will be skipped"));
                }
                else if (resolved != defName)
                {
                    // Resolved via alias
                    result.ResolvedAliases.Add(new MissingPresetItem("Map Feature", defName, $"Resolved to {resolved}"));
                }
            }

            // Validate Stones
            foreach (var kvp in preset.Filters.Stones.ItemImportance)
            {
                if (kvp.Value == FilterImportance.Ignored) continue;

                var defName = kvp.Key;
                var stoneDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (stoneDef == null)
                {
                    result.MissingItems.Add(new MissingPresetItem("Stone", defName, "Will be skipped"));
                }
            }

            // Validate Rivers
            foreach (var kvp in preset.Filters.Rivers.ItemImportance)
            {
                if (kvp.Value == FilterImportance.Ignored) continue;

                var defName = kvp.Key;
                var riverDef = DefDatabase<RiverDef>.GetNamedSilentFail(defName);
                if (riverDef == null)
                {
                    result.MissingItems.Add(new MissingPresetItem("River", defName, "Will be skipped"));
                }
            }

            // Validate Roads
            foreach (var kvp in preset.Filters.Roads.ItemImportance)
            {
                if (kvp.Value == FilterImportance.Ignored) continue;

                var defName = kvp.Key;
                var roadDef = DefDatabase<RoadDef>.GetNamedSilentFail(defName);
                if (roadDef == null)
                {
                    result.MissingItems.Add(new MissingPresetItem("Road", defName, "Will be skipped"));
                }
            }

            // Validate AdjacentBiomes
            foreach (var kvp in preset.Filters.AdjacentBiomes.ItemImportance)
            {
                if (kvp.Value == FilterImportance.Ignored) continue;

                var defName = kvp.Key;
                var biomeDef = DefDatabase<BiomeDef>.GetNamedSilentFail(defName);
                if (biomeDef == null)
                {
                    result.MissingItems.Add(new MissingPresetItem("Biome", defName, "Will be skipped"));
                }
            }

            // Validate MutatorQualityOverrides (these are also mutator defNames)
            foreach (var kvp in preset.MutatorQualityOverrides)
            {
                var defName = kvp.Key;
                var resolved = MapFeatureFilter.ResolveToRuntimeMutator(defName);

                if (resolved == null)
                {
                    // Check if already reported from MapFeatures
                    if (!result.MissingItems.Any(m => m.DefName == defName && m.Category == "Map Feature"))
                    {
                        result.MissingItems.Add(new MissingPresetItem("Quality Override", defName, "Will be ignored"));
                    }
                }
            }

            // Validate FallbackTiers if present
            if (preset.FallbackTiers != null)
            {
                foreach (var tier in preset.FallbackTiers)
                {
                    // Recursively validate each tier's filters
                    var tierPreset = new Preset { Filters = tier.Filters };
                    var tierResult = ValidatePresetDependencies(tierPreset);

                    // Merge results (avoid duplicates)
                    foreach (var item in tierResult.MissingItems)
                    {
                        if (!result.MissingItems.Any(m => m.DefName == item.DefName && m.Category == item.Category))
                        {
                            result.MissingItems.Add(item);
                        }
                    }
                    foreach (var item in tierResult.ResolvedAliases)
                    {
                        if (!result.ResolvedAliases.Any(m => m.DefName == item.DefName && m.Category == item.Category))
                        {
                            result.ResolvedAliases.Add(item);
                        }
                    }
                }
            }

            return result;
        }

        // ===== PRESET ↔ DTO CONVERSION =====

        private static PresetTokenDTO PresetToDTO(Preset preset)
        {
            var dto = new PresetTokenDTO
            {
                Version = CURRENT_VERSION,
                Name = preset.Name,
                Description = string.IsNullOrEmpty(preset.Description) ? null : preset.Description,
                MinimumStrictness = preset.MinimumStrictness,
                TargetRarity = preset.TargetRarity?.ToString(),
                FilterSummary = string.IsNullOrEmpty(preset.FilterSummary) ? null : preset.FilterSummary,
                MutatorOverrides = preset.MutatorQualityOverrides?.Count > 0 ? preset.MutatorQualityOverrides : null,
                Filters = FilterSettingsToDTO(preset.Filters),
                FallbackTiers = preset.FallbackTiers?.Count > 0
                    ? preset.FallbackTiers.Select(tier => new FallbackTierTokenDTO
                    {
                        Name = tier.Name,
                        Filters = FilterSettingsToDTO(tier.Filters)
                    }).ToList()
                    : null
            };

            return dto;
        }

        private static (Preset? preset, string? error) DTOToPreset(PresetTokenDTO dto)
        {
            // Validate defNames and build FilterSettings
            var (filters, validationError) = DTOToFilterSettings(dto.Filters);
            if (validationError != null)
                return (null, validationError);

            // Parse TargetRarity if provided
            TileRarity? targetRarity = null;
            if (!string.IsNullOrEmpty(dto.TargetRarity))
            {
                if (Enum.TryParse<TileRarity>(dto.TargetRarity, out var rarity))
                    targetRarity = rarity;
            }

            // Parse FallbackTiers if provided
            List<FallbackTier>? fallbackTiers = null;
            if (dto.FallbackTiers != null && dto.FallbackTiers.Count > 0)
            {
                fallbackTiers = new List<FallbackTier>();
                foreach (var tierDto in dto.FallbackTiers)
                {
                    var (tierFilters, tierError) = DTOToFilterSettings(tierDto.Filters);
                    if (tierError != null)
                        return (null, $"Fallback tier '{tierDto.Name}': {tierError}");

                    fallbackTiers.Add(new FallbackTier
                    {
                        Name = tierDto.Name,
                        Filters = tierFilters
                    });
                }
            }

            var preset = new Preset
            {
                Id = Guid.NewGuid().ToString(),
                Name = dto.Name ?? "Imported Preset",
                Description = dto.Description ?? "",
                Category = "User",
                MinimumStrictness = dto.MinimumStrictness,
                TargetRarity = targetRarity,
                FilterSummary = dto.FilterSummary ?? "",
                Filters = filters,
                MutatorQualityOverrides = dto.MutatorOverrides ?? new Dictionary<string, int>(),
                FallbackTiers = fallbackTiers
            };

            return (preset, null);
        }

        private static FilterTokenDTO FilterSettingsToDTO(FilterSettings filters)
        {
            var dto = new FilterTokenDTO();

            // Temperature filters - only include if importance != Ignored
            if (filters.AverageTemperatureImportance != FilterImportance.Ignored)
                dto.AverageTemperature = new object[] { ImportanceToCode(filters.AverageTemperatureImportance), filters.AverageTemperatureRange.min, filters.AverageTemperatureRange.max };

            if (filters.MinimumTemperatureImportance != FilterImportance.Ignored)
                dto.MinimumTemperature = new object[] { ImportanceToCode(filters.MinimumTemperatureImportance), filters.MinimumTemperatureRange.min, filters.MinimumTemperatureRange.max };

            if (filters.MaximumTemperatureImportance != FilterImportance.Ignored)
                dto.MaximumTemperature = new object[] { ImportanceToCode(filters.MaximumTemperatureImportance), filters.MaximumTemperatureRange.min, filters.MaximumTemperatureRange.max };

            // Climate filters
            if (filters.RainfallImportance != FilterImportance.Ignored)
                dto.Rainfall = new object[] { ImportanceToCode(filters.RainfallImportance), filters.RainfallRange.min, filters.RainfallRange.max };

            if (filters.GrowingDaysImportance != FilterImportance.Ignored)
                dto.GrowingDays = new object[] { ImportanceToCode(filters.GrowingDaysImportance), filters.GrowingDaysRange.min, filters.GrowingDaysRange.max };

            if (filters.PollutionImportance != FilterImportance.Ignored)
                dto.Pollution = new object[] { ImportanceToCode(filters.PollutionImportance), filters.PollutionRange.min, filters.PollutionRange.max };

            if (filters.ForageImportance != FilterImportance.Ignored)
                dto.Forageability = new object[] { ImportanceToCode(filters.ForageImportance), filters.ForageabilityRange.min, filters.ForageabilityRange.max };

            // ForageableFood: [defName, importance] (not a range)
            if (filters.ForageableFoodImportance != FilterImportance.Ignored)
                dto.ForageableFood = new object[] { filters.ForageableFoodDefName ?? "", ImportanceToCode(filters.ForageableFoodImportance) };

            if (filters.SwampinessImportance != FilterImportance.Ignored)
                dto.Swampiness = new object[] { ImportanceToCode(filters.SwampinessImportance), filters.SwampinessRange.min, filters.SwampinessRange.max };

            // Wildlife & Ecology
            if (filters.AnimalDensityImportance != FilterImportance.Ignored)
                dto.AnimalDensity = new object[] { ImportanceToCode(filters.AnimalDensityImportance), filters.AnimalDensityRange.min, filters.AnimalDensityRange.max };

            if (filters.FishPopulationImportance != FilterImportance.Ignored)
                dto.FishPopulation = new object[] { ImportanceToCode(filters.FishPopulationImportance), filters.FishPopulationRange.min, filters.FishPopulationRange.max };

            if (filters.PlantDensityImportance != FilterImportance.Ignored)
                dto.PlantDensity = new object[] { ImportanceToCode(filters.PlantDensityImportance), filters.PlantDensityRange.min, filters.PlantDensityRange.max };

            // Geography filters
            if (filters.ElevationImportance != FilterImportance.Ignored)
                dto.Elevation = new object[] { ImportanceToCode(filters.ElevationImportance), filters.ElevationRange.min, filters.ElevationRange.max };

            if (filters.MovementDifficultyImportance != FilterImportance.Ignored)
                dto.MovementDifficulty = new object[] { ImportanceToCode(filters.MovementDifficultyImportance), filters.MovementDifficultyRange.min, filters.MovementDifficultyRange.max };

            // Hilliness - only include if restricted (less than all 4 types)
            if (filters.AllowedHilliness.Count < 4) // Only include if restricted
            {
                dto.Hilliness = filters.AllowedHilliness.Select(h => h.ToString()).ToList();
            }

            if (filters.CoastalImportance != FilterImportance.Ignored)
                dto.Coastal = ImportanceToCode(filters.CoastalImportance);

            if (filters.CoastalLakeImportance != FilterImportance.Ignored)
                dto.CoastalLake = ImportanceToCode(filters.CoastalLakeImportance);

            if (filters.WaterAccessImportance != FilterImportance.Ignored)
                dto.WaterAccess = ImportanceToCode(filters.WaterAccessImportance);

            // Individual importance containers
            dto.Rivers = IndividualImportanceToDTO(filters.Rivers);
            dto.Roads = IndividualImportanceToDTO(filters.Roads);
            dto.Stones = IndividualImportanceToDTO(filters.Stones);
            dto.Stockpiles = IndividualImportanceToDTO(filters.Stockpiles);
            dto.MapFeatures = IndividualImportanceToDTO(filters.MapFeatures);
            dto.AdjacentBiomes = IndividualImportanceToDTO(filters.AdjacentBiomes);
            dto.PlantGrove = IndividualImportanceToDTO(filters.PlantGrove);
            dto.AnimalHabitat = IndividualImportanceToDTO(filters.AnimalHabitat);
            dto.MineralOres = IndividualImportanceToDTO(filters.MineralOres);

            // Resource filters - graze is importance-only
            if (filters.GrazeImportance != FilterImportance.Ignored)
                dto.Graze = ImportanceToCode(filters.GrazeImportance);

            // World features: [defName, importance]
            if (filters.FeatureImportance != FilterImportance.Ignored)
                dto.WorldFeature = new object[] { filters.RequiredFeatureDefName ?? "", ImportanceToCode(filters.FeatureImportance) };

            // Landmark: importance-only
            if (filters.LandmarkImportance != FilterImportance.Ignored)
                dto.Landmark = ImportanceToCode(filters.LandmarkImportance);

            // Biome lock
            if (filters.LockedBiome != null)
                dto.LockedBiome = filters.LockedBiome.defName;

            // MaxResults
            if (filters.MaxResults != FilterSettings.DefaultMaxResults)
                dto.MaxResults = filters.MaxResults;

            return dto;
        }

        private static (FilterSettings filters, string? error) DTOToFilterSettings(FilterTokenDTO dto)
        {
            var filters = new FilterSettings();
            // Start from clean slate - don't inherit default Preferred values
            filters.ClearAll();

            // Temperature filters
            if (dto.AverageTemperature != null)
            {
                var (importance, min, max, error) = ParseRangeFilter(dto.AverageTemperature);
                if (error != null) return (filters, error);
                filters.AverageTemperatureImportance = importance;
                filters.AverageTemperatureRange = new FloatRange((float)min, (float)max);
            }

            if (dto.MinimumTemperature != null)
            {
                var (importance, min, max, error) = ParseRangeFilter(dto.MinimumTemperature);
                if (error != null) return (filters, error);
                filters.MinimumTemperatureImportance = importance;
                filters.MinimumTemperatureRange = new FloatRange((float)min, (float)max);
            }

            if (dto.MaximumTemperature != null)
            {
                var (importance, min, max, error) = ParseRangeFilter(dto.MaximumTemperature);
                if (error != null) return (filters, error);
                filters.MaximumTemperatureImportance = importance;
                filters.MaximumTemperatureRange = new FloatRange((float)min, (float)max);
            }

            // Climate filters
            if (dto.Rainfall != null)
            {
                var (importance, min, max, error) = ParseRangeFilter(dto.Rainfall);
                if (error != null) return (filters, error);
                filters.RainfallImportance = importance;
                filters.RainfallRange = new FloatRange((float)min, (float)max);
            }

            if (dto.GrowingDays != null)
            {
                var (importance, min, max, error) = ParseRangeFilter(dto.GrowingDays);
                if (error != null) return (filters, error);
                filters.GrowingDaysImportance = importance;
                filters.GrowingDaysRange = new FloatRange((float)min, (float)max);
            }

            if (dto.Pollution != null)
            {
                var (importance, min, max, error) = ParseRangeFilter(dto.Pollution);
                if (error != null) return (filters, error);
                filters.PollutionImportance = importance;
                filters.PollutionRange = new FloatRange((float)min, (float)max);
            }

            if (dto.Forageability != null)
            {
                var (importance, min, max, error) = ParseRangeFilter(dto.Forageability);
                if (error != null) return (filters, error);
                filters.ForageImportance = importance;
                filters.ForageabilityRange = new FloatRange((float)min, (float)max);
            }

            // ForageableFood: [defName, importance] (not a range)
            if (dto.ForageableFood != null && dto.ForageableFood.Length == 2)
            {
                filters.ForageableFoodDefName = dto.ForageableFood[0].ToString();
                filters.ForageableFoodImportance = CodeToImportance(dto.ForageableFood[1].ToString() ?? "ign");
            }

            if (dto.Swampiness != null)
            {
                var (importance, min, max, error) = ParseRangeFilter(dto.Swampiness);
                if (error != null) return (filters, error);
                filters.SwampinessImportance = importance;
                filters.SwampinessRange = new FloatRange((float)min, (float)max);
            }

            // Wildlife & Ecology
            if (dto.AnimalDensity != null)
            {
                var (importance, min, max, error) = ParseRangeFilter(dto.AnimalDensity);
                if (error != null) return (filters, error);
                filters.AnimalDensityImportance = importance;
                filters.AnimalDensityRange = new FloatRange((float)min, (float)max);
            }

            if (dto.FishPopulation != null)
            {
                var (importance, min, max, error) = ParseRangeFilter(dto.FishPopulation);
                if (error != null) return (filters, error);
                filters.FishPopulationImportance = importance;
                filters.FishPopulationRange = new FloatRange((float)min, (float)max);
            }

            if (dto.PlantDensity != null)
            {
                var (importance, min, max, error) = ParseRangeFilter(dto.PlantDensity);
                if (error != null) return (filters, error);
                filters.PlantDensityImportance = importance;
                filters.PlantDensityRange = new FloatRange((float)min, (float)max);
            }

            // Geography filters
            if (dto.Elevation != null)
            {
                var (importance, min, max, error) = ParseRangeFilter(dto.Elevation);
                if (error != null) return (filters, error);
                filters.ElevationImportance = importance;
                filters.ElevationRange = new FloatRange((float)min, (float)max);
            }

            if (dto.MovementDifficulty != null)
            {
                var (importance, min, max, error) = ParseRangeFilter(dto.MovementDifficulty);
                if (error != null) return (filters, error);
                filters.MovementDifficultyImportance = importance;
                filters.MovementDifficultyRange = new FloatRange((float)min, (float)max);
            }

            // Hilliness - List<string> of allowed hilliness types
            if (dto.Hilliness != null && dto.Hilliness.Count > 0)
            {
                filters.AllowedHilliness.Clear();
                foreach (var hillinessStr in dto.Hilliness)
                {
                    if (Enum.TryParse<Hilliness>(hillinessStr, out var hilliness))
                        filters.AllowedHilliness.Add(hilliness);
                    else
                        return (filters, $"Invalid hilliness value: {hillinessStr}");
                }
            }

            if (dto.Coastal != null)
                filters.CoastalImportance = CodeToImportance(dto.Coastal);

            if (dto.CoastalLake != null)
                filters.CoastalLakeImportance = CodeToImportance(dto.CoastalLake);

            if (dto.WaterAccess != null)
                filters.WaterAccessImportance = CodeToImportance(dto.WaterAccess);

            // Individual importance containers - all use string keys (defNames)
            if (dto.Rivers != null)
            {
                var (container, error) = DTOToIndividualImportance<string>(dto.Rivers, asString: true);
                if (error != null) return (filters, error);
                filters.Rivers = container;
            }

            if (dto.Roads != null)
            {
                var (container, error) = DTOToIndividualImportance<string>(dto.Roads, asString: true);
                if (error != null) return (filters, error);
                filters.Roads = container;
            }

            if (dto.Stones != null)
            {
                var (container, error) = DTOToIndividualImportance<string>(dto.Stones, asString: true);
                if (error != null) return (filters, error);
                filters.Stones = container;
            }

            if (dto.Stockpiles != null)
            {
                var (container, error) = DTOToIndividualImportance<string>(dto.Stockpiles, asString: true);
                if (error != null) return (filters, error);
                filters.Stockpiles = container;
            }

            if (dto.MapFeatures != null)
            {
                var (container, error) = DTOToIndividualImportance<string>(dto.MapFeatures, asString: true);
                if (error != null) return (filters, error);
                filters.MapFeatures = container;
            }

            if (dto.AdjacentBiomes != null)
            {
                var (container, error) = DTOToIndividualImportance<string>(dto.AdjacentBiomes, asString: true);
                if (error != null) return (filters, error);
                filters.AdjacentBiomes = container;
            }

            if (dto.PlantGrove != null)
            {
                var (container, error) = DTOToIndividualImportance<string>(dto.PlantGrove, asString: true);
                if (error != null) return (filters, error);
                filters.PlantGrove = container;
            }

            if (dto.AnimalHabitat != null)
            {
                var (container, error) = DTOToIndividualImportance<string>(dto.AnimalHabitat, asString: true);
                if (error != null) return (filters, error);
                filters.AnimalHabitat = container;
            }

            if (dto.MineralOres != null)
            {
                var (container, error) = DTOToIndividualImportance<string>(dto.MineralOres, asString: true);
                if (error != null) return (filters, error);
                filters.MineralOres = container;
            }

            // Resource filters - graze is importance-only
            if (dto.Graze != null)
                filters.GrazeImportance = CodeToImportance(dto.Graze);

            // World features: [defName, importance]
            if (dto.WorldFeature != null && dto.WorldFeature.Length == 2)
            {
                filters.RequiredFeatureDefName = dto.WorldFeature[0].ToString();
                filters.FeatureImportance = CodeToImportance(dto.WorldFeature[1].ToString() ?? "ign");
            }

            // Landmark: importance-only
            if (dto.Landmark != null)
                filters.LandmarkImportance = CodeToImportance(dto.Landmark);

            // Biome lock
            if (dto.LockedBiome != null)
            {
                var biome = DefDatabase<BiomeDef>.GetNamedSilentFail(dto.LockedBiome);
                if (biome == null)
                    return (filters, $"Unknown biome: {dto.LockedBiome}");
                filters.LockedBiome = biome;
            }

            // MaxResults
            if (dto.MaxResults.HasValue)
                filters.MaxResults = dto.MaxResults.Value;

            return (filters, null);
        }

        // ===== HELPER METHODS =====

        private static IndividualImportanceTokenDTO? IndividualImportanceToDTO<T>(IndividualImportanceContainer<T> container) where T : notnull
        {
            if (container.ItemImportance.Count == 0)
                return null;

            var dto = new IndividualImportanceTokenDTO
            {
                Operator = container.Operator == ImportanceOperator.AND ? "AND" : "OR"
            };

            var mustHaves = new List<string>();
            var mustNotHaves = new List<string>();
            var priorities = new List<string>();
            var preferreds = new List<string>();
            var ignoreds = new List<string>();

            foreach (var kvp in container.ItemImportance)
            {
                var keyObj = kvp.Key;
                if (keyObj == null) continue;
                string defName = typeof(T) == typeof(string) ? keyObj.ToString()! : (keyObj as Def)?.defName ?? "";
                if (string.IsNullOrEmpty(defName)) continue;

                switch (kvp.Value)
                {
                    case FilterImportance.MustHave:  // Critical is alias for MustHave
                        mustHaves.Add(defName);
                        break;
                    case FilterImportance.MustNotHave:
                        mustNotHaves.Add(defName);
                        break;
                    case FilterImportance.Priority:
                        priorities.Add(defName);
                        break;
                    case FilterImportance.Preferred:
                        preferreds.Add(defName);
                        break;
                    case FilterImportance.Ignored:
                        ignoreds.Add(defName);
                        break;
                }
            }

            if (mustHaves.Count > 0) dto.MustHave = mustHaves;
            if (mustNotHaves.Count > 0) dto.MustNotHave = mustNotHaves;
            if (priorities.Count > 0) dto.Priority = priorities;
            if (preferreds.Count > 0) dto.Preferred = preferreds;
            if (ignoreds.Count > 0) dto.Ignored = ignoreds;

            return dto;
        }

        private static (IndividualImportanceContainer<T> container, string? error) DTOToIndividualImportance<T>(
            IndividualImportanceTokenDTO dto,
            IEnumerable<T>? validDefs = null,
            bool asString = false) where T : notnull
        {
            var container = new IndividualImportanceContainer<T>
            {
                Operator = dto.Operator == "OR" ? ImportanceOperator.OR : ImportanceOperator.AND
            };

            // Helper to process a list of defNames with a given importance
            void ProcessList(List<string>? list, FilterImportance importance)
            {
                if (list == null) return;
                foreach (var defName in list)
                {
                    if (asString)
                    {
                        container.ItemImportance[(T)(object)defName] = importance;
                    }
                }
            }

            // v1 legacy: Critical maps to MustHave
            ProcessList(dto.Critical, FilterImportance.MustHave);

            // v2: Full 5-state support
            ProcessList(dto.MustHave, FilterImportance.MustHave);
            ProcessList(dto.MustNotHave, FilterImportance.MustNotHave);
            ProcessList(dto.Priority, FilterImportance.Priority);
            ProcessList(dto.Preferred, FilterImportance.Preferred);
            ProcessList(dto.Ignored, FilterImportance.Ignored);

            return (container, null);
        }

        private static (FilterImportance importance, double min, double max, string? error) ParseRangeFilter(object[] array)
        {
            if (array.Length != 3)
                return (FilterImportance.Ignored, 0, 0, "Invalid range filter format - expected [importance, min, max]");

            var importance = CodeToImportance(array[0].ToString() ?? "ign");

            if (!double.TryParse(array[1].ToString(), out var min))
                return (FilterImportance.Ignored, 0, 0, $"Invalid min value: {array[1]}");

            if (!double.TryParse(array[2].ToString(), out var max))
                return (FilterImportance.Ignored, 0, 0, $"Invalid max value: {array[2]}");

            return (importance, min, max, null);
        }

        private static string ImportanceToCode(FilterImportance importance)
        {
            return importance switch
            {
                FilterImportance.MustHave => "must",      // Critical is alias for MustHave
                FilterImportance.MustNotHave => "mustNot",
                FilterImportance.Priority => "prio",
                FilterImportance.Preferred => "pref",
                FilterImportance.Ignored => "ign",
                _ => "ign"
            };
        }

        private static FilterImportance CodeToImportance(string code)
        {
            return code switch
            {
                "must" => FilterImportance.MustHave,
                "crit" => FilterImportance.MustHave,      // v1 legacy compatibility
                "mustNot" => FilterImportance.MustNotHave,
                "prio" => FilterImportance.Priority,
                "pref" => FilterImportance.Preferred,
                "ign" => FilterImportance.Ignored,
                _ => FilterImportance.Ignored
            };
        }

        // ===== WORKSPACE SERIALIZATION =====

        /// <summary>
        /// Serializes a BucketWorkspace to a WorkspaceTokenDTO.
        /// </summary>
        public static WorkspaceTokenDTO? WorkspaceToDTO(LandingZone.Core.UI.BucketWorkspace? workspace)
        {
            if (workspace == null) return null;

            var dto = new WorkspaceTokenDTO
            {
                MustHave = BucketToDTO(workspace, FilterImportance.MustHave),
                MustNotHave = BucketToDTO(workspace, FilterImportance.MustNotHave),
                Priority = BucketToDTO(workspace, FilterImportance.Priority),
                Preferred = BucketToDTO(workspace, FilterImportance.Preferred)
            };

            // Return null if entirely empty
            if (dto.MustHave == null && dto.MustNotHave == null &&
                dto.Priority == null && dto.Preferred == null)
                return null;

            return dto;
        }

        private static BucketTokenDTO? BucketToDTO(LandingZone.Core.UI.BucketWorkspace workspace, FilterImportance importance)
        {
            var clauses = workspace.GetClausesInBucket(importance);
            if (clauses == null || !clauses.Any(c => c.Chips.Count > 0)) return null;

            var dto = new BucketTokenDTO();

            foreach (var clause in clauses.Where(c => c.Chips.Count > 0))
            {
                var clauseDto = new ClauseTokenDTO();
                foreach (var chip in clause.Chips)
                {
                    clauseDto.Chips.Add(new ChipTokenDTO
                    {
                        Id = chip.FilterId,
                        OrGroupId = chip.OrGroupId,
                        ValueDisplay = chip.ValueDisplay
                    });
                }
                dto.Clauses.Add(clauseDto);
            }

            return dto.Clauses.Count > 0 ? dto : null;
        }

        /// <summary>
        /// Deserializes a WorkspaceTokenDTO into a BucketWorkspace.
        /// </summary>
        public static void DTOToWorkspace(WorkspaceTokenDTO? dto, LandingZone.Core.UI.BucketWorkspace workspace)
        {
            if (dto == null || workspace == null) return;

            // Note: Caller should clear workspace before calling if needed
            DTOToBucket(dto.MustHave, workspace, FilterImportance.MustHave);
            DTOToBucket(dto.MustNotHave, workspace, FilterImportance.MustNotHave);
            DTOToBucket(dto.Priority, workspace, FilterImportance.Priority);
            DTOToBucket(dto.Preferred, workspace, FilterImportance.Preferred);
        }

        private static void DTOToBucket(BucketTokenDTO? dto, LandingZone.Core.UI.BucketWorkspace workspace, FilterImportance importance)
        {
            if (dto?.Clauses == null || dto.Clauses.Count == 0) return;

            for (int i = 0; i < dto.Clauses.Count; i++)
            {
                var clauseDto = dto.Clauses[i];
                if (clauseDto.Chips.Count == 0) continue;

                // Create clause (first clause may already exist)
                var existingClauses = workspace.GetClausesInBucket(importance);
                LandingZone.Core.UI.BucketWorkspace.Clause clause;
                if (i < existingClauses.Count)
                {
                    clause = existingClauses[i];
                }
                else
                {
                    clause = workspace.AddClause(importance);
                }

                foreach (var chipDto in clauseDto.Chips)
                {
                    // Parse filter ID to extract category (format: "category:value" or just "filterId")
                    var parts = chipDto.Id.Split(new[] { ':' }, 2);
                    var category = parts.Length == 2 ? parts[0] : "scalar";
                    var label = parts.Length == 2 ? parts[1] : chipDto.Id;

                    // Determine if heavy filter (containers are heavy)
                    bool isHeavy = category is "stones" or "mineral_ores" or "plant_grove" or "animal_habitat" or "stockpiles";

                    var chip = new LandingZone.Core.UI.BucketWorkspace.FilterChip(
                        chipDto.Id, label, isHeavy, category, chipDto.ValueDisplay);

                    workspace.AddChipToClause(chip, clause.ClauseId);

                    // Restore OR group if present
                    if (chipDto.OrGroupId.HasValue)
                    {
                        chip.OrGroupId = chipDto.OrGroupId;
                    }
                }
            }
        }

        // ===== COMPRESSION & ENCODING =====

        private static byte[] Compress(byte[] data)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionMode.Compress))
            {
                gzip.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }

        private static byte[] Decompress(byte[] data)
        {
            using var input = new MemoryStream(data);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }

        private static string Base64UrlEncode(byte[] data)
        {
            var base64 = Convert.ToBase64String(data);
            // Convert to base64url: replace +/= with -_
            return base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        private static byte[]? Base64UrlDecode(string base64Url)
        {
            try
            {
                // Convert from base64url: replace -_ with +/
                var base64 = base64Url.Replace('-', '+').Replace('_', '/');

                // Add padding if needed
                switch (base64.Length % 4)
                {
                    case 2: base64 += "=="; break;
                    case 3: base64 += "="; break;
                }

                return Convert.FromBase64String(base64);
            }
            catch
            {
                return null;
            }
        }
    }
}

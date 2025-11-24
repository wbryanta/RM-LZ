using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Data
{
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
        /// Returns (preset, errorMessage) - errorMessage is null on success.
        /// </summary>
        public static (Preset? preset, string? error) DecodePreset(string token)
        {
            try
            {
                // 1. Decode from Base64Url
                var compressed = Base64UrlDecode(token);
                if (compressed == null)
                    return (null, "Invalid token format - could not decode base64url");

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
                    return (null, "Invalid token format - could not parse JSON");

                // 4. Validate version
                if (dto.Version > CURRENT_VERSION)
                    return (null, $"Token version {dto.Version} is newer than supported version {CURRENT_VERSION}. Please update the mod.");

                // 5. Convert DTO → Preset with validation
                var (preset, validationError) = DTOToPreset(dto);
                if (validationError != null)
                    return (null, validationError);

                return (preset, null);
            }
            catch (Exception ex)
            {
                return (null, $"Failed to import preset: {ex.Message}");
            }
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

        private static IndividualImportanceTokenDTO? IndividualImportanceToDTO<T>(IndividualImportanceContainer<T> container)
        {
            if (container.ItemImportance.Count == 0)
                return null;

            var dto = new IndividualImportanceTokenDTO
            {
                Operator = container.Operator == ImportanceOperator.AND ? "AND" : "OR"
            };

            var criticals = new List<string>();
            var preferreds = new List<string>();
            var ignoreds = new List<string>();

            foreach (var kvp in container.ItemImportance)
            {
                string defName = typeof(T) == typeof(string) ? kvp.Key.ToString()! : (kvp.Key as Def)?.defName ?? "";
                if (string.IsNullOrEmpty(defName)) continue;

                switch (kvp.Value)
                {
                    case FilterImportance.Critical:
                        criticals.Add(defName);
                        break;
                    case FilterImportance.Preferred:
                        preferreds.Add(defName);
                        break;
                    case FilterImportance.Ignored:
                        ignoreds.Add(defName);
                        break;
                }
            }

            if (criticals.Count > 0) dto.Critical = criticals;
            if (preferreds.Count > 0) dto.Preferred = preferreds;
            if (ignoreds.Count > 0) dto.Ignored = ignoreds;

            return dto;
        }

        private static (IndividualImportanceContainer<T> container, string? error) DTOToIndividualImportance<T>(
            IndividualImportanceTokenDTO dto,
            IEnumerable<T>? validDefs = null,
            bool asString = false)
        {
            var container = new IndividualImportanceContainer<T>
            {
                Operator = dto.Operator == "OR" ? ImportanceOperator.OR : ImportanceOperator.AND
            };

            // Since all FilterSettings containers use string keys (defNames), asString is always true
            // validDefs parameter is unused but kept for future extensibility
            IEnumerable<T> defs = Enumerable.Empty<T>();

            // Process critical items
            if (dto.Critical != null)
            {
                foreach (var defName in dto.Critical)
                {
                    if (asString)
                    {
                        container.ItemImportance[(T)(object)defName] = FilterImportance.Critical;
                    }
                    else
                    {
                        var def = defs.FirstOrDefault(d => (d as Def)?.defName == defName);
                        if (def == null)
                            return (container, $"Unknown def: {defName}");
                        container.ItemImportance[def] = FilterImportance.Critical;
                    }
                }
            }

            // Process preferred items
            if (dto.Preferred != null)
            {
                foreach (var defName in dto.Preferred)
                {
                    if (asString)
                    {
                        container.ItemImportance[(T)(object)defName] = FilterImportance.Preferred;
                    }
                    else
                    {
                        var def = defs.FirstOrDefault(d => (d as Def)?.defName == defName);
                        if (def == null)
                            return (container, $"Unknown def: {defName}");
                        container.ItemImportance[def] = FilterImportance.Preferred;
                    }
                }
            }

            // Process ignored items
            if (dto.Ignored != null)
            {
                foreach (var defName in dto.Ignored)
                {
                    if (asString)
                    {
                        container.ItemImportance[(T)(object)defName] = FilterImportance.Ignored;
                    }
                    else
                    {
                        var def = defs.FirstOrDefault(d => (d as Def)?.defName == defName);
                        if (def == null)
                            return (container, $"Unknown def: {defName}");
                        container.ItemImportance[def] = FilterImportance.Ignored;
                    }
                }
            }

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
                FilterImportance.Critical => "crit",
                FilterImportance.Preferred => "pref",
                FilterImportance.Ignored => "ign",
                _ => "ign"
            };
        }

        private static FilterImportance CodeToImportance(string code)
        {
            return code switch
            {
                "crit" => FilterImportance.Critical,
                "pref" => FilterImportance.Preferred,
                "ign" => FilterImportance.Ignored,
                _ => FilterImportance.Ignored
            };
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

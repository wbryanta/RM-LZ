using System.Collections.Generic;
using System.Runtime.Serialization;
using RimWorld.Planet;

namespace LandingZone.Data
{
    /// <summary>
    /// Data Transfer Object for compact preset serialization.
    /// Only includes non-default values to minimize token size.
    /// </summary>
    [DataContract]
    public class PresetTokenDTO
    {
        [DataMember(Name = "ver")]
        public int Version { get; set; } = 1;

        [DataMember(Name = "name")]
        public string Name { get; set; } = "";

        [DataMember(Name = "desc", EmitDefaultValue = false)]
        public string? Description { get; set; }

        [DataMember(Name = "strict", EmitDefaultValue = false)]
        public float? MinimumStrictness { get; set; }

        [DataMember(Name = "rarity", EmitDefaultValue = false)]
        public string? TargetRarity { get; set; }

        [DataMember(Name = "summary", EmitDefaultValue = false)]
        public string? FilterSummary { get; set; }

        [DataMember(Name = "filters")]
        public FilterTokenDTO Filters { get; set; } = new FilterTokenDTO();

        [DataMember(Name = "mutators", EmitDefaultValue = false)]
        public Dictionary<string, int>? MutatorOverrides { get; set; }

        [DataMember(Name = "fallbacks", EmitDefaultValue = false)]
        public List<FallbackTierTokenDTO>? FallbackTiers { get; set; }
    }

    /// <summary>
    /// Compact representation of FallbackTier.
    /// Each tier: { "name": "Tier Name", "filters": {...} }
    /// </summary>
    [DataContract]
    public class FallbackTierTokenDTO
    {
        [DataMember(Name = "name")]
        public string Name { get; set; } = "";

        [DataMember(Name = "filters")]
        public FilterTokenDTO Filters { get; set; } = new FilterTokenDTO();
    }

    /// <summary>
    /// Compact filter settings representation.
    /// Uses short codes: "crit"/"pref"/"ign" for importance, "AND"/"OR" for operators.
    /// Each range filter is [importance, min, max].
    /// </summary>
    [DataContract]
    public class FilterTokenDTO
    {
        // Temperature filters (compact: [importance, min, max])
        [DataMember(Name = "avg", EmitDefaultValue = false)]
        public object[]? AverageTemperature { get; set; }

        [DataMember(Name = "min", EmitDefaultValue = false)]
        public object[]? MinimumTemperature { get; set; }

        [DataMember(Name = "max", EmitDefaultValue = false)]
        public object[]? MaximumTemperature { get; set; }

        // Climate filters
        [DataMember(Name = "rain", EmitDefaultValue = false)]
        public object[]? Rainfall { get; set; }

        [DataMember(Name = "grow", EmitDefaultValue = false)]
        public object[]? GrowingDays { get; set; }

        [DataMember(Name = "poll", EmitDefaultValue = false)]
        public object[]? Pollution { get; set; }

        [DataMember(Name = "forage", EmitDefaultValue = false)]
        public object[]? Forageability { get; set; }

        // Forageable food: [defName, importance] (string selection, not range)
        [DataMember(Name = "forageFood", EmitDefaultValue = false)]
        public object[]? ForageableFood { get; set; }

        [DataMember(Name = "swamp", EmitDefaultValue = false)]
        public object[]? Swampiness { get; set; }

        // Wildlife & Ecology
        [DataMember(Name = "animal", EmitDefaultValue = false)]
        public object[]? AnimalDensity { get; set; }

        [DataMember(Name = "fish", EmitDefaultValue = false)]
        public object[]? FishPopulation { get; set; }

        [DataMember(Name = "plant", EmitDefaultValue = false)]
        public object[]? PlantDensity { get; set; }

        // Geography filters
        [DataMember(Name = "elev", EmitDefaultValue = false)]
        public object[]? Elevation { get; set; }

        [DataMember(Name = "move", EmitDefaultValue = false)]
        public object[]? MovementDifficulty { get; set; }

        [DataMember(Name = "hill", EmitDefaultValue = false)]
        public List<string>? Hilliness { get; set; } // List of hilliness type names (e.g., "Flat", "SmallHills")

        [DataMember(Name = "coastal", EmitDefaultValue = false)]
        public string? Coastal { get; set; }

        [DataMember(Name = "coastalLake", EmitDefaultValue = false)]
        public string? CoastalLake { get; set; }

        [DataMember(Name = "water", EmitDefaultValue = false)]
        public string? WaterAccess { get; set; }

        // Individual importance containers (rivers, roads, stones, etc.)
        [DataMember(Name = "rivers", EmitDefaultValue = false)]
        public IndividualImportanceTokenDTO? Rivers { get; set; }

        [DataMember(Name = "roads", EmitDefaultValue = false)]
        public IndividualImportanceTokenDTO? Roads { get; set; }

        [DataMember(Name = "stones", EmitDefaultValue = false)]
        public IndividualImportanceTokenDTO? Stones { get; set; }

        [DataMember(Name = "stockpiles", EmitDefaultValue = false)]
        public IndividualImportanceTokenDTO? Stockpiles { get; set; }

        [DataMember(Name = "features", EmitDefaultValue = false)]
        public IndividualImportanceTokenDTO? MapFeatures { get; set; }

        [DataMember(Name = "adjBiomes", EmitDefaultValue = false)]
        public IndividualImportanceTokenDTO? AdjacentBiomes { get; set; }

        // Resource filters - graze is importance-only
        [DataMember(Name = "graze", EmitDefaultValue = false)]
        public string? Graze { get; set; }

        // World features: [defName, importance]
        [DataMember(Name = "feature", EmitDefaultValue = false)]
        public object[]? WorldFeature { get; set; }

        // Landmark: importance-only
        [DataMember(Name = "landmark", EmitDefaultValue = false)]
        public string? Landmark { get; set; }

        // Biome lock
        [DataMember(Name = "biome", EmitDefaultValue = false)]
        public string? LockedBiome { get; set; }

        // Max results (lives on FilterSettings, not Preset)
        [DataMember(Name = "limit", EmitDefaultValue = false)]
        public int? MaxResults { get; set; }
    }

    /// <summary>
    /// Compact representation of IndividualImportanceContainer.
    /// Format: { "op": "AND|OR", "crit": ["defName1"], "pref": ["defName2"] }
    /// </summary>
    [DataContract]
    public class IndividualImportanceTokenDTO
    {
        [DataMember(Name = "op")]
        public string Operator { get; set; } = "AND";

        [DataMember(Name = "crit", EmitDefaultValue = false)]
        public List<string>? Critical { get; set; }

        [DataMember(Name = "pref", EmitDefaultValue = false)]
        public List<string>? Preferred { get; set; }

        [DataMember(Name = "ign", EmitDefaultValue = false)]
        public List<string>? Ignored { get; set; }
    }
}

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
        public FloatRange TemperatureRange { get; set; } = new FloatRange(10f, 32f);
        public FloatRange RainfallRange { get; set; } = new FloatRange(1000f, 2200f);
        public FloatRange GrowingDaysRange { get; set; } = new FloatRange(40f, 60f);
        public FloatRange PollutionRange { get; set; } = new FloatRange(0f, 0.25f);
        public FloatRange ForageabilityRange { get; set; } = new FloatRange(0.5f, 1f);
        public const int DefaultMaxResults = 10;
        public const int MaxResultsLimit = 25;

        public FloatRange MovementDifficultyRange { get; set; } = new FloatRange(0f, 2f);
        public FilterImportance TemperatureImportance { get; set; } = FilterImportance.Preferred;
        public FilterImportance RainfallImportance { get; set; } = FilterImportance.Preferred;
        public FilterImportance GrowingDaysImportance { get; set; } = FilterImportance.Preferred;
        public FilterImportance PollutionImportance { get; set; } = FilterImportance.Preferred;
        public FilterImportance ForageImportance { get; set; } = FilterImportance.Preferred;
        public FilterImportance MovementDifficultyImportance { get; set; } = FilterImportance.Preferred;
        public FilterImportance CoastalImportance { get; set; } = FilterImportance.Ignored;
        public FilterImportance RiverImportance { get; set; } = FilterImportance.Critical;
        public FilterImportance GrazeImportance { get; set; } = FilterImportance.Ignored;
        public FilterImportance FeatureImportance { get; set; } = FilterImportance.Ignored;
        public FilterImportance StoneImportance { get; set; } = FilterImportance.Preferred;
        public BiomeDef? LockedBiome { get; set; }
        public string? RequiredFeatureDefName { get; set; }
        public HashSet<string> RequiredStoneDefNames { get; } = new HashSet<string>();
        public HashSet<Hilliness> AllowedHilliness { get; } = new HashSet<Hilliness>
        {
            Hilliness.SmallHills,
            Hilliness.LargeHills,
            Hilliness.Mountainous
        };
        private int _maxResults = DefaultMaxResults;
        public int MaxResults
        {
            get => _maxResults;
            set => _maxResults = Mathf.Clamp(value, 1, MaxResultsLimit);
        }

        public void Reset()
        {
            TemperatureRange = new FloatRange(10f, 32f);
            RainfallRange = new FloatRange(1000f, 2200f);
            GrowingDaysRange = new FloatRange(40f, 60f);
            PollutionRange = new FloatRange(0f, 0.25f);
            ForageabilityRange = new FloatRange(0.5f, 1f);
            MovementDifficultyRange = new FloatRange(0f, 2f);
            TemperatureImportance = FilterImportance.Preferred;
            RainfallImportance = FilterImportance.Preferred;
            GrowingDaysImportance = FilterImportance.Preferred;
            PollutionImportance = FilterImportance.Preferred;
            ForageImportance = FilterImportance.Preferred;
            MovementDifficultyImportance = FilterImportance.Preferred;
            CoastalImportance = FilterImportance.Ignored;
            RiverImportance = FilterImportance.Critical;
            GrazeImportance = FilterImportance.Ignored;
            FeatureImportance = FilterImportance.Ignored;
            StoneImportance = FilterImportance.Preferred;
            LockedBiome = null;
            RequiredFeatureDefName = null;
            RequiredStoneDefNames.Clear();
            RequiredStoneDefNames.Add("Granite");
            RequiredStoneDefNames.Add("Limestone");
            RequiredStoneDefNames.Add("Sandstone");
            AllowedHilliness.Clear();
            AllowedHilliness.Add(Hilliness.SmallHills);
            AllowedHilliness.Add(Hilliness.LargeHills);
            AllowedHilliness.Add(Hilliness.Mountainous);
            MaxResults = DefaultMaxResults;
        }
    }

    public enum FilterImportance : byte
    {
        Ignored = 0,
        Preferred = 1,
        Critical = 2
    }
}

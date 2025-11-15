using System.Collections.Generic;
using UnityEngine;

namespace LandingZone.Data
{
    public readonly struct MatchBreakdown
    {
        public MatchBreakdown(
            bool temperatureEnabled,
            float temperatureScore,
            bool rainfallEnabled,
            float rainfallScore,
            bool growingEnabled,
            float growingScore,
            bool pollutionEnabled,
            float pollutionScore,
            bool forageEnabled,
            float forageScore,
            bool movementEnabled,
            float movementScore,
            FilterImportance coastalImportance,
            bool hasCoastal,
            FilterImportance riverImportance,
            bool hasRiver,
            FilterImportance featureImportance,
            bool featureMatched,
            string? requiredFeature,
            FilterImportance grazeImportance,
            bool canGraze,
            FilterImportance stoneImportance,
            int requiredStoneCount,
            int stoneMatches,
            bool hillinessAllowed,
            float mutatorScore,
            IReadOnlyList<string>? tileMutators,
            float finalScore)
        {
            TemperatureEnabled = temperatureEnabled;
            TemperatureScore = Mathf.Clamp01(temperatureScore);
            RainfallEnabled = rainfallEnabled;
            RainfallScore = Mathf.Clamp01(rainfallScore);
            GrowingSeasonEnabled = growingEnabled;
            GrowingSeasonScore = Mathf.Clamp01(growingScore);
            PollutionEnabled = pollutionEnabled;
            PollutionScore = Mathf.Clamp01(pollutionScore);
            ForageEnabled = forageEnabled;
            ForageScore = Mathf.Clamp01(forageScore);
            MovementEnabled = movementEnabled;
            MovementScore = Mathf.Clamp01(movementScore);
            CoastalImportance = coastalImportance;
            HasCoastal = hasCoastal;
            RiverImportance = riverImportance;
            HasRiver = hasRiver;
            FeatureImportance = featureImportance;
            FeatureMatched = featureMatched;
            RequiredFeature = requiredFeature;
            GrazeImportance = grazeImportance;
            CanGraze = canGraze;
            StoneImportance = stoneImportance;
            RequiredStoneCount = requiredStoneCount;
            StoneMatches = stoneMatches;
            HillinessAllowed = hillinessAllowed;
            MutatorScore = Mathf.Clamp01(mutatorScore);
            TileMutators = tileMutators;
            FinalScore = Mathf.Clamp01(finalScore);
        }

        public bool TemperatureEnabled { get; }
        public float TemperatureScore { get; }
        public bool RainfallEnabled { get; }
        public float RainfallScore { get; }
        public bool GrowingSeasonEnabled { get; }
        public float GrowingSeasonScore { get; }
        public bool PollutionEnabled { get; }
        public float PollutionScore { get; }
        public bool ForageEnabled { get; }
        public float ForageScore { get; }
        public bool MovementEnabled { get; }
        public float MovementScore { get; }
        public FilterImportance CoastalImportance { get; }
        public bool HasCoastal { get; }
        public FilterImportance RiverImportance { get; }
        public bool HasRiver { get; }
        public FilterImportance FeatureImportance { get; }
        public bool FeatureMatched { get; }
        public string? RequiredFeature { get; }
        public FilterImportance GrazeImportance { get; }
        public bool CanGraze { get; }
        public FilterImportance StoneImportance { get; }
        public int RequiredStoneCount { get; }
        public int StoneMatches { get; }
        public bool HillinessAllowed { get; }
        public float MutatorScore { get; }
        public IReadOnlyList<string>? TileMutators { get; }
        public float FinalScore { get; }

        public bool RequiresFeature => FeatureImportance == FilterImportance.Critical;
    }
}

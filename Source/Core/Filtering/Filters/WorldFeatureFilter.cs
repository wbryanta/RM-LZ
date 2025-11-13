using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by world features (FeatureDef - mountain ranges, river sources, etc.).
    /// Uses Preferred/Critical importance with optional specific feature requirement.
    /// </summary>
    public sealed class WorldFeatureFilter : ISiteFilter
    {
        public string Id => "world_feature";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.State.Preferences.Filters;
            var importance = filters.FeatureImportance;

            if (importance == FilterImportance.Ignored)
                return inputTiles;

            // K-of-N architecture: Apply() only filters for Critical.
            // Preferred is handled by scoring phase.
            if (importance != FilterImportance.Critical)
                return inputTiles;

            var requiredFeatureDefName = filters.RequiredFeatureDefName;

            // Build feature lookup once for efficiency
            var tileFeatureMap = BuildTileFeatureLookup();

            return inputTiles.Where(id =>
            {
                if (!tileFeatureMap.TryGetValue(id, out var featureDefName))
                    return false;

                // If specific feature required, check for exact match
                if (!string.IsNullOrEmpty(requiredFeatureDefName))
                {
                    return featureDefName == requiredFeatureDefName;
                }

                // If no specific feature required, just require ANY feature
                return true;
            });
        }

        public string Describe(FilterContext context)
        {
            var filters = context.State.Preferences.Filters;
            var importance = filters.FeatureImportance;

            if (importance == FilterImportance.Ignored)
                return "World features ignored";

            var featureDefName = filters.RequiredFeatureDefName;
            if (!string.IsNullOrEmpty(featureDefName))
            {
                string importanceLabel = importance == FilterImportance.Critical ? " (required)" : " (preferred)";
                return $"World feature: {featureDefName}{importanceLabel}";
            }

            string label = importance == FilterImportance.Critical ? "Any world feature required" : "World features preferred";
            return label;
        }

        /// <summary>
        /// Builds a dictionary mapping tile IDs to their world feature defNames.
        /// This is done once per filter application for efficiency (O(m) instead of O(n*m)).
        /// </summary>
        private static Dictionary<int, string> BuildTileFeatureLookup()
        {
            var tileFeatureMap = new Dictionary<int, string>();
            var worldFeatures = Find.World?.features;

            if (worldFeatures == null)
                return tileFeatureMap;

            // Iterate through all world features once and build the lookup map
            foreach (var feature in worldFeatures.features)
            {
                if (feature?.Tiles != null && feature.def?.defName != null)
                {
                    foreach (var tileId in feature.Tiles)
                    {
                        // If a tile belongs to multiple features, keep the first one
                        if (!tileFeatureMap.ContainsKey(tileId))
                        {
                            tileFeatureMap[tileId] = feature.def.defName;
                        }
                    }
                }
            }

            return tileFeatureMap;
        }
    }
}

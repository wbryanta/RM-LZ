using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by a specific individual stone type (e.g., just granite, just marble).
    /// Each stone type gets its own filter with independent importance level.
    /// Uses Heavy weight since it requires expensive tile data computation.
    /// </summary>
    public sealed class IndividualStoneFilter : ISiteFilter
    {
        private readonly string _stoneDefName;
        private readonly string _displayName;

        public IndividualStoneFilter(string stoneDefName, string displayName)
        {
            _stoneDefName = stoneDefName;
            _displayName = displayName;
        }

        public string Id => $"Stone_{_stoneDefName}";
        public FilterHeaviness Heaviness => FilterHeaviness.Heavy;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var importance = GetImportance(context);

            // Only filter in Apply phase if Critical
            if (importance != FilterImportance.Critical)
                return inputTiles;

            var cache = context.TileCache;
            Log.Message($"[LandingZone] {_displayName}Filter: Filtering for {_displayName} (Critical mode)");

            var result = inputTiles.Where(id =>
            {
                var extended = cache.GetOrCompute(id);
                if (extended.StoneDefNames == null || extended.StoneDefNames.Length == 0)
                    return false;

                return extended.StoneDefNames.Contains(_stoneDefName);
            }).ToList();

            Log.Message($"[LandingZone] {_displayName}Filter: Filtered {inputTiles.Count()} -> {result.Count} tiles");
            return result;
        }

        public string Describe(FilterContext context)
        {
            var importance = GetImportance(context);

            if (importance == FilterImportance.Ignored)
                return $"{_displayName} not required";

            string importanceLabel = importance == FilterImportance.Critical ? " (required)" : " (preferred)";
            return $"{_displayName}{importanceLabel}";
        }

        private FilterImportance GetImportance(FilterContext context)
        {
            var filters = context.State.Preferences.Filters;
            return _stoneDefName switch
            {
                "Granite" => filters.GraniteImportance,
                "Marble" => filters.MarbleImportance,
                "Limestone" => filters.LimestoneImportance,
                "Slate" => filters.SlateImportance,
                "Sandstone" => filters.SandstoneImportance,
                _ => FilterImportance.Ignored
            };
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by growing period length (number of days suitable for growing crops).
    /// Uses TileDataCache for lazy computation since calculating growing period is expensive (2-3ms per tile).
    /// </summary>
    public sealed class GrowingDaysFilter : ISiteFilter
    {
        public string Id => "growing_days";
        public FilterHeaviness Heaviness => FilterHeaviness.Heavy;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            var importance = filters.GrowingDaysImportance;

            // Only hard gates (MustHave/MustNotHave) filter in Apply phase
            // Priority/Preferred are handled in scoring phase
            if (!importance.IsHardGate())
                return inputTiles;

            var range = filters.GrowingDaysRange;

            return inputTiles.Where(id =>
            {
                var extended = context.TileCache.GetOrCompute(id);
                bool inRange = extended.GrowingDays >= range.min && extended.GrowingDays <= range.max;

                // MustHave: tile must be in range
                // MustNotHave: tile must NOT be in range (exclude tiles that match)
                return importance == FilterImportance.MustNotHave ? !inRange : inRange;
            });
        }

        public string Describe(FilterContext context)
        {
            var filters = context.Filters;
            if (filters.GrowingDaysImportance == FilterImportance.Ignored)
                return "Any growing season";

            var range = filters.GrowingDaysRange;
            string importanceLabel = filters.GrowingDaysImportance == FilterImportance.Critical ? " (required)" : " (preferred)";
            return $"Growing days {range.min:F0} - {range.max:F0}{importanceLabel}";
        }

        public float Membership(int tileId, FilterContext context)
        {
            var range = context.Filters.GrowingDaysRange;
            var extended = context.TileCache.GetOrCompute(tileId);
            return MembershipFunctions.Trapezoid(extended.GrowingDays, range.min, range.max);
        }
    }
}

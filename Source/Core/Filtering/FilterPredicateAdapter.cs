using System.Collections;
using System.Linq;
using LandingZone.Data;

namespace LandingZone.Core.Filtering
{
    /// <summary>
    /// Adapts existing ISiteFilter implementations to IFilterPredicate interface.
    /// Provides backward compatibility - all existing filters work without modification.
    /// </summary>
    public sealed class FilterPredicateAdapter : IFilterPredicate
    {
        private readonly ISiteFilter _filter;
        private readonly FilterImportance _importance;

        public FilterPredicateAdapter(ISiteFilter filter, FilterImportance importance)
        {
            _filter = filter;
            _importance = importance;
        }

        /// <summary>
        /// Gets the underlying ISiteFilter for direct Apply() calls on survivor sets.
        /// Used for efficient heavy gate evaluation on cheap survivors only.
        /// </summary>
        public ISiteFilter UnderlyingFilter => _filter;

        public string Id => _filter.Id;

        public FilterImportance Importance => _importance;

        public bool IsHeavy => _filter.Heaviness == FilterHeaviness.Heavy;

        public BitArray Evaluate(FilterContext context, int tileCount)
        {
            // If filter is Ignored, all tiles match
            if (_importance == FilterImportance.Ignored)
            {
                var allMatch = new BitArray(tileCount, true);
                return allMatch;
            }

            // Generate all tile IDs (0 to tileCount-1)
            var allTiles = Enumerable.Range(0, tileCount);

            // Call existing filter's Apply method
            var matchingTiles = _filter.Apply(context, allTiles);

            // Convert to BitArray
            var bitset = new BitArray(tileCount, false);
            foreach (var tileId in matchingTiles)
            {
                if (tileId >= 0 && tileId < tileCount)
                    bitset[tileId] = true;
            }

            return bitset;
        }

        public string Describe(FilterContext context)
        {
            return _filter.Describe(context);
        }
    }
}

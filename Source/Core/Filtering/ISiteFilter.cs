using System.Collections.Generic;

namespace LandingZone.Core.Filtering
{
    public interface ISiteFilter
    {
        string Id { get; }
        FilterHeaviness Heaviness { get; }
        IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles);
        string Describe(FilterContext context);

        /// <summary>
        /// Compute continuous membership score [0,1] for a tile.
        /// - 1.0 = perfect match
        /// - 0.0 = complete mismatch
        /// - Values in between indicate partial match (e.g., close to desired range)
        ///
        /// Used by new membership-based scoring system for fuzzy preference matching.
        /// </summary>
        float Membership(int tileId, FilterContext context);
    }
}

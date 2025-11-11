using System.Collections.Generic;

namespace LandingZone.Core.Filtering
{
    public interface ISiteFilter
    {
        string Id { get; }
        FilterHeaviness Heaviness { get; }
        IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles);
        string Describe(FilterContext context);
    }
}

using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;

namespace LandingZone.Core.Filtering
{
    public sealed class SiteFilterRegistry
    {
        private readonly List<ISiteFilter> _filters = new List<ISiteFilter>();

        public void Register(ISiteFilter filter)
        {
            _filters.Add(filter);
            _filters.Sort((a, b) => a.Heaviness.CompareTo(b.Heaviness));
        }

        public IEnumerable<int> ApplyAll(GameState state, int totalTiles)
        {
            var context = new FilterContext(state);
            IEnumerable<int> current = state.WorldSnapshot?.SettleableTiles?.Count > 0
                ? state.WorldSnapshot.SettleableTiles
                : Enumerable.Range(0, totalTiles);

            foreach (var filter in _filters)
            {
                current = filter.Apply(context, current).ToList();
                if (!current.Any())
                    break;
            }

            return current;
        }

        public IReadOnlyList<ISiteFilter> Filters => _filters;
    }
}

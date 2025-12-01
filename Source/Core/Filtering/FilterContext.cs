using LandingZone.Data;

namespace LandingZone.Core.Filtering
{
    public readonly struct FilterContext
    {
        private readonly FilterSettings? _overrideFilters;

        public FilterContext(GameState state, TileDataCache tileCache, FilterSettings? overrideFilters = null)
        {
            State = state;
            TileCache = tileCache;
            _overrideFilters = overrideFilters;
        }

        public GameState State { get; }
        public TileDataCache TileCache { get; }

        /// <summary>
        /// Convenience accessor for the active FilterSettings.
        /// Returns override filters if provided, otherwise delegates to State.Preferences.GetActiveFilters().
        /// This allows relaxed search to use temporary filters without mutating user preferences.
        /// </summary>
        public FilterSettings Filters => _overrideFilters ?? State.Preferences.GetActiveFilters();
    }
}

using LandingZone.Data;

namespace LandingZone.Core.Filtering
{
    public readonly struct FilterContext
    {
        public FilterContext(GameState state, TileDataCache tileCache)
        {
            State = state;
            TileCache = tileCache;
        }

        public GameState State { get; }
        public TileDataCache TileCache { get; }
    }
}

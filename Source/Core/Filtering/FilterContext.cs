using LandingZone.Data;

namespace LandingZone.Core.Filtering
{
    public readonly struct FilterContext
    {
        public FilterContext(GameState state)
        {
            State = state;
        }

        public GameState State { get; }
    }
}

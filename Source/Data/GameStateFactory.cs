using Verse;

namespace LandingZone.Data
{
    public static class GameStateFactory
    {
        public static GameState CreateDefault()
        {
            var defCache = new DefCache();
            var snapshot = new WorldSnapshot();
            var preferences = new UserPreferences();
            var profile = BestSiteProfile.CreateDefault();

            // Load definitions immediately; world snapshot will be refreshed once the world exists.
            defCache.Refresh();

            return new GameState(defCache, snapshot, preferences, profile);
        }
    }
}

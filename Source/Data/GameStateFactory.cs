using Verse;

namespace LandingZone.Data
{
    public static class GameStateFactory
    {
        public static GameState CreateDefault()
        {
            var defCache = new DefCache();
            var preferences = new UserPreferences();
            var profile = BestSiteProfile.CreateDefault();

            // Load definitions immediately
            defCache.Refresh();

            return new GameState(defCache, preferences, profile);
        }
    }
}

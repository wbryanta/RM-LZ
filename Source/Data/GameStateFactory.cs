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

            // Migrate legacy stone settings to new individual stone importance properties
            preferences.Filters.MigrateLegacyStoneSettings();

            return new GameState(defCache, preferences, profile);
        }
    }
}

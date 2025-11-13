namespace LandingZone.Data
{
    /// <summary>
    /// Aggregates runtime state used across the mod.
    /// </summary>
    public sealed class GameState
    {
        public GameState(DefCache defCache, UserPreferences preferences, BestSiteProfile profile)
        {
            DefCache = defCache;
            Preferences = preferences;
            BestSiteProfile = profile;
        }

        public DefCache DefCache { get; }
        public UserPreferences Preferences { get; }
        public BestSiteProfile BestSiteProfile { get; }

        public void RefreshAll()
        {
            DefCache.Refresh();
        }
    }
}

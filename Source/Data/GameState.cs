namespace LandingZone.Data
{
    /// <summary>
    /// Aggregates runtime state used across the mod.
    /// </summary>
    public sealed class GameState
    {
        public GameState(DefCache defCache, WorldSnapshot worldSnapshot, UserPreferences preferences, BestSiteProfile profile)
        {
            DefCache = defCache;
            WorldSnapshot = worldSnapshot;
            Preferences = preferences;
            BestSiteProfile = profile;
        }

        public DefCache DefCache { get; }
        public WorldSnapshot WorldSnapshot { get; }
        public UserPreferences Preferences { get; }
        public BestSiteProfile BestSiteProfile { get; }

        public void RefreshAll()
        {
            DefCache.Refresh();
            WorldSnapshot.RefreshFromCurrentWorld();
        }
    }
}

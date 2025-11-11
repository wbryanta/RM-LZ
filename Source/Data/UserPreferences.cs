namespace LandingZone.Data
{
    public sealed class UserPreferences
    {
        public FilterSettings Filters { get; } = new FilterSettings();
        public LandingZoneOptions Options { get; } = new LandingZoneOptions();

        public void ResetAll()
        {
            Filters.Reset();
            Options.Reset();
        }
    }
}

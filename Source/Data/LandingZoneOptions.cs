namespace LandingZone.Data
{
    /// <summary>
    /// Miscellaneous UI/behavior options that mirror the Options tab.
    /// </summary>
    public sealed class LandingZoneOptions
    {
        public bool AutoOpenWindow { get; set; } = true;
        public bool LiveFiltering { get; set; } = true;
        public bool HighlightMatches { get; set; } = true;
        public UIMode PreferencesUIMode { get; set; } = UIMode.Default;

        public void Reset()
        {
            AutoOpenWindow = true;
            LiveFiltering = true;
            HighlightMatches = true;
            PreferencesUIMode = UIMode.Default;
        }
    }

    /// <summary>
    /// UI presentation mode for Landing Zone preferences window.
    /// </summary>
    public enum UIMode : byte
    {
        /// <summary>
        /// Simplified UI with preset cards and 6-8 key filters for casual users.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Full-featured UI with all 40+ filters organized by groups for power users.
        /// </summary>
        Advanced = 1
    }
}

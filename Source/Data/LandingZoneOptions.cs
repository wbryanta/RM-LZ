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

        /// <summary>
        /// Enable new membership-based scoring system (fuzzy preferences).
        /// Default: true (enabled for testing/validation).
        /// Set to false to revert to legacy k-of-n binary scoring.
        /// </summary>
        public bool UseNewScoring { get; set; } = true;

        public void Reset()
        {
            AutoOpenWindow = true;
            LiveFiltering = true;
            HighlightMatches = true;
            PreferencesUIMode = UIMode.Default;
            UseNewScoring = true;
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

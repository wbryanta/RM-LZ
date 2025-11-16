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
        public UIMode PreferencesUIMode { get; set; } = UIMode.Simple;

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
            PreferencesUIMode = UIMode.Simple;
            UseNewScoring = true;
        }
    }

    /// <summary>
    /// UI presentation mode for Landing Zone preferences window.
    /// Each mode maintains independent filter settings that persist across sessions.
    /// </summary>
    public enum UIMode : byte
    {
        /// <summary>
        /// Simplified UI with preset cards and essential filters for casual users.
        /// Maintains separate FilterSettings from Advanced mode.
        /// </summary>
        Simple = 0,

        /// <summary>
        /// Full-featured UI with all 40+ filters organized by categories for power users.
        /// Maintains separate FilterSettings from Simple mode.
        /// </summary>
        Advanced = 1
    }
}

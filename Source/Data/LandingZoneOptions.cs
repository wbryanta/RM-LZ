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

        public void Reset()
        {
            AutoOpenWindow = true;
            LiveFiltering = true;
            HighlightMatches = true;
            PreferencesUIMode = UIMode.Simple;
        }
    }

    /// <summary>
    /// UI presentation mode for Landing Zone preferences window.
    /// Three-tier progressive disclosure: Preset Hub → Guided Builder → Advanced Studio.
    /// </summary>
    public enum UIMode : byte
    {
        /// <summary>
        /// Tier 1: Preset Hub - Quick-start presets with essential tweaks for casual users.
        /// </summary>
        Simple = 0,

        /// <summary>
        /// Tier 2: Guided Builder - Goal-based wizard with priority ranking for intermediate users.
        /// </summary>
        GuidedBuilder = 1,

        /// <summary>
        /// Tier 3: Advanced Studio - Full-featured UI with all 40+ filters for power users.
        /// </summary>
        Advanced = 2
    }
}

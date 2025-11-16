namespace LandingZone.Data
{
    /// <summary>
    /// User preferences containing independent filter settings for Simple and Advanced modes.
    /// Each mode maintains its own FilterSettings that persist across sessions.
    /// </summary>
    public sealed class UserPreferences
    {
        /// <summary>
        /// Filter settings for Simple mode (preset-driven, simplified UI)
        /// </summary>
        public FilterSettings SimpleFilters { get; } = new FilterSettings();

        /// <summary>
        /// Filter settings for Advanced mode (full control, all 40+ filters)
        /// </summary>
        public FilterSettings AdvancedFilters { get; } = new FilterSettings();

        public LandingZoneOptions Options { get; } = new LandingZoneOptions();

        /// <summary>
        /// Gets the active filter settings based on current UI mode
        /// </summary>
        public FilterSettings GetActiveFilters()
        {
            return Options.PreferencesUIMode == UIMode.Simple
                ? SimpleFilters
                : AdvancedFilters;
        }

        public void ResetAll()
        {
            SimpleFilters.Reset();
            AdvancedFilters.Reset();
            Options.Reset();
        }

        /// <summary>
        /// Resets only the active mode's filters
        /// </summary>
        public void ResetActiveFilters()
        {
            GetActiveFilters().Reset();
        }

        /// <summary>
        /// Copies filter settings from Simple mode to Advanced mode
        /// </summary>
        public void CopySimpleToAdvanced()
        {
            AdvancedFilters.CopyFrom(SimpleFilters);
        }

        /// <summary>
        /// Copies filter settings from Advanced mode to Simple mode
        /// </summary>
        public void CopyAdvancedToSimple()
        {
            SimpleFilters.CopyFrom(AdvancedFilters);
        }
    }
}

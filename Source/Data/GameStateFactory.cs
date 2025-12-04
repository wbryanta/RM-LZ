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

            // Apply default preset to SimpleFilters
            ApplyDefaultPreset(preferences);

            return new GameState(defCache, preferences, profile);
        }

        /// <summary>
        /// Applies the user's default preset (or "balanced" fallback) to SimpleFilters.
        /// Called when creating a new game state on world load.
        /// </summary>
        private static void ApplyDefaultPreset(UserPreferences preferences)
        {
            // Get the user's selected default preset ID (persisted in ModSettings)
            var presetId = LandingZoneSettings.DefaultPresetId ?? "balanced";

            // Try to load the preset
            var preset = PresetLibrary.GetById(presetId);

            // Fallback to "balanced" if the selected default is missing (e.g., user deleted it)
            if (preset == null && presetId != "balanced")
            {
                Log.Warning($"[LandingZone] Default preset '{presetId}' not found, falling back to 'balanced'");
                preset = PresetLibrary.GetById("balanced");

                // Reset the persisted setting to fallback
                LandingZoneSettings.DefaultPresetId = "balanced";
                LandingZoneMod.Instance?.WriteSettings();
            }

            // Apply the preset if found
            if (preset != null)
            {
                preset.ApplyTo(preferences.SimpleFilters);
                preferences.ActivePreset = preset;
                Log.Message($"[LandingZone] Applied default preset: {preset.Name}");
            }
            else
            {
                Log.Warning("[LandingZone] No default preset found - SimpleFilters will be empty");
            }
        }
    }
}

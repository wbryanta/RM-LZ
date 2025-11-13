using LandingZone.Data;
using UnityEngine;
using Verse;

namespace LandingZone.Core.UI
{
    /// <summary>
    /// Simplified UI renderer for casual users.
    /// Shows preset cards + 6-8 key filters for quick site selection.
    /// </summary>
    public static class DefaultModeUI
    {
        private const float PresetCardHeight = 60f;
        private const float KeyFiltersHeight = 400f;

        /// <summary>
        /// Renders the Default mode UI (preset cards + key filters).
        /// </summary>
        /// <param name="inRect">Available drawing area</param>
        /// <param name="preferences">User preferences containing filter settings</param>
        /// <returns>Total height consumed by rendering</returns>
        public static float DrawContent(Rect inRect, UserPreferences preferences)
        {
            float curY = inRect.y;

            // Preset cards section
            curY += DrawPresetCards(new Rect(inRect.x, curY, inRect.width, PresetCardHeight), preferences);
            curY += 10f; // Spacing

            // Key filters section (6-8 essential filters)
            curY += DrawKeyFilters(new Rect(inRect.x, curY, inRect.width, KeyFiltersHeight), preferences);

            return curY - inRect.y;
        }

        private static float DrawPresetCards(Rect rect, UserPreferences preferences)
        {
            // TODO: Implement preset card system
            // - 3-5 preset buttons (Temperate Paradise, Arctic Survival, Desert Challenge, etc.)
            // - Visual cards with icons/descriptions
            // - Apply preset FilterSettings templates on click

            Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.2f, 0.2f, 0.5f));
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, "PRESET CARDS - Coming in Phase 2A");
            Text.Anchor = TextAnchor.UpperLeft;

            return rect.height;
        }

        private static float DrawKeyFilters(Rect rect, UserPreferences preferences)
        {
            // TODO: Implement 6-8 key filter controls
            // Essential filters for casual users:
            // 1. Biome (multi-select dropdown)
            // 2. Temperature range (simple slider)
            // 3. Growing season (simple slider)
            // 4. Rainfall (simple slider)
            // 5. Coastal (Yes/No/Either toggle)
            // 6. Hilliness (Flat/Small/Large buttons)
            // 7. Stone types (simplified selector)
            // 8. Has caves (Yes/No/Either toggle)

            Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.2f, 0.2f, 0.5f));
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, "KEY FILTERS (6-8) - Coming in Phase 2A");
            Text.Anchor = TextAnchor.UpperLeft;

            return rect.height;
        }
    }
}

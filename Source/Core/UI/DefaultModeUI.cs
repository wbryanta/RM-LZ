using System;
using System.Linq;
using LandingZone.Core.Filtering.Filters;
using LandingZone.Data;
using RimWorld;
using RimWorld.Planet;
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
        private const float PresetCardWidth = 120f;
        private const float PresetCardHeight = 70f;
        private const float PresetCardSpacing = 8f;

        /// <summary>
        /// Renders the Default mode UI (preset cards + key filters).
        /// </summary>
        /// <param name="inRect">Available drawing area</param>
        /// <param name="preferences">User preferences containing filter settings</param>
        /// <returns>Total height consumed by rendering</returns>
        public static float DrawContent(Rect inRect, UserPreferences preferences)
        {
            var listing = new Listing_Standard { ColumnWidth = inRect.width };
            listing.Begin(inRect);

            // Header
            Text.Font = GameFont.Medium;
            listing.Label("Quick Setup");
            Text.Font = GameFont.Small;
            listing.GapLine();

            // Preset cards section
            DrawPresetCards(listing, preferences);
            listing.Gap(20f);

            // Key filters section
            listing.Label("Fine-Tune Your Preferences:");
            listing.Gap(10f);
            DrawKeyFilters(listing, preferences);

            listing.Gap(20f);

            // Reset button only - Search button is in main toolbar
            if (listing.ButtonText("Reset to Defaults"))
            {
                preferences.ResetActiveFilters();
                Messages.Message("Filter settings reset to defaults", MessageTypeDefOf.NeutralEvent, false);
            }

            listing.Gap(8f);

            // Import from Advanced mode
            if (listing.ButtonText("Copy from Advanced mode"))
            {
                preferences.CopyAdvancedToSimple();
                Messages.Message("Advanced mode settings copied to Simple mode", MessageTypeDefOf.NeutralEvent, false);
            }

            listing.End();
            return listing.CurHeight;
        }

        private static void DrawPresetCards(Listing_Standard listing, UserPreferences preferences)
        {
            var filters = preferences.GetActiveFilters();
            var curatedPresets = PresetLibrary.GetCurated();
            var userPresets = PresetLibrary.GetUserPresets();

            const int columns = 4;
            const float totalWidth = (PresetCardWidth * columns) + (PresetCardSpacing * (columns - 1));

            // Draw curated presets in 4-column grid
            int curatedRows = (curatedPresets.Count + columns - 1) / columns; // Ceiling division
            for (int row = 0; row < curatedRows; row++)
            {
                Rect rowRect = listing.GetRect(PresetCardHeight + PresetCardSpacing);

                for (int col = 0; col < columns; col++)
                {
                    int index = row * columns + col;
                    if (index >= curatedPresets.Count) break;

                    float cardX = rowRect.x + col * (PresetCardWidth + PresetCardSpacing);
                    Rect cardRect = new Rect(cardX, rowRect.y, PresetCardWidth, PresetCardHeight);
                    DrawPresetCard(cardRect, curatedPresets[index], filters);
                }
            }

            // Draw user presets section (up to 4 slots)
            listing.Gap(10f);
            Text.Font = GameFont.Tiny;
            listing.Label("My Presets:");
            Text.Font = GameFont.Small;

            Rect userRowRect = listing.GetRect(PresetCardHeight + PresetCardSpacing);
            int userCount = Math.Min(userPresets.Count, 4); // Cap at 4 user presets

            for (int i = 0; i < userCount; i++)
            {
                float cardX = userRowRect.x + i * (PresetCardWidth + PresetCardSpacing);
                Rect cardRect = new Rect(cardX, userRowRect.y, PresetCardWidth, PresetCardHeight);
                DrawPresetCard(cardRect, userPresets[i], filters);
            }

            // Add "Save as Preset" button
            listing.Gap(10f);
            if (listing.ButtonText("Save Current Filters as Preset"))
            {
                Find.WindowStack.Add(new Dialog_SavePreset(filters));
            }
        }

        private static void DrawPresetCard(Rect rect, Preset preset, FilterSettings filters)
        {
            // Draw card background
            Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.15f));
            Widgets.DrawBox(rect);

            // Card content
            Rect contentRect = rect.ContractedBy(6f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // Rarity badge (top-right corner)
            if (preset.TargetRarity.HasValue)
            {
                var rarity = preset.TargetRarity.Value;
                var badgeColor = rarity.ToColor();
                var badgeLabel = rarity.ToBadgeLabel();  // Use compact label to prevent wrapping

                Rect badgeRect = new Rect(rect.xMax - 42f, rect.y + 4f, 38f, 16f);
                Widgets.DrawBoxSolid(badgeRect, badgeColor * 0.7f);
                Widgets.DrawBox(badgeRect);

                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.white;
                Widgets.Label(badgeRect, badgeLabel);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            // Title
            Rect titleRect = new Rect(contentRect.x, contentRect.y, contentRect.width - 40f, 20f);
            Widgets.Label(titleRect, preset.Name);

            // Description (truncated to fit)
            Text.Font = GameFont.Tiny;
            Rect descRect = new Rect(contentRect.x, contentRect.y + 22f, contentRect.width, 30f);
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.Label(descRect, preset.Description);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            Text.Anchor = TextAnchor.UpperLeft;

            // Click to apply preset
            if (Widgets.ButtonInvisible(rect))
            {
                preset.ApplyTo(filters);
                Messages.Message($"Applied preset: {preset.Name}", MessageTypeDefOf.NeutralEvent, false);
            }

            // Tooltip with filter summary
            string tooltip = $"{preset.Name}\n\n{preset.Description}\n\n";
            if (!string.IsNullOrEmpty(preset.FilterSummary))
                tooltip += $"Filters: {preset.FilterSummary}\n\n";
            if (preset.TargetRarity.HasValue)
                tooltip += $"Target Rarity: {preset.TargetRarity.Value.ToLabel()}\n\n";
            tooltip += "Click to apply this preset.";

            TooltipHandler.TipRegion(rect, tooltip);
        }

        private static void DrawKeyFilters(Listing_Standard listing, UserPreferences preferences)
        {
            var filters = preferences.GetActiveFilters();

            // 1. Temperature (honor user's C/F/K preference)
            var tempMode = Prefs.TemperatureMode;
            string tempUnit = tempMode == TemperatureDisplayMode.Fahrenheit ? "°F"
                            : tempMode == TemperatureDisplayMode.Kelvin ? "K"
                            : "°C";

            // Convert stored Celsius values to display unit
            float displayMin = GenTemperature.CelsiusTo(filters.AverageTemperatureRange.min, tempMode);
            float displayMax = GenTemperature.CelsiusTo(filters.AverageTemperatureRange.max, tempMode);

            // Define slider range in display unit
            float sliderMin = GenTemperature.CelsiusTo(-60f, tempMode);  // -60°C = -76°F = 213K
            float sliderMax = GenTemperature.CelsiusTo(60f, tempMode);   // 60°C = 140°F = 333K

            listing.Label($"Temperature: {displayMin:F0}{tempUnit} to {displayMax:F0}{tempUnit}");
            var tempRect = listing.GetRect(30f);

            // Slider works in display units
            var displayRange = new FloatRange(displayMin, displayMax);
            DrawRangeSlider(tempRect, ref displayRange, sliderMin, sliderMax);

            // Convert back to Celsius for storage
            filters.AverageTemperatureRange = new FloatRange(
                ConvertToCelsius(displayRange.min, tempMode),
                ConvertToCelsius(displayRange.max, tempMode)
            );

            var tempImportance = filters.AverageTemperatureImportance;
            UIHelpers.DrawImportanceSelector(listing.GetRect(30f), "Temperature Importance", ref tempImportance);
            filters.AverageTemperatureImportance = tempImportance;
            listing.Gap(10f);

            // 2. Growing Season
            listing.Label($"Growing Season: {filters.GrowingDaysRange.min:F0} to {filters.GrowingDaysRange.max:F0} days/year");
            var growingRect = listing.GetRect(30f);
            var growingRange = filters.GrowingDaysRange;
            DrawRangeSlider(growingRect, ref growingRange, 0f, 60f);
            filters.GrowingDaysRange = growingRange;

            var growingImportance = filters.GrowingDaysImportance;
            UIHelpers.DrawImportanceSelector(listing.GetRect(30f), "Growing Season Importance", ref growingImportance);
            filters.GrowingDaysImportance = growingImportance;
            listing.Gap(10f);

            // 3. Rainfall
            listing.Label($"Rainfall: {filters.RainfallRange.min:F0} to {filters.RainfallRange.max:F0} mm/year");
            var rainfallRect = listing.GetRect(30f);
            var rainfallRange = filters.RainfallRange;
            DrawRangeSlider(rainfallRect, ref rainfallRange, 0f, 5000f);
            filters.RainfallRange = rainfallRange;

            var rainfallImportance = filters.RainfallImportance;
            UIHelpers.DrawImportanceSelector(listing.GetRect(30f), "Rainfall Importance", ref rainfallImportance);
            filters.RainfallImportance = rainfallImportance;
            listing.Gap(10f);

            // 4. Coastal (Any - ocean or lake)
            DrawCoastalControl(listing.GetRect(30f), filters);
            listing.Gap(10f);

            // 5. Rivers (Any)
            DrawRiversControl(listing.GetRect(30f), filters);
            listing.Gap(10f);

            // 6. Roads (Any)
            DrawRoadsControl(listing.GetRect(30f), filters);
            listing.Gap(10f);

            // 7. Caves (via MapFeatures)
            var caveImportance = filters.MapFeatures.GetImportance("Caves");
            UIHelpers.DrawImportanceSelector(listing.GetRect(30f), "Caves", ref caveImportance);
            filters.MapFeatures.SetImportance("Caves", caveImportance);
            listing.Gap(10f);

            // 8. Hilliness
            listing.Label("Terrain Hilliness:");
            DrawHillinessToggles(listing.GetRect(30f), filters);
            listing.Gap(10f);

            // 9. Stones (Granite and Marble)
            listing.Label("Preferred Stones:");
            DrawStonesControls(listing, filters);
        }

        private static void DrawRangeSlider(Rect rect, ref FloatRange range, float min, float max)
        {
            // Simple two-handle range slider
            float newMin = range.min;
            float newMax = range.max;

            Widgets.FloatRange(rect, rect.GetHashCode(), ref range, min, max);
        }

        private static void DrawHillinessToggles(Rect rect, FilterSettings filters)
        {
            var hillinessValues = new[] { Hilliness.Flat, Hilliness.SmallHills, Hilliness.LargeHills, Hilliness.Mountainous };
            var labels = new[] { "Flat", "Small", "Large", "Mountain" };

            float buttonWidth = (rect.width - 10f) / 4f;
            float x = rect.x;

            for (int i = 0; i < hillinessValues.Length; i++)
            {
                var hilliness = hillinessValues[i];
                Rect buttonRect = new Rect(x, rect.y, buttonWidth, rect.height);

                bool isSelected = filters.AllowedHilliness.Contains(hilliness);
                bool clicked = Widgets.ButtonText(buttonRect, labels[i], active: isSelected);

                if (clicked)
                {
                    if (isSelected)
                        filters.AllowedHilliness.Remove(hilliness);
                    else
                        filters.AllowedHilliness.Add(hilliness);
                }

                x += buttonWidth + 3f;
            }
        }

        private static void DrawCoastalControl(Rect rect, FilterSettings filters)
        {
            // Simple importance selector for "any coastal" (ocean or lake)
            // When set, applies importance to both CoastalImportance and CoastalLakeImportance
            var currentImportance = GetAnyCoastalImportance(filters);
            var newImportance = currentImportance;

            UIHelpers.DrawImportanceSelector(rect, "Coastal (Any)", ref newImportance);

            if (newImportance != currentImportance)
            {
                // Set both ocean and lake coastal to the same importance
                filters.CoastalImportance = newImportance;
                filters.CoastalLakeImportance = newImportance;
            }
        }

        private static void DrawRiversControl(Rect rect, FilterSettings filters)
        {
            // Simple importance selector for "any river"
            // When set, applies importance to all river types
            var currentImportance = GetAnyRiverImportance(filters.Rivers);
            var newImportance = currentImportance;

            UIHelpers.DrawImportanceSelector(rect, "Rivers (Any)", ref newImportance);

            if (newImportance != currentImportance)
            {
                // Get all river types and set them all to the new importance
                var allRivers = RiverFilter.GetAllRiverTypes().Select(r => r.defName);
                filters.Rivers.SetAllTo(allRivers, newImportance);

                // Set OR operator for "Any" semantics (tile must have ANY river, not ALL)
                if (newImportance == FilterImportance.Critical || newImportance == FilterImportance.Preferred)
                    filters.Rivers.Operator = ImportanceOperator.OR;
                else
                    filters.Rivers.Operator = ImportanceOperator.AND;  // Reset when ignored
            }
        }

        private static void DrawRoadsControl(Rect rect, FilterSettings filters)
        {
            // Simple importance selector for "any road"
            // When set, applies importance to all road types
            var currentImportance = GetAnyRoadImportance(filters.Roads);
            var newImportance = currentImportance;

            UIHelpers.DrawImportanceSelector(rect, "Roads (Any)", ref newImportance);

            if (newImportance != currentImportance)
            {
                // Get all road types and set them all to the new importance
                var allRoads = RoadFilter.GetAllRoadTypes().Select(r => r.defName);
                filters.Roads.SetAllTo(allRoads, newImportance);

                // Set OR operator for "Any" semantics (tile must have ANY road, not ALL)
                if (newImportance == FilterImportance.Critical || newImportance == FilterImportance.Preferred)
                    filters.Roads.Operator = ImportanceOperator.OR;
                else
                    filters.Roads.Operator = ImportanceOperator.AND;  // Reset when ignored
            }
        }

        private static void DrawStonesControls(Listing_Standard listing, FilterSettings filters)
        {
            // Show Granite and Marble with individual selectors (default to Preferred)
            var graniteImportance = filters.Stones.GetImportance("Granite");
            UIHelpers.DrawImportanceSelector(listing.GetRect(30f), "Granite", ref graniteImportance);
            filters.Stones.SetImportance("Granite", graniteImportance);
            listing.Gap(2f);

            var marbleImportance = filters.Stones.GetImportance("Marble");
            UIHelpers.DrawImportanceSelector(listing.GetRect(30f), "Marble", ref marbleImportance);
            filters.Stones.SetImportance("Marble", marbleImportance);
        }

        /// <summary>
        /// Gets the "overall" importance for coastal - returns highest of ocean or lake importance.
        /// </summary>
        private static FilterImportance GetAnyCoastalImportance(FilterSettings filters)
        {
            // Return the highest importance between ocean and lake
            if (filters.CoastalImportance == FilterImportance.Critical || filters.CoastalLakeImportance == FilterImportance.Critical)
                return FilterImportance.Critical;

            if (filters.CoastalImportance == FilterImportance.Preferred || filters.CoastalLakeImportance == FilterImportance.Preferred)
                return FilterImportance.Preferred;

            return FilterImportance.Ignored;
        }

        /// <summary>
        /// Gets the "overall" importance for rivers - returns highest importance set, or Ignored if none.
        /// </summary>
        private static FilterImportance GetAnyRiverImportance(IndividualImportanceContainer<string> rivers)
        {
            if (!rivers.HasAnyImportance)
                return FilterImportance.Ignored;

            if (rivers.HasCritical)
                return FilterImportance.Critical;

            // Check if any river is Preferred
            var allRivers = RiverFilter.GetAllRiverTypes().Select(r => r.defName);
            foreach (var river in allRivers)
            {
                if (rivers.GetImportance(river) == FilterImportance.Preferred)
                    return FilterImportance.Preferred;
            }

            return FilterImportance.Ignored;
        }

        /// <summary>
        /// Gets the "overall" importance for roads - returns highest importance set, or Ignored if none.
        /// </summary>
        private static FilterImportance GetAnyRoadImportance(IndividualImportanceContainer<string> roads)
        {
            if (!roads.HasAnyImportance)
                return FilterImportance.Ignored;

            if (roads.HasCritical)
                return FilterImportance.Critical;

            // Check if any road is Preferred
            var allRoads = RoadFilter.GetAllRoadTypes().Select(r => r.defName);
            foreach (var road in allRoads)
            {
                if (roads.GetImportance(road) == FilterImportance.Preferred)
                    return FilterImportance.Preferred;
            }

            return FilterImportance.Ignored;
        }

        /// <summary>
        /// Converts temperature from display mode back to Celsius for storage.
        /// </summary>
        private static float ConvertToCelsius(float displayValue, TemperatureDisplayMode mode)
        {
            if (mode == TemperatureDisplayMode.Fahrenheit)
            {
                // F to C: (F - 32) / 1.8
                return (displayValue - 32f) / 1.8f;
            }
            else if (mode == TemperatureDisplayMode.Kelvin)
            {
                // K to C: K - 273.15
                return displayValue - 273.15f;
            }
            else
            {
                // Already Celsius
                return displayValue;
            }
        }
    }
}

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

            // Action buttons
            if (listing.ButtonText("Search for Landing Zones"))
            {
                LandingZoneContext.RequestEvaluation(EvaluationRequestSource.Manual, focusOnComplete: true);
            }

            if (listing.ButtonText("Reset to Defaults"))
            {
                preferences.Filters.Reset();
            }

            listing.End();
            return listing.CurHeight;
        }

        private static void DrawPresetCards(Listing_Standard listing, UserPreferences preferences)
        {
            var filters = preferences.Filters;
            var presets = FilterPresets.AllPresets;

            // Draw preset cards in a horizontal row
            Rect presetRowRect = listing.GetRect(PresetCardHeight + 10f);
            float cardX = presetRowRect.x;

            foreach (var preset in presets)
            {
                if (cardX + PresetCardWidth > presetRowRect.xMax)
                    break; // Don't overflow, wrap in future if needed

                Rect cardRect = new Rect(cardX, presetRowRect.y, PresetCardWidth, PresetCardHeight);
                DrawPresetCard(cardRect, preset, filters);

                cardX += PresetCardWidth + PresetCardSpacing;
            }
        }

        private static void DrawPresetCard(Rect rect, PresetDefinition preset, FilterSettings filters)
        {
            // Draw card background
            Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.15f));
            Widgets.DrawBox(rect);

            // Card content
            Rect contentRect = rect.ContractedBy(6f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // Title
            Rect titleRect = new Rect(contentRect.x, contentRect.y, contentRect.width, 20f);
            Widgets.Label(titleRect, preset.Name);

            // Description
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
                preset.Apply(filters);
                Messages.Message($"Applied preset: {preset.Name}", MessageTypeDefOf.NeutralEvent, false);
            }

            // Tooltip
            TooltipHandler.TipRegion(rect, $"{preset.Name}\n\n{preset.Description}\n\nClick to apply this preset.");
        }

        private static void DrawKeyFilters(Listing_Standard listing, UserPreferences preferences)
        {
            var filters = preferences.Filters;

            // 1. Temperature (honor user's C/F preference)
            bool useFahrenheit = LandingZoneMod.UseFahrenheit;
            string tempUnit = useFahrenheit ? "°F" : "°C";
            float displayMin = useFahrenheit
                ? GenTemperature.CelsiusTo(filters.AverageTemperatureRange.min, TemperatureDisplayMode.Fahrenheit)
                : filters.AverageTemperatureRange.min;
            float displayMax = useFahrenheit
                ? GenTemperature.CelsiusTo(filters.AverageTemperatureRange.max, TemperatureDisplayMode.Fahrenheit)
                : filters.AverageTemperatureRange.max;

            listing.Label($"Temperature: {displayMin:F0}{tempUnit} to {displayMax:F0}{tempUnit}");
            var tempRect = listing.GetRect(30f);
            var tempRange = filters.AverageTemperatureRange;
            DrawRangeSlider(tempRect, ref tempRange, -60f, 60f);
            filters.AverageTemperatureRange = tempRange;

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

            // 4. Coastal
            var coastalImportance = filters.CoastalImportance;
            UIHelpers.DrawImportanceSelector(listing.GetRect(30f), "Coastal (Ocean)", ref coastalImportance);
            filters.CoastalImportance = coastalImportance;
            listing.Gap(10f);

            // 5. Rivers (Any)
            var riversImportance = GetAnyRiverImportance(filters.Rivers);
            UIHelpers.DrawImportanceSelector(listing.GetRect(30f), "Rivers (Any)", ref riversImportance);
            SetAllRiversImportance(filters.Rivers, riversImportance);
            listing.Gap(10f);

            // 6. Roads (Any)
            var roadsImportance = GetAnyRoadImportance(filters.Roads);
            UIHelpers.DrawImportanceSelector(listing.GetRect(30f), "Roads (Any)", ref roadsImportance);
            SetAllRoadsImportance(filters.Roads, roadsImportance);
            listing.Gap(10f);

            // 7. Hilliness
            listing.Label("Terrain Hilliness:");
            DrawHillinessToggles(listing.GetRect(30f), filters);
            listing.Gap(10f);

            // 8. Caves (via MapFeatures)
            var caveImportance = filters.MapFeatures.GetImportance("Cave");
            UIHelpers.DrawImportanceSelector(listing.GetRect(30f), "Caves", ref caveImportance);
            filters.MapFeatures.SetImportance("Cave", caveImportance);
            listing.Gap(10f);

            // 7. Rivers (any type)
            DrawRiversControl(listing.GetRect(30f), filters);
            listing.Gap(10f);

            // 8. Roads (any type)
            DrawRoadsControl(listing.GetRect(30f), filters);
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
        /// Sets all river types to the specified importance level.
        /// Used by Basic mode "Rivers (Any)" toggle.
        /// </summary>
        private static void SetAllRiversImportance(IndividualImportanceContainer<string> rivers, FilterImportance importance)
        {
            var allRivers = RiverFilter.GetAllRiverTypes().Select(r => r.defName);
            foreach (var river in allRivers)
            {
                if (importance == FilterImportance.Ignored)
                    rivers.ItemImportance.Remove(river);
                else
                    rivers.SetImportance(river, importance);
            }
        }

        /// <summary>
        /// Sets all road types to the specified importance level.
        /// Used by Basic mode "Roads (Any)" toggle.
        /// </summary>
        private static void SetAllRoadsImportance(IndividualImportanceContainer<string> roads, FilterImportance importance)
        {
            var allRoads = RoadFilter.GetAllRoadTypes().Select(r => r.defName);
            foreach (var road in allRoads)
            {
                if (importance == FilterImportance.Ignored)
                    roads.ItemImportance.Remove(road);
                else
                    roads.SetImportance(road, importance);
            }
        }
    }
}

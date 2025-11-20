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

        // Quick Tweaks panel state
        private static bool _quickTweaksCollapsed = true;

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

            // Quick Tweaks panel (collapsible)
            DrawQuickTweaksPanel(listing, preferences);
            listing.Gap(20f);

            // Tier 1 is preset-focused: no granular filter controls
            // All detailed filtering happens in Advanced mode (Tier 3)

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
                    DrawPresetCard(cardRect, curatedPresets[index], filters, preferences);
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
                DrawPresetCard(cardRect, userPresets[i], filters, preferences);
            }

            // Add "Save as Preset" button
            listing.Gap(10f);
            if (listing.ButtonText("Save Current Filters as Preset"))
            {
                Find.WindowStack.Add(new Dialog_SavePreset(filters, preferences.ActivePreset));
            }
        }

        private static void DrawQuickTweaksPanel(Listing_Standard listing, UserPreferences preferences)
        {
            var filters = preferences.GetActiveFilters();

            // Collapsible header
            Rect headerRect = listing.GetRect(30f);
            Widgets.DrawBoxSolid(headerRect, new Color(0.2f, 0.2f, 0.2f));

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect labelRect = new Rect(headerRect.x + 30f, headerRect.y, headerRect.width - 30f, headerRect.height);
            Widgets.Label(labelRect, "Quick Tweaks");
            Text.Anchor = TextAnchor.UpperLeft;

            // Collapse/expand indicator
            Rect indicatorRect = new Rect(headerRect.x + 8f, headerRect.y + 10f, 16f, 16f);
            GUI.DrawTexture(indicatorRect, _quickTweaksCollapsed ? TexButton.Reveal : TexButton.Collapse);

            if (Widgets.ButtonInvisible(headerRect))
            {
                _quickTweaksCollapsed = !_quickTweaksCollapsed;
            }

            if (!_quickTweaksCollapsed)
            {
                listing.Gap(8f);

                // Result Count slider
                listing.Label($"Result Limit: {filters.MaxResults}");
                Rect resultSliderRect = listing.GetRect(30f);
                int resultCount = (int)Widgets.HorizontalSlider(
                    resultSliderRect,
                    filters.MaxResults,
                    FilterSettings.MinMaxResults,
                    FilterSettings.MaxResultsLimit,
                    true,
                    $"{filters.MaxResults} results",
                    $"{FilterSettings.MinMaxResults}",
                    $"{FilterSettings.MaxResultsLimit}"
                );
                filters.MaxResults = resultCount;
                listing.Gap(8f);

                // Temperature range slider (simplified - show center point with ±5°C implied width)
                var tempMode = Prefs.TemperatureMode;
                string tempUnit = tempMode == TemperatureDisplayMode.Fahrenheit ? "°F"
                                : tempMode == TemperatureDisplayMode.Kelvin ? "K"
                                : "°C";

                float tempCenter = (filters.AverageTemperatureRange.min + filters.AverageTemperatureRange.max) / 2f;
                float displayCenter = GenTemperature.CelsiusTo(tempCenter, tempMode);

                // Slider range in display unit
                float sliderMin = GenTemperature.CelsiusTo(-60f, tempMode);
                float sliderMax = GenTemperature.CelsiusTo(60f, tempMode);

                listing.Label($"Temperature Center: {displayCenter:F0}{tempUnit}");
                Rect tempSliderRect = listing.GetRect(30f);
                float newDisplayCenter = Widgets.HorizontalSlider(
                    tempSliderRect,
                    displayCenter,
                    sliderMin,
                    sliderMax,
                    true,
                    $"{displayCenter:F0}{tempUnit}",
                    $"{sliderMin:F0}{tempUnit}",
                    $"{sliderMax:F0}{tempUnit}"
                );

                // Convert back to Celsius and update range (maintain ~10°C width)
                float newTempCenter = ConvertToCelsius(newDisplayCenter, tempMode);
                float halfWidth = 5f; // ±5°C = 10°C total range
                filters.AverageTemperatureRange = new FloatRange(newTempCenter - halfWidth, newTempCenter + halfWidth);

                listing.Gap(8f);

                // Biome lock dropdown
                listing.Label($"Biome Lock: {(filters.LockedBiome?.LabelCap ?? "Any")}");
                if (listing.ButtonText(filters.LockedBiome?.LabelCap ?? "Any Biome"))
                {
                    var biomeOptions = new System.Collections.Generic.List<FloatMenuOption>();

                    // "Any" option (clear lock)
                    biomeOptions.Add(new FloatMenuOption("Any Biome", () => {
                        filters.LockedBiome = null;
                    }));

                    // All available biomes
                    var orderedBiomes = DefDatabase<BiomeDef>.AllDefsListForReading
                        .OrderBy(b =>
                        {
                            var resolved = b.LabelCap.Resolve();
                            return string.IsNullOrEmpty(resolved) ? b.defName : resolved;
                        })
                        .ThenBy(b => b.defName);
                    foreach (var biome in orderedBiomes)
                    {
                        biomeOptions.Add(new FloatMenuOption(biome.LabelCap, () => {
                            filters.LockedBiome = biome;
                        }));
                    }

                    Find.WindowStack.Add(new FloatMenu(biomeOptions));
                }
            }
        }

        private static void DrawPresetCard(Rect rect, Preset preset, FilterSettings filters, UserPreferences preferences)
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

            // Remix button (bottom-right corner) - opens preset in Advanced mode for editing
            // Add 4px padding from edges for breathing room
            Rect remixRect = new Rect(contentRect.xMax - 42f, contentRect.yMax - 20f, 38f, 16f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;

            // Yellow color to stand out from card background
            GUI.color = Color.yellow;
            bool remixClicked = Widgets.ButtonText(remixRect, "Remix", false, true, true);
            GUI.color = Color.white;

            if (remixClicked)
            {
                // Apply preset to Advanced mode filters (not Simple mode)
                preset.ApplyTo(preferences.AdvancedFilters);
                preferences.ActivePreset = preset; // Track active preset for mutator quality overrides
                preferences.Options.PreferencesUIMode = UIMode.Advanced; // Switch to Advanced mode
                Messages.Message($"Loaded '{preset.Name}' into Advanced mode for editing", MessageTypeDefOf.NeutralEvent, false);
            }
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // Hover tooltip for Remix button
            if (Mouse.IsOver(remixRect))
            {
                string remixTooltip = "Load this preset into Advanced mode for customization";
                TooltipHandler.TipRegion(remixRect, remixTooltip);
            }

            // Click to apply preset (invisible button excludes remix button area)
            Rect clickableArea = rect;
            if (!remixRect.Contains(Event.current.mousePosition))
            {
                if (Widgets.ButtonInvisible(rect))
                {
                    preset.ApplyTo(filters);
                    preferences.ActivePreset = preset; // Track active preset for mutator quality overrides
                    Messages.Message($"Applied preset: {preset.Name}", MessageTypeDefOf.NeutralEvent, false);
                }
            }

            // Tooltip with filter summary (for main card area, not remix button)
            if (!Mouse.IsOver(remixRect))
            {
                string tooltip = $"{preset.Name}\n\n{preset.Description}\n\n";
                if (!string.IsNullOrEmpty(preset.FilterSummary))
                    tooltip += $"Filters: {preset.FilterSummary}\n\n";
                if (preset.TargetRarity.HasValue)
                    tooltip += $"Target Rarity: {preset.TargetRarity.Value.ToLabel()}\n\n";
                tooltip += "Click to apply this preset.\nRemix: Load into Advanced mode for editing.";

                TooltipHandler.TipRegion(rect, tooltip);
            }
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

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private const float PresetCardWidth = 160f;  // +33% from 120f
        private const float PresetCardHeight = 93f;  // +33% from 70f
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
            listing.Label("LandingZone_QuickSetup".Translate());
            Text.Font = GameFont.Small;
            listing.GapLine();

            // Preset cards section
            DrawPresetCards(listing, preferences);
            listing.Gap(12f);
            listing.GapLine(); // Visual separator between presets and quick tweaks
            listing.Gap(16f);

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
            listing.Gap(16f);
            listing.GapLine(); // Visual separator between curated and user presets
            listing.Gap(10f);
            Text.Font = GameFont.Tiny;
            listing.Label("LandingZone_MyPresets".Translate());
            Text.Font = GameFont.Small;

            Rect userRowRect = listing.GetRect(PresetCardHeight + PresetCardSpacing);
            int userCount = Math.Min(userPresets.Count, 4); // Cap at 4 user presets

            for (int i = 0; i < userCount; i++)
            {
                float cardX = userRowRect.x + i * (PresetCardWidth + PresetCardSpacing);
                Rect cardRect = new Rect(cardX, userRowRect.y, PresetCardWidth, PresetCardHeight);
                DrawPresetCard(cardRect, userPresets[i], filters, preferences);
            }

            // Community Presets section
            listing.Gap(16f);
            listing.GapLine(); // Visual separator
            listing.Gap(10f);
            Text.Font = GameFont.Tiny;
            listing.Label("LandingZone_CommunityPresets".Translate());
            Text.Font = GameFont.Small;

            // Placeholder box for community presets
            Rect communityPlaceholderRect = listing.GetRect(90f);
            GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.5f); // Subtle dark background
            Widgets.DrawBoxSolid(communityPlaceholderRect, GUI.color);
            GUI.color = Color.white;
            Widgets.DrawBox(communityPlaceholderRect);

            // Draw placeholder text
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Rect textRect = communityPlaceholderRect.ContractedBy(8f);
            Widgets.Label(textRect, "LandingZone_CommunityPresetsPlaceholder".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // Token import/export buttons (two columns)
            listing.Gap(8f);
            var tokenButtonsRect = listing.GetRect(32f);
            var importButtonRect = new Rect(tokenButtonsRect.x, tokenButtonsRect.y, (tokenButtonsRect.width / 2f) - 4f, tokenButtonsRect.height);
            var exportButtonRect = new Rect(tokenButtonsRect.x + (tokenButtonsRect.width / 2f) + 4f, tokenButtonsRect.y, (tokenButtonsRect.width / 2f) - 4f, tokenButtonsRect.height);

            // Import button
            if (Widgets.ButtonText(importButtonRect, "LandingZone_ImportPresetToken".Translate()))
            {
                Find.WindowStack.Add(new Dialog_ImportPresetToken());
            }

            // Export button (disabled if no user presets)
            GUI.enabled = userPresets.Count > 0;
            if (Widgets.ButtonText(exportButtonRect, "LandingZone_ExportPresetToken".Translate()))
            {
                // Show menu to select which preset to export
                var exportOptions = new List<FloatMenuOption>();
                foreach (var preset in userPresets)
                {
                    var presetCapture = preset;
                    exportOptions.Add(new FloatMenuOption(preset.Name, () =>
                    {
                        var token = PresetTokenCodec.EncodePreset(presetCapture);
                        if (!string.IsNullOrEmpty(token))
                        {
                            // Copy to clipboard and show dialog with token
                            GUIUtility.systemCopyBuffer = token;
                            Messages.Message("LandingZone_TokenCopied".Translate(presetCapture.Name), MessageTypeDefOf.NeutralEvent, false);
                            Find.WindowStack.Add(new Dialog_ShowToken(presetCapture.Name, token));
                        }
                        else
                        {
                            Messages.Message("LandingZone_TokenExportFailed".Translate(), MessageTypeDefOf.RejectInput, false);
                        }
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(exportOptions));
            }
            GUI.enabled = true;
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
            Widgets.Label(labelRect, "LandingZone_QuickTweaks".Translate());
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
                listing.Label("LandingZone_ResultLimit".Translate(filters.MaxResults));
                Rect resultSliderRect = listing.GetRect(30f);
                int resultCount = (int)Widgets.HorizontalSlider(
                    resultSliderRect,
                    filters.MaxResults,
                    FilterSettings.MinMaxResults,
                    FilterSettings.MaxResultsLimit,
                    true,
                    "LandingZone_ResultCountLabel".Translate(filters.MaxResults),
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

                listing.Label("LandingZone_TemperatureCenter".Translate(displayCenter.ToString("F0"), tempUnit));
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
                listing.Label("LandingZone_BiomeLock".Translate(filters.LockedBiome?.LabelCap ?? "LandingZone_Any".Translate()));
                if (listing.ButtonText(filters.LockedBiome?.LabelCap ?? "LandingZone_AnyBiome".Translate()))
                {
                    var biomeOptions = new System.Collections.Generic.List<FloatMenuOption>();

                    // "Any" option (clear lock)
                    biomeOptions.Add(new FloatMenuOption("LandingZone_AnyBiome".Translate(), () => {
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
            // Check if this is the active preset
            bool isActive = preferences.ActivePreset?.Id == preset.Id;

            // Check if active preset has been modified (basic check: compare temperature ranges)
            bool isModified = false;
            if (isActive && preferences.ActivePreset != null)
            {
                // Simple modification check: compare a few key properties
                // TODO: Implement comprehensive FilterSettings comparison method
                var presetTemp = preset.Filters.AverageTemperatureRange;
                var currentTemp = filters.AverageTemperatureRange;
                isModified = Math.Abs(presetTemp.min - currentTemp.min) > 0.1f ||
                            Math.Abs(presetTemp.max - currentTemp.max) > 0.1f ||
                            preset.Filters.MaxResults != filters.MaxResults;
            }

            // Draw card background
            Color bgColor = new Color(0.15f, 0.15f, 0.15f);
            Widgets.DrawBoxSolid(rect, bgColor);

            // Active preset: Draw highlighted border with glow effect
            if (isActive)
            {
                Color highlightColor = new Color(0.4f, 0.8f, 0.4f); // Soft green glow
                Widgets.DrawBox(rect, 2); // Thicker border
                GUI.color = highlightColor;
                Widgets.DrawBox(rect);
                GUI.color = Color.white;
            }
            else
            {
                Widgets.DrawBox(rect);
            }

            // Card content
            Rect contentRect = rect.ContractedBy(6f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // Modification badge (top-left corner) - shows when active preset is tweaked
            if (isModified)
            {
                Rect modBadgeRect = new Rect(rect.x + 4f, rect.y + 4f, 16f, 16f);
                Color yellowWarning = new Color(1f, 0.9f, 0.3f);
                GUI.color = yellowWarning;
                // Draw triangle pointing right (▶ shape to indicate "modified from")
                Widgets.DrawBoxSolid(modBadgeRect, yellowWarning * 0.3f);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(modBadgeRect, "✎"); // Pencil icon to indicate edit
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;

                // Tooltip for modification badge
                if (Mouse.IsOver(modBadgeRect))
                {
                    TooltipHandler.TipRegion(modBadgeRect, "LandingZone_ModifiedFromPreset".Translate(preset.Name));
                }
            }

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

            // For user presets only, add Delete button
            bool isUserPreset = preset.Category == "User";
            float remixXOffset = 42f; // Remix button offset

            if (isUserPreset)
            {
                // Delete button (for user presets only) - X icon
                Rect deleteRect = new Rect(contentRect.xMax - 66f, contentRect.yMax - 20f, 20f, 16f);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;

                GUI.color = new Color(1f, 0.3f, 0.3f); // Red for delete
                bool deleteClicked = Widgets.ButtonText(deleteRect, "X", false, true, true);
                GUI.color = Color.white;

                if (deleteClicked)
                {
                    // Simple confirmation using Find.WindowStack
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "LandingZone_DeletePresetConfirm".Translate(preset.Name),
                        delegate
                        {
                            if (PresetLibrary.DeleteUserPreset(preset.Name))
                            {
                                Messages.Message("LandingZone_PresetDeleted".Translate(preset.Name), MessageTypeDefOf.NeutralEvent, false);
                            }
                        },
                        destructive: true
                    ));
                }
            }

            // Remix button (bottom-right corner) - opens preset in Advanced mode for editing
            // Add 4px padding from edges for breathing room
            Rect remixRect = new Rect(contentRect.xMax - remixXOffset, contentRect.yMax - 20f, 38f, 16f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;

            // Yellow color to stand out from card background
            GUI.color = Color.yellow;
            bool remixClicked = Widgets.ButtonText(remixRect, "LandingZone_Remix".Translate(), false, true, true);
            GUI.color = Color.white;

            if (remixClicked)
            {
                var remixTimer = Stopwatch.StartNew();
                Log.Message($"[LandingZone][Remix] Start remix of preset '{preset.Name}' into Advanced mode");
                // Apply preset to Advanced mode filters (not Simple mode)
                preset.ApplyTo(preferences.AdvancedFilters);
                preferences.ActivePreset = preset; // Track active preset for mutator quality overrides
                preferences.Options.PreferencesUIMode = UIMode.Advanced; // Switch to Advanced mode

                // If in Workspace mode and preset has hidden filters, auto-switch to Classic
                AdvancedModeUI.EnsureHiddenFiltersVisible(preferences.AdvancedFilters);

                remixTimer.Stop();
                Log.Message($"[LandingZone][Remix] Completed remix of '{preset.Name}' in {remixTimer.ElapsedMilliseconds} ms");
                Messages.Message("LandingZone_LoadedPresetForEditing".Translate(preset.Name), MessageTypeDefOf.NeutralEvent, false);
            }
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // Hover tooltip for Remix button
            if (Mouse.IsOver(remixRect))
            {
                string remixTooltip = "LandingZone_LoadPresetTooltip".Translate();
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

                    // If in Advanced mode with Workspace view, check for hidden filters
                    if (preferences.Options.PreferencesUIMode == UIMode.Advanced)
                    {
                        AdvancedModeUI.EnsureHiddenFiltersVisible(filters);
                    }

                    Messages.Message("LandingZone_AppliedPreset".Translate(preset.Name), MessageTypeDefOf.NeutralEvent, false);
                }
            }

            // Tooltip with filter summary (for main card area, not remix button)
            if (!Mouse.IsOver(remixRect))
            {
                string tooltip = $"{preset.Name}\n\n{preset.Description}\n\n";
                if (!string.IsNullOrEmpty(preset.FilterSummary))
                    tooltip += "LandingZone_PresetFilters".Translate(preset.FilterSummary) + "\n\n";
                if (preset.TargetRarity.HasValue)
                    tooltip += "LandingZone_TargetRarity".Translate(preset.TargetRarity.Value.ToLabel()) + "\n\n";
                tooltip += "LandingZone_PresetTooltipAction".Translate();

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

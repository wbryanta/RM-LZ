using System.Collections.Generic;
using System.Linq;
using LandingZone.Core.Filtering.Filters;
using LandingZone.Data;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LandingZone.Core.UI
{
    public sealed class LandingZonePreferencesWindow : Window
    {
        private const float ScrollbarWidth = 16f;

        // UI state
        private Vector2 _scrollPos;

        public LandingZonePreferencesWindow()
        {
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            draggable = true;
            onlyOneOfTypeAllowed = true;
            doCloseX = true;
        }

        public override Vector2 InitialSize => new Vector2(960f, 720f); // 50% wider for breathing room

        public override void PreClose()
        {
            // Both Simple and Advanced modes modify FilterSettings directly - no persistence needed
            // Old legacy code persisted local variables which would overwrite direct modifications
        }

        public override void DoWindowContents(Rect inRect)
        {
            var options = LandingZoneContext.State?.Preferences.Options ?? new LandingZoneOptions();
            var currentMode = options.PreferencesUIMode;

            // Mode toggle (3 buttons in single row)
            var modeToggleRect = new Rect(inRect.x, inRect.y, inRect.width, 36f);
            DrawModeToggle(modeToggleRect, ref currentMode);

            // Info/warning banner below mode toggle
            var bannerRect = new Rect(inRect.x, inRect.y + 40f, inRect.width, 24f);
            DrawInfoBanner(bannerRect);

            // Save mode change if toggled
            if (currentMode != options.PreferencesUIMode)
            {
                options.PreferencesUIMode = currentMode;

                // Clear ActivePreset when switching away from Preset Hub
                if (currentMode != UIMode.Simple && LandingZoneContext.State?.Preferences != null)
                {
                    LandingZoneContext.State.Preferences.ActivePreset = null;
                }
            }

            // Content area (below banner)
            var contentRect = new Rect(inRect.x, inRect.y + 70f, inRect.width, inRect.height - 70f);

            // Render appropriate mode UI
            switch (currentMode)
            {
                case UIMode.Simple:
                    DrawSimpleModeContent(contentRect);
                    break;
                case UIMode.GuidedBuilder:
                    DrawGuidedBuilderContent(contentRect);
                    break;
                case UIMode.Advanced:
                    DrawAdvancedModeContent(contentRect);
                    break;
            }
        }

        private void DrawModeToggle(Rect rect, ref UIMode currentMode)
        {
            // Three-tier mode buttons in single row: Preset Hub | Guided Builder | Advanced
            var buttonWidth = 140f;
            var spacing = 12f;
            var totalWidth = (buttonWidth * 3) + (spacing * 2);
            var startX = rect.x + (rect.width - totalWidth) / 2f;

            var presetHubRect = new Rect(startX, rect.y, buttonWidth, 32f);
            var guidedBuilderRect = new Rect(startX + buttonWidth + spacing, rect.y, buttonWidth, 32f);
            var advancedRect = new Rect(startX + (buttonWidth + spacing) * 2, rect.y, buttonWidth, 32f);

            var prevColor = GUI.color;

            // Preset Hub button (Tier 1)
            var isPresetHub = currentMode == UIMode.Simple;
            if (isPresetHub)
            {
                GUI.color = new Color(0.8f, 1f, 0.8f); // Light green tint for active
            }

            if (Widgets.ButtonText(presetHubRect, "Preset Hub"))
            {
                if (!isPresetHub)
                {
                    currentMode = UIMode.Simple;
                }
            }

            GUI.color = prevColor;

            // Guided Builder button (Tier 2)
            var isGuidedBuilder = currentMode == UIMode.GuidedBuilder;
            if (isGuidedBuilder)
            {
                GUI.color = new Color(0.8f, 1f, 0.8f);
            }

            if (Widgets.ButtonText(guidedBuilderRect, "Guided Builder"))
            {
                if (!isGuidedBuilder)
                {
                    currentMode = UIMode.GuidedBuilder;
                }
            }

            GUI.color = prevColor;

            // Advanced button (Tier 3)
            var isAdvanced = currentMode == UIMode.Advanced;
            if (isAdvanced)
            {
                GUI.color = new Color(0.8f, 1f, 0.8f);
            }

            if (Widgets.ButtonText(advancedRect, "Advanced"))
            {
                if (!isAdvanced)
                {
                    currentMode = UIMode.Advanced;
                }
            }

            GUI.color = prevColor;
        }

        private void DrawInfoBanner(Rect rect)
        {
            // Info/warning banner below mode buttons
            // TODO: Swap to warning color when conflicts detected
            var bgColor = new Color(0.15f, 0.15f, 0.15f);
            Widgets.DrawBoxSolid(rect, bgColor);
            Widgets.DrawBox(rect);

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, "Preset Hub: Quick starts | Guided Builder: Goal-based wizard | Advanced: Full control");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        private void DrawSimpleModeContent(Rect contentRect)
        {
            // Tier 1: Preset Hub renderer
            var preferences = LandingZoneContext.State?.Preferences ?? new UserPreferences();

            // Dynamic height: let DefaultModeUI calculate its own content height
            var viewRect = new Rect(0f, 0f, contentRect.width - ScrollbarWidth, 1200f);
            Widgets.BeginScrollView(contentRect, ref _scrollPos, viewRect);

            DefaultModeUI.DrawContent(viewRect, preferences);

            Widgets.EndScrollView();
        }

        private void DrawGuidedBuilderContent(Rect contentRect)
        {
            // Tier 2: Guided Builder renderer (multi-step priority wizard)
            // TODO: Implement 4-step priority wizard in Phase 3
            // For now, show placeholder text
            var listing = new Listing_Standard { ColumnWidth = contentRect.width };
            listing.Begin(contentRect);

            Text.Font = GameFont.Medium;
            listing.Label("Guided Builder (Coming Soon)");
            Text.Font = GameFont.Small;
            listing.Gap(12f);
            listing.Label("This will be a 4-step priority wizard:");
            listing.Label("  1. Select Priority 1 goal (Climate Comfort, Resource Wealth, etc.)");
            listing.Label("  2. Select Priority 2 goal");
            listing.Label("  3. Select Priority 3 goal (optional)");
            listing.Label("  4. Select Priority 4 goal (optional)");
            listing.Gap(12f);
            listing.Label("Then see combined recommendations with:");
            listing.Label("  • Search Now");
            listing.Label("  • Tweak in Advanced");
            listing.Label("  • Save as Preset");

            listing.End();
        }

        private void DrawAdvancedModeContent(Rect contentRect)
        {
            // Use new AdvancedModeUI renderer
            var preferences = LandingZoneContext.State?.Preferences ?? new UserPreferences();

            // Calculate height needed for content + buttons
            var buttonAreaHeight = Prefs.DevMode ? 200f : 80f;
            var viewRect = new Rect(0f, 0f, contentRect.width - ScrollbarWidth, 1800f + buttonAreaHeight);

            Widgets.BeginScrollView(contentRect, ref _scrollPos, viewRect);

            var listing = new Listing_Standard { ColumnWidth = viewRect.width };
            listing.Begin(viewRect);

            // Render Advanced mode filters
            var filterRect = new Rect(0f, 0f, viewRect.width, 1800f);
            AdvancedModeUI.DrawContent(filterRect, preferences);

            // Skip past the filter content
            listing.GetRect(1800f);

            listing.Gap(20f);
            listing.GapLine();

            // Control buttons
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            listing.Label("Changes are saved automatically");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            listing.Gap(4f);

            if (listing.ButtonText("Reset to defaults"))
            {
                preferences.ResetActiveFilters();
                Messages.Message("Filters reset to defaults", MessageTypeDefOf.NeutralEvent, false);
            }

            listing.Gap(8f);

            // Import/Export between modes
            var currentUIMode = preferences.Options.PreferencesUIMode;
            if (currentUIMode == UIMode.Simple)
            {
                if (listing.ButtonText("Copy to Advanced mode"))
                {
                    preferences.CopySimpleToAdvanced();
                    Messages.Message("Simple mode settings copied to Advanced mode", MessageTypeDefOf.NeutralEvent, false);
                }
            }
            else
            {
                if (listing.ButtonText("Copy to Simple mode"))
                {
                    preferences.CopyAdvancedToSimple();
                    Messages.Message("Advanced mode settings copied to Simple mode", MessageTypeDefOf.NeutralEvent, false);
                }
            }

            // Dev mode: Developer tools section
            if (Prefs.DevMode)
            {
                listing.GapLine(12f);
                Text.Font = GameFont.Small;
                listing.Label("=== DEVELOPER TOOLS ===");
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                listing.Label("Dev Mode only - hidden in release builds.");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                listing.Gap(8f);

                // Logging level selector
                listing.Label($"Logging Level: {LandingZoneSettings.LogLevel.ToLabel()}");
                if (listing.ButtonTextLabeled("Current level:", LandingZoneSettings.LogLevel.ToLabel()))
                {
                    var options = new System.Collections.Generic.List<FloatMenuOption>();
                    foreach (LoggingLevel level in System.Enum.GetValues(typeof(LoggingLevel)))
                    {
                        options.Add(new FloatMenuOption(level.ToLabel() + " - " + level.GetTooltip(), () => {
                            LandingZoneSettings.LogLevel = level;
                            Messages.Message($"Logging level set to {level.ToLabel()}", MessageTypeDefOf.NeutralEvent, false);
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }

                listing.Gap(4f);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                listing.Label(LandingZoneSettings.LogLevel.GetTooltip());
                GUI.color = Color.white;
                Text.Font = GameFont.Small;

                listing.Gap(12f);

                // Cache dump button
                if (listing.ButtonText("[DEV] Dump FULL World Cache"))
                {
                    Log.Message("[LandingZone] Dumping FULL world cache (this may take a while)...");
                    Diagnostics.WorldDataDumper.DumpWorldData();
                }

                listing.Gap(4f);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                listing.Label("Dumps all ~295k tiles with full property reflection to Config folder");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;

                listing.Gap(12f);

                // Performance test button
                if (listing.ButtonText("[DEV] Run Performance Test"))
                {
                    Log.Message("[LandingZone] Running performance test...");
                    Diagnostics.FilterPerformanceTest.RunPerformanceTest();
                    Messages.Message("[LandingZone] Performance test complete - check Player.log", MessageTypeDefOf.NeutralEvent, false);
                }

                listing.Gap(4f);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                listing.Label("Benchmarks filter performance on current world");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;

                listing.Gap(12f);

                // Note about Results window DEBUG dump
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                listing.Label("Note: [DEBUG] Dump button also available in Results window (top-right)");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;

                listing.Gap(12f);
            }

            listing.End();
            Widgets.EndScrollView();
        }
    }
}

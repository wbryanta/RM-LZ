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

        public override Vector2 InitialSize => new Vector2(520f, 620f);

        public override void PreClose()
        {
            // Both Simple and Advanced modes modify FilterSettings directly - no persistence needed
            // Old legacy code persisted local variables which would overwrite direct modifications
        }

        public override void DoWindowContents(Rect inRect)
        {
            var options = LandingZoneContext.State?.Preferences.Options ?? new LandingZoneOptions();
            var currentMode = options.PreferencesUIMode;

            // Mode toggle header (before scroll view)
            var headerRect = new Rect(inRect.x, inRect.y, inRect.width, 60f);
            DrawModeToggle(headerRect, ref currentMode);

            // Save mode change if toggled
            if (currentMode != options.PreferencesUIMode)
            {
                options.PreferencesUIMode = currentMode;
            }

            // Content area (below mode toggle)
            var contentRect = new Rect(inRect.x, inRect.y + 70f, inRect.width, inRect.height - 70f);

            // Render appropriate mode UI
            if (currentMode == UIMode.Simple)
            {
                // Simple mode: Use new simplified UI
                DrawSimpleModeContent(contentRect);
            }
            else
            {
                // Advanced mode: Use AdvancedModeUI
                DrawAdvancedModeContent(contentRect);
            }
        }

        private void DrawModeToggle(Rect rect, ref UIMode currentMode)
        {
            // Mode toggle buttons (Simple | Advanced)
            var buttonWidth = 120f;
            var spacing = 10f;
            var totalWidth = (buttonWidth * 2) + spacing;
            var startX = rect.x + (rect.width - totalWidth) / 2f;

            var simpleRect = new Rect(startX, rect.y + 5f, buttonWidth, 30f);
            var advancedRect = new Rect(startX + buttonWidth + spacing, rect.y + 5f, buttonWidth, 30f);

            // Simple button - draw with visual highlight if active, but only switch if NOT already simple
            var isSimple = currentMode == UIMode.Simple;

            // Visual styling for active button
            var prevColor = GUI.color;
            if (isSimple)
            {
                GUI.color = new Color(0.8f, 1f, 0.8f); // Light green tint for active
            }

            if (Widgets.ButtonText(simpleRect, "Simple"))
            {
                if (!isSimple)
                {
                    currentMode = UIMode.Simple;
                }
            }

            GUI.color = prevColor;

            // Advanced button - same pattern
            var isAdvanced = currentMode == UIMode.Advanced;

            if (isAdvanced)
            {
                GUI.color = new Color(0.8f, 1f, 0.8f); // Light green tint for active
            }

            if (Widgets.ButtonText(advancedRect, "Advanced"))
            {
                if (!isAdvanced)
                {
                    currentMode = UIMode.Advanced;
                }
            }

            GUI.color = prevColor;

            // Helper text explaining mode independence
            var helpTextRect = new Rect(rect.x, rect.y + 40f, rect.width, 18f);
            Text.Font = GameFont.Tiny;
            var prevTextColor = GUI.color;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(helpTextRect, "Each mode maintains its own filter settings that persist across sessions.");
            GUI.color = prevTextColor;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        private void DrawSimpleModeContent(Rect contentRect)
        {
            // Use new DefaultModeUI renderer (still named DefaultModeUI for now)
            var preferences = LandingZoneContext.State?.Preferences ?? new UserPreferences();

            // Dynamic height: let DefaultModeUI calculate its own content height
            var viewRect = new Rect(0f, 0f, contentRect.width - ScrollbarWidth, 1200f); // Increased to accommodate all filters
            Widgets.BeginScrollView(contentRect, ref _scrollPos, viewRect);

            DefaultModeUI.DrawContent(viewRect, preferences);

            Widgets.EndScrollView();
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

            // Dev mode: Performance test button
            if (Prefs.DevMode)
            {
                listing.GapLine(12f);
                Text.Font = GameFont.Small;
                listing.Label("=== DEVELOPER TOOLS ===");
                listing.Gap(4f);

                if (listing.ButtonText("[DEV] Run Performance Test"))
                {
                    Log.Message("[LandingZone] Running performance test...");
                    Diagnostics.FilterPerformanceTest.RunPerformanceTest();
                    Messages.Message("[LandingZone] Performance test complete - check Player.log", MessageTypeDefOf.NeutralEvent, false);
                }

                listing.Gap(4f);

                if (listing.ButtonText("[DEV] Dump FULL World Cache"))
                {
                    Log.Message("[LandingZone] Dumping FULL world cache (this may take a while)...");
                    Diagnostics.WorldDataDumper.DumpWorldData();
                }

                listing.Gap(12f);
            }

            listing.End();
            Widgets.EndScrollView();
        }
    }
}

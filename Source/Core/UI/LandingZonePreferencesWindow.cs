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
            // Both Default and Advanced modes modify FilterSettings directly - no persistence needed
            // Old legacy code persisted local variables which would overwrite direct modifications
        }

        public override void DoWindowContents(Rect inRect)
        {
            var options = LandingZoneContext.State?.Preferences.Options ?? new LandingZoneOptions();
            var currentMode = options.PreferencesUIMode;

            // Mode toggle header (before scroll view)
            var headerRect = new Rect(inRect.x, inRect.y, inRect.width, 40f);
            DrawModeToggle(headerRect, ref currentMode);

            // Save mode change if toggled
            if (currentMode != options.PreferencesUIMode)
            {
                options.PreferencesUIMode = currentMode;
            }

            // Content area (below mode toggle)
            var contentRect = new Rect(inRect.x, inRect.y + 50f, inRect.width, inRect.height - 50f);

            // Render appropriate mode UI
            if (currentMode == UIMode.Default)
            {
                // Default mode: Use new simplified UI
                DrawDefaultModeContent(contentRect);
            }
            else
            {
                // Advanced mode: Use AdvancedModeUI
                DrawAdvancedModeContent(contentRect);
            }
        }

        private void DrawModeToggle(Rect rect, ref UIMode currentMode)
        {
            // Mode toggle buttons (Default | Advanced)
            var buttonWidth = 120f;
            var spacing = 10f;
            var totalWidth = (buttonWidth * 2) + spacing;
            var startX = rect.x + (rect.width - totalWidth) / 2f;

            var defaultRect = new Rect(startX, rect.y + 5f, buttonWidth, 30f);
            var advancedRect = new Rect(startX + buttonWidth + spacing, rect.y + 5f, buttonWidth, 30f);

            // Default button
            var isDefault = currentMode == UIMode.Default;
            if (Widgets.ButtonText(defaultRect, "Default", active: isDefault))
            {
                currentMode = UIMode.Default;
            }

            // Advanced button
            var isAdvanced = currentMode == UIMode.Advanced;
            if (Widgets.ButtonText(advancedRect, "Advanced", active: isAdvanced))
            {
                currentMode = UIMode.Advanced;
            }
        }

        private void DrawDefaultModeContent(Rect contentRect)
        {
            // Use new DefaultModeUI renderer
            var preferences = LandingZoneContext.State?.Preferences ?? new UserPreferences();

            var viewRect = new Rect(0f, 0f, contentRect.width - ScrollbarWidth, 600f);
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
                preferences.Filters.Reset();
                Messages.Message("Filters reset to defaults", MessageTypeDefOf.NeutralEvent, false);
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

                if (listing.ButtonText("[DEV] Dump World Tile Data"))
                {
                    Log.Message("[LandingZone] Dumping world tile data...");
                    Diagnostics.WorldDataDumper.DumpWorldData();
                }

                listing.Gap(4f);

                if (listing.ButtonText("[DEV] Dump FULL World Cache"))
                {
                    Log.Message("[LandingZone] Dumping FULL world cache (this may take a while)...");
                    Diagnostics.WorldDataDumper.DumpFullWorldCache();
                }

                listing.Gap(12f);
            }

            listing.End();
            Widgets.EndScrollView();
        }
    }
}

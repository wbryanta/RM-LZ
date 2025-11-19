using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace LandingZone.Core.UI
{
    /// <summary>
    /// Dev Mode only window for debug tools and diagnostics.
    /// Accessible via "Dev" button on world selection screen bottom toolbar.
    /// </summary>
    public class DevToolsWindow : Window
    {
        private Vector2 _scrollPosition = Vector2.zero;

        public DevToolsWindow()
        {
            doCloseX = true;
            doCloseButton = false;
            forcePause = false;
            absorbInputAroundWindow = false;
            closeOnAccept = false;
            closeOnCancel = true;
        }

        public override Vector2 InitialSize => new Vector2(450f, 450f);

        public override void DoWindowContents(Rect inRect)
        {
            // Calculate content height - scrollbar appears automatically if content exceeds window height
            float contentHeight = 350f; // Estimated total height of all content

            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, contentHeight);
            Rect scrollRect = new Rect(inRect.x, inRect.y, inRect.width, inRect.height);

            Widgets.BeginScrollView(scrollRect, ref _scrollPosition, viewRect, true);

            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            Text.Font = GameFont.Medium;
            listing.Label("Developer Tools");
            Text.Font = GameFont.Small;
            listing.Gap(4f);

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            listing.Label("Dev Mode only - hidden in release builds");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            listing.Gap(12f);

            // Logging level selector
            listing.Label($"Logging Level: {LandingZoneSettings.LogLevel.ToLabel()}");
            if (listing.ButtonTextLabeled("Select level:", LandingZoneSettings.LogLevel.ToLabel()))
            {
                var options = new List<FloatMenuOption>();
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
            if (listing.ButtonText("Dump FULL World Cache"))
            {
                Log.Message("[LandingZone] Dumping FULL world cache (this may take a while)...");
                Diagnostics.WorldDataDumper.DumpWorldData();

                // Show file location
                string configPath;
                if (Application.platform == RuntimePlatform.OSXPlayer)
                {
                    configPath = "~/Library/Application Support/RimWorld/Config/";
                }
                else if (Application.platform == RuntimePlatform.WindowsPlayer)
                {
                    configPath = "%USERPROFILE%\\AppData\\LocalLow\\Ludeon Studios\\RimWorld by Ludeon Studios\\Config\\";
                }
                else
                {
                    configPath = "~/.config/unity3d/Ludeon Studios/RimWorld by Ludeon Studios/Config/";
                }

                Messages.Message($"[LandingZone] Full world cache dump started. Check {configPath} for LandingZone_FullCache_[timestamp].txt", MessageTypeDefOf.NeutralEvent, false);
            }

            listing.Gap(4f);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            listing.Label("Dumps all ~295k tiles with full property reflection.");
            listing.Label("Output: Config/LandingZone_FullCache_[timestamp].txt");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            listing.Gap(12f);

            // Performance test button
            if (listing.ButtonText("Run Performance Test"))
            {
                Log.Message("[LandingZone] Running performance test...");
                Diagnostics.FilterPerformanceTest.RunPerformanceTest();
                Messages.Message("[LandingZone] Performance test complete - check Player.log", MessageTypeDefOf.NeutralEvent, false);
            }

            listing.Gap(4f);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            listing.Label("Benchmarks filter performance on current world.");
            listing.Label("Results written to Player.log.");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            listing.Gap(12f);

            // Note about Results window DEBUG dump
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            listing.Label("Note: [DEBUG] Dump button also available in Results window (top-right).");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            listing.End();
            Widgets.EndScrollView();
        }
    }
}

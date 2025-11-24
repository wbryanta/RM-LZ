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
            listing.Label("LandingZone_DevTools_WindowTitle".Translate());
            Text.Font = GameFont.Small;
            listing.Gap(4f);

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            listing.Label("LandingZone_DevTools_DevModeOnly".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            listing.Gap(12f);

            // Logging level selector
            listing.Label("LandingZone_DevTools_LoggingLevel".Translate(LandingZoneSettings.LogLevel.ToLabel()));
            if (listing.ButtonTextLabeled("LandingZone_DevTools_SelectLevel".Translate(), LandingZoneSettings.LogLevel.ToLabel()))
            {
                var options = new List<FloatMenuOption>();
                foreach (LoggingLevel level in System.Enum.GetValues(typeof(LoggingLevel)))
                {
                    options.Add(new FloatMenuOption(level.ToLabel() + " - " + level.GetTooltip(), () => {
                        LandingZoneSettings.LogLevel = level;
                        Messages.Message("LandingZone_DevTools_LoggingLevelSet".Translate(level.ToLabel()), MessageTypeDefOf.NeutralEvent, false);
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
            if (listing.ButtonText("LandingZone_DevTools_DumpWorldCache".Translate()))
            {
                Log.Message("LandingZone_DevTools_DumpStarted".Translate());
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

                Messages.Message("LandingZone_DevTools_DumpCompleted".Translate(configPath), MessageTypeDefOf.NeutralEvent, false);
            }

            listing.Gap(4f);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            listing.Label("LandingZone_DevTools_DumpDescription".Translate());
            listing.Label("LandingZone_DevTools_DumpOutputPath".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            listing.Gap(12f);

            // Performance test button
            if (listing.ButtonText("LandingZone_DevTools_RunPerformanceTest".Translate()))
            {
                Log.Message("LandingZone_DevTools_PerfTestStarted".Translate());
                Diagnostics.FilterPerformanceTest.RunPerformanceTest();
                Messages.Message("LandingZone_DevTools_PerfTestCompleted".Translate(), MessageTypeDefOf.NeutralEvent, false);
            }

            listing.Gap(4f);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            listing.Label("LandingZone_DevTools_PerfTestDescription".Translate());
            listing.Label("LandingZone_DevTools_PerfTestResults".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            listing.Gap(12f);

            // Note about match data logging
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            listing.Label("LandingZone_DevTools_VerboseTip".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            listing.End();
            Widgets.EndScrollView();
        }
    }
}

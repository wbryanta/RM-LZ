using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using LandingZone.Core.Diagnostics;

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
            // Calculate content height - keep generous to ensure all controls are visible
            float contentHeight = 680f;  // Increased for new debug buttons

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
            listing.GapLine();
            listing.Gap(8f);

            // Phase-A diagnostics toggle
            var diagLabel = DevDiagnostics.PhaseADiagnosticsEnabled
                ? "LandingZone_DevTools_PhaseADiag_On".Translate()
                : "LandingZone_DevTools_PhaseADiag_Off".Translate();
            if (listing.ButtonText(diagLabel))
            {
                DevDiagnostics.PhaseADiagnosticsEnabled = !DevDiagnostics.PhaseADiagnosticsEnabled;
                var stateLabel = DevDiagnostics.PhaseADiagnosticsEnabled ? "ON" : "OFF";
                Messages.Message($"[DEV] Phase-A diagnostics {stateLabel}", MessageTypeDefOf.NeutralEvent, false);
            }

            listing.Gap(6f);
            // Mini world snapshot (small text dump)
            if (listing.ButtonText("LandingZone_DevTools_MiniSnapshot".Translate()))
            {
                try
                {
                    DevDiagnostics.DumpMiniWorldSnapshot();
                }
                catch (System.Exception ex)
                {
                    Log.Error($"[LandingZone][DEV] Failed to dump mini world snapshot: {ex}");
                    Messages.Message("[DEV] Mini snapshot dump failed. See log.", MessageTypeDefOf.RejectInput, false);
                }
            }

            listing.Gap(4f);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            listing.Label("LandingZone_DevTools_MiniSnapshot_Desc".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            listing.Gap(12f);
            listing.GapLine();
            listing.Gap(8f);

            // World definition dump (biomes, mutators, world objects)
            if (listing.ButtonText("LandingZone_DevTools_DumpWorld".Translate()))
            {
                try
                {
                    DevDiagnostics.DumpWorldDefinitions();
                    Messages.Message("LandingZone_DevTools_DumpWorld_Message".Translate(), MessageTypeDefOf.TaskCompletion, false);
                }
                catch (System.Exception ex)
                {
                    Log.Error($"[LandingZone][DEV] Failed to dump world definitions: {ex}");
                    Messages.Message("[DEV] Dump failed. See log.", MessageTypeDefOf.RejectInput, false);
                }
            }

            listing.Gap(6f);
            // Coverage comparison: runtime mutators vs UI map features
            if (listing.ButtonText("LandingZone_DevTools_CompareMutators".Translate()))
            {
                try
                {
                    DevDiagnostics.CompareMutatorCoverage();
                    Messages.Message("LandingZone_DevTools_CompareMutators_Message".Translate(), MessageTypeDefOf.TaskCompletion, false);
                }
                catch (System.Exception ex)
                {
                    Log.Error($"[LandingZone][DEV] Failed mutator coverage compare: {ex}");
                    Messages.Message("[DEV] Coverage compare failed. See log.", MessageTypeDefOf.RejectInput, false);
                }
            }

            listing.Gap(6f);
            // Dump top search results with detailed breakdown
            if (listing.ButtonText("LandingZone_DevTools_DumpTopResults".Translate()))
            {
                try
                {
                    DevDiagnostics.DumpTopResults(10);
                    Messages.Message("LandingZone_DevTools_DumpTopResults_Message".Translate(), MessageTypeDefOf.TaskCompletion, false);
                }
                catch (System.Exception ex)
                {
                    Log.Error($"[LandingZone][DEV] Failed to dump top results: {ex}");
                    Messages.Message("[DEV] Top results dump failed. See log.", MessageTypeDefOf.RejectInput, false);
                }
            }

            listing.Gap(4f);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            listing.Label("LandingZone_DevTools_DumpTopResults_Desc".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            listing.Gap(6f);
            // Dump valid mineable ores (for mod ore debugging)
            if (listing.ButtonText("LandingZone_DevTools_DumpOres".Translate()))
            {
                try
                {
                    DevDiagnostics.DumpValidOres();
                    Messages.Message("LandingZone_DevTools_DumpOres_Message".Translate(), MessageTypeDefOf.TaskCompletion, false);
                }
                catch (System.Exception ex)
                {
                    Log.Error($"[LandingZone][DEV] Failed to dump valid ores: {ex}");
                    Messages.Message("[DEV] Valid ores dump failed. See log.", MessageTypeDefOf.RejectInput, false);
                }
            }

            listing.Gap(4f);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            listing.Label("LandingZone_DevTools_DumpOres_Desc".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            listing.Gap(12f);
            listing.GapLine();
            listing.Gap(8f);

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

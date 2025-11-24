using RimWorld;
using UnityEngine;
using Verse;

namespace LandingZone.Core.UI
{
    /// <summary>
    /// Informational dialog shown when search exceeds 20s threshold.
    /// Offers optional suggestions to improve search performance.
    /// User can dismiss and continue with current settings.
    /// </summary>
    public class Dialog_PerformanceWarning : Window
    {
        private readonly long _elapsedMs;
        private readonly int _currentChunkSize;
        private readonly MaxCandidateTilesLimit _currentMaxCandidates;

        public Dialog_PerformanceWarning(long elapsedMs, int currentChunkSize, MaxCandidateTilesLimit currentMaxCandidates)
        {
            _elapsedMs = elapsedMs;
            _currentChunkSize = currentChunkSize;
            _currentMaxCandidates = currentMaxCandidates;

            doCloseButton = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            forcePause = true;
        }

        public override Vector2 InitialSize => new Vector2(650f, 480f);

        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            // Header
            Text.Font = GameFont.Medium;
            GUI.color = new Color(0.7f, 0.85f, 1f); // Informational blue (not warning yellow)
            listing.Label("⏱ Performance Suggestion");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            listing.GapLine();
            listing.Gap(8f);

            // Informational message (not a warning)
            listing.Label($"Your search has been running for {_elapsedMs / 1000f:F1} seconds.");
            listing.Gap(4f);
            listing.Label($"Current settings: Chunk size = {_currentChunkSize}, Max candidates = {_currentMaxCandidates.ToLabel()}");
            listing.Gap(8f);

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            listing.Label("Large worlds or complex filters can cause longer search times.");
            listing.Label("You can continue searching, or try these optional optimizations:");
            listing.Gap(6f);
            listing.Label("• Reduce chunk size → faster UI updates (slightly slower overall search)");
            listing.Label("• Lower max candidates → fewer tiles processed in Stage B");
            listing.Label("• Apply Safe profile → conservative settings for slow machines");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            listing.Gap(16f);

            // Optional quick-apply actions
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.9f, 0.9f, 0.9f);
            listing.Label("Optional Quick Actions (or close to continue):");
            GUI.color = Color.white;
            listing.Gap(8f);

            // Reduce chunk size button
            if (_currentChunkSize > 250)
            {
                if (listing.ButtonText($"Reduce Chunk Size to 250 (currently {_currentChunkSize})"))
                {
                    var settings = LandingZoneMod.Instance?.Settings;
                    if (settings != null)
                    {
                        settings.EvaluationChunkSize = 250;
                        LandingZoneMod.Instance.WriteSettings();
                        Messages.Message("Chunk size reduced to 250. Restart search for changes to take effect.", MessageTypeDefOf.TaskCompletion, false);
                    }
                    Close();
                }
            }
            else
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                listing.Label("Chunk size already at 250 or lower");
                GUI.color = Color.white;
            }

            listing.Gap(4f);

            // Reduce max candidates button (if not already Conservative)
            if (_currentMaxCandidates != MaxCandidateTilesLimit.Conservative)
            {
                if (listing.ButtonText($"Lower Max Candidates to Conservative (25k) (currently {_currentMaxCandidates.ToLabel()})"))
                {
                    LandingZoneSettings.MaxCandidates = MaxCandidateTilesLimit.Conservative;
                    LandingZoneMod.Instance?.WriteSettings();
                    Messages.Message("Max candidates set to Conservative (25k). Restart search for changes to take effect.", MessageTypeDefOf.TaskCompletion, false);
                    Close();
                }
            }
            else
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                listing.Label("Max candidates already at Conservative (25k)");
                GUI.color = Color.white;
            }

            listing.Gap(4f);

            // Apply Safe profile button
            if (LandingZoneSettings.CurrentPerformanceProfile != PerformanceProfile.Safe)
            {
                if (listing.ButtonText("Apply Safe Profile (Chunk: 250, Max: 25k)"))
                {
                    var settings = LandingZoneMod.Instance?.Settings;
                    if (settings != null)
                    {
                        settings.ApplyPerformanceProfile(PerformanceProfile.Safe);
                        Messages.Message("Performance profile set to Safe. Restart search for changes to take effect.", MessageTypeDefOf.TaskCompletion, false);
                    }
                    Close();
                }
            }
            else
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                listing.Label("Safe profile already active");
                GUI.color = Color.white;
            }

            listing.Gap(16f);

            // Continue message (emphasize it's optional)
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.7f, 0.85f, 1f);
            listing.Label("Close this dialog to continue searching with current settings.");
            listing.Label("No changes required - this is just a performance tip.");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            listing.End();
        }
    }
}

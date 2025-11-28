#nullable enable
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
            listing.Label("LandingZone_Perf_Header".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            listing.GapLine();
            listing.Gap(8f);

            // Informational message (not a warning)
            listing.Label("LandingZone_Perf_Runtime".Translate(_elapsedMs / 1000f));
            listing.Gap(4f);
            listing.Label("LandingZone_Perf_CurrentSettings".Translate(_currentChunkSize, _currentMaxCandidates.ToLabel()));
            listing.Gap(8f);

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            listing.Label("LandingZone_Perf_InfoLine1".Translate());
            listing.Label("LandingZone_Perf_InfoLine2".Translate());
            listing.Gap(6f);
            listing.Label("LandingZone_Perf_Opt1".Translate());
            listing.Label("LandingZone_Perf_Opt2".Translate());
            listing.Label("LandingZone_Perf_Opt3".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            listing.Gap(16f);

            // Optional quick-apply actions
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.9f, 0.9f, 0.9f);
            listing.Label("LandingZone_Perf_QuickActions".Translate());
            GUI.color = Color.white;
            listing.Gap(8f);

            // Reduce chunk size button
            if (_currentChunkSize > 250)
            {
                if (listing.ButtonText("LandingZone_Perf_ChunkButton".Translate(_currentChunkSize)))
                {
                    var settings = LandingZoneMod.Instance?.Settings;
                    if (settings != null)
                    {
                        settings.EvaluationChunkSize = 250;
                        LandingZoneMod.Instance?.WriteSettings();
                        Messages.Message("LandingZone_Perf_ChunkApplied".Translate(), MessageTypeDefOf.TaskCompletion, false);
                    }
                    Close();
                }
            }
            else
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                listing.Label("LandingZone_Perf_ChunkAlready".Translate());
                GUI.color = Color.white;
            }

            listing.Gap(4f);

            // Reduce max candidates button (if not already Conservative)
            if (_currentMaxCandidates != MaxCandidateTilesLimit.Conservative)
            {
                if (listing.ButtonText("LandingZone_Perf_MaxButton".Translate(_currentMaxCandidates.ToLabel())))
                {
                    LandingZoneSettings.MaxCandidates = MaxCandidateTilesLimit.Conservative;
                    LandingZoneMod.Instance?.WriteSettings();
                    Messages.Message("LandingZone_Perf_MaxApplied".Translate(), MessageTypeDefOf.TaskCompletion, false);
                    Close();
                }
            }
            else
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                listing.Label("LandingZone_Perf_MaxAlready".Translate());
                GUI.color = Color.white;
            }

            listing.Gap(4f);

            // Apply Safe profile button
            if (LandingZoneSettings.CurrentPerformanceProfile != PerformanceProfile.Safe)
            {
                if (listing.ButtonText("LandingZone_Perf_ProfileButton".Translate()))
                {
                    var settings = LandingZoneMod.Instance?.Settings;
                    if (settings != null)
                    {
                        settings.ApplyPerformanceProfile(PerformanceProfile.Safe);
                        Messages.Message("LandingZone_Perf_ProfileApplied".Translate(), MessageTypeDefOf.TaskCompletion, false);
                    }
                    Close();
                }
            }
            else
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                listing.Label("LandingZone_Perf_ProfileAlready".Translate());
                GUI.color = Color.white;
            }

            listing.Gap(16f);

            // Continue message (emphasize it's optional)
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.7f, 0.85f, 1f);
            listing.Label("LandingZone_Perf_CloseContinue".Translate());
            listing.Label("LandingZone_Perf_NoChanges".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            listing.End();
        }
    }
}

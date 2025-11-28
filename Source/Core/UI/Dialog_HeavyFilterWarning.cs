#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld;
using UnityEngine;
using Verse;

namespace LandingZone.Core.UI
{
    /// <summary>
    /// Warning dialog shown when heavy filters are set to MustHave/MustNotHave.
    /// Offers three choices: Proceed anyway, Demote to Priority, or Cancel.
    /// </summary>
    public class Dialog_HeavyFilterWarning : Window
    {
        private readonly List<(string Id, string Label, FilterImportance Importance)> _heavyGateFilters;
        private readonly FilterSettings _filters;
        private readonly Action _onProceed;
        private readonly Action? _onCancel;

        public Dialog_HeavyFilterWarning(
            List<(string Id, string Label, FilterImportance Importance)> heavyGateFilters,
            FilterSettings filters,
            Action onProceed,
            Action? onCancel = null)
        {
            _heavyGateFilters = heavyGateFilters;
            _filters = filters;
            _onProceed = onProceed;
            _onCancel = onCancel;

            doCloseButton = false;
            doCloseX = true;
            absorbInputAroundWindow = true;
            forcePause = true;
            closeOnClickedOutside = false;
        }

        public override Vector2 InitialSize => new Vector2(550f, 380f);

        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            // Header with warning icon
            Text.Font = GameFont.Medium;
            GUI.color = new Color(1f, 0.8f, 0.4f); // Amber warning
            listing.Label("LandingZone_HeavyFilter_Header".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            listing.GapLine();
            listing.Gap(8f);

            // Explanation
            listing.Label("LandingZone_HeavyFilter_Explanation".Translate());
            listing.Gap(8f);

            // List affected filters
            Text.Font = GameFont.Small;
            GUI.color = new Color(1f, 0.7f, 0.3f);
            listing.Label("LandingZone_HeavyFilter_AffectedFilters".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Tiny;

            foreach (var (id, label, importance) in _heavyGateFilters)
            {
                string importanceLabel = importance == FilterImportance.MustHave
                    ? "LandingZone_ImportanceState_MustHave".Translate()
                    : "LandingZone_ImportanceState_MustNotHave".Translate();
                listing.Label($"  • {label} ({importanceLabel})");
            }

            Text.Font = GameFont.Small;
            listing.Gap(12f);

            // Performance impact note
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            listing.Label("LandingZone_HeavyFilter_PerformanceNote".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            listing.Gap(16f);

            // Action buttons
            listing.Label("LandingZone_HeavyFilter_ChooseAction".Translate());
            listing.Gap(8f);

            // Button 1: Proceed anyway
            if (listing.ButtonText("LandingZone_HeavyFilter_Proceed".Translate()))
            {
                Log.Message($"[LandingZone] Heavy filter warning: User chose PROCEED with {_heavyGateFilters.Count} heavy+gate filter(s)");
                Close();
                _onProceed.Invoke();
            }
            listing.Gap(4f);

            // Button 2: Demote to Priority
            if (listing.ButtonText("LandingZone_HeavyFilter_Demote".Translate()))
            {
                DemoteHeavyFilters();
                Log.Message($"[LandingZone] Heavy filter warning: User chose DEMOTE - {_heavyGateFilters.Count} filter(s) demoted to Priority");
                Messages.Message("LandingZone_HeavyFilter_DemotedMessage".Translate(_heavyGateFilters.Count), MessageTypeDefOf.TaskCompletion, false);
                Close();
                _onProceed.Invoke();
            }
            listing.Gap(4f);

            // Button 3: Cancel
            if (listing.ButtonText("LandingZone_Cancel".Translate()))
            {
                Log.Message("[LandingZone] Heavy filter warning: User chose CANCEL");
                Close();
                _onCancel?.Invoke();
            }

            listing.End();
        }

        private void DemoteHeavyFilters()
        {
            foreach (var (id, _, _) in _heavyGateFilters)
            {
                // Demote the filter importance from MustHave/MustNotHave to Priority
                switch (id)
                {
                    case "growing_days":
                        _filters.GrowingDaysImportance = FilterImportance.Priority;
                        break;
                    case "graze":
                        _filters.GrazeImportance = FilterImportance.Priority;
                        break;
                    case "forageable_food":
                        _filters.ForageableFoodImportance = FilterImportance.Priority;
                        break;
                }
            }
        }

        public override void OnCancelKeyPressed()
        {
            Close();
            _onCancel?.Invoke();
        }
    }
}

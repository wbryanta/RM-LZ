using System.Linq;
using LandingZone.Data;
using RimWorld;
using UnityEngine;
using Verse;

namespace LandingZone.Core.UI
{
    /// <summary>
    /// Dialog for saving current Simple mode filters as a user preset.
    /// </summary>
    public class Dialog_SavePreset : Window
    {
        private readonly FilterSettings _filters;
        private readonly Preset? _activePreset;
        private string _presetName = "";
        private bool _showOverwriteConfirm = false;

        public Dialog_SavePreset(FilterSettings filters, Preset? activePreset = null)
        {
            _filters = filters;
            _activePreset = activePreset;
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnCancel = true;
        }

        public override Vector2 InitialSize => new Vector2(450f, _showOverwriteConfirm ? 240f : 200f);

        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            Text.Font = GameFont.Medium;
            listing.Label("LandingZone_SavePresetTitle".Translate());
            Text.Font = GameFont.Small;
            listing.GapLine();

            listing.Gap(10f);

            listing.Label("LandingZone_PresetNameLabel".Translate());
            _presetName = listing.TextEntry(_presetName);

            // Enforce 10-character limit
            if (_presetName.Length > 10)
            {
                _presetName = _presetName.Substring(0, 10);
            }

            // Show overwrite confirmation if preset exists
            if (_showOverwriteConfirm)
            {
                listing.Gap(10f);
                GUI.color = new Color(1f, 0.8f, 0.5f); // Orange warning color
                listing.Label("LandingZone_PresetOverwriteConfirm".Translate(_presetName));
                GUI.color = Color.white;
            }

            listing.Gap(20f);

            // Buttons
            Rect buttonRect = listing.GetRect(35f);

            if (_showOverwriteConfirm)
            {
                // Three buttons: Overwrite, Rename, Cancel
                float buttonWidth = (buttonRect.width - 20f) / 3f;

                Rect overwriteRect = new Rect(buttonRect.x, buttonRect.y, buttonWidth, buttonRect.height);
                Rect renameRect = new Rect(buttonRect.x + buttonWidth + 10f, buttonRect.y, buttonWidth, buttonRect.height);
                Rect cancelRect = new Rect(buttonRect.x + (buttonWidth + 10f) * 2f, buttonRect.y, buttonWidth, buttonRect.height);

                GUI.color = new Color(1f, 0.7f, 0.5f); // Orange for overwrite
                if (Widgets.ButtonText(overwriteRect, "LandingZone_Overwrite".Translate()))
                {
                    PerformOverwrite();
                }
                GUI.color = Color.white;

                if (Widgets.ButtonText(renameRect, "LandingZone_Rename".Translate()))
                {
                    _showOverwriteConfirm = false;
                }

                if (Widgets.ButtonText(cancelRect, "LandingZone_Cancel".Translate()))
                {
                    Close();
                }
            }
            else
            {
                // Two buttons: Save, Cancel
                float buttonWidth = (buttonRect.width - 10f) / 2f;

                Rect saveRect = new Rect(buttonRect.x, buttonRect.y, buttonWidth, buttonRect.height);
                Rect cancelRect = new Rect(buttonRect.x + buttonWidth + 10f, buttonRect.y, buttonWidth, buttonRect.height);

                if (Widgets.ButtonText(saveRect, "LandingZone_Save".Translate()))
                {
                    TrySavePreset();
                }

                if (Widgets.ButtonText(cancelRect, "LandingZone_Cancel".Translate()))
                {
                    Close();
                }
            }

            listing.End();
        }

        private void TrySavePreset()
        {
            if (string.IsNullOrWhiteSpace(_presetName))
            {
                Messages.Message("LandingZone_PresetNameRequired".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            // Check if preset with this name already exists
            var userPresets = LandingZoneMod.Instance?.Settings?.UserPresets;
            if (userPresets != null && userPresets.Any(p => p.Name.Equals(_presetName, System.StringComparison.OrdinalIgnoreCase)))
            {
                // Show overwrite confirmation
                _showOverwriteConfirm = true;
                SetInitialSizeAndPosition(); // Resize window
                return;
            }

            // No conflict - save directly
            bool saved = PresetLibrary.SaveUserPreset(_presetName, _filters, _activePreset);
            if (saved)
            {
                Messages.Message("LandingZone_PresetSaved".Translate(_presetName), MessageTypeDefOf.PositiveEvent, false);
                Close();
            }
        }

        private void PerformOverwrite()
        {
            // Delete existing preset first
            var userPresets = LandingZoneMod.Instance?.Settings?.UserPresets;
            var existing = userPresets?.FirstOrDefault(p => p.Name.Equals(_presetName, System.StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                PresetLibrary.DeleteUserPreset(existing.Id);
            }

            // Now save the new preset
            bool saved = PresetLibrary.SaveUserPreset(_presetName, _filters, _activePreset);
            if (saved)
            {
                Messages.Message("LandingZone_PresetOverwritten".Translate(_presetName), MessageTypeDefOf.PositiveEvent, false);
                Close();
            }
            else
            {
                Messages.Message("LandingZone_PresetSaveFailed".Translate(), MessageTypeDefOf.RejectInput, false);
            }
        }
    }
}

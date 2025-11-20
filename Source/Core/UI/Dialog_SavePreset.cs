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

        public Dialog_SavePreset(FilterSettings filters, Preset? activePreset = null)
        {
            _filters = filters;
            _activePreset = activePreset;
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnCancel = true;
        }

        public override Vector2 InitialSize => new Vector2(450f, 200f);

        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            Text.Font = GameFont.Medium;
            listing.Label("Save Preset");
            Text.Font = GameFont.Small;
            listing.GapLine();

            listing.Gap(10f);

            listing.Label("Preset Name (max 10 characters):");
            _presetName = listing.TextEntry(_presetName);

            // Enforce 10-character limit
            if (_presetName.Length > 10)
            {
                _presetName = _presetName.Substring(0, 10);
            }

            listing.Gap(20f);

            // Buttons
            Rect buttonRect = listing.GetRect(35f);
            float buttonWidth = (buttonRect.width - 10f) / 2f;

            Rect saveRect = new Rect(buttonRect.x, buttonRect.y, buttonWidth, buttonRect.height);
            Rect cancelRect = new Rect(buttonRect.x + buttonWidth + 10f, buttonRect.y, buttonWidth, buttonRect.height);

            if (Widgets.ButtonText(saveRect, "Save"))
            {
                if (string.IsNullOrWhiteSpace(_presetName))
                {
                    Messages.Message("Please enter a preset name", MessageTypeDefOf.RejectInput, false);
                }
                else
                {
                    bool saved = PresetLibrary.SaveUserPreset(_presetName, _filters, _activePreset);
                    if (saved)
                    {
                        Messages.Message($"Saved preset: {_presetName}", MessageTypeDefOf.PositiveEvent, false);
                        Close();
                    }
                    else
                    {
                        Messages.Message($"Preset name '{_presetName}' already exists. Please choose a different name.", MessageTypeDefOf.RejectInput, false);
                    }
                }
            }

            if (Widgets.ButtonText(cancelRect, "Cancel"))
            {
                Close();
            }

            listing.End();
        }
    }
}

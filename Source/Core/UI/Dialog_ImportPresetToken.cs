using LandingZone.Data;
using RimWorld;
using UnityEngine;
using Verse;

namespace LandingZone.Core.UI
{
    /// <summary>
    /// Dialog for importing a preset from a token string.
    /// User pastes token → validates → optionally renames → saves to user presets.
    /// </summary>
    public class Dialog_ImportPresetToken : Window
    {
        private string _tokenInput = "";
        private string _presetName = "";
        private string? _errorMessage = null;
        private Preset? _decodedPreset = null;

        public Dialog_ImportPresetToken()
        {
            doCloseButton = true;
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            forcePause = true;
        }

        public override Vector2 InitialSize => new Vector2(600f, 400f);

        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            // Header
            Text.Font = GameFont.Medium;
            listing.Label("LandingZone_ImportPresetToken".Translate());
            Text.Font = GameFont.Small;
            listing.GapLine();
            listing.Gap(8f);

            // Instructions
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            listing.Label("LandingZone_ImportTokenInstructions".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            listing.Gap(12f);

            // Token input field (multi-line text area)
            listing.Label("LandingZone_PasteTokenHere".Translate());
            Rect textAreaRect = listing.GetRect(100f);
            _tokenInput = GUI.TextArea(textAreaRect, _tokenInput ?? "");

            listing.Gap(8f);

            // Name input field (optional - rename before import)
            listing.Label("LandingZone_PresetNameOptional".Translate());
            _presetName = listing.TextEntry(_presetName ?? "");

            listing.Gap(12f);

            // Single Import button - decodes and saves in one step
            if (listing.ButtonText("LandingZone_ImportPreset".Translate()))
            {
                // Decode token
                var (preset, error) = PresetTokenCodec.DecodePreset(_tokenInput);
                if (error != null)
                {
                    _errorMessage = error;
                    _decodedPreset = null;
                }
                else
                {
                    _decodedPreset = preset;

                    // Use provided name or keep original
                    string finalName = string.IsNullOrWhiteSpace(_presetName) ? preset.Name : _presetName;

                    // Check for duplicate name
                    var existingPresets = PresetLibrary.GetUserPresets();
                    bool isDuplicate = false;
                    foreach (var existing in existingPresets)
                    {
                        if (existing.Name == finalName)
                        {
                            isDuplicate = true;
                            break;
                        }
                    }

                    if (isDuplicate)
                    {
                        _errorMessage = "LandingZone_PresetNameExists".Translate(finalName);
                    }
                    else
                    {
                        // Save preset preserving all fields (mutator overrides, strictness, etc.)
                        preset.Name = finalName;
                        if (PresetLibrary.SaveUserPreset(preset))
                        {
                            Messages.Message("LandingZone_PresetImported".Translate(finalName), MessageTypeDefOf.PositiveEvent, false);
                            Close();
                        }
                        else
                        {
                            _errorMessage = "LandingZone_PresetImportFailed".Translate();
                        }
                    }
                }
            }

            listing.Gap(12f);

            // Show error message if any
            if (_errorMessage != null)
            {
                GUI.color = new Color(1f, 0.5f, 0.5f);
                Text.Font = GameFont.Tiny;
                listing.Label($"⚠ {_errorMessage}");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }

            // Show preset details if decoded successfully
            if (_decodedPreset != null)
            {
                listing.Gap(8f);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.8f, 0.8f, 0.8f);
                listing.Label($"Preview: {_decodedPreset.Description}");
                if (_decodedPreset.MinimumStrictness.HasValue)
                    listing.Label($"Strictness: {_decodedPreset.MinimumStrictness.Value:F2}");
                if (_decodedPreset.TargetRarity.HasValue)
                    listing.Label($"Target Rarity: {_decodedPreset.TargetRarity.Value}");
                if (_decodedPreset.Filters.MaxResults != FilterSettings.DefaultMaxResults)
                    listing.Label($"Max Results: {_decodedPreset.Filters.MaxResults}");
                if (_decodedPreset.MutatorQualityOverrides?.Count > 0)
                    listing.Label($"Mutator Overrides: {_decodedPreset.MutatorQualityOverrides.Count}");
                if (_decodedPreset.FallbackTiers?.Count > 0)
                    listing.Label($"Fallback Tiers: {_decodedPreset.FallbackTiers.Count}");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }

            listing.End();
        }
    }
}

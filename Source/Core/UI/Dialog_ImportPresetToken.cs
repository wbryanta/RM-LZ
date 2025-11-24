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
        private bool _hasDecoded = false;

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

            // Decode button
            if (listing.ButtonText("LandingZone_DecodeToken".Translate()))
            {
                _hasDecoded = true;
                var (preset, error) = PresetTokenCodec.DecodePreset(_tokenInput);
                if (error != null)
                {
                    _errorMessage = error;
                    _decodedPreset = null;
                }
                else
                {
                    _errorMessage = null;
                    _decodedPreset = preset;
                    _presetName = preset?.Name ?? "";
                }
            }

            listing.Gap(12f);

            // Show error or success message
            if (_hasDecoded)
            {
                if (_errorMessage != null)
                {
                    // Error message
                    GUI.color = new Color(1f, 0.5f, 0.5f);
                    Text.Font = GameFont.Tiny;
                    listing.Label($"⚠ {_errorMessage}");
                    GUI.color = Color.white;
                    Text.Font = GameFont.Small;
                }
                else if (_decodedPreset != null)
                {
                    // Success - show preset details
                    GUI.color = new Color(0.7f, 1f, 0.7f);
                    Text.Font = GameFont.Small;
                    listing.Label("LandingZone_TokenDecodedSuccess".Translate());
                    GUI.color = Color.white;
                    Text.Font = GameFont.Small;

                    listing.Gap(8f);

                    // Show preset details
                    Text.Font = GameFont.Tiny;
                    GUI.color = new Color(0.8f, 0.8f, 0.8f);
                    listing.Label($"Description: {_decodedPreset.Description}");
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

                    listing.Gap(12f);

                    // Name input field (allow renaming before import)
                    listing.Label("LandingZone_PresetNameOptional".Translate());
                    _presetName = listing.TextEntry(_presetName ?? "");

                    listing.Gap(12f);

                    // Import button
                    if (listing.ButtonText("LandingZone_ImportPreset".Translate()))
                    {
                        // Use provided name or keep original
                        string finalName = string.IsNullOrWhiteSpace(_presetName) ? _decodedPreset.Name : _presetName;

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
                            _decodedPreset.Name = finalName;
                            if (PresetLibrary.SaveUserPreset(_decodedPreset))
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
            }

            listing.End();
        }
    }
}

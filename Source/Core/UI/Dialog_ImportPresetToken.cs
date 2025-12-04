#nullable enable
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
        private PresetValidationResult? _validationResult = null;
        private Vector2 _scrollPosition = Vector2.zero;

        public Dialog_ImportPresetToken()
        {
            doCloseButton = true;
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            forcePause = true;
        }

        public override Vector2 InitialSize => new Vector2(600f, 520f);

        public override void DoWindowContents(Rect inRect)
        {
            // Calculate content height to determine if scrolling needed
            float estimatedHeight = 400f; // Base content
            if (_validationResult?.HasMissingItems == true)
                estimatedHeight += 50f + (_validationResult.MissingItems.Count * 20f);
            if (_validationResult?.HasResolvedAliases == true)
                estimatedHeight += 30f + (_validationResult.ResolvedAliases.Count * 20f);
            if (_decodedPreset != null)
                estimatedHeight += 100f;

            var viewRect = new Rect(0f, 0f, inRect.width - 16f, estimatedHeight);
            Widgets.BeginScrollView(inRect, ref _scrollPosition, viewRect);

            var listing = new Listing_Standard();
            listing.Begin(viewRect);

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
                // Decode token with validation
                var (preset, error, validation) = PresetTokenCodec.DecodePreset(_tokenInput);
                _validationResult = validation;

                if (error != null)
                {
                    _errorMessage = error;
                    _decodedPreset = null;
                }
                else if (preset != null)
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
                            // Include validation info in success message if there are issues
                            if (validation?.HasMissingItems == true)
                            {
                                Messages.Message("LandingZone_PresetImportedWithWarnings".Translate(finalName, validation.SkippedCount), MessageTypeDefOf.CautionInput, false);
                            }
                            else
                            {
                                Messages.Message("LandingZone_PresetImported".Translate(finalName), MessageTypeDefOf.PositiveEvent, false);
                            }
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
                listing.Label(_errorMessage);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }

            // Show validation warnings (missing items that will be skipped)
            if (_validationResult?.HasMissingItems == true)
            {
                listing.Gap(8f);
                GUI.color = new Color(1f, 0.85f, 0.5f); // Yellow/orange for warnings
                Text.Font = GameFont.Tiny;
                listing.Label("LandingZone_ImportValidationWarning".Translate(_validationResult.SkippedCount));

                // List missing items by category
                foreach (var item in _validationResult.MissingItems)
                {
                    listing.Label($"  - {item.Category}: {item.DefName} ({item.Resolution})");
                }

                listing.Gap(4f);
                listing.Label("LandingZone_ImportValidationNote".Translate());
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }

            // Show resolved aliases (informational, not warnings)
            if (_validationResult?.HasResolvedAliases == true)
            {
                listing.Gap(4f);
                GUI.color = new Color(0.7f, 0.9f, 0.7f); // Light green for info
                Text.Font = GameFont.Tiny;
                listing.Label("LandingZone_ImportAliasInfo".Translate(_validationResult.ResolvedAliases.Count));

                foreach (var item in _validationResult.ResolvedAliases)
                {
                    listing.Label($"  - {item.DefName} -> {item.Resolution?.Replace("Resolved to ", "")}");
                }
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
            Widgets.EndScrollView();
        }
    }
}

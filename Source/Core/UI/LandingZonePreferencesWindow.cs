using System.Collections.Generic;
using System.Linq;
using LandingZone.Core.Filtering.Filters;
using LandingZone.Data;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LandingZone.Core.UI
{
    public sealed class LandingZonePreferencesWindow : Window
    {
        private const float ScrollbarWidth = 16f;

        // UI state
        private Vector2 _scrollPos;

        // Guided Builder state
        private static PriorityGoal _priority1 = PriorityGoal.None;
        private static PriorityGoal _priority2 = PriorityGoal.None;
        private static PriorityGoal _priority3 = PriorityGoal.None;
        private static PriorityGoal _priority4 = PriorityGoal.None;

        public LandingZonePreferencesWindow()
        {
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            draggable = true;
            onlyOneOfTypeAllowed = true;
            doCloseX = true;
        }

        public override Vector2 InitialSize => new Vector2(960f, 720f); // 50% wider for breathing room

        public override void PreClose()
        {
            // Both Simple and Advanced modes modify FilterSettings directly - no persistence needed
            // Old legacy code persisted local variables which would overwrite direct modifications
        }

        public override void DoWindowContents(Rect inRect)
        {
            var options = LandingZoneContext.State?.Preferences.Options ?? new LandingZoneOptions();
            var currentMode = options.PreferencesUIMode;

            // Mode toggle (3 buttons in single row)
            var modeToggleRect = new Rect(inRect.x, inRect.y, inRect.width, 36f);
            DrawModeToggle(modeToggleRect, ref currentMode);

            // Info/warning banner below mode toggle
            var bannerRect = new Rect(inRect.x, inRect.y + 40f, inRect.width, 24f);
            DrawInfoBanner(bannerRect);

            // Save mode change if toggled
            if (currentMode != options.PreferencesUIMode)
            {
                options.PreferencesUIMode = currentMode;

                // Clear ActivePreset when switching away from Preset Hub
                if (currentMode != UIMode.Simple && LandingZoneContext.State?.Preferences != null)
                {
                    LandingZoneContext.State.Preferences.ActivePreset = null;
                }
            }

            // Content area (below banner)
            var contentRect = new Rect(inRect.x, inRect.y + 70f, inRect.width, inRect.height - 70f);

            // Render appropriate mode UI
            switch (currentMode)
            {
                case UIMode.Simple:
                    DrawSimpleModeContent(contentRect);
                    break;
                case UIMode.GuidedBuilder:
                    DrawGuidedBuilderContent(contentRect);
                    break;
                case UIMode.Advanced:
                    DrawAdvancedModeContent(contentRect);
                    break;
            }
        }

        private void DrawModeToggle(Rect rect, ref UIMode currentMode)
        {
            // Three-tier mode buttons in single row: Preset Hub | Guided Builder | Advanced
            var buttonWidth = 140f;
            var spacing = 12f;
            var totalWidth = (buttonWidth * 3) + (spacing * 2);
            var startX = rect.x + (rect.width - totalWidth) / 2f;

            var presetHubRect = new Rect(startX, rect.y, buttonWidth, 32f);
            var guidedBuilderRect = new Rect(startX + buttonWidth + spacing, rect.y, buttonWidth, 32f);
            var advancedRect = new Rect(startX + (buttonWidth + spacing) * 2, rect.y, buttonWidth, 32f);

            var prevColor = GUI.color;

            // Preset Hub button (Tier 1)
            var isPresetHub = currentMode == UIMode.Simple;
            if (isPresetHub)
            {
                GUI.color = new Color(0.8f, 1f, 0.8f); // Light green tint for active
            }

            if (Widgets.ButtonText(presetHubRect, "Preset Hub"))
            {
                if (!isPresetHub)
                {
                    currentMode = UIMode.Simple;
                }
            }

            GUI.color = prevColor;

            // Guided Builder button (Tier 2)
            var isGuidedBuilder = currentMode == UIMode.GuidedBuilder;
            if (isGuidedBuilder)
            {
                GUI.color = new Color(0.8f, 1f, 0.8f);
            }

            if (Widgets.ButtonText(guidedBuilderRect, "Guided Builder"))
            {
                if (!isGuidedBuilder)
                {
                    currentMode = UIMode.GuidedBuilder;
                }
            }

            GUI.color = prevColor;

            // Advanced button (Tier 3)
            var isAdvanced = currentMode == UIMode.Advanced;
            if (isAdvanced)
            {
                GUI.color = new Color(0.8f, 1f, 0.8f);
            }

            if (Widgets.ButtonText(advancedRect, "Advanced"))
            {
                if (!isAdvanced)
                {
                    currentMode = UIMode.Advanced;
                }
            }

            GUI.color = prevColor;
        }

        private void DrawInfoBanner(Rect rect)
        {
            // Info/warning banner below mode buttons
            // TODO: Swap to warning color when conflicts detected
            var bgColor = new Color(0.15f, 0.15f, 0.15f);
            Widgets.DrawBoxSolid(rect, bgColor);
            Widgets.DrawBox(rect);

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, "Preset Hub: Quick starts | Guided Builder: Goal-based wizard | Advanced: Full control");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        private void DrawSimpleModeContent(Rect contentRect)
        {
            // Tier 1: Preset Hub renderer
            var preferences = LandingZoneContext.State?.Preferences ?? new UserPreferences();

            // Dynamic height: let DefaultModeUI calculate its own content height
            var viewRect = new Rect(0f, 0f, contentRect.width - ScrollbarWidth, 1200f);
            Widgets.BeginScrollView(contentRect, ref _scrollPos, viewRect);

            DefaultModeUI.DrawContent(viewRect, preferences);

            Widgets.EndScrollView();
        }

        private void DrawGuidedBuilderContent(Rect contentRect)
        {
            // Tier 2: Guided Builder - 4-step priority wizard
            var viewRect = new Rect(0f, 0f, contentRect.width - ScrollbarWidth, 800f);
            Widgets.BeginScrollView(contentRect, ref _scrollPos, viewRect);

            var listing = new Listing_Standard { ColumnWidth = viewRect.width };
            listing.Begin(viewRect);

            // Header
            Text.Font = GameFont.Medium;
            listing.Label("Priority-Based Site Finder");
            Text.Font = GameFont.Small;
            listing.GapLine();
            listing.Gap(8f);

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            listing.Label("Select 1-4 goals in priority order. Priority 1 = Critical filters, Priority 2-4 = Preferred filters.");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            listing.Gap(12f);

            // Priority 1 (required)
            DrawPrioritySlot(listing, 1, ref _priority1, true);
            listing.Gap(8f);

            // Priority 2-4 (optional)
            DrawPrioritySlot(listing, 2, ref _priority2, false);
            listing.Gap(8f);
            DrawPrioritySlot(listing, 3, ref _priority3, false);
            listing.Gap(8f);
            DrawPrioritySlot(listing, 4, ref _priority4, false);
            listing.Gap(16f);

            listing.GapLine();
            listing.Gap(16f);

            // Preview of selected goals
            DrawGoalPreview(listing);
            listing.Gap(16f);

            listing.GapLine();
            listing.Gap(16f);

            // Action buttons
            var preferences = LandingZoneContext.State?.Preferences ?? new UserPreferences();
            DrawActionButtons(listing, preferences);

            listing.End();
            Widgets.EndScrollView();
        }

        private void DrawPrioritySlot(Listing_Standard listing, int slotNumber, ref PriorityGoal currentGoal, bool required)
        {
            string slotLabel = required
                ? $"Priority {slotNumber} (Required)"
                : $"Priority {slotNumber} (Optional)";

            Text.Font = GameFont.Small;
            GUI.color = required ? new Color(1f, 0.9f, 0.7f) : Color.white;
            listing.Label(slotLabel);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // Dropdown button
            string buttonLabel = currentGoal == PriorityGoal.None
                ? "Select a goal..."
                : GuidedBuilderGoals.GetGoalName(currentGoal);

            if (listing.ButtonText(buttonLabel))
            {
                var options = new List<FloatMenuOption>();

                // "None" option for optional slots
                if (!required)
                {
                    options.Add(new FloatMenuOption("None", () => {
                        if (slotNumber == 1) _priority1 = PriorityGoal.None;
                        else if (slotNumber == 2) _priority2 = PriorityGoal.None;
                        else if (slotNumber == 3) _priority3 = PriorityGoal.None;
                        else if (slotNumber == 4) _priority4 = PriorityGoal.None;
                    }));
                }

                // Collect already-selected goals from other slots (prevent duplicates)
                var selectedGoals = new HashSet<PriorityGoal>();
                if (slotNumber != 1 && _priority1 != PriorityGoal.None) selectedGoals.Add(_priority1);
                if (slotNumber != 2 && _priority2 != PriorityGoal.None) selectedGoals.Add(_priority2);
                if (slotNumber != 3 && _priority3 != PriorityGoal.None) selectedGoals.Add(_priority3);
                if (slotNumber != 4 && _priority4 != PriorityGoal.None) selectedGoals.Add(_priority4);

                // All available goals (skip already-selected ones)
                foreach (var goal in GuidedBuilderGoals.GetAllGoals())
                {
                    var goalName = GuidedBuilderGoals.GetGoalName(goal);
                    var goalDesc = GuidedBuilderGoals.GetGoalDescription(goal);
                    var goalCapture = goal; // Capture for lambda

                    // Skip if this goal is already selected in another slot
                    if (selectedGoals.Contains(goal))
                    {
                        // Add disabled option to show it's taken
                        options.Add(new FloatMenuOption($"{goalName} (already selected)", null));
                    }
                    else
                    {
                        options.Add(new FloatMenuOption(goalName, () => {
                            if (slotNumber == 1) _priority1 = goalCapture;
                            else if (slotNumber == 2) _priority2 = goalCapture;
                            else if (slotNumber == 3) _priority3 = goalCapture;
                            else if (slotNumber == 4) _priority4 = goalCapture;
                        }));
                    }
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }

            // Show description if goal selected
            if (currentGoal != PriorityGoal.None)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                listing.Label(GuidedBuilderGoals.GetGoalDescription(currentGoal));
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }
        }

        private void DrawGoalPreview(Listing_Standard listing)
        {
            var selectedGoals = new List<(int slot, PriorityGoal goal)>();
            if (_priority1 != PriorityGoal.None) selectedGoals.Add((1, _priority1));
            if (_priority2 != PriorityGoal.None) selectedGoals.Add((2, _priority2));
            if (_priority3 != PriorityGoal.None) selectedGoals.Add((3, _priority3));
            if (_priority4 != PriorityGoal.None) selectedGoals.Add((4, _priority4));

            if (selectedGoals.Count == 0)
            {
                Text.Font = GameFont.Small;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                listing.Label("Select at least Priority 1 to see recommendations.");
                GUI.color = Color.white;
                return;
            }

            Text.Font = GameFont.Small;
            listing.Label("Selected Priorities:");
            Text.Font = GameFont.Tiny;

            foreach (var (slot, goal) in selectedGoals)
            {
                string importance = slot == 1 ? "Critical" : "Preferred";
                GUI.color = slot == 1 ? new Color(1f, 0.7f, 0.7f) : new Color(0.7f, 0.7f, 1f);
                listing.Label($"  {slot}. {GuidedBuilderGoals.GetGoalName(goal)} ({importance})");
            }

            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private void DrawActionButtons(Listing_Standard listing, UserPreferences preferences)
        {
            bool hasValidSelection = _priority1 != PriorityGoal.None;

            // Search Now button
            GUI.enabled = hasValidSelection;
            if (listing.ButtonText("Apply & Search Now"))
            {
                ApplyGuidedBuilderFilters(preferences);
                LandingZoneContext.RequestEvaluation(EvaluationRequestSource.Manual, focusOnComplete: true);
            }
            GUI.enabled = true;

            listing.Gap(8f);

            // Open in Advanced button
            GUI.enabled = hasValidSelection;
            if (listing.ButtonText("Apply & Open in Advanced"))
            {
                ApplyGuidedBuilderFilters(preferences);
                preferences.Options.PreferencesUIMode = UIMode.Advanced;
                Messages.Message("Applied priority-based filters to Advanced mode", MessageTypeDefOf.NeutralEvent, false);
            }
            GUI.enabled = true;

            listing.Gap(8f);

            // Save as Preset button
            GUI.enabled = hasValidSelection;
            if (listing.ButtonText("Save as Custom Preset"))
            {
                var tempFilters = new FilterSettings();
                ApplyGuidedBuilderFilters(preferences, tempFilters);
                Find.WindowStack.Add(new Dialog_SavePreset(tempFilters, null));
            }
            GUI.enabled = true;

            listing.Gap(12f);

            // Reset button
            if (listing.ButtonText("Clear All Priorities"))
            {
                _priority1 = PriorityGoal.None;
                _priority2 = PriorityGoal.None;
                _priority3 = PriorityGoal.None;
                _priority4 = PriorityGoal.None;
            }
        }

        private void ApplyGuidedBuilderFilters(UserPreferences preferences, FilterSettings targetFilters = null)
        {
            var filters = targetFilters ?? preferences.GetActiveFilters();

            // Reset to defaults first
            filters.Reset();

            // Apply each priority in order
            if (_priority1 != PriorityGoal.None)
                GuidedBuilderGoals.ApplyGoalFilters(_priority1, 1, filters);
            if (_priority2 != PriorityGoal.None)
                GuidedBuilderGoals.ApplyGoalFilters(_priority2, 2, filters);
            if (_priority3 != PriorityGoal.None)
                GuidedBuilderGoals.ApplyGoalFilters(_priority3, 3, filters);
            if (_priority4 != PriorityGoal.None)
                GuidedBuilderGoals.ApplyGoalFilters(_priority4, 4, filters);
        }

        private void DrawAdvancedModeContent(Rect contentRect)
        {
            // Use new AdvancedModeUI renderer
            var preferences = LandingZoneContext.State?.Preferences ?? new UserPreferences();

            // Calculate height needed for content + buttons
            var buttonAreaHeight = 80f;
            var viewRect = new Rect(0f, 0f, contentRect.width - ScrollbarWidth, 1800f + buttonAreaHeight);

            Widgets.BeginScrollView(contentRect, ref _scrollPos, viewRect);

            var listing = new Listing_Standard { ColumnWidth = viewRect.width };
            listing.Begin(viewRect);

            // Render Advanced mode filters
            var filterRect = new Rect(0f, 0f, viewRect.width, 1800f);
            AdvancedModeUI.DrawContent(filterRect, preferences);

            // Skip past the filter content
            listing.GetRect(1800f);

            listing.Gap(20f);
            listing.GapLine();

            // Control buttons
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            listing.Label("Changes are saved automatically");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            listing.Gap(4f);

            if (listing.ButtonText("Reset to defaults"))
            {
                preferences.ResetActiveFilters();
                Messages.Message("Filters reset to defaults", MessageTypeDefOf.NeutralEvent, false);
            }

            listing.Gap(8f);

            // Import/Export between modes
            var currentUIMode = preferences.Options.PreferencesUIMode;
            if (currentUIMode == UIMode.Simple)
            {
                if (listing.ButtonText("Copy to Advanced mode"))
                {
                    preferences.CopySimpleToAdvanced();
                    Messages.Message("Simple mode settings copied to Advanced mode", MessageTypeDefOf.NeutralEvent, false);
                }
            }
            else
            {
                if (listing.ButtonText("Copy to Simple mode"))
                {
                    preferences.CopyAdvancedToSimple();
                    Messages.Message("Advanced mode settings copied to Simple mode", MessageTypeDefOf.NeutralEvent, false);
                }
            }

            listing.End();
            Widgets.EndScrollView();
        }
    }
}

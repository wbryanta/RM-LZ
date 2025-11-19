using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld;
using UnityEngine;
using Verse;
using static LandingZone.Core.UI.GoalTemplates;

namespace LandingZone.Core.UI
{
    /// <summary>
    /// Tier 2 (Guided Builder) wizard window.
    /// Walks users through goal-based filter configuration.
    /// </summary>
    public class GuidedBuilderWindow : Window
    {
        private enum WizardStep
        {
            GoalSelection,
            Refinement,
            Preview
        }

        private WizardStep _currentStep = WizardStep.GoalSelection;
        private GoalCategory? _selectedGoal;
        private FoodProductionType _selectedFoodType = FoodProductionType.Mixed;
        private List<FilterRecommendation> _recommendations = new List<FilterRecommendation>();
        private Vector2 _scrollPosition = Vector2.zero;

        public GuidedBuilderWindow()
        {
            doCloseX = true;
            draggable = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(700f, 600f);

        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard { ColumnWidth = inRect.width };
            listing.Begin(inRect);

            // Header with step indicator
            DrawStepIndicator(listing);
            listing.Gap(20f);

            // Main content based on current step
            switch (_currentStep)
            {
                case WizardStep.GoalSelection:
                    DrawGoalSelection(listing);
                    break;
                case WizardStep.Refinement:
                    DrawRefinement(listing);
                    break;
                case WizardStep.Preview:
                    DrawPreview(listing, inRect);
                    break;
            }

            listing.End();
        }

        private void DrawStepIndicator(Listing_Standard listing)
        {
            Text.Font = GameFont.Medium;
            string stepText = _currentStep switch
            {
                WizardStep.GoalSelection => "Step 1: Choose Your Goal",
                WizardStep.Refinement => "Step 2: Refine Your Goal",
                WizardStep.Preview => "Step 3: Review Filters",
                _ => "Guided Builder"
            };
            listing.Label(stepText);
            Text.Font = GameFont.Small;
            listing.GapLine();
        }

        private void DrawGoalSelection(Listing_Standard listing)
        {
            listing.Label("What's your priority for this colony?");
            listing.Gap(12f);

            // 2-column grid of goal cards
            var goals = new[]
            {
                GoalCategory.ClimateComfort,
                GoalCategory.ResourceWealth,
                GoalCategory.Defensibility,
                GoalCategory.FoodProduction,
                GoalCategory.PowerGeneration,
                GoalCategory.TradeAccess,
                GoalCategory.ChallengeRarity,
                GoalCategory.SpecificFeature
            };

            const float cardWidth = 320f;
            const float cardHeight = 80f;
            const float cardGap = 12f;

            for (int i = 0; i < goals.Length; i += 2)
            {
                var rowRect = listing.GetRect(cardHeight);

                // Left card
                DrawGoalCard(
                    new Rect(rowRect.x, rowRect.y, cardWidth, cardHeight),
                    goals[i]
                );

                // Right card (if exists)
                if (i + 1 < goals.Length)
                {
                    DrawGoalCard(
                        new Rect(rowRect.x + cardWidth + cardGap, rowRect.y, cardWidth, cardHeight),
                        goals[i + 1]
                    );
                }

                listing.Gap(cardGap);
            }

            // Back button
            listing.Gap(20f);
            if (listing.ButtonText("← Back to Preferences"))
            {
                Close();
            }
        }

        private void DrawGoalCard(Rect rect, GoalCategory goal)
        {
            bool isSelected = _selectedGoal == goal;

            // Background
            Color bgColor = isSelected ? new Color(0.3f, 0.4f, 0.3f) : new Color(0.15f, 0.15f, 0.15f);
            Widgets.DrawBoxSolid(rect, bgColor);
            Widgets.DrawBox(rect);

            var contentRect = rect.ContractedBy(8f);

            // Label
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.Label(
                new Rect(contentRect.x, contentRect.y, contentRect.width, 22f),
                GetGoalLabel(goal)
            );

            // Description
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.Label(
                new Rect(contentRect.x, contentRect.y + 24f, contentRect.width, 48f),
                GetGoalDescription(goal)
            );
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // Click handler
            if (Widgets.ButtonInvisible(rect))
            {
                _selectedGoal = goal;

                // Check if this goal needs refinement
                if (goal == GoalCategory.FoodProduction)
                {
                    _currentStep = WizardStep.Refinement;
                }
                else if (goal == GoalCategory.SpecificFeature)
                {
                    // Specific feature just opens Advanced mode
                    Messages.Message("Opening Advanced mode for custom feature selection...", MessageTypeDefOf.NeutralEvent, false);
                    Close();
                    var prefs = LandingZoneContext.State?.Preferences;
                    if (prefs != null)
                    {
                        prefs.Options.PreferencesUIMode = UIMode.Advanced;
                    }
                }
                else
                {
                    // Generate recommendations and go to preview
                    _recommendations = GetRecommendationsForGoal(goal);
                    _currentStep = WizardStep.Preview;
                }
            }

            // Tooltip
            TooltipHandler.TipRegion(rect, $"{GetGoalLabel(goal)}\n\n{GetGoalDescription(goal)}");
        }

        private void DrawRefinement(Listing_Standard listing)
        {
            if (_selectedGoal == GoalCategory.FoodProduction)
            {
                listing.Label("What type of food production?");
                listing.Gap(12f);

                var foodTypes = new[]
                {
                    (FoodProductionType.Farming, "Farming", "Long growing season, fertile soil, consistent rainfall"),
                    (FoodProductionType.Hunting, "Hunting", "Abundant wildlife, high animal density"),
                    (FoodProductionType.Fishing, "Fishing", "Coastal + rivers, high fish population"),
                    (FoodProductionType.Mixed, "Mixed", "Balance of farming, hunting, and fishing")
                };

                foreach (var (type, label, description) in foodTypes)
                {
                    var buttonRect = listing.GetRect(50f);
                    bool isSelected = _selectedFoodType == type;

                    Color bgColor = isSelected ? new Color(0.3f, 0.4f, 0.3f) : new Color(0.15f, 0.15f, 0.15f);
                    Widgets.DrawBoxSolid(buttonRect, bgColor);
                    Widgets.DrawBox(buttonRect);

                    var contentRect = buttonRect.ContractedBy(8f);
                    Text.Font = GameFont.Small;
                    Widgets.Label(new Rect(contentRect.x, contentRect.y, contentRect.width, 18f), label);
                    Text.Font = GameFont.Tiny;
                    GUI.color = new Color(0.8f, 0.8f, 0.8f);
                    Widgets.Label(new Rect(contentRect.x, contentRect.y + 20f, contentRect.width, 24f), description);
                    GUI.color = Color.white;
                    Text.Font = GameFont.Small;

                    if (Widgets.ButtonInvisible(buttonRect))
                    {
                        _selectedFoodType = type;
                    }

                    listing.Gap(8f);
                }

                listing.Gap(20f);

                // Navigation buttons
                if (listing.ButtonText("← Back"))
                {
                    _currentStep = WizardStep.GoalSelection;
                }

                listing.Gap(8f);

                if (listing.ButtonText("Continue →"))
                {
                    _recommendations = GetRecommendationsForGoal(GoalCategory.FoodProduction, _selectedFoodType);
                    _currentStep = WizardStep.Preview;
                }
            }
        }

        private void DrawPreview(Listing_Standard listing, Rect inRect)
        {
            var goalLabel = _selectedGoal.HasValue ? GetGoalLabel(_selectedGoal.Value) : "Unknown";
            listing.Label($"Recommended Filters ({goalLabel}):");
            listing.Gap(8f);

            // Scrollable filter preview
            var scrollViewRect = new Rect(0f, listing.CurHeight, inRect.width, inRect.height - listing.CurHeight - 100f);
            var contentRect = new Rect(0f, 0f, scrollViewRect.width - 20f, _recommendations.Count * 90f);

            Widgets.BeginScrollView(scrollViewRect, ref _scrollPosition, contentRect);

            float y = 0f;
            foreach (var rec in _recommendations)
            {
                DrawFilterRecommendation(new Rect(0f, y, contentRect.width, 80f), rec);
                y += 90f;
            }

            Widgets.EndScrollView();

            // Skip past scroll view
            listing.GetRect(scrollViewRect.height);
            listing.Gap(12f);

            // Action buttons
            var buttonRect = listing.GetRect(35f);
            float buttonWidth = (buttonRect.width - 8f) / 2f;

            // Tweak in Advanced button
            if (Widgets.ButtonText(new Rect(buttonRect.x, buttonRect.y, buttonWidth, buttonRect.height), "Tweak in Advanced"))
            {
                ApplyRecommendations(LandingZoneContext.State?.Preferences?.AdvancedFilters);
                var prefs = LandingZoneContext.State?.Preferences;
                if (prefs != null)
                {
                    prefs.Options.PreferencesUIMode = UIMode.Advanced;
                }
                Messages.Message($"Loaded '{goalLabel}' filters into Advanced mode for editing", MessageTypeDefOf.NeutralEvent, false);
                Close();
            }

            // Search Now button
            if (Widgets.ButtonText(new Rect(buttonRect.x + buttonWidth + 8f, buttonRect.y, buttonWidth, buttonRect.height), "Search Now"))
            {
                ApplyRecommendations(LandingZoneContext.State?.Preferences?.GetActiveFilters());
                LandingZoneContext.RequestEvaluation(EvaluationRequestSource.Manual, focusOnComplete: true);
                Close();
            }

            listing.Gap(8f);

            if (listing.ButtonText("← Back"))
            {
                if (_selectedGoal == GoalCategory.FoodProduction)
                    _currentStep = WizardStep.Refinement;
                else
                    _currentStep = WizardStep.GoalSelection;
            }
        }

        private void DrawFilterRecommendation(Rect rect, FilterRecommendation rec)
        {
            var bgColor = rec.Importance == FilterImportance.Critical
                ? new Color(0.25f, 0.15f, 0.15f)
                : new Color(0.15f, 0.15f, 0.25f);

            Widgets.DrawBoxSolid(rect, bgColor);
            Widgets.DrawBox(rect);

            var contentRect = rect.ContractedBy(8f);

            // Importance badge
            string badge = rec.Importance == FilterImportance.Critical ? "🔴 Critical" : "🔵 Preferred";
            var badgeRect = new Rect(contentRect.x, contentRect.y, 100f, 18f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(badgeRect, badge);

            // Filter name
            Text.Font = GameFont.Small;
            var nameRect = new Rect(contentRect.x, contentRect.y + 20f, contentRect.width, 22f);
            Widgets.Label(nameRect, rec.FilterName);

            // Explanation
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            var explainRect = new Rect(contentRect.x, contentRect.y + 44f, contentRect.width, 32f);
            Widgets.Label(explainRect, rec.Explanation);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private void ApplyRecommendations(FilterSettings filters)
        {
            if (filters == null) return;

            foreach (var rec in _recommendations)
            {
                rec.ApplyAction(filters);
            }
        }
    }
}

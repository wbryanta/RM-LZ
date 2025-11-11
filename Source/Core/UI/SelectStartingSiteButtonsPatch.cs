using System;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using LandingZone.Core;
using Verse.Sound;

namespace LandingZone.Core.UI
{
    [HarmonyPatch(typeof(Page_SelectStartingSite), "DoCustomBottomButtons")]
    internal static class SelectStartingSiteButtonsPatch
    {
        public static bool Prefix(Page_SelectStartingSite __instance)
        {
            LandingZoneBottomButtonDrawer.Draw(__instance);
            return false;
        }
    }

    /// <summary>
    /// Patches DoWindowContents to tick the evaluation job every frame.
    /// This allows background evaluation to work on the world selection screen
    /// where GameComponent.GameComponentTick() doesn't run.
    /// </summary>
    [HarmonyPatch(typeof(Page_SelectStartingSite), "DoWindowContents")]
    internal static class SelectStartingSiteDoWindowContentsPatch
    {
        public static void Postfix()
        {
            // Tick the background evaluation job if one is active
            LandingZoneContext.StepEvaluation();
        }
    }

    internal static class LandingZoneBottomButtonDrawer
    {
        private const float Gap = 10f;
        private static readonly Vector2 ButtonSize = new Vector2(150f, 38f);
        private static readonly Func<Page, bool> CanDoBack = AccessTools.MethodDelegate<Func<Page, bool>>(AccessTools.Method(typeof(Page), "CanDoBack"));
        private static readonly Action<Page> DoBack = AccessTools.MethodDelegate<Action<Page>>(AccessTools.Method(typeof(Page), "DoBack"));
        private static readonly Func<Page_SelectStartingSite, bool> CanDoNext = AccessTools.MethodDelegate<Func<Page_SelectStartingSite, bool>>(AccessTools.Method(typeof(Page_SelectStartingSite), "CanDoNext"));
        private static readonly Action<Page_SelectStartingSite> DoNext = AccessTools.MethodDelegate<Action<Page_SelectStartingSite>>(AccessTools.Method(typeof(Page_SelectStartingSite), "DoNext"));

        public static void Draw(Page_SelectStartingSite page)
        {
            const int primaryButtons = 4; // Back, Random, Factions, Next
            float width = ButtonSize.x * primaryButtons + Gap * (primaryButtons + 1);
            float height = ButtonSize.y * 2f + Gap * 4f + 16f;
            Rect rect = new Rect(((float)Verse.UI.screenWidth - width) / 2f, (float)Verse.UI.screenHeight - height - 4f, width, height);

            WorldInspectPane inspectPane = Find.WindowStack.WindowOfType<WorldInspectPane>();
            if (inspectPane != null)
            {
                float paneWidth = InspectPaneUtility.PaneWidthFor(inspectPane) + 4f;
                if (rect.x < paneWidth)
                {
                    rect.x = paneWidth;
                }
            }

            Widgets.DrawWindowBackground(rect);
            Text.Font = GameFont.Small;

            float cursorX = rect.xMin + Gap;
            float cursorY = rect.yMin + Gap;

            DrawBackButton(page, new Rect(cursorX, cursorY, ButtonSize.x, ButtonSize.y));
            cursorX += ButtonSize.x + Gap;
            DrawRandomSiteButton(new Rect(cursorX, cursorY, ButtonSize.x, ButtonSize.y));
            cursorX += ButtonSize.x + Gap;
            DrawFactionsButton(new Rect(cursorX, cursorY, ButtonSize.x, ButtonSize.y));
            cursorX += ButtonSize.x + Gap;
            DrawNextButton(page, new Rect(cursorX, cursorY, ButtonSize.x, ButtonSize.y));

            var highlightRow = new Rect(rect.xMin + Gap, cursorY + ButtonSize.y + Gap, width - Gap * 2f, ButtonSize.y);
            DrawHighlightRow(highlightRow);

            var statusRect = new Rect(highlightRow.x, highlightRow.yMax + 2f, highlightRow.width, 16f);
            DrawEvaluationStatus(statusRect);

            GenUI.AbsorbClicksInRect(rect);
        }

        private static void DrawBackButton(Page_SelectStartingSite page, Rect rect)
        {
            bool clicked = Widgets.ButtonText(rect, "Back".Translate());
            bool cancelKey = KeyBindingDefOf.Cancel.KeyDownEvent;
            if ((clicked || cancelKey) && CanDoBack(page))
            {
                DoBack(page);
            }
        }

        private static void DrawRandomSiteButton(Rect rect)
        {
            if (!Widgets.ButtonText(rect, "SelectRandomSite".Translate()))
                return;

            SoundDefOf.Click.PlayOneShotOnCamera();
            if (ModsConfig.OdysseyActive && Rand.Bool)
            {
                Find.WorldInterface.SelectedTile = TileFinder.RandomSettlementTileFor(Find.WorldGrid.Surface, Faction.OfPlayer, mustBeAutoChoosable: true, (PlanetTile x) => x.Tile.Landmark != null);
            }
            else
            {
                Find.WorldInterface.SelectedTile = TileFinder.RandomStartingTile();
            }

            Find.WorldCameraDriver.JumpTo(Find.WorldGrid.GetTileCenter(Find.WorldInterface.SelectedTile));
        }

        private static void DrawHighlightRow(Rect rect)
        {
            var highlightState = LandingZoneContext.HighlightState;
            bool isShowing = highlightState?.ShowBestSites ?? false;
            const float navWidth = 44f;
            const float gearWidth = 38f;
            const float innerGap = 6f;
            var leftNavRect = new Rect(rect.x, rect.y, navWidth, rect.height);
            var rightNavRect = new Rect(rect.xMax - navWidth, rect.y, navWidth, rect.height);
            var gearRect = new Rect(rightNavRect.x - gearWidth - innerGap, rect.y, gearWidth, rect.height);
            float buttonWidth = Mathf.Max(80f, gearRect.x - innerGap - (leftNavRect.xMax + innerGap));
            var buttonRect = new Rect(leftNavRect.xMax + innerGap, rect.y, buttonWidth, rect.height);

            var prevColor = GUI.color;
            GUI.color = isShowing ? new Color(0.55f, 0.85f, 0.55f) : Color.white;
            if (Widgets.ButtonText(buttonRect, "Landing Zone"))
            {
                if (highlightState != null)
                {
                    // Always run a new search when button clicked - allows users to update filters and re-search
                    highlightState.ShowBestSites = true;
                    LandingZoneContext.RequestEvaluation(EvaluationRequestSource.ShowBestSites, focusOnComplete: true);
                }
            }
            GUI.color = prevColor;
            TooltipHandler.TipRegion(buttonRect, "Click to search for best landing sites based on current filters");

            if (Widgets.ButtonImage(gearRect, TexButton.OpenInspectSettings))
            {
                TogglePreferencesWindow();
            }
            TooltipHandler.TipRegion(gearRect, "Adjust LandingZone filters");

            DrawMatchNavigation(leftNavRect, rightNavRect);
        }

        private static void DrawEvaluationStatus(Rect rect)
        {
            var statusRect = new Rect(rect.x, rect.y, rect.width, rect.height);
            var prevFont = Text.Font;
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            string status = LandingZoneContext.IsEvaluating
                ? $"Searching... {(LandingZoneContext.EvaluationProgress * 100f):F0}%"
                : (LandingZoneContext.LastEvaluationCount > 0
                    ? $"{LandingZoneContext.LastEvaluationCount} matches | {LandingZoneContext.LastEvaluationMs:F0} ms"
                    : "No matches yet");
            Widgets.Label(statusRect, status);
            GUI.color = Color.white;
            Text.Font = prevFont;
        }

        private static void DrawMatchNavigation(Rect leftRect, Rect rightRect)
        {
            if (!LandingZoneContext.HasMatches || LandingZoneContext.IsEvaluating)
                return;

            if (Widgets.ButtonText(leftRect, "<"))
            {
                LandingZoneContext.FocusNextMatch(-1);
            }
            if (Widgets.ButtonText(rightRect, ">"))
            {
                LandingZoneContext.FocusNextMatch(1);
            }
        }

        private static void DrawFactionsButton(Rect rect)
        {
            if (Widgets.ButtonText(rect, "WorldFactionsTab".Translate()))
            {
                Find.WindowStack.Add(new Dialog_FactionDuringLanding());
            }
        }

        private static void TogglePreferencesWindow()
        {
            var existing = Find.WindowStack.WindowOfType<LandingZonePreferencesWindow>();
            if (existing != null)
            {
                existing.Close();
            }
            else
            {
                Find.WindowStack.Add(new LandingZonePreferencesWindow());
            }
        }

        private static void DrawNextButton(Page_SelectStartingSite page, Rect rect)
        {
            if (Widgets.ButtonText(rect, "Next".Translate()) && CanDoNext(page))
            {
                DoNext(page);
            }
        }

    }
}

using System.Collections.Generic;
using System.Linq;
using LandingZone.Core.Filtering;
using LandingZone.Data;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LandingZone.Core.UI
{
    public sealed class LandingZoneResultsWindow : Window
    {
        private const float RowHeight = 76f;
        private Vector2 _scroll;

        public LandingZoneResultsWindow()
        {
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            draggable = true;
            doCloseButton = false;
            doCloseX = true;
        }

        public override Vector2 InitialSize => new Vector2(520f, 560f);

        public override void PostClose()
        {
            LandingZoneResultsController.NotifyClosed(this);
        }

        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.Label($"Landing Zone Results ({LandingZoneMod.Version})");
            listing.GapLine();

            var highlightState = LandingZoneContext.HighlightState;
            bool showing = highlightState?.ShowBestSites ?? false;
            if (listing.ButtonText(showing ? "Hide highlights" : "Show highlights"))
            {
                if (highlightState != null)
                {
                    highlightState.ShowBestSites = !showing;
                    if (highlightState.ShowBestSites && !LandingZoneContext.HasMatches)
                    {
                        LandingZoneContext.RequestEvaluation(EvaluationRequestSource.ShowBestSites, focusOnComplete: true);
                    }
                }
            }

            listing.Gap(6f);
            if (LandingZoneContext.IsEvaluating)
            {
                listing.Label($"Searching... {(LandingZoneContext.EvaluationProgress * 100f):F0}%");
            }
            else
            {
                listing.Label(LandingZoneContext.LastEvaluationCount > 0
                    ? $"{LandingZoneContext.LastEvaluationCount} matches in {LandingZoneContext.LastEvaluationMs:F0} ms"
                    : "No active match data. Run a search to populate this list.");
            }

            float headerHeight = listing.CurHeight;
            listing.End();

            var listRect = new Rect(inRect.x, inRect.y + headerHeight, inRect.width, inRect.height - headerHeight);
            DrawMatches(listRect);
        }

        private void DrawMatches(Rect rect)
        {
            var matches = LandingZoneContext.LatestResults;
            if (matches.Count == 0)
            {
                var label = LandingZoneContext.IsEvaluating
                    ? $"Searching... {(LandingZoneContext.EvaluationProgress * 100f):F0}% complete."
                    : "Once LandingZone finishes a search the ranked sites will appear here.";
                Widgets.Label(rect.ContractedBy(6f), label);
                return;
            }

            var viewRect = new Rect(0f, 0f, rect.width - 16f, matches.Count * (RowHeight + 6f));
            Widgets.BeginScrollView(rect, ref _scroll, viewRect);
            float curY = 0f;
            for (int i = 0; i < matches.Count; i++)
            {
                var rowRect = new Rect(0f, curY, viewRect.width, RowHeight);
                DrawMatchRow(rowRect, matches[i], i);
                curY += RowHeight + 6f;
            }
            Widgets.EndScrollView();
        }

        private void DrawMatchRow(Rect rect, TileScore score, int index)
        {
            Widgets.DrawHighlightIfMouseover(rect);
            WorldSnapshot.TileInfo tileInfo = default;
            var snapshot = LandingZoneContext.State?.WorldSnapshot;
            bool hasInfo = snapshot != null && snapshot.TryGetInfo(score.TileId, out tileInfo);
            string biomeLabel = hasInfo && tileInfo.Biome != null ? tileInfo.Biome.LabelCap : "Unknown biome";
            string header = $"#{index + 1} • {(score.Score * 100f):F0}% match • Tile {score.TileId} • {biomeLabel}";
            Widgets.Label(new Rect(rect.x, rect.y, rect.width - 80f, 24f), header);

            var focusRect = new Rect(rect.xMax - 70f, rect.y, 70f, 24f);
            if (Widgets.ButtonText(focusRect, "Focus"))
            {
                LandingZoneContext.FocusTile(score.TileId);
            }

            float lineY = rect.y + 26f;
            var statsRect = new Rect(rect.x, lineY, rect.width, 20f);
            Widgets.Label(statsRect, BuildStatLine(score.TileId, hasInfo ? tileInfo : (WorldSnapshot.TileInfo?)null));
            lineY += 20f;

            var matchedRect = new Rect(rect.x, lineY, rect.width, 18f);
            Widgets.Label(matchedRect, $"Matched: {string.Join(", ", BuildMatchTags(score.Breakdown, true))}");
            lineY += 18f;

            var missingRect = new Rect(rect.x, lineY, rect.width, 18f);
            Widgets.Label(missingRect, $"Missing: {string.Join(", ", BuildMatchTags(score.Breakdown, false))}");
        }

        private static string BuildStatLine(int tileId, WorldSnapshot.TileInfo? basicInfo)
        {
            if (basicInfo == null)
                return "No snapshot data";

            // Fetch extended properties from cache
            var extended = LandingZoneContext.Filters?.TileCache.GetOrCompute(tileId) ?? default;

            List<string> stats = new List<string>();
            stats.Add($"{extended.GrowingDays:F0} growing days");
            stats.Add($"{extended.Forageability * 100f:F0}% forage");
            stats.Add(basicInfo.Value.Hilliness.GetLabelCap());
            if (extended.StoneDefNames != null && extended.StoneDefNames.Length > 0)
            {
                stats.Add(string.Join("/", extended.StoneDefNames));
            }

            return string.Join(" • ", stats);
        }

        private static IEnumerable<string> BuildMatchTags(MatchBreakdown breakdown, bool matched)
        {
            var tags = new List<string>();

            AppendTag(tags, "Coastal", breakdown.CoastalImportance, breakdown.HasCoastal, matched);
            AppendTag(tags, "River", breakdown.RiverImportance, breakdown.HasRiver, matched);
            AppendTag(tags, "Graze", breakdown.GrazeImportance, breakdown.CanGraze, matched);
            if (breakdown.FeatureImportance != FilterImportance.Ignored && !string.IsNullOrEmpty(breakdown.RequiredFeature))
            {
                AppendTag(tags, breakdown.RequiredFeature!, breakdown.FeatureImportance, breakdown.FeatureMatched, matched);
            }
            if (breakdown.RequiredStoneCount > 0 && breakdown.StoneImportance != FilterImportance.Ignored)
            {
                bool satisfied = breakdown.StoneMatches >= breakdown.RequiredStoneCount;
                if (matched && satisfied)
                {
                    tags.Add($"Stone mix {breakdown.StoneMatches}/{breakdown.RequiredStoneCount}");
                }
                else if (!matched && !satisfied)
                {
                    tags.Add($"Stone mix {breakdown.StoneMatches}/{breakdown.RequiredStoneCount}");
                }
            }
            AppendSimpleTag(tags, breakdown.HillinessAllowed, matched, "Terrain");

            if (matched && tags.Count == 0)
                tags.Add("None");
            if (!matched && tags.Count == 0)
                tags.Add("None");
            return tags;
        }

        private static void AppendTag(List<string> tags, string label, FilterImportance importance, bool satisfied, bool targetMatched)
        {
            if (importance == FilterImportance.Ignored)
                return;

            if (targetMatched && satisfied)
            {
                tags.Add($"{label} {ImportanceIcon(importance)}");
            }
            else if (!targetMatched && !satisfied)
            {
                tags.Add($"{label} ✗");
            }
        }

        private static void AppendSimpleTag(List<string> tags, bool satisfied, bool targetMatched, string label)
        {
            if (targetMatched && satisfied)
            {
                tags.Add(label);
            }
            else if (!targetMatched && !satisfied)
            {
                tags.Add(label);
            }
        }

        private static string ImportanceIcon(FilterImportance importance)
        {
            return importance switch
            {
                FilterImportance.Critical => "!",
                FilterImportance.Preferred => "✓",
                _ => "X"
            };
        }
    }
}

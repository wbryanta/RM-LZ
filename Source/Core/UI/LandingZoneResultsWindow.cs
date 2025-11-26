using System;
using System.Collections.Generic;
using System.Linq;
using LandingZone.Core.Filtering;
using LandingZone.Core.Filtering.Filters;
using LandingZone.Data;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LandingZone.Core.UI
{
    public sealed class LandingZoneResultsWindow : Window
    {
        private const float MinRowHeight = 80f; // Minimum card height (header + biome)
        private Vector2 _scroll;
        private SortMode _sortMode = SortMode.Score;
        private float _minScoreFilter = 0f;

        // Collapsible section state per tile
        private Dictionary<int, bool> _matchedExpanded = new Dictionary<int, bool>();
        private Dictionary<int, bool> _missedExpanded = new Dictionary<int, bool>();
        private Dictionary<int, bool> _modifiersExpanded = new Dictionary<int, bool>();

        private enum SortMode
        {
            Score,
            GrowingDays,
            Temperature,
            BiomeName
        }

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
            listing.Label("LandingZone_ResultsTitle".Translate(LandingZoneMod.Version));
            listing.GapLine();

            var highlightState = LandingZoneContext.HighlightState;
            bool showing = highlightState?.ShowBestSites ?? false;

            Rect highlightButtonRect = listing.GetRect(30f);
            if (Widgets.ButtonText(highlightButtonRect, (showing ? "LandingZone_HideHighlights" : "LandingZone_ShowHighlights").Translate()))
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

            // Tooltip explaining highlight functionality
            TooltipHandler.TipRegion(highlightButtonRect, "LandingZone_HighlightsTooltip".Translate());

            listing.Gap(4f);
            if (LandingZoneContext.IsEvaluating)
            {
                listing.Label("LandingZone_SearchingEllipsis".Translate((LandingZoneContext.EvaluationProgress * 100f).ToString("F0")));
            }
            else
            {
                listing.Label(LandingZoneContext.LastEvaluationCount > 0
                    ? "LandingZone_SearchComplete".Translate(LandingZoneContext.LastEvaluationCount, LandingZoneContext.LastEvaluationMs.ToString("F0"))
                    : "LandingZone_NoSearchData".Translate());

                // Add summary statistics when results available
                if (LandingZoneContext.LastEvaluationCount > 0 && LandingZoneContext.LatestResults.Count > 0)
                {
                    listing.Gap(2f);
                    DrawSummaryStatistics(listing);
                }
            }

            float headerHeight = listing.CurHeight;
            listing.End();

            // Draw toolbar with sorting and filtering controls
            float toolbarY = inRect.y + headerHeight;
            float toolbarHeight = LandingZoneContext.LatestResults.Count > 0 ? 26f : 0f;
            if (toolbarHeight > 0)
            {
                var toolbarRect = new Rect(inRect.x, toolbarY, inRect.width, toolbarHeight);
                DrawToolbar(toolbarRect);
            }

            var listRect = new Rect(inRect.x, toolbarY + toolbarHeight, inRect.width, inRect.height - headerHeight - toolbarHeight);
            DrawMatches(listRect);
        }

        private void DrawToolbar(Rect rect)
        {
            var prevFont = Text.Font;
            Text.Font = GameFont.Tiny;

            // Sort dropdown
            var sortLabelRect = new Rect(rect.x + 4f, rect.y + 2f, 40f, rect.height - 4f);
            Widgets.Label(sortLabelRect, "LandingZone_SortLabel".Translate());

            var sortButtonRect = new Rect(sortLabelRect.xMax + 3f, rect.y + 2f, 90f, rect.height - 4f);
            if (Widgets.ButtonText(sortButtonRect, GetSortModeLabel(_sortMode)))
            {
                var sortOptions = new List<FloatMenuOption>
                {
                    new FloatMenuOption("LandingZone_SortByScore".Translate(), () => _sortMode = SortMode.Score),
                    new FloatMenuOption("LandingZone_SortByGrowingDays".Translate(), () => _sortMode = SortMode.GrowingDays),
                    new FloatMenuOption("LandingZone_SortByTemperature".Translate(), () => _sortMode = SortMode.Temperature),
                    new FloatMenuOption("LandingZone_SortByBiomeName".Translate(), () => _sortMode = SortMode.BiomeName)
                };
                Find.WindowStack.Add(new FloatMenu(sortOptions));
            }

            // Quick filter buttons
            var filterX = sortButtonRect.xMax + 10f;
            var filterLabelRect = new Rect(filterX, rect.y + 2f, 35f, rect.height - 4f);
            Widgets.Label(filterLabelRect, "LandingZone_ShowLabel".Translate());

            var buttonWidth = 55f;
            var buttonGap = 3f;
            filterX = filterLabelRect.xMax + 3f;

            var allRect = new Rect(filterX, rect.y + 2f, buttonWidth, rect.height - 4f);
            DrawFilterButton(allRect, "LandingZone_ShowAll".Translate(), 0f);

            filterX += buttonWidth + buttonGap;
            var excellentRect = new Rect(filterX, rect.y + 2f, buttonWidth, rect.height - 4f);
            DrawFilterButton(excellentRect, "LandingZone_Show90Plus".Translate(), 0.90f);

            filterX += buttonWidth + buttonGap;
            var goodRect = new Rect(filterX, rect.y + 2f, buttonWidth, rect.height - 4f);
            DrawFilterButton(goodRect, "LandingZone_Show75Plus".Translate(), 0.75f);

            // Right-side buttons
            float rightX = rect.xMax - 4f;

            // Bookmark All button
            var bookmarkAllWidth = 85f;
            var bookmarkAllRect = new Rect(rightX - bookmarkAllWidth, rect.y + 2f, bookmarkAllWidth, rect.height - 4f);
            if (Widgets.ButtonText(bookmarkAllRect, "LandingZone_BookmarkAll".Translate()))
            {
                BookmarkAllResults();
            }

            // Note: Debug dump moved to DevToolsWindow for cleaner UI

            Text.Font = prevFont;
        }

        private void DrawFilterButton(Rect rect, string label, float threshold)
        {
            bool isActive = Mathf.Approximately(_minScoreFilter, threshold);
            var prevColor = GUI.color;
            if (isActive)
            {
                GUI.color = new Color(0.6f, 0.9f, 0.6f);
            }
            if (Widgets.ButtonText(rect, label))
            {
                _minScoreFilter = threshold;
            }
            GUI.color = prevColor;
        }

        private static string GetSortModeLabel(SortMode mode)
        {
            return mode switch
            {
                SortMode.Score => "LandingZone_SortByScore".Translate(),
                SortMode.GrowingDays => "LandingZone_SortByGrowingDays".Translate(),
                SortMode.Temperature => "LandingZone_SortByTemperature".Translate(),
                SortMode.BiomeName => "LandingZone_SortByBiomeName".Translate(),
                _ => "LandingZone_SortByScore".Translate()
            };
        }

        private static void DrawSummaryStatistics(Listing_Standard listing)
        {
            var matches = LandingZoneContext.LatestResults;
            if (matches.Count == 0) return;

            int excellent = 0, good = 0, acceptable = 0, poor = 0;
            float totalScore = 0f;
            float topScore = 0f;

            foreach (var match in matches)
            {
                totalScore += match.Score;
                if (match.Score > topScore) topScore = match.Score;

                if (match.Score >= 0.90f) excellent++;
                else if (match.Score >= 0.75f) good++;
                else if (match.Score >= 0.60f) acceptable++;
                else poor++;
            }

            float avgScore = totalScore / matches.Count;

            var prevFont = Text.Font;
            Text.Font = GameFont.Tiny;

            var summaryText = "LandingZone_SummaryTop".Translate((topScore * 100f).ToString("F0")) + " | " + "LandingZone_SummaryAverage".Translate((avgScore * 100f).ToString("F0")) + " | ";
            if (excellent > 0) summaryText += "LandingZone_SummaryExcellent".Translate(excellent) + "  ";
            if (good > 0) summaryText += "LandingZone_SummaryGood".Translate(good) + "  ";
            if (acceptable > 0) summaryText += "LandingZone_SummaryAcceptable".Translate(acceptable) + "  ";
            if (poor > 0) summaryText += "LandingZone_SummaryPoor".Translate(poor);

            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            listing.Label(summaryText);
            GUI.color = Color.white;

            Text.Font = prevFont;
        }

        private void DrawMatches(Rect rect)
        {
            var allMatches = LandingZoneContext.LatestResults;
            if (allMatches.Count == 0)
            {
                var label = LandingZoneContext.IsEvaluating
                    ? "LandingZone_SearchingProgress".Translate((LandingZoneContext.EvaluationProgress * 100f).ToString("F0"))
                    : "LandingZone_WaitingForSearch".Translate();
                Widgets.Label(rect.ContractedBy(6f), label);
                return;
            }

            // Apply filtering and sorting
            var matches = GetFilteredAndSortedMatches(allMatches);

            // Calculate dynamic card heights
            var cardHeights = new float[matches.Count];
            float totalHeight = 0f;
            for (int i = 0; i < matches.Count; i++)
            {
                cardHeights[i] = CalculateCardHeight(matches[i]);
                totalHeight += cardHeights[i] + 4f; // +4 for spacing
            }

            var viewRect = new Rect(0f, 0f, rect.width - 16f, totalHeight);
            Widgets.BeginScrollView(rect, ref _scroll, viewRect);
            float curY = 0f;
            for (int i = 0; i < matches.Count; i++)
            {
                var rowRect = new Rect(0f, curY, viewRect.width, cardHeights[i]);
                DrawMatchRow(rowRect, matches[i], i);
                curY += cardHeights[i] + 4f;
            }
            Widgets.EndScrollView();
        }

        private List<TileScore> GetFilteredAndSortedMatches(IReadOnlyList<TileScore> allMatches)
        {
            // Apply score filter
            var filtered = allMatches.Where(m => m.Score >= _minScoreFilter).ToList();

            // Apply sorting
            switch (_sortMode)
            {
                case SortMode.Score:
                    filtered.Sort((a, b) => b.Score.CompareTo(a.Score)); // Descending
                    break;
                case SortMode.GrowingDays:
                    filtered.Sort((a, b) =>
                    {
                        var extA = LandingZoneContext.Filters?.TileCache.GetOrCompute(a.TileId) ?? default;
                        var extB = LandingZoneContext.Filters?.TileCache.GetOrCompute(b.TileId) ?? default;
                        return extB.GrowingDays.CompareTo(extA.GrowingDays); // Descending
                    });
                    break;
                case SortMode.Temperature:
                    filtered.Sort((a, b) =>
                    {
                        var worldGrid = Find.World?.grid;
                        if (worldGrid == null) return 0;
                        var tileA = worldGrid[a.TileId];
                        var tileB = worldGrid[b.TileId];
                        float tempA = tileA?.temperature ?? 0f;
                        float tempB = tileB?.temperature ?? 0f;
                        return tempB.CompareTo(tempA); // Descending
                    });
                    break;
                case SortMode.BiomeName:
                    filtered.Sort((a, b) =>
                    {
                        var worldGrid = Find.World?.grid;
                        if (worldGrid == null) return 0;
                        var tileA = worldGrid[a.TileId];
                        var tileB = worldGrid[b.TileId];
                        string nameA = tileA?.PrimaryBiome != null ? tileA.PrimaryBiome.LabelCap.ToString() : "";
                        string nameB = tileB?.PrimaryBiome != null ? tileB.PrimaryBiome.LabelCap.ToString() : "";
                        return string.Compare(nameA, nameB, StringComparison.Ordinal);
                    });
                    break;
            }

            return filtered;
        }

        private static (float min, float max, float avg) GetSeasonalTempRange(int tileId)
        {
            var tile = Find.WorldGrid[tileId];
            float winterTemp = GenTemperature.AverageTemperatureAtTileForTwelfth(tileId, (Twelfth)0); // Twelfth 0 = winter
            float summerTemp = GenTemperature.AverageTemperatureAtTileForTwelfth(tileId, (Twelfth)6); // Twelfth 6 = summer
            float avg = tile.temperature;

            // Ensure min/max are correct
            float min = Mathf.Min(winterTemp, summerTemp);
            float max = Mathf.Max(winterTemp, summerTemp);

            return (min, max, avg);
        }

        private static string GetForageQualityLabel(float forageability)
        {
            if (forageability >= 0.8f) return "LandingZone_ForageExcellent".Translate();
            if (forageability >= 0.5f) return "LandingZone_ForageGood".Translate();
            if (forageability >= 0.2f) return "LandingZone_ForagePoor".Translate();
            return "LandingZone_ForageMinimal".Translate();
        }

        private static Color GetMetricMatchColor(float value, FloatRange range, FilterImportance importance)
        {
            if (importance == FilterImportance.Ignored)
                return new Color(0.85f, 0.85f, 0.85f); // Default grey

            bool matches = value >= range.min && value <= range.max;
            if (!matches)
                return new Color(0.85f, 0.85f, 0.85f); // Grey for non-match

            // Matched - return color based on importance
            if (importance == FilterImportance.Critical)
                return new Color(0.29f, 0.95f, 0.29f); // Bright green #4af34a
            else // Preferred
                return new Color(0.77f, 0.82f, 0.29f); // Yellow-green #c4d14a
        }

        private static Color GetBooleanMatchColor(bool hasFeature, FilterImportance importance)
        {
            if (importance == FilterImportance.Ignored)
                return new Color(0.85f, 0.85f, 0.85f); // Default grey

            // For boolean features, only color if matches (has the feature)
            if (!hasFeature)
                return new Color(0.85f, 0.85f, 0.85f); // Grey for non-match

            // Matched
            if (importance == FilterImportance.Critical)
                return new Color(0.29f, 0.95f, 0.29f); // Bright green
            else // Preferred
                return new Color(0.77f, 0.82f, 0.29f); // Yellow-green
        }

        private static void DrawColoredClimateLine(Rect rect, int tileId, RimWorld.Planet.Tile tile, TileInfoExtended extended, FilterSettings? filters)
        {
            Text.Font = GameFont.Tiny;
            float x = rect.x;
            float y = rect.y;

            if (filters == null)
            {
                // Fallback to grey if no filters
                GUI.color = new Color(0.85f, 0.85f, 0.85f);
                Widgets.Label(rect, "LandingZone_NoFilterData".Translate());
                return;
            }

            var tempRange = GetSeasonalTempRange(tileId);
            float maxGrowingDays = 60f; // Default to 60 for now - could calculate actual max

            // Growing days: "XX/YYd grow"
            string growText = "LandingZone_GrowingDaysLabel".Translate(extended.GrowingDays.ToString("F0"), maxGrowingDays.ToString("F0"));
            GUI.color = GetMetricMatchColor(extended.GrowingDays, filters.GrowingDaysRange, filters.GrowingDaysImportance);
            var growRect = new Rect(x, y, Text.CalcSize(growText).x, rect.height);
            Widgets.Label(growRect, growText);
            x += growRect.width;

            // Separator
            string sep1 = " • ";
            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            var sep1Rect = new Rect(x, y, Text.CalcSize(sep1).x, rect.height);
            Widgets.Label(sep1Rect, sep1);
            x += sep1Rect.width;

            // Temperature: "XX.X°F (min°F to max°F)"
            string tempText = LandingZoneMod.UseFahrenheit
                ? "LandingZone_TemperatureFahrenheit".Translate(
                    GenTemperature.CelsiusTo(tempRange.avg, TemperatureDisplayMode.Fahrenheit).ToString("F1"),
                    GenTemperature.CelsiusTo(tempRange.min, TemperatureDisplayMode.Fahrenheit).ToString("F0"),
                    GenTemperature.CelsiusTo(tempRange.max, TemperatureDisplayMode.Fahrenheit).ToString("F0"))
                : "LandingZone_TemperatureCelsius".Translate(
                    tempRange.avg.ToString("F1"),
                    tempRange.min.ToString("F0"),
                    tempRange.max.ToString("F0"));
            GUI.color = GetMetricMatchColor(tile.temperature, filters.AverageTemperatureRange, filters.AverageTemperatureImportance);
            var tempRect = new Rect(x, y, Text.CalcSize(tempText).x, rect.height);
            Widgets.Label(tempRect, tempText);
            x += tempRect.width;

            // Separator
            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            var sep2Rect = new Rect(x, y, Text.CalcSize(sep1).x, rect.height);
            Widgets.Label(sep2Rect, sep1);
            x += sep2Rect.width;

            // Rainfall: "XXXXmm rain"
            string rainText = "LandingZone_RainfallLabel".Translate(tile.rainfall.ToString("F0"));
            GUI.color = GetMetricMatchColor(tile.rainfall, filters.RainfallRange, filters.RainfallImportance);
            var rainRect = new Rect(x, y, Text.CalcSize(rainText).x, rect.height);
            Widgets.Label(rainRect, rainText);
            x += rainRect.width;

            // Separator
            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            var sep3Rect = new Rect(x, y, Text.CalcSize(sep1).x, rect.height);
            Widgets.Label(sep3Rect, sep1);
            x += sep3Rect.width;

            // Forage: "forage: XX% (food type)"
            string forageFood = GetPrimaryForageFood(tile?.PrimaryBiome) ?? "LandingZone_ForageNone".Translate();
            string forageText = "LandingZone_ForageLabel".Translate((extended.Forageability * 100f).ToString("F0"), forageFood);
            GUI.color = GetMetricMatchColor(extended.Forageability, filters.ForageabilityRange, filters.ForageImportance);
            var forageRect = new Rect(x, y, Text.CalcSize(forageText).x, rect.height);
            Widgets.Label(forageRect, forageText);
        }

        private static void DrawFailureLine(Rect rect, int tileId, RimWorld.Planet.Tile tile, TileInfoExtended extended, FilterSettings? filters)
        {
            if (filters == null) return;

            var failures = new List<(string text, Color color)>();

            // Check temperature
            if (filters.AverageTemperatureImportance != FilterImportance.Ignored)
            {
                if (tile.temperature < filters.AverageTemperatureRange.min || tile.temperature > filters.AverageTemperatureRange.max)
                {
                    var tempRange = GetSeasonalTempRange(tileId);
                    string tempText = LandingZoneMod.UseFahrenheit
                        ? "LandingZone_MismatchTemp".Translate(
                            GenTemperature.CelsiusTo(tempRange.avg, TemperatureDisplayMode.Fahrenheit).ToString("F1") + "°F",
                            GenTemperature.CelsiusTo(filters.AverageTemperatureRange.min, TemperatureDisplayMode.Fahrenheit).ToString("F0") + "-" + GenTemperature.CelsiusTo(filters.AverageTemperatureRange.max, TemperatureDisplayMode.Fahrenheit).ToString("F0") + "°F")
                        : "LandingZone_MismatchTemp".Translate(
                            tempRange.avg.ToString("F1") + "°C",
                            filters.AverageTemperatureRange.min.ToString("F0") + "-" + filters.AverageTemperatureRange.max.ToString("F0") + "°C");
                    Color color = filters.AverageTemperatureImportance == FilterImportance.Critical
                        ? new Color(1f, 0.27f, 0.27f) // Bright red #ff4444
                        : new Color(1f, 0.67f, 0.27f); // Orange-yellow #ffaa44
                    failures.Add((tempText, color));
                }
            }

            // Check rainfall
            if (filters.RainfallImportance != FilterImportance.Ignored)
            {
                if (tile.rainfall < filters.RainfallRange.min || tile.rainfall > filters.RainfallRange.max)
                {
                    string rainText = "LandingZone_MismatchRain".Translate(tile.rainfall.ToString("F0"), filters.RainfallRange.min.ToString("F0"), filters.RainfallRange.max.ToString("F0"));
                    Color color = filters.RainfallImportance == FilterImportance.Critical
                        ? new Color(1f, 0.27f, 0.27f)
                        : new Color(1f, 0.67f, 0.27f);
                    failures.Add((rainText, color));
                }
            }

            // Check growing days
            if (filters.GrowingDaysImportance != FilterImportance.Ignored)
            {
                if (extended.GrowingDays < filters.GrowingDaysRange.min || extended.GrowingDays > filters.GrowingDaysRange.max)
                {
                    string growText = "LandingZone_MismatchGrow".Translate(extended.GrowingDays.ToString("F0"), filters.GrowingDaysRange.min.ToString("F0"), filters.GrowingDaysRange.max.ToString("F0"));
                    Color color = filters.GrowingDaysImportance == FilterImportance.Critical
                        ? new Color(1f, 0.27f, 0.27f)
                        : new Color(1f, 0.67f, 0.27f);
                    failures.Add((growText, color));
                }
            }

            // Check forage
            if (filters.ForageImportance != FilterImportance.Ignored)
            {
                if (extended.Forageability < filters.ForageabilityRange.min || extended.Forageability > filters.ForageabilityRange.max)
                {
                    string forageText = "LandingZone_MismatchForage".Translate((extended.Forageability * 100f).ToString("F0"), (filters.ForageabilityRange.min * 100f).ToString("F0"), (filters.ForageabilityRange.max * 100f).ToString("F0"));
                    Color color = filters.ForageImportance == FilterImportance.Critical
                        ? new Color(1f, 0.27f, 0.27f)
                        : new Color(1f, 0.67f, 0.27f);
                    failures.Add((forageText, color));
                }
            }

            // Check coastal
            if (filters.CoastalImportance != FilterImportance.Ignored && !tile.IsCoastal)
            {
                string coastText = "LandingZone_CoastalNoRequired".Translate();
                Color color = filters.CoastalImportance == FilterImportance.Critical
                    ? new Color(1f, 0.27f, 0.27f)
                    : new Color(1f, 0.67f, 0.27f);
                failures.Add((coastText, color));
            }

            // Draw failure line if there are failures
            if (failures.Count > 0)
            {
                Text.Font = GameFont.Tiny;
                float x = rect.x;
                float y = rect.y;

                for (int i = 0; i < failures.Count; i++)
                {
                    var (text, color) = failures[i];
                    GUI.color = color;
                    var textRect = new Rect(x, y, Text.CalcSize(text).x, rect.height);
                    Widgets.Label(textRect, text);
                    x += textRect.width;

                    if (i < failures.Count - 1)
                    {
                        string sep = " • ";
                        GUI.color = new Color(0.85f, 0.85f, 0.85f);
                        var sepRect = new Rect(x, y, Text.CalcSize(sep).x, rect.height);
                        Widgets.Label(sepRect, sep);
                        x += sepRect.width;
                    }
                }
            }
        }

        /// <summary>
        /// Calculates the dynamic height needed for a card based on its content and expand/collapse state.
        /// Uses precise measurements that match actual rendering.
        /// </summary>
        private float CalculateCardHeight(TileScore score)
        {
            const float baseHeight = 80f; // Header + score + biome
            float height = baseHeight;

            if (!score.BreakdownV2.HasValue)
                return Math.Max(MinRowHeight, 140f); // Fallback for old display

            var breakdown = score.BreakdownV2.Value;

            // Check if sections are expanded (match default expansion logic)
            bool matchedExpanded = !_matchedExpanded.ContainsKey(score.TileId)
                ? breakdown.MatchedFilters.Count <= 8  // Default: expanded if <=8 items
                : _matchedExpanded[score.TileId];

            bool missedExpanded = !_missedExpanded.ContainsKey(score.TileId)
                ? true  // Default: always expanded for missed
                : _missedExpanded[score.TileId];

            bool modifiersExpanded = _modifiersExpanded.ContainsKey(score.TileId)
                ? _modifiersExpanded[score.TileId]
                : false;  // Default: collapsed for modifiers

            // Matched section (two-column layout)
            if (breakdown.MatchedFilters.Count > 0)
            {
                height += 20f; // Section header (18f) + gap (2f)
                if (matchedExpanded)
                {
                    // Two-column layout: rows = ceiling(count / 2)
                    int totalRows = (breakdown.MatchedFilters.Count + 1) / 2;
                    height += totalRows * 16f; // 16f per row
                    height += 4f; // Bottom padding
                }
            }

            // Missed section (single-column list with icons and tags)
            if (breakdown.MissedFilters.Count > 0)
            {
                height += 20f; // Section header (18f) + gap (2f)
                if (missedExpanded)
                {
                    height += breakdown.MissedFilters.Count * 18f; // 18f per missed filter (16f line + 2f spacing)
                }
            }

            // Modifiers section (single-column list)
            if (breakdown.Mutators.Count > 0)
            {
                height += 20f; // Section header (18f) + gap (2f)
                if (modifiersExpanded)
                {
                    height += breakdown.Mutators.Count * 18f; // 18f per mutator
                }
            }

            return Math.Max(MinRowHeight, height);
        }

        private void DrawMatchRow(Rect rect, TileScore score, int index)
        {
            // Check if this is a perfect match
            bool isPerfectMatch = score.BreakdownV2?.IsPerfectMatch ?? false;
            bool isPerfectPlus = score.BreakdownV2?.IsPerfectPlus ?? false;

            // Draw score-tier colored background
            var bgColor = GetScoreTierColor(score.Score);
            var bgRect = rect.ContractedBy(1f);
            Widgets.DrawBoxSolid(bgRect, new Color(bgColor.r, bgColor.g, bgColor.b, 0.15f));

            // Draw border for perfect matches (silver for 1.0, gold for >1.0)
            // Contract by 8px to create visual buffer between content and border
            if (isPerfectMatch)
            {
                Color borderColor = isPerfectPlus
                    ? new Color(1f, 0.843f, 0f)    // Gold #FFD700 for perfect+
                    : new Color(0.75f, 0.75f, 0.75f); // Silver #C0C0C0 for perfect

                var borderRect = rect.ContractedBy(4f); // 4px buffer on each side
                var prevColor = GUI.color;
                GUI.color = borderColor;
                Widgets.DrawBox(borderRect, 2);
                GUI.color = prevColor;

                // Draw label in bottom-right corner (inside border buffer)
                Text.Font = GameFont.Tiny;
                string label = isPerfectPlus ? "LandingZone_TierPerfectPlus".Translate() : "LandingZone_TierPerfect".Translate();
                Vector2 labelSize = Text.CalcSize(label);
                var labelBgRect = new Rect(borderRect.xMax - labelSize.x - 8f, borderRect.yMax - labelSize.y - 4f, labelSize.x + 6f, labelSize.y + 2f);
                Widgets.DrawBoxSolid(labelBgRect, borderColor);

                var labelRect = new Rect(labelBgRect.x + 3f, labelBgRect.y, labelSize.x, labelSize.y);
                GUI.color = Color.black;
                Widgets.Label(labelRect, label);
                GUI.color = Color.white;
            }

            Widgets.DrawHighlightIfMouseover(rect);
            var worldGrid = Find.World?.grid;
            var tile = worldGrid?[score.TileId];
            bool hasInfo = tile != null;
            string biomeLabel = tile?.PrimaryBiome?.LabelCap ?? "LandingZone_UnknownBiome".Translate();

            // Line 1: Rank and tile ID (with trophy if perfect, star if rare)
            const float leftPad = 8f;
            const float rightPad = 8f;
            var rankRect = new Rect(rect.x + leftPad, rect.y + 6f, rect.width - 120f, 16f);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);

            // Rarity detection: Rare if total results < 10 and this is in top 5
            var totalResults = LandingZoneContext.LatestResults.Count;
            bool isRare = totalResults < 10 && (index + 1) <= 5;

            string rankLabel = "LandingZone_RankLabel".Translate(index + 1, score.TileId);
            if (isPerfectMatch) rankLabel += " 🏆";
            if (isRare && !isPerfectMatch) rankLabel += " ⭐"; // Star for rare finds

            Widgets.Label(rankRect, rankLabel);
            GUI.color = Color.white;

            // Bookmark icon (top right, before Focus) - direct icon like toolbar
            var manager = Current.Game?.GetComponent<BookmarkManager>();
            bool isBookmarked = manager?.IsBookmarked(score.TileId) ?? false;
            string bookmarkIcon = isBookmarked ? "★" : "☆";
            var bookmarkRect = new Rect(rect.xMax - 118f, rect.y + 3f, 20f, 18f);

            // Draw star directly (not as button) with color coding
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            var bookmarkPrevColor = GUI.color;
            GUI.color = isBookmarked ? new Color(1f, 0.843f, 0f) : new Color(0.7f, 0.7f, 0.7f); // Gold when bookmarked, gray when not
            Widgets.Label(bookmarkRect, bookmarkIcon);
            GUI.color = bookmarkPrevColor;
            Text.Anchor = TextAnchor.UpperLeft;

            // Invisible button for click detection
            if (Widgets.ButtonInvisible(bookmarkRect))
            {
                if (manager != null)
                {
                    if (isBookmarked)
                    {
                        manager.RemoveBookmark(score.TileId);
                    }
                    else
                    {
                        Color color = GetBookmarkColorByRank(index + 1);
                        manager.AddBookmark(score.TileId, color, "LandingZone_RankBookmarkLabel".Translate(index + 1));
                    }
                }
            }

            // Focus button (top right)
            var focusRect = new Rect(rect.xMax - 62f, rect.y + 3f, 54f, 18f);
            if (Widgets.ButtonText(focusRect, "LandingZone_Focus".Translate()))
            {
                LandingZoneContext.FocusTile(score.TileId);
            }

            // Line 2: Raw score with tier label (not percentage)
            var scoreRect = new Rect(rect.x + leftPad, rect.y + 26f, rect.width - leftPad - rightPad, 20f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = bgColor;
            string tierLabel = GetScoreTierLabel(score.Score, score.BreakdownV2);
            Widgets.Label(scoreRect, "LandingZone_ScoreLabel".Translate(score.Score.ToString("F3"), tierLabel));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            // Line 3: Biome name with rarity badge
            var biomeRect = new Rect(rect.x + leftPad, rect.y + 50f, rect.width - leftPad - rightPad - 60f, 16f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(biomeRect, biomeLabel);

            // Rarity badge (right side of biome line)
            var (probability, rarity) = RarityCalculator.ComputeTileRarity(score.TileId);
            if (rarity >= TileRarity.Rare) // Only show badge for Rare and above
            {
                var rarityBadgeRect = new Rect(rect.xMax - 58f - rightPad, rect.y + 48f, 58f, 18f);
                var rarityColor = rarity.ToColor();
                var rarityLabel = rarity.ToBadgeLabel();  // Use compact label to prevent wrapping

                Widgets.DrawBoxSolid(rarityBadgeRect, rarityColor * 0.6f);
                Widgets.DrawBox(rarityBadgeRect);

                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.white;
                Widgets.Label(rarityBadgeRect, rarityLabel);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            float curY = rect.y + 70f;

            // NEW: Use detailed breakdown if available
            if (score.BreakdownV2.HasValue)
            {
                var breakdown = score.BreakdownV2.Value;
                var contentRect = new Rect(rect.x, curY, rect.width, rect.height - (curY - rect.y));

                // Draw three sections: Matched, Missed, Modifiers (all collapsible)
                DrawMatchedSection(contentRect, breakdown.MatchedFilters, score.TileId, ref curY);
                DrawMissedSection(contentRect, breakdown.MissedFilters, score.TileId, ref curY);
                DrawModifiersSection(contentRect, breakdown.Mutators, breakdown.MutatorScore, score.TileId, ref curY);
            }
            else
            {
                // Fallback: Old display (climate, terrain, failures)
                Text.Font = GameFont.Tiny;

                if (hasInfo)
                {
                    var extended = LandingZoneContext.Filters?.TileCache.GetOrCompute(score.TileId) ?? default;
                    var filters = LandingZoneContext.State?.Preferences?.GetActiveFilters();

                    // Climate line - draw colored segments
                    DrawColoredClimateLine(new Rect(rect.x + 4f, curY, rect.width - 8f, 16f), score.TileId, tile, extended, filters);
                    curY += 20f;

                    // Terrain line
                    string terrainLine = tile.hilliness.GetLabelCap().ToString();
                    // Get stone types directly (cheap: 0.0014ms/tile)
                    var world = Find.World;
                    var planetTile = new PlanetTile(score.TileId, world.grid.Surface);
                    var stoneTypes = world.NaturalRockTypesIn(planetTile)?
                        .Where(t => t != null)
                        .Select(t => t.defName)
                        .ToArray();
                    if (stoneTypes != null && stoneTypes.Length > 0)
                    {
                        terrainLine += " • " + string.Join("/", stoneTypes);
                    }
                    GUI.color = new Color(0.85f, 0.85f, 0.85f);
                    var terrainRect = new Rect(rect.x + 4f, curY, rect.width - 8f, 16f);
                    Widgets.Label(terrainRect, terrainLine);
                    curY += 20f;

                    // Failure line (only show if there are failures)
                    DrawFailureLine(new Rect(rect.x + 4f, curY, rect.width - 8f, 16f), score.TileId, tile, extended, filters);
                    curY += 20f;
                }
                else
                {
                    GUI.color = new Color(0.85f, 0.85f, 0.85f);
                    Widgets.Label(new Rect(rect.x + 4f, curY, rect.width - 8f, 16f), "LandingZone_NoSnapshotData".Translate());
                    curY += 20f;
                }
            }

            GUI.color = Color.white;
            Text.Font = GameFont.Medium;
        }

        private static string FormatTemp(float tempC)
        {
            return LandingZoneMod.UseFahrenheit
                ? $"{GenTemperature.CelsiusTo(tempC, TemperatureDisplayMode.Fahrenheit):F0}°F"
                : $"{tempC:F0}°C";
        }

        private static Color GetScoreTierColor(float score)
        {
            if (score >= 0.90f) return new Color(0.3f, 0.9f, 0.3f); // Excellent - bright green
            if (score >= 0.75f) return new Color(0.3f, 0.85f, 0.9f); // Good - cyan
            if (score >= 0.60f) return new Color(0.95f, 0.9f, 0.3f); // Acceptable - yellow
            return new Color(1.0f, 0.6f, 0.2f); // Poor - orange
        }

        private static string GetScoreTierLabel(float score, MatchBreakdownV2? breakdown)
        {
            // If we have breakdown data, use it to distinguish perfect cases
            if (breakdown.HasValue)
            {
                var b = breakdown.Value;

                // Perfect+ = all criticals/preferreds matched perfectly AND modifiers pushed score > 1.0
                if (b.IsPerfectPlus)
                    return "LandingZone_TierPerfectPlus".Translate();

                // Perfect = all criticals/preferreds matched perfectly AND final score >= 1.0
                if (b.IsPerfectMatch && score >= 1.0f)
                    return "LandingZone_TierPerfect".Translate();

                // Perfect- = all criticals/preferreds matched perfectly BUT modifiers/penalty dragged below 1.0
                if (b.IsPerfectMatch && score < 1.0f)
                    return "LandingZone_TierPerfectMinus".Translate();
            }

            // Fallback to numeric thresholds (for tiles without breakdown, or imperfect matches)
            if (score >= 0.95f) return "LandingZone_TierNearPerfect".Translate();
            if (score >= 0.80f) return "LandingZone_TierGood".Translate();
            return "LandingZone_TierFair".Translate();
        }

        /// <summary>
        /// Gets the most common forageable food type from a biome's wild plants.
        /// Returns the label of the harvested food (e.g., "berries", "agave fruit").
        /// </summary>
        private static string? GetPrimaryForageFood(BiomeDef? biome)
        {
            if (biome?.wildPlants == null || biome.wildPlants.Count == 0)
                return null;

            // Find the most common wild plant that produces forageable food
            ThingDef? primaryFood = null;
            float highestCommonality = 0f;

            foreach (var plantRecord in biome.wildPlants)
            {
                var plant = plantRecord?.plant;
                if (plant?.plant?.harvestedThingDef != null &&
                    plant.plant.harvestedThingDef.IsNutritionGivingIngestible)
                {
                    if (plantRecord.commonality > highestCommonality)
                    {
                        highestCommonality = plantRecord.commonality;
                        primaryFood = plant.plant.harvestedThingDef;
                    }
                }
            }

            return primaryFood?.label;
        }

        // ===== Phase 5: Bookmark integration =====

        /// <summary>
        /// Bookmarks all results with auto-color coding by rank
        /// </summary>
        private void BookmarkAllResults()
        {
            var manager = Current.Game?.GetComponent<BookmarkManager>();
            if (manager == null)
            {
                Messages.Message("LandingZone_BookmarkNotAvailable".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            var matches = LandingZoneContext.LatestResults;
            if (matches.Count == 0) return;

            // Check if ALL results are already bookmarked (toggle behavior)
            int alreadyBookmarked = 0;
            for (int i = 0; i < matches.Count && i < 20; i++)
            {
                if (manager.IsBookmarked(matches[i].TileId))
                    alreadyBookmarked++;
            }

            int resultsToProcess = Math.Min(matches.Count, 20);
            bool removeMode = alreadyBookmarked == resultsToProcess;

            if (removeMode)
            {
                // Remove all bookmarked results
                int removed = 0;
                for (int i = 0; i < resultsToProcess; i++)
                {
                    if (manager.RemoveBookmark(matches[i].TileId))
                        removed++;
                }

                Messages.Message("LandingZone_RemovedBookmarks".Translate(removed), MessageTypeDefOf.SilentInput, false);
            }
            else
            {
                // Add missing bookmarks
                int bookmarked = 0;
                int skipped = 0;

                for (int i = 0; i < resultsToProcess; i++)
                {
                    int rank = i + 1;
                    var match = matches[i];
                    Color color = GetBookmarkColorByRank(rank);
                    string label = "LandingZone_RankBookmarkLabel".Translate(rank);

                    if (manager.AddBookmark(match.TileId, color, label))
                        bookmarked++;
                    else
                        skipped++;
                }

                if (bookmarked > 0)
                {
                    Messages.Message("LandingZone_BookmarkedResults".Translate(bookmarked), MessageTypeDefOf.SilentInput, false);
                }

                if (skipped > 0)
                {
                    Messages.Message("LandingZone_AlreadyBookmarked".Translate(skipped), MessageTypeDefOf.RejectInput, false);
                }
            }
        }

        // ===== Phase 3: Helper methods for new breakdown UI =====

        /// <summary>
        /// Gets bookmark color based on rank (1-3: Red, 4-10: Orange, 11-20: Yellow, 21+: Green)
        /// </summary>
        private static Color GetBookmarkColorByRank(int rank)
        {
            if (rank <= 3) return BookmarkColors.Red;
            if (rank <= 10) return BookmarkColors.Orange;
            if (rank <= 20) return BookmarkColors.Yellow;
            return BookmarkColors.Green;
        }

        /// <summary>
        /// Converts filter ID to user-friendly display name.
        /// For multi-select filters (map_features, river, road, stones):
        /// - If isMatched=true: shows specific items present on the tile
        /// - If isMatched=false: shows what the USER configured (for missed filters)
        /// </summary>
        private static string FormatFilterDisplayName(string filterId, int tileId, bool isMatched = true)
        {
            var filters = LandingZoneContext.State?.Preferences?.GetActiveFilters();

            // Handle multi-select filters
            switch (filterId.ToLowerInvariant())
            {
                case "map_features":
                    if (filters?.MapFeatures.HasAnyImportance == true)
                    {
                        if (isMatched)
                        {
                            // MATCHED: Show specific features on this tile with friendly labels
                            var tileFeatures = MapFeatureFilter.GetTileMapFeatures(tileId).ToList();
                            var selectedFeatures = tileFeatures.Where(f => filters.MapFeatures.GetImportance(f) != FilterImportance.Ignored).ToList();
                            if (selectedFeatures.Any())
                            {
                                var friendlyLabels = selectedFeatures.Select(MapFeatureFilter.GetMutatorFriendlyLabel);
                                return string.Join(", ", friendlyLabels);
                            }
                        }
                        else
                        {
                            // MISSED: Show what user configured (critical/preferred features) with friendly labels
                            var criticalFeatures = filters.MapFeatures.GetCriticalItems().ToList();
                            var preferredFeatures = filters.MapFeatures.GetPreferredItems().ToList();
                            var allConfigured = criticalFeatures.Concat(preferredFeatures).ToList();
                            if (allConfigured.Any())
                            {
                                var friendlyLabels = allConfigured.Select(MapFeatureFilter.GetMutatorFriendlyLabel);
                                return string.Join(", ", friendlyLabels);
                            }
                        }
                    }
                    return "LandingZone_MapFeatures".Translate();

                case "river":
                    // Show specific river on this tile (e.g., "Large River" instead of "River")
                    if (filters?.Rivers.HasAnyImportance == true)
                    {
                        var tile = Find.World?.grid?[tileId];
                        if (tile?.Rivers != null && tile.Rivers.Count > 0)
                        {
                            // Get river defNames from tile (deduplicate - same river can have multiple links)
                            var tileRivers = tile.Rivers
                                .Select(r => r.river?.defName)
                                .Where(n => n != null)
                                .Distinct()
                                .ToList();
                            var selectedRivers = tileRivers.Where(r => filters.Rivers.GetImportance(r) != FilterImportance.Ignored).ToList();
                            if (selectedRivers.Any())
                            {
                                // Convert defNames to display labels and title-case
                                var riverLabels = selectedRivers.Select(defName =>
                                {
                                    var riverDef = DefDatabase<RiverDef>.GetNamedSilentFail(defName);
                                    string label = riverDef?.label ?? defName;
                                    return GenText.ToTitleCaseSmart(label);
                                }).Distinct().ToList(); // Deduplicate again after conversion
                                return string.Join(", ", riverLabels);
                            }
                        }
                    }
                    return "LandingZone_River".Translate();

                case "road":
                    // Show specific road on this tile (e.g., "Ancient Asphalt Road" instead of "Road")
                    if (filters?.Roads.HasAnyImportance == true)
                    {
                        var tile = Find.World?.grid?[tileId];
                        if (tile?.Roads != null && tile.Roads.Count > 0)
                        {
                            // Get road defNames from tile (deduplicate - same road can have multiple links)
                            var tileRoads = tile.Roads
                                .Select(r => r.road?.defName)
                                .Where(n => n != null)
                                .Distinct()
                                .ToList();
                            var selectedRoads = tileRoads.Where(r => filters.Roads.GetImportance(r) != FilterImportance.Ignored).ToList();
                            if (selectedRoads.Any())
                            {
                                // Convert defNames to display labels and title-case
                                var roadLabels = selectedRoads.Select(defName =>
                                {
                                    var roadDef = DefDatabase<RoadDef>.GetNamedSilentFail(defName);
                                    string label = roadDef?.label ?? defName;
                                    return GenText.ToTitleCaseSmart(label);
                                }).Distinct().ToList(); // Deduplicate again after conversion
                                return string.Join(", ", roadLabels);
                            }
                        }
                    }
                    return "LandingZone_Road".Translate();

                case "specificstone" or "individualstone" or "stones":
                    // Show specific stones on this tile (e.g., "Granite, Marble" instead of "Stones")
                    if (filters?.Stones.HasAnyImportance == true)
                    {
                        // Get stone types directly (cheap: 0.0014ms/tile)
                        var world = Find.World;
                        var planetTile = new PlanetTile(tileId, world.grid.Surface);
                        var stoneTypes = world.NaturalRockTypesIn(planetTile)?
                            .Where(t => t != null)
                            .Select(t => t.defName)
                            .ToArray();
                        if (stoneTypes != null && stoneTypes.Length > 0)
                        {
                            var selectedStones = stoneTypes.Where(s => filters.Stones.GetImportance(s) != FilterImportance.Ignored).ToList();
                            if (selectedStones.Any())
                                return string.Join(", ", selectedStones);
                        }
                    }
                    return "LandingZone_Stones".Translate();
            }

            // Standard single-value filters
            return filterId.ToLowerInvariant() switch
            {
                "temperature" or "averagetemperature" or "average_temperature" => "LandingZone_Temperature".Translate(),
                "mintemperature" or "minimumtemperature" or "minimum_temperature" => "LandingZone_MinTemperature".Translate(),
                "maxtemperature" or "maximumtemperature" or "maximum_temperature" => "LandingZone_MaxTemperature".Translate(),
                "rainfall" => "LandingZone_Rainfall".Translate(),
                "growing" or "growingseason" => "LandingZone_GrowingSeason".Translate(),
                "pollution" => "LandingZone_Pollution".Translate(),
                "forage" or "forageablefood" or "forageable_food" => "LandingZone_ForageableFood".Translate(),
                "movement" => "LandingZone_MovementCost".Translate(),
                "coastal" => "LandingZone_CoastalOcean".Translate(),
                "coastallake" or "coastal_lake" => "LandingZone_CoastalLake".Translate(),
                "caves" or "hascave" or "has_cave" => "LandingZone_Caves".Translate(),
                "graze" => "LandingZone_Grazing".Translate(),
                "stonecount" or "stone_count" => "LandingZone_StoneCount".Translate(),
                "feature" or "worldfeature" or "world_feature" => "LandingZone_WorldFeature".Translate(),
                "landmark" => "LandingZone_Landmark".Translate(),
                "adjacentbiomes" or "adjacent_biomes" => "LandingZone_AdjacentBiomes".Translate(),
                "elevation" => "LandingZone_Elevation".Translate(),
                _ => filterId // Fallback to raw ID if unknown
            };
        }

        /// <summary>
        /// Draws the "✓ Matched" section showing filters the tile successfully met
        /// </summary>
        private void DrawMatchedSection(Rect rect, IReadOnlyList<FilterMatchInfo> matchedFilters, int tileId, ref float curY)
        {
            if (matchedFilters.Count == 0) return;

            // Get or initialize expanded state (default: expanded if <=8 items, collapsed if >8)
            if (!_matchedExpanded.ContainsKey(tileId))
                _matchedExpanded[tileId] = matchedFilters.Count <= 8;

            bool isExpanded = _matchedExpanded[tileId];

            // Section header (clickable)
            var headerRect = new Rect(rect.x, curY, rect.width, 18f);
            if (DrawClickableSectionHeader(headerRect, "LandingZone_MatchedSection".Translate(), new Color(0.3f, 0.9f, 0.3f), matchedFilters.Count, isExpanded))
            {
                _matchedExpanded[tileId] = !isExpanded;
            }
            curY += 20f;

            // Only draw content if expanded
            if (!isExpanded) return;

            // List matched filters in two-column layout
            Text.Font = GameFont.Tiny;
            var criticals = matchedFilters.Where(f => f.IsCritical).ToList();
            var preferred = matchedFilters.Where(f => !f.IsCritical).ToList();

            // Combine criticals and preferred into single list
            var allMatched = new List<string>();

            // Criticals first (show ⚡ indicator)
            foreach (var filter in criticals)
            {
                string name = FormatFilterDisplayName(filter.FilterName, tileId);
                if (filter.IsRangeFilter && filter.Membership < 1.0f)
                    allMatched.Add($"⚡ {name} ({filter.Membership:F2})");
                else
                    allMatched.Add($"⚡ {name}");
            }

            // Preferred second
            foreach (var filter in preferred)
            {
                string name = FormatFilterDisplayName(filter.FilterName, tileId);
                if (filter.IsRangeFilter && filter.Membership < 1.0f)
                    allMatched.Add($"{name} ({filter.Membership:F2})");
                else
                    allMatched.Add(name);
            }

            // Draw in two-column layout
            if (allMatched.Count > 0)
            {
                GUI.color = new Color(0.85f, 0.85f, 0.85f);
                float lineHeight = 16f;
                float columnWidth = (rect.width - 24f) / 2f; // Two equal columns
                float leftX = rect.x + 12f;
                float rightX = rect.x + 12f + columnWidth + 8f; // 8px gap between columns

                for (int i = 0; i < allMatched.Count; i++)
                {
                    int row = i / 2;
                    int col = i % 2;
                    float x = col == 0 ? leftX : rightX;
                    float y = curY + (row * lineHeight);

                    var itemRect = new Rect(x, y, columnWidth, lineHeight);
                    Widgets.Label(itemRect, allMatched[i]);
                }

                // Calculate total height used
                int totalRows = (allMatched.Count + 1) / 2; // Ceiling division
                curY += totalRows * lineHeight + 4f;
                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// Draws the "✗ Missed" section showing filters the tile failed
        /// </summary>
        private void DrawMissedSection(Rect rect, IReadOnlyList<FilterMatchInfo> missedFilters, int tileId, ref float curY)
        {
            if (missedFilters.Count == 0) return;

            // Get or initialize expanded state (default: always expanded for missed)
            if (!_missedExpanded.ContainsKey(tileId))
                _missedExpanded[tileId] = true;

            bool isExpanded = _missedExpanded[tileId];

            // Section header (clickable)
            var headerRect = new Rect(rect.x, curY, rect.width, 18f);
            if (DrawClickableSectionHeader(headerRect, "LandingZone_MissedSection".Translate(), new Color(1.0f, 0.3f, 0.3f), missedFilters.Count, isExpanded))
            {
                _missedExpanded[tileId] = !isExpanded;
            }
            curY += 20f;

            // Only draw content if expanded
            if (!isExpanded) return;

            Text.Font = GameFont.Tiny;

            // Sort: Criticals first, then by penalty magnitude
            var sorted = missedFilters.OrderByDescending(f => f.IsCritical).ThenByDescending(f => f.Penalty).ToList();

            foreach (var missed in sorted)
            {
                string displayName = FormatFilterDisplayName(missed.FilterName, tileId, isMatched: false);
                string icon = "";
                string tags = "";

                if (missed.IsCritical)
                {
                    icon = "⚠ "; // Warning icon for critical misses
                    tags = " [CRITICAL]";
                }
                else if (missed.IsNearMiss)
                {
                    tags = " [near miss]";
                }

                string line = $"{icon}{displayName}{tags} (-{missed.Penalty:F2})";

                // Color code by severity
                if (missed.IsCritical)
                    GUI.color = new Color(1.0f, 0.27f, 0.27f); // Bright red for critical
                else if (missed.IsNearMiss)
                    GUI.color = new Color(1.0f, 0.8f, 0.4f); // Orange for near miss
                else
                    GUI.color = new Color(1.0f, 0.67f, 0.27f); // Light orange for preferred

                var lineRect = new Rect(rect.x + 12f, curY, rect.width - 16f, 16f);
                Widgets.Label(lineRect, line);
                curY += 18f;

                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// Draws the "⚡ Modifiers" section showing mutator bonuses/penalties
        /// </summary>
        private void DrawModifiersSection(Rect rect, IReadOnlyList<MutatorContribution> mutators, float mutatorScore, int tileId, ref float curY)
        {
            if (mutators.Count == 0) return;

            // Get or initialize expanded state (default: collapsed for modifiers)
            if (!_modifiersExpanded.ContainsKey(tileId))
                _modifiersExpanded[tileId] = false;

            bool isExpanded = _modifiersExpanded[tileId];

            // Section header with net contribution (clickable)
            float netContribution = mutators.Sum(m => m.Contribution);
            string headerLabel = netContribution >= 0
                ? "LandingZone_ModifiersSectionWithBonus".Translate(netContribution.ToString("F2"))
                : "LandingZone_ModifiersSectionWithPenalty".Translate(netContribution.ToString("F2"));
            Color headerColor = netContribution >= 0 ? new Color(0.4f, 0.9f, 0.9f) : new Color(0.9f, 0.7f, 0.3f);

            var headerRect = new Rect(rect.x, curY, rect.width, 18f);
            if (DrawClickableSectionHeader(headerRect, headerLabel, headerColor, mutators.Count, isExpanded))
            {
                _modifiersExpanded[tileId] = !isExpanded;
            }
            curY += 20f;

            // Only draw content if expanded
            if (!isExpanded) return;

            Text.Font = GameFont.Tiny;

            // Sort by absolute contribution (largest impact first)
            var sorted = mutators.OrderByDescending(m => Math.Abs(m.Contribution)).ToList();

            foreach (var mutator in sorted)
            {
                string displayName = mutator.MutatorName.Replace("_", " ");
                string sign = mutator.Contribution >= 0 ? "+" : "";
                string line = $"{displayName} ({sign}{mutator.Contribution:F2})";

                // Color by positive/negative
                GUI.color = mutator.IsPositive
                    ? new Color(0.4f, 1.0f, 0.4f)  // Lime green for positive
                    : new Color(1.0f, 0.4f, 0.4f); // Salmon red for negative

                var lineRect = new Rect(rect.x + 12f, curY, rect.width - 16f, 16f);
                Widgets.Label(lineRect, line);
                curY += 18f;

                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// Draws a clickable section header with icon, count, and expand/collapse indicator
        /// </summary>
        /// <returns>True if clicked</returns>
        private static bool DrawClickableSectionHeader(Rect rect, string label, Color color, int count, bool isExpanded)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;

            // Draw invisible button for clicking
            bool clicked = Widgets.ButtonInvisible(rect);

            // Highlight on hover
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }

            // Draw header text with expand/collapse indicator
            GUI.color = color;
            string arrow = isExpanded ? "▼" : "▶";
            string headerText = count > 0 ? $"{arrow} {label} ({count})" : $"{arrow} {label}";
            Widgets.Label(rect, headerText);

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            return clicked;
        }

        /// <summary>
        /// Dumps comprehensive match data for all results to the log for forensic analysis.
        /// Only callable in dev mode.
        /// </summary>
        private void DumpMatchDataForAnalysis()
        {
            var allMatches = LandingZoneContext.LatestResults;
            var matches = GetFilteredAndSortedMatches(allMatches);
            var sb = new System.Text.StringBuilder();
            var preset = LandingZoneContext.State?.Preferences?.ActivePreset;
            var modeLabel = LandingZoneContext.State?.Preferences?.Options?.PreferencesUIMode.ToString() ?? "Unknown";
            var presetLabel = preset != null ? $"{preset.Id} ({preset.Name})" : (LandingZoneContext.State?.Preferences?.Options?.PreferencesUIMode == UIMode.Simple ? "custom_simple" : "advanced");

            sb.AppendLine("========================================");
            sb.AppendLine("LandingZone_DebugDumpHeader".Translate());
            sb.AppendLine("LandingZone_DebugTotalMatches".Translate(matches.Count, allMatches.Count));
            sb.AppendLine("LandingZone_DebugMinScoreFilter".Translate(_minScoreFilter > 0 ? $"{_minScoreFilter:P0}" : "All"));
            sb.AppendLine("LandingZone_DebugSortMode".Translate(_sortMode));
            sb.AppendLine("LandingZone_DebugPresetMode".Translate(presetLabel, modeLabel));
            if (preset != null)
                sb.AppendLine("LandingZone_DebugMutatorOverrides".Translate(preset.MutatorQualityOverrides.Count));
            sb.AppendLine("LandingZone_DebugLoggingTier".Translate(LandingZoneSettings.LogLevel, LandingZoneLogger.IsVerbose ? "LandingZone_DebugAllCaps".Translate() : "LandingZone_DebugTop3".Translate()));
            sb.AppendLine("========================================\n");

            int dumpLimit = LandingZoneLogger.IsVerbose ? matches.Count : Math.Min(3, matches.Count);

            for (int i = 0; i < dumpLimit; i++)
            {
                var match = matches[i];
                var worldGrid = Find.World?.grid;
                var tile = worldGrid?[match.TileId];

                sb.AppendLine("LandingZone_DebugRankTile".Translate(i + 1, match.TileId));
                sb.AppendLine("LandingZone_DebugScore".Translate(match.Score.ToString("F6")));
                sb.AppendLine("LandingZone_DebugBiome".Translate(tile?.PrimaryBiome?.LabelCap ?? "Unknown"));

                if (match.BreakdownV2.HasValue)
                {
                    var breakdown = match.BreakdownV2.Value;

                    sb.AppendLine("LandingZone_DebugPerfectMatch".Translate(breakdown.IsPerfectMatch));
                    sb.AppendLine("LandingZone_DebugPerfectPlus".Translate(breakdown.IsPerfectPlus));
                    sb.AppendLine("LandingZone_DebugCriticalScore".Translate(breakdown.CriticalScore.ToString("F4")));
                    sb.AppendLine("LandingZone_DebugPreferredScore".Translate(breakdown.PreferredScore.ToString("F4")));
                    sb.AppendLine("LandingZone_DebugMutatorScore".Translate(breakdown.MutatorScore.ToString("F4")));
                    sb.AppendLine("LandingZone_DebugPenalty".Translate(breakdown.Penalty.ToString("F4")));
                    sb.AppendLine("LandingZone_DebugCriticalMisses".Translate(breakdown.CriticalMissCount));

                    // Matched filters
                    if (breakdown.MatchedFilters.Count > 0)
                    {
                        sb.AppendLine("\n  " + "LandingZone_DebugMatchedFilters".Translate(breakdown.MatchedFilters.Count));
                        foreach (var filter in breakdown.MatchedFilters)
                        {
                            string criticalTag = filter.IsCritical ? " " + "LandingZone_DebugCriticalTag".Translate() : " " + "LandingZone_DebugPreferredTag".Translate();
                            string rangeTag = filter.IsRangeFilter ? " " + "LandingZone_DebugRangeTag".Translate() : " " + "LandingZone_DebugBooleanTag".Translate();
                            sb.AppendLine($"    - {filter.FilterName}{criticalTag}{rangeTag}");
                            sb.AppendLine("      " + "LandingZone_DebugMembership".Translate(filter.Membership.ToString("F4"), filter.Penalty.ToString("F4")));
                        }
                    }

                    // Missed filters
                    if (breakdown.MissedFilters.Count > 0)
                    {
                        sb.AppendLine("\n  " + "LandingZone_DebugMissedFilters".Translate(breakdown.MissedFilters.Count));
                        foreach (var filter in breakdown.MissedFilters)
                        {
                            string criticalTag = filter.IsCritical ? " " + "LandingZone_DebugCriticalTag".Translate() : " " + "LandingZone_DebugPreferredTag".Translate();
                            string rangeTag = filter.IsRangeFilter ? " " + "LandingZone_DebugRangeTag".Translate() : " " + "LandingZone_DebugBooleanTag".Translate();
                            string nearMissTag = filter.IsNearMiss ? "LandingZone_NearMissTag".Translate() : "";
                            sb.AppendLine($"    - {filter.FilterName}{criticalTag}{rangeTag}{nearMissTag}");
                            sb.AppendLine("      " + "LandingZone_DebugMembership".Translate(filter.Membership.ToString("F4"), filter.Penalty.ToString("F4")));
                        }
                    }

                    // Modifiers
                    if (breakdown.Mutators.Count > 0)
                    {
                        sb.AppendLine($"\n  ~ MODIFIERS ({breakdown.Mutators.Count}):");
                        foreach (var mutator in breakdown.Mutators)
                        {
                            string sign = mutator.IsPositive ? "+" : "";
                            sb.AppendLine($"    - {mutator.MutatorName}: Quality {mutator.QualityRating:+0;-0}, Contribution {sign}{mutator.Contribution:F4}");
                        }
                    }
                }
                else
                {
                    sb.AppendLine("(No breakdown available - old scoring system)");
                }

                sb.AppendLine(); // Blank line between matches
            }

            int remaining = matches.Count - dumpLimit;
            if (remaining > 0)
            {
                sb.AppendLine("LandingZone_MoreMatchesTruncated".Translate(remaining));
            }

            sb.AppendLine("========================================");
            sb.AppendLine("END MATCH DATA DUMP");
            sb.AppendLine("========================================");

            Log.Message($"[LandingZone] MATCH DATA DUMP:\n{sb}");
            Messages.Message("LandingZone_MatchDataDumped".Translate(matches.Count), MessageTypeDefOf.NeutralEvent, false);
        }
    }
}

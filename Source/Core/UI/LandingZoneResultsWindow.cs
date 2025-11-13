using System;
using System.Collections.Generic;
using System.Linq;
using LandingZone.Core.Filtering;
using LandingZone.Data;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LandingZone.Core.UI
{
    public sealed class LandingZoneResultsWindow : Window
    {
        private const float RowHeight = 135f;
        private Vector2 _scroll;
        private SortMode _sortMode = SortMode.Score;
        private float _minScoreFilter = 0f;

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

            listing.Gap(4f);
            if (LandingZoneContext.IsEvaluating)
            {
                listing.Label($"Searching... {(LandingZoneContext.EvaluationProgress * 100f):F0}%");
            }
            else
            {
                listing.Label(LandingZoneContext.LastEvaluationCount > 0
                    ? $"{LandingZoneContext.LastEvaluationCount} matches in {LandingZoneContext.LastEvaluationMs:F0} ms"
                    : "No active match data. Run a search to populate this list.");

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
            Widgets.Label(sortLabelRect, "Sort:");

            var sortButtonRect = new Rect(sortLabelRect.xMax + 3f, rect.y + 2f, 90f, rect.height - 4f);
            if (Widgets.ButtonText(sortButtonRect, GetSortModeLabel(_sortMode)))
            {
                var sortOptions = new List<FloatMenuOption>
                {
                    new FloatMenuOption("Score", () => _sortMode = SortMode.Score),
                    new FloatMenuOption("Growing Days", () => _sortMode = SortMode.GrowingDays),
                    new FloatMenuOption("Temperature", () => _sortMode = SortMode.Temperature),
                    new FloatMenuOption("Biome Name", () => _sortMode = SortMode.BiomeName)
                };
                Find.WindowStack.Add(new FloatMenu(sortOptions));
            }

            // Quick filter buttons
            var filterX = sortButtonRect.xMax + 10f;
            var filterLabelRect = new Rect(filterX, rect.y + 2f, 35f, rect.height - 4f);
            Widgets.Label(filterLabelRect, "Show:");

            var buttonWidth = 55f;
            var buttonGap = 3f;
            filterX = filterLabelRect.xMax + 3f;

            var allRect = new Rect(filterX, rect.y + 2f, buttonWidth, rect.height - 4f);
            DrawFilterButton(allRect, "All", 0f);

            filterX += buttonWidth + buttonGap;
            var excellentRect = new Rect(filterX, rect.y + 2f, buttonWidth, rect.height - 4f);
            DrawFilterButton(excellentRect, "90%+", 0.90f);

            filterX += buttonWidth + buttonGap;
            var goodRect = new Rect(filterX, rect.y + 2f, buttonWidth, rect.height - 4f);
            DrawFilterButton(goodRect, "75%+", 0.75f);

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
                SortMode.Score => "Score",
                SortMode.GrowingDays => "Growing Days",
                SortMode.Temperature => "Temperature",
                SortMode.BiomeName => "Biome Name",
                _ => "Score"
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

            var summaryText = $"Top: {(topScore * 100f):F0}% | Avg: {(avgScore * 100f):F0}% | ";
            if (excellent > 0) summaryText += $"Excellent: {excellent}  ";
            if (good > 0) summaryText += $"Good: {good}  ";
            if (acceptable > 0) summaryText += $"Acceptable: {acceptable}  ";
            if (poor > 0) summaryText += $"Poor: {poor}";

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
                    ? $"Searching... {(LandingZoneContext.EvaluationProgress * 100f):F0}% complete."
                    : "Once LandingZone finishes a search the ranked sites will appear here.";
                Widgets.Label(rect.ContractedBy(6f), label);
                return;
            }

            // Apply filtering and sorting
            var matches = GetFilteredAndSortedMatches(allMatches);

            var viewRect = new Rect(0f, 0f, rect.width - 16f, matches.Count * (RowHeight + 4f));
            Widgets.BeginScrollView(rect, ref _scroll, viewRect);
            float curY = 0f;
            for (int i = 0; i < matches.Count; i++)
            {
                var rowRect = new Rect(0f, curY, viewRect.width, RowHeight);
                DrawMatchRow(rowRect, matches[i], i);
                curY += RowHeight + 4f;
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
            if (forageability >= 0.8f) return "excellent";
            if (forageability >= 0.5f) return "good";
            if (forageability >= 0.2f) return "poor";
            return "minimal";
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
                Widgets.Label(rect, "No filter data available");
                return;
            }

            var tempRange = GetSeasonalTempRange(tileId);
            float maxGrowingDays = 60f; // Default to 60 for now - could calculate actual max

            // Growing days: "XX/YYd grow"
            string growText = $"{extended.GrowingDays:F0}/{maxGrowingDays:F0}d grow";
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
                ? $"{GenTemperature.CelsiusTo(tempRange.avg, TemperatureDisplayMode.Fahrenheit):F1}°F ({GenTemperature.CelsiusTo(tempRange.min, TemperatureDisplayMode.Fahrenheit):F0}°F to {GenTemperature.CelsiusTo(tempRange.max, TemperatureDisplayMode.Fahrenheit):F0}°F)"
                : $"{tempRange.avg:F1}°C ({tempRange.min:F0}°C to {tempRange.max:F0}°C)";
            GUI.color = GetMetricMatchColor(tile.temperature, filters.TemperatureRange, filters.TemperatureImportance);
            var tempRect = new Rect(x, y, Text.CalcSize(tempText).x, rect.height);
            Widgets.Label(tempRect, tempText);
            x += tempRect.width;

            // Separator
            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            var sep2Rect = new Rect(x, y, Text.CalcSize(sep1).x, rect.height);
            Widgets.Label(sep2Rect, sep1);
            x += sep2Rect.width;

            // Rainfall: "XXXXmm rain"
            string rainText = $"{tile.rainfall:F0}mm rain";
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
            string forageFood = GetPrimaryForageFood(tile?.PrimaryBiome) ?? "none";
            string forageText = $"forage: {(extended.Forageability * 100f):F0}% ({forageFood})";
            GUI.color = GetMetricMatchColor(extended.Forageability, filters.ForageabilityRange, filters.ForageImportance);
            var forageRect = new Rect(x, y, Text.CalcSize(forageText).x, rect.height);
            Widgets.Label(forageRect, forageText);
        }

        private static void DrawFailureLine(Rect rect, int tileId, RimWorld.Planet.Tile tile, TileInfoExtended extended, FilterSettings? filters)
        {
            if (filters == null) return;

            var failures = new List<(string text, Color color)>();

            // Check temperature
            if (filters.TemperatureImportance != FilterImportance.Ignored)
            {
                if (tile.temperature < filters.TemperatureRange.min || tile.temperature > filters.TemperatureRange.max)
                {
                    var tempRange = GetSeasonalTempRange(tileId);
                    string tempText = LandingZoneMod.UseFahrenheit
                        ? $"Temp: {GenTemperature.CelsiusTo(tempRange.avg, TemperatureDisplayMode.Fahrenheit):F1}°F (wanted {GenTemperature.CelsiusTo(filters.TemperatureRange.min, TemperatureDisplayMode.Fahrenheit):F0}-{GenTemperature.CelsiusTo(filters.TemperatureRange.max, TemperatureDisplayMode.Fahrenheit):F0}°F)"
                        : $"Temp: {tempRange.avg:F1}°C (wanted {filters.TemperatureRange.min:F0}-{filters.TemperatureRange.max:F0}°C)";
                    Color color = filters.TemperatureImportance == FilterImportance.Critical
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
                    string rainText = $"Rain: {tile.rainfall:F0}mm (wanted {filters.RainfallRange.min:F0}-{filters.RainfallRange.max:F0}mm)";
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
                    string growText = $"Grow: {extended.GrowingDays:F0}d (wanted {filters.GrowingDaysRange.min:F0}-{filters.GrowingDaysRange.max:F0}d)";
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
                    string forageText = $"Forage: {(extended.Forageability * 100f):F0}% (wanted {(filters.ForageabilityRange.min * 100f):F0}-{(filters.ForageabilityRange.max * 100f):F0}%)";
                    Color color = filters.ForageImportance == FilterImportance.Critical
                        ? new Color(1f, 0.27f, 0.27f)
                        : new Color(1f, 0.67f, 0.27f);
                    failures.Add((forageText, color));
                }
            }

            // Check river
            bool hasRiver = tile is RimWorld.Planet.SurfaceTile surface && surface.Rivers != null && surface.Rivers.Count > 0;
            if (filters.RiverImportance != FilterImportance.Ignored && !hasRiver)
            {
                string riverText = "River: none (required)";
                Color color = filters.RiverImportance == FilterImportance.Critical
                    ? new Color(1f, 0.27f, 0.27f)
                    : new Color(1f, 0.67f, 0.27f);
                failures.Add((riverText, color));
            }

            // Check coastal
            if (filters.CoastalImportance != FilterImportance.Ignored && !tile.IsCoastal)
            {
                string coastText = "Coastal: no (required)";
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

        private void DrawMatchRow(Rect rect, TileScore score, int index)
        {
            // Draw score-tier colored background
            var bgColor = GetScoreTierColor(score.Score);
            var bgRect = rect.ContractedBy(1f);
            Widgets.DrawBoxSolid(bgRect, new Color(bgColor.r, bgColor.g, bgColor.b, 0.15f));

            Widgets.DrawHighlightIfMouseover(rect);
            var worldGrid = Find.World?.grid;
            var tile = worldGrid?[score.TileId];
            bool hasInfo = tile != null;
            string biomeLabel = tile?.PrimaryBiome?.LabelCap ?? "Unknown biome";

            // Line 1: Rank and tile ID
            var rankRect = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 62f, 16f);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.Label(rankRect, $"#{index + 1} • Tile {score.TileId}");
            GUI.color = Color.white;

            // Focus button (top right)
            var focusRect = new Rect(rect.xMax - 58f, rect.y + 1f, 54f, 18f);
            if (Widgets.ButtonText(focusRect, "Focus"))
            {
                LandingZoneContext.FocusTile(score.TileId);
            }

            // Line 2: Score percentage with tier label
            var scoreRect = new Rect(rect.x + 4f, rect.y + 24f, rect.width - 8f, 20f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = bgColor;
            Widgets.Label(scoreRect, $"{(score.Score * 100f):F0}% {GetScoreTierLabel(score.Score)}");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            // Line 3: Biome name (separate line for clarity)
            var biomeRect = new Rect(rect.x + 4f, rect.y + 48f, rect.width - 8f, 16f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(biomeRect, biomeLabel);

            // Lines 4-5-6: Climate, terrain, and failures (color-coded)
            float lineY = rect.y + 68f;
            Text.Font = GameFont.Tiny;

            if (hasInfo)
            {
                var extended = LandingZoneContext.Filters?.TileCache.GetOrCompute(score.TileId) ?? default;
                var filters = LandingZoneContext.State?.Preferences?.Filters;

                // Climate line - draw colored segments
                DrawColoredClimateLine(new Rect(rect.x + 4f, lineY, rect.width - 8f, 16f), score.TileId, tile, extended, filters);
                lineY += 20f;

                // Terrain line
                string terrainLine = tile.hilliness.GetLabelCap().ToString();
                if (extended.StoneDefNames != null && extended.StoneDefNames.Length > 0)
                {
                    terrainLine += " • " + string.Join("/", extended.StoneDefNames);
                }
                GUI.color = new Color(0.85f, 0.85f, 0.85f);
                var terrainRect = new Rect(rect.x + 4f, lineY, rect.width - 8f, 16f);
                Widgets.Label(terrainRect, terrainLine);
                lineY += 20f;

                // Failure line (only show if there are failures)
                DrawFailureLine(new Rect(rect.x + 4f, lineY, rect.width - 8f, 16f), score.TileId, tile, extended, filters);
                lineY += 20f;
            }
            else
            {
                GUI.color = new Color(0.85f, 0.85f, 0.85f);
                Widgets.Label(new Rect(rect.x + 4f, lineY, rect.width - 8f, 16f), "No snapshot data");
                lineY += 20f;
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

        private static string GetScoreTierLabel(float score)
        {
            if (score >= 0.90f) return "Excellent";
            if (score >= 0.75f) return "Good";
            if (score >= 0.60f) return "Acceptable";
            return "Poor";
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
    }
}

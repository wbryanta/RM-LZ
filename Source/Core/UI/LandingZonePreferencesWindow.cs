using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LandingZone.Core.UI
{
    public sealed class LandingZonePreferencesWindow : Window
    {
        private const float WindowPadding = 12f;
        private const float ScrollbarWidth = 16f;
        private readonly List<FeatureEntry> _featureOptions;
        private readonly Dictionary<FeatureCategory, List<FeatureEntry>> _featureBuckets = new();
        private readonly Dictionary<string, FeatureEntry> _featureLookup = new();
        private readonly List<ThingDef> _stoneOptions;
        private readonly Hilliness[] _hillinessOptions =
        {
            Hilliness.Flat,
            Hilliness.SmallHills,
            Hilliness.LargeHills,
            Hilliness.Mountainous
        };
        private static readonly (FeatureCategory Category, string Label)[] FeatureGroups =
        {
            (FeatureCategory.Geological, "Geographical features"),
            (FeatureCategory.Resource, "Resource nodes"),
            (FeatureCategory.PointOfInterest, "Points of interest"),
            (FeatureCategory.Other, "Miscellaneous features")
        };

        private struct FeatureEntry
        {
            public FeatureEntry(FeatureDef def, string label, FeatureCategory category)
            {
                Def = def;
                Label = label;
                Category = category;
            }

            public FeatureDef Def;
            public string Label;
            public FeatureCategory Category;
        }

        private enum FeatureCategory
        {
            Geological,
            Resource,
            PointOfInterest,
            Other
        }

        private FloatRange _temperature;
        private FloatRange _rainfall;
        private FloatRange _growingDays;
        private FloatRange _pollution;
        private FloatRange _forage;
        private FloatRange _movement;
        private FilterImportance _temperatureImportance;
        private FilterImportance _rainfallImportance;
        private FilterImportance _growingDaysImportance;
        private FilterImportance _pollutionImportance;
        private FilterImportance _forageImportance;
        private FilterImportance _movementImportance;
        private FilterImportance _coastalImportance;
        private FilterImportance _riverImportance;
        private FilterImportance _grazeImportance;
        private FilterImportance _featureImportance;
        private FilterImportance _stoneImportance;
        private string? _featureDefName;
        private readonly HashSet<string> _selectedStoneDefs = new();
        private readonly HashSet<Hilliness> _selectedHilliness = new();
        private Vector2 _stoneScroll;
        private Vector2 _scrollPos;
        private float _contentHeight = 600f;
        private int _maxResults = FilterSettings.DefaultMaxResults;
        private bool _dirty;
        private static readonly Dictionary<string, bool> SectionExpanded = new Dictionary<string, bool>
        {
            { "Climate", true },
            { "Pollution", true },
            { "Terrain", true },
            { "Hydrology", true },
            { "Features & resources", false },
            { "Results", true }
        };

        public LandingZonePreferencesWindow()
        {
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            draggable = true;
            onlyOneOfTypeAllowed = true;
            doCloseX = true;

            _featureOptions = new List<FeatureEntry>();
            foreach (var def in DefDatabase<FeatureDef>.AllDefsListForReading)
            {
                if (def == null)
                    continue;

                var label = ResolveFeatureLabel(def);
                if (string.IsNullOrEmpty(label))
                    continue;

                var category = ClassifyFeature(def);
                var entry = new FeatureEntry(def, label, category);
                _featureOptions.Add(entry);
                if (!string.IsNullOrEmpty(def.defName))
                {
                    _featureLookup[def.defName] = entry;
                }
                if (!_featureBuckets.TryGetValue(category, out var bucket))
                {
                    bucket = new List<FeatureEntry>();
                    _featureBuckets[category] = bucket;
                }
                bucket.Add(entry);
            }
            foreach (var bucket in _featureBuckets.Values)
            {
                bucket.Sort((a, b) => string.Compare(a.Label, b.Label, System.StringComparison.OrdinalIgnoreCase));
            }

            _stoneOptions = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def?.building?.isNaturalRock ?? false)
                .OrderBy(def => def.label ?? def.defName ?? string.Empty)
                .ToList();

            var filters = LandingZoneContext.State?.Preferences.Filters ?? new FilterSettings();
            _temperature = filters.TemperatureRange;
            _rainfall = filters.RainfallRange;
            _growingDays = filters.GrowingDaysRange;
            _pollution = filters.PollutionRange;
            _forage = filters.ForageabilityRange;
            _movement = filters.MovementDifficultyRange;
            _temperatureImportance = filters.TemperatureImportance;
            _rainfallImportance = filters.RainfallImportance;
            _growingDaysImportance = filters.GrowingDaysImportance;
            _pollutionImportance = filters.PollutionImportance;
            _forageImportance = filters.ForageImportance;
            _movementImportance = filters.MovementDifficultyImportance;
            _coastalImportance = filters.CoastalImportance;
            _riverImportance = filters.RiverImportance;
            _grazeImportance = filters.GrazeImportance;
            _featureImportance = filters.FeatureImportance;
            _stoneImportance = filters.StoneImportance;
            _featureDefName = filters.RequiredFeatureDefName;
            foreach (var hill in filters.AllowedHilliness)
                _selectedHilliness.Add(hill);
            foreach (var stone in filters.RequiredStoneDefNames)
                _selectedStoneDefs.Add(stone);
            if (_selectedHilliness.Count == 0)
            {
                foreach (var hill in _hillinessOptions)
                    _selectedHilliness.Add(hill);
            }
            _maxResults = Mathf.Clamp(filters.MaxResults, 1, FilterSettings.MaxResultsLimit);
        }

        public override Vector2 InitialSize => new Vector2(520f, 620f);

        public override void PreClose()
        {
            PersistFilters();
        }

        public override void DoWindowContents(Rect inRect)
        {
            var outRect = inRect.AtZero().ContractedBy(WindowPadding);
            var viewRect = new Rect(0f, 0f, outRect.width - ScrollbarWidth, Mathf.Max(_contentHeight, outRect.height + 1f));
            Widgets.BeginScrollView(outRect, ref _scrollPos, viewRect, true);
            var listing = new Listing_Standard { ColumnWidth = viewRect.width };
            listing.Begin(viewRect);

            Text.Font = GameFont.Medium;
            listing.Label("LandingZone Filters");
            Text.Font = GameFont.Small;
            listing.GapLine();

            DrawSection(listing, "Climate", DrawClimateSection);
            DrawSection(listing, "Pollution", DrawPollutionSection);
            DrawSection(listing, "Terrain", DrawTerrainSection);
            DrawSection(listing, "Hydrology", DrawHydrologySection);
            DrawSection(listing, "Features & resources", DrawFeaturesSection);
            DrawSection(listing, "Results", DrawResultsSection);

            listing.Gap();
            if (listing.ButtonText("Save filters"))
            {
                PersistFilters();
            }

            if (listing.ButtonText("Reset to defaults"))
            {
                ResetFilters();
                PersistFilters();
            }

            float usedHeight = listing.CurHeight;
            listing.End();
            _contentHeight = Mathf.Max(usedHeight + 12f, outRect.height + 1f);
            Widgets.EndScrollView();
        }

        private void DrawSection(Listing_Standard listing, string key, System.Action<Listing_Standard> drawer)
        {
            if (!SectionExpanded.TryGetValue(key, out var expanded))
                expanded = true;

            var headerRect = listing.GetRect(24f);
            string arrow = expanded ? "\u25BC" : "\u25B6";
            if (Widgets.ButtonText(headerRect, $"{arrow} {key}"))
            {
                expanded = !expanded;
            }
            SectionExpanded[key] = expanded;
            listing.Gap(2f);
            if (!expanded)
                return;

            var previousFont = Text.Font;
            Text.Font = GameFont.Small;
            drawer(listing);
            Text.Font = previousFont;
            listing.GapLine();
        }

        private void DrawClimateSection(Listing_Standard listing)
        {
            DrawTemperatureRange(listing);
            DrawRangeWithImportance(listing, "Rainfall (mm)", ref _rainfall, ref _rainfallImportance, 0f, 4000f);
            DrawRangeWithImportance(listing, "Growing days", ref _growingDays, ref _growingDaysImportance, 0f, 60f);
        }

        private void DrawPollutionSection(Listing_Standard listing)
        {
            DrawRangeWithImportance(listing, "Pollution (%)", ref _pollution, ref _pollutionImportance, 0f, 1f, percent: true);
            DrawRangeWithImportance(listing, "Forageability (%)", ref _forage, ref _forageImportance, 0f, 1f, percent: true);
        }

        private void DrawTerrainSection(Listing_Standard listing)
        {
            DrawRangeWithImportance(listing, "Movement difficulty", ref _movement, ref _movementImportance, 0f, 5f);
            DrawHillinessOptions(listing);
        }

        private void DrawHydrologySection(Listing_Standard listing)
        {
            DrawBooleanImportance(listing, "Coastal tiles", ref _coastalImportance);
            DrawBooleanImportance(listing, "Rivers", ref _riverImportance);
            DrawBooleanImportance(listing, "Graze now", ref _grazeImportance);
        }

        private void DrawFeaturesSection(Listing_Standard listing)
        {
            bool featureActive = DrawImportanceHeader(listing, "World feature", ref _featureImportance);
            if (!featureActive)
            {
                listing.Label("Any special features");
            }

            foreach (var (category, title) in FeatureGroups)
            {
                DrawFeatureGroup(listing, category, title);
            }
            listing.Gap(6f);
            DrawImportanceHeader(listing, "Stone mix", ref _stoneImportance);
            listing.Label("Select the stone types you want available.");
            DrawStoneSelectors(listing);
        }

        private void DrawResultsSection(Listing_Standard listing)
        {
            DrawMaxResults(listing);
            DrawEvaluationSummary(listing);
        }

        private void DrawFloatRange(Listing_Standard listing, string label, ref FloatRange range, float min, float max, bool percent = false)
        {
            var displayMin = percent ? range.min * 100f : range.min;
            var displayMax = percent ? range.max * 100f : range.max;
            listing.Label(percent
                ? $"{label}: {displayMin:F0}% - {displayMax:F0}%"
                : $"{label}: {displayMin:F1} - {displayMax:F1}");
            var rect = listing.GetRect(24f);
            var before = range;
            Widgets.FloatRange(rect, GetHashCode() ^ label.GetHashCode(), ref range, min, max);
            if (percent)
            {
                range.min = Mathf.Clamp(range.min, min, max);
                range.max = Mathf.Clamp(range.max, min, max);
            }
            if (!Mathf.Approximately(before.min, range.min) || !Mathf.Approximately(before.max, range.max))
            {
                _dirty = true;
            }
        }
        private void DrawRangeWithImportance(Listing_Standard listing, string label, ref FloatRange range, ref FilterImportance importance, float min, float max, bool percent = false)
        {
            if (!DrawImportanceHeader(listing, label, ref importance))
            {
                listing.Label("Any value");
                listing.Gap(4f);
                return;
            }

            DrawFloatRange(listing, label, ref range, min, max, percent);
            listing.Gap(4f);
        }

        private void DrawBooleanImportance(Listing_Standard listing, string label, ref FilterImportance importance)
        {
            DrawImportanceHeader(listing, label, ref importance);
            listing.Gap(4f);
        }

        private void DrawTemperatureRange(Listing_Standard listing)
        {
            string label = LandingZoneMod.UseFahrenheit ? "Temperature (°F)" : "Temperature (°C)";
            if (!DrawImportanceHeader(listing, label, ref _temperatureImportance))
            {
                listing.Label("Any temperature");
                listing.Gap(4f);
                return;
            }

            var displayRange = new FloatRange(ToDisplayTemp(_temperature.min), ToDisplayTemp(_temperature.max));
            var sliderMin = ToDisplayTemp(-60f);
            var sliderMax = ToDisplayTemp(60f);
            listing.Label($"{displayRange.min:F1} - {displayRange.max:F1}");
            var rect = listing.GetRect(24f);
            var before = displayRange;
            Widgets.FloatRange(rect, GetHashCode() ^ 2191, ref displayRange, sliderMin, sliderMax);
            if (!Mathf.Approximately(before.min, displayRange.min) || !Mathf.Approximately(before.max, displayRange.max))
            {
                _temperature = new FloatRange(FromDisplayTemp(displayRange.min), FromDisplayTemp(displayRange.max));
                _dirty = true;
            }
            listing.Gap(4f);
        }

        private void DrawFeatureGroup(Listing_Standard listing, FeatureCategory category, string header)
        {
            if (!_featureBuckets.TryGetValue(category, out var entries) || entries.Count == 0)
                return;

            string currentLabel = ResolveCurrentFeatureLabel(entries);
            var rect = listing.GetRect(24f);
            if (Widgets.ButtonText(rect, $"{header}: {currentLabel}"))
            {
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("Any", () =>
                    {
                        if (!string.IsNullOrEmpty(_featureDefName))
                        {
                            _featureDefName = null;
                            _dirty = true;
                        }
                    })
                };
                foreach (var entry in entries)
                {
                    var captured = entry;
                    options.Add(new FloatMenuOption(captured.Label, () =>
                    {
                        _featureDefName = captured.Def.defName;
                        _dirty = true;
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            listing.Gap(4f);
        }

        private string ResolveCurrentFeatureLabel(System.Collections.Generic.List<FeatureEntry> entries)
        {
            if (string.IsNullOrEmpty(_featureDefName))
                return "Any";
            string selected = _featureDefName!;

            foreach (var entry in entries)
            {
                if (entry.Def.defName == selected)
                    return entry.Label;
            }

            if (_featureLookup.TryGetValue(selected, out var other))
            {
                return $"{other.Label} (other group)";
            }

            return "Any";
        }

        private void DrawStoneSelectors(Listing_Standard listing)
        {
            float height = Mathf.Min(6, _stoneOptions.Count) * 24f + 8f;
            var container = listing.GetRect(height);
            Widgets.DrawMenuSection(container);
            var viewRect = new Rect(0f, 0f, container.width - 16f, _stoneOptions.Count * 24f);
            Widgets.BeginScrollView(container, ref _stoneScroll, viewRect);
            float curY = 0f;
            foreach (var stone in _stoneOptions)
            {
                var row = new Rect(0f, curY, viewRect.width, 22f);
                bool selected = _selectedStoneDefs.Contains(stone.defName);
                bool before = selected;
                Widgets.CheckboxLabeled(row, stone.LabelCap, ref selected);
                if (before != selected)
                {
                    _dirty = true;
                    if (selected)
                        _selectedStoneDefs.Add(stone.defName);
                    else
                        _selectedStoneDefs.Remove(stone.defName);
                }
                curY += 22f;
            }
            Widgets.EndScrollView();
        }

        private void DrawHillinessOptions(Listing_Standard listing)
        {
            var gridRect = listing.GetRect(60f);
            Widgets.DrawMenuSection(gridRect);
            var inner = gridRect.ContractedBy(4f);
            float curX = inner.x;
            float curY = inner.y;
            float colWidth = inner.width / 2f;
            foreach (var hill in _hillinessOptions)
            {
                var label = hill.GetLabelCap();
                var row = new Rect(curX, curY, colWidth - 10f, 22f);
                bool selected = _selectedHilliness.Contains(hill);
                bool before = selected;
                Widgets.CheckboxLabeled(row, label, ref selected);
                if (before != selected)
                {
                    _dirty = true;
                    if (selected)
                        _selectedHilliness.Add(hill);
                    else
                        _selectedHilliness.Remove(hill);
                }
                curX += colWidth;
                if (curX + colWidth > inner.xMax + 1f)
                {
                    curX = inner.x;
                    curY += 24f;
                }
            }
            if (_selectedHilliness.Count == 0)
            {
                foreach (var hill in _hillinessOptions)
                    _selectedHilliness.Add(hill);
            }
        }

        private void DrawEvaluationSummary(Listing_Standard listing)
        {
            var prevFont = Text.Font;
            Text.Font = GameFont.Tiny;
            string status;
            if (LandingZoneContext.IsEvaluating)
            {
                status = $"Searching... {(LandingZoneContext.EvaluationProgress * 100f):F0}%";
            }
            else if (LandingZoneContext.LastEvaluationCount > 0)
            {
                status = $"Last search: {LandingZoneContext.LastEvaluationCount} matches in {LandingZoneContext.LastEvaluationMs:F0} ms";
            }
            else
            {
                status = "No LandingZone searches have run yet.";
            }
            listing.Label(status);
            Text.Font = prevFont;
        }

        private void PersistFilters()
        {
            if (!_dirty)
                return;

            var filters = LandingZoneContext.State?.Preferences.Filters;
            if (filters == null)
                return;

            filters.TemperatureRange = _temperature;
            filters.RainfallRange = _rainfall;
            filters.GrowingDaysRange = _growingDays;
            filters.PollutionRange = _pollution;
            filters.ForageabilityRange = _forage;
            filters.MovementDifficultyRange = _movement;
            filters.TemperatureImportance = _temperatureImportance;
            filters.RainfallImportance = _rainfallImportance;
            filters.GrowingDaysImportance = _growingDaysImportance;
            filters.PollutionImportance = _pollutionImportance;
            filters.ForageImportance = _forageImportance;
            filters.MovementDifficultyImportance = _movementImportance;
            filters.CoastalImportance = _coastalImportance;
            filters.RiverImportance = _riverImportance;
            filters.GrazeImportance = _grazeImportance;
            filters.FeatureImportance = string.IsNullOrEmpty(_featureDefName) ? FilterImportance.Ignored : _featureImportance;
            filters.RequiredFeatureDefName = _featureDefName;
            filters.StoneImportance = _selectedStoneDefs.Count == 0 ? FilterImportance.Ignored : _stoneImportance;
            filters.RequiredStoneDefNames.Clear();
            foreach (var stone in _selectedStoneDefs)
                filters.RequiredStoneDefNames.Add(stone);
            filters.AllowedHilliness.Clear();
            foreach (var hill in _selectedHilliness)
                filters.AllowedHilliness.Add(hill);
            filters.MaxResults = Mathf.Clamp(_maxResults, 1, FilterSettings.MaxResultsLimit);

            _dirty = false;
        }

        private void ResetFilters()
        {
            var filters = LandingZoneContext.State?.Preferences.Filters;
            if (filters == null)
                return;

            filters.Reset();
            _temperature = filters.TemperatureRange;
            _rainfall = filters.RainfallRange;
            _growingDays = filters.GrowingDaysRange;
            _pollution = filters.PollutionRange;
            _forage = filters.ForageabilityRange;
            _movement = filters.MovementDifficultyRange;
            _temperatureImportance = filters.TemperatureImportance;
            _rainfallImportance = filters.RainfallImportance;
            _growingDaysImportance = filters.GrowingDaysImportance;
            _pollutionImportance = filters.PollutionImportance;
            _forageImportance = filters.ForageImportance;
            _movementImportance = filters.MovementDifficultyImportance;
            _coastalImportance = filters.CoastalImportance;
            _riverImportance = filters.RiverImportance;
            _grazeImportance = filters.GrazeImportance;
            _featureImportance = filters.FeatureImportance;
            _stoneImportance = filters.StoneImportance;
            _featureDefName = filters.RequiredFeatureDefName;
            _selectedStoneDefs.Clear();
            foreach (var stone in filters.RequiredStoneDefNames)
                _selectedStoneDefs.Add(stone);
            _selectedHilliness.Clear();
            foreach (var hill in filters.AllowedHilliness)
                _selectedHilliness.Add(hill);
            if (_selectedHilliness.Count == 0)
            {
                foreach (var hill in _hillinessOptions)
                    _selectedHilliness.Add(hill);
            }
            _maxResults = Mathf.Clamp(filters.MaxResults, 1, FilterSettings.MaxResultsLimit);
            _dirty = true;
        }

        private static float ToDisplayTemp(float value)
        {
            return LandingZoneMod.UseFahrenheit ? value * 9f / 5f + 32f : value;
        }

        private static float FromDisplayTemp(float value)
        {
            return LandingZoneMod.UseFahrenheit ? (value - 32f) * 5f / 9f : value;
        }

        private void DrawMaxResults(Listing_Standard listing)
        {
            listing.Label($"Top matches displayed: {_maxResults}");
            var rect = listing.GetRect(24f);
            int before = _maxResults;
            float slider = Widgets.HorizontalSlider(rect, _maxResults, 1f, FilterSettings.MaxResultsLimit, true, "", "1", FilterSettings.MaxResultsLimit.ToString());
            _maxResults = Mathf.Clamp(Mathf.RoundToInt(slider), 1, FilterSettings.MaxResultsLimit);
            if (_maxResults != before)
                _dirty = true;
        }
        private bool DrawImportanceHeader(Listing_Standard listing, string label, ref FilterImportance importance)
        {
            var row = listing.GetRect(24f);
            var buttonRect = new Rect(row.x, row.y, 28f, row.height);
            if (Widgets.ButtonText(buttonRect, ImportanceIcon(importance)))
            {
                importance = NextImportance(importance);
                _dirty = true;
            }

            var labelRect = new Rect(buttonRect.xMax + 4f, row.y, row.width - buttonRect.width - 4f, row.height);
            Widgets.Label(labelRect, $"{label} ({ImportanceDescription(importance)})");
            return importance != FilterImportance.Ignored;
        }

        private static FilterImportance NextImportance(FilterImportance current)
        {
            return current switch
            {
                FilterImportance.Ignored => FilterImportance.Preferred,
                FilterImportance.Preferred => FilterImportance.Critical,
                _ => FilterImportance.Ignored
            };
        }

        private static string ImportanceIcon(FilterImportance importance)
        {
            return importance switch
            {
                FilterImportance.Ignored => "X",
                FilterImportance.Preferred => "✓",
                FilterImportance.Critical => "!",
                _ => "?"
            };
        }

        private static string ImportanceDescription(FilterImportance importance)
        {
            return importance switch
            {
                FilterImportance.Ignored => "ignored",
                FilterImportance.Preferred => "preferred",
                FilterImportance.Critical => "critical",
                _ => "unknown"
            };
        }

        private static string ResolveFeatureLabel(FeatureDef def)
        {
            if (!string.IsNullOrWhiteSpace(def.LabelCap))
                return def.LabelCap;
            if (!string.IsNullOrWhiteSpace(def.label))
                return def.label;
            return def.defName ?? string.Empty;
        }

        private static FeatureCategory ClassifyFeature(FeatureDef def)
        {
            var text = $"{def.defName} {def.label}".ToLowerInvariant();
            if (ContainsAny(text, "lump", "ore", "mine", "precious", "deposit"))
                return FeatureCategory.Resource;
            if (ContainsAny(text, "ruin", "vault", "site", "complex", "platform", "launch", "outpost", "base", "structure"))
                return FeatureCategory.PointOfInterest;
            if (ContainsAny(text, "canyon", "crater", "valley", "oasis", "lake", "river", "coast", "swamp", "cave", "volcano", "mountain", "plateau"))
                return FeatureCategory.Geological;
            return FeatureCategory.Other;
        }

        private static bool ContainsAny(string source, params string[] terms)
        {
            foreach (var term in terms)
            {
                if (source.Contains(term))
                    return true;
            }

            return false;
        }
    }
}

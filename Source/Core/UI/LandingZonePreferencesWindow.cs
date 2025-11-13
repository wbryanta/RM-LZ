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

        // Temperature filters (separate for average, min, max)
        private FloatRange _avgTemperature;
        private FloatRange _minTemperature;
        private FloatRange _maxTemperature;
        private FilterImportance _avgTemperatureImportance;
        private FilterImportance _minTemperatureImportance;
        private FilterImportance _maxTemperatureImportance;

        // Climate filters
        private FloatRange _rainfall;
        private FloatRange _growingDays;
        private FilterImportance _rainfallImportance;
        private FilterImportance _growingDaysImportance;

        // Environment filters
        private FloatRange _pollution;
        private FloatRange _forage;
        private FilterImportance _pollutionImportance;
        private FilterImportance _forageImportance;

        // Terrain filters
        private FloatRange _movement;
        private FloatRange _elevation;
        private FilterImportance _movementImportance;
        private FilterImportance _elevationImportance;
        private readonly HashSet<Hilliness> _selectedHilliness = new();

        // Geography filters
        private FilterImportance _coastalImportance;
        // Rivers and Roads now use IndividualImportanceContainer (filters.Rivers, filters.Roads)
        private FilterImportance _coastalLakeImportance;

        // Resource filters
        private FilterImportance _grazeImportance;
        private FilterImportance _forageableFoodImportance;
        private string? _forageableFoodDefName;

        // World features
        private FilterImportance _featureImportance;
        // MapFeatures and AdjacentBiomes now use IndividualImportanceContainer (filters.MapFeatures, filters.AdjacentBiomes)
        private FilterImportance _landmarkImportance;
        private string? _featureDefName;

        // UI state
        private Vector2 _scrollPos;
        private float _contentHeight = 2400f;  // Fixed height large enough for all sections fully expanded
        private int _maxResults = FilterSettings.DefaultMaxResults;
        private bool _dirty;
        private static readonly Dictionary<string, bool> SectionExpanded = new Dictionary<string, bool>
        {
            { "Temperature", true },
            { "Climate & Environment", true },
            { "Terrain & Hilliness", true },
            { "Geography & Hydrology", true },
            { "Resources & Grazing", false },
            { "World Features", false },
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

            // Filter to only the 5 valid world-gen stone types
            // These are the only stones that appear naturally during world generation
            var validStoneDefNames = new HashSet<string>
            {
                "Sandstone",
                "Marble",
                "Granite",
                "Slate",
                "Limestone"
            };

            _stoneOptions = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def =>
                {
                    if (def?.building?.isNaturalRock != true)
                        return false;

                    // Only include the 5 valid world-gen stones
                    return validStoneDefNames.Contains(def.defName);
                })
                .OrderBy(def => def.label ?? def.defName ?? string.Empty)
                .ToList();

            var filters = LandingZoneContext.State?.Preferences.Filters ?? new FilterSettings();
            // Temperature filters
            _avgTemperature = filters.AverageTemperatureRange;
            _minTemperature = filters.MinimumTemperatureRange;
            _maxTemperature = filters.MaximumTemperatureRange;
            _avgTemperatureImportance = filters.AverageTemperatureImportance;
            _minTemperatureImportance = filters.MinimumTemperatureImportance;
            _maxTemperatureImportance = filters.MaximumTemperatureImportance;

            // Climate filters
            _rainfall = filters.RainfallRange;
            _growingDays = filters.GrowingDaysRange;
            _rainfallImportance = filters.RainfallImportance;
            _growingDaysImportance = filters.GrowingDaysImportance;

            // Environment filters
            _pollution = filters.PollutionRange;
            _forage = filters.ForageabilityRange;
            _pollutionImportance = filters.PollutionImportance;
            _forageImportance = filters.ForageImportance;
            _forageableFoodDefName = filters.ForageableFoodDefName;
            _forageableFoodImportance = filters.ForageableFoodImportance;

            // Terrain filters
            _movement = filters.MovementDifficultyRange;
            _movementImportance = filters.MovementDifficultyImportance;
            _elevation = filters.ElevationRange;
            _elevationImportance = filters.ElevationImportance;

            // Geography filters
            _coastalImportance = filters.CoastalImportance;
            _coastalLakeImportance = filters.CoastalLakeImportance;
            // Rivers and Roads now use IndividualImportanceContainer directly from filters

            // Resource filters
            _grazeImportance = filters.GrazeImportance;

            // World features
            _featureImportance = filters.FeatureImportance;
            _featureDefName = filters.RequiredFeatureDefName;
            // MapFeatures and AdjacentBiomes now use IndividualImportanceContainer directly from filters
            _landmarkImportance = filters.LandmarkImportance;
            foreach (var hill in filters.AllowedHilliness)
                _selectedHilliness.Add(hill);
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
            var options = LandingZoneContext.State?.Preferences.Options ?? new LandingZoneOptions();
            var currentMode = options.PreferencesUIMode;

            // Mode toggle header (before scroll view)
            var headerRect = new Rect(inRect.x, inRect.y, inRect.width, 40f);
            DrawModeToggle(headerRect, ref currentMode);

            // Save mode change if toggled
            if (currentMode != options.PreferencesUIMode)
            {
                options.PreferencesUIMode = currentMode;
                _dirty = true;
            }

            // Content area (below mode toggle)
            var contentRect = new Rect(inRect.x, inRect.y + 50f, inRect.width, inRect.height - 50f);

            // Render appropriate mode UI
            if (currentMode == UIMode.Default)
            {
                // Default mode: Use new simplified UI
                DrawDefaultModeContent(contentRect);
            }
            else
            {
                // Advanced mode: Use existing full UI (will be refactored in Phase 2B)
                DrawAdvancedModeContent(contentRect);
            }
        }

        private void DrawModeToggle(Rect rect, ref UIMode currentMode)
        {
            // Mode toggle buttons (Default | Advanced)
            var buttonWidth = 120f;
            var spacing = 10f;
            var totalWidth = (buttonWidth * 2) + spacing;
            var startX = rect.x + (rect.width - totalWidth) / 2f;

            var defaultRect = new Rect(startX, rect.y + 5f, buttonWidth, 30f);
            var advancedRect = new Rect(startX + buttonWidth + spacing, rect.y + 5f, buttonWidth, 30f);

            // Default button
            var isDefault = currentMode == UIMode.Default;
            if (Widgets.ButtonText(defaultRect, "Default", active: isDefault))
            {
                currentMode = UIMode.Default;
            }

            // Advanced button
            var isAdvanced = currentMode == UIMode.Advanced;
            if (Widgets.ButtonText(advancedRect, "Advanced", active: isAdvanced))
            {
                currentMode = UIMode.Advanced;
            }
        }

        private void DrawDefaultModeContent(Rect contentRect)
        {
            // Use new DefaultModeUI renderer (stub for now, Phase 2A will implement)
            var preferences = LandingZoneContext.State?.Preferences ?? new UserPreferences();

            var viewRect = new Rect(0f, 0f, contentRect.width - ScrollbarWidth, 600f);
            Widgets.BeginScrollView(contentRect, ref _scrollPos, viewRect);

            DefaultModeUI.DrawContent(viewRect, preferences);

            Widgets.EndScrollView();
        }

        private void DrawAdvancedModeContent(Rect contentRect)
        {
            // Existing full UI (will be replaced with AdvancedModeUI in Phase 2B)
            var viewRect = new Rect(0f, 0f, contentRect.width - ScrollbarWidth, _contentHeight);
            Widgets.BeginScrollView(contentRect, ref _scrollPos, viewRect);
            var listing = new Listing_Standard { ColumnWidth = viewRect.width };
            listing.Begin(viewRect);

            Text.Font = GameFont.Medium;
            listing.Label("LandingZone Filters");
            Text.Font = GameFont.Small;
            listing.GapLine();

            // Reorganized sections with active filter counts
            DrawSection(listing, "Temperature", DrawTemperatureSection);
            DrawSection(listing, "Climate & Environment", DrawClimateEnvironmentSection);
            DrawSection(listing, "Terrain & Hilliness", DrawTerrainSection);
            DrawSection(listing, "Geography & Hydrology", DrawGeographySection);
            DrawSection(listing, "Resources & Grazing", DrawResourcesSection);
            DrawSection(listing, "World Features", DrawWorldFeaturesSection);
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

            // Dev mode: Performance test button
            if (Prefs.DevMode)
            {
                listing.GapLine(12f);
                Text.Font = GameFont.Small;
                listing.Label("=== DEVELOPER TOOLS ===");
                listing.Gap(4f);

                if (listing.ButtonText("[DEV] Run Performance Test"))
                {
                    Log.Message("[LandingZone] Running performance test...");
                    Diagnostics.FilterPerformanceTest.RunPerformanceTest();
                    Messages.Message("[LandingZone] Performance test complete - check Player.log", MessageTypeDefOf.NeutralEvent, false);
                }

                listing.Gap(4f);

                if (listing.ButtonText("[DEV] Dump World Tile Data"))
                {
                    Log.Message("[LandingZone] Dumping world tile data...");
                    Diagnostics.WorldDataDumper.DumpWorldData();
                }

                listing.Gap(4f);

                if (listing.ButtonText("[DEV] Dump FULL World Cache"))
                {
                    Log.Message("[LandingZone] Dumping FULL world cache (this may take a while)...");
                    Diagnostics.WorldDataDumper.DumpFullWorldCache();
                }

                listing.Gap(12f);
            }

            listing.End();
            Widgets.EndScrollView();
        }

        // Original DoWindowContents code continues below (DrawSection, etc.)

        private void DrawSection(Listing_Standard listing, string key, System.Action<Listing_Standard> drawer)
        {
            if (!SectionExpanded.TryGetValue(key, out var expanded))
                expanded = true;

            int activeCount = GetActiveSectionFilterCount(key);

            var headerRect = listing.GetRect(26f);
            UIHelpers.DrawSectionHeaderWithBadge(headerRect, key, activeCount, expanded);

            // Make it clickable to toggle
            if (Widgets.ButtonInvisible(headerRect))
            {
                expanded = !expanded;
                SectionExpanded[key] = expanded;
            }

            listing.Gap(2f);
            if (!expanded)
                return;

            var previousFont = Text.Font;
            Text.Font = GameFont.Small;
            drawer(listing);
            Text.Font = previousFont;
            listing.GapLine();
        }

        private int GetActiveSectionFilterCount(string sectionKey)
        {
            return sectionKey switch
            {
                "Temperature" => CountTemperatureFilters(),
                "Climate & Environment" => CountClimateFilters(),
                "Terrain & Hilliness" => CountTerrainFilters(),
                "Geography & Hydrology" => CountGeographyFilters(),
                "Resources & Grazing" => CountResourceFilters(),
                "World Features" => CountWorldFeatureFilters(),
                _ => 0
            };
        }

        private int CountTemperatureFilters()
        {
            int count = 0;
            if (_avgTemperatureImportance != FilterImportance.Ignored) count++;
            if (_minTemperatureImportance != FilterImportance.Ignored) count++;
            if (_maxTemperatureImportance != FilterImportance.Ignored) count++;
            return count;
        }

        private int CountClimateFilters()
        {
            int count = 0;
            if (_rainfallImportance != FilterImportance.Ignored) count++;
            if (_growingDaysImportance != FilterImportance.Ignored) count++;
            if (_pollutionImportance != FilterImportance.Ignored) count++;
            if (_forageImportance != FilterImportance.Ignored) count++;
            return count;
        }

        private int CountTerrainFilters()
        {
            int count = 0;
            if (_movementImportance != FilterImportance.Ignored) count++;
            if (_elevationImportance != FilterImportance.Ignored) count++;
            if (_selectedHilliness.Count != _hillinessOptions.Length) count++; // Count if not all selected
            return count;
        }

        private int CountGeographyFilters()
        {
            int count = 0;
            var filters = LandingZoneContext.State?.Preferences?.Filters;
            if (filters == null) return count;

            if (_coastalImportance != FilterImportance.Ignored) count++;
            if (_coastalLakeImportance != FilterImportance.Ignored) count++;
            if (filters.Rivers.HasAnyImportance) count++;
            if (filters.Roads.HasAnyImportance) count++;
            return count;
        }

        private int CountResourceFilters()
        {
            int count = 0;
            if (_grazeImportance != FilterImportance.Ignored) count++;

            // TODO: Add stone filter counting when rebuilt in Sprint 1.2

            return count;
        }

        private int CountWorldFeatureFilters()
        {
            int count = 0;
            var filters = LandingZoneContext.State?.Preferences?.Filters;
            if (filters == null) return count;

            if (_featureImportance != FilterImportance.Ignored && !string.IsNullOrEmpty(_featureDefName)) count++;
            if (filters.MapFeatures.HasAnyImportance) count++;
            if (filters.AdjacentBiomes.HasAnyImportance) count++;
            if (_landmarkImportance != FilterImportance.Ignored) count++;
            return count;
        }

        private void DrawTemperatureSection(Listing_Standard listing)
        {
            Text.Font = GameFont.Tiny;
            listing.Label("Control temperature ranges separately for maximum flexibility.");
            Text.Font = GameFont.Small;
            listing.Gap(4f);

            DrawTemperatureRange(listing, "Average temperature", ref _avgTemperature, ref _avgTemperatureImportance,
                "Year-round average temperature");
            DrawTemperatureRange(listing, "Winter minimum", ref _minTemperature, ref _minTemperatureImportance,
                "Coldest temperature in winter - affects survival");
            DrawTemperatureRange(listing, "Summer maximum", ref _maxTemperature, ref _maxTemperatureImportance,
                "Hottest temperature in summer - affects heat stroke risk");
        }

        private void DrawClimateEnvironmentSection(Listing_Standard listing)
        {
            DrawRangeWithImportance(listing, "Rainfall (mm)", ref _rainfall, ref _rainfallImportance, 0f, 4000f,
                tooltip: "Annual rainfall - affects plant growth and water availability");
            DrawRangeWithImportance(listing, "Growing days", ref _growingDays, ref _growingDaysImportance, 0f, 60f,
                tooltip: "Days per year warm enough for crops - critical for farming");
            listing.GapLine(4f);
            DrawRangeWithImportance(listing, "Pollution (%)", ref _pollution, ref _pollutionImportance, 0f, 1f, percent: true,
                tooltip: "Environmental pollution level - affects colonist health");
            DrawRangeWithImportance(listing, "Forageability (%)", ref _forage, ref _forageImportance, 0f, 1f, percent: true,
                tooltip: "Wild food availability - affects survival without farming");
        }

        private void DrawTerrainSection(Listing_Standard listing)
        {
            DrawRangeWithImportance(listing, "Movement difficulty", ref _movement, ref _movementImportance, 0f, 5f,
                tooltip: "Terrain traversal difficulty - affects caravan speed");

            // Elevation
            DrawRangeWithImportance(listing, "Elevation (meters)", ref _elevation, ref _elevationImportance, -500f, 5000f,
                tooltip: "Height above sea level - affects temperature and accessibility");

            listing.GapLine(4f);

            DrawHillinessOptions(listing);
        }

        private void DrawGeographySection(Listing_Standard listing)
        {
            DrawBooleanImportance(listing, "Ocean coastal", ref _coastalImportance,
                "Adjacent to ocean - enables fishing and naval defense");

            // Coastal lake
            DrawBooleanImportance(listing, "Lake coastal", ref _coastalLakeImportance,
                "Adjacent to freshwater lake - scenic and strategic");

            listing.GapLine(6f);

            // Rivers - individual importance
            Text.Font = GameFont.Small;
            listing.Label("Rivers (individual importance per type):");
            Text.Font = GameFont.Tiny;
            listing.Label("Each river type can be Ignored, Preferred, or Critical.");
            Text.Font = GameFont.Small;
            listing.Gap(2f);

            var riverTypes = RiverFilter.GetAllRiverTypes().ToList();
            if (riverTypes.Any())
            {
                var filters = LandingZoneContext.State?.Preferences?.Filters;
                if (filters != null)
                {
                    UIHelpers.DrawIndividualImportanceUtilityButtons(listing, filters.Rivers, riverTypes.Select(r => r.defName), 300f);
                    UIHelpers.DrawIndividualImportanceList(
                        listing,
                        filters.Rivers,
                        riverTypes.Select(r => r.defName),
                        defName => riverTypes.First(r => r.defName == defName).LabelCap.ToString(),
                        28f,
                        200f);
                    _dirty = true;
                }
            }
            else
            {
                listing.Label("No river types available");
            }

            listing.GapLine(6f);

            // Roads - individual importance
            Text.Font = GameFont.Small;
            listing.Label("Roads (individual importance per type):");
            Text.Font = GameFont.Tiny;
            listing.Label("Each road type can be Ignored, Preferred, or Critical.");
            Text.Font = GameFont.Small;
            listing.Gap(2f);

            var roadTypes = RoadFilter.GetAllRoadTypes().ToList();
            if (roadTypes.Any())
            {
                var filters = LandingZoneContext.State?.Preferences?.Filters;
                if (filters != null)
                {
                    UIHelpers.DrawIndividualImportanceUtilityButtons(listing, filters.Roads, roadTypes.Select(r => r.defName), 300f);
                    UIHelpers.DrawIndividualImportanceList(
                        listing,
                        filters.Roads,
                        roadTypes.Select(r => r.defName),
                        defName => roadTypes.First(r => r.defName == defName).LabelCap.ToString(),
                        28f,
                        200f);
                    _dirty = true;
                }
            }
            else
            {
                listing.Label("No road types available");
            }
        }

        private void DrawResourcesSection(Listing_Standard listing)
        {
            // Graze
            DrawBooleanImportance(listing, "Animals can graze", ref _grazeImportance,
                "Whether animals can eat virtual plants in current season");

            listing.GapLine(4f);

            // TODO Sprint 1.2: Rebuild stone filters using IndividualImportanceContainer pattern
            // Stone UI temporarily removed during deprecated code cleanup
            listing.Label("Stone filters - coming soon!");
        }

        private void DrawWorldFeaturesSection(Listing_Standard listing)
        {
            Text.Font = GameFont.Tiny;
            listing.Label("World-scale features like geothermal vents and ancient complexes.");
            Text.Font = GameFont.Small;
            listing.Gap(4f);

            DrawBooleanImportance(listing, "World feature", ref _featureImportance,
                "Special map features like geothermal vents, ruins, and ancient sites");

            if (_featureImportance != FilterImportance.Ignored)
            {
                foreach (var (category, title) in FeatureGroups)
                {
                    DrawFeatureGroup(listing, category, title);
                }
            }

            listing.GapLine(6f);

            // Landmark filter
            DrawBooleanImportance(listing, "Has landmark", ref _landmarkImportance,
                "Tile has a proper named landmark (e.g., 'Mount Erebus', 'Lake Victoria')");

            listing.GapLine(6f);

            // Map Features - individual importance (RimWorld 1.6+)
            Text.Font = GameFont.Small;
            listing.Label("Map Features (actual world generation data):");
            Text.Font = GameFont.Tiny;
            listing.Label("Tile Mutators from world generation: Caves, Ruins, Mountain, MixedBiome, etc. Uses actual game data, not estimates.");
            Text.Font = GameFont.Small;
            listing.Gap(2f);

            var mapFeatures = MapFeatureFilter.GetAllMapFeatureTypes().ToList();
            if (mapFeatures.Any())
            {
                var filters = LandingZoneContext.State?.Preferences?.Filters;
                if (filters != null)
                {
                    UIHelpers.DrawIndividualImportanceUtilityButtons(listing, filters.MapFeatures, mapFeatures, 300f);
                    UIHelpers.DrawIndividualImportanceList(
                        listing,
                        filters.MapFeatures,
                        mapFeatures,
                        feature => feature,  // Feature names are already display-ready
                        28f,
                        200f);
                    _dirty = true;
                }
            }
            else
            {
                listing.Label("No map features discovered yet - generate a world first");
            }

            listing.GapLine(6f);

            // Adjacent Biomes - individual importance
            Text.Font = GameFont.Small;
            listing.Label("Adjacent Biomes (individual importance per type):");
            Text.Font = GameFont.Tiny;
            listing.Label("Find tiles bordering specific biome types.");
            Text.Font = GameFont.Small;
            listing.Gap(2f);

            var biomeTypes = AdjacentBiomesFilter.GetAllBiomeTypes().ToList();
            if (biomeTypes.Any())
            {
                var filters = LandingZoneContext.State?.Preferences?.Filters;
                if (filters != null)
                {
                    UIHelpers.DrawIndividualImportanceUtilityButtons(listing, filters.AdjacentBiomes, biomeTypes.Select(b => b.defName), 300f);
                    UIHelpers.DrawIndividualImportanceList(
                        listing,
                        filters.AdjacentBiomes,
                        biomeTypes.Select(b => b.defName),
                        defName => biomeTypes.First(b => b.defName == defName).LabelCap.ToString(),
                        28f,
                        200f);
                    _dirty = true;
                }
            }
            else
            {
                listing.Label("No biome types available");
            }
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
        private void DrawRangeWithImportance(Listing_Standard listing, string label, ref FloatRange range, ref FilterImportance importance, float min, float max, bool percent = false, string tooltip = null)
        {
            var headerRect = listing.GetRect(24f);
            bool active = UIHelpers.DrawImportanceSelector(headerRect, label, ref importance, tooltip);

            if (!active)
            {
                listing.Label("Any value");
                listing.Gap(4f);
                return;
            }

            if (active)
            {
                _dirty = true;
            }

            DrawFloatRange(listing, label, ref range, min, max, percent);
            listing.Gap(4f);
        }

        private void DrawBooleanImportance(Listing_Standard listing, string label, ref FilterImportance importance, string tooltip = null)
        {
            var headerRect = listing.GetRect(24f);
            FilterImportance before = importance;
            UIHelpers.DrawImportanceSelector(headerRect, label, ref importance, tooltip);
            if (importance != before)
                _dirty = true;
            listing.Gap(4f);
        }

        private void DrawTemperatureRange(Listing_Standard listing, string labelPrefix, ref FloatRange temperatureRange, ref FilterImportance importance, string tooltip = null)
        {
            string unit = LandingZoneMod.UseFahrenheit ? "°F" : "°C";
            string fullLabel = $"{labelPrefix} ({unit})";

            var headerRect = listing.GetRect(24f);
            bool active = UIHelpers.DrawImportanceSelector(headerRect, fullLabel, ref importance, tooltip);

            if (!active)
            {
                listing.Label("Any temperature");
                listing.Gap(4f);
                return;
            }

            var displayRange = new FloatRange(ToDisplayTemp(temperatureRange.min), ToDisplayTemp(temperatureRange.max));
            var sliderMin = ToDisplayTemp(-60f);
            var sliderMax = ToDisplayTemp(60f);
            listing.Label($"{displayRange.min:F1} - {displayRange.max:F1}");
            var rect = listing.GetRect(24f);
            var before = displayRange;
            Widgets.FloatRange(rect, GetHashCode() ^ labelPrefix.GetHashCode(), ref displayRange, sliderMin, sliderMax);
            if (!Mathf.Approximately(before.min, displayRange.min) || !Mathf.Approximately(before.max, displayRange.max))
            {
                temperatureRange = new FloatRange(FromDisplayTemp(displayRange.min), FromDisplayTemp(displayRange.max));
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

        // TODO Sprint 1.2: Rebuild stone selector UI using IndividualImportanceContainer pattern
        // Method temporarily stubbed during deprecated code cleanup
        private void DrawStoneSelectors(Listing_Standard listing)
        {
            listing.Label("Stone selectors - coming soon!");
        }

        private void DrawHillinessOptions(Listing_Standard listing)
        {
            Text.Font = GameFont.Tiny;
            listing.Label("Allowed terrain hilliness levels - tiles must match one of the selected types.");
            Text.Font = GameFont.Small;
            listing.Gap(2f);

            // Add utility buttons
            UIHelpers.DrawMultiSelectUtilityButtons(
                listing,
                onAll: () =>
                {
                    foreach (var hill in _hillinessOptions)
                    {
                        if (!_selectedHilliness.Contains(hill))
                        {
                            _selectedHilliness.Add(hill);
                            _dirty = true;
                        }
                    }
                },
                onNone: () =>
                {
                    if (_selectedHilliness.Count > 0)
                    {
                        _selectedHilliness.Clear();
                        _dirty = true;
                    }
                },
                onReset: () =>
                {
                    _selectedHilliness.Clear();
                    _selectedHilliness.Add(Hilliness.SmallHills);
                    _selectedHilliness.Add(Hilliness.LargeHills);
                    _selectedHilliness.Add(Hilliness.Mountainous);
                    _dirty = true;
                }
            );

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

                // Color code selected items
                var prevColor = GUI.color;
                if (selected)
                    GUI.color = UIHelpers.ActiveFilterColor;

                Widgets.CheckboxLabeled(row, label, ref selected);
                GUI.color = prevColor;

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

            // Show tile cache status if precomputing
            if (LandingZoneContext.IsTileCachePrecomputing)
            {
                int percent = (int)(LandingZoneContext.TileCacheProgress * 100f);
                status = $"Analyzing world... {percent}% ({LandingZoneContext.TileCacheProcessedTiles:N0}/{LandingZoneContext.TileCacheTotalTiles:N0} tiles)";
            }
            else if (LandingZoneContext.IsEvaluating)
            {
                string phaseDesc = LandingZoneContext.CurrentPhaseDescription;
                if (!string.IsNullOrEmpty(phaseDesc))
                {
                    status = $"{(LandingZoneContext.EvaluationProgress * 100f):F0}% - {phaseDesc}";
                }
                else
                {
                    status = $"Searching... {(LandingZoneContext.EvaluationProgress * 100f):F0}%";
                }
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

            // Temperature filters
            filters.AverageTemperatureRange = _avgTemperature;
            filters.MinimumTemperatureRange = _minTemperature;
            filters.MaximumTemperatureRange = _maxTemperature;
            filters.AverageTemperatureImportance = _avgTemperatureImportance;
            filters.MinimumTemperatureImportance = _minTemperatureImportance;
            filters.MaximumTemperatureImportance = _maxTemperatureImportance;

            // Climate filters
            filters.RainfallRange = _rainfall;
            filters.GrowingDaysRange = _growingDays;
            filters.RainfallImportance = _rainfallImportance;
            filters.GrowingDaysImportance = _growingDaysImportance;

            // Environment filters
            filters.PollutionRange = _pollution;
            filters.ForageabilityRange = _forage;
            filters.PollutionImportance = _pollutionImportance;
            filters.ForageImportance = _forageImportance;
            filters.ForageableFoodDefName = _forageableFoodDefName;
            filters.ForageableFoodImportance = _forageableFoodImportance;

            // Terrain filters
            filters.MovementDifficultyRange = _movement;
            filters.MovementDifficultyImportance = _movementImportance;
            filters.ElevationRange = _elevation;
            filters.ElevationImportance = _elevationImportance;

            // Geography filters
            filters.CoastalImportance = _coastalImportance;
            filters.CoastalLakeImportance = _coastalLakeImportance;
            // Rivers and Roads IndividualImportanceContainer is modified directly via UI

            // Resource filters
            filters.GrazeImportance = _grazeImportance;

            // World features
            filters.FeatureImportance = _featureImportance;
            filters.RequiredFeatureDefName = _featureDefName;
            // MapFeatures and AdjacentBiomes IndividualImportanceContainer is modified directly via UI
            filters.LandmarkImportance = _landmarkImportance;

            // Terrain constraints
            filters.AllowedHilliness.Clear();
            foreach (var hill in _selectedHilliness)
                filters.AllowedHilliness.Add(hill);

            // Results
            filters.MaxResults = Mathf.Clamp(_maxResults, 1, FilterSettings.MaxResultsLimit);

            _dirty = false;
        }

        private void ResetFilters()
        {
            var filters = LandingZoneContext.State?.Preferences.Filters;
            if (filters == null)
                return;

            filters.Reset();

            // Temperature filters
            _avgTemperature = filters.AverageTemperatureRange;
            _minTemperature = filters.MinimumTemperatureRange;
            _maxTemperature = filters.MaximumTemperatureRange;
            _avgTemperatureImportance = filters.AverageTemperatureImportance;
            _minTemperatureImportance = filters.MinimumTemperatureImportance;
            _maxTemperatureImportance = filters.MaximumTemperatureImportance;

            // Climate filters
            _rainfall = filters.RainfallRange;
            _growingDays = filters.GrowingDaysRange;
            _rainfallImportance = filters.RainfallImportance;
            _growingDaysImportance = filters.GrowingDaysImportance;

            // Environment filters
            _pollution = filters.PollutionRange;
            _forage = filters.ForageabilityRange;
            _pollutionImportance = filters.PollutionImportance;
            _forageImportance = filters.ForageImportance;
            _forageableFoodDefName = filters.ForageableFoodDefName;
            _forageableFoodImportance = filters.ForageableFoodImportance;

            // Terrain filters
            _movement = filters.MovementDifficultyRange;
            _movementImportance = filters.MovementDifficultyImportance;
            _elevation = filters.ElevationRange;
            _elevationImportance = filters.ElevationImportance;

            // Geography filters
            _coastalImportance = filters.CoastalImportance;
            _coastalLakeImportance = filters.CoastalLakeImportance;
            // Rivers and Roads now use IndividualImportanceContainer directly from filters

            // Resource filters
            _grazeImportance = filters.GrazeImportance;

            // World features
            _featureImportance = filters.FeatureImportance;
            _featureDefName = filters.RequiredFeatureDefName;
            // MapFeatures and AdjacentBiomes now use IndividualImportanceContainer directly from filters
            _landmarkImportance = filters.LandmarkImportance;

            // Collections
            _selectedHilliness.Clear();
            foreach (var hill in filters.AllowedHilliness)
                _selectedHilliness.Add(hill);
            if (_selectedHilliness.Count == 0)
            {
                foreach (var hill in _hillinessOptions)
                    _selectedHilliness.Add(hill);
            }

            // Results
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

            // Resource nodes
            if (ContainsAny(text, "lump", "ore", "mine", "precious", "deposit"))
                return FeatureCategory.Resource;

            // Points of interest (man-made or special sites)
            if (ContainsAny(text, "ruin", "vault", "site", "complex", "platform", "launch", "outpost", "base", "structure", "ancient"))
                return FeatureCategory.PointOfInterest;

            // Geological features (natural terrain features)
            if (ContainsAny(text, "canyon", "crater", "valley", "oasis", "lake", "river", "coast", "swamp", "cave",
                "volcano", "mountain", "plateau", "fjord", "island", "headwater", "grove", "biome", "foggy",
                "animal", "life", "willow", "thermal", "spring", "geyser"))
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

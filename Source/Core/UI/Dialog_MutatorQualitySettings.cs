using System.Collections.Generic;
using System.Linq;
using LandingZone.Core.Filtering;
using RimWorld;
using UnityEngine;
using Verse;

namespace LandingZone.Core.UI
{
    /// <summary>
    /// Dialog for customizing mutator quality ratings.
    /// Allows users to override default ratings (-10 to +10) for each mutator.
    /// Organized by category matching the Palette structure.
    /// </summary>
    public class Dialog_MutatorQualitySettings : Window
    {
        private Vector2 _scrollPosition = Vector2.zero;
        private HashSet<string> _collapsedCategories = new HashSet<string>();
        private string _searchQuery = "";
        private bool _showOnlyOverrides = false;

        // Cached category data (built once)
        private List<(string Category, List<MutatorEntry> Mutators)>? _categoryData;

        private class MutatorEntry
        {
            public string DefName;
            public string DisplayName;
            public int DefaultRating;
            public int CurrentRating;
            public bool HasOverride;

            public MutatorEntry(string defName, string displayName, int defaultRating)
            {
                DefName = defName;
                DisplayName = displayName;
                DefaultRating = defaultRating;
                CurrentRating = LandingZoneSettings.UserMutatorQualityOverrides != null
                    && LandingZoneSettings.UserMutatorQualityOverrides.TryGetValue(defName, out int userRating)
                    ? userRating
                    : defaultRating;
                HasOverride = LandingZoneSettings.UserMutatorQualityOverrides?.ContainsKey(defName) ?? false;
            }
        }

        public Dialog_MutatorQualitySettings()
        {
            doCloseX = true;
            doCloseButton = true;
            forcePause = false;
            absorbInputAroundWindow = false;
            closeOnCancel = true;
            closeOnAccept = false;
        }

        public override Vector2 InitialSize => new Vector2(500f, 600f);

        public override void DoWindowContents(Rect inRect)
        {
            // Build category data if not cached
            _categoryData ??= BuildCategoryData();

            // Header
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 100f, 30f), "LandingZone_MutatorDialog_Title".Translate());
            Text.Font = GameFont.Small;

            // Reset All button (top right)
            Rect resetRect = new Rect(inRect.xMax - 90f, inRect.y, 80f, 24f);
            if (Widgets.ButtonText(resetRect, "LandingZone_MutatorDialog_ResetAll".Translate()))
            {
                LandingZoneSettings.UserMutatorQualityOverrides?.Clear();
                _categoryData = BuildCategoryData(); // Rebuild cache
                LandingZoneMod.Instance?.WriteSettings();
                Messages.Message("LandingZone_MutatorDialog_ResetAllConfirm".Translate(), MessageTypeDefOf.NeutralEvent, false);
            }

            // Subtitle
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(inRect.x, inRect.y + 28f, inRect.width, 20f),
                "LandingZone_MutatorDialog_Subtitle".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // Search bar
            Rect searchRect = new Rect(inRect.x, inRect.y + 52f, inRect.width - 160f, 24f);
            _searchQuery = Widgets.TextField(searchRect, _searchQuery);
            if (string.IsNullOrEmpty(_searchQuery))
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(searchRect.x + 4f, searchRect.y, searchRect.width, searchRect.height), "LandingZone_MutatorDialog_SearchPlaceholder".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }

            // "Show Only Overrides" toggle
            Rect toggleRect = new Rect(inRect.xMax - 150f, inRect.y + 54f, 150f, 20f);
            Widgets.CheckboxLabeled(toggleRect, "LandingZone_MutatorDialog_OnlyOverrides".Translate(), ref _showOnlyOverrides);

            // Scrollable content
            float scrollStartY = inRect.y + 84f;
            float scrollHeight = inRect.height - 120f; // Leave room for close button
            Rect scrollRect = new Rect(inRect.x, scrollStartY, inRect.width, scrollHeight);
            float contentHeight = CalculateContentHeight();
            // Guard against zero/negative height corrupting scroll view
            if (contentHeight < 50f) contentHeight = 50f;
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, contentHeight);

            Widgets.BeginScrollView(scrollRect, ref _scrollPosition, viewRect);

            float y = 0f;
            string searchLower = _searchQuery.ToLowerInvariant();

            foreach (var (category, mutators) in _categoryData)
            {
                // Filter mutators by search query and show-only-overrides toggle
                var filteredMutators = mutators.Where(m =>
                    (string.IsNullOrEmpty(searchLower) || m.DisplayName.ToLowerInvariant().Contains(searchLower) || m.DefName.ToLowerInvariant().Contains(searchLower))
                    && (!_showOnlyOverrides || m.HasOverride)
                ).ToList();

                if (filteredMutators.Count == 0)
                    continue;

                // Category header
                bool isCollapsed = _collapsedCategories.Contains(category);
                Rect headerRect = new Rect(0f, y, viewRect.width, 24f);

                // Header background
                Widgets.DrawBoxSolid(headerRect, new Color(0.15f, 0.15f, 0.18f));

                // Collapse arrow
                string arrow = isCollapsed ? "+" : "-";
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(4f, y, 20f, 24f), arrow);

                // Category name
                Widgets.Label(new Rect(20f, y, viewRect.width - 80f, 24f), category);

                // Count badge
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                int overrideCount = filteredMutators.Count(m => m.HasOverride);
                string countText = overrideCount > 0
                    ? "LandingZone_MutatorDialog_CustomCount".Translate(filteredMutators.Count, overrideCount)
                    : filteredMutators.Count.ToString();
                Widgets.Label(new Rect(viewRect.width - 100f, y, 90f, 24f), countText);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;

                // Click to toggle collapse
                if (Widgets.ButtonInvisible(headerRect))
                {
                    if (isCollapsed)
                        _collapsedCategories.Remove(category);
                    else
                        _collapsedCategories.Add(category);
                }

                y += 26f;

                if (isCollapsed)
                    continue;

                // Draw mutator rows
                foreach (var mutator in filteredMutators)
                {
                    y += DrawMutatorRow(new Rect(0f, y, viewRect.width, 28f), mutator);
                }

                y += 8f; // Gap between categories
            }

            Widgets.EndScrollView();
        }

        private float DrawMutatorRow(Rect rect, MutatorEntry mutator)
        {
            // Row background (alternate)
            if (mutator.HasOverride)
            {
                Widgets.DrawBoxSolid(rect, new Color(0.18f, 0.2f, 0.22f)); // Highlight overridden
            }

            // Label
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = mutator.HasOverride ? Color.white : new Color(0.8f, 0.8f, 0.8f);
            Widgets.Label(new Rect(rect.x + 8f, rect.y, 160f, rect.height), mutator.DisplayName);
            GUI.color = Color.white;

            // Slider
            float sliderX = rect.x + 170f;
            float sliderWidth = rect.width - 280f;
            Rect sliderRect = new Rect(sliderX, rect.y + 4f, sliderWidth, rect.height - 8f);

            // Draw slider background with gradient coloring
            DrawRatingSliderBackground(sliderRect);

            // Draw slider
            int newValue = Mathf.RoundToInt(Widgets.HorizontalSlider(sliderRect, mutator.CurrentRating, -10f, 10f, true));

            // Value display with color
            Color valueColor = GetRatingColor(newValue);
            Rect valueRect = new Rect(sliderRect.xMax + 8f, rect.y, 40f, rect.height);
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = valueColor;
            Text.Font = GameFont.Small;
            Widgets.Label(valueRect, newValue >= 0 ? $"+{newValue}" : newValue.ToString());
            GUI.color = Color.white;

            // Reset button (only if overridden)
            if (mutator.HasOverride)
            {
                Rect resetBtnRect = new Rect(rect.xMax - 50f, rect.y + 2f, 44f, rect.height - 4f);
                if (Widgets.ButtonText(resetBtnRect, "X"))
                {
                    LandingZoneSettings.UserMutatorQualityOverrides?.Remove(mutator.DefName);
                    mutator.CurrentRating = mutator.DefaultRating;
                    mutator.HasOverride = false;
                    LandingZoneMod.Instance?.WriteSettings();
                }
            }
            else
            {
                // Show default indicator
                Rect defaultRect = new Rect(rect.xMax - 50f, rect.y, 44f, rect.height);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                Widgets.Label(defaultRect, "LandingZone_MutatorDialog_DefaultIndicator".Translate());
                GUI.color = Color.white;
            }

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // Handle value change
            if (newValue != mutator.CurrentRating)
            {
                mutator.CurrentRating = newValue;

                // Update or remove override
                if (newValue != mutator.DefaultRating)
                {
                    LandingZoneSettings.UserMutatorQualityOverrides ??= new Dictionary<string, int>();
                    LandingZoneSettings.UserMutatorQualityOverrides[mutator.DefName] = newValue;
                    mutator.HasOverride = true;
                }
                else
                {
                    // Reset to default = remove override
                    LandingZoneSettings.UserMutatorQualityOverrides?.Remove(mutator.DefName);
                    mutator.HasOverride = false;
                }
                LandingZoneMod.Instance?.WriteSettings();
            }

            return 30f;
        }

        private void DrawRatingSliderBackground(Rect rect)
        {
            // Draw a subtle gradient from red (left) through neutral (center) to green (right)
            int segments = 20;
            float segmentWidth = rect.width / segments;

            for (int i = 0; i < segments; i++)
            {
                float t = i / (float)(segments - 1); // 0 to 1
                float rating = Mathf.Lerp(-10f, 10f, t);
                Color segmentColor = GetRatingColor(Mathf.RoundToInt(rating));
                segmentColor.a = 0.2f; // Very subtle

                Rect segmentRect = new Rect(rect.x + i * segmentWidth, rect.y, segmentWidth + 1f, rect.height);
                Widgets.DrawBoxSolid(segmentRect, segmentColor);
            }
        }

        private Color GetRatingColor(int rating)
        {
            if (rating >= 8)
                return new Color(0.2f, 0.9f, 0.3f); // Bright green
            if (rating >= 5)
                return new Color(0.4f, 0.8f, 0.4f); // Green
            if (rating >= 1)
                return new Color(0.6f, 0.8f, 0.5f); // Light green
            if (rating == 0)
                return new Color(0.7f, 0.7f, 0.7f); // Gray (neutral)
            if (rating >= -4)
                return new Color(0.9f, 0.7f, 0.4f); // Yellow-orange
            if (rating >= -7)
                return new Color(0.9f, 0.5f, 0.3f); // Orange
            return new Color(0.9f, 0.3f, 0.3f); // Red
        }

        private float CalculateContentHeight()
        {
            if (_categoryData == null) return 400f;

            float height = 0f;
            string searchLower = _searchQuery.ToLowerInvariant();

            foreach (var (category, mutators) in _categoryData)
            {
                var filteredMutators = mutators.Where(m =>
                    (string.IsNullOrEmpty(searchLower) || m.DisplayName.ToLowerInvariant().Contains(searchLower) || m.DefName.ToLowerInvariant().Contains(searchLower))
                    && (!_showOnlyOverrides || m.HasOverride)
                ).ToList();

                if (filteredMutators.Count == 0)
                    continue;

                height += 26f; // Category header

                if (!_collapsedCategories.Contains(category))
                {
                    height += filteredMutators.Count * 30f;
                }

                height += 8f; // Gap
            }

            return height + 20f;
        }

        private List<(string Category, List<MutatorEntry> Mutators)> BuildCategoryData()
        {
            var result = new List<(string, List<MutatorEntry>)>();

            // Define category order and their mutators (using translation keys)
            var categoryDefinitions = new[]
            {
                ("LandingZone_MutatorCategory_Climate".Translate().ToString(), new[]
                {
                    ("SunnyMutator", "Sunny"),
                    ("FoggyMutator", "Foggy"),
                    ("WindyMutator", "Windy"),
                    ("WetClimate", "Wet Climate"),
                    ("Pollution_Increased", "Pollution Increased"),
                }),
                ("LandingZone_MutatorCategory_WaterFeatures".Translate().ToString(), new[]
                {
                    ("River", "River (Landform)"),
                    ("RiverDelta", "River Delta"),
                    ("RiverConfluence", "Confluence"),
                    ("RiverIsland", "River Island"),
                    ("Headwater", "Headwater"),
                    ("Lake", "Lake"),
                    ("LakeWithIsland", "Lake w/ Island"),
                    ("LakeWithIslands", "Lake w/ Islands"),
                    ("Lakeshore", "Lakeshore"),
                    ("CaveLakes", "Cave Lakes"),
                    ("Pond", "Pond"),
                    ("Fjord", "Fjord"),
                    ("Bay", "Bay"),
                    ("Coast", "Coast"),
                    ("Harbor", "Harbor"),
                    ("Cove", "Cove"),
                    ("Peninsula", "Peninsula"),
                    ("Archipelago", "Archipelago"),
                    ("CoastalAtoll", "Coastal Atoll"),
                    ("CoastalIsland", "Coastal Island"),
                    ("Iceberg", "Iceberg"),
                    ("Oasis", "Oasis"),
                    ("HotSprings", "Hot Springs"),
                    ("ToxicLake", "Toxic Lake"),
                }),
                ("LandingZone_MutatorCategory_Elevation".Translate().ToString(), new[]
                {
                    ("Mountain", "Mountain (Landform)"),
                    ("Valley", "Valley"),
                    ("Basin", "Basin"),
                    ("Plateau", "Plateau"),
                    ("Hollow", "Hollow"),
                    ("Caves", "Caves"),
                    ("Cavern", "Cavern"),
                    ("LavaCaves", "Lava Caves"),
                    ("LavaCrater", "Lava Crater"),
                    ("LavaFlow", "Lava Flow"),
                    ("Cliffs", "Cliffs"),
                    ("Chasm", "Chasm"),
                    ("Crevasse", "Crevasse"),
                    ("Dunes", "Dunes"),
                }),
                ("LandingZone_MutatorCategory_ResourceModifiers".Translate().ToString(), new[]
                {
                    ("Fertile", "Fertile Soil"),
                    ("MineralRich", "Mineral Rich"),
                    ("SteamGeysers_Increased", "Steam Geysers+"),
                    ("AncientHeatVent", "Ancient Heat Vent"),
                    ("AnimalLife_Increased", "Animal Life+"),
                    ("AnimalLife_Decreased", "Animal Life-"),
                    ("PlantLife_Increased", "Plant Life+"),
                    ("PlantLife_Decreased", "Plant Life-"),
                    ("Fish_Increased", "Fish+"),
                    ("Fish_Decreased", "Fish-"),
                    ("DryGround", "Dry Ground"),
                    ("DryLake", "Dry Lake"),
                }),
                ("LandingZone_MutatorCategory_FloraWildlife".Translate().ToString(), new[]
                {
                    ("WildPlants", "Wild Plants"),
                    ("WildTropicalPlants", "Wild Tropical Plants"),
                    ("PlantGrove", "Plant Grove"),
                    ("ArcheanTrees", "Archean Trees"),
                    ("ObsidianDeposits", "Obsidian Deposits"),
                    ("AnimalHabitat", "Animal Habitat"),
                }),
                ("LandingZone_MutatorCategory_TerrainTypes".Translate().ToString(), new[]
                {
                    ("Muddy", "Muddy"),
                    ("Marshy", "Marshy"),
                    ("Sandy", "Sandy"),
                    ("Wetland", "Wetland"),
                    ("MixedBiome", "Mixed Biome"),
                }),
                ("LandingZone_MutatorCategory_RuinsSalvage".Translate().ToString(), new[]
                {
                    ("AncientRuins", "Ancient Ruins"),
                    ("AncientRuins_Frozen", "Ancient Ruins (Frozen)"),
                    ("AncientQuarry", "Ancient Quarry"),
                    ("AncientWarehouse", "Ancient Warehouse"),
                    ("AncientUplink", "Ancient Uplink"),
                    ("Stockpile", "Stockpile"),
                    ("Junkyard", "Junkyard"),
                    ("TerraformingScar", "Terraforming Scar"),
                }),
                ("LandingZone_MutatorCategory_AbandonedColonies".Translate().ToString(), new[]
                {
                    ("AbandonedColonyOutlander", "Abandoned Outlander Colony"),
                    ("AbandonedColonyTribal", "Abandoned Tribal Colony"),
                }),
                ("LandingZone_MutatorCategory_Hazards".Translate().ToString(), new[]
                {
                    ("AncientInfestedSettlement", "Infested Settlement"),
                    ("InsectMegahive", "Insect Megahive"),
                    ("AncientToxVent", "Ancient Tox Vent"),
                    ("AncientSmokeVent", "Ancient Smoke Vent"),
                    ("AncientChemfuelRefinery", "Ancient Chemfuel Refinery"),
                    ("AncientGarrison", "Ancient Garrison"),
                    ("AncientLaunchSite", "Ancient Launch Site"),
                }),
            };

            // Track which mutators we've categorized
            var categorized = new HashSet<string>();

            foreach (var (category, mutatorDefs) in categoryDefinitions)
            {
                var entries = new List<MutatorEntry>();
                foreach (var (defName, displayName) in mutatorDefs)
                {
                    int defaultRating = MutatorQualityRatings.GetDefaultQuality(defName);
                    entries.Add(new MutatorEntry(defName, displayName, defaultRating));
                    categorized.Add(defName);
                }

                if (entries.Count > 0)
                {
                    result.Add((category, entries));
                }
            }

            // Add any remaining mutators from the ratings dictionary that weren't categorized
            var uncategorized = new List<MutatorEntry>();
            foreach (var mutatorName in MutatorQualityRatings.GetAllRatedMutators())
            {
                if (!categorized.Contains(mutatorName))
                {
                    int defaultRating = MutatorQualityRatings.GetDefaultQuality(mutatorName);
                    uncategorized.Add(new MutatorEntry(mutatorName, mutatorName, defaultRating));
                }
            }

            if (uncategorized.Count > 0)
            {
                result.Add(("LandingZone_MutatorCategory_Other".Translate().ToString(), uncategorized.OrderBy(m => m.DisplayName).ToList()));
            }

            return result;
        }
    }
}

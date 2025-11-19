# LandingZone Three-Tier UX Specification
**Version:** v0.2.0-draft
**Date:** 2025-11-17
**Status:** Draft for Implementation

## Executive Summary

LandingZone's user experience is redesigned as a progressive disclosure system with three tiers optimized for different user expertise levels and use cases. Each tier provides increasing control and complexity, allowing users to graduate naturally from quick presets to advanced custom configurations.

---

## Tier 1: Preset (Quick Start)

**Target User:** New players, quick starts, common scenarios
**Complexity:** Minimal - click and go
**Time to Result:** <30 seconds

### Design Philosophy

Curated, one-click solutions for 90% of landing site scenarios. Users select a preset card, optionally tweak 1-2 high-level settings, and search immediately. No filter knowledge required.

### UI Layout

**4-column grid** of preset cards (12 curated + user-saved)

```
┌─────────────┬─────────────┬─────────────┬─────────────┐
│  Elysian    │  Exotic     │  SubZero    │  Scorched   │  ← Row 1: Special
├─────────────┼─────────────┼─────────────┼─────────────┤
│Desert Oasis │  Defense    │  Agrarian   │   Power     │  ← Row 2: Curated
├─────────────┼─────────────┼─────────────┼─────────────┤
│   Bayou     │  Savannah   │  Aquatic    │ Homesteader │  ← Row 3: Curated
└─────────────┴─────────────┴─────────────┴─────────────┘
                    ↓ Scroll for user presets ↓
```

### Preset Card Components

Each card shows:
1. **Name** (large, bold)
2. **Icon/Color** (visual identifier - e.g., snowflake for SubZero, fire for Scorched)
3. **One-line description** (what makes this special)
4. **Filter summary** (key constraints, e.g., "Desert | Rivers | Hot")
5. **Rarity badge** (Common/Uncommon/Rare/VeryRare/Epic) - sets user expectation for result count
6. **[Search] button** - immediate action

### Optional Quick Tweaks (Collapsed by Default)

After selecting a preset, show collapsible "Quick Tweaks" panel:
- **Result Count** slider (10-100, default 25)
- **Temperature bias** (-10°C to +10°C offset from preset default)
- **Biome filter** (optional lock to specific biome if preset allows)

### Features

- ✅ **Zero configuration required** - preset handles all filter logic
- ✅ **Mutator quality overrides** - presets can invert global ratings (e.g., Scorched values lava features)
- ✅ **Fallback tiers** - Exotic/rare presets automatically loosen constraints if zero results
- ✅ **Save custom** - "Save as Preset" button to persist tweaked settings
- ✅ **Import/Export** - Share preset JSON files with community

### Technical Implementation

- Presets defined in `Source/Data/Preset.cs` (currently exists)
- Each preset = `FilterSettings` bundle + `MutatorQualityOverrides` + optional `FallbackTiers`
- User presets stored in save file's `UserPreferences.SavedPresets`
- Preset cards render via `PresetLibrary.GetCurated()` + `GetUserPresets()`

---

## Tier 2: Guided Builder (Goals → Filters)

**Target User:** Intermediate players, specific but common goals
**Complexity:** Medium - structured wizard
**Time to Result:** 2-5 minutes

### Design Philosophy

Bridge between presets and full customization. Users express goals in plain language ("I want good farming"), system suggests relevant filter configurations, users tweak and combine.

### UI Flow

**Step 1: Choose Goal Category**

```
┌──────────────────────────────────────────────────────────┐
│  What's your priority for this colony?                  │
│                                                           │
│  [Climate Comfort]  [Resource Wealth]  [Defensibility]   │
│  [Food Production]  [Power Generation] [Trade Access]    │
│  [Challenge/Rarity] [Specific Feature]                   │
└──────────────────────────────────────────────────────────┘
```

**Step 2: Refine Goal** (context-dependent questions)

Example for "Food Production":
```
┌──────────────────────────────────────────────────────────┐
│  What type of food production?                           │
│                                                           │
│  ○ Farming (long growing season, fertile soil)           │
│  ○ Hunting (abundant wildlife)                           │
│  ○ Fishing (coastal + rivers, high fish population)      │
│  ○ Mixed (balance all three)                             │
└──────────────────────────────────────────────────────────┘
```

**Step 3: Filter Snippet Preview**

Show **generated filter configuration** with explanations:
```
┌──────────────────────────────────────────────────────────┐
│  Recommended Filters (Farming Focus):                    │
│                                                           │
│  🔴 Critical:                                            │
│    • Growing Days: 50-60 days/year (year-round crops)   │
│    • Rainfall: 1200-2500mm (consistent moisture)         │
│                                                           │
│  🔵 Preferred:                                           │
│    • Temperature: 15-28°C (optimal crop range)           │
│    • Fertile soil mutator (faster growth)                │
│    • Rivers OR coastal (irrigation + fishing backup)     │
│                                                           │
│  [Tweak Filters] [Search Now] [← Back]                  │
└──────────────────────────────────────────────────────────┘
```

**Step 4: Optional Tweaks** (loads Tier 3 with pre-configured filters)

Clicking "Tweak Filters" transitions to Advanced Studio with the generated config pre-loaded.

### Goal Templates

Pre-defined goal → filter mappings:

| Goal Category | Key Filters (Critical) | Bonus Filters (Preferred) |
|--------------|------------------------|---------------------------|
| **Climate Comfort** | Temp 10-25°C, Rain 600-1400mm | SunnyMutator, WetClimate |
| **Resource Wealth** | MineralRich, High Forage | WildPlants, AnimalLife_Increased |
| **Defensibility** | Mountain/Caves, Hilliness=Mountainous | Cliffs, Peninsula, Chasm |
| **Food: Farming** | GrowingDays 50-60, Rain 1200+ | Fertile, Muddy, Rivers |
| **Food: Hunting** | AnimalDensity 3.0+, AnimalLife_Increased | AnimalHabitat, WildPlants |
| **Power Generation** | SteamGeysers_Increased | WindyMutator, Rivers (hydro), Uranium |
| **Trade Access** | Coastal OR Roads | Harbor, Bay, AncientAsphaltRoad |

### Features

- ✅ **Natural language goals** - no filter jargon required
- ✅ **Context-aware questions** - each goal has custom refinement UI
- ✅ **Educational** - shows *why* filters were chosen with tooltips
- ✅ **Escape hatch to Advanced** - "Tweak Filters" graduates user to Tier 3
- ✅ **Saves as preset** - "Save this Configuration" creates user preset

### Technical Implementation

- Goal templates defined in new `Source/Core/UI/GoalTemplates.cs`
- Each goal = GoalCategory enum + refinement questions + FilterSettings generator
- UI flow: GoalSelectionWindow → RefinementWindow → PreviewWindow
- Preview window can launch Advanced Studio with pre-loaded FilterSettings

---

## Tier 3: Advanced Studio (Full Control)

**Target User:** Power users, modders, edge cases
**Complexity:** High - full filter access
**Time to Result:** 5-30 minutes (complex configs)

### Design Philosophy

Expose all filters with grouped organization, live feedback, conflict detection, and fallback chain management. For users who know exactly what they want or need combinations not covered by presets/goals.

### UI Layout (Tabbed Sections)

```
┌─────────────────────────────────────────────────────────────────┐
│ [Climate] [Geography] [Resources] [Features] [Results Control]  │ ← Tabs
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  === CLIMATE FILTERS ===                                        │
│                                                                  │
│  Average Temperature: [━━━●━━━] 10°C - 32°C                     │
│    Importance: ( ) Ignored (●) Preferred ( ) Critical           │
│    📊 Match: ~45% of settleable tiles (127k / 295k)             │
│                                                                  │
│  Rainfall: [━━━━━●] 1000 - 2200 mm/year                         │
│    Importance: ( ) Ignored (●) Preferred ( ) Critical           │
│    📊 Match: ~38% of settleable tiles                           │
│                                                                  │
│  ⚠ Conflict Warning: Temperature + Rainfall + GrowingDays       │
│     restrict to 12% of tiles (too narrow?)                      │
│     [Suggest Loosening]                                         │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Filter Grouping (by Tab)

**1. Climate Tab**
- Average Temperature (range, importance)
- Min/Max Temperature (heavy)
- Rainfall (range, importance)
- Growing Days (range, importance)
- Pollution (range, importance)

**2. Geography Tab**
- Hilliness (multi-select checkboxes)
- Coastal (importance)
- Coastal Lake (importance)
- Water Access (importance - combined coastal/river check)
- Movement Difficulty (range, importance)
- Swampiness (range, importance)

**3. Resources Tab**
- Stones (multi-select with importance per stone, AND/OR operator)
- Forageability (range, importance)
- Plant Density (range, importance)
- Animal Density (range, importance)
- Fish Population (range, importance)
- Grazing (boolean, importance)

**4. Features Tab**
- Map Features (multi-select with importance per mutator, AND/OR operator)
  - Grouped by type: Positive (+10 to +5), Geographic (neutral), Negative (-5 to -10)
  - Show rarity % next to each (e.g., "ArcheanTrees (0.0034%)")
- Rivers (multi-select with importance, AND/OR operator)
- Roads (multi-select with importance, AND/OR operator)

**5. Results Control Tab**
- Result Count (slider 10-200)
- Strictness slider (0.0 = fuzzy, 1.0 = hard enforcement)
- Fallback Tiers (add/remove/reorder loosening stages)

### Critical vs. Preferred Styling

**Visual Differentiation:**
- 🔴 **Critical** filters = red accent, bold label, "Hard requirement" tooltip
- 🔵 **Preferred** filters = blue accent, normal weight, "Scoring bonus" tooltip

**Behavior:**
- Critical: Tiles must pass to enter candidate set (Apply phase)
- Preferred: Tiles score higher if they match (Score phase)

### Live Feedback Panel (Right Sidebar)

```
┌────────────────────────────┐
│  LIVE FILTER PREVIEW       │
├────────────────────────────┤
│  Estimated Matches:        │
│    ~18,500 tiles (6.3%)    │
│                            │
│  Critical Filters (3):     │
│    ✓ Temperature           │
│    ✓ Rainfall              │
│    ✓ Mountain/Caves        │
│                            │
│  Preferred Filters (5):    │
│    • GrowingDays           │
│    • MineralRich           │
│    • SteamGeysers          │
│    • Uranium ore           │
│    • Plasteel ore          │
│                            │
│  ⚠ Warnings:               │
│    • Uranium + Plasteel    │
│      together rare (0.2%)  │
│      [Suggest OR operator] │
│                            │
│  [Search Now]              │
└────────────────────────────┘
```

### Grouped AND/OR Logic

**Example: Stones filter**

```
Stones (Operator: OR)
  [●] MineablePlasteel     Importance: (●) Critical
  [●] MineableUranium      Importance: (●) Critical
  [ ] MineableGold         Importance: ( ) Ignored

Operator: (●) OR  ( ) AND

Info: OR = accept tiles with ANY selected stone
      AND = require tiles with ALL selected stones (very restrictive)
```

### Conflict Detection & Suggestions

System analyzes filter combinations and warns:
- **Too restrictive** (estimated <100 results): Suggest loosening Critical → Preferred
- **Contradictory** (e.g., Temp<0°C + GrowingDays>50): Highlight conflict, suggest resolution
- **Ineffective Preferred** (all tiles already pass): Suggest removing redundant filter
- **Impossible AND logic** (e.g., Plasteel AND Uranium AND Gold - tiles only have 1 ore): Suggest OR operator

### Fallback Chain Manager

For ultra-restrictive configs, users can define fallback tiers:

```
┌──────────────────────────────────────────────────────┐
│  Fallback Tiers (searched in order if zero results)  │
├──────────────────────────────────────────────────────┤
│  Tier 1 (Primary):                                   │
│    Critical: ArcheanTrees, MineralRich, Fertile      │
│    [Edit] [▲] [▼] [×]                                │
│                                                       │
│  Tier 2 (Fallback):                                  │
│    Critical: Any ultra-rare mutator (OR)             │
│    [Edit] [▲] [▼] [×]                                │
│                                                       │
│  [+ Add Fallback Tier]                               │
└──────────────────────────────────────────────────────┘
```

### Features

- ✅ **Full filter access** - every filter from FilterSettings exposed
- ✅ **Live match estimates** - approximate result count as you configure
- ✅ **Conflict warnings** - intelligent validation with suggestions
- ✅ **Grouped layout** - filters organized by category for scannability
- ✅ **AND/OR operators** - per multi-select filter (Stones, MapFeatures, Rivers, Roads)
- ✅ **Strictness control** - global fuzzy-to-hard slider
- ✅ **Fallback chains** - multi-tier loosening for rare feature hunting
- ✅ **Save as preset** - export config to Tier 1 preset library
- ✅ **Import goal templates** - start from Tier 2 goal and refine

### Technical Implementation

- Extends existing `LandingZonePreferencesWindow.cs` Advanced mode
- Add live match estimation via `FilterSelectivityAnalyzer` (estimate candidate set size)
- Conflict detection via new `FilterConflictDetector.cs`:
  - Analyze Critical filter combinations
  - Check for impossible AND logic (e.g., multi-select exclusivity)
  - Estimate result counts via heuristics (biome %, mutator rarity, etc.)
- Fallback tier UI via `FallbackTierEditorPanel` (add/edit/reorder tiers)

---

## Tier Progression & Transitions

### Natural Graduation Path

```
Preset (Tier 1)
    ↓ "Tweak this preset" OR "Not quite right, want more control"
    ↓
Guided Builder (Tier 2)
    ↓ "Advanced tweaking needed" OR "Learn how filters work"
    ↓
Advanced Studio (Tier 3)
    ↓ "Save for reuse" → creates new Tier 1 preset
    ↓
Preset (Tier 1) with custom preset
```

### Cross-Tier Features

- **Save anywhere** - all tiers can save current config as preset
- **Import preset to Advanced** - load any preset into Tier 3 for modification
- **Template to Advanced** - Tier 2 goals pre-load Tier 3
- **Preset metadata** - track which tier created the preset (for analytics/UX refinement)

---

## Implementation Phases

### Phase 1: Foundation (Current State)
- ✅ Tier 1 working (12 curated presets, user preset save/load)
- ✅ Tier 3 partially implemented (Advanced mode exists, needs live feedback)

### Phase 2: Tier 1 Polish
- [ ] 4-column grid layout for preset cards
- [ ] Rarity badges on cards
- [ ] Quick Tweaks collapsible panel
- [ ] Preset import/export (JSON files)

### Phase 3: Tier 3 Enhancements
- [ ] Live match estimation in right sidebar
- [ ] Conflict detection with warnings
- [ ] Fallback tier editor UI
- [ ] Grouped filter tabs (Climate/Geography/Resources/Features/Results)
- [ ] Critical vs. Preferred visual styling (red/blue accents)

### Phase 4: Tier 2 Introduction
- [ ] Goal category selection UI
- [ ] Goal template definitions (GoalTemplates.cs)
- [ ] Refinement question flows per goal
- [ ] Filter snippet preview window
- [ ] "Tweak Filters" → Advanced Studio transition

### Phase 5: Integration & Testing
- [ ] Tier progression analytics (track which paths users take)
- [ ] In-game tutorials for each tier
- [ ] Community preset sharing (Workshop integration?)

---

## Success Metrics

- **Tier 1**: 80% of searches use presets, <30s time-to-search
- **Tier 2**: 50% of "custom" searches start from goals, 70% of goal users graduate to Advanced
- **Tier 3**: 90% of Advanced users save at least one custom preset, <5% encounter zero results
- **Cross-Tier**: 60% of users try multiple tiers within first 10 searches

---

## Appendices

### A. Filter Variable Catalog Integration

All three tiers pull from `docs/data/filter_variables_catalog.{json,md}`:
- **Tier 1**: Presets reference validated defNames (biomes, mutators, ores, rivers, roads)
- **Tier 2**: Goal templates use canonical names + rarity data for suggestions
- **Tier 3**: Advanced UI shows rarity % and DLC requirements from catalog

### B. Logging & Debugging

**Dev Mode Tools** (accessible in all tiers when Prefs.DevMode = true):
- Dump current FilterSettings to JSON
- Log estimated vs. actual result counts (validate selectivity analyzer)
- Export preset for bug reports
- Performance profiling for filter Apply/Score phases

### C. Accessibility Considerations

- Color-blind friendly: Critical/Preferred use shapes + labels, not just red/blue
- Keyboard navigation: All UI navigable via Tab/Enter/Arrow keys
- Screen reader support: ARIA labels for all interactive elements
- High contrast mode: Preset cards have sufficient contrast ratios
- Font scaling: UI adapts to RimWorld's font size settings

---

**End of Specification**

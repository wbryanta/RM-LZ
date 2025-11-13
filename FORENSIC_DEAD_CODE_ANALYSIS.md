# Forensic Dead Code Analysis: LandingZonePreferencesWindow.cs

**Date:** 2025-11-13
**Reason:** Post-integration cleanup after switching to DefaultModeUI and AdvancedModeUI
**Method:** Exhaustive grep-based call chain tracing

## Executive Summary

After integrating the new DefaultModeUI and AdvancedModeUI renderers, approximately **900-1000 lines** of legacy code in `LandingZonePreferencesWindow.cs` became dead code. This analysis forensically validates each component before removal.

## Call Chain Analysis

### OLD SYSTEM (Pre-Integration)
```
DrawAdvancedModeContent()
  └─> DrawSection("Temperature", DrawTemperatureSection)
      └─> GetActiveSectionFilterCount("Temperature")
          └─> CountTemperatureFilters()
              └─> Uses: _avgTemperature, _avgTemperatureImportance, etc.
  └─> DrawSection("Climate & Environment", DrawClimateEnvironmentSection)
  └─> DrawSection("Terrain & Hilliness", DrawTerrainSection)
  └─> DrawSection("Geography & Hydrology", DrawGeographySection)
  └─> DrawSection("Resources & Grazing", DrawResourcesSection)
  └─> DrawSection("World Features", DrawWorldFeaturesSection)
      └─> DrawFeatureGroup() [if _featureImportance != Ignored]
          └─> Uses: _featureBuckets, _featureLookup, FeatureEntry
  └─> DrawSection("Results", DrawResultsSection)
      └─> DrawMaxResults()
          └─> Uses: _maxResults
  └─> PersistFilters() [on button click or window close]
      └─> Writes all local variables back to FilterSettings
  └─> ResetFilters() [on button click]
      └─> Reads FilterSettings into all local variables
```

### NEW SYSTEM (Post-Integration)
```
DrawAdvancedModeContent()
  └─> AdvancedModeUI.DrawContent(preferences)
      └─> Modifies FilterSettings DIRECTLY (no local variables)
  └─> Reset button: preferences.Filters.Reset() DIRECTLY
```

## Dead Code Categories

### 1. Local Filter State Variables (DEAD)

**Lines 58-98**: All private filter state fields

**Evidence:**
- Initialized in constructor (lines 178-226)
- Used only in: CountXFilters(), DrawXSection(), PersistFilters(), ResetFilters()
- **None of these methods are called anymore** (see Method Analysis below)

**Affected Variables:**
```csharp
// Temperature (6 fields)
_avgTemperature, _minTemperature, _maxTemperature
_avgTemperatureImportance, _minTemperatureImportance, _maxTemperatureImportance

// Climate (4 fields)
_rainfall, _growingDays
_rainfallImportance, _growingDaysImportance

// Environment (4 fields)
_pollution, _forage
_pollutionImportance, _forageImportance

// Terrain (5 fields)
_movement, _elevation
_movementImportance, _elevationImportance
_selectedHilliness

// Geography (2 fields)
_coastalImportance, _coastalLakeImportance

// Resources (3 fields)
_grazeImportance, _forageableFoodImportance, _forageableFoodDefName

// World features (3 fields)
_featureImportance, _landmarkImportance, _featureDefName
```

**Total:** 27 local state fields → **DEAD**

---

### 2. Feature Classification System (DEAD)

**Lines 16-55**: Old FeatureDef dropdown system

**Evidence:**
```bash
$ grep "DrawFeatureGroup\(" LandingZonePreferencesWindow.cs
634:    DrawFeatureGroup(listing, category, title);  # Called from DrawWorldFeaturesSection
797: private void DrawFeatureGroup(...)              # Method definition

$ grep "DrawWorldFeaturesSection\(" LandingZonePreferencesWindow.cs
620: private void DrawWorldFeaturesSection(...)      # Method definition ONLY, never called
```

**Call Chain:**
- `_featureOptions` populated in constructor (lines 124-150)
- Used only in `DrawFeatureGroup()` → called from `DrawWorldFeaturesSection()` → **never called**

**Affected Code:**
```csharp
private readonly List<FeatureEntry> _featureOptions;
private readonly Dictionary<FeatureCategory, List<FeatureEntry>> _featureBuckets;
private readonly Dictionary<string, FeatureEntry> _featureLookup;
private readonly List<ThingDef> _stoneOptions;
private readonly Hilliness[] _hillinessOptions;
private static readonly (FeatureCategory, string)[] FeatureGroups;

private struct FeatureEntry { ... }
private enum FeatureCategory { Geological, Resource, PointOfInterest, Other }
```

**Total:** 6 fields, 1 struct, 1 enum, 1 static array → **DEAD**

**Reason:** AdvancedModeUI uses `MapFeatureFilter.GetAllMapFeatureTypes()` directly (canonical SSoT), not the old feature classification dropdown system.

---

### 3. UI State Variables (PARTIALLY DEAD)

**Lines 101-114**:

| Variable | Status | Evidence |
|----------|--------|----------|
| `_scrollPos` | **ALIVE** | Used in both DrawDefaultModeContent() and DrawAdvancedModeContent() |
| `_contentHeight` | **DEAD** | Line 102: warning CS0414 "assigned but its value is never used" |
| `_maxResults` | **DEAD** | Used only in DrawMaxResults() → called from DrawResultsSection() → never called |
| `_dirty` | **DEAD** | Set in 14 places, checked only in PersistFilters() → never called |
| `SectionExpanded` | **DEAD** | Used only in DrawSection() → never called |

**Dead UI state:** 4 fields + 1 static dictionary → **DEAD**

---

### 4. Rendering Methods (DEAD)

**Evidence:** None of these methods are called anywhere except their own definitions.

```bash
$ grep -n "DrawSection\(" LandingZonePreferencesWindow.cs
385: private void DrawSection(...)  # Definition ONLY

$ grep -n "DrawTemperatureSection\(" LandingZonePreferencesWindow.cs
491: private void DrawTemperatureSection(...)  # Definition ONLY

# Same for all section methods - defined but never called
```

**Dead Methods List:**

| Method | Lines | Purpose | Called By |
|--------|-------|---------|-----------|
| `DrawSection` | 385-410 | Generic section renderer | **NOWHERE** (was in old DrawAdvancedModeContent) |
| `GetActiveSectionFilterCount` | 413-423 | Count active filters | DrawSection → **DEAD** |
| `CountTemperatureFilters` | 427-433 | Count temp filters | GetActiveSectionFilterCount → **DEAD** |
| `CountClimateFilters` | 435-443 | Count climate filters | GetActiveSectionFilterCount → **DEAD** |
| `CountTerrainFilters` | 445-452 | Count terrain filters | GetActiveSectionFilterCount → **DEAD** |
| `CountGeographyFilters` | 454-465 | Count geography filters | GetActiveSectionFilterCount → **DEAD** |
| `CountResourceFilters` | 467-475 | Count resource filters | GetActiveSectionFilterCount → **DEAD** |
| `CountWorldFeatureFilters` | 477-488 | Count world feature filters | GetActiveSectionFilterCount → **DEAD** |
| `DrawTemperatureSection` | 491-504 | Render temperature filters | DrawSection → **DEAD** |
| `DrawClimateEnvironmentSection` | 506-517 | Render climate filters | DrawSection → **DEAD** |
| `DrawTerrainSection` | 519-531 | Render terrain filters | DrawSection → **DEAD** |
| `DrawGeographySection` | 533-605 | Render geography filters | DrawSection → **DEAD** |
| `DrawResourcesSection` | 607-618 | Render resource filters | DrawSection → **DEAD** |
| `DrawWorldFeaturesSection` | 620-706 | Render world features | DrawSection → **DEAD** |
| `DrawResultsSection` | 710-714 | Render results section | DrawSection → **DEAD** |
| `DrawFloatRange` | 716-735 | Range slider helper | DrawRangeWithImportance → **DEAD** |
| `DrawRangeWithImportance` | 736-755 | Range + importance control | DrawXSection methods → **DEAD** |
| `DrawBooleanImportance` | 757-765 | Boolean importance selector | DrawXSection methods → **DEAD** |
| `DrawTemperatureRange` | 767-795 | Temperature with F/C conversion | DrawTemperatureSection → **DEAD** |
| `DrawFeatureGroup` | 797-829 | Feature dropdown | DrawWorldFeaturesSection → **DEAD** |
| `ResolveCurrentFeatureLabel` | 831-848 | Get current feature label | DrawFeatureGroup → **DEAD** |
| `DrawStoneSelectors` | 851-856 | Stone UI (stubbed) | DrawResourcesSection → **DEAD** |
| `DrawHillinessOptions` | 858-938 | Hilliness checkboxes | DrawTerrainSection → **DEAD** |
| `DrawEvaluationSummary` | 940-973 | Show evaluation status | DrawResultsSection → **DEAD** |
| `PersistFilters` | 976-1036 | Write local vars to FilterSettings | **NOWHERE** (was in PreClose + button) |
| `ResetFilters` | 1038-1100 | Read FilterSettings to local vars | **NOWHERE** (was in button) |
| `DrawMaxResults` | 1113-1122 | Max results slider | DrawResultsSection → **DEAD** |
| `DrawImportanceHeader` | 1123-1136 | Importance header | **NOWHERE** (unused helper) |
| `ResolveFeatureLabel` | 1170-1177 | Get feature display label | Constructor init → used only for dead _featureOptions |
| `ClassifyFeature` | 1179-1202 | Categorize features | Constructor init → used only for dead _featureBuckets |
| `ContainsAny` | 1200-1207 | String contains helper | ClassifyFeature → **DEAD** |

**Total Dead Methods:** 31 methods → **DEAD**

---

### 5. Static Helper Methods (PARTIALLY DEAD)

| Method | Lines | Status | Used By |
|--------|-------|--------|---------|
| `NextImportance` | 1138-1146 | **DEAD** | DrawImportanceHeader → never called |
| `ImportanceIcon` | 1148-1157 | **DEAD** | DrawImportanceHeader → never called |
| `ImportanceDescription` | 1159-1168 | **DEAD** | DrawImportanceHeader → never called |
| `ToDisplayTemp` | 1103-1106 | **DEAD** | DrawTemperatureRange → never called |
| `FromDisplayTemp` | 1108-1111 | **DEAD** | DrawTemperatureRange → never called |

**Total:** 5 static helper methods → **DEAD**

---

### 6. Constructor Initialization (PARTIALLY DEAD)

**Lines 124-226**: Constructor populates all the dead variables

**Dead initialization blocks:**
- Lines 124-150: `_featureOptions`, `_featureBuckets`, `_featureLookup` population
- Lines 155-174: `_stoneOptions` filtering (for dead DrawStoneSelectors)
- Lines 176-226: All local filter variable initialization

**Alive initialization:**
- Lines 118-122: Window properties (closeOnClickedOutside, etc.) → **ALIVE**

---

## Summary Statistics

| Category | Count | Lines Affected |
|----------|-------|----------------|
| Dead local state fields | 27 | ~40 lines |
| Dead feature system (fields + types) | 6 fields + 2 types | ~55 lines |
| Dead UI state fields | 4 fields + 1 dict | ~13 lines |
| Dead rendering methods | 31 methods | ~850 lines |
| Dead constructor code | ~100 lines | ~100 lines |
| **TOTAL DEAD CODE** | **~1,058 lines** | **~1,058 lines** |

## Validation Method

For each component, validated dead status using:

```bash
# 1. Find all references to identifier
grep -n "IdentifierName" LandingZonePreferencesWindow.cs

# 2. Categorize references as:
#    - Definition (method signature, field declaration)
#    - Assignment (initialization, setting)
#    - Usage (method call, property read)

# 3. If only Definition + Assignment (no Usage) → DEAD
# 4. If Usage exists, trace caller → if caller is dead, this is dead too
```

## Conclusion

**All identified code is provably dead** via exhaustive call chain tracing. Safe to remove.

**Root Cause:** Integration of DefaultModeUI and AdvancedModeUI changed the architecture:
- **Before:** Local variables + PersistFilters() on close
- **After:** Direct FilterSettings modification

The old section-based rendering system became a 1,000+ line orphan.

## Removal Plan

1. Remove all dead methods (lines ~385-1207)
2. Remove dead fields (lines 16-19, 58-98, 102-104)
3. Remove dead types (FeatureEntry struct, FeatureCategory enum)
4. Remove dead constructor initialization (lines 124-226 except 118-122)
5. Keep: `_scrollPos` (still used in both modes)
6. Build and verify: 0 compilation errors

**Expected LOC reduction:** ~1,000 lines → ~200 lines (80% reduction)

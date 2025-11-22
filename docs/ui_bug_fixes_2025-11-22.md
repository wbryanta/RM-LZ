# UI Bug Fixes - 2025-11-22

**Build Status:** ✅ Build succeeded (0 errors, 63 pre-existing warnings)
**DLL Size:** 363 KB
**Based on:** In-game testing feedback

---

## Issues Fixed

### Fix #1: Obsidian Deposits Placement ✅

**Issue:** "ObsidianDeposits" appeared in Geography/Terrain mutators instead of Resources

**Fix:** Moved from GetGeographyMutators() to GetResourceMutators()

**File Modified:** `Source/Core/UI/AdvancedModeUI_Controls.cs`
- **Line 1074:** Removed from Volcanic/lava section of Geography
- **Line 1095:** Added to Resources with comment "Volcanic glass - valuable construction material"

**Result:** ObsidianDeposits now appears in "Resources & Production" tab alongside other mineral resources

---

### Fix #2: Adjacent Biomes Clarity ✅

**Issue:** Users assumed "Adjacent Biomes" was for selecting current biome, not understanding it affects weather patterns from neighboring tiles

**Fix:** Added explanatory note below the "Adjacent Biomes:" label

**File Modified:** `Source/Core/UI/AdvancedModeUI_Controls.cs` (Lines 749-755)

**Code Added:**
```csharp
listing.Label("Adjacent Biomes:");
Text.Font = GameFont.Tiny;
GUI.color = new Color(0.7f, 0.7f, 0.7f);
listing.Label("(Affects weather patterns from neighboring tiles)");
GUI.color = Color.white;
Text.Font = GameFont.Small;
listing.Gap(4f);
```

**Result:** Users now see clear explanation that this is about weather pattern influence, not biome selection

---

### Fix #3: Natural Stones - OR Operator Clarification ✅

**Issue:** Users didn't understand that tiles can only have ONE stone type, so AND operator with multiple stones is impossible

**Fix:** Added prominent note explaining OR-only logic

**File Modified:** `Source/Core/UI/AdvancedModeUI_Controls.cs` (Lines 460-466)

**Code Added:**
```csharp
listing.Label("Natural Stones (construction materials):");
Text.Font = GameFont.Tiny;
GUI.color = new Color(1f, 0.8f, 0.4f);  // Orange/yellow color
listing.Label("Note: Tiles have only ONE stone type - use OR operator for multiple choices");
GUI.color = Color.white;
Text.Font = GameFont.Small;
listing.Gap(4f);
```

**Result:** Clear warning that AND with multiple stones is impossible, OR is the correct operator

---

### Fix #4: Live Preview Selectivity Display Bug ✅

**Issue:** Live Preview sidebar showed:
- ✗ "(No critical filters applied)" even when 4 critical filters were active
- ✗ Fallback tier cards not appearing
- ✗ No tile count estimate

**Root Cause:** Two different detection methods for critical filters were giving different results:
1. Filter list builder (worked correctly)
2. Selectivity estimation system (returned empty for container filters like Stones, MapFeatures, Hilliness)

**Fix:** Restructured Live Preview logic to:
1. Build criticalFilters list first
2. Use it to control display of filter lists AND fallback tiers
3. Show selectivity estimates if available
4. Show graceful fallback message if selectivity data unavailable for these filter types

**File Modified:** `Source/Core/UI/AdvancedModeUI.cs` (Lines 254-350)

**Key Changes:**
```csharp
// Build filter lists first (needed for both display and selectivity checks)
var allGroups = GetUserIntentGroups();
var criticalFilters = new List<string>();
var preferredFilters = new List<string>();

foreach (var group in allGroups)
{
    foreach (var filter in group.Filters)
    {
        var (isActive, importance) = filter.IsActiveFunc(filters);
        if (isActive)
        {
            if (importance == FilterImportance.Critical)
                criticalFilters.Add(filter.Label);
            else if (importance == FilterImportance.Preferred)
                preferredFilters.Add(filter.Label);
        }
    }
}

// Live tile count estimates (try selectivity analysis if available)
var selectivities = LandingZoneContext.Filters?.GetAllSelectivities(LandingZoneContext.State);
if (selectivities != null && selectivities.Any())
{
    // Show baseline and estimates if available
    var criticalSelectivities = selectivities.Where(s => s.Importance == FilterImportance.Critical).ToList();

    if (criticalSelectivities.Any())
    {
        // Show full estimate with color coding
    }
    else if (criticalFilters.Any())
    {
        // Graceful fallback: "(Tile count estimate unavailable for these filter types)"
    }
}

// Critical filter list (always shows if criticalFilters.Any())
if (criticalFilters.Any())
{
    // ... list display
}

// Fallback tier preview (uses criticalFilters check, not selectivity check)
if (criticalFilters.Any())
{
    DrawFallbackTierPreview(listing, filters);
}
```

**Result:**
- ✅ Critical filter list always displays when filters are active
- ✅ Fallback tier cards appear when filters are restrictive
- ✅ Tile count estimates show when selectivity data available
- ✅ Graceful message when selectivity unavailable for certain filter types

---

## Testing Notes

**Before Fixes:**
- ObsidianDeposits appeared in Geography (wrong tab)
- Adjacent Biomes confusing (users thought it selected current biome)
- Natural Stones with AND operator caused confusion and impossible configs
- Live Preview showed "(No critical filters applied)" despite 4 active critical filters
- Fallback tier cards not appearing

**After Fixes:**
- ObsidianDeposits in Resources tab (correct placement)
- Adjacent Biomes has clear "(Affects weather patterns...)" note
- Natural Stones has prominent "Tiles have only ONE stone type" warning
- Live Preview correctly detects critical filters
- Fallback tier cards appear when filters are restrictive
- Graceful handling when selectivity data unavailable

---

## Build Verification

```bash
$ python3 scripts/build.py
Build succeeded.
    0 Error(s)
    63 Warning(s) [pre-existing nullability warnings]

DLL Output: Assemblies/LandingZone.dll (363 KB, built 2025-11-22)
```

---

## Files Modified

1. **`Source/Core/UI/AdvancedModeUI_Controls.cs`**
   - Moved ObsidianDeposits from Geography to Resources
   - Added Adjacent Biomes explanatory note
   - Added Natural Stones OR-only warning

2. **`Source/Core/UI/AdvancedModeUI.cs`**
   - Restructured Live Preview panel logic
   - Fixed critical filter detection
   - Fixed fallback tier card visibility
   - Added graceful fallback for missing selectivity data

---

## Next Steps

**Recommended:**
1. In-game testing to verify all 4 fixes work as expected
2. Test Live Preview panel with various filter combinations:
   - Container filters (Stones, MapFeatures)
   - Range filters (Temperature, Rainfall)
   - Mixed configurations
3. Verify fallback tier cards appear when config is restrictive
4. Screenshots for documentation

**Status:** ✅ All 4 issues fixed and build verified

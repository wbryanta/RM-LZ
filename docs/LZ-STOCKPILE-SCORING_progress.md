# LZ-STOCKPILE-SCORING Progress Report

**Date:** 2025-11-19
**Status:** Implementation Framework Complete - Awaiting Investigation Results

---

## Summary

All implementation stubs and framework code for stockpile scoring are complete and building successfully. The system is ready to plug in the actual stockpile resolution once we discover the API via reflection investigation.

---

## Completed Work

### 1. Design Documentation ✅
**File:** `docs/stockpile_scoring_design.md` (6,800 words)

- Comprehensive design document covering:
  - Investigation approach (following MineralRich pattern)
  - Quality rating system (Weapons +8, Medicine +7, Components +6, etc.)
  - Cache structure (already in place in MineralStockpileCache.cs)
  - Filter implementation pattern
  - Scoring integration approach
  - Preset integration recommendations
  - Testing plan
  - Implementation checklist

### 2. Enhanced Investigation Tool ✅
**File:** `Source/Core/UI/DevToolsWindow.cs` (lines 149-315)

**Enhancement:** Probes non-public members using BindingFlags.NonPublic

**Features:**
- Enumerates ALL methods (public + non-public) with visibility labels
- Enumerates ALL properties (public + non-public)
- Enumerates ALL fields (public + non-public)
- Shows declaring type ([this] vs [base]) for each member
- Automatically reads promising field values (stockpile/thing/loot/content keywords)
- Attempts to invoke methods with PlanetTile parameter
- Shows first 10 items for enumerable results

**Fixed Issues:**
- Window height increased from 350px to 450px (button now visible)
- Variable name error fixed (`methods` → `allMethods`)

### 3. StockpileFilter Implementation ✅
**File:** `Source/Core/Filtering/Filters/StockpileFilter.cs` (new file, 165 lines)

**Implementation:**
- Follows StoneFilter pattern exactly
- Implements ISiteFilter interface (Apply, Membership, Describe)
- Uses MineralStockpileCache.GetStockpileTypes() for data access
- Supports Critical/Preferred importance levels
- Supports AND/OR operator logic via IndividualImportanceContainer
- Includes GetStockpileQuality() static method for consistent quality ratings

**Quality Ratings:**
```csharp
Weapons → 8      // High-value armory start
Medicine → 7     // Survival-critical
Components → 6   // Industrial bottleneck
Steel/Plasteel → 6
Chemfuel → 5     // Power/trading
Food → 5         // Survival buffer
Textiles → 4
Stone blocks → 3
Wood → 2
Unknown → 4      // Generic valuable bonus
```

### 4. FilterSettings Integration ✅
**File:** `Source/Data/FilterSettings.cs` (line 97-98)

**Added:**
```csharp
// Stockpiles (individual importance per stockpile type: Weapons, Medicine, Components, etc.)
public IndividualImportanceContainer<string> Stockpiles { get; set; } = new IndividualImportanceContainer<string>();
```

**Pattern:** Identical to Stones/Rivers/Roads/MapFeatures pattern

### 5. Filter Registration ✅
**Files Modified:**
- `Source/Core/Filtering/SiteFilterRegistry.cs` (lines 59, 135-137)
- `Source/Core/Filtering/FilterService.cs` (line 59)

**Changes:**
1. Added StockpileFilter registration in FilterService.RegisterDefaultFilters() (line 59)
2. Added "stockpile" case to SiteFilterRegistry.GetFilterImportance() (lines 135-137)

### 6. Scoring Integration ✅
**File:** `Source/Core/Filtering/FilterService.cs` (lines 972-990)

**Implementation:**
- Follows stone contribution pattern exactly
- Gets stockpile types from MineralStockpileCache.GetStockpileTypes(tileId)
- Filters by user preferences (Critical or Preferred only)
- Uses StockpileFilter.GetStockpileQuality() for consistent ratings
- Adds to mutatorContributions list for MatchBreakdownV2
- Contributions will appear in [DEBUG] Dump Match Data output

### 7. Build Verification ✅
**Build Status:** Success - 42 warnings (nullability only), 0 errors

**Output:** `Assemblies/LandingZone.dll` updated and ready for testing

---

## Pending Work (Blocked on Investigation)

### Phase 2: Cache Resolution (BLOCKED)
**Blocker:** Awaiting in-game investigation results from DevToolsWindow tool

**Target:** `Source/Data/MineralStockpileCache.cs` lines 213-218

**Current Placeholder:**
```csharp
// TODO: Resolve Stockpile contents when we add that feature
if (mutators.Any(m => m.defName == "Stockpile"))
{
    // Placeholder - we'd need to research how to resolve stockpile contents
    stockpileCount++;
}
```

**Expected Implementation (pattern depends on API discovery):**

**Pattern A: Private Getter Method (Most Likely)**
```csharp
var stockpileMutator = mutators.FirstOrDefault(m => m.defName == "Stockpile");
if (stockpileMutator != null && stockpileMutator.Worker != null)
{
    var planetTile = new PlanetTile(tileId, world.grid.Surface);

    // Use reflection (similar to MineralRich pattern)
    var method = AccessTools.Method(stockpileMutator.Worker.GetType(), "MethodNameTBD");
    if (method != null)
    {
        var result = method.Invoke(stockpileMutator.Worker, new object[] { planetTile });
        // Parse result based on type (ThingDef, List<ThingDef>, etc.)
        stockpiles.Add(result.defName);
        stockpileCount++;
    }
}
```

**Pattern B: Label Parsing (Fallback)**
```csharp
// We know GetLabel(PlanetTile) returns "weapons stockpile", "medicine stockpile"
var label = stockpileMutator.Worker.GetLabel(planetTile);
var stockpileType = ParseStockpileLabel(label); // Extract "weapons", "medicine", etc.
stockpiles.Add(stockpileType);
```

### Phase 3: Preset Integration (Optional)
**Files:** `Source/Data/Preset.cs`

**Candidates for Stockpile Preferences:**
1. **Homesteader** (salvage theme) → Weapons, Components, Steel (Preferred)
2. **Defense** (fortification) → Weapons (Preferred)
3. **Agrarian** (food security) → Food (Preferred)
4. **Power** (industrial) → Chemfuel, Components (Preferred)

---

## Testing Plan (Post-Implementation)

### 1. Cache Build Verification
```
1. Load world with DevTools enabled
2. Check Player.log for:
   [LandingZone] MineralStockpileCache: Built cache in Xms (Y MineralRich, Z Stockpile tiles cached)
   [LandingZone] MineralStockpileCache: Stockpile distribution: Weapons:15, Medicine:8, Components:12, ...
3. Verify defNames match expected patterns
```

### 2. Scoring Contributions
```
1. Find tile with known stockpile (DevTools investigation shows tileId)
2. Create test preset with Stockpiles.Weapons = Preferred
3. Search, find the tile in results
4. Click [DEBUG] Dump Match Data
5. Verify modifiers section shows: "Weapons: Quality +8"
```

### 3. Filter Application
```
1. Create test preset with Critical stockpile requirement
2. Search, verify only stockpile tiles returned
3. Check logs show correct tile reduction:
   [LandingZone] FilterService: Apply phase reduced 156000 → 37 tiles (StockpileFilter)
```

---

## Files Created/Modified

### Created:
- `docs/stockpile_scoring_design.md` (6,800 words)
- `docs/LZ-STOCKPILE-SCORING_progress.md` (this file)
- `Source/Core/Filtering/Filters/StockpileFilter.cs` (165 lines)

### Modified:
- `Source/Core/UI/DevToolsWindow.cs` (enhanced investigation tool, lines 149-315)
- `Source/Data/FilterSettings.cs` (added Stockpiles property, line 97-98)
- `Source/Core/Filtering/SiteFilterRegistry.cs` (added stockpile importance mapping, lines 135-137)
- `Source/Core/Filtering/FilterService.cs` (registered StockpileFilter line 59, added scoring lines 972-990)

---

## Next Steps

**Immediate (User Action Required):**
1. Launch RimWorld with updated mod
2. Enable Dev Mode
3. Open Landing Zone Dev Tools window
4. Click "Investigate Stockpile Mutators"
5. Check Player.log for non-public member discovery
6. Report findings (copy relevant log sections)

**After Investigation Results:**
1. Analyze log to identify private method/field for stockpile contents
2. Implement cache resolution in MineralStockpileCache.cs (lines 213-218)
3. Add whitelist validation (similar to validOres pattern)
4. Test cache build, verify distribution logging
5. Test scoring contributions with [DEBUG] dumps
6. Optionally add stockpile preferences to presets
7. Update documentation and close LZ-STOCKPILE-SCORING task

---

## Technical Notes

### Quality Rating Rationale

**Weapons (+8):**
- High strategic value (armory start)
- Expensive to craft early game
- Immediate defensive capability

**Medicine (+7):**
- Survival-critical (disease, injuries)
- Cannot be easily crafted without industrial tech
- Difference between glitterworld meds vs herbal medicine

**Components (+6):**
- Major industrial progression bottleneck
- Required for advanced crafting/research
- Time-consuming to gather otherwise

**Steel/Plasteel (+6):**
- Core construction materials
- Large quantities needed for base building
- Plasteel especially valuable (rare, needed for advanced items)

**Chemfuel (+5):**
- Power generation (chemfuel generators)
- Trading commodity
- Requires refinery to produce

**Food (+5):**
- Early-game survival buffer
- Packaged meals are efficient (no cooking time)
- Frees colonists for other tasks

**Lower tiers (2-4):**
- More easily obtained or less critical
- Still valuable but not game-changing

### Rarity Context

**Stockpile Occurrence:** 0.029% of settleable tiles (205 out of 703,755 in canonical aggregate)

**Comparison:**
- MineralRich: 0.004% (28 tiles) - 7x rarer than Stockpiles
- ArcheanTrees: ~0.005% (rare DLC feature)
- Caves: 8.1% (common)

**Conclusion:** Stockpiles are ultra-rare but not as rare as MineralRich. Worth implementing given their strategic value (starting resources) and thematic fit with Homesteader/salvage presets.

---

**Status:** Ready for Phase 2 once investigation completes.
**Build Status:** ✅ Compiling successfully, DLL deployed to Assemblies/
**Next Action:** User runs DevToolsWindow investigation tool in-game

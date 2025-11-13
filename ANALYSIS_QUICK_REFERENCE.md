# LandingZone Forensic Analysis - Quick Reference

## Grade: A- (Excellent with minor cleanup)

Architecture is **highly aligned** with clean slate vision. ~90% complete on core systems.

---

## WHAT'S ALIGNED (Keep These)

| Component | Status | Notes |
|-----------|--------|-------|
| Game cache as SSOT | ✓ Perfect | Find.World.grid[tileId] directly, no duplication |
| Two-phase filtering | ✓ Perfect | Apply (hard) → Score (preference) |
| FilterImportance enum | ✓ Perfect | Critical/Preferred/Ignored for k-of-n |
| IndividualImportanceContainer | ✓ Perfect | Per-item importance (Granite=Critical, etc.) |
| TileDataCache lazy memoization | ✓ Perfect | Expensive data only on survivors |
| 16 filters implemented | ✓ Good | Light/Medium/Heavy categorized correctly |
| Harmony patches | ✓ Good | Minimal, focused, no excessive hooking |
| FilterService + Registry | ✓ Good | Clean separation, heaviness-based ordering |

---

## WHAT NEEDS CLEANUP

### DEAD CODE TO DELETE (680 LOC total)

```
Source/Data/TriStateFilter.cs (175 LOC)
  → Legacy pattern, unused, replaced by FilterImportance enum
  
Source/Data/MultiSelectFilterContainer.cs (225 LOC)
  → Old multi-select pattern, replaced by IndividualImportanceContainer
  
Source/Core/Filtering/BranchAndBoundScorer.cs (200 LOC)
  → Unused alternative scorer, FilterService.BuildTileScore() is active
  
Source/Core/Filtering/PrecomputedBitsetCache.cs (100 LOC)
  → Unused optimization, not integrated
  
Source/Core/Filtering/BitsetAggregator.cs (80 LOC)
  → Unused bitwise utility
```

### INCOMPLETE REFACTORING

#### Stone Filtering UI
- **Problem:** FilterSettings.Stones is IndividualImportanceContainer (correct)
- **Problem:** LandingZonePreferencesWindow still uses old per-stone pattern (3 TODOs)
- **Fix:** Rebuild stone selector to match FilterSettings.Stones architecture

#### CoastalLakeFilter + AdjacentBiomesFilter
- **Problem:** Using heuristic neighbor detection (unverified)
- **Problem:** TODOs reference RimWorld icosahedral grid clarification needed
- **Fix:** Verify/implement actual RimWorld API neighbor detection

#### FilterValidator.cs
- **Problem:** TODO about updating validation for IndividualImportanceContainer
- **Fix:** Update validation to aggregate Critical/Preferred from containers

#### Documentation
- **Problem:** Docs reference deleted code (TemperatureFilter, HasCaveFilter, etc.)
- **Tasks:** LZ-DOCS-001, LZ-DOCS-002 in TODO
- **Fix:** Update after git changes finalized

---

## FILTERS STATUS

### Implemented (16)
- Light (game cache): Biome, AverageTemp, MinTemp, MaxTemp, Rainfall, Coastal, CoastalLake, Elevation, River, Road, WorldFeature, Landmark
- Heavy (expensive): ForageableFood, Graze, MapFeature, AdjacentBiomes

### Deleted (cleanup in git diff)
- TemperatureFilter (split into Avg/Min/Max)
- IndividualStoneFilter (consolidated)
- SpecificStoneFilter (consolidated)
- StoneCountFilter (consolidated)

### Planned (not started)
- AnimalDensityFilter (properties in FilterSettings, straightforward)
- FishPopulationFilter (properties in FilterSettings, straightforward)

---

## IMMEDIATE ACTIONS (For 0.1.0-beta)

### 1. Clean Code (1-2 hours)
```bash
# Delete these 5 files:
rm Source/Data/TriStateFilter.cs
rm Source/Data/MultiSelectFilterContainer.cs
rm Source/Core/Filtering/BranchAndBoundScorer.cs
rm Source/Core/Filtering/PrecomputedBitsetCache.cs
rm Source/Core/Filtering/BitsetAggregator.cs
```

### 2. Commit Git Changes
- Stone filter consolidation + deletions already in git diff
- Clean up stale TODOs about old filter patterns

### 3. Update Documentation (2-3 hours)
- Remove WorldSnapshot references from CLAUDE.md + docs
- Remove deleted filter references from architecture doc
- Add current performance metrics from testing

### 4. Stone UI Rebuild (3-4 hours)
- Populate stone selector from game's available stones
- Use FilterSettings.Stones (IndividualImportanceContainer)
- Add Reset/All/None buttons
- Update stone counting indicators

### 5. Filter Validation (1-2 hours)
- Update FilterValidator.cs to handle IndividualImportanceContainer
- Test critical/preferred aggregation logic

---

## SHORT-TERM WORK (Next Sprint)

### Priority 1: Basic/Advanced UI Mode (LZ-UI-021)
- **Impact:** Critical for casual users
- **Scope:** Toggle + conditional rendering in LandingZonePreferencesWindow
- **Basic:** Simple ranges, quick features (coastal/river/caves), growing season
- **Advanced:** Full granularity (all current sections)

### Priority 2: New Filters (LZ-FILTER-015, 016)
- **AnimalDensityFilter:** tile.AnimalDensity (0-6.5 range)
- **FishPopulationFilter:** tile.MaxFishPopulation (0-900 range)
- **Status:** Properties already in FilterSettings, straightforward additions

### Priority 3: Filter Verification (Defect Prevention)
- **CoastalLakeFilter:** Verify neighbor detection against actual RimWorld API
- **MapFeatureFilter:** Add reflection fallback + error handling

---

## ARCHITECTURE INSIGHTS

### What Makes This Good

1. **Single Source of Truth:** No data duplication, game cache is authoritative
2. **Instant Initialization:** No expensive pre-computation, world cache ready immediately
3. **Lazy Evaluation:** Only compute expensive data for survivors (~500-2000 tiles, not 156k)
4. **Clean Separation:** Hard filtering (Apply) vs preference scoring (Score) separate phases
5. **Fine-Grained Control:** IndividualImportanceContainer allows per-item importance
6. **No Premature Optimization:** Bitset, precomputed caches not forced into pipeline
7. **Minimal Harmony:** Only essential patches, not excessive hooking

### Performance Pattern (Validated)

```
World: 295,732 tiles
Settleable: 156,545 tiles (53%)

Apply Phase: 156,545 → 500-2000 tiles (90-95% filtered)
Score Phase: Only 500-2000 tiles scored
Result: Seconds instead of minutes
```

---

## CONCERNS TO MONITOR

1. **Reflection in MapFeatureFilter** - Uses Mutators property, brittle for version changes
   - Solution: Cache reflection result, add error handling
   
2. **CoastalLakeFilter heuristic** - May not match actual RimWorld adjacency
   - Solution: Verify/implement proper grid neighbor detection
   
3. **Dead code accumulation** - TriStateFilter, BranchAndBoundScorer still present
   - Solution: Delete confirmed unused code before 0.1.0-beta
   
4. **Documentation lag** - Docs reference deleted code
   - Solution: Update after git changes finalized

---

## FILE LOCATIONS

**Detailed Analysis:** `/Users/will/Dev/Rimworld_Mods/LandingZone/FORENSIC_ANALYSIS.md`

**This Document:** `/Users/will/Dev/Rimworld_Mods/LandingZone/ANALYSIS_QUICK_REFERENCE.md`

---

## BOTTOM LINE

**The LandingZone codebase is production-ready architecturally.** Current work is:
- Cleaning up legacy code (680 LOC dead code)
- Completing stone UI refactoring (already planned in TODOs)
- Adding missing filters (AnimalDensity, FishPopulation)
- Improving UX with Basic/Advanced mode (critical priority)

Nothing fundamental needs rework. Execution is the focus.

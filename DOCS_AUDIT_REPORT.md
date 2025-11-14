# Documentation Audit Report - 0.1.* Architecture Alignment (2025-11-13)

## Executive Summary

Forensic audit of all documentation files to identify 0.0.* relics and ensure alignment with current 0.1.* architecture (membership scoring, no WorldSnapshot, stone filters refactored).

## Obsolete References Found

### Deleted Classes (Must Remove)
- `TemperatureFilter` - deleted in Sprint 1.1, functionality moved to AverageTemperatureFilter/MinimumTemperatureFilter/MaximumTemperatureFilter
- `HasCaveFilter` - deleted in v0.0.3-alpha, replaced by MapFeatureFilter
- `SpecificStoneFilter` - deleted in Sprint 1.1, replaced by IndividualStoneFilter
- `StoneCountFilter` - deleted in Sprint 1.1, integrated into stone filtering
- `GrazeFilter` - still exists, but mentioned incorrectly in some docs
- `WorldSnapshot` - deleted in v0.0.3-alpha, now use game cache directly

### Architecture Changes Not Reflected
- k-of-n scoring → membership scoring (fuzzy preferences)
- Binary pass/fail → continuous [0,1] memberships
- Penalty term approach (P_C based on worst critical)
- Mutator quality scoring integrated (83 mutators rated)

## Files Requiring Updates

### Priority 1: Active Documentation

**1. docs/filtering-architecture_v0_0_3-alpha.md**
Status: ⚠️ HEAVILY OUTDATED (v0.0.3-alpha specific)
Issues:
- Line 25: Mentions TemperatureFilter (deleted)
- Line 30: Mentions SpecificStoneFilter (deleted)
- Line 93: Mentions StoneCountFilter, GrazeFilter (deleted/refactored)
- Multiple references to WorldSnapshot throughout
- Describes k-of-n scoring, not membership scoring
- No mention of current 0.1.* features (Default/Advanced UI, membership functions)

Recommendation: **Archive or major rewrite** - this doc is v0.0.3-alpha specific and doesn't reflect 0.1.* reality

**2. docs/architecture.md**
Status: ⚠️ PARTIALLY OUTDATED
Issues:
- References WorldSnapshot in GameState aggregation
- May reference old scoring approach

Recommendation: Update to current architecture

**3. CLAUDE.md**
Status: ✅ MOSTLY ACCURATE
Issues:
- Correctly references TileDataCache (still exists)
- Correctly describes two-phase filtering
- References are accurate to current codebase

Recommendation: Minor updates only

**4. README.md**
Status: ❓ NOT YET AUDITED

Recommendation: Quick audit

### Priority 2: Scoring Documentation

**5. docs/mathing-the-math.md**
Status: ✅ ACCURATE (describes membership scoring - current system)

**6. docs/user-modifying-mathed-math.md**
Status: ✅ ACCURATE (describes tuning parameters for membership scoring)

### Priority 3: Forensic Artifacts (Historical Reference)

These files document the forensic analysis process and are **intentionally historical**. They should be kept as-is for reference:

- FORENSIC_ANALYSIS.md
- ANALYSIS_QUICK_REFERENCE.md
- ANALYSIS_INDEX.md
- CLEAN_SLATE_SUMMARY.md
- FORENSIC_DEAD_CODE_ANALYSIS.md

### Priority 4: Prepare Landing Research (Reference Material)

These are research notes from analyzing Prepare Landing mod. Keep as-is:

- docs/prepare-landing-notes.md
- docs/prepare-landing-pain-points.md
- docs/prepare-landing-feature-list.md
- docs/rimworld-modding-context.md
- docs/advanced-mode-organization.md

## Current 0.1.* Architecture (For Documentation Updates)

### Core Systems

**Data Layer**:
- Game Cache (`Find.World.grid`) - SSOT for cheap tile data
- TileDataCache - Lazy computation for expensive data (growing days, stones, etc.)
- FilterSettings - User preferences with IndividualImportanceContainer pattern
- GameState - Aggregates Preferences, TileDataCache

**Filtering**:
- Two-phase: Apply (Critical only) → Score (Preferred penalties + membership)
- Light/Heavy filter heaviness for optimization
- ISiteFilter interface with Apply() and Membership() methods
- IndividualImportanceContainer for per-item importance (stones, rivers, roads, etc.)

**Scoring**:
- Membership-based (fuzzy preferences), not k-of-n binary
- Continuous [0,1] memberships via trapezoid falloff
- Group scores: S_C (critical), S_P (preferred), S_mut (mutator quality)
- Penalty term: P_C based on worst critical membership
- Global weights: λ_C, λ_P, λ_mut
- Final score: S = P_C × (λ_C·S_C + λ_P·S_P + λ_mut·S_mut)
- 83 mutators rated -10 to +10 for quality scoring

**UI**:
- Default/Advanced mode toggle
- Default mode: 8 essential filters (simplified)
- Advanced mode: 40+ filters with full control
- Stone selectors using IndividualImportanceContainer
- Results window with top-N display

### Active Filter Implementations (0.1.*)

**Light Filters** (game cache):
- BiomeFilter
- RiverFilter (IndividualImportanceContainer)
- RoadFilter (IndividualImportanceContainer)
- CoastalFilter (ocean)
- CoastalLakeFilter
- WorldFeatureFilter
- LandmarkFilter

**Heavy Filters** (TileDataCache):
- IndividualStoneFilter (per-stone importance)
- ForageableFoodFilter
- MapFeatureFilter (reads Mutators)
- AdjacentBiomesFilter

**Numeric Range Filters** (Light, with membership):
- AverageTemperatureFilter
- MinimumTemperatureFilter
- MaximumTemperatureFilter
- RainfallFilter (game cache + membership)
- ElevationFilter
- GrowingDaysFilter (Heavy - uses TileDataCache)

## Recommended Actions

1. **Archive docs/filtering-architecture_v0_0_3-alpha.md** to `docs/archive/`
   - Too specific to v0.0.3-alpha
   - Would require complete rewrite to be accurate

2. **Update docs/architecture.md**
   - Remove WorldSnapshot references
   - Add membership scoring overview
   - Document current GameState structure

3. **Create docs/filtering-architecture_v0_1_beta.md** (NEW)
   - Document current two-phase pipeline with membership scoring
   - List active filter implementations
   - Explain Light/Heavy distinction with TileDataCache
   - Document IndividualImportanceContainer pattern

4. **Update CLAUDE.md**
   - Add reference to membership scoring
   - Update filter list to current implementations
   - Remove any 0.0.* specific notes

5. **Audit README.md**
   - Ensure no stale references
   - Update feature list if needed

6. **Update tasks.json meta notes**
   - Remove "Ready for in-game validation testing (LZ-SCORING-004)" (deleted task)
   - Update version to 0.1.3-beta

## Deleted Code Reference (For Documentation Updates)

These classes have been **deleted** and should not be mentioned in active docs:

### Sprint 1.1 Cleanup:
- TemperatureFilter → AverageTemperatureFilter, MinimumTemperatureFilter, MaximumTemperatureFilter
- SpecificStoneFilter → IndividualStoneFilter (uses IndividualImportanceContainer)
- StoneCountFilter → integrated into stone filtering logic
- TriStateFilter → deleted (pattern replaced by IndividualImportanceContainer)
- MultiSelectFilterContainer → deleted (pattern replaced by IndividualImportanceContainer)

### v0.0.3-alpha Cleanup:
- WorldSnapshot → deleted, use game cache directly
- HasCaveFilter → deleted, use MapFeatureFilter

### Scoring Refactor:
- BranchAndBoundScorer → deleted
- Legacy k-of-n scoring → replaced with membership scoring

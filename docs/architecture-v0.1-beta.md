# LandingZone Architecture v0.1-beta

**Last Updated**: 2025-11-13
**Version**: 0.1.3-beta
**Status**: Current production architecture

## Overview

LandingZone is a RimWorld mod for intelligent landing site selection using a hybrid filtering architecture: two-phase pipeline (Apply → Score) + membership-based fuzzy scoring + lazy expensive computation.

**Core Principles**:
- Game cache (`Find.World.grid`) as single source of truth - zero duplication
- Two-phase filtering: Apply (hard Critical) → Score (soft Preferred + membership)
- Lazy evaluation: expensive data only computed for survivors
- Membership scoring: Fuzzy preferences (trapezoid falloff) replace binary cutoffs
- Quality over shortcuts: proper solutions, not hacks

## System Components

### 1. Data Layer

**GameState** (`Source/Data/GameState.cs`)
- Singleton aggregating all mod state
- Contains: `Preferences` (user filters), `TileCache` (expensive computations)
- Factory: `GameStateFactory.CreateDefault()`

**TileDataCache** (`Source/Data/TileDataCache.cs`)
- Lazy cache for expensive RimWorld API calls
- Computes on-demand: growing days (2-3ms), stone types (1-2ms), pollution, movement difficulty
- `GetOrCompute(tileId)` → `TileInfoExtended`
- Cache persists for evaluation lifetime, cleared between searches

**FilterSettings** (`Source/Data/FilterSettings.cs`)
- User preferences for all filters
- Uses `IndividualImportanceContainer` for per-item importance:
  - `Stones`: Granite=Critical, Marble=Preferred, etc.
  - `Rivers`: HugeRiver=Critical, Creek=Ignored, etc.
  - `Roads`: AncientAsphaltRoad=Preferred, etc.
- Numeric ranges: `AverageTemperatureRange`, `RainfallRange`, `ElevationRange`, etc.
- Importance levels: `Critical` (must match) | `Preferred` (nice to have) | `Ignored`

**LandingZoneOptions** (`Source/Data/LandingZoneOptions.cs`)
- UI/UX preferences
- `PreferencesUIMode`: Default (simplified) | Advanced (full control)
- `UseNewScoring`: Enable membership-based scoring (default: true)

### 2. Filtering Pipeline

**FilterService** (`Source/Core/Filtering/FilterService.cs`)
- Orchestrates two-phase pipeline via `FilterEvaluationJob`
- Manages filter execution, candidate tracking, scoring

**Two-Phase Architecture**:

#### Phase 1: Apply (Hard Filtering)
Location: `FilterEvaluationJob` constructor (line 283-343)

```
Start: ~156k settleable tiles
  ↓ Light Filters (game cache) - instant, zero-cost
  ↓ Heavy Filters (TileDataCache) - lazy, 5-10ms per tile
End: 8-20k candidate tiles (90-95% reduction)
```

**Characteristics**:
- **Sequential**: Each filter processes output of previous filter
- **Critical only**: Filters only apply when `FilterImportance.Critical`
- **Ordered by heaviness**: Light → Heavy (cheap filters eliminate bulk first)
- **Synchronous**: All filters complete in constructor
- **BitsetAggregator**: For cheap filters, uses bitwise operations for 10x speedup
- **Early termination**: Stops if candidate count reaches zero

#### Phase 2: Score (Precision Ranking)
Location: `BuildTileScore()` (line 403-518)

```csharp
foreach (tileId in candidates) {
    // 1. Collect memberships from all filters
    var criticalMemberships = [];
    var preferredMemberships = [];

    // 2. Compute group scores (weighted averages)
    var S_C = ComputeGroupScore(criticalMemberships, weights);
    var S_P = ComputeGroupScore(preferredMemberships, weights);

    // 3. Find worst critical (for penalty)
    var W_C = ComputeWorstCritical(criticalMemberships);
    var P_C = ComputePenalty(W_C); // penalty multiplier

    // 4. Compute mutator quality score
    var S_mut = ComputeMutatorScore(tileId);

    // 5. Final membership-based score
    var score = ComputeMembershipScore(S_C, S_P, S_mut, P_C);

    // 6. Track top N results
    InsertTopResult(topN, tileId, score);
}
```

**Characteristics**:
- **Membership-based**: Continuous [0,1] scores, not binary pass/fail
- **Fuzzy preferences**: Trapezoid falloff for numeric ranges
- **Penalty term**: Worst critical reduces overall score
- **Mutator quality**: 83 mutators rated -10 (bad) to +10 (good)
- **Top-N tracking**: Only keeps best `MaxResults` tiles (configurable 25k-150k)

### 3. Membership Scoring System

**MembershipFunctions** (`Source/Core/Filtering/MembershipFunctions.cs`)
Utility functions for computing [0,1] memberships:
- `TrapezoidMembership()`: Numeric ranges with soft margins
- `DistanceDecayMembership()`: Exponential falloff from target
- `BinaryMembership()`: Boolean checks (has river, is coastal, etc.)
- `SetMembership()`: Multi-select containers (biomes, stones, etc.)

**ScoringWeights** (`Source/Core/Filtering/ScoringWeights.cs`)
Scoring algorithm implementation:
- `ComputeGroupScore()`: Weighted average of memberships
- `ComputeWorstCritical()`: Min membership among criticals
- `ComputePenalty()`: Penalty multiplier from worst critical
- `ComputeMembershipScore()`: Final score formula

**Scoring Formula**:
```
S = P_C × (λ_C·S_C + λ_P·S_P + λ_mut·S_mut)

Where:
  S_C = weighted average critical membership [0,1]
  S_P = weighted average preferred membership [0,1]
  S_mut = mutator quality score [0,1]
  P_C = penalty based on worst critical [0,1]
  λ_C, λ_P, λ_mut = global weights (configurable in mod settings)
```

**Penalty Function**:
```
P_C = α_pen + (1 - α_pen) × W_C^γ_pen

Where:
  W_C = worst critical membership
  α_pen = penalty floor (min score fraction that survives)
  γ_pen = penalty sharpness (how harshly worst critical punishes)
```

**Mutator Quality Scoring**:
83 mutators rated -10 (very bad) to +10 (very good):
- **Positive**: Geothermal vents (+10), Fertile soil (+8), Arable land (+7)
- **Negative**: Toxic lakes (-10), Lava craters (-9), Polluted tiles (-8)
- Uses tanh squashing: `S_mut = 0.5 × (1 + tanh(0.25 × Q_raw))`

### 4. Filter Implementations

**ISiteFilter Interface**:
```csharp
public interface ISiteFilter
{
    string Id { get; }
    FilterHeaviness Heaviness { get; }
    IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles);
    float Membership(FilterContext context, int tileId);
}
```

**Filter Taxonomy**:

**Light Filters** (game cache - instant):
- `BiomeFilter` - Biome multi-select with IndividualImportanceContainer
- `RiverFilter` - River types with IndividualImportanceContainer
- `RoadFilter` - Road types with IndividualImportanceContainer
- `CoastalFilter` - Ocean coastal check
- `CoastalLakeFilter` - Lake coastal check (neighbor heuristic)
- `WorldFeatureFilter` - World features (mountain ranges, oceans)
- `LandmarkFilter` - Landmarks (requires world feature)
- `AverageTemperatureFilter` - Average temp with trapezoid membership
- `MinimumTemperatureFilter` - Min temp with trapezoid membership
- `MaximumTemperatureFilter` - Max temp with trapezoid membership
- `RainfallFilter` - Rainfall with trapezoid membership
- `ElevationFilter` - Elevation with trapezoid membership

**Heavy Filters** (TileDataCache - 5-10ms per tile):
- `IndividualStoneFilter` - Per-stone importance from DefDatabase
- `ForageableFoodFilter` - Forageable food types
- `MapFeatureFilter` - Reads actual Mutators (caves, ancient ruins, etc.)
- `AdjacentBiomesFilter` - Neighbor biome checks

**Pattern: IndividualImportanceContainer**:
Used for per-item importance (stones, rivers, roads, biomes):
```csharp
filters.Stones.SetImportance("Granite", FilterImportance.Critical);
filters.Stones.SetImportance("Marble", FilterImportance.Preferred);
filters.Rivers.SetImportance("HugeRiver", FilterImportance.Critical);
```

### 5. UI System

**LandingZonePreferencesWindow** (`Source/Core/UI/LandingZonePreferencesWindow.cs`)
- Main filter configuration window
- Mode toggle: Default | Advanced
- Renders appropriate UI based on mode

**DefaultModeUI** (`Source/Core/UI/DefaultModeUI.cs`)
- Simplified interface with 8 essential filters
- Preset cards: Temperate, Arctic Challenge, Desert Oasis
- Target audience: Casual users

**AdvancedModeUI** (`Source/Core/UI/AdvancedModeUI.cs`)
- Full control with 40+ filters
- Organized into sections: Climate, Terrain, Geography, Features, Resources
- Stone selector with dynamic DefDatabase loading
- Target audience: Power users

**LandingZoneResultsWindow** (`Source/Core/UI/LandingZoneResultsWindow.cs`)
- Top-N results display
- Per-tile details: biome, climate, terrain, stones, features
- Score display (membership score if UseNewScoring=true)
- Click to jump to tile on world map

**Bottom Panel UI** (`Source/Core/UI/SelectStartingSiteButtonsPatch.cs`)
- Integrated into RimWorld's "Select Starting Site" page
- Buttons: `Filters` (open preferences) | `Top (XX)` (open results) | `>` (highlight next)
- Status display: progress bar during evaluation, match count when complete
- Icon buttons: Bookmark toggle, Bookmark manager

### 6. Performance Optimizations

**1. Game Cache as SSOT**
- Use `Find.World.grid[tileId]` directly for cheap properties
- Zero initialization cost, zero memory duplication
- ~10 properties instantly available: biome, temperature, rainfall, elevation, hilliness, coastal, etc.

**2. Lazy Expensive Computation**
- TileDataCache only computes for tiles that survive cheap filters
- Growing days: 2-3ms per tile
- Stone types: 1-2ms per tile
- Only ~8-20k tiles processed, not full ~156k

**3. BitsetAggregator**
- Cheap filters use bitwise operations
- 10x faster than naive LINQ (benchmark: 100ms → 10ms for 156k tiles)
- Precomputes bitsets for lookups (rivers, roads, biomes, features)

**4. Filter Ordering**
- Light filters run first (eliminate 90-95% of tiles instantly)
- Heavy filters only process survivors (~8-20k tiles)
- Example: BiomeFilter eliminates 100k tiles in 10ms, then IndividualStoneFilter processes 8k tiles in 80ms

**5. Top-N Tracking**
- Min-heap tracks only best N results
- Avoids sorting full candidate list
- Memory: O(N) instead of O(candidates)

**6. Membership Caching**
- Membership values cached during scoring phase
- Expensive filters (stones, growing days) computed once per tile

## Mod Settings

**Performance**:
- Auto-run search on world load (default: off)
- Tiles processed per frame (50-1000, default: 250)
- Max candidate tiles (25k-150k, default: 100k)
- Allow cancel search (default: on)

**Scoring**:
- Scoring weight preset: Balanced, Critical Focused (default), Strict Hierarchy, Ultra Critical, Precision Match

**Logging**:
- Log level: Verbose, Standard (default), Brief

## File Structure

```
Source/
├─ LandingZoneMod.cs              - Entry point, Harmony registration
├─ Data/
│  ├─ GameState.cs                - Singleton state aggregator
│  ├─ GameStateFactory.cs         - Factory for GameState
│  ├─ FilterSettings.cs           - User filter preferences
│  ├─ LandingZoneOptions.cs       - UI/UX preferences
│  ├─ UserPreferences.cs          - Aggregates FilterSettings + Options
│  ├─ TileDataCache.cs            - Lazy expensive computation cache
│  ├─ TileInfoExtended.cs         - Extended tile data structure
│  ├─ IndividualImportanceContainer.cs - Per-item importance pattern
│  └─ TriStateFilter.cs           - Tri-state toggle helper (Ignored/Preferred/Critical)
├─ Core/
│  ├─ LandingZoneContext.cs       - Global state + convenience methods
│  ├─ Filtering/
│  │  ├─ FilterService.cs         - Two-phase pipeline orchestrator
│  │  ├─ FilterContext.cs         - Shared context for filters
│  │  ├─ SiteFilterRegistry.cs    - Filter registration
│  │  ├─ MembershipFunctions.cs   - Membership computation utilities
│  │  ├─ ScoringWeights.cs        - Scoring algorithm implementation
│  │  ├─ MutatorQualityRatings.cs - 83 mutators rated -10 to +10
│  │  ├─ BitsetAggregator.cs      - Bitwise optimization for cheap filters
│  │  └─ Filters/
│  │     ├─ BiomeFilter.cs
│  │     ├─ RiverFilter.cs
│  │     ├─ IndividualStoneFilter.cs
│  │     ├─ AverageTemperatureFilter.cs
│  │     └─ ... (40+ filters)
│  └─ UI/
│     ├─ LandingZonePreferencesWindow.cs - Main filter config window
│     ├─ DefaultModeUI.cs         - Simplified mode renderer
│     ├─ AdvancedModeUI.cs        - Full control renderer
│     ├─ LandingZoneResultsWindow.cs - Results display
│     ├─ SelectStartingSiteButtonsPatch.cs - Bottom panel integration
│     └─ UIHelpers.cs             - Shared UI utilities
```

## Version History

- **v0.1.3-beta** (2025-11-13): Fixed mode toggle, tasks board, removed tile limit
- **v0.1.2-beta** (2025-11-13): Stop button, max candidates setting, Top button fix
- **v0.1.1-beta** (2025-11-13): Membership scoring complete, mod settings, UI consolidation
- **v0.1.0-beta** (2025-11-XX): First beta with Default/Advanced modes, stone selectors
- **v0.0.3-alpha** (2025-11-XX): Removed WorldSnapshot, game cache SSOT, cave filter fix

## Future Roadmap

See `tasks.json` for full task list. Upcoming features:
- Preset save/load system (P2)
- Score breakdown transparency UI (P2)
- Bookmark manager for favorite tiles (P2)
- Heatmap overlay visualization (P3)
- Advanced drag-to-order ranking with AND/OR logic (Future)

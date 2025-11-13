⏺ LandingZone Filtering Architecture

  The Scale Challenge

  RimWorld World Data:
  - ~295,732 total tiles on a standard planet
  - ~156,545 settleable tiles (53%)
  - Each tile has:
    - Cheap data (RimWorld's native cache): biome, temperature, rainfall, elevation, hilliness,
   coastal status, river status (~10 properties) - instant O(1) access via Find.World.grid
    - Expensive data (computed on-demand): growing days (2-3ms), stone types (1-2ms), grazing
  availability (1-2ms), movement difficulty (1-2ms), min/max temps (1-2ms)
  - Computing all expensive data upfront = ~750-1500 seconds (12-25 minutes)

  Two-Phase Architecture

  Phase 1: Apply Filters (Hard Filtering)

  Location: SiteFilterRegistry.ApplyAll() → FilterService.FilterEvaluationJob constructor (line
   317)

  How it works:
  Start: 156,545 settleable tiles
    ↓ BiomeFilter (Light) - removes non-allowed biomes
    ↓ TemperatureFilter (Light) - removes out-of-range temps (cheap data)
    ↓ RainfallFilter (Light) - removes out-of-range rainfall (cheap data)
    ↓ CoastalFilter (Light) - removes non-coastal if Critical
    ↓ RiverFilter (Light) - removes non-river if Critical
    ↓ ... (other Light filters)
    ↓ SpecificStoneFilter (Heavy) - removes tiles without required stones (EXPENSIVE)
    ↓ LandmarkFilter (Light but runs late) - removes non-landmark tiles if Critical
  End: 8-20k tiles typically (90-95% reduction)

  Key characteristics:
  - Sequential execution - each filter gets output of previous filter
  - Synchronous - all filters run to completion in constructor (.ToList() forces evaluation)
  - Sorted by FilterHeaviness enum (Light → Medium → Heavy)
  - Critical importance only - filters only reject tiles when FilterImportance.Critical
  - Early termination - stops if any filter returns 0 tiles
  - Access to TileCache (as of latest fix) - expensive data computed on-demand

  File: FilterService.cs:83, SiteFilterRegistry.cs:17-32

  Phase 2: Scoring (Precision Ranking)

  Location: FilterService.BuildTileScore() (line 113-198)

  How it works:
  foreach (var tileId in tiles) // tiles from Apply phase
  {
      var score = 1f;

      // CHEAP: Query game cache (Find.World.grid[tileId])
      if (!ApplyRangeConstraint(tile.temperature, ...)) return score=0;
      if (!ApplyRangeConstraint(tile.rainfall, ...)) return score=0;
      if (!ApplyBooleanPreference(tile.IsCoastal(), ...)) return score=0;
      // ... ~5 more cheap checks

      // EXPENSIVE: Query TileDataCache (lazy computation)
      var extended = cache.GetOrCompute(tileId);  // ← Cache lookup or 5-10ms compute
      if (!ApplyRangeConstraint(extended.GrowingDays, ...)) return score=0;
      if (!ApplyRangeConstraint(extended.Pollution, ...)) return score=0;
      // ... ~5 more expensive checks

      // Keep only top N results (MaxResults = 20 default)
      InsertTopResult(_lastScores, candidate, maxResults);
  }

  Key characteristics:
  - Two-tier data access: Cheap properties first, expensive only if needed
  - Lazy caching: TileDataCache only computes expensive data for tiles that pass cheap filters
  - Top-N tracking: Only keeps best MaxResults tiles in memory (default 20, max 100)
  - Preferred scoring: Non-critical filters apply penalties (-0.03 to -0.25) rather than
  rejecting
  - Final sort: Results sorted by score descending

  File: FilterService.cs:113-198

  Filter Taxonomy

  Light Filters (Fast - run first)

  - Data source: RimWorld's game cache (Find.World.grid[tileId]) - instant, zero-cost access
  - Complexity: O(1) per tile
  - Examples: BiomeFilter, TemperatureFilter, RainfallFilter, CoastalFilter, RiverFilter,
  RoadFilter, WorldFeatureFilter, LandmarkFilter
  - Implementation: Direct property access or Dictionary/HashSet lookups built once per evaluation

  Heavy Filters (Slow - run last)

  - Data source: TileDataCache (expensive RimWorld API calls)
  - Complexity: 5-10ms per tile first access, O(1) subsequent
  - Examples: SpecificStoneFilter, GrazeFilter, StoneCountFilter
  - Implementation: Calls world.NaturalRockTypesIn(), VirtualPlantsUtility, etc.

  Performance Optimizations & Tradeoffs

  1. Game Cache as Single Source of Truth

  What: Use RimWorld's native world cache (Find.World.grid) directly for all cheap tile properties
  Where: All Light filters access Find.World.grid[tileId] - biome, temp, rainfall, coastal, river,
  elevation, hilliness
  Tradeoff: Zero initialization cost, zero memory duplication vs. previous WorldSnapshot approach
  Savings: ~150ms initialization eliminated, ~5MB memory eliminated, simpler code
  Architecture benefit: Single source of truth - no sync issues, always accurate

  Previous approach (removed in v0.0.3-alpha): WorldSnapshot.cs duplicated game data

  2. Lazy TileDataCache

  What: Only compute expensive data for tiles that survive Apply phase
  Where: TileDataCache.GetOrCompute() - memoizes growing days, stones, grazing, movement, etc.
  Tradeoff: Typical evaluation computes ~500-2000 tiles (0.5-1.5% of world) vs all 156k tiles
  Savings: 12-25 minutes → 2-10 seconds

  Example:
  - User sets Critical: River, Landmark, Granite+Marble
  - Apply filters: 156,545 → 8 tiles (99.99% filtered)
  - Only 8 tiles get expensive data computed (40-80ms total vs 12+ minutes)

  File: Data/TileDataCache.cs

  3. Cave Filtering Accuracy (Fixed in v0.0.3-alpha)

  Problem: HasCaveFilter used broken heuristic (Mountainous=100% caves, LargeHills=30% random)
  Impact: False positives - tiles showing 100% match when they don't actually have caves
  Solution: Removed HasCaveFilter, use MapFeatureFilter exclusively (reads actual Mutators)
  Result: Cave filtering now 100% accurate using game's actual world generation data

  Files: Removed HasCaveFilter.cs, updated MapFeatureFilter.cs

  4. O(m) Lookup Pattern

  What: Build feature/road/river lookups once, then O(1) tile checks
  Where: LandmarkFilter, WorldFeatureFilter, RoadFilter
  Tradeoff: Iterate 174-222 features once (5-10ms) vs checking each tile against all features

  Before (O(n×m)):
  foreach (var tile in tiles) // 156k tiles
      foreach (var feature in features) // 222 features
          if (feature.Contains(tile)) ... // 34M checks

  After (O(m + n)):
  var lookup = BuildLookup(features); // 222 features → dictionary (5ms)
  foreach (var tile in tiles) // 156k tiles
      if (lookup.Contains(tile)) ... // 156k checks

  File: Filters/LandmarkFilter.cs:66-106, Filters/WorldFeatureFilter.cs:77-102

  5. Top-N Heap Pattern

  What: Only track best MaxResults tiles instead of scoring all tiles
  Where: InsertTopResult() - maintains size-N list, replaces worst when better found
  Tradeoff: Final sort of N=20 vs N=156k
  Savings: Memory O(20) vs O(156k), Sort O(20 log 20) vs O(156k log 156k)

  File: FilterService.cs:263-291

  6. Filter Ordering by Heaviness

  What: Run cheap filters first to reduce dataset before expensive filters
  Where: SiteFilterRegistry.Register() - sorts by FilterHeaviness enum
  Tradeoff: Better average case (90% filtered by cheap filters) vs worst case (all filters run)

  Example flow:
  156,545 tiles
    ↓ BiomeFilter (O(1)) → 139,549 tiles (11% reduction)
    ↓ RiverFilter (O(1)) → 11,221 tiles (92% reduction)
    ↓ SpecificStoneFilter (O(expensive)) → 432 tiles (only 11k expensive lookups)
    ↓ LandmarkFilter (O(1)) → 8 tiles

  File: FilterService.cs:32-64, SiteFilterRegistry.cs:11-15

  Current Bottlenecks & Issues

  1. Synchronous Apply Phase

  Problem: Line 317 in FilterEvaluationJob constructor calls .ToList(), forcing all Apply
  filters to complete before stepping begins
  Impact: Heavy filters (SpecificStoneFilter) block UI for 1-5 seconds
  Why it happens: FilterEvaluationJob expects a List to iterate over, can't step through lazy
  IEnumerable

  File: FilterService.cs:317

  2. Stone Filtering Duplication

  Problem: Until latest fix, stone filtering happened BOTH in Apply (SpecificStoneFilter) AND
  Scoring (BuildTileScore)
  Impact: Waste compute; worse, scoring rejection caused "0 results" when all Apply survivors
  failed stone check
  Current state: Now only Apply for Critical, only Scoring for Preferred

  File: FilterService.cs:142-163, Filters/SpecificStoneFilter.cs

  3. No Filter Dependency Awareness

  Problem: Filters run in fixed order (Light → Heavy), unaware of inter-filter dependencies
  Example: If LandmarkFilter is Critical and reduces to 8 tiles, running SpecificStoneFilter
  AFTER would only check 8 tiles vs 11k tiles
  Impact: May compute expensive data for tiles that will be filtered anyway

  File: SiteFilterRegistry.cs:24-29

  4. TileDataCache Invalidation Granularity

  Problem: Cache invalidates entire dataset on world change, not individual tile changes
  Impact: Changing from one generated world to another requires full recompute (rare, but
  possible)

  File: TileDataCache.cs:34-41

  5. Preferred Scoring Has Fixed Penalties

  Problem: All Preferred penalties are hardcoded (-0.03, -0.08, -0.15, -0.25)
  Impact: User can't adjust relative importance of Preferred filters
  Example: User prefers coastal 3x more than rivers, but both get same penalty

  File: FilterService.cs:200-247

  Data Flow Diagram

  User Input (FilterSettings)
           ↓
     GameState.Preferences.Filters
           ↓
  [FilterService.CreateJob] ← receives GameState
           ↓
  [FilterEvaluationJob constructor]
           ↓
  [SiteFilterRegistry.ApplyAll] ← SYNCHRONOUS, BLOCKS HERE
      156k tiles → FilterContext(GameState, TileCache)
           ↓
      [Filter 1 Light].Apply(context, tiles) → 140k tiles
           ↓
      [Filter 2 Light].Apply(context, tiles) → 120k tiles
           ↓
      [Filter 3 Heavy].Apply(context, tiles) → 5k tiles (SLOW)
           ↓
      [Filter 4 Light].Apply(context, tiles) → 10 tiles
           ↓
      .ToList() ← forces evaluation
           ↓
  [Step() iteration] ← NOW asynchronous
      foreach (tileId in 10 tiles)
           ↓
          [Find.World.grid[tileId]] ← O(1) cheap data from game cache
           ↓
          [BuildTileScore]
              - Check cheap constraints (temp, rain, coastal, river) via game cache
              - TileCache.GetOrCompute() ← O(expensive) first call for heavy data
              - Check expensive constraints (growing days, stones, grazing)
              - Calculate final score
           ↓
          [InsertTopResult] ← maintain top 20 only
           ↓
  [Sort final results]
           ↓
  UI Display

  Key Architecture Files

  Core logic:
  - FilterService.cs - Orchestrates filtering, scoring, top-N tracking
  - SiteFilterRegistry.cs - Manages filter collection, Apply pipeline
  - FilterContext.cs - Passes GameState + TileCache to filters
  - ISiteFilter.cs - Filter interface (Apply, Describe, Heaviness)

  Data structures:
  - TileDataCache.cs - Lazy expensive tile data (on-demand, ~5-10ms per tile)
  - FilterSettings.cs - User preferences (importance, ranges, selections)
  - TileScore.cs - Result struct (tileId, score, breakdown)
  - Find.World.grid - RimWorld's native tile cache (cheap data, zero-cost access)

  Filter implementations:
  - Filters/BiomeFilter.cs - Light, HashSet lookup
  - Filters/SpecificStoneFilter.cs - Heavy, TileCache access
  - Filters/LandmarkFilter.cs - Light, O(m) HashSet pre-build
  - 20+ more filters following same patterns

  Summary of Performance Strategy

  1. Use game cache for cheap data (Find.World.grid) - zero init cost, zero memory overhead
  2. Filter aggressively with cheap data (Apply phase Light filters) - 90-95% reduction
  3. Compute expensive data lazily (TileCache) - only for survivors
  4. Run expensive filters late (Apply phase Heavy filters) - smaller dataset
  5. Track only top N (InsertTopResult) - O(20) memory
  6. Score survivors precisely (BuildTileScore) - 10-2000 tiles vs 156k

  Result: ~2-10 second searches vs 12-25 minutes naive implementation

  v0.0.3-alpha improvements: Removed WorldSnapshot (~150ms faster init, ~5MB less memory, simpler code)

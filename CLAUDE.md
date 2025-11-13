# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

LandingZone is a RimWorld mod for intelligent landing site selection. Uses Harmony for runtime patching and a hybrid filtering architecture: game's native cache (cheap, instant) + lazy expensive computation (TileDataCache) + two-phase filtering (Apply → Score).

**Core Value: Quality over shortcuts.** Don't compromise project integrity when facing obstacles - solve them properly or engineer better solutions. Never assume/guess API behavior - validate with evidence.

## Quick Start

**Build and deploy:**
```bash
python3 scripts/build.py              # Builds and copies DLL to Assemblies/
```

**Add a new filter:**
1. Create `Source/Core/Filtering/Filters/YourFilter.cs` implementing `ISiteFilter`
2. Choose `FilterHeaviness.Light` (cheap data) or `.Heavy` (expensive RimWorld APIs)
3. Register in `SiteFilterRegistry.cs`
4. Add properties to `FilterSettings.cs` (importance, ranges, etc.)
5. Wire up UI in `LandingZonePreferencesWindow.cs`

**Debug issues:**
1. Check RimWorld logs: `Player.log` in RimWorld data directory
2. Look for `[LandingZone]` prefixed messages
3. Add diagnostic logging to verify assumptions
4. Never assume RimWorld API methods exist - verify at compile time

**Track work:**
- `tasks.json` = source of truth for task status
- Task IDs: `LZ-{AREA}-{NUM}` (e.g., `LZ-PERF-007`)
- Update status when starting/completing/blocking work

## Build System

```bash
python3 scripts/build.py              # Debug (default)
python3 scripts/build.py -c Release   # Release
```

**What it does:** Restores, builds, and copies `LandingZone.dll` from `Source/bin/{config}/net472/` → `Assemblies/` (what RimWorld loads).

**Always use this script** - manual builds require manual DLL copy.

## Task Management

**Source of truth:** `tasks.json` (buckets: todo/in_progress/blocked/completed)
**Visualization:** `tasks.html` Kanban board via `python3 scripts/tasks_api.py`

**Update when:**
- Starting work: Move to `in_progress`
- Completing: Move to `completed`
- Blocked: Move to `blocked`, note dependency
- New work: Add with clear deliverables

## Architecture

**Key directories:**
- `Source/Core/Filtering/` - Filter pipeline (FilterService, SiteFilterRegistry)
- `Source/Core/Filtering/Filters/` - Individual filter implementations
- `Source/Core/UI/` - Windows (Preferences, Results)
- `Source/Data/` - TileDataCache (expensive lazy computation), FilterSettings

**Two-Phase Pipeline:**

1. **Apply (Hard Filtering)** - Runs in `FilterService.FilterEvaluationJob`
   - Reduces full world → small candidate set (90-95% filtered)
   - Light filters use game cache (`Find.World.grid[tileId]`) directly - cheap, instant access
   - Heavy filters use TileDataCache for expensive computations
   - Synchronous, runs in heaviness order (Light → Heavy)
   - Only applies Critical importance filters

2. **Score (Precision Ranking)** - Runs in `FilterService.BuildTileScore()`
   - Scores candidates: cheap checks first, expensive (TileCache) only if pass
   - Applies Preferred importance as score penalties
   - Maintains top-N heap, returns sorted by score

**Performance pattern:** Cheap filters eliminate bulk of tiles → expensive filters only process survivors → lazy scoring → seconds instead of minutes

**Filter implementation:**
```csharp
public class ExampleFilter : ISiteFilter
{
    public string Id => "example";
    public FilterHeaviness Heaviness => FilterHeaviness.Light; // or Heavy

    public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
    {
        var importance = context.State.Preferences.Filters.ExampleImportance;
        if (importance != FilterImportance.Critical) return inputTiles;

        // Light: Use Find.World.grid[tileId] or build O(1) lookup once
        // Heavy: Use context.Cache.GetOrCompute(tileId) for expensive data
        return inputTiles.Where(id => MeetsCriteria(id));
    }
}
```

## Critical Design Patterns

### 1. O(m) Lookup Pattern
Don't iterate features per tile. Build lookup once, check tiles with O(1):

```csharp
// ❌ BAD: O(n×m) = 156k tiles × 222 features = 34M checks
foreach (var tile in tiles)
    foreach (var feature in features)
        if (feature.Contains(tile)) ...

// ✅ GOOD: O(m + n) = 222 + 156k checks
var lookup = BuildHashSet(features); // Once
foreach (var tile in tiles)
    if (lookup.Contains(tile)) ...
```

### 2. Game Cache First, Expensive Computation Last
Use cheap game cache properties first, only compute expensive data for survivors:

```csharp
// Game cache (cheap, instant): Always check first
var tile = Find.World.grid[tileId]; // O(1) - RimWorld's native cache
if (tile.temperature < minTemp) return;
if (tile.rainfall < minRainfall) return;

// TileDataCache (expensive, lazy): Only access after cheap checks pass
var extended = context.Cache.GetOrCompute(tileId); // 5-10ms first call
if (extended.GrowingDays < minGrowing) return;
```

### 3. Filter Ordering
Register filters with correct heaviness to optimize pipeline:
- `Light`: Uses game cache (`Find.World.grid`) or simple lookups - instant, zero-cost access
- `Heavy`: Uses TileDataCache or expensive RimWorld APIs - lazy computation, 5-10ms per tile

## Testing & Validation

**In-game:**
1. Build: `python3 scripts/build.py`
2. Launch RimWorld, create/load world
3. Open Landing Zone preferences (bottom ribbon)
4. Set filters, search
5. Check `Player.log` in RimWorld data directory

**Key log patterns:**
```
[LandingZone] World cache ready - tiles: {N}, settleable: {M}
[LandingZone] FilterService: Apply phase reduced {N} → {M} tiles
[LandingZone] FilterService: Found {N} results, scores {range}
```

**Performance expectations:**
- Game cache access: Zero initialization cost (uses RimWorld's native cache)
- Searches: Seconds, not minutes (depends on filter complexity)
- More restrictive filters = faster (fewer tiles to score)

## Documentation

**Primary docs:**
- `docs/filtering-architecture_v0_0_3-alpha.md` - Performance analysis, patterns, bottlenecks
- `tasks.json` - Work items, dependencies, deliverables
- `README.md` - Project goals

**When investigating issues:**
1. Check `tasks.json` for related work
2. Read architecture doc for performance context
3. Check RimWorld logs for actual behavior
4. Add diagnostic logging - never assume

## Common Pitfalls

**❌ Assuming RimWorld APIs exist**
```csharp
var feature = worldFeatures.GetFeatureAt(tileId); // Doesn't exist - verify first!
```

**❌ Computing expensive data for non-Critical filters in Apply phase**
```csharp
// BAD: All tiles, even if Preferred
var stones = world.NaturalRockTypesIn(tileId); // Expensive!

// GOOD: Critical in Apply, Preferred in Score via TileCache
if (importance == Critical)
    var extended = context.Cache.GetOrCompute(tileId);
```

**❌ Nested loops over features/tiles**
```csharp
// BAD: O(n×m)
foreach (var tile in tiles)
    foreach (var feature in features)
        if (feature.Contains(tile)) ...

// GOOD: O(m+n) - build HashSet once, check with Contains
var lookup = BuildHashSet(features);
foreach (var tile in tiles)
    if (lookup.Contains(tile)) ...
```

**✅ Validate with logging**
```csharp
Log.Message($"[LandingZone] Debug: {context}");
```

## Workflow Principles

1. **Quality obsession**: End-user experience is non-negotiable
2. **Evidence over assumptions**: Test actual behavior, don't guess
3. **Proper solutions over shortcuts**: Fix root causes, don't work around them
4. **Clear communication**: Log important operations for debugging
5. **Task tracking**: Update `tasks.json` as work progresses

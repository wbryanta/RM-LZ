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

## Canonical World Data (Single Source of Truth)

**Source:** `/Users/will/Library/Application Support/RimWorld/Config/LandingZone_CacheAnalysis_2025-11-13_12-49-46.txt`
**Generated from:** `LandingZone_FullCache_2025-11-13_12-46-35.txt` (215MB, 295,732 tiles, 137,159 settleable)

**NEVER assume or guess mutator names or availability. Always reference this canonical data.**

### Favorable Mutators (QoL Improvements)

Use these exact defNames when configuring MapFeatures filters:

**High-Value Mutators** (boost tile quality):
- `AnimalLife_Increased` - 1,994 tiles (1.5%) - More hunting/wool/eggs
- `PlantLife_Increased` - 1,874 tiles (1.4%) - More foraging/wood
- `SteamGeysers_Increased` - 1,317 tiles (1.0%) - Free geothermal power/heat
- `WildPlants` - 2,344 tiles (1.7%) - Extra wild crops for foraging
- `SunnyMutator` - 2,642 tiles (1.9%) - Solar panel bonus
- `Fertile` - 838 tiles (0.6%) - Better soil for farming
- `WindyMutator` - 546 tiles (0.4%) - Wind turbine bonus
- `MineralRich` - 28 tiles (0.0%) - Extra ore/stone deposits

**Salvage/Resources** (loot opportunities):
- `Junkyard` - 1,066 tiles (0.8%) - Salvageable materials
- `AncientRuins` - 42 tiles (0.0%) - Ancient loot (danger risk)
- `Stockpile` - 38 tiles (0.0%) - Abandoned supplies
- `AncientWarehouse` - 7 tiles (0.0%) - Major loot cache

**Geographic Features** (situational value):
- `Caves` - 11,063 tiles (8.1%) - Defense, storage
- `Mountain` - 23,294 tiles (17.0%) - Defensible position
- `HotSprings` - 11 tiles (0.0%) - Natural heating

### Unfavorable Mutators (Avoid)

- `AnimalLife_Decreased` - 1,407 tiles (1.0%)
- `PlantLife_Decreased` - 856 tiles (0.6%)
- `Fish_Decreased` - 3 tiles (0.0%)
- `Pollution_Increased` - 23 tiles (0.0%)
- `FoggyMutator` - 906 tiles (0.7%) - Reduces solar efficiency

### Complete Mutator List

All 83 mutators discovered: AbandonedColonyOutlander, AbandonedColonyTribal, AncientChemfuelRefinery, AncientGarrison, AncientHeatVent, AncientInfestedSettlement, AncientLaunchSite, AncientQuarry, AncientRuins, AncientRuins_Frozen, AncientSmokeVent, AncientToxVent, AncientUplink, AncientWarehouse, AnimalHabitat, AnimalLife_Decreased, AnimalLife_Increased, ArcheanTrees, Archipelago, Basin, Bay, CaveLakes, Cavern, Caves, Chasm, Cliffs, Coast, CoastalAtoll, CoastalIsland, Cove, Crevasse, DryGround, DryLake, Dunes, Fertile, Fish_Decreased, Fish_Increased, Fjord, FoggyMutator, Harbor, Headwater, Hollow, HotSprings, Iceberg, InsectMegahive, Junkyard, Lake, LakeWithIsland, LakeWithIslands, Lakeshore, LavaCaves, LavaCrater, LavaFlow, Marshy, MineralRich, MixedBiome, Mountain, Muddy, Oasis, ObsidianDeposits, Peninsula, PlantGrove, PlantLife_Decreased, PlantLife_Increased, Plateau, Pollution_Increased, Pond, River, RiverConfluence, RiverDelta, RiverIsland, Sandy, SteamGeysers_Increased, Stockpile, SunnyMutator, TerraformingScar, ToxicLake, Valley, WetClimate, Wetland, WildPlants, WildTropicalPlants, WindyMutator

## Build System

```bash
python3 scripts/build.py              # Debug (default)
python3 scripts/build.py -c Release   # Release
```

**What it does:** Restores, builds, and copies `LandingZone.dll` from `Source/bin/{config}/net472/` → `Assemblies/` (what RimWorld loads).

**Always use this script** - manual builds require manual DLL copy.

## Versioning

**Single source of truth:** `About/About.xml`
- Code reads version at runtime: `content.ModMetaData.ModVersion`
- Never hardcode version in `.cs` files

**Current phase:** Beta (0.1.x-beta)
- Increment patch version when completing tasks
- Increment minor version for major features

**Workflow:**
1. Update `About/About.xml`: `<modVersion>0.1.2-beta</modVersion>`
2. Commit: `git commit -m "chore: bump version to 0.1.2-beta"`
3. Tag: `git tag -a v0.1.2-beta -m "Version 0.1.2-beta: Description"`

See `VERSIONING.md` for full guide.

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
   - Computes membership-based scores using fuzzy preference matching
   - Critical filters use continuous [0,1] memberships (trapezoid falloff)
   - Preferred filters contribute to score with weighted averages
   - Penalty term (P_C) based on worst critical membership
   - Mutator quality scoring (83 mutators rated -10 to +10)
   - Maintains top-N heap, returns sorted by membership score

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
- `docs/architecture-v0.1-beta.md` - Current architecture, two-phase pipeline, membership scoring
- `docs/mathing-the-math.md` - Membership scoring mathematics and formulas
- `tasks.json` - Work items, dependencies, deliverables
- `VERSIONING.md` - Version management workflow
- `README.md` - Project goals and overview

**Archived docs** (historical reference):
- `docs/archive/filtering-architecture_v0_0_3-alpha.md` - v0.0.3-alpha architecture (outdated)
- `docs/archive/architecture-blueprint-original.md` - Original planning blueprint

**When investigating issues:**
1. Check `tasks.json` for related work
2. Read `docs/architecture-v0.1-beta.md` for current architecture
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

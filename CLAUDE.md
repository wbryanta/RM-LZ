# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Contents
- [Project Overview](#project-overview)
- [Quick Start](#quick-start)
- [Canonical World Data (Single Source of Truth)](#canonical-world-data-single-source-of-truth)
- [Build System](#build-system)
- [Versioning](#versioning)
- [Task Management](#task-management)
- [Architecture](#architecture)
- [Critical Design Patterns](#critical-design-patterns)
- [Testing & Validation](#testing--validation)
- [Documentation](#documentation)
- [Common Pitfalls](#common-pitfalls)
- [Workflow Principles](#workflow-principles)
- [Forensic Analysis Pattern](#forensic-analysis-pattern)

## Project Overview

LandingZone is a RimWorld mod for intelligent landing site selection. Uses Harmony for runtime patching and a hybrid filtering architecture: game's native cache (cheap, instant) + lazy expensive computation (TileDataCache) + two-phase filtering (Apply → Score).

**Core Value: Quality over shortcuts.** Don't compromise project integrity when facing obstacles - solve them properly or engineer better solutions. Never assume/guess API behavior or data names—validate with evidence from canonical sources.

### AI Agents & Review Boundary
- **You are DevAgent (Claude Code)** — follow this file for implementation guidance.
- **Codex** acts as QA/QC Overwatch under `AGENTS.md`. Codex’s instructions stay there; do not duplicate them here.
- Expect Codex to audit your work using the verdict/issue template defined in `AGENTS.md`. Provide evidence (code, tests, logs) so Codex can validate without guesswork.

## Quick Start

**Build and deploy:** Run `python3 scripts/build.py` to restore/build and copy `LandingZone.dll` into `Assemblies/`. See [Build System](#build-system) for Release builds and troubleshooting.

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

**Default location (macOS example):** `~/Library/Application Support/RimWorld/Config/`
**Windows equivalent:** `%USERPROFILE%/AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios/Config/`
**Artifacts:** `LandingZone_FullCache_<timestamp>.txt` (raw dump) → `LandingZone_CacheAnalysis_<timestamp>.txt` (processed report; current snapshot covers 295,732 tiles / 137,159 settleable).

**To regenerate:**
1. Enable Dev Mode, open the Landing Zone preferences window, and click `[DEV] Dump FULL World Cache` to emit a fresh dump file in the Config directory.
2. Run `python3 scripts/analyze_world_cache.py /path/to/LandingZone_FullCache_<timestamp>.txt > LandingZone_CacheAnalysis_<date>.txt`.
3. Update references here with the new timestamp/path so future contributors know which dataset is authoritative.

**NEVER assume or guess mutator names or availability. Always reference this canonical data. If a defName is absent from the aggregate, stop and ask before proceeding.**

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

**Single source of truth:** `VERSIONING.md` + `About/About.xml`
- Code reads version at runtime: `content.ModMetaData.ModVersion`.
- Do not hardcode version strings elsewhere; reference `VERSIONING.md` for bump workflow (patch vs minor) and current version.

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

**❌ Skipping defName validation / canonical data checks**
- Always verify new mutators/biomes/features against `docs/data/canonical_world_library_aggregate.json` or the latest `LandingZone_CacheAnalysis_*` before wiring filters/presets.
- If absent, stop and ask for guidance instead of guessing.

**❌ Designing presets that return zero results**
- Avoid ultra-rare stacks as hard AND gates; provide fallbacks (e.g., OR tiers or staged loosening) so searches don’t silently return 0.

**❌ Forgetting preset-specific quality overrides**
- When a preset wants negative-rated mutators, ensure `MutatorQualityOverrides` is applied in scoring and scoped to the active preset only; otherwise desired tiles get penalized.

**❌ Bleeding state between Simple/Advanced**
- Simple and Advanced have independent `FilterSettings`. Use the copy buttons intentionally; don’t assume shared state.

## Workflow Principles

1. **Quality obsession**: End-user experience is non-negotiable
2. **Evidence over assumptions**: Test actual behavior, don't guess
3. **Proper solutions over shortcuts**: Fix root causes, don't work around them
4. **Clear communication**: Log important operations for debugging
5. **Task tracking**: Update `tasks.json` as work progresses

## Forensic Analysis Pattern

**Core Value:** Forensic-level analysis, critical thinking, and challenging assumptions lead to quality results. Projects languish when parameters, standards, and processes become muddy. We value granular definition and precision.

### When Facing Bugs or Unclear Behavior

**1. Add Diagnostic Tools FIRST** - Don't guess, instrument
   - Add DEBUG dump buttons/commands (dev mode only) before attempting fixes
   - Log comprehensive state to `Player.log` with `[LandingZone]` prefix
   - Dump actual runtime data: filter states, match breakdowns, tile properties
   - Example: Match data dump revealed IsPerfectMatch logic was correct, but filter IDs were generic

```csharp
// In dev mode: Add forensic dump for later analysis
if (Prefs.DevMode)
{
    if (Widgets.ButtonText(debugRect, "[DEBUG] Dump State"))
    {
        DumpComprehensiveState(); // Log EVERYTHING relevant
    }
}
```

**2. Plot the RIGHT Solution, Not the Easiest** - Quality over shortcuts
   - Quick fix: Hard-code filter names → "Cave", "Granite" in UI
   - Right solution: Resolve filter IDs dynamically per tile using actual RimWorld data
   - Ask: "What produces the best user experience long-term?"
   - Ask: "What happens when user adds new filters/mutators?"

**3. Validate Assumptions with Code, Not Intuition**
   - ❌ "The API probably works like this based on the name..."
   - ✅ Read the source: `MapFeatureFilter.cs` returns generic "map_features" ID
   - ✅ Check actual usage: `Find.WorldGrid[id]` not `Find.World.grid[id].biome`
   - ✅ Test with logging: `Log.Message($"[LandingZone] Actual value: {actual}")"`

```csharp
// DON'T assume Find.World.grid[id].biome exists
var tile = Find.World.grid[id];
var biome = tile.biome; // COMPILE ERROR - SurfaceTile doesn't have .biome

// DO verify actual API
var tile = Find.WorldGrid[id]; // Tile, not SurfaceTile
var biome = tile.PrimaryBiome; // Correct property
```

**4. Holistic Assessment: UX + Aesthetics + Functionality**
   - Not just "does it work?" but "is this intuitive at-a-glance?"
   - Example: Two-column layout wasn't required, but reduces vertical waste by 50%
   - Example: ⚠ icons for critical misses improve scannability without cluttering
   - Example: Specific filter names ("Cave", "Granite") vs generic ("map_features", "stones")
   - Plan layout changes with mockups/descriptions before coding

### When Progress Stalls

If stuck for more than 15 minutes:

1. **Stop coding** - Adding more code won't help if assumptions are wrong
2. **Add forensic logging** - Instrument the actual runtime behavior
3. **Dump to Player.log** - Get evidence of what's ACTUALLY happening
4. **Compare expected vs actual** - What did you assume? What's the evidence?
5. **Ask the right question**: "What's the correct solution?" not "What's the quick fix?"

### Example: Filter Name Resolution Investigation

**Symptoms:** Users see "map_features" instead of "Cave"

**Wrong approach:**
- Assume it's a display bug
- Quick fix: Replace "map_features" → "Cave" in UI code
- Ship it

**Right approach:**
1. ✅ Add DEBUG dump showing actual `FilterMatchInfo.FilterName` values
2. ✅ Read `FilterMatchInfo` struct → `FilterName` is just the filter ID
3. ✅ Read `MapFeatureFilter.cs` → `Id => "map_features"` (generic container)
4. ✅ Find `GetTileMapFeatures(tileId)` → Returns actual feature names per tile
5. ✅ Solution: Enhance `FormatFilterDisplayName(filterId, tileId)` to resolve specific items
6. ✅ Result: Shows "Cave", "Granite", "huge river" based on actual tile data

**Quality outcome:** Works for ANY mutator/river/road/stone, not just hard-coded cases

### Standards and Precision

**Projects languish when standards become muddy.** Maintain granular definition:

- ✅ `FilterImportance.Critical` vs `.Preferred` - precise enum, not bool
- ✅ `FilterHeaviness.Light` vs `.Heavy` - performance contract, not comments
- ✅ `FilterMatchInfo.IsCritical` - computed property, single source of truth
- ✅ Task IDs: `LZ-RESULTS-007` - specific, trackable, granular
- ❌ "The filter is important" - vague, no precision
- ❌ "This might be slow" - vague, use heaviness classification instead

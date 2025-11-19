# Stockpile Scoring Design (LZ-STOCKPILE-SCORING)

## Overview

Stockpile mutators represent abandoned supply caches that can provide valuable starting resources. This document outlines the design for discovering, caching, and scoring stockpile contents.

**Status:** Investigation phase - awaiting reflection results from DevToolsWindow investigation tool

**Rarity:** 0.029% of settleable tiles (205 out of 703,755 in canonical aggregate)

---

## Investigation Approach

### Pattern: Follow MineralRich Example

The MineralRich mutator provides a template for how to resolve mutator contents via reflection:

```csharp
// From MineralStockpileCache.cs lines 186-211
var mineralRichMutator = mutators.FirstOrDefault(m => m.defName == "MineralRich");
if (mineralRichMutator != null && mineralRichMutator.Worker != null)
{
    var planetTile = new PlanetTile(tileId, world.grid.Surface);

    // Use reflection to call PRIVATE GetMineableThingDefForTile
    var method = AccessTools.Method(mineralRichMutator.Worker.GetType(), "GetMineableThingDefForTile");
    if (method != null)
    {
        var oreDef = method.Invoke(mineralRichMutator.Worker, new object[] { planetTile }) as ThingDef;
        if (oreDef != null)
        {
            minerals.Add(oreDef.defName);
        }
    }
}
```

### Expected Discovery Patterns

Based on the MineralRich pattern, we expect to find ONE of these patterns for Stockpile:

**Pattern A: Private Getter Method (Most Likely)**
```csharp
// Expected signature (to be confirmed via reflection)
private ThingDef GetStockpileThingDefForTile(PlanetTile tile)
private List<ThingDef> GetStockpileContentsForTile(PlanetTile tile)
private IEnumerable<ThingDef> GetStockpileThingDefsForTile(PlanetTile tile)
```

**Pattern B: Cached Field**
```csharp
// Possible cached data structures
private ThingDef stockpileThingDef;
private List<ThingDef> stockpileContents;
private Dictionary<int, ThingDef> cachedStockpiles;
```

**Pattern C: Configuration Property**
```csharp
// Possible def-level configuration
public ThingDef stockpileType;
public List<ThingDef> allowedStockpileTypes;
```

**Pattern D: Label Parsing (Fallback)**
```csharp
// We know GetLabel(PlanetTile) returns "weapons stockpile", "medicine stockpile", etc.
// Could parse label text as last resort (fragile, localization issues)
public string GetLabel(PlanetTile tile) // Returns "weapons stockpile"
```

### Investigation Tool Status

Enhanced DevToolsWindow.cs InvestigateStockpileMutators() method:
- ✅ Scans up to 5 Stockpile mutators
- ✅ Enumerates ALL methods (public + non-public) with BindingFlags.NonPublic
- ✅ Enumerates ALL properties (public + non-public)
- ✅ Enumerates ALL fields (public + non-public)
- ✅ Logs visibility (public/private/protected/internal)
- ✅ Shows declaring type ([this] vs [base])
- ✅ Attempts to read promising field values (stockpile/thing/loot/content keywords)
- ✅ Attempts to invoke methods with PlanetTile parameter

**Next Step:** Run in-game, check Player.log for non-public member discovery

---

## Quality Rating System

### Stockpile Type → Quality Mapping

Based on RimWorld's loot categories and strategic value:

| Stockpile Type | Quality | Rationale |
|---|---|---|
| Weapons | +8 | High-value armory start (guns, melee weapons) |
| Medicine | +7 | Survival-critical (herbal→glitterworld meds) |
| Components | +6 | Industrial progression bottleneck |
| Steel/Plasteel | +6 | Core construction materials |
| Chemfuel | +5 | Power/trading commodity |
| Food (packaged) | +5 | Early-game survival buffer |
| Textiles | +4 | Clothing/bedding production |
| Stone blocks | +3 | Construction convenience |
| Wood | +2 | Common resource |

**Default (unknown type):** +4 (generic "something valuable" bonus)

### Quality Contribution Examples

- Tile with "weapons stockpile" → +8 to quality score
- Tile with "medicine stockpile" + "components stockpile" → +7 + +6 = +13
- Multiple stockpiles on one tile (ultra-rare) stack additively

---

## Cache Structure

### Already Implemented in MineralStockpileCache.cs

```csharp
// Lines 25-42: TileDetail struct
public readonly struct TileDetail
{
    public TileDetail(List<string> minerals, List<string> stockpiles)
    {
        Minerals = minerals;
        Stockpiles = stockpiles;
    }

    public List<string> Minerals { get; }
    public List<string> Stockpiles { get; } // ← Ready for implementation
}

// Lines 67-75: Accessor method
public List<string> GetStockpileTypes(int tileId)
{
    EnsureInitialized();
    return _cache != null && _cache.TryGetValue(tileId, out var detail)
        ? detail.Stockpiles
        : new List<string>();
}
```

### Implementation Target (Lines 213-218)

```csharp
// TODO: Resolve Stockpile contents when we add that feature
if (mutators.Any(m => m.defName == "Stockpile"))
{
    // Placeholder - we'd need to research how to resolve stockpile contents
    stockpileCount++;
}
```

**To be replaced with:**
```csharp
// Resolve Stockpile to specific loot types
var stockpileMutator = mutators.FirstOrDefault(m => m.defName == "Stockpile");
if (stockpileMutator != null && stockpileMutator.Worker != null)
{
    var planetTile = new PlanetTile(tileId, world.grid.Surface);

    // Use reflection to discover stockpile contents (method TBD from investigation)
    var method = AccessTools.Method(stockpileMutator.Worker.GetType(), "MethodNameTBD");
    if (method != null)
    {
        // Pattern depends on discovery results:
        // - Single ThingDef? stockpiles.Add(def.defName)
        // - List<ThingDef>? stockpiles.AddRange(defs.Select(d => d.defName))
        // - Label string? ParseStockpileLabel(label)

        stockpileCount++;
    }
}
```

---

## Filter Implementation

### StockpileFilter.cs (New File)

Similar to StoneFilter.cs pattern:

```csharp
public sealed class StockpileFilter : ISiteFilter
{
    public string Id => "stockpile";
    public FilterHeaviness Heaviness => FilterHeaviness.Light; // Cache is pre-computed

    public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
    {
        var filters = context.Filters;
        var stockpiles = filters.Stockpiles; // TBD: Add to FilterSettings

        if (!stockpiles.HasAnyImportance)
            return inputTiles;

        if (!stockpiles.HasCritical)
            return inputTiles; // Preferred handled by scoring

        var cache = context.State.MineralStockpileCache;

        return inputTiles.Where(id =>
        {
            var tileStockpiles = cache.GetStockpileTypes(id);
            if (tileStockpiles == null || tileStockpiles.Count == 0)
                return false;

            return stockpiles.MeetsCriticalRequirements(tileStockpiles);
        });
    }

    public float Membership(int tileId, FilterContext context)
    {
        // Similar to StoneFilter.Membership - count matches, apply operator
        // Quality contribution happens in scoring phase
    }
}
```

### FilterSettings.cs Changes (TBD)

```csharp
// Add stockpile preferences similar to Stones
public MultiImportance<string> Stockpiles { get; set; } = new MultiImportance<string>();
```

---

## Scoring Integration

### FilterService.cs Changes

Similar to stone contributions (lines ~500-520):

```csharp
// Add stockpile modifiers to breakdown
var stockpileTypes = context.State.MineralStockpileCache.GetStockpileTypes(tileId);
if (stockpileTypes != null && stockpileTypes.Count > 0)
{
    foreach (var stockpileType in stockpileTypes)
    {
        int quality = GetStockpileQuality(stockpileType);
        modifiers.Add(new FilterMatchInfo(
            FilterName: "stockpile",
            FilterId: "stockpile",
            MatchType: FilterMatchType.Modifier,
            Contribution: quality,
            Details: $"{stockpileType} stockpile"
        ));
    }
}

private int GetStockpileQuality(string stockpileType)
{
    // Map stockpile type to quality rating
    return stockpileType switch
    {
        "Weapons" => 8,
        "Medicine" => 7,
        "Components" => 6,
        "Steel" => 6,
        "Plasteel" => 6,
        "Chemfuel" => 5,
        "Food" => 5,
        "Textiles" => 4,
        "StoneBlocks" => 3,
        "WoodLog" => 2,
        _ => 4 // Unknown type gets generic bonus
    };
}
```

---

## Preset Integration

### Which Presets Should Prefer Stockpiles?

**Homesteader (Salvage theme):**
- Already has abandoned structures as Critical
- Stockpiles complement salvage theme → Add as Preferred
- Priority: Weapons (+8), Components (+6), Steel (+6)

**Defense:**
- Fortification focus
- Weapons stockpiles highly valuable → Add as Preferred
- Priority: Weapons (+8)

**Agrarian:**
- Food security focus
- Food stockpiles valuable → Add as Preferred
- Priority: Food (+5)

**Power:**
- Industrial focus
- Chemfuel/Components valuable → Add as Preferred
- Priority: Chemfuel (+5), Components (+6)

**Example Preset Addition (Homesteader):**
```csharp
// In Preset.cs Homesteader definition
Filters = new FilterSettings
{
    // ... existing filters ...
    Stockpiles = new MultiImportance<string>
    {
        Items = new Dictionary<string, FilterImportance>
        {
            ["Weapons"] = FilterImportance.Preferred,
            ["Components"] = FilterImportance.Preferred,
            ["Steel"] = FilterImportance.Preferred,
        },
        Operator = ImportanceOperator.OR
    }
}
```

---

## Implementation Checklist

### Phase 1: Investigation (Current)
- [x] Create DevToolsWindow investigation tool
- [x] Enhance to probe non-public members
- [ ] **BLOCKED:** Run in-game, collect Player.log results
- [ ] Identify private method/field for stockpile contents

### Phase 2: Cache Resolution
- [ ] Implement stockpile resolution in MineralStockpileCache.cs (lines 213-218)
- [ ] Add whitelist validation similar to validOres pattern
- [ ] Test cache build logs stockpile distribution

### Phase 3: Filter Implementation
- [ ] Create Source/Core/Filtering/Filters/StockpileFilter.cs
- [ ] Add Stockpiles property to FilterSettings.cs
- [ ] Register StockpileFilter in SiteFilterRegistry.cs
- [ ] Test Apply and Membership methods

### Phase 4: Scoring Integration
- [ ] Add GetStockpileQuality() helper to FilterService.cs
- [ ] Add stockpile modifiers to BuildDetailedBreakdown()
- [ ] Test MatchBreakdownV2 dumps show stockpile contributions

### Phase 5: Preset Integration
- [ ] Add Stockpile preferences to Homesteader preset
- [ ] Add to Defense, Agrarian, Power presets as appropriate
- [ ] Test preset searches surface stockpile tiles

### Phase 6: Documentation
- [ ] Update CLAUDE.md with stockpile scoring pattern
- [ ] Update P0_VALIDATION_SUMMARY.md (close LZ-STOCKPILE-SCORING)
- [ ] Add to filter_variables_catalog.md

---

## Testing Plan

### Unit Tests (In-Game)

1. **Cache Build Verification:**
   - Load world with known Stockpile count (DevTools button shows count)
   - Check Player.log for stockpile distribution summary
   - Verify defNames match expected patterns

2. **Filter Application:**
   - Create test preset with Critical stockpile requirement
   - Search, verify only stockpile tiles returned
   - Check logs show correct tile reduction

3. **Scoring Contributions:**
   - Find tile with known stockpile (e.g., "weapons stockpile")
   - Use [DEBUG] Dump Match Data button
   - Verify modifiers section shows: `Weapons stockpile: Quality +8`

4. **Preset Integration:**
   - Test Homesteader preset
   - Verify tiles with stockpiles + abandoned structures rank highest
   - Compare scores with/without stockpiles

---

## Open Questions (Pending Investigation)

1. **API Discovery:** What is the exact method/field to access stockpile contents?
   - Does TileMutatorWorker_Stockpile have a GetStockpileThingDefForTile method?
   - Is there a cached contents field?
   - Do we need to parse the GetLabel() result?

2. **Data Structure:** Single ThingDef or List<ThingDef>?
   - MineralRich returns single ThingDef
   - Stockpile might contain multiple items (e.g., weapons + ammo)

3. **defName Patterns:** What are the actual defNames?
   - Are they category names ("Weapons", "Medicine")?
   - Specific items ("Gun_Pistol", "MedicineIndustrial")?
   - Need whitelist like validOres

4. **Rarity Handling:** With 0.029% occurrence, worth the complexity?
   - Yes - high strategic value (starting resources)
   - Fits "salvage" theme for Homesteader preset
   - Pattern reusable for other rare mutators

---

**Status:** Ready for Phase 2 once reflection investigation completes.
**Next Step:** User runs DevToolsWindow investigation, provides Player.log output with non-public member discovery.

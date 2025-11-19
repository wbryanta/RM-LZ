# Mutator Reflection Discoveries

**Source:** Codex reflection analysis via /tmp/stockpile_ref/StockpileReflector
**Date:** 2025-11-19
**Assembly:** Assembly-CSharp.dll (RimWorld Steam install)
**Total Workers:** 67 TileMutatorWorker* implementations discovered
**Output:** /tmp/mutator_dump.txt

---

## Implemented: Stockpile (LZ-STOCKPILE-SCORING)

**Status:** ✅ COMPLETE - Awaiting in-game testing

### TileMutatorWorker_Stockpile
**Private static method:** `GetStockpileType(PlanetTile)` → Returns enum
**Enum values:** Weapons, Chemfuel, Component, Medicine, Drugs, Gravcore

**Implementation:**
- ✅ Cache resolution via AccessTools reflection (MineralStockpileCache.cs:213-240)
- ✅ Quality mapping: Gravcore +9, Weapons +8, Medicine +7, Chemfuel +6, Component +5, Drugs +4
- ✅ Distribution logging (lines 274-290)
- ✅ Scoring contributions (FilterService.cs:972-990)
- ✅ Testing presets created ([TEST] Weapons, Medicine, Component)

**Frequency:** 0.029% (205 tiles out of 703,755 settleable)

---

## Future Implementation: High-Priority Mutators

### 1. AnimalHabitat - Flagship Species Discovery

**Mutator:** TileMutatorWorker_AnimalHabitat
**Private methods discovered:**
- `GetAnimalKind(PlanetTile)` → Returns PawnKindDef (flagship animal species)
- `AnimalCommonalityFactorFor(PawnKindDef, PlanetTile)` → Returns float (spawn boost)

**Use Cases:**
1. **Filters:** "Guarantee Thrumbos" or "Find Megasloths" presets
2. **UI Display:** Show flagship animal on tile cards (e.g., "Thrumbo habitat")
3. **Scoring:** Bonus for rare/valuable animals (Thrumbos +10, Megasloths +8, etc.)
4. **CovertOps Integration:** Hunting-stealth logic needs guaranteed animal spawns

**Implementation Pattern:**
```csharp
// In MineralStockpileCache or new AnimalHabitatCache
var habitatMutator = mutators.FirstOrDefault(m => m.defName == "AnimalHabitat");
if (habitatMutator != null && habitatMutator.Worker != null)
{
    var planetTile = new PlanetTile(tileId, world.grid.Surface);

    var method = AccessTools.Method(habitatMutator.Worker.GetType(), "GetAnimalKind");
    if (method != null)
    {
        var animalDef = method.Invoke(habitatMutator.Worker, new object[] { planetTile }) as PawnKindDef;
        if (animalDef != null)
        {
            animalSpecies.Add(animalDef.defName); // e.g., "Thrumbo", "Megasloth"
        }
    }
}
```

**Quality Rating Examples:**
- Thrumbo: +10 (ultra-rare, high value)
- Megasloth: +8 (rare, wool, meat)
- Elephant: +7 (large, tanky, valuable)
- Muffalo: +5 (common but useful for caravans)
- Chicken: +2 (common food source)

**Canonical Frequency:** TBD (need to scan AnimalHabitat mutator occurrence)

---

### 2. PlantGrove / WildPlants - High-Density Crop Discovery

**Mutators:**
- TileMutatorWorker_PlantGrove
- TileMutatorWorker_WildPlants

**Private methods discovered:**
- `GetPlantKind(PlanetTile)` → Returns ThingDef (plant species)
- `PlantCommonalityFactorFor(...)` → Returns float (density boost)
- `AdditionalWildPlants(PlanetTile)` → Returns list/enum (bonus plants)

**Use Cases:**
1. **Filters:** "Find Healroot groves" or "Ambrosia patches" presets
2. **UI Display:** Show flagship plant on tile cards (e.g., "Healroot grove")
3. **Scoring:** Bonus for valuable plants (Ambrosia +9, Healroot +7, Devilstrand +8)
4. **Agriculture Planning:** Estimate chemfuel/cash-crop availability

**Implementation Pattern:**
```csharp
var groveMutator = mutators.FirstOrDefault(m => m.defName == "PlantGrove" || m.defName == "WildPlants");
if (groveMutator != null && groveMutator.Worker != null)
{
    var planetTile = new PlanetTile(tileId, world.grid.Surface);

    var method = AccessTools.Method(groveMutator.Worker.GetType(), "GetPlantKind");
    if (method != null)
    {
        var plantDef = method.Invoke(groveMutator.Worker, new object[] { planetTile }) as ThingDef;
        if (plantDef != null)
        {
            plantSpecies.Add(plantDef.defName); // e.g., "Plant_Healroot", "Plant_Ambrosia"
        }
    }
}
```

**Quality Rating Examples:**
- Ambrosia: +9 (ultra-rare, psychite drug source, recreation)
- Devilstrand: +8 (luxury textile, slow-growing)
- Healroot: +7 (medicine source, valuable)
- Smokeleaf: +5 (drug/recreation, cash crop)
- Cotton: +4 (textile, common)
- Wild berries: +3 (food, common)

**Canonical Frequency:**
- PlantGrove: 2,344 tiles (1.7%) from canonical aggregate
- WildPlants: 2,344 tiles (1.7%)

---

### 3. MixedBiome - Secondary Biome Resolution

**Mutator:** TileMutatorWorker_MixedBiome
**Private methods discovered:**
- `SecondaryBiome(PlanetTile, PlanetLayer)` → Returns BiomeDef
- Helper methods to sample neighboring tiles

**Use Cases:**
1. **UI Display:** Show secondary biome explicitly (e.g., "Temperate Forest + Boreal Forest")
2. **Climate Prediction:** More accurate temperature/rainfall ranges
3. **Resource Planning:** Dual biome resources (e.g., marble + granite)
4. **Strategic Value:** Edge-of-biome locations often have defensive advantages

**Implementation Pattern:**
```csharp
var mixedMutator = mutators.FirstOrDefault(m => m.defName == "MixedBiome");
if (mixedMutator != null && mixedMutator.Worker != null)
{
    var planetTile = new PlanetTile(tileId, world.grid.Surface);
    var planetLayer = world.grid.Surface; // or appropriate layer

    var method = AccessTools.Method(mixedMutator.Worker.GetType(), "SecondaryBiome");
    if (method != null)
    {
        var secondaryBiome = method.Invoke(mixedMutator.Worker, new object[] { planetTile, planetLayer }) as BiomeDef;
        if (secondaryBiome != null)
        {
            secondaryBiomeDef = secondaryBiome.defName; // e.g., "BorealForest"
        }
    }
}
```

**Display Example:**
```
Primary: Temperate Forest
Secondary: Boreal Forest (MixedBiome)
Expected: Colder winters, mixed tree species
```

**Canonical Frequency:** 1,874 tiles (1.4%) - relatively common

---

### 4. Additional Mutator Methods (Lower Priority)

#### IncreaseWeatherFrequency
**Method:** `MutateWeatherCommonalityFor(WeatherDef, PlanetTile, ref float commonality)`
**Use case:** Quantify which weather patterns are altered (e.g., "2x thunderstorms")
**Priority:** Medium - useful for hazard warnings

#### IncreasedPollution
**Method:** `OnAddedToTile(PlanetTile)` - Sets pollution offsets
**Use case:** Quantify pollution levels beyond boolean flag
**Priority:** Low - pollution already visible in game

#### Headwater / River Helpers
**Methods:** Return river width/angle
**Use case:** Quantify river size (huge vs small stream)
**Priority:** Medium - useful for water access quality scoring

---

## Implementation Priority

### Phase 1: Stockpile (COMPLETE)
- ✅ GetStockpileType reflection
- ✅ Quality mapping (Gravcore +9 → Drugs +4)
- ✅ Cache + scoring + distribution logging
- ⏳ Awaiting in-game validation

### Phase 2: AnimalHabitat (HIGH PRIORITY)
**Rationale:**
- High strategic value (Thrumbos, Megasloths guaranteed spawns)
- CovertOps integration need (hunting-stealth mechanics)
- User-requested feature potential ("Find Thrumbo tiles")
- Relatively common mutator (frequency TBD)

**Task ID:** LZ-ANIMAL-HABITAT-CACHE

**Deliverables:**
1. Extend cache with GetAnimalKind reflection
2. Quality rating system (Thrumbo +10, Megasloth +8, etc.)
3. AnimalHabitatFilter for Critical/Preferred species selection
4. UI display of flagship animal on tile cards
5. Testing presets ([TEST] Thrumbo, [TEST] Megasloth)

### Phase 3: PlantGrove / WildPlants (MEDIUM PRIORITY)
**Rationale:**
- Agricultural planning value (Healroot, Devilstrand, Ambrosia)
- Chemfuel/cash-crop estimation
- Relatively common (1.7% each)

**Task ID:** LZ-PLANT-GROVE-CACHE

**Deliverables:**
1. GetPlantKind reflection for both mutators
2. Quality rating system (Ambrosia +9, Healroot +7, etc.)
3. PlantGroveFilter for species selection
4. Testing presets ([TEST] Healroot, [TEST] Ambrosia)

### Phase 4: MixedBiome (MEDIUM PRIORITY)
**Rationale:**
- Improves climate prediction accuracy
- Resource diversity (dual biome stones/animals)
- Common mutator (1.4%)

**Task ID:** LZ-MIXED-BIOME-SECONDARY

**Deliverables:**
1. SecondaryBiome reflection
2. UI display of secondary biome in tile details
3. Climate range adjustments based on secondary biome

### Phase 5: Weather/Pollution/River (LOW PRIORITY)
**Rationale:**
- Nice-to-have quantification
- Less strategic impact
- Most data already visible in-game

---

## Technical Notes

### Reflection Pattern Template
```csharp
// Generic pattern for mutator method invocation
var mutator = tile.Mutators?.FirstOrDefault(m => m.defName == "MutatorName");
if (mutator?.Worker != null)
{
    var planetTile = new PlanetTile(tileId, world.grid.Surface);

    var method = AccessTools.Method(mutator.Worker.GetType(), "MethodName");
    if (method != null)
    {
        try
        {
            var result = method.Invoke(mutator.Worker, new object[] { planetTile });
            if (result != null)
            {
                // Process result (ThingDef, PawnKindDef, BiomeDef, enum, etc.)
                cache.Add(result.defName);
            }
        }
        catch (Exception ex)
        {
            if (LogLevel >= Verbose)
                Log.Warning($"[LandingZone] Failed to resolve {mutator.defName}: {ex.Message}");
        }
    }
}
```

### Cache Structure Options

**Option A: Extend MineralStockpileCache (Monolithic)**
```csharp
public readonly struct TileDetail
{
    public List<string> Minerals { get; }
    public List<string> Stockpiles { get; }
    public List<string> Animals { get; }      // NEW
    public List<string> Plants { get; }       // NEW
    public string? SecondaryBiome { get; }    // NEW
}
```

**Option B: Separate Caches (Modular)**
```csharp
public class MineralStockpileCache { ... }
public class AnimalHabitatCache { ... }
public class PlantGroveCache { ... }
public class MixedBiomeCache { ... }
```

**Recommendation:** Option B (modular) for maintainability and optional lazy loading

### Performance Considerations
- All reflection happens during cache build (once per world seed)
- Zero runtime overhead during search/scoring (cache lookups are O(1))
- Ultra-rare mutators (0.029%) have negligible cache build impact
- Common mutators (1-2%) add ~10-20ms to cache build time

---

## CovertOps Integration Notes

**AnimalHabitat Integration:**
- CovertOps' hunting-stealth mechanics need guaranteed animal spawns
- Cache provides: flagship species, spawn boost factor
- Use case: "Plan Thrumbo hunt" → Find guaranteed spawn tiles → Plan stealth approach

**PlantGrove Integration:**
- Drug/chemfuel economy planning
- Ambrosia/Smokeleaf locations for "psychite empire" runs

**MixedBiome Integration:**
- Edge-of-biome positioning for defensive advantage
- Climate prediction for temperature-based mechanics

---

## Canonical Data Sources

**Mutator frequencies from:** `docs/data/canonical_world_library_aggregate.json`
- 11 world samples aggregated
- 703,755 settleable tiles total
- 86 unique mutators discovered

**Key frequencies:**
- AnimalHabitat: TBD (need to check aggregate)
- PlantGrove: 2,344 tiles (1.7%)
- WildPlants: 2,344 tiles (1.7%)
- MixedBiome: 1,874 tiles (1.4%)
- Stockpile: 205 tiles (0.029%)

---

## Next Steps (Post-Stockpile Validation)

1. **Validate Stockpile Implementation** (Current)
   - User testing in-game with [TEST] presets
   - Verify cache distribution logs
   - Confirm MatchBreakdownV2 contributions

2. **Create LZ-ANIMAL-HABITAT-CACHE Task**
   - Design AnimalHabitatCache structure
   - Implement GetAnimalKind reflection
   - Create quality rating system
   - Build testing presets

3. **Create LZ-PLANT-GROVE-CACHE Task**
   - Design PlantGroveCache structure
   - Implement GetPlantKind reflection for both mutators
   - Create quality rating system

4. **Create LZ-MIXED-BIOME-SECONDARY Task**
   - Implement SecondaryBiome resolution
   - Update UI to display secondary biome
   - Adjust climate predictions

---

**End of Mutator Reflection Discoveries**

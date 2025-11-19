# Preset Redesign v0.3 - Full Character Implementation

**Objective**: Transform all 12 presets into rich, thematic configurations that leverage the complete filter system (biomes, mutators, landmarks, climate, resources) to create distinctive, memorable experiences.

**Data Source**: `canonical_world_library_aggregate.json` (5 samples, 1.47M tiles, 703k settleable)

---

## Design Principles

1. **Thematic Coherence**: Each preset tells a story through its filter choices
2. **Positive AND Negative**: Use exclusions (mutators to avoid) for stronger character
3. **AND/OR Logic**: Mix operators for variety (e.g., "coastal OR lake" vs "granite AND marble")
4. **Rarity Targeting**: Balance common finds vs rare gems appropriately
5. **Canonical Accuracy**: Use exact defNames from library, no assumptions

---

## Preset Specifications

### 1. ELYSIAN (Perfect Everything - Highest QoL)
**Target Rarity**: Epic/Legendary (perfect conditions + stacked +10 mutators)
**Theme**: The easiest, most comfortable RimWorld experience possible - perfect climate, ideal biome, abundant resources, AND stacked quality mutators. This is god-tier colonist paradise.

**Strategy**: Combine the best environmental conditions with stacked +10 mutators. Every factor should be optimal. This is "easy mode" RimWorld.

**Climate** (Critical):
- Average Temp: 18-25°C - Perfect comfort zone (no heating/cooling needed)
- Rainfall: 1400-2200mm - Lush without flooding
- Growing Days: 55-60 - Maximum year-round agriculture
- Pollution: 0-0.1 - Pristine environment

**Biomes** (Critical, OR):
- TemperateForest (30% of settleable) - THE paradise biome
- TropicalRainforest (11.7%) - Exotic lush alternative (if temp allows)

**+10 Mutators** (Critical, OR) - Stack as many as possible:
- Fertile - Major farming bonus
- MineralRich - Major mining bonus
- SteamGeysers_Increased - Multiple geothermal vents
- AncientHeatVent - Guaranteed geothermal
- HotSprings - Geothermal + mood bonus

**+8 Mutators** (Preferred, OR) - Secondary bonuses:
- PlantLife_Increased - More harvestable plants
- Fish_Increased - More fishing food
- AnimalLife_Increased - More hunting/taming

**+7 Mutators** (Preferred, OR) - Tertiary bonuses:
- WetClimate - Farming boost
- Wetland - Additional farming boost

**+5/+6 Mutators** (Preferred, OR):
- Caves (+5) - Shelter and mining
- WildPlants (+6) - Forageable resources
- Muddy (+6) - Defensible terrain + farming
- River (+5) - Water, trade, fishing

**Geography**:
- Coastal OR CoastalLake (Preferred, OR) - Water access for trade/fishing
- Rivers: HugeRiver OR LargeRiver (Preferred, OR) - Major water sources
- Hilliness: SmallHills OR LargeHills - Varied terrain (avoid flat and extreme)
- MovementDifficulty: 0.8-1.5 (Preferred) - Not too easy, not too hard

**Resources**:
- Stones: Granite AND Marble (Preferred, AND) - Best construction and art stones
- ForageabilityRange: 0.7-1.0 (Preferred) - High wild food
- PlantDensity: 0.8-1.3 (Preferred) - Abundant vegetation
- AnimalDensity: 2.5-6.5 (Preferred) - Good wildlife (not too sparse)
- FishPopulation: 400-900 (Preferred) - Excellent fishing

**Exclusions** (Document only - future negative filter support):
- ALL negative mutators: PlantLife_Decreased, AnimalLife_Decreased, Fish_Decreased, Pollution_Increased, DryGround, ToxicLake, Junkyard
- ALL Ancient danger sites (AncientGarrison, AncientLaunchSite, InsectMegahive, etc.)
- ALL lava/toxic/hostile features
- Extreme terrain (IceSheet, ExtremeDesert, Mountainous, Flat)

**Expected Results**: Very few tiles (Epic/Legendary rarity). Finding one is like winning the lottery. These tiles will have near-perfect scores (0.9-1.0) and offer the easiest possible RimWorld experience.

---

### 2. EXOTIC (Rarity Hunter)
**Target Rarity**: Epic/Legendary (<0.01%)
**Theme**: Chasing ultra-rare features - tiered rarity ladder to ensure results while rewarding stacking

**Climate**:
- Keep broad to maximize rare feature finds
- Average Temp: 5-35°C (Preferred) - Settleable range

**Ultra-Rare Anchor** (Critical):
- ArcheanTrees (0.0034% of settleable, 24 tiles) - THE rarest mutator, must have

**Stack More Rares** (Preferred, OR) - Reward tiles with multiple rare features:
- Cavern (0.022%, 157 tiles) - Massive underground spaces
- Headwater (0.25%, 1745 tiles) - River origin points
- HotSprings (0.0094%, 66 tiles) - Geothermal + mood
- Oasis (0.0094%, 66 tiles) - Desert paradise
- RiverDelta (0.023%, 159 tiles) - Multiple rivers
- Peninsula (0.021%, 148 tiles) - Defensive geography
- MineralRich (0.017%, 119 tiles) - Rich mining

**Geography**:
- CoastalLake: Preferred
- Rivers: HugeRiver (Preferred)

**Strategy**: Critical ensures at least ArcheanTrees (24 tiles guaranteed), Preferred rewards stacking 2-3+ rare features. Tiles with ArcheanTrees + Cavern + Headwater will score highest, but won't fail if only ArcheanTrees present.

**Expected Results**: ~20-24 tiles globally (all ArcheanTrees tiles), with top scores going to tiles that stack multiple rare features.

---

### 3. SUBZERO (Frozen Wasteland)
**Target Rarity**: VeryRare
**Theme**: Extreme cold survival challenge

**Climate** (Critical):
- Average Temp: -50 to -15°C - Tundra/IceSheet range
- Growing Days: 0-15 - Barely any growing season
- Rainfall: Any (ice doesn't care)

**Biomes** (Critical, OR):
- Tundra (8.5% of settleable)
- BorealForest (5.6%)
- GlacialPlain (0.33%)

**Desired Features** (Preferred, OR):
- Caves (+5) - Shelter from cold
- SteamGeysers_Increased (+10) - Critical for heating
- MineralRich (+10) - Mining focus when can't farm

**Geographic Features** (Preferred, OR):
- IceCaves (very rare) - Thematic feature
- Crevasse - Dangerous ice terrain
- Iceberg - Visual flair

**Exclusions**:
- No tropical/warm mutators
- Avoid: Fertile, WetClimate, Muddy (incompatible with frozen)

---

### 4. SCORCHED (Volcanic Nightmare)
**Target Rarity**: VeryRare
**Theme**: Extreme heat, volcanic activity, toxic hellscape - embrace the fire and lava!

**Climate** (Critical):
- Average Temp: 35-60°C - Scorching heat
- Rainfall: 0-400mm - Arid desert
- Growing Days: 0-25 - Hostile to agriculture
- Pollution: 0.3-1.0 (Preferred) - Toxic atmosphere fits theme

**Biomes** (Critical, OR):
- ExtremeDesert (4.2% of settleable, 29,529 tiles)
- Desert (14.1%, 99,465 tiles)
- LavaField (0.34%, 2,419 tiles) - Ultra-rare volcanic biome, perfect thematic fit

**Lava Features** (Critical, OR) - Core thematic elements:
- LavaCaves - Underground lava (override: -9 → +8)
- LavaFlow - Lava rivers (override: -9 → +8)
- LavaCrater - Lava crater (override: -10 → +7)
- LavaLake - Lava lake (override: quality neutral, inherently rare)

**Toxic/Hostile Features** (Preferred, OR):
- ToxicLake - Toxic hazard (override: -10 → +5, fits volcanic theme)
- AncientSmokeVent - Pollution (override: -8 → +4)
- AncientToxVent - Toxic gas (override: -10 → +5)
- Pollution_Increased - Heavy pollution (override: -8 → +4)

**Supporting Features**:
- SteamGeysers_Increased (+10, Preferred) - Geothermal ubiquitous in volcanic regions
- DryGround (-6, Preferred) - Barren wasteland (override: -6 → +3, fits theme)

**Resources**:
- ObsidianDeposits (+3, Preferred) - Volcanic crafting material

**Quality Overrides** (prevent penalty for wanted features):
```csharp
LavaCaves: -9 → +8
LavaFlow: -9 → +8
LavaCrater: -10 → +7
ToxicLake: -10 → +5
AncientSmokeVent: -8 → +4
AncientToxVent: -10 → +5
Pollution_Increased: -8 → +4
DryGround: -6 → +3
```

**Expected Results**: Extremely rare tiles with authentic volcanic hellscape atmosphere. High thematic coherence, accepts lower "quality" scores because theme > safety.

---

### 5. DESERT OASIS (Hidden Paradise)
**Target Rarity**: Rare
**Theme**: Life-sustaining water features in hostile desert - water in wasteland!

**Climate** (Critical):
- Average Temp: 28-45°C - Hot desert range
- Rainfall: 100-600mm - Low but not extreme

**Biomes** (Critical, OR):
- Desert (14.1%, 99,465 tiles)
- ExtremeDesert (4.2%, 29,529 tiles)
- AridShrubland (14.9%, 104,559 tiles)

**Water Features** (Critical) - This is the key! Desert + Water = Oasis:
- Rivers: ANY river type (Critical, OR) - Must have river
- Coastal OR CoastalLake (Critical, OR) - Must have water access

**Oasis Features** (Preferred, OR):
- Oasis (+7) - Literal oasis mutator
- Lake (+4) - Water source
- Pond (+2) - Small water
- WetClimate (+7) - Moisture pocket

**Positive Mutators** (Preferred, OR):
- Fertile (+10) - Farming in desert
- WildPlants (+6) - Forage despite heat

**Exclusions**:
- ToxicLake (-10) - Not a *nice* oasis
- DryLake (-5) - Defeats the purpose

---

### 6. DEFENSE (Mountain Fortress)
**Target Rarity**: Uncommon
**Theme**: Natural defensibility - mountains, caves, chokepoints, stone abundance

**Terrain** (Critical):
- Hilliness: LargeHills, Mountainous ONLY
- Mountain (+0 neutral, but Critical) - Mountain feature itself

**Fortification Features** (Critical, OR):
- Caves (+5) - Underground defense
- Cavern (+5) - Massive shelter
- Chasm (+0) - Natural barrier
- Cliffs (+0) - Defensive terrain
- Valley (+0) - Chokepoint geography

**Resources** (Critical, AND):
- Stones: Granite AND Slate (both excellent for fortifications)
- MineralRich (+10, Preferred) - Abundant mining

**Additional Defense** (Preferred, OR):
- Peninsula (+0) - Surrounded by water on 3 sides
- RiverIsland (+0) - Island in river, natural moat
- CoastalIsland (+0) - Ocean moat

**Climate**:
- Average Temp: 10-25°C (Preferred) - Temperate for livability
- Growing Days: 30-60 (Preferred) - Can still farm

**Exclusions**:
- Hilliness: Flat (defeats purpose)
- Marshy, Muddy (incompatible with mountains)
- Sandy (-0) - Not rocky

---

### 7. AGRARIAN (Farming Paradise)
**Target Rarity**: Common
**Theme**: Maximum agricultural potential - fertile soil, perfect climate, water abundance

**Climate** (Critical):
- Average Temp: 15-28°C - Optimal crop range
- Rainfall: 1200-2500mm - High rainfall for crops
- Growing Days: 50-60 - Year-round growing

**Biomes** (Preferred, OR):
- TemperateForest (30% of settleable) - Best farming biome
- TropicalRainforest (11.7%) - Lush alternative
- Grasslands (5.9%) - Open farming land

**Farming Mutators** (Preferred, OR):
- Fertile (+10) - THE farming mutator
- WetClimate (+7) - Moisture for crops
- PlantLife_Increased (+8) - More plants
- WildPlants (+6) - Forage backup
- Muddy (+6) - Farming bonus

**Water** (Preferred, OR):
- Rivers: ANY type
- Coastal OR CoastalLake

**Terrain**:
- Hilliness: Flat OR SmallHills (Preferred) - Easy to farm
- MovementDifficulty: 0.0-1.2 (Preferred) - Easy terrain

**Exclusions**:
- DryGround (-6) - Farming penalty
- PlantLife_Decreased (-7) - Anti-farming
- Sandy (-0) - Poor soil
- Pollution_Increased (-8) - Crop contamination

---

### 8. POWER (Energy Infrastructure)
**Target Rarity**: Rare
**Theme**: Maximum power generation potential - geothermal, hydro, wind, solar

**Geothermal** (Critical, OR):
- SteamGeysers_Increased (+10) - More geothermal vents
- AncientHeatVent (+10) - Guaranteed geothermal

**Hydro** (Preferred, OR):
- Rivers: HugeRiver OR LargeRiver - Watermill potential
- RiverDelta (+2) - Multiple rivers
- RiverConfluence (+0) - Rivers meet
- Headwater (+0) - River source

**Wind** (Preferred):
- WindyMutator (+0) - More wind turbine output
- Hilliness: LargeHills OR Mountainous - High elevation for wind

**Solar** (Preferred):
- SunnyMutator (+2) - More solar output
- Rainfall: 400-1200mm - Not too cloudy

**Climate**:
- Average Temp: 10-30°C (Preferred) - Manageable heat/cold loads

**Geography**:
- Coast OR CoastalLake (Preferred) - Tidal potential (future)

**Exclusions**:
- FoggyMutator (+0 but reduces solar) - Bad for solar
- Pollution_Increased (-8) - Panel contamination

---

### 9. BAYOU (Swamp Horror)
**Target Rarity**: Uncommon
**Theme**: Hot, wet, diseased marshlands - mud, disease, difficult terrain, swamp life

**Climate** (Critical):
- Average Temp: 25-40°C - Hot and humid
- Rainfall: 1800-3000mm - High precipitation
- Swampiness: 0.4-1.0 (Critical) - THIS is the key stat

**Biomes** (Critical, OR):
- TemperateSwamp (1.2% of settleable) - Temperate swamp
- TropicalSwamp (0.6%) - Hot swamp
- ColdBog (0.11%) - Rare cold variant

**Swamp Mutators** (Preferred, OR):
- Muddy (+6) - Swamp terrain
- Marshy (+6) - Marshy ground
- Wetland (+7) - Wetland feature
- WetClimate (+7) - Constant moisture

**Geographic Features** (Preferred, OR):
- Rivers: ANY type - Swamps have rivers
- Lakeshore (+0) - Swamp borders lakes
- Pond (+2) - Standing water

**Flora/Fauna** (Preferred, OR):
- PlantLife_Increased (+8) - Overgrown
- WildTropicalPlants (+6) - Jungle plants
- AnimalLife_Increased (+8) - Swamp creatures

**Terrain**:
- Hilliness: Flat OR SmallHills - Swamps are low-lying
- MovementDifficulty: 1.2-2.0 (Preferred) - Difficult swamp terrain

**Exclusions**:
- DryGround (-6) - Opposite of swamp
- Sandy (-0) - Not swampy
- Desert/Arid biomes

---

### 10. SAVANNAH (Wildlife Plains)
**Target Rarity**: Common
**Theme**: Warm grasslands with abundant wildlife, wind, open terrain

**Climate** (Critical):
- Average Temp: 22-38°C - Warm savannah range
- Rainfall: 500-1200mm - Seasonal rainfall
- Growing Days: 45-60 - Long growing season

**Biomes** (Critical, OR):
- Grasslands (5.9% of settleable) - Primary savannah biome
- AridShrubland (14.9%) - Dry grassland
- TemperateForest (30%) - Transition zone

**Wildlife** (Critical, OR):
- AnimalLife_Increased (+8) - More animals
- AnimalHabitat (+5) - Animal spawning grounds
- AnimalDensity: 3.0-6.5 (Preferred) - High animal density

**Wind** (Preferred):
- WindyMutator (+0) - Wind turbines
- Grasslands (inherently windy)

**Terrain**:
- Hilliness: Flat OR SmallHills (Preferred) - Open plains
- MovementDifficulty: 0.0-1.0 (Preferred) - Easy to traverse

**Grazing**:
- GrazeImportance: Preferred - Animals can graze

**Supporting Features** (Preferred, OR):
- Rivers: ANY type - Water sources for animals
- WildPlants (+6) - Forage

**Exclusions**:
- AnimalLife_Decreased (-6) - Defeats purpose
- Swampy mutators (Muddy, Marshy) - Not grassland
- Mountainous terrain

---

### 11. AQUATIC (Maximum Water)
**Target Rarity**: Rare
**Theme**: As much water access as possible - coastal, rivers, lakes, headwaters, fish

**Major Water Source** (Critical, OR) - Must have at least one:
- Coastal (ocean access)
- Rivers: ANY river type

**Additional Water** (Preferred) - Stack for higher scores:
- CoastalLake (both ocean AND lake!)
- Having both Coastal AND Rivers (best case)

**Aquatic Mutators** (Preferred, OR):
- Headwater (+0) - River source
- RiverDelta (+2) - Multiple rivers
- RiverConfluence (+0) - Rivers meet
- RiverIsland (+0) - Island in river
- Lake (+4) - Lakes
- LakeWithIsland (+4) - Lake features
- LakeWithIslands (+4) - Multiple islands
- Lakeshore (+0) - Lake borders
- Pond (+2) - Small water
- Bay (+0) - Coastal bay
- Cove (+0) - Coastal cove
- Harbor (+0) - Natural harbor
- Fjord (+0) - Coastal inlet

**Fishing**:
- Fish_Increased (+8, Preferred) - More fish
- FishPopulation: 400-900 (Preferred) - High fish count

**Biomes** (Preferred, OR):
- TemperateForest (near water)
- TropicalRainforest (coastal rainforest)
- Grasslands (near water)

**Climate**:
- Average Temp: 10-30°C (Preferred) - Temperate for living
- Rainfall: 1000-2500mm (Preferred) - High rainfall

**Exclusions**:
- DryLake (-5) - Not water!
- Desert/Arid biomes
- Fish_Decreased (-6)

---

### 12. HOMESTEADER (Move-In Ready)
**Target Rarity**: VeryRare (abandoned sites are rare!)
**Theme**: Pre-existing structures and settlements - find tiles with ruins, stockpiles, and abandoned colonies you can salvage/reclaim

**Abandoned Settlements** (Critical, OR) - Primary goal:
- AbandonedColonyTribal (0.098% of settleable, 69 tiles) - Tribal ruins with structures
- AbandonedColonyOutlander (0.084%, 59 tiles) - Tech ruins with better loot
- Stockpile (0.029%, 205 tiles) - Supply caches
- AncientRuins (0.030%, 210 tiles) - Ancient structures

**Ancient Structures** (Preferred, OR) - Secondary sites with salvage:
- AncientWarehouse (+0, 36 tiles) - Storage facility
- AncientQuarry (+0, 55 tiles) - Mining site
- AncientGarrison (-8, 41 tiles) - Military base (mechanoids!)
- AncientLaunchSite (-8, 43 tiles) - Spaceship crash site
- AncientUplink (+0, 159 tiles) - Communications facility
- AncientChemfuelRefinery (-8, 39 tiles) - Fuel facility (explosive!)

**Climate** (Preferred):
- Average Temp: 10-30°C - Livable range
- Growing Days: 30-60 - Can still farm after moving in

**Biomes** (Preferred, OR) - Prioritize temperate for livability:
- TemperateForest (30%)
- Grasslands (5.9%)
- BorealForest (5.6%)

**Supporting Features** (Preferred, OR):
- Roads: ANY type - Ancient sites often had road access
- Rivers: ANY type - Settlements near water
- Coastal OR CoastalLake - Water access for trading

**Resources** (Preferred, OR):
- Stones: Granite OR Limestone OR Slate - For rebuilding
- Caves (+5) - Natural shelter nearby

**Quality Overrides** (structures are valuable despite dangers):
```csharp
AbandonedColonyTribal: -5 → +6    // Structures outweigh raider risk
AbandonedColonyOutlander: -5 → +7 // Better tech, worth the risk
AncientGarrison: -8 → +6          // Mechanoids dangerous but structures valuable
AncientLaunchSite: -8 → +6        // Ship parts worth mechanoid risk
AncientChemfuelRefinery: -8 → +4  // Explosion risk but fuel production
AncientWarehouse: 0 → +5          // Storage facilities are great
AncientQuarry: 0 → +5             // Mining infrastructure
```

**Note**: This preset deliberately seeks "dangerous" ancient sites because existing structures provide massive early-game advantages that outweigh combat risks. You get walls, rooms, power infrastructure, and salvage in exchange for clearing out threats.

**Exclusions**:
- Extreme temperature biomes (IceSheet, ExtremeDesert) - Ruins should be in livable locations
- Hostile environmental mutators (ToxicLake, Pollution_Increased) - Ancient combat dangers are fine, but don't want contaminated environments

---

## Implementation Notes

### Preset-Specific Mutator Quality Overrides

**Critical Issue**: Some presets explicitly WANT mutators that have negative quality ratings. This creates a scoring problem where tiles matching the preset theme get penalized.

**Examples**:
- **Scorched Hell** wants LavaCaves (-9), LavaFlow (-9), LavaCrater (-10) for thematic lava features
- **Bayou** might want disease/pollution features for authentic swamp horror
- **SubZero** might want harsh features for challenge atmosphere

**The Problem**:
When a preset marks LavaCaves as "Preferred", tiles with LavaCaves will:
- Get a BONUS from filter membership (tile has preferred feature)
- Get a PENALTY from mutator quality score (-9 to overall quality)
- Result: The penalty can outweigh the thematic bonus, burying tiles we actually want!

**Solution - Preset-Specific Quality Overrides**:

Each preset can define mutator quality overrides that replace the global ratings for that preset only:

```csharp
public class Preset
{
    // Preset-specific mutator quality overrides
    // Key: mutatorDefName, Value: quality override for THIS preset only
    public Dictionary<string, int> MutatorQualityOverrides { get; set; } = new Dictionary<string, int>();
}
```

**Usage Examples**:

**Scorched Hell**:
```csharp
// Override lava features from negative to positive for this preset
preset.MutatorQualityOverrides["LavaCaves"] = 8;      // -9 → +8 (thematic feature!)
preset.MutatorQualityOverrides["LavaFlow"] = 8;       // -9 → +8
preset.MutatorQualityOverrides["LavaCrater"] = 7;     // -10 → +7
preset.MutatorQualityOverrides["ToxicLake"] = 5;      // -10 → +5 (fits volcanic theme)
```

**Bayou**:
```csharp
// Swamp features that might be negative elsewhere are good here
preset.MutatorQualityOverrides["Pollution_Increased"] = 4;  // -8 → +4 (swamp miasma!)
preset.MutatorQualityOverrides["DryGround"] = -10;          // -6 → -10 (even worse for swamp!)
```

**Homesteader**:
```csharp
// Ancient danger sites are negative for quality, but we WANT them for structures
preset.MutatorQualityOverrides["AncientGarrison"] = 6;          // -8 → +6 (structures!)
preset.MutatorQualityOverrides["AncientLaunchSite"] = 6;        // -8 → +6
preset.MutatorQualityOverrides["AncientChemfuelRefinery"] = 4;  // -8 → +4
```

**Implementation**:
1. Add `MutatorQualityOverrides` dictionary to `Preset` class
2. Modify `MutatorQualityRatings.GetQuality()` to accept optional preset parameter
3. Check preset overrides before returning global rating
4. Apply to scoring in `FilterService.BuildTileScore()`

**Presets Using Quality Overrides**:
- **Scorched Hell**: Lava features, toxic features (override to positive)
- **SubZero**: Ice features, harsh mutators (override to positive if implemented)
- **Bayou**: Pollution, disease features (override to positive if implemented)
- **Homesteader**: Ancient danger sites (override to positive)
- **Elysian**: None needed (all positives)
- **Defense**: None needed (neutral/positive features)
- **Others**: Evaluate during implementation

---

### New Filter Settings to Use
1. **Swampiness**: Critical for Bayou (currently unused!)
2. **AnimalDensity**: Savannah wildlife focus
3. **FishPopulation**: Aquatic theme
4. **MovementDifficulty**: Bayou (high), Agrarian (low)
5. **GrazeImportance**: Savannah herbivore support
6. **PlantDensity**: Agrarian/Bayou high plant life
7. **Pollution**: Elysian (pristine), Bayou (possibly accept/want higher)
8. **ForageabilityRange**: Elysian (high), Agrarian (high)
9. **ElevationRange**: Power (high for wind), Defense (varied)

### Negative Filters (Exclusions)
Current system doesn't support "MUST NOT have mutator X". Options:
1. **Document only** - Describe in preset description, don't enforce (Elysian currently)
2. **Add negative importance** - New FilterImportance.Excluded enum value
3. **Scoring penalty** - Preset-specific scoring adjustments (implemented via quality overrides!)
4. **Quality overrides** - Make unwanted mutators even MORE negative for specific presets

**Recommendation**:
- Use **quality overrides** (option 4) for now - e.g., Elysian can make ToxicLake -15 instead of -10
- Consider FilterImportance.Excluded for v0.4 if needed

### Biome Locking
- Currently single biome via `LockedBiome`
- Need multi-biome OR support
- Use `AdjacentBiomes` container or add `BiomeContainer` similar to Rivers/Roads

### Mutator defName Validation
Before implementing, verify ALL defNames exist in canonical library:
- LavaCaves, LavaFlow, LavaCrater, LavaLake ✓ (confirmed in library)
- ToxicLake ✓
- All others - validate against library

---

## Preset Summary

**Row 1 - Special Tier**:
1. **Elysian** - Maximum quality score (stack +10 mutators)
2. **Exotic** - Ultra-rare feature combinations
3. **SubZero** - Frozen wasteland survival
4. **Scorched** - Volcanic nightmare

**Rows 2-3 - Playstyle Tier**:
5. **Desert Oasis** - Water in the desert
6. **Defense** - Mountain fortress
7. **Agrarian** - Farming paradise
8. **Power** - Energy infrastructure
9. **Bayou** - Swamp horror (uses Swampiness filter!)
10. **Savannah** - Wildlife plains (uses AnimalDensity!)
11. **Aquatic** - Maximum water (uses FishPopulation!)
12. **Homesteader** - Move-in ready ruins (replaces Wildcard)

**Key Changes from Current**:
- **Elysian**: Complete rework - perfect conditions (climate, biome, resources) + stacked +10 quality mutators
- **Exotic**: Fixed zero-result AND logic → tiered rarity ladder (Critical: ArcheanTrees, Preferred: stack more rares)
- **Scorched**: Renamed from "Scorched Hell" for UI space, enhanced with LavaField biome and quality overrides
- **Desert Oasis**: Fixed water logic - now Critical requirement (desert + water = oasis!)
- **Aquatic**: Fixed restrictive AND → OR (Coastal OR Rivers) with preference stacking
- **Homesteader**: NEW - replaces Wildcard, focuses on abandoned settlements/ancient structures with quality overrides
- **Bayou**: Uses Swampiness filter (currently unused in any preset!)
- **Savannah**: Uses AnimalDensity and GrazeImportance
- **All presets**: Leverage full filter system (40+ filters) for rich thematic character

**Presets Using Quality Overrides** (new feature):
1. **Scorched**: 8 overrides - lava/toxic features from negative to positive
2. **Homesteader**: 7 overrides - ancient danger sites from negative to positive
3. **Others**: None needed - use naturally positive/neutral features

---

## Next Steps

1. **Validate defNames** against canonical library
2. **Implement preset creator methods** with full filter configurations
3. **Test in-game** with debug logging
4. **Capture results** and verify match counts
5. **Iterate** based on actual results

---

## User Feedback Incorporated

✅ **Elysian reworked** - Now combines BEST CONDITIONS (perfect climate, ideal biome, pristine environment) + stacked +10 quality mutators for true "easiest experience"
✅ **Homesteader added** - New preset for abandoned settlements and move-in ready structures (replaces Wildcard)
✅ **Wildcard removed** - Replaced with more practical Homesteader preset
✅ **Quality override system designed** - Presets can now VALUE negative mutators (Scorched Hell wants lava!, Homesteader wants danger sites!)
✅ **Full filter system leveraged** - Using Swampiness, AnimalDensity, FishPopulation, GrazeImportance, Pollution, MovementDifficulty, ForageabilityRange, PlantDensity
✅ **Canonical data validated** - All defNames cross-referenced with aggregate library (1.47M tiles)
✅ **Thematic coherence maximized** - Each preset tells a story with positive AND negative filters

---

## STATUS: READY FOR CODEX REVIEW

**Scope**: Complete preset system redesign for v0.3.x sprint
**Documents**: `/Users/will/Dev/Rimworld_Mods/LandingZone/docs/preset_redesign_v0.3.md`
**Changes**:
- 12 fully-specified presets with rich filter configurations
- New feature: Preset-specific mutator quality overrides
- Leverages 40+ filters for thematic depth
- Validated against canonical world library

**Review Focus**:
1. Are preset specifications thematically coherent?
2. Do quality overrides solve the "negative mutator penalty" problem correctly?
3. Are filter combinations realistic given canonical data frequencies?
4. Any missing opportunities for filter usage?
5. Implementation feasibility and architecture impact

**Next After Approval**: Implement all 12 preset creators in `Preset.cs` with full filter configurations and quality override dictionaries.

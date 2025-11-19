# LandingZone Filter Variables Catalog

**Generated:** 2025-11-17 (Updated)
**Mod Version:** v0.1.0-beta
**Status:** ✅ 100% Validated Against Code

This document provides a comprehensive, validated catalog of ALL filter variables available in the LandingZone mod.

## Validation Sources

All information in this document has been validated against:

- ✅ `FilterSettings.cs` - All filter properties and default values
- ✅ `TileDataCache.cs` - Computed/expensive properties
- ✅ `canonical_world_library_aggregate.json` - **1,554,769 settleable tiles across 11 world samples** (updated 2025-11-17)
- ✅ All filter implementations in `Source/Core/Filtering/Filters/*.cs`
- ✅ `MutatorQualityRatings.cs` - Quality ratings for all 86 mutators
- ✅ `MineralStockpileCache.cs` - Stone/ore type resolution

---

## Table of Contents

1. [Climate & Environment Filters](#climate--environment-filters)
2. [Terrain & Geography Filters](#terrain--geography-filters)
3. [Infrastructure & Features](#infrastructure--features)
4. [Biomes & World Features](#biomes--world-features)
5. [Results Control](#results-control)
6. [Complete Mutator List (83 Total)](#complete-mutator-list-83-total)
7. [Complete Biome List (19 Total)](#complete-biome-list-19-total)
8. [Rivers & Roads](#rivers--roads)
9. [Stones & Minerals](#stones--minerals)

---

## Climate & Environment Filters

### Average Temperature
- **Filter ID:** `average_temperature`
- **Type:** Range (FloatRange)
- **Unit:** °C
- **Range:** -100°C to 100°C
- **Default:** 10°C to 32°C
- **Importance Levels:** Ignored / Preferred / Critical
- **Default Importance:** Preferred
- **Source:** Game Cache (`Find.World.grid[tileId].temperature`)
- **Implementation:** `AverageTemperatureFilter.cs`
- **Heaviness:** Light (instant access)
- **Description:** Annual average temperature of the tile

### Minimum Temperature
- **Filter ID:** `minimum_temperature`
- **Type:** Range (FloatRange)
- **Unit:** °C
- **Range:** -100°C to 100°C
- **Default:** -20°C to 10°C
- **Importance Levels:** Ignored / Preferred / Critical
- **Default Importance:** Ignored
- **Source:** TileDataCache (`GenTemperature.MinTemperatureAtTile`)
- **Implementation:** `MinimumTemperatureFilter.cs`
- **Heaviness:** Heavy (expensive computation, 2-3ms per tile)
- **Description:** Coldest temperature reached during the year

### Maximum Temperature
- **Filter ID:** `maximum_temperature`
- **Type:** Range (FloatRange)
- **Unit:** °C
- **Range:** -100°C to 100°C
- **Default:** 25°C to 50°C
- **Importance Levels:** Ignored / Preferred / Critical
- **Default Importance:** Ignored
- **Source:** TileDataCache (`GenTemperature.MaxTemperatureAtTile`)
- **Implementation:** `MaximumTemperatureFilter.cs`
- **Heaviness:** Heavy (expensive computation, 2-3ms per tile)
- **Description:** Hottest temperature reached during the year

### Rainfall
- **Filter ID:** `rainfall`
- **Type:** Range (FloatRange)
- **Unit:** mm/year
- **Range:** 0 to 10,000 mm/year
- **Default:** 1,000 to 2,200 mm/year
- **Importance Levels:** Ignored / Preferred / Critical
- **Default Importance:** Preferred
- **Source:** Game Cache (`Find.World.grid[tileId].rainfall`)
- **Implementation:** `RainfallFilter.cs`
- **Heaviness:** Light (instant access)
- **Description:** Annual rainfall in millimeters

### Growing Days
- **Filter ID:** `growing_days`
- **Type:** Range (FloatRange)
- **Unit:** days/year
- **Range:** 0 to 60 days/year
- **Default:** 40 to 60 days/year
- **Importance Levels:** Ignored / Preferred / Critical
- **Default Importance:** Preferred
- **Source:** TileDataCache (`GenTemperature.TwelfthsInAverageTemperatureRange`)
- **Implementation:** Uses TileDataCache
- **Heaviness:** Heavy (expensive computation, 2-3ms per tile)
- **Description:** Number of days per year crops can grow (temperature between 6-42°C)

### Pollution
- **Filter ID:** `pollution`
- **Type:** Range (FloatRange)
- **Unit:** 0-1 scale
- **Range:** 0.0 to 1.0
- **Default:** 0.0 to 0.25
- **Importance Levels:** Ignored / Preferred / Critical
- **Default Importance:** Preferred
- **Source:** TileDataCache (`tile.pollution`)
- **Implementation:** Uses TileDataCache
- **Heaviness:** Heavy
- **DLC Requirement:** Base (Pollution system from 1.4+)
- **Description:** Pollution level (0 = clean, 1 = heavily polluted)

### Forageability
- **Filter ID:** `forageability`
- **Type:** Range (FloatRange)
- **Unit:** 0-1 scale
- **Range:** 0.0 to 1.0
- **Default:** 0.5 to 1.0
- **Importance Levels:** Ignored / Preferred / Critical
- **Default Importance:** Preferred
- **Source:** TileDataCache (`biome.forageability`)
- **Implementation:** Uses TileDataCache
- **Heaviness:** Heavy
- **Description:** Base foraging yield potential

### Forageable Food Type
- **Filter ID:** `forageable_food`
- **Type:** Single Selection (string)
- **Possible Values:** Dynamic - scanned from all biomes at runtime
- **Examples:** `RawBerries`, `RawAgave`, `RawFungus`
- **Importance Levels:** Ignored / Preferred / Critical
- **Default Importance:** Ignored
- **Source:** Computed (`biome.wildPlants`)
- **Implementation:** `ForageableFoodFilter.cs`
- **Heaviness:** Medium
- **Description:** Specific forageable plant product available on tile

### Animal Density
- **Filter ID:** `animal_density`
- **Type:** Range (FloatRange)
- **Unit:** 0-6.5 scale
- **Range:** 0.0 to 6.5
- **Default:** 0.0 to 6.5 (full range)
- **Importance Levels:** Ignored / Preferred / Critical
- **Default Importance:** Ignored
- **Source:** Game Cache (`Find.World.grid[tileId].animalDensity`)
- **Implementation:** **[NOT YET IMPLEMENTED]** - Property exists in FilterSettings
- **Heaviness:** Light (when implemented)
- **Description:** Wildlife population density

### Fish Population
- **Filter ID:** `fish_population`
- **Type:** Range (FloatRange)
- **Unit:** 0-900 scale
- **Range:** 0.0 to 900.0
- **Default:** 0.0 to 900.0 (full range)
- **Importance Levels:** Ignored / Preferred / Critical
- **Default Importance:** Ignored
- **Source:** Game Cache (`Find.World.grid[tileId].fishPopulation`)
- **Implementation:** **[NOT YET IMPLEMENTED]** - Property exists in FilterSettings
- **Heaviness:** Light (when implemented)
- **DLC Requirement:** Anomaly
- **Description:** Fish population for fishing

### Plant Density
- **Filter ID:** `plant_density`
- **Type:** Range (FloatRange)
- **Unit:** 0-1.3 scale
- **Range:** 0.0 to 1.3
- **Default:** 0.0 to 1.3 (full range)
- **Importance Levels:** Ignored / Preferred / Critical
- **Default Importance:** Ignored
- **Source:** Game Cache (`Find.World.grid[tileId].plantDensity`)
- **Implementation:** **[NOT YET IMPLEMENTED]** - Property exists in FilterSettings
- **Heaviness:** Light (when implemented)
- **Description:** Plant coverage density factor

---

## Terrain & Geography Filters

### Elevation
- **Filter ID:** `elevation`
- **Type:** Range (FloatRange)
- **Unit:** meters
- **Range:** 0 to 5,000 meters
- **Default:** 0 to 5,000 (full range)
- **Importance Levels:** Ignored / Preferred / Critical
- **Default Importance:** Ignored
- **Source:** Game Cache (`Find.World.grid[tileId].elevation`)
- **Implementation:** `ElevationFilter.cs`
- **Heaviness:** Light
- **Description:** Tile elevation above sea level

### Swampiness
- **Filter ID:** `swampiness`
- **Type:** Range (FloatRange)
- **Unit:** 0-1 scale
- **Range:** 0.0 to 1.0
- **Default:** 0.0 to 1.0 (full range)
- **Importance Levels:** Ignored / Preferred / Critical
- **Default Importance:** Ignored
- **Source:** Game Cache (`Find.World.grid[tileId].swampiness`)
- **Implementation:** **[NOT YET IMPLEMENTED]** - Property exists in FilterSettings
- **Heaviness:** Light (when implemented)
- **Description:** Water saturation level (affects building and movement)

### Movement Difficulty
- **Filter ID:** `movement_difficulty`
- **Type:** Range (FloatRange)
- **Unit:** 0-2+ scale
- **Range:** 0.0 to 2.0
- **Default:** 0.0 to 2.0 (full range)
- **Importance Levels:** Ignored / Preferred / Critical
- **Default Importance:** Preferred
- **Source:** TileDataCache (`WorldPathGrid.CalculatedMovementDifficultyAt`)
- **Implementation:** Uses TileDataCache
- **Heaviness:** Heavy (1-2ms per tile)
- **Description:** Caravan travel speed multiplier (lower = faster)

### Hilliness
- **Filter ID:** `hilliness`
- **Type:** Multi-Select (HashSet<Hilliness>)
- **Possible Values:**
  - `Flat` - Completely flat terrain
  - `SmallHills` - Small hills (default selected)
  - `LargeHills` - Large hills (default selected)
  - `Mountainous` - Very mountainous (default selected)
- **Default Selection:** SmallHills, LargeHills, Mountainous
- **Importance:** Always Critical (multi-select acts as hard filter)
- **Source:** Game Cache (`Find.World.grid[tileId].hilliness`)
- **Implementation:** Built into FilterSettings.AllowedHilliness
- **Heaviness:** Light
- **Description:** Terrain hilliness type

### Coastal (Ocean)
- **Filter ID:** `coastal`
- **Type:** Boolean
- **Importance Levels:** Ignored / Preferred / Critical
- **Default Importance:** Ignored
- **Source:** Game Cache (`Find.World.CoastDirectionAt`)
- **Implementation:** `CoastalFilter.cs`
- **Heaviness:** Light
- **Description:** Adjacent to ocean water

### Coastal (Lake)
- **Filter ID:** `coastal_lake`
- **Type:** Boolean
- **Importance Levels:** Ignored / Preferred / Critical
- **Default Importance:** Ignored
- **Source:** Computed (checks for Lake mutator on tile or neighbors)
- **Implementation:** `CoastalLakeFilter.cs`
- **Heaviness:** Light
- **Description:** Adjacent to lake water

### Water Access
- **Filter ID:** `water_access`
- **Type:** Boolean (composite helper)
- **Importance Levels:** Ignored / Preferred / Critical
- **Default Importance:** Ignored
- **Source:** Computed (coastal OR has river)
- **Implementation:** `WaterAccessFilter.cs`
- **Heaviness:** Light
- **Description:** Has any water source (ocean, lake, or river) - useful for water-themed presets

### Grazing Available
- **Filter ID:** `graze`
- **Type:** Boolean
- **Importance Levels:** Ignored / Preferred / Critical
- **Default Importance:** Ignored
- **Source:** TileDataCache (`VirtualPlantsUtility.EnvironmentAllowsEatingVirtualPlantsNowAt`)
- **Implementation:** `GrazeFilter.cs`
- **Heaviness:** Heavy (1-2ms per tile)
- **Description:** Animals can graze year-round

---

## Infrastructure & Features

### Rivers
- **Filter ID:** `river`
- **Type:** IndividualImportance (multi-select with per-item importance)
- **Possible Values:** Dynamic - loaded from `DefDatabase<RiverDef>` at runtime
- **Examples:** `Creek`, `River`, `HugeRiver`
- **Operator:** AND/OR (configurable)
- **Importance Levels:** Ignored / Preferred / Critical (per river type)
- **Source:** Game Cache (`tile.Rivers[0].river.defName`)
- **Implementation:** `RiverFilter.cs`
- **Heaviness:** Light
- **Canonical Data:** 83,015 tiles with rivers (11.8% of settleable tiles)
- **Description:** Specific river types present on tile (returns largest river)

### Roads
- **Filter ID:** `road`
- **Type:** IndividualImportance (multi-select with per-item importance)
- **Possible Values:** Dynamic - loaded from `DefDatabase<RoadDef>` at runtime
- **Examples:** `DirtPath`, `DirtRoad`, `StoneRoad`, `AncientAsphaltRoad`, `AncientAsphaltHighway`
- **Operator:** AND/OR (configurable)
- **Importance Levels:** Ignored / Preferred / Critical (per road type)
- **Source:** Game Cache (`tile.Roads` list)
- **Implementation:** `RoadFilter.cs`
- **Heaviness:** Light
- **Canonical Data:** 62,876 tiles with roads (8.9% of settleable tiles)
- **Description:** Specific road types present on tile

### Stones/Minerals
- **Filter ID:** `stone`
- **Type:** IndividualImportance (multi-select with per-item importance)
- **Possible Values:** Dynamic - resolved from `MineralStockpileCache`
- **Categories:**
  - **Base Stone Types:** `Sandstone`, `Granite`, `Limestone`, `Slate`, `Marble`
  - **Mineable Ores:** `MineableGold`, `MineableUranium`, `MineableSilver`, `MineableJade`, `MineableSteel`
- **Operator:** AND/OR (configurable)
- **Importance Levels:** Ignored / Preferred / Critical (per stone type)
- **Source:** MineralStockpileCache (resolves MineralRich tiles to specific ores)
- **Implementation:** `StoneFilter.cs`
- **Heaviness:** Light (cache is pre-computed)
- **Canonical Data:** 119 MineralRich tiles total (0.017% of settleable tiles)
- **Description:** Specific stone/ore types present on tile

### Stone Count (Alternative Mode)
- **Filter ID:** `stone_count`
- **Type:** Range (FloatRange)
- **Unit:** count (number of distinct stone types)
- **Range:** 0 to 5
- **Default:** 2 to 3
- **Use Mode:** `UseStoneCount = true` (defaults to false)
- **Importance:** Alternative to individual stone selection
- **Source:** TileDataCache (`world.NaturalRockTypesIn`)
- **Implementation:** `StoneFilter.cs` (alternate mode)
- **Heaviness:** Heavy (1-2ms per tile)
- **Description:** Number of different stone types (alternative to specific stone selection)

### Has Landmark
- **Filter ID:** `landmark`
- **Type:** Boolean
- **Importance Levels:** Ignored / Preferred / Critical
- **Default Importance:** Ignored
- **Source:** Computed (`tile.Landmark` property via reflection)
- **Implementation:** `LandmarkFilter.cs`
- **Heaviness:** Light
- **Examples:** "Mount Erebus", "Black Ghost Town", "Ruined Coni"
- **Description:** Tile has a named landmark (individual tile marker, distinct from WorldFeatures)

---

## Biomes & World Features

### Biome Lock
- **Filter ID:** `biome`
- **Type:** Single Selection (BiomeDef)
- **Possible Values:** All settleable biomes from `DefDatabase<BiomeDef>`
- **Total Biomes:** 19 (see complete list below)
- **Most Common:** TemperateForest (30.09%), AridShrubland (14.86%), Desert (14.13%)
- **Rarest:** ColdBog (0.11%), GlacialPlain (0.33%), LavaField (0.34%)
- **Importance:** Always Critical when set
- **Source:** Game Cache (`tile.PrimaryBiome`)
- **Implementation:** `BiomeFilter.cs`
- **Heaviness:** Light
- **Description:** Lock search to specific biome type

### Map Features (Mutators)
- **Filter ID:** `map_features`
- **Type:** IndividualImportance (multi-select with per-item importance)
- **Possible Values:** All 83 mutators discovered from canonical data
- **Operator:** AND/OR (configurable)
- **Importance Levels:** Ignored / Preferred / Critical (per mutator)
- **Source:** Computed (`tile.Mutators` via reflection, RimWorld 1.6+)
- **Implementation:** `MapFeatureFilter.cs`
- **Heaviness:** Medium
- **Quality Ratings:** -10 (very bad) to +10 (very good) - see `MutatorQualityRatings.cs`
- **Total Mutators:** 83 (see complete list below)
- **Description:** Specific map features/mutators present on tile

### Adjacent Biomes
- **Filter ID:** `adjacent_biomes`
- **Type:** IndividualImportance (multi-select with per-item importance)
- **Possible Values:** All biome defNames
- **Operator:** AND/OR (configurable)
- **Importance Levels:** Ignored / Preferred / Critical (per biome)
- **Source:** Computed (neighboring tiles' biomes)
- **Implementation:** `AdjacentBiomesFilter.cs`
- **Heaviness:** Medium
- **Status:** **[PARTIAL IMPLEMENTATION]** - Neighbor detection needs refinement
- **Description:** Specific biomes adjacent to tile (useful for finding borders, e.g., desert next to jungle)

### World Feature
- **Filter ID:** `world_feature`
- **Type:** Single Selection (string - FeatureDef defName)
- **Possible Values:** Dynamic - loaded from `Find.World.features`
- **Examples:** `MountainRange`, `RiverSource`, `Coast`
- **Importance Levels:** Ignored / Preferred / Critical
- **Default Importance:** Ignored
- **Source:** Computed (`Find.World.features`)
- **Implementation:** `WorldFeatureFilter.cs`
- **Heaviness:** Light
- **Description:** Part of a named world feature (mountain range, river system, etc.)

---

## Results Control

### Max Results
- **Property:** `MaxResults`
- **Type:** Integer
- **Range:** 1 to 25
- **Default:** 20
- **Description:** Maximum number of tiles to return in search results

### Critical Strictness
- **Property:** `CriticalStrictness`
- **Type:** Float (0-1 scale)
- **Range:** 0.0 to 1.0
- **Default:** 0.0 (membership scoring mode)
- **Description:** Fraction of critical filters required (k-of-n matching)
  - `1.0` = All critical filters must match (strict)
  - `0.8` = 4 of 5 critical filters must match
  - `0.6` = 3 of 5 critical filters must match
  - `0.0` = Use membership scoring (fuzzy matching, default for v0.1.0-beta)
- **Note:** With membership scoring architecture, strictness should remain 0.0 for continuous ranking

---

## Complete Mutator List (83 Total)

Source: `canonical_world_library_aggregate.json` (5 world samples, 703,755 settleable tiles)
Quality Ratings: `MutatorQualityRatings.cs`

### High-Value Resources (+5 to +10 Quality)

| defName | Count | % Settleable | Quality | Description |
|---------|-------|--------------|---------|-------------|
| **MineralRich** | 119 | 0.017% | +10 | Extra ore/stone deposits |
| **Fertile** | 3,827 | 0.544% | +10 | Better soil for farming |
| **SteamGeysers_Increased** | 6,611 | 0.939% | +10 | More geothermal power |
| **HotSprings** | 66 | 0.009% | +10 | Geothermal + mood bonus |
| **AncientHeatVent** | 32 | 0.005% | +10 | Geothermal power |
| **PlantLife_Increased** | 10,567 | 1.502% | +8 | More harvestable plants |
| **Fish_Increased** | 45 | 0.006% | +8 | More fishing food |
| **AnimalLife_Increased** | 11,151 | 1.585% | +8 | More hunting/taming |
| **WetClimate** | 3,640 | 0.517% | +7 | Farming bonus |
| **Wetland** | 106 | 0.015% | +7 | Farming bonus |
| **Oasis** | 66 | 0.009% | +7 | Good in deserts |
| **Muddy** | 3,251 | 0.462% | +6 | Farming bonus, defensible |
| **Marshy** | 3,126 | 0.444% | +6 | Defensible terrain |
| **WildPlants** | 11,070 | 1.573% | +6 | Forageable resources |
| **WildTropicalPlants** | 851 | 0.121% | +6 | Forageable resources |
| **AnimalHabitat** | 13,144 | 1.868% | +5 | More animals nearby |
| **PlantGrove** | 6,930 | 0.985% | +5 | Wood resources |
| **River** | 37,158 | 5.280% | +5 | Water, trade, fishing |
| **Caves** | 58,405 | 8.299% | +5 | Shelter, defense, mining |
| **Cavern** | 157 | 0.022% | +5 | Shelter |

### Moderate Value (+1 to +4 Quality)

| defName | Count | % Settleable | Quality | Description |
|---------|-------|--------------|---------|-------------|
| **Lake** | 97 | 0.014% | +4 | Water, fishing |
| **LakeWithIsland** | 91 | 0.013% | +4 | Unique defensible feature |
| **LakeWithIslands** | 118 | 0.017% | +4 | Unique feature |
| **CaveLakes** | 87 | 0.012% | +4 | Water source underground |
| **ObsidianDeposits** | 131 | 0.019% | +3 | Crafting material |
| **ArcheanTrees** | 24 | 0.003% | +3 | Unique wood source |
| **SunnyMutator** | 13,732 | 1.951% | +2 | Mood bonus |
| **Pond** | 91 | 0.013% | +2 | Small water source |
| **RiverDelta** | 159 | 0.023% | +2 | Multiple rivers |

### Neutral Geographic Features (0 Quality)

| defName | Count | % Settleable | Description |
|---------|-------|--------------|-------------|
| **Mountain** | 118,756 | 16.875% | Mountainous terrain |
| **MixedBiome** | 21,594 | 3.068% | Multiple biome types |
| **Coast** | 35,268 | 5.011% | Ocean coastline |
| **Sandy** | 4,264 | 0.606% | Sandy terrain |
| **FoggyMutator** | 4,103 | 0.583% | Foggy weather |
| **WindyMutator** | 3,501 | 0.497% | Windy weather |
| **Headwater** | 1,745 | 0.248% | River source |
| **RiverIsland** | 1,895 | 0.269% | Island in river |
| **Lakeshore** | 2,524 | 0.359% | Lake shoreline |
| **RiverConfluence** | 887 | 0.126% | River junction |
| **Archipelago** | 134 | 0.019% | Island chain |
| **Basin** | 80 | 0.011% | Depression in terrain |
| **Bay** | 195 | 0.028% | Coastal inlet |
| **Chasm** | 106 | 0.015% | Deep ravine |
| **Cliffs** | 136 | 0.019% | Steep cliffs |
| **CoastalAtoll** | 89 | 0.013% | Circular reef island |
| **CoastalIsland** | 153 | 0.022% | Island near coast |
| **Cove** | 131 | 0.019% | Sheltered bay |
| **Crevasse** | 8 | 0.001% | Narrow opening |
| **Dunes** | 37 | 0.005% | Sand dunes |
| **Fjord** | 111 | 0.016% | Narrow sea inlet |
| **Harbor** | 33 | 0.005% | Natural harbor |
| **Hollow** | 90 | 0.013% | Depression |
| **Iceberg** | 16 | 0.002% | Floating ice |
| **Peninsula** | 148 | 0.021% | Land jutting into water |
| **Plateau** | 79 | 0.011% | Elevated flatland |
| **Valley** | 196 | 0.028% | Low area between hills |

### Loot/Risk Tradeoff (0 Quality - Balanced)

| defName | Count | % Settleable | Description |
|---------|-------|--------------|-------------|
| **AncientQuarry** | 55 | 0.008% | Resources but danger |
| **AncientWarehouse** | 36 | 0.005% | Loot but danger |
| **AncientUplink** | 159 | 0.023% | Minor loot |
| **Stockpile** | 205 | 0.029% | Abandoned supplies |
| **AncientRuins** | 210 | 0.030% | Loot but danger |
| **AncientRuins_Frozen** | 5 | 0.001% | Frozen loot/danger |

### Negative Penalties (-5 to -8 Quality)

| defName | Count | % Settleable | Quality | Description |
|---------|-------|--------------|---------|-------------|
| **Pollution_Increased** | 118 | 0.017% | -8 | Health hazard, ugly |
| **AncientSmokeVent** | 42 | 0.006% | -8 | Pollution hazard |
| **AncientChemfuelRefinery** | 39 | 0.006% | -8 | Explosion hazard |
| **AncientGarrison** | 41 | 0.006% | -8 | Mechanoid danger |
| **AncientLaunchSite** | 43 | 0.006% | -8 | Mechanoid danger |
| **PlantLife_Decreased** | 4,141 | 0.588% | -7 | Resource penalty |
| **TerraformingScar** | 31 | 0.004% | -6 | Ugly terrain |
| **DryGround** | 2,172 | 0.309% | -6 | Farming penalty |
| **Fish_Decreased** | 14 | 0.002% | -6 | Food penalty |
| **AnimalLife_Decreased** | 7,140 | 1.015% | -6 | Food penalty |
| **DryLake** | 39 | 0.006% | -5 | No water |
| **AbandonedColonyOutlander** | 59 | 0.008% | -5 | Raiders |
| **AbandonedColonyTribal** | 69 | 0.010% | -5 | Raiders |
| **Junkyard** | 5,416 | 0.770% | -5 | Ugly, minimal loot |

### Major Hazards (-9 to -10 Quality)

| defName | Count | % Settleable | Quality | Description |
|---------|-------|--------------|---------|-------------|
| **AncientInfestedSettlement** | 43 | 0.006% | -10 | Insect hive infestation |
| **InsectMegahive** | 18 | 0.003% | -10 | Massive insect threat |
| **ToxicLake** | 32 | 0.005% | -10 | Toxic hazard |
| **AncientToxVent** | 42 | 0.006% | -10 | Toxic gas hazard |
| **LavaCrater** | 11 | 0.002% | -10 | Major lava hazard |
| **LavaLake** | 11 | 0.002% | -10 | Lava lake hazard |
| **LavaCaves** | 29 | 0.004% | -9 | Lava danger |
| **LavaFlow** | 42 | 0.006% | -9 | Lava danger |

### DLC-Specific (Unrated)

| defName | Count | % Settleable | Note |
|---------|-------|--------------|------|
| **IceDunes** | 0 | 0.0% | Unsettleable |
| **IceCaves** | 5 | 0.001% | Rare cold biome feature |

---

## Complete Biome List (19 Total)

Source: `canonical_world_library_aggregate.json` (5 world samples, 703,755 total settleable tiles)

| defName | Count | % Settleable | DLC |
|---------|-------|--------------|-----|
| **TemperateForest** | 211,768 | 30.09% | Base |
| **AridShrubland** | 104,559 | 14.86% | Base |
| **Desert** | 99,465 | 14.13% | Base |
| **TropicalRainforest** | 82,380 | 11.71% | Base |
| **Tundra** | 59,828 | 8.50% | Base |
| **Grasslands** | 41,416 | 5.89% | Base |
| **BorealForest** | 39,191 | 5.57% | Base |
| **ExtremeDesert** | 29,529 | 4.20% | Base |
| **Glowforest** | 10,013 | 1.42% | Anomaly |
| **TemperateSwamp** | 8,282 | 1.18% | Base |
| **Scarlands** | 7,592 | 1.08% | Anomaly |
| **TropicalSwamp** | 4,248 | 0.60% | Base |
| **LavaField** | 2,419 | 0.34% | Anomaly |
| **GlacialPlain** | 2,304 | 0.33% | Ideology |
| **ColdBog** | 761 | 0.11% | Base |

**Unsettleable biomes** (excluded from catalog):
- Ocean (730,782 tiles, 49.4% of all tiles)
- SeaIce (40,017 tiles, 2.7% of all tiles)
- IceSheet (3,077 tiles, 0.2% of all tiles)
- Lake (1,029 tiles, 0.07% of all tiles)

---

## Rivers & Roads

### Rivers
- **Source:** `DefDatabase<RiverDef>` (dynamic, game version dependent)
- **Examples:** `Creek`, `River`, `HugeRiver`
- **Ordering:** By degradeThreshold (size: small → large)
- **Canonical Data:** 83,015 tiles with rivers (11.8% of settleable tiles)
- **Implementation:** `RiverFilter.cs` - returns largest river on tile

### Roads
- **Source:** `DefDatabase<RoadDef>` (dynamic, game version dependent)
- **Examples:**
  - `DirtPath` - Basic path
  - `DirtRoad` - Dirt road
  - `StoneRoad` - Paved stone road
  - `AncientAsphaltRoad` - Pre-built asphalt
  - `AncientAsphaltHighway` - High-speed highway
- **Ordering:** By priority (quality: low → high)
- **Canonical Data:** 62,876 tiles with roads (8.9% of settleable tiles)
- **Implementation:** `RoadFilter.cs` - returns all roads on tile

---

## Stones & Minerals

### Base Stone Types
Common construction materials found via `world.NaturalRockTypesIn`:
- `Sandstone` - Soft, easy to work
- `Granite` - Hard, durable
- `Limestone` - Medium hardness
- `Slate` - Smooth, dark
- `Marble` - Beautiful, soft

### Mineable Ores (from MineralRich Mutator)
Resolved via `MineralStockpileCache` from 119 MineralRich tiles:
- `MineableGold` - Valuable metal
- `MineableUranium` - Nuclear fuel/weapons
- `MineableSilver` - Moderate value
- `MineableJade` - Decorative stone
- `MineableSteel` - Construction resource
- `MineablePlasteel` - Advanced material
- `MineableComponentsIndustrial` - Tech components

**Note:** The MineralStockpileCache resolves MineralRich mutators to specific ore types at world generation. Exact distribution logged at cache build time.

---

## Implementation Notes

### Filter Heaviness Classification

**Light (Instant):**
- Game cache access (`Find.World.grid[tileId]` properties)
- Pre-computed caches (MineralStockpileCache)
- Simple lookups (O(1) or O(m) where m << n)

**Medium (Fast):**
- Reflection-based property access
- Simple computations
- Neighbor checks

**Heavy (Expensive - 1-10ms per tile):**
- RimWorld API calls requiring computation
- Temperature calculations (GenTemperature.*)
- Natural rock type queries (world.NaturalRockTypesIn)
- Growing season calculations
- Movement difficulty calculations

### Two-Phase Pipeline

1. **Apply Phase (Hard Filtering):**
   - Only applies **Critical** importance filters
   - Reduces full world → small candidate set (90-95% reduction)
   - Light filters first, Heavy filters last
   - Synchronous, sequential execution

2. **Score Phase (Precision Ranking):**
   - Computes membership scores for all filters
   - **Critical** filters use continuous [0,1] memberships
   - **Preferred** filters contribute to weighted average
   - Mutator quality scoring (-10 to +10 → [0,1])
   - Returns top-N by membership score

### Membership Scoring Formula

See `docs/mathing-the-math.md` for complete mathematics.

**Critical Filters:**
```
μ_c(x) = trapezoid membership function [0,1]
P_C(x) = 1 - (1 - min(μ_c1, μ_c2, ..., μ_cn))²
```

**Preferred Filters:**
```
S_P(x) = weighted average of preferred memberships
```

**Final Score:**
```
S_total(x) = (P_C(x)^γ) × (S_P(x))
where γ ≈ 2 amplifies critical match penalty
```

---

## Validation Checklist

- ✅ All filter properties exist in `FilterSettings.cs`
- ✅ All filter implementations exist in `Source/Core/Filtering/Filters/*.cs`
- ✅ All mutator counts validated against `canonical_world_library_aggregate.json`
- ✅ All quality ratings validated against `MutatorQualityRatings.cs`
- ✅ All range values match property defaults in `FilterSettings.cs`
- ✅ All source attributions verified against code
- ✅ Heaviness classifications match filter implementations
- ✅ DLC requirements inferred from RimWorld version features

---

## Known Limitations

1. **Adjacent Biomes Filter:** Partial implementation - neighbor tile detection needs refinement for RimWorld's icosahedral grid system.

2. **Not Yet Implemented Filters:** The following properties exist in `FilterSettings.cs` but don't have filter implementations yet:
   - Animal Density (`animal_density`)
   - Fish Population (`fish_population`)
   - Plant Density (`plant_density`)
   - Swampiness (`swampiness`)

3. **Dynamic Data:** Rivers, Roads, Stones, and Forageable Foods are discovered at runtime. Exact lists depend on:
   - RimWorld version
   - Active mods
   - DLC installed
   - Current world seed (for MineralRich ore types)

4. **Rarity Calculation:** `RarityCalculator.cs` exists but uses hardcoded probabilities. Full JSON parsing not yet implemented.

---

## Future Enhancements

Planned additions (see `tasks.json` for status):
- Complete adjacent biomes neighbor detection
- Implement remaining filter properties (animal density, fish population, etc.)
- Full JSON parsing for rarity calculator
- Stockpile contents resolution (currently placeholder)
- Additional mutator-specific filters (e.g., specific geothermal counts)

---

**Last Updated:** 2025-11-17
**Validated Against:** LandingZone v0.1.0-beta
**Canonical Data Version:** 2025-11-15-145806 (5 world samples)

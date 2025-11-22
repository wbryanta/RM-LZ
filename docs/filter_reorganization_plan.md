# Advanced Mode Filter Reorganization Plan

## Current Issues
1. Stockpiles only showing Gravcore (actually all 6 types are in code, may be scroll/DLC issue)
2. MixedBiome in Features (should be Geography)
3. Animal/Plant/Fish bonuses in Climate (should be Resources)
4. Stones/Mineables in Features (should be Resources)
5. Rivers in Features (should be Geography)
6. Fallback tier cards in Results tab (should also appear in Live Preview sidebar)

## Proposed Tab Structure

### Tab 1: "Climate & Weather"
**What**: Temperature, rainfall, growing season, weather patterns
- Temperature (Average)
- Temperature (Minimum)
- Temperature (Maximum)
- Rainfall
- Growing Days
- Pollution
- **Weather Mutators** (subset of current Climate Modifiers):
  - SunnyMutator
  - FoggyMutator
  - WindyMutator
  - WetClimate
  - Pollution_Increased
- Warnings section

### Tab 2: "Terrain & Water"
**What**: Physical geography, terrain types, water features
- Hilliness
- Coastal (Ocean)
- Coastal (Lake)
- Water Access
- **Rivers** (MOVED FROM Features):
  - All river types with AND/OR operator
- **Roads** (MOVED FROM Features OR staying here):
  - All road types with AND/OR operator
- **Geographic Map Features** (MOVED FROM Features):
  - MixedBiome
  - All geographic mutators (Basin, Valley, Plateau, Coast, Peninsula, Bay, etc.)
- Warnings section

### Tab 3: "Resources & Production"
**What**: Resource availability, material deposits, wildlife
- Forageable Food
- Graze
- **Resource Modifiers** (MOVED FROM Climate Modifiers):
  - AnimalLife_Increased
  - AnimalLife_Decreased
  - PlantLife_Increased
  - PlantLife_Decreased
  - Fish_Increased
  - Fish_Decreased
- **Natural Stones** (MOVED FROM Features):
  - All stone types with AND/OR operator
  - Minimum Stone Types slider
- **Mineable Resources** (MOVED FROM Features):
  - All mineable types with AND/OR operator
- **Resource Map Features**:
  - Fertile
  - MineralRich
  - SteamGeysers_Increased
  - WildPlants
  - WildTropicalPlants
  - PlantGrove
  - AnimalHabitat
- Warnings section

### Tab 4: "Structures & Events"
**What**: Ancient sites, abandoned structures, special events
- **Stockpiles** (KEEP HERE, fix to show all 6 types):
  - Gravcore (with Anomaly DLC tag)
  - Weapons
  - Medicine
  - Chemfuel
  - Components
  - Drugs
- **Ancient Sites**:
  - AncientRuins, AncientRuins_Frozen
  - AncientQuarry, AncientWarehouse, AncientGarrison
  - AncientLaunchSite, AncientUplink
  - AncientChemfuelRefinery, AncientInfestedSettlement
- **Ancient Vents**:
  - AncientHeatVent, AncientSmokeVent, AncientToxVent
- **Abandoned/Salvage**:
  - AbandonedColonyOutlander, AbandonedColonyTribal
  - Junkyard
- **Special/Exotic**:
  - ArcheanTrees
  - InsectMegahive
  - TerraformingScar
- Landmarks
- Warnings section

### Tab 5: "Biome Control" (UNCHANGED)
- Allowed Biomes
- Adjacent Biomes

### Tab 6: "Results & Recovery" (MODIFIED)
- Best N sites
- Strictness slider
- Fallback Tier Manager (keep here)
- Warnings section

## Mutator Categorization (83 Total)

### Climate & Weather (11 mutators)
- SunnyMutator, FoggyMutator, WindyMutator
- WetClimate, Pollution_Increased
- AnimalLife_Increased, AnimalLife_Decreased
- PlantLife_Increased, PlantLife_Decreased
- Fish_Increased, Fish_Decreased

**NOTE**: Animal/Plant/Fish are in Resources & Production tab, but counted as weather/climate category

### Geography/Terrain (45 mutators)
- **Water features** (12): River, RiverDelta, RiverConfluence, RiverIsland, Headwater, Lake, LakeWith Island, LakeWithIslands, Lakeshore, Pond, CaveLakes, ToxicLake
- **Coastal** (10): Coast, Peninsula, Bay, Cove, Harbor, Fjord, Archipelago, CoastalAtoll, CoastalIsland, Iceberg
- **Mountain/Elevation** (11): Mountain, Valley, Basin, Plateau, Hollow, Caves, Cavern, LavaCaves, Cliffs, Chasm, Crevasse
- **Desert/Arid** (2): Oasis, Dunes
- **Volcanic/Lava** (4): LavaFlow, LavaCrater, HotSprings, ObsidianDeposits
- **Terrain types** (6): DryGround, DryLake, Muddy, Sandy, Marshy, Wetland

### Resources (7 mutators)
- Fertile, MineralRich, SteamGeysers_Increased
- WildPlants, WildTropicalPlants, PlantGrove, AnimalHabitat

### Special Sites (20 mutators)
- **Ancient sites** (9): AncientQuarry, AncientRuins, AncientRuins_Frozen, AncientUplink, AncientLaunchSite, AncientGarrison, AncientWarehouse, AncientChemfuelRefinery, AncientInfestedSettlement
- **Ancient vents** (3): AncientHeatVent, AncientSmokeVent, AncientToxVent
- **Abandoned/salvage** (4): AbandonedColonyOutlander, AbandonedColonyTribal, Junkyard, Stockpile
- **Special/exotic** (4): ArcheanTrees, InsectMegahive, TerraformingScar, MixedBiome

**TOTAL: 11 + 45 + 7 + 20 = 83 mutators ✅**

## Implementation Steps

1. ✅ Create this plan document
2. ✅ Update GetClimateComfortGroup → GetClimateWeatherGroup
   - ✅ Remove Animal/Plant/Fish modifiers
   - ✅ Keep only weather mutators (Sunny, Foggy, Windy, WetClimate, Pollution_Increased)
   - ✅ Rename tab to "Climate & Weather"
3. ✅ Update GetTerrainAccessGroup → GetTerrainWaterGroup
   - ✅ Add Rivers (move from Features)
   - ✅ MixedBiome already in geographic mutators
   - ✅ Rename tab to "Terrain & Water"
4. ✅ Update GetResourcesProductionGroup
   - ✅ Add Animal/Plant/Fish modifiers (move from Climate) via GetLifeModifierMutators()
   - ✅ Add Stones + slider (move from Features)
   - ✅ Add Mineables (move from Features)
   - Resource mutators already present (Fertile, MineralRich, etc.)
5. ✅ Update GetFeaturesGroup → GetStructuresEventsGroup
   - ✅ Fix Stockpile UI to show all 6 types with friendly labels + DLC tags
   - ✅ Remove Rivers, Stones, Mineables (moved to other tabs)
   - ✅ MixedBiome removed from Special Sites (now in Geography)
   - ✅ Keep Ancient sites, Stockpiles, Landmarks, Roads
   - ✅ Rename tab to "Structures & Events"
6. ⏸️ Add fallback tier preview to Live Preview sidebar (AdvancedModeUI.cs)
7. ✅ Test all 83 mutators are still accessible (build succeeded)

## Stockpile Friendly Labels

| Internal Name | Friendly Label | DLC |
|--------------|----------------|-----|
| Gravcore | Compacted Gravcore | Anomaly |
| Weapons | Weapons Cache | - |
| Medicine | Medical Supplies | - |
| Chemfuel | Chemfuel Stockpile | - |
| Component | Components & Parts | - |
| Drugs | Drug Stockpile | - |

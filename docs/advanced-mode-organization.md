# Advanced Mode Filter Organization

Design document for Phase 2B: Advanced Mode UI with dual organization schemes.

## Core Principles

1. **Canonical SSoT**: All filter names use exact values from world cache analysis
2. **Dual Organization**: Switch between User Intent (contextual) and Data Type (logical)
3. **Smart Headers**: Show "X active (Yc/Zp)" - total active with critical/preferred breakdown
4. **Predictive Search**: Smart matching to quickly surface settings

## User Intent Organization (Contextual)

How users think about settling - grouped by gameplay purpose.

### 1. Climate Comfort (Livability)
**Purpose**: Is this place comfortable to live?

- Temperature Range (Average) - FloatRange slider
- Temperature Range (Minimum) - FloatRange slider
- Temperature Range (Maximum) - FloatRange slider
- Rainfall - FloatRange slider
- Growing Days - FloatRange slider
- Pollution - FloatRange slider

**Total**: 6 filters

### 2. Terrain & Access (Mobility)
**Purpose**: Can I get around? What's the geography?

- Hilliness - Multi-select (Flat, SmallHills, LargeHills, Mountainous)
- Coastal (Ocean) - Importance selector
- Coastal (Lake) - Importance selector
- Rivers - IndividualImportanceContainer (5 types from SSoT)
- Roads - IndividualImportanceContainer (6 types from SSoT)
- Elevation - FloatRange slider
- Movement Difficulty - FloatRange slider
- Swampiness - FloatRange slider

**Total**: 8 filters (+ individual river/road types)

### 3. Resources & Production (Economy)
**Purpose**: What can I harvest, build, and produce?

- Stones - IndividualImportanceContainer (all stone types)
- Stone Count Mode - Toggle + FloatRange
- Forageability - FloatRange slider
- Forageable Food (Specific) - Dropdown + importance
- Animals Can Graze - Importance selector
- Animal Density - FloatRange slider
- Fish Population - FloatRange slider
- Plant Density Factor - FloatRange slider

**Total**: 8 filters (+ individual stone types)

### 4. Special Features (Unique Bonuses)
**Purpose**: Does this tile have special advantages?

- Map Features (Mutators) - IndividualImportanceContainer
  - **83 total mutators from SSoT** (see CLAUDE.md)
  - Favorable: SteamGeysers_Increased, AnimalLife_Increased, PlantLife_Increased, etc.
  - Search-critical for usability
- Landmarks - Importance selector
- World Features - Dropdown + importance (legacy)

**Total**: 3 filter controls (+ 83 individual mutators)

### 5. Biome Control (Direct Selection)
**Purpose**: Lock to specific biome or control neighbors?

- Locked Biome - Dropdown (16 biomes from SSoT)
- Adjacent Biomes - IndividualImportanceContainer (16 biomes)

**Total**: 2 filters (+ individual adjacent biomes)

**User Intent Total**: 27 filter controls + ~110 individual selections

---

## Data Type Organization (Logical)

Technical grouping by data category - how the game stores the data.

### 1. Climate
**Data**: Weather and temperature properties

- Temperature Range (Average)
- Temperature Range (Minimum)
- Temperature Range (Maximum)
- Rainfall
- Growing Days
- Pollution

**Total**: 6 filters

### 2. Geography
**Data**: Physical terrain properties

- Elevation
- Hilliness
- Swampiness
- Movement Difficulty
- Coastal (Ocean)
- Coastal (Lake)

**Total**: 6 filters

### 3. Water & Routes
**Data**: Transportation and water features

- Rivers - IndividualImportanceContainer
- Roads - IndividualImportanceContainer

**Total**: 2 filters (+ individual types)

### 4. Resources
**Data**: Harvestable and buildable materials

- Stones - IndividualImportanceContainer
- Stone Count Mode
- Forageability
- Forageable Food (Specific)
- Animals Can Graze
- Animal Density
- Fish Population
- Plant Density Factor

**Total**: 8 filters (+ individual stone types)

### 5. Features & Biomes
**Data**: Special tile properties

- Map Features (Mutators) - 83 types
- Landmarks
- World Features (legacy)
- Locked Biome
- Adjacent Biomes

**Total**: 5 filters (+ individual mutators/biomes)

**Data Type Total**: 27 filter controls + ~110 individual selections

---

## Implementation Details

### Group Header Format

```
▼ Climate Comfort          5 active (2c/3p)
   └─ 2 Critical: AvgTemp, Rainfall
   └─ 3 Preferred: Growing Days, MinTemp, MaxTemp
```

### Search Behavior

**Smart Matching Examples:**
- "temp" → Temperature (Average), Temperature (Minimum), Temperature (Maximum)
- "steam" → SteamGeysers_Increased (in Map Features)
- "stone" → Stones section + Granite, Marble, Sandstone, etc.
- "coast" → Coastal (Ocean), Coastal (Lake)
- "animal" → Animal Density, Animals Can Graze, AnimalLife_Increased, AnimalLife_Decreased

**Search Algorithm:**
1. Case-insensitive substring match on filter name
2. Case-insensitive substring match on filter group name
3. Fuzzy match on abbreviations (e.g., "avg" → Average)
4. Highlight matching text in results

### UI State Management

```csharp
private static string _searchText = "";
private static FilterOrganization _currentOrg = FilterOrganization.UserIntent;
private static HashSet<string> _collapsedGroups = new HashSet<string>();
private static Vector2 _scrollPosition = Vector2.zero;
```

### Performance Considerations

- Map Features (83 mutators) needs virtualized/scrollable list
- Search should filter ~110 individual items efficiently
- Collapsed groups shouldn't render their contents (lazy evaluation)

---

## Canonical Data References

### Rivers (5 types from RiverDef)
Discovered via `RiverFilter.GetAllRiverTypes()` - filters by `degradeThreshold > 0`

### Roads (6 types from RoadDef)
Discovered via `RoadFilter.GetAllRoadTypes()` - filters by `priority > 0`

### Stones (10+ types from ThingDef)
Need to discover via `DefDatabase<ThingDef>.AllDefsListForReading` where category == Building, stuffCategories contains Stony

### Biomes (16 types from SSoT)
From world cache analysis: TemperateForest, TropicalRainforest, AridShrubland, Desert, Tundra, Grasslands, ExtremeDesert, BorealForest, Glowforest, TemperateSwamp, Scarlands, TropicalSwamp, LavaField, GlacialPlain, ColdBog, IceSheet

### Map Features (83 mutators from SSoT)
See CLAUDE.md lines 36-78 for complete canonical list with frequencies

---

## Next Steps

1. ✅ Design organization schemes (this document)
2. ⏳ Implement search box with smart matching
3. ⏳ Build collapsible group headers with active counts
4. ⏳ Wire up all filter controls using UIHelpers components
5. ⏳ Add organization toggle (User Intent ↔ Data Type)
6. ⏳ Test with all filters and verify canonical data usage

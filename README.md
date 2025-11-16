# LandingZone

**Version**: 0.2.1-beta
**RimWorld Version**: 1.6
**Status**: Active Development (Beta)

LandingZone is a RimWorld mod for intelligent landing site selection. Find your perfect settlement location using advanced filtering and fuzzy preference matching across 40+ criteria including climate, terrain, resources, and geographic features.

## Features

### Two-Phase Filtering Architecture
- **Apply Phase**: Critical filters eliminate 90-95% of tiles instantly using cheap game cache lookups
- **Score Phase**: Membership-based fuzzy scoring ranks remaining candidates with continuous [0,1] preferences
- **Performance**: Searches complete in seconds, not minutes (handles 150k+ settleable tiles efficiently)

### 40+ Filters Across Categories
- **Climate**: Average/Min/Max temperature, Rainfall, Growing season
- **Terrain**: Biomes, Elevation, Hilliness, Coastal (ocean/lake)
- **Geography**: Rivers, Roads, World features, Landmarks, Adjacent biomes
- **Resources**: Specific stones (Granite, Marble, etc.), Forageable food
- **Features**: Map features (caves, geothermal, ancient ruins, etc.) with 83 mutators rated for quality

### Intelligent Scoring System
- **Fuzzy Preferences**: Continuous [0,1] membership scores with trapezoid falloff for numeric ranges
- **Penalty Term**: Worst critical membership reduces overall score (prevents "jack of all trades" results)
- **Mutator Quality**: 83 map features rated -10 (very bad) to +10 (very good) - geothermal vents boost scores, toxic lakes reduce them
- **Configurable Weights**: 5 scoring presets (Balanced, Critical Focused, Strict Hierarchy, Ultra Critical, Precision Match)

### Dual-Mode Interface
- **Simple Mode**: Simplified UI with 8 essential filters and 4-column preset grid (11 curated + 4 user slots)
- **Advanced Mode**: Full control with 40+ filters organized into collapsible sections
- **Mode Independence**: Each mode maintains its own filter settings that persist across sessions. Copy settings between modes as needed.
- **Stone Selector**: Per-stone importance (Granite=Critical, Marble=Preferred) dynamically loaded from DefDatabase
- **Results Window**: Top-N matches with detailed tile information and rarity badges
  - **Rarity Badges**: Tiles with Rare or higher combinations show color-coded badges (Rare → Epic → Legendary → Mythic)
  - **Probabilistic Rarity**: Computed from canonical world data - multiplies biome × mutator probabilities
  - Click any result to jump to that tile on the world map

### Performance & Customization
- **Max Candidate Tiles**: Configurable Conservative (25k) through Unlimited (all settleable), default Standard (100k). Unlimited mode processes all tiles but may cause delays on large worlds.
- **Tiles Per Frame**: Default 500 (adjustable 50-1000). High-end systems can use 1000 for faster searches.
- **Stop Button**: Cancel long-running searches mid-evaluation
- **Logging Levels**: Verbose/Standard/Brief to control log spam
- **Mod Settings**: Performance tuning, scoring presets, UI preferences

## Installation

1. Download latest release from [Releases](https://github.com/wbryanta/RM-LZ/releases)
2. Extract to RimWorld/Mods/LandingZone
3. Enable in RimWorld mod list (requires Harmony)
4. Launch game and start new world or load existing save

## Usage

### Quick Start
1. Generate or load a RimWorld world
2. Click **"Filters"** button in bottom panel on "Select Starting Site" screen
3. Choose **Simple** or **Advanced** mode
4. Set your preferences (temperature, biome, coastal, etc.)
5. Click **"Search Landing Zones (Simple/Advanced)"** to start search
6. Click **"Top (20)"** to view results
7. Click any result to jump to that tile on the world map

### Simple Mode
- Simplified interface for casual users
- **Preset System**: 4-column grid with 12 curated presets + up to 4 user presets
  - **Special Tier** (Row 1): Elysian (perfect QoL), Exotic (rare combos), SubZero (frozen), Scorched Hell (heat)
  - **Playstyle Tier** (Rows 2-3): Desert Oasis, Defense, Agrarian, Power, Bayou, Savannah, Aquatic, Wildcard (random filters)
- **User Presets**: Save your current Simple mode filters as custom presets for reuse
- **Rarity Badges**: Presets show target rarity tier (Common → Mythic) based on probabilistic rarity scoring
- 8 essential filters: Biomes, Temperature, Rainfall, Coastal, Growing Season, Rivers, Roads, Stones
- Tri-state toggles: Ignored | Preferred | Critical
- **Independent settings**: Simple mode maintains its own filter preferences that persist across sessions

### Advanced Mode
- Full filter control for power users
- 40+ filters organized by category
- Per-item importance for stones, rivers, roads, biomes, features
- Stone Count mode: "Any 3 types" instead of specific stones
- All numeric filters have range sliders with fuzzy margins
- **Independent settings**: Advanced mode maintains its own filter preferences that persist across sessions

**Mode Independence**: Simple and Advanced modes each maintain their own independent filter settings. You can copy settings between modes using the "Copy to/from" buttons in each mode.

### Understanding Scores
- **1.0 = Perfect match** - all criticals excellent, all preferreds met
- **0.8-0.9 = Very good** - strong critical matches, good preferred matches
- **0.6-0.7 = Good** - solid criticals, some preferred misses
- **0.4-0.5 = Acceptable** - marginal criticals or weak preferreds
- **<0.4 = Poor** - at least one critical severely unmet

Worst critical membership acts as a penalty multiplier - a tile with 9 perfect criticals but 1 terrible critical will score poorly.

## Architecture

See `docs/architecture-v0.1-beta.md` for detailed architecture documentation.

**Core Components**:
- **Game Cache** (`Find.World.grid`) - Single source of truth for cheap tile data
- **TileDataCache** - Lazy computation for expensive RimWorld API calls
- **FilterService** - Two-phase pipeline orchestrator (Apply → Score)
- **MembershipFunctions** - Trapezoid, distance decay, binary membership computations
- **ScoringWeights** - Penalty term, global weights, final score calculation

**Filter Pattern**:
```csharp
public class ExampleFilter : ISiteFilter
{
    public FilterHeaviness Heaviness => FilterHeaviness.Light; // or Heavy

    public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
    {
        // Hard filtering: eliminate tiles that don't meet Critical importance
        return inputTiles.Where(id => MeetsCriteria(id));
    }

    public float Membership(FilterContext context, int tileId)
    {
        // Soft scoring: return [0,1] membership for Preferred importance
        return ComputeMembership(tileId);
    }
}
```

## Development

### Build
```bash
python3 scripts/build.py              # Debug build (default)
python3 scripts/build.py -c Release   # Release build
```

Builds project and copies DLL to `Assemblies/` directory for RimWorld to load.

### Task Management
```bash
python3 scripts/tasks_api.py          # Start task board server
# Open http://localhost:8080 in browser
```

Kanban board for tracking development tasks. Drag cards between swimlanes to update `tasks.json`.

### Adding a New Filter
1. Create `Source/Core/Filtering/Filters/YourFilter.cs` implementing `ISiteFilter`
2. Choose `FilterHeaviness.Light` (cheap data) or `.Heavy` (expensive APIs)
3. Register in `SiteFilterRegistry.RegisterDefaultFilters()`
4. Add properties to `FilterSettings.cs` (importance, ranges, etc.)
5. Wire up UI in `DefaultModeUI.cs` and/or `AdvancedModeUI.cs`

See `CLAUDE.md` for detailed development guide.

### Canonical World Library

For feature/defName accuracy and rarity data, consult `docs/CANONICAL_WORLD_LIBRARY.md` which is generated from the latest full world cache dump. It includes machine-readable JSON (`docs/data/canonical_world_library_2025-11-15.json`) with exact names and frequencies for every biome, mutator, river, and road. Regenerate it with `python3 scripts/analyze_world_cache.py <FullCache.txt> --json-summary <output>` whenever a new world baseline is captured.

## AI Agents & Overwatch

LandingZone uses two coordinated AI agents:

- **DevAgent (Claude Code)** – virtual SDE that implements features following `CLAUDE.md`.
- **Codex (QA/QC Overwatch)** – independent reviewer documented in `AGENTS.md` who validates code, tests, docs, and tasks before work lands.

Codex operates from the instructions in `AGENTS.md` only; DevAgent’s instructions remain in `CLAUDE.md`. Refer to `AGENTS.md` when requesting reviews or quality sign-off so Codex can apply the mandated verdict/issue template and severity bars.

### Versioning
Version follows semantic versioning: `major.minor.patch(-prerelease)`

Current: **0.1.3-beta**
- 0.1.x-beta: Feature additions and bug fixes during beta
- 1.0.0: First stable release (planned)

See `VERSIONING.md` for workflow details.

## Roadmap

**Completed (v0.1.0-0.1.3)**:
- ✅ Two-phase filtering architecture
- ✅ 40+ filter implementations
- ✅ Membership-based fuzzy scoring
- ✅ Mutator quality scoring (83 mutators)
- ✅ Default/Advanced UI modes
- ✅ Stone selector with per-stone importance
- ✅ Results window with top-N display
- ✅ Mod settings (performance, scoring, logging)
- ✅ Configurable max candidates
- ✅ Stop button for canceling searches

**Completed (v0.2.1-beta)**:
- ✅ Preset system with Angel/Unicorn/Demon bundles
- ✅ Rarity scoring based on canonical world data
- ✅ User preset save/load system
- ✅ Rarity badges on results window

**In Progress (v0.3.0+)**:
- 🔨 Documentation cleanup and architecture alignment
- 📋 Score breakdown transparency UI
- 📋 Results window UX refactor

**Planned (v0.3.0+)**:
- 📋 Bookmark manager for favorite tiles
- 📋 Heatmap overlay visualization on world map
- 📋 Advanced drag-to-order ranking with AND/OR logic
- 📋 "Why No Results?" diagnostic analysis

See `tasks.json` for full task list with priorities and estimates.

## Known Issues

- CoastalLakeFilter and AdjacentBiomesFilter use neighbor heuristic (may not perfectly match icosahedral grid)
- MapFeatureFilter uses reflection to access RimWorld 1.6+ Mutators (graceful degradation on older versions)
- FilterPerformanceTest.cs still references WorldSnapshot in comments (code is correct, comments outdated)

## Credits

**Inspiration**: [Prepare Landing](https://github.com/neitsa/PrepareLanding) (MIT License) by neitsa, m00nl1ght, and contributors. LandingZone is a from-scratch rewrite with new architecture and scoring system.

**Development**: Built with [Claude Code](https://claude.com/claude-code) assistance for forensic analysis, architecture design, and implementation.

**License**: MIT (see LICENSE file)

## Support

- **Issues**: [GitHub Issues](https://github.com/wbryanta/RM-LZ/issues)
- **Source**: [GitHub Repository](https://github.com/wbryanta/RM-LZ)

## Changelog

### v0.2.1-beta (2025-11-15)
- Added preset system with 4-column grid layout (11 curated + 4 user preset slots)
- Special tier presets: Elysian (perfect QoL), Exotic (rare combos), SubZero (frozen), Scorched Hell (heat)
- Playstyle tier presets: Desert Oasis, Defense, Agrarian, Power, Bayou, Savannah, Aquatic
- Implemented probabilistic rarity scoring based on canonical world data
- Added user preset save/load system with duplicate name detection
- Added rarity badges to results window (Rare → Epic → Legendary → Mythic)
- Removed emoji characters (not supported by RimWorld font)

### v0.1.3-beta (2025-11-13)
- Fixed Default/Advanced mode toggle buttons (removed 'active' parameter bug)
- Fixed tasks board API to handle nested priority structure
- Moved LZ-STONE-001 to completed (forensic analysis confirmed implementation complete)
- Removed LZ-SCORING-004 (validation task, scoring works in production)

### v0.1.2-beta (2025-11-13)
- Added Stop button to cancel running searches
- Fixed Top (XX) button to open results window
- Fixed Filters button accidentally opening results
- Added Max Candidate Tiles setting (25k-150k, default 100k)
- Fixed OutOfMemoryException from int.MaxValue pre-allocation

### v0.1.1-beta (2025-11-13)
- Mod settings: Scoring presets, logging levels
- UI consolidation to bottom panel
- Compact icon buttons for bookmarks
- Temperature display honors Celsius/Fahrenheit setting
- Fixed CriticalStrictness and button positioning bugs

See `VERSIONING.md` for complete version history.

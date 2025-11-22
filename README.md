# LandingZone

**Version**: 0.3.0-beta
**RimWorld Version**: 1.6
**Status**: Beta Release
**Author**: wcbryant

LandingZone helps you find the perfect settlement location by filtering world tiles based on climate, terrain, resources, and geographic features.

**This is a beta release.** While heavily tested with a wide range of popular mods, there are likely things to add, edit, remove, and improve. I will rely heavily on your feedback in Workshop or via https://github.com/wbryanta/RM-LZ

## Features

### 12 Curated Presets
One-click configurations for different playstyles and in-game experiences:
- **Special Presets** (4): Elysian (perfect paradise), Exotic (ultra-rare features), SubZero (frozen challenge), Scorched (volcanic nightmare)
- **Playstyle Presets** (8): Desert Oasis, Defense, Agrarian, Power, Bayou, Savannah, Aquatic, Homestead

All presets can be modified, and you can create and save your own custom presets.

### Advanced Mode
50+ filter criteria organized across 5 tabs:
- **Climate**: Temperature, rainfall, growing days, pollution
- **Geography**: Hilliness, coastal access, movement difficulty, swampiness
- **Resources**: Stones, forageability, plant/animal density, fish, grazing
- **Features**: Map features (mutators), rivers, roads, biomes
- **Results**: Result count, strictness, fallback tiers

### Live Selectivity Feedback
Real-time tile count estimates and restrictiveness warnings as you configure filters help you understand search scope before running.

### Intelligent Fallback Tiers
Progressive loosening when your ideal configuration has zero results. Presets targeting ultra-rare features (like Exotic or Scorched) automatically try fallback tiers to ensure you get results.

### Bookmarks
Save and name interesting tile locations for later comparison and reference. Bookmarks persist across sessions and can be viewed on the world map.

### Two-Phase Architecture
Efficient filtering and ranking of thousands of tiles in seconds:
- **Apply Phase**: Critical filters eliminate 90-95% of tiles instantly using game cache lookups
- **Score Phase**: Fuzzy membership scoring ranks remaining candidates with continuous [0,1] preferences
- **Performance**: Searches complete in seconds on standard worlds (handles 150k+ settleable tiles)

## Installation

1. Subscribe on Steam Workshop (coming soon) or download from [Releases](https://github.com/wbryanta/RM-LZ/releases)
2. Extract to `RimWorld/Mods/LandingZone`
3. Enable in RimWorld mod list (requires Harmony)
4. Launch game and start new world or load existing save

## Usage

### Quick Start
1. Generate or load a RimWorld world
2. Click **"Filters"** button in bottom panel on "Select Starting Site" screen
3. Choose a preset or configure filters manually
4. Click **"Search Landing Zones"** to start search
5. Click **"Top (20)"** to view results
6. Click any result to jump to that tile on the world map

### Default Mode (Presets)
- **12 curated presets** covering common playstyles (Temperate Paradise, Arctic Challenge, Desert Survival, etc.)
- **Visual preset cards** showing description and rarity target
- **Quick Tweaks panel** for minor adjustments without switching to Advanced mode
- **Save custom presets** from your current filter configuration

### Advanced Mode (Power Users)
- **5-tab interface** organizing 50+ filters by category
- **Per-item importance** for stones, rivers, roads, biomes, map features (Critical vs Preferred)
- **AND/OR operators** for multi-select filters
- **Live Preview panel** showing active filters, conflict warnings, and fallback tier preview
- **Search box** for quickly finding specific filters

### Understanding Scores
- **1.0 = Perfect match** - all criticals excellent, all preferreds met
- **0.8-0.9 = Very good** - strong critical matches, good preferred matches
- **0.6-0.7 = Good** - solid criticals, some preferred misses
- **0.4-0.5 = Acceptable** - marginal criticals or weak preferreds
- **<0.4 = Poor** - at least one critical severely unmet

Worst critical membership acts as a penalty multiplier - a tile with 9 perfect criticals but 1 terrible critical will score poorly.

## Development

### Build
```bash
python3 scripts/build.py              # Debug build (default)
python3 scripts/build.py -c Release   # Release build
```

Builds project and copies DLL to `Assemblies/` directory for RimWorld to load.

### Adding a New Filter
1. Create `Source/Core/Filtering/Filters/YourFilter.cs` implementing `ISiteFilter`
2. Choose `FilterHeaviness.Light` (cheap data) or `.Heavy` (expensive APIs)
3. Register in `SiteFilterRegistry.RegisterDefaultFilters()`
4. Add properties to `FilterSettings.cs` (importance, ranges, etc.)
5. Wire up UI in `DefaultModeUI.cs` and/or `AdvancedModeUI_Controls.cs`

### Architecture
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

## Known Issues

- CoastalLakeFilter and AdjacentBiomesFilter use neighbor heuristic (may not perfectly match icosahedral grid)
- MapFeatureFilter uses reflection to access RimWorld 1.6+ Mutators (graceful degradation on older versions)

## Credits

**Inspiration**: [Prepare Landing](https://github.com/neitsa/PrepareLanding) (MIT License) by neitsa, m00nl1ght, and contributors. LandingZone is a from-scratch rewrite with new architecture and scoring system.

**Development**: Built with [Claude Code](https://claude.com/claude-code) assistance for forensic analysis, architecture design, and implementation.

**License**: MIT (see LICENSE file)

## Support

- **Issues**: [GitHub Issues](https://github.com/wbryanta/RM-LZ/issues)
- **Source**: [GitHub Repository](https://github.com/wbryanta/RM-LZ)
- **Steam Workshop**: Coming soon

## Changelog

### v0.3.0-beta (2025-11-22)
- **12 curated presets** organized as 4 special + 8 playstyle themes
- **Advanced Mode redesign** with 5-tab organization (Climate, Geography, Resources, Features, Results)
- **Live selectivity feedback** with real-time tile count estimates
- **Intelligent fallback tiers** for presets targeting ultra-rare features
- **Bookmarks system** for saving and comparing interesting tiles
- **Conflict detection** warns when filter combinations are impossible or restrictive
- **Mod icons** added (Preview.png 16:9 format + ModIcon.png for world generation)
- **Repository cleanup** - removed internal development docs from public repo
- **Branding** - Author: wcbryant, Package ID: wcb.landingzone

### v0.2.1-beta (2025-11-15)
- Added preset system with 4-column grid layout
- Implemented probabilistic rarity scoring based on canonical world data
- Added user preset save/load system with duplicate name detection
- Added rarity badges to results window (Rare → Epic → Legendary → Mythic)

### v0.1.3-beta (2025-11-13)
- Fixed Default/Advanced mode toggle buttons
- Added Max Candidate Tiles setting (25k-150k, default 100k)
- Added Stop button to cancel running searches

### v0.1.1-beta (2025-11-13)
- Mod settings: Scoring presets, logging levels
- UI consolidation to bottom panel
- Temperature display honors Celsius/Fahrenheit setting

## Roadmap

**Completed (v0.3.0-beta)**:
- ✅ 12 curated presets with fallback tiers
- ✅ Advanced Mode with 5-tab organization
- ✅ Live selectivity feedback
- ✅ Bookmarks system
- ✅ Conflict detection
- ✅ Mod icons and branding

**Planned (v0.4.0+)**:
- 📋 Guided Builder mode (interactive preset creation wizard)
- 📋 Preset customization enhancements
- 📋 Canonical data automation
- 📋 Heatmap overlay visualization on world map
- 📋 Performance optimizations for huge worlds

## License

MIT License - see LICENSE file for details.

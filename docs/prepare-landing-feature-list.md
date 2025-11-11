# Prepare Landing – Feature Inventory

_Source code reference: `_downloads/PrepareLanding` (m00nl1ght 1.5/1.6 fork)._  
_Purpose: capture everything the legacy mod offers so LandingZone can plan parity and improvements._

## 1. Core Architecture
- **Entry point (`PrepareLanding.cs`)** wires up the singleton, pulls RimWorld settings, instantiates `GameData`, `WorldTileFilter`, `TileHighlighter`, and applies all Harmony patches.
- **Game data containers**: `GameData/GameData.cs` groups `DefData` (cached defs), `WorldData` (per-world stats), `UserData` (filter selections + options), and `GodModeData` (mutable world overrides). All user-facing values fire `INotifyPropertyChanged` so filters auto-respond.
- **Event bus (`RimWorldEventHandler`)** abstracts Harmony callbacks such as defs loaded, world generated, world loaded from save, world interface GUI tick, etc., allowing UI/services to subscribe.
- **WorldTileFilter** orchestrates every `ITileFilter`, subscribing to `UserData` changes and sequencing filters by “heaviness” (light → heavy) to reduce UI stalls. It maintains lists of all settleable tiles, matching tiles, and logging via `FilterInfoLogger`.
- **MonoController/TileHighlighter**: Unity `MonoBehaviour` that owns a `LineRenderer`, handles per-frame highlighting, blink timing, and draws tile labels on OnGUI hooks.

## 2. UI Surface (Main Window)
Main window (`MainWindow.cs`) is a custom `MinimizableWindow` shown via `Ctrl+P`/`P` hotkey. Key behaviors:
- **Tabs** (ordered via `TabGuiUtilityController`):
  1. `TabTerrain` – Biome, hilliness, roads, rivers, movement difficulty, forageability/food, stone types + ordering, coastal states, time zones, elevation, coastal rotation.
  2. `TabTemperature` – Temperature bands (avg/min/max), rainfall, growing period, animals graze, “most/least” characteristics, caves, world feature selector, coordinate window launcher, temperature forecast widget, “open coordinates” helper.
  3. `TabFilteredTiles` – Paginated list of matching tiles, tile selection info panel (biome, stats, world features, factions), controls to clear/highlight, scroll view reused in minimized window.
  4. `TabInfo` – World metadata (seed, coverage, counts), biome breakdown, world records, filter log inspector, and matching tile statistics.
  5. `TabOptions` – Filter behavior toggles (live filtering, allow impassable, disable prefilter, reset on new world, show heaviness), highlighter options (disable highlight/blink, debug ID labels, bypass max highlight limit).
  6. `TabLoadSave` – Preset manager with load/save modes, metadata (author, description), option to include mod options, pagination for preset files, and delete/overwrite confirmation flow.
  7. `TabGodMode` – Dev-mode-only panel to modify selected tiles (biome, temperature, hilliness, elevation, rainfall, roads/rivers, stone combos) before world generation completes; includes tile selection block and info readouts.
  8. Optional `TabOverlays` (compiled when flag enabled) for drawing custom overlays.
- **Bottom buttons** (always visible): Filter, Reset filters, Select random tile, Minimize, Close. In minimized mode there’s a compact control bar with paging buttons and the filtered list summary.
- **Window logic**: auto-minimize on demand, supports dragging, respects `IsWindowValidInContext` (world map only), and stores last scroll positions.

## 3. Filtering Catalog
WorldTileFilter registers each `ITileFilter` against a `UserData` property (list trimmed for brevity):
- **Terrain & geography**: Biome (`TileFilterBiomes`), Hilliness, Roads, Rivers, Movement Difficulty, Forageability, Foraged food type, Stone types (`ThreeStateItemContainerOrdered` for ordering), Coastal ocean/lake checks, Elevation, Time Zone, Coastal rotation.
- **Climate**: Average/min/max temperature, Growing period (start/end twelfths), Rainfall, “Animals can graze now” multi-state, Has caves state.
- **Advanced metrics**: Most/least characteristic (e.g., highest rainfall, temp, etc.), World feature membership, Forageable food, and future adjacency/feature filters (1.6 adds `TileFilterWorldFeature`).
- **Filter options**: heaviness metadata used for scheduling; OR/AND logic helpers handle tri-state checkboxes (On/Off/Partial) and def-lists. Prefilter stage validates prerequisites (e.g., requiring biome & terrain on 50%+ coverage) unless disabled.
- **Filtered tile outputs**: `AllMatchingTiles`, `AllValidTiles`, `AllTilesWithRoad/River`, with highlight integration and logging of warnings/errors for UI display.

## 4. Presets & Persistence
- `PresetManager` serializes user filters/options to disk (XML in legacy mod), supports load/save/delete, metadata (author, description), and optional inclusion of mod options. Preset tab surfaces preview of filter settings, option toggles, and handles Steam persona default author names.
- Presets capture filter states (biome choices, numeric ranges, tri-state toggles) but historically missed new feature filters (motivating improvements).

## 5. Visualization & Tools
- **Tile highlighting**: configurable blink duration, alpha, debug labels, and bypass for 10k tile cap. Hooks into world renderer via custom `WorldLayerHighlightedTiles` and listens to world interface GUI events for per-frame draws.
- **Filtered tile selection**: clicking a tile from the list selects it in RimWorld, jumps camera, and logs stats. Buttons exist to jump to start/end of list and to page when minimized.
- **Info surfaces**: filter log aggregator displays warnings/errors (e.g., “filter cleared tiles”), world info includes settleable counts and biome disease rates, and a world-record pane lists extreme tiles (highest elevation, rainfall, etc.).
- **Coordinate window**: launched from TabTemperature to inspect lat/long and distances.

## 6. God Mode Editing
- Only available when `Prefs.DevMode && DebugSettings.godMode` and before gameplay start. Allows per-tile overrides for biome, hilliness, elevation, temperature, rainfall, roads/rivers (limited counts), and stone combinations. Updates `GodModeData` and can refresh map visuals when toggled.

## 7. Harmony Patches & World Integration
- `PatchGenerateWorld` fires pre/post hooks so services reset caches and regenerate user data.
- `PatchWorldInterface` surfaces `WorldInterfaceOnGUI` and `WorldInterfaceUpdate` events for drawing overlays and hooking keyboard shortcuts.
- `PatchNaturalRockTypesIn` ensures rock generation respects user selections (esp. god mode).
- `PatchCreateWorldParams`, `PatchGenerateGridIntoWorld`, and `PatchGameFinalizeInit` integrate mod initialization with world/startup flow.
- `PagePreciseWorldGeneration` injects the “Precise World Generation Percentage” slider/page, letting players pick arbitrary coverage percentages (1–100%) beyond vanilla’s discrete options.

## 8. Hotkeys & Workflow Enhancements
- Default hotkey `Ctrl+P` (sometimes remapped to `P`); there’s a `KeyBindingDef` entry plus instructions in TabInfo/help text. Mod auto-opens when entering world map (can’t be disabled in original, per user complaints).
- Random tile selection button, and `AllowLiveFiltering` option to re-run filters automatically as settings change.
- Options to reset filters when generating a new world, allow impassable tiles, show filter heaviness, and allow invalid tiles for settlement (for debug/testing).

## 9. Known UX/Tech Debt (from feature review + comments)
- Filtering requires explicit biome/terrain under some conditions; “Any” selections aren’t always respected.
- Prefilter guard rails and error messaging are terse; minimizing often hides tile lists.
- God mode tab visibility tied to dev mode and can silently disappear; hotkey conflicts and window auto-open behavior frustrate users.
- Feature filters (habitats, faction proximity) are limited despite user demand; presets don’t capture new filter types.

This inventory should guide LandingZone’s architecture doc, parity checklist, and prioritization of improvements (e.g., new filters, better diagnostics, configurable UI behavior, richer presets).

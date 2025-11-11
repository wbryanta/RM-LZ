# LandingZone Architecture Blueprint

## 1. Goals & Scope
- Modern, modular alternative to Prepare Landing focused on fast landing-site discovery.
- Drop-in UX for the "Select Starting Site" page with optional intel overlays on in-game world maps.
- Provide a small public API (`LandingZone.IntelService`) for sister mods (e.g., CovertOps) to query filtered tiles, faction intel, and highlights.

## 2. High-Level Modules
| Module | Responsibility |
|--------|----------------|
| **Core** | Entry point (`LandingZoneMod`), Harmony registration, lifecycle hooks. |
| **Data** | `GameState` aggregates `DefCache`, `WorldSnapshot`, `UserPreferences`, `BestSiteProfile`. |
| **Filtering** | Registry of `ISiteFilter` + `ISiteScorer`; executes over settleable tiles with telemetry. |
| **UI** | Window/tab system (Terrain, Climate, Results, Options, Presets) + "Show Best Sites" button/heatmap slider drawer. |
| **Visualization** | Tile highlight service supporting score-based gradients, faction outlines. |
| **Intel API** | Service interface exposing filtered tile summaries, faction hostility, highlight hooks. |

## 3. Data Model
```
GameState
 ├─ DefCache (BiomeDefs, RiverDefs, FeatureDefs, etc.)
 ├─ WorldSnapshot (seed, coverage, settleable IDs, faction positions)
 ├─ UserPreferences
 │    ├─ FilterSettings (biome, temperature ranges, toggles)
 │    ├─ Options (auto-open, live filtering, highlight behavior)
 │    └─ Presets metadata
 └─ BestSiteProfile
      ├─ Requirements (coastal?, river?, feature?)
      ├─ TemperatureRange (min/max double-slider)
      ├─ RainfallRange
      └─ WeightMap (score contributions)
```

## 4. Filtering & Scoring Pipeline
1. `TileEnumerator` builds the list of candidate tiles (respecting settleable/impassable options).
2. `FilterEngine` iterates over registered `ISiteFilter`s (light → heavy). Each filter returns `FilterResult` (matched IDs, diagnostics, duration).
3. `Telemetry` records per-filter timing, eliminated counts, warnings.
4. `ScoringEngine` (optional) takes surviving tiles + `BestSiteProfile` to compute normalized scores (0–1) for heatmap.
5. Results propagate to UI tabs, highlight service, and Intel API.

Interfaces:
```csharp
public interface ISiteFilter
{
    string Id { get; }
    FilterHeaviness Heaviness { get; }
    FilterResult Apply(FilterContext context);
}

public interface ISiteScorer
{
    string Id { get; }
    float ScoreTile(TileContext tile, BestSiteProfile profile);
}
```

## 5. UI Layout (MVP)
- **Tabs**: Terrain, Climate, Results, Options, Presets. Each tab renders using Verse `Window` API but with a slimmer control palette.
- **Results Tab**: consolidated list view + quick stats; integrated keyboard navigation.
- **Controls**:
  - `Show Best Sites` (primary button). Subtitle indicates number of highlighted tiles.
  - **Preferences Drawer** (chevron icon) with checkboxes (Coastal, River, Feature, Biome lock), double-ended sliders for temperature/rainfall, weighting toggles.
  - Tooltip for diagnostics (“Filtered 15,000 tiles, matched 234. 12 eliminated due to rainfall < 400mm”).
- **Window behavior settings** accessible via gear icon (auto-open, anchor, hotkey reference). State stored in `UserPreferences.Options`.

## 6. Visualization
- `HighlightService` maintains multiple layers:
  - `FilterMatchLayer`: standard highlight (default cyan outline).
  - `BestSiteHeatmap`: gradient stroke per tile (green→yellow depending on score percentile).
  - `FactionIntelLayer`: outlines hostiles (red) or allies (blue) when toggled in gameplay mode.
- Each layer listens to FilterEngine events and supports `HighlightRequest` objects so external mods can register overlays safely.

## 7. Intel Service (API Surface)
```csharp
public interface IIntelService
{
    IReadOnlyList<SiteSummary> GetFilteredSites(SiteQuery query);
    IReadOnlyList<FactionIntel> GetFactionIntel(FactionQuery query);
    IDisposable PushHighlight(HighlightRequest request);
}
```
- `SiteSummary` includes tile ID, biome, stats, best-site score.
- `FactionIntel` includes settlement ID, goodwill, hostility, last-known raid status.
- `HighlightRequest` allows external mods to draw overlays with color/label metadata (subject to throttle & sandbox rules).

## 8. World Map Modes
- **World Generation (“Select Starting Site”)**: full UI window visible. Highlight layers available: filter matches, best-site heatmap.
- **In-Game World View**: condensed toolbar (Show Enemies, Show Allies, Show Best Sites). Uses the same highlight layers but hides filter tabs. Settings stored per save.

## 9. Extension Points
- Filters & scorers discovered via reflection on `LandingZone.Filters` namespace (or attribute `[RegisterSiteFilter]`). Allows optional content packs.
- Preset schema versioned (store `schemaVersion` in JSON). Migration helpers inform users if a preset is stale.
- `IntelService` hooks allow CovertOps or future mods to subscribe to tile changes or highlight states without directly poking UI internals.

## 10. Next Steps
1. Finalize data classes (`UserPreferences`, `BestSiteProfile`).
2. Scaffold `FilterEngine`, register a handful of core filters (biome, temperature, rainfall, coastal).
3. Implement `HighlightService` placeholder to unblock UI prototyping.
4. Flesh out `IIntelService` contract (docs + stub implementation) to satisfy `LZ-API-001`.

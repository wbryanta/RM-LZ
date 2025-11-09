# Prepare Landing Reverse-Engineering Notes

These notes capture the behavior and structure of m00nl1ght's Prepare Landing fork so we can selectively
port ideas into LandingZone without inheriting legacy complexity.

## Project Layout
- `PrepareLanding.cs` bootstraps the mod, sets up `GameData` (defs, user data, god-mode data) and wires
  `WorldTileFilter`, `TileHighlighter`, and the main Harmony instance.
- `GameData/` splits state into reusable buckets: definition cache, per-world data, and the user-facing
  filter/god-mode selections.
- `Filters/` contains dozens of `ITileFilter` implementations. Each filter inherits from `TileFilter`
  which provides shared OR / AND helpers for def-based filtering.
- `Core/Gui/` holds custom window/tab abstractions, plus `TileHighlighter` and world layers for drawing
  highlighted tiles.
- `MainWindow.cs` orchestrates all tabs (terrain, temperature, info, options, load/save, god mode) and
  the minimized floating window.
- `Patches/` uses Harmony to hook world generation, world grid updates, RimWorld UI entry points, and
  precise world gen percentage pages.

## Functional Takeaways
1. **Filter Pipeline** – Filters are registered in `WorldTileFilter` with a user-data property name.
   Filters are sorted by "heaviness" (light → heavy) to reduce UI hitching.
2. **UserData Binding** – Each user-facing option raises `PropertyChanged`, which the filter listens to
   for auto-refresh cues.
3. **Tile Highlighting** – Highlights run through a custom `WorldLayerHighlightedTiles` that is marked
   dirty whenever the matching tile list changes.
4. **Preset Manager** – Presets serialize both filter settings and mod options, enabling quick load/save
   cycles across playthroughs.
5. **MonoController** – A persistent `GameObject` provides Unity components (e.g., a `LineRenderer`)
   needed for overlays.

## Pain Points / Opportunities
- The filter registry is a large switchboard; new filters require editing the constructor directly.
- Tile blinking/highlighting loops are tightly coupled to `PrepareLanding.Instance` and Unity leaks.
- Harmony patches live in a flat namespace with limited documentation, making it harder to see why a
  patch exists.
- Heavy filters can still freeze the UI because everything runs synchronously on the main thread.

## LandingZone Action Items
- Extract a data-driven filter registration system (e.g., attribute-based or config-driven) so that
  additional filters stay decoupled.
- Build an event bus that does not rely on `static` singletons for easier testing.
- Provide diagnostics/telemetry for filter runtimes to surface expensive combinations.
- Keep presets but store them in a versioned schema for forward compatibility.
- Consider async chunking or per-frame work budgets when processing large tile sets.

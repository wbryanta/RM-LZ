# Prepare Landing Community Feedback (Steam posts)

_Source: `_downloads/m00nl1ght_PrepareLanding_comments.md` (first ~10 pages of Steam comments, Nov 2024–Jul 2025 timeframe)._

## Recurring Issues & Pain Points

### 1. Filtering Logic & Results
- **Under-reported matches** – users believe the mod only inspects the “top N” tiles rather than filtering across the whole world (e.g., rainfall characteristic limited to first 10k tiles).
- **Hard requirements on biome/terrain** – filtering fails on 100% world coverage unless both biome and terrain are explicitly set; users want logical OR behavior and the ability to search “any biome/terrain”.
- **Prefilter guardrails too strict** – disabling prefilter checks is hidden; people hit errors like “filtering terrains resulted in 0 matching tiles”.
- **Minimized tile list missing** – several reports that the minimized window doesn’t show the filtered list, making it impossible to cycle results.
- **Large-world performance** – 100% coverage searches either hang or get blocked when coastal/biome filters are “Any”.

### 2. UX & Discoverability
- **Opening the window** – confusion over the CTRL+P (sometimes just P) hotkey; users request clearer instructions or configurable bindings.
- **Window auto-opening** – players want to disable the window appearing every time they open the world map; it obstructs vanilla UI elements.
- **Keybind conflicts** – in space maps, the hotkey changed to `P`, conflicting with “Select similar”; also disables pause/escape keys in some cases.
- **God mode visibility** – reports that the god mode tab can disappear (esp. on 1.6), even when enabled in dev mode.

### 3. Feature Gaps / Requests
- **River granularity** – ability to filter by specific river sizes (huge/large/creek).
- **Feature filters** – search for specific map features (habitats, adjacent biomes); 1.6 update added a basic feature filter, but users still want more (e.g., # of features, animal habitats, nearby factions).
- **Saved presets** – include feature selections in saved filters; currently missing.
- **Nearby faction filters** – requested ability to filter tiles by distance to factions.
- **Extreme map sizes** – mod reportedly disables extreme map sizes from other mods.
- **Window navigation** – request for keyboard navigation (up/down) through the filtered tile list.

### 4. Compatibility/Load Issues
- **RimPy/RimSort** – users note Prepare Landing not appearing when loaded via RimPy; others recommend RimSort.
- **Missing in mod list** – some players can’t see the mod or can’t open the UI despite it being enabled.
- **Planet color changes** – one report blaming Prepare Landing for brown maps; author states mod doesn’t touch generation colors.

### 5. Quality-of-life Friction
- **Hotkey instructions** – multiple players simply didn’t know the hotkey or that the mod works post-settlement.
- **Popup spam** – the window reopens on every map visit and sits over time controls.
- **Confusing error messages** – e.g., “Filtering Terrains resulted in 0 matching tiles” could explain what setting to adjust.

## Opportunities for LandingZone

1. **Transparent Filter Engine**
   - Always scan all tiles; no hidden sampling. Surface the number of tiles evaluated vs matched.
   - Allow “Any” selections without forcing biome/hilliness combos; handle constraints with warnings, not hard stops.
   - Provide better explanations when filters eliminate everything (e.g., show which constraint zeroed results).

2. **Configurable UI Behavior**
   - Expose window open behavior (auto vs manual) and per-context settings (only on world gen, never on load, etc.).
   - Add clear hotkey reminders and allow rebinding.
   - Ensure minimized view shows the tile list with page controls; add keyboard navigation.

3. **Feature Enhancements**
   - Built-in filters for river magnitude, nearby factions, feature counts, animal habitats.
   - Include feature selections in preset serialization and support versioned presets.
   - Consider an adjacency filter for modded content (habitats added by other mods).

4. **Stability & Compatibility**
   - Provide diagnostics when the mod fails to open (missing Harmony, conflicting mods).
   - Avoid meddling with other mods’ map-size settings; only observe world data.
   - Document how to use LandingZone post-settlement and in secondary colonies.

5. **God Mode Reliability**
   - Ensure the god mode tab is discoverable, respects dev-mode toggles, and logs when it can’t be shown.

6. **Quality of Feedback**
   - Add logging/toast notifications when filters are too strict, rather than silent failures.
   - Offer a “What’s wrong?” helper describing why the list is empty or why a prefilter triggered.

These points should inform `docs/prepare-landing-review.md`, future task breakdowns (e.g., `LZ-FILTER-001`, `LZ-UI-001`), and the roadmap we’ll draft.

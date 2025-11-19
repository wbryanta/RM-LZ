# Codex Session Handoff (2025-11-16)

Minimal context to bootstrap next session (beyond AGENTS.md/CLAUDE.md):

- Canonical data: `docs/data/canonical_world_library_aggregate.json` (5 dumps; ~1.47M tiles, ~703k settleable). World dumps live in `~/Library/Application Support/RimWorld/Config/` (multiple `LandingZone_FullCache_*.txt` and `LandingZone_CacheAnalysis_*` reports). Always validate defNames against the aggregate before wiring filters/presets.

- Versioning: single source of truth = `VERSIONING.md` + `About/About.xml`. Avoid hardcoded version strings elsewhere. Current working build line is ~0.2.1-beta (preset hub, Simple/Advanced split, rarity badges).

- Simple vs Advanced: independent `FilterSettings` with copy buttons. Simple hosts preset grid (12 curated + up to 4 user presets). Advanced remains fully granular. Copy buttons are explicit; no shared state.

- Preset hub state: 12 presets (Elysian, Exotic, SubZero, Scorched Hell, Desert Oasis, Defense, Agrarian, Power, Bayou, Savannah, Aquatic, Homesteader). Preset redesign spec: `docs/preset_redesign_v0.3.md`. Key risks: (1) Exotic uses triple AND of ultra-rares → likely zero results; needs staged/fallback logic. (2) Desert Oasis water bullets contradict “Critical, AND” header. (3) Aquatic requires Coastal AND River (may zero out on some worlds). (4) MutatorQualityOverrides must flow through scoring, scoped to active preset only.

- Tasks/sprint: 0.3.x goals include preset refinement per `preset_redesign_v0.3.md`, `LZ-UX-REORG` (Advanced grouping/search/affordances), `LZ-CANONICAL-COVERAGE` (one control per mutator/feature, friendly labels, dedupe), `LZ-ADV-SEARCH-FILTER`, `LZ-SCORE-LABEL-VALIDATION`. FUT candidate: `FUT-WEIGHTED-IMPORTANCE` (not in 0.3.x scope).

- Logging: prefer `LandingZoneLogger` (Standard/Debug) with concise, probative messages. Avoid verbose spam that suppresses RimWorld logging.

- Latest notable log: `_downloads/debug_log_16112025-1009.txt` (Unlimited, ~157k candidates). New dumps: `LandingZone_FullCache_2025-11-15_*` and `LandingZone_FullCache_2025-11-16_*` (if present). Use `[DEBUG] Dump` outputs to verify scoring breakdowns.

- UX notes: rarity badges abbreviated; emojis removed. Preset grid widened to 4 columns (window 640×720). User presets capped at 4.

Bring this file into the next session if history is reset.

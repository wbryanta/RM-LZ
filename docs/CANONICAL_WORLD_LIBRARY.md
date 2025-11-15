# Canonical World Library

LandingZone now maintains a machine-readable catalog of every biome, map feature (mutator), river, and road discovered in the latest full world cache dump. This file is the single source of truth for naming, frequency, and relative rarity when implementing filters or UI labels.

- **Source dump:** `/Users/will/Library/Application Support/RimWorld/Config/LandingZone_FullCache_2025-11-15_11-02-17.txt`
- **Generated summary:** `docs/data/canonical_world_library_2025-11-15.json`
- **Extractor:** `python3 scripts/analyze_world_cache.py <FullCache.txt> --json-summary <output>`

## File structure

The JSON summary contains:

```json
{
  "generated": "2025-11-15T11:17:21",
  "total_tiles": 295732,
  "settleable_tiles": 127752,
  "map_features": [{
    "name": "Caves",
    "count_all": 11063,
    "percent_all": 0.037,
    "count_settleable": 11063,
    "percent_settleable": 0.0866
  }, ...],
  "rivers": [...],
  "roads": [...],
  "biomes": [...]
}
```

Every entry supplies the raw counts (all tiles + settleable) and normalized percentages, so engineers can immediately determine how common a feature is under "normal" world generation settings. Use these values when:

1. Wiring hardcoded names – pull the `name` field directly so we never repeat the "Cave" vs "Caves" mistake.
2. Designing UX defaults – rarity data tells us which filters deserve top billing or warnings.
3. Building heuristics – e.g., "Caves" appear on 8.7% of settleable tiles; any search returning 0 results for that criterion is suspect.

## Regenerating the library

1. Dump a full world cache from the Advanced preferences window (Dev Mode required).
2. Run `python3 scripts/analyze_world_cache.py /path/to/LandingZone_FullCache_<timestamp>.txt --json-summary docs/data/canonical_world_library_<timestamp>.json`
3. Update this document (and any references) to point at the new file.

Always treat the JSON summary as canonical before introducing new mutators, DLC-specific features, or validation logic.

### Aggregating multiple worlds

To compute cross-world frequencies, use:

```bash
python3 scripts/aggregate_world_stats.py docs/data/canonical_world_library_*.json --output docs/data/canonical_world_library_aggregate.json
```

This produces combined counts/percentages across every JSON summary passed in, enabling features like "Unicorn" or "Demon" searches that rely on true rarity metrics.

### Feature rarity snapshot (2025-11-15 world)

Most common mutators (settleable tiles):
- Mountain: 23,387 (18.31%)
- Caves: 11,435 (8.95%)
- Coast: 7,394 (5.79%)
- River: 6,201 (4.85%)
- SunnyMutator: 2,463 (1.93%)

Rarest mutators observed (non-zero occurrences):
- IceCaves: 3 (0.002%)
- ArcheanTrees: 3 (0.002%)
- LavaLake: 2 (0.002%)
- Iceberg: 1 (0.001%)
- Fish_Decreased: 2 (0.002%)

Use these values when designing Angel/Unicorn/Demon presets: “Unicorn” should target the <0.01% cohort, whereas “Angel” can leverage the abundant positives.

_Current aggregate (Nov 13 + Nov 15 worlds):_ `docs/data/canonical_world_library_aggregate.json`
- Samples: 3 full-cache dumps
- Total tiles aggregated: 887,196 (418,372 settleable)

Aggregate snapshot:
- Common mutators: Mountain 69,984 (16.73%), Caves 34,014 (8.13%), Coast 21,885 (5.23%), River 21,997 (5.26%), SunnyMutator 8,101 (1.94%)
- Rare mutators observed: LavaCrater 7 (0.0017%), AncientRuins_Frozen 3 (0.0007%), Fish_Decreased 6 (0.0014%), Crevasse 6 (0.0014%), IceCaves 4 (0.0010%)

Aggregate snapshot (4 worlds as of 2025-11-15):
- Samples: 4 full dumps (total tiles: 1,182,928; settleable: 563,803)
- Common mutators: Mountain 94,391 (16.74%), Caves 46,407 (8.23%), Coast 29,282 (5.19%), River 29,175 (5.17%), SunnyMutator 11,007 (1.95%)
- Rare mutators observed: LavaLake 11 (0.0020%), AncientRuins_Frozen 4 (0.0007%), LavaCrater 7 (0.0012%), Crevasse 7 (0.0012%), IceCaves 5 (0.0009%)

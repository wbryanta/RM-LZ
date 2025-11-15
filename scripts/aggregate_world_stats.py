#!/usr/bin/env python3
"""Aggregate multiple canonical_world_library_*.json summaries into combined stats."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Dict, List


def load_summary(path: Path) -> dict:
    with path.open("r", encoding="utf-8") as fp:
        return json.load(fp)


def combine_entries(entries: List[dict]) -> dict:
    combined = {}
    for entry in entries:
        name = entry["name"]
        data = combined.setdefault(name, {
            "name": name,
            "count_all": 0,
            "count_settleable": 0,
        })
        data["count_all"] += entry.get("count_all", 0)
        data["count_settleable"] += entry.get("count_settleable", 0)
    return combined


def aggregate(files: List[Path]) -> dict:
    total_tiles = 0
    settleable_tiles = 0
    biomes: Dict[str, dict] = {}
    mutators: Dict[str, dict] = {}
    rivers: Dict[str, dict] = {}
    roads: Dict[str, dict] = {}

    for path in files:
        summary = load_summary(path)
        total_tiles += summary.get("total_tiles", 0)
        settleable_tiles += summary.get("settleable_tiles", 0)
        biomes = combine_entries(list(biomes.values()) + summary.get("biomes", []))
        mutators = combine_entries(list(mutators.values()) + summary.get("map_features", []))
        rivers = combine_entries(list(rivers.values()) + summary.get("rivers", []))
        roads = combine_entries(list(roads.values()) + summary.get("roads", []))

    def finalize(entries: Dict[str, dict]) -> List[dict]:
        results = []
        for data in entries.values():
            count_all = data["count_all"]
            count_settleable = data["count_settleable"]
            results.append({
                "name": data["name"],
                "count_all": count_all,
                "percent_all": count_all / total_tiles if total_tiles else 0,
                "count_settleable": count_settleable,
                "percent_settleable": count_settleable / settleable_tiles if settleable_tiles else 0,
            })
        return sorted(results, key=lambda e: e["count_all"], reverse=True)

    return {
        "generated": Path(files[-1]).name,
        "samples": len(files),
        "total_tiles": total_tiles,
        "settleable_tiles": settleable_tiles,
        "biomes": finalize(biomes),
        "map_features": finalize(mutators),
        "rivers": finalize(rivers),
        "roads": finalize(roads),
    }


def main():
    parser = argparse.ArgumentParser(description="Aggregate canonical world library JSON files")
    parser.add_argument("inputs", nargs="+", type=Path, help="List of canonical_world_library_*.json files")
    parser.add_argument("--output", required=True, type=Path, help="Output JSON path")
    args = parser.parse_args()

    missing = [path for path in args.inputs if not path.exists()]
    if missing:
        raise SystemExit(f"Missing inputs: {missing}")

    summary = aggregate(args.inputs)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("w", encoding="utf-8") as fp:
        json.dump(summary, fp, indent=2)

    print(f"Written aggregated stats to {args.output}")


if __name__ == "__main__":
    main()

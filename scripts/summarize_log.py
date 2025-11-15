#!/usr/bin/env python3
"""Extracts key LandingZone diagnostics from large debug logs.

Usage:
    python3 scripts/summarize_log.py /path/to/debug_log.txt [--top 10]

Outputs:
    - Summary of filter application phases
    - Candidate counts (initial, surviving)
    - Any warnings/errors
    - Top zero-results diagnostics (if present)
    - Score summary samples (top ranks)
"""
from __future__ import annotations

import argparse
import re
from pathlib import Path
from typing import Iterable

SECTION_HEADERS = [
    "Filter application summary",
    "Membership scoring summary",
    "Results dump",
    "Diagnostics"
]

FILTER_APPLY_RE = re.compile(r"\[LandingZone\] (.+?): Filtered (\d+) → (\d+) tiles")
BITSET_RE = re.compile(r"\[LandingZone\] BitsetAggregator: (.+)")
WARNING_RE = re.compile(r"\[LandingZone\] (?:⚠️|WARNING|ERROR)(.+)")
RESULT_ROW_RE = re.compile(r"Rank (\d+) :: Tile (\d+) :: Score ([0-9.]+)")
PROGRESS_RE = re.compile(r"\[LandingZone\] Search progress ([0-9.]+)% - Evaluating (\d+) candidates")


def parse_log_lines(lines: Iterable[str], max_results: int = 10) -> dict:
    data: dict[str, object] = {
        "filter_counts": [],
        "bitset": [],
        "warnings": [],
        "results": [],
        "progress": [],
    }

    for line in lines:
        line = line.rstrip("\n")
        if match := FILTER_APPLY_RE.search(line):
            data["filter_counts"].append(match.groups())
        elif match := BITSET_RE.search(line):
            data["bitset"].append(match.group(1))
        elif match := RESULT_ROW_RE.search(line):
            if len(data["results"]) < max_results:
                data["results"].append(match.groups())
        elif match := WARNING_RE.search(line):
            data["warnings"].append(match.group(1).strip())
        elif match := PROGRESS_RE.search(line):
            data["progress"].append(match.groups())

    return data


def summarize(parsed: dict[str, object]) -> str:
    parts: list[str] = []

    filters = parsed["filter_counts"]
    if filters:
        parts.append("Filter Apply Counts:")
        for name, before, after in filters[:15]:
            parts.append(f"  - {name}: {before} → {after}")

    if parsed["bitset"]:
        parts.append("Bitset Aggregator Stats:")
        parts.extend(f"  - {entry}" for entry in parsed["bitset"])

    if parsed["progress"]:
        last = parsed["progress"][-1]
        parts.append(f"Final Progress: {last[0]}% ({last[1]} candidates)")

    if parsed["results"]:
        parts.append("Top Results:")
        for rank, tile, score in parsed["results"]:
            parts.append(f"  - Rank {rank} tile {tile}: score {score}")

    if parsed["warnings"]:
        parts.append("Warnings/Errors:")
        for msg in parsed["warnings"]:
            parts.append(f"  - {msg}")

    if not parts:
        parts.append("No LandingZone markers found in log (check path or format).")

    return "\n".join(parts)


def main() -> None:
    parser = argparse.ArgumentParser(description="Summarize LandingZone debug logs")
    parser.add_argument("log_path", type=Path)
    parser.add_argument("--top", type=int, default=10, help="Number of top results to show (default: 10)")
    args = parser.parse_args()

    if not args.log_path.exists():
        raise SystemExit(f"Log not found: {args.log_path}")

    with args.log_path.open("r", encoding="utf-8", errors="ignore") as fh:
        parsed = parse_log_lines(fh, max_results=args.top)

    print(summarize(parsed))


if __name__ == "__main__":
    main()

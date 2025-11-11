#!/usr/bin/env python3
"""Convenience build script for LandingZone.

Usage:
  python scripts/build.py [--configuration Release]

Builds the Source project with dotnet, then copies LandingZone.dll
into Assemblies/ for RimWorld to load.
"""
from __future__ import annotations

import argparse
import os
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE_DIR = ROOT / "Source"
ASSEMBLIES_DIR = ROOT / "Assemblies"
PROJECT = SOURCE_DIR / "LandingZone.csproj"
DLL_NAME = "LandingZone.dll"
DOTNET_CLI_HOME = ROOT / ".dotnet-cli"

def run(cmd: list[str], cwd: Path) -> None:
    env = os.environ.copy()
    DOTNET_CLI_HOME.mkdir(parents=True, exist_ok=True)
    env.setdefault("DOTNET_CLI_HOME", str(DOTNET_CLI_HOME))
    env.setdefault("DOTNET_SKIP_FIRST_TIME_EXPERIENCE", "1")
    env.setdefault("DOTNET_CLI_TELEMETRY_OPTOUT", "1")
    env.setdefault("DOTNET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER", "0")
    result = subprocess.run(cmd, cwd=cwd, env=env)
    if result.returncode != 0:
        raise SystemExit(result.returncode)

def restore() -> None:
    if not PROJECT.exists():
        raise SystemExit(f"Missing project file: {PROJECT}")

    cmd = ["dotnet", "restore", str(PROJECT)]
    print("Running:", " ".join(str(part) for part in cmd))
    run(cmd, cwd=SOURCE_DIR)


def build(configuration: str) -> Path:
    print(f"Building {PROJECT} ({configuration})")
    cmd = ["dotnet", "build", str(PROJECT), "-c", configuration, "--no-restore"]
    print("Running:", " ".join(str(part) for part in cmd))
    run(cmd, cwd=SOURCE_DIR)

    dll_path = SOURCE_DIR / "bin" / configuration / "net472" / DLL_NAME
    if not dll_path.exists():
        raise SystemExit(f"Build succeeded but {dll_path} was not found")
    return dll_path

def copy_to_assemblies(dll_path: Path) -> None:
    ASSEMBLIES_DIR.mkdir(parents=True, exist_ok=True)
    target = ASSEMBLIES_DIR / DLL_NAME
    shutil.copy2(dll_path, target)
    print(f"Copied {dll_path.relative_to(ROOT)} -> {target.relative_to(ROOT)}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build LandingZone and stage Assemblies output")
    parser.add_argument("--configuration", "-c", default="Debug", choices=["Debug", "Release"],
                        help="Build configuration (Debug or Release). Default: Debug")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    restore()
    dll_path = build(args.configuration)
    copy_to_assemblies(dll_path)


if __name__ == "__main__":
    main()

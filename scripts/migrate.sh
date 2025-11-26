#!/usr/bin/env bash
set -euo pipefail

# Syncs the release-facing LandingZone folder from LZ-DEV.
# Default is dry-run; pass --apply to perform the copy.

usage() {
  cat <<'EOF'
Usage: migrate.sh [--apply]
  Syncs release assets from LZ-DEV into ../LandingZone.
  Default: dry-run (prints planned changes).
  --apply   Execute the sync (uses rsync --delete).
EOF
}

if [[ "${1-}" == "-h" || "${1-}" == "--help" ]]; then
  usage
  exit 0
fi

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
SRC_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"                 # LZ-DEV
DST_ROOT="$(cd "$SRC_ROOT/.." && pwd)/LandingZone"       # Release folder

extract_mod_version() {
  local file="$1"
  if [[ -f "$file" ]]; then
    # BSD-friendly sed: strip leading content up to <modVersion> and trailing </modVersion>
    grep -m1 "<modVersion>" "$file" | sed -e 's/.*<modVersion>//' -e 's#</modVersion>.*##' | tr -d '[:space:]'
  fi
}

compare_versions() {
  python3 - "$1" "$2" <<'PY'
import sys
def base(v):
    return v.split('-')[0]
def cmp(a,b):
    pa=list(map(int, base(a).split('.')))
    pb=list(map(int, base(b).split('.')))
    m=max(len(pa), len(pb))
    pa+= [0]*(m-len(pa))
    pb+= [0]*(m-len(pb))
    if pa>pb: print(1)
    elif pa<pb: print(-1)
    else: print(0)
cmp(sys.argv[1], sys.argv[2])
PY
}

REQUIRED_FILES=(
  "About/About.xml"
  "About/Manifest.xml"
  "Assemblies/LandingZone.dll"
  "Languages/English/Keyed/LandingZone.xml"
  "Defs/GlobalWorldDrawLayerDefs/LandingZone_WorldLayers.xml"
)

for rel in "${REQUIRED_FILES[@]}"; do
  if [[ ! -f "$SRC_ROOT/$rel" ]]; then
    echo "Missing required file in LZ-DEV: $rel" >&2
    exit 1
  fi
done

APPLY=0
if [[ "${1-}" == "--apply" ]]; then
  APPLY=1
fi

mkdir -p "$DST_ROOT"

# Version guards
DEV_VERSION=$(extract_mod_version "$SRC_ROOT/About/About.xml")
DST_VERSION=$(extract_mod_version "$DST_ROOT/About/About.xml")

if [[ -z "$DEV_VERSION" ]]; then
  echo "Missing <modVersion> in $SRC_ROOT/About/About.xml" >&2
  exit 1
fi

if [[ "$DEV_VERSION" != *"-dev" ]]; then
  echo "DEV version should end with -dev (found '$DEV_VERSION')." >&2
  exit 1
fi

if [[ -n "$DST_VERSION" ]]; then
  cmp=$(compare_versions "$DEV_VERSION" "$DST_VERSION")
  if [[ "$cmp" -le 0 ]]; then
    echo "Refusing to migrate: DEV version ($DEV_VERSION) is not greater than LandingZone version ($DST_VERSION)." >&2
    exit 1
  fi
fi

RSYNC_FLAGS=(-avh --delete)
RSYNC_FLAGS+=(--no-times --omit-dir-times --no-perms)
if [[ $APPLY -eq 0 ]]; then
  echo "Dry run (pass --apply to execute)"
  RSYNC_FLAGS+=(--dry-run)
fi

sync_dir() {
  local subdir="$1"
  local src="$SRC_ROOT/$subdir/"
  local dst="$DST_ROOT/$subdir/"
  rsync "${RSYNC_FLAGS[@]}" "$src" "$dst"
}

sync_dir "About"
sync_dir "Assemblies"
sync_dir "Defs"
sync_dir "Languages"
sync_dir "Patches"

# Rewrite modVersion/version in LandingZone to beta suffix
if [[ $APPLY -eq 1 ]]; then
  BETA_VERSION="${DEV_VERSION/-dev/-beta}"
  if [[ -f "$DST_ROOT/About/About.xml" ]]; then
    perl -0pi -e "s#<modVersion>[^<]+</modVersion>#<modVersion>$BETA_VERSION</modVersion>#g" "$DST_ROOT/About/About.xml"
    # Force release name and packageId to non-dev values
    perl -0pi -e "s#<name>[^<]+</name>#<name>LandingZone</name>#g" "$DST_ROOT/About/About.xml"
    perl -0pi -e "s#<packageId>[^<]+</packageId>#<packageId>wcb.landingzone</packageId>#g" "$DST_ROOT/About/About.xml"
  fi
  if [[ -f "$DST_ROOT/About/Manifest.xml" ]]; then
    perl -0pi -e "s#<version>[^<]+</version>#<version>$BETA_VERSION</version>#g" "$DST_ROOT/About/Manifest.xml"
  fi
fi

echo
echo "LandingZone contents after sync:"
du -sh "$DST_ROOT"

echo
echo "Checksums:"
md5 "$DST_ROOT/Assemblies/LandingZone.dll" "$DST_ROOT/Languages/English/Keyed/LandingZone.xml"

echo
echo "Version check:"
grep -m1 "<modVersion>" "$DST_ROOT/About/About.xml" || true

#!/usr/bin/env bash
set -euo pipefail

# Syncs the release-facing LandingZone folder from LZ-DEV.
# Default is dry-run; pass --apply to perform the copy.

usage() {
  cat <<'EOF'
Usage: migrate.sh [--apply] [--install-app]
  Syncs release assets from LZ-DEV into ../LandingZone.
  Default: dry-run (prints planned changes).
  --apply        Execute the sync (uses rsync --delete).
  --install-app  After sync, also deploy LandingZone into RimWorldMac.app/Mods.
  --skip-lint    Skip translation/UI hardcoded string lint (use sparingly).
EOF
}

APPLY=0
INSTALL_APP=0
SKIP_LINT=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    -h|--help) usage; exit 0 ;;
    --apply) APPLY=1 ;;
    --install-app) INSTALL_APP=1 ;;
    --skip-lint) SKIP_LINT=1 ;;
    *) echo "Unknown option: $1" >&2; usage; exit 1 ;;
  esac
  shift
done

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
SRC_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"                 # LZ-DEV
DST_ROOT="$(cd "$SRC_ROOT/.." && pwd)/LandingZone"       # Release folder

clean_mac_junk() {
  # No-op: we no longer delete macOS metadata (.DS_Store/._*) as part of migrate.
  # Removing these automatically has caused friction without material benefit.
  return 0
}

lint_release_tree() {
  local issues=0
  local allowed=(About Assemblies Defs Languages Patches)
  # Top-level sanity: only allow known mod folders
  while IFS= read -r entry; do
    local base
    base=$(basename "$entry")
    local ok=0
    for a in "${allowed[@]}"; do
      if [[ "$base" == "$a" ]]; then ok=1; break; fi
    done
    if [[ $ok -eq 0 ]]; then
      echo "Unexpected top-level item in LandingZone: $base" >&2
      issues=1
    fi
  done < <(find "$DST_ROOT" -mindepth 1 -maxdepth 1)

  # Explicitly ban dev/ops files
  local banned=("AGENTS.md" "CLAUDE.md" "README.md" "VERSIONING.md" "tasks.json" "scripts" "Source" "docs")
  for b in "${banned[@]}"; do
    if find "$DST_ROOT" -name "$b" | grep -q .; then
      echo "Banned file/folder present in LandingZone: $b" >&2
      issues=1
    fi
  done

  if [[ $issues -ne 0 ]]; then
    echo "Lint failed: clean LandingZone before upload." >&2
    exit 1
  fi
}

lint_translations() {
  python3 - <<'PY'
import os, re, sys, xml.etree.ElementTree as ET

root = os.path.abspath(os.environ.get('LZ_ROOT', os.getcwd()))
lang_file = os.path.join(root, 'Languages', 'English', 'Keyed', 'LandingZone.xml')

try:
    tree = ET.parse(lang_file)
    keys = {el.tag for el in tree.getroot()}
except Exception as e:
    print(f"Failed to parse translations: {e}", file=sys.stderr)
    sys.exit(1)

used = set()
translate_re = re.compile(r'Translate\(\s*"([A-Za-z0-9_\.]+)"')
for dirpath, _, files in os.walk(os.path.join(root, 'Source')):
    for f in files:
        if not f.endswith('.cs'):
            continue
        with open(os.path.join(dirpath, f), encoding='utf-8', errors='ignore') as fh:
            for line in fh:
                for m in translate_re.finditer(line):
                    used.add(m.group(1))

missing = sorted(k for k in used if k not in keys)
if missing:
    print("Missing translation keys (used in code but not in Languages/English/Keyed/LandingZone.xml):", file=sys.stderr)
    for k in missing:
        print(f"  - {k}", file=sys.stderr)
    sys.exit(1)

# Hardcoded UI string check: crude scan for Label/ButtonText with raw literals (UI folder only)
ui_dir = os.path.join(root, 'Source', 'Core', 'UI')
hardcoded = []
pattern = re.compile(r'(Label|ButtonText)\(\s*"([^"]*[A-Za-z][^"]*)"')
for dirpath, _, files in os.walk(ui_dir):
    for f in files:
        if not f.endswith('.cs'):
            continue
        path = os.path.join(dirpath, f)
        with open(path, encoding='utf-8', errors='ignore') as fh:
            for i, line in enumerate(fh, 1):
                m = pattern.search(line)
                if m and 'Translate(' not in line:
                    hardcoded.append(f"{os.path.relpath(path, root)}:{i}: {m.group(0).strip()}")

if hardcoded:
    print("Hardcoded UI strings detected (use Translate()):", file=sys.stderr)
    for h in hardcoded:
        print(f"  - {h}", file=sys.stderr)
    sys.exit(1)

PY
}

# Cleanup and lint before proceeding
clean_mac_junk "$SRC_ROOT"
clean_mac_junk "$DST_ROOT"
if [[ $SKIP_LINT -eq 0 ]]; then
  lint_translations
fi

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
  if [[ "$cmp" -lt 0 ]]; then
    echo "Refusing to migrate: DEV version ($DEV_VERSION) is older than LandingZone version ($DST_VERSION)." >&2
    exit 1
  fi
fi

RSYNC_FLAGS=(-avh --delete)
RSYNC_FLAGS+=(--no-times --omit-dir-times --no-perms)
if [[ $APPLY -eq 0 ]]; then
  echo "Dry run (pass --apply to execute)"
  RSYNC_FLAGS+=(--dry-run)
fi

# Cleanup and lint before proceeding
clean_mac_junk "$SRC_ROOT"
clean_mac_junk "$DST_ROOT"
if [[ $SKIP_LINT -eq 0 ]]; then
  lint_translations
fi

clean_mac_junk() {
  local target="$1"
  local junk
  junk=$(find "$target" -name '.DS_Store' -o -name '._*' || true)
  if [[ -n "$junk" ]]; then
    if [[ $APPLY -eq 1 ]]; then
      echo "Removing macOS metadata files from $(basename "$target")"
      # shellcheck disable=SC2086
      find "$target" -name '.DS_Store' -o -name '._*' -delete || true
    else
      echo "macOS metadata files present (dry run):"
      echo "$junk"
    fi
  fi
}

lint_release_tree() {
  local issues=0
  local allowed=(About Assemblies Defs Languages Patches)
  # Top-level sanity: only allow known mod folders
  while IFS= read -r entry; do
    local base
    base=$(basename "$entry")
    local ok=0
    for a in "${allowed[@]}"; do
      if [[ "$base" == "$a" ]]; then ok=1; break; fi
    done
    if [[ $ok -eq 0 ]]; then
      echo "Unexpected top-level item in LandingZone: $base" >&2
      issues=1
    fi
  done < <(find "$DST_ROOT" -mindepth 1 -maxdepth 1)

  # Explicitly ban dev/ops files
  local banned=("AGENTS.md" "CLAUDE.md" "README.md" "VERSIONING.md" "tasks.json" "scripts" "Source" "docs")
  for b in "${banned[@]}"; do
    if find "$DST_ROOT" -name "$b" | grep -q .; then
      echo "Banned file/folder present in LandingZone: $b" >&2
      issues=1
    fi
  done

  if [[ $issues -ne 0 ]]; then
    echo "Lint failed: clean LandingZone before upload." >&2
    exit 1
  fi
}

lint_translations() {
  python3 - <<'PY'
import os, re, sys, xml.etree.ElementTree as ET

root = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
lang_file = os.path.join(root, 'Languages', 'English', 'Keyed', 'LandingZone.xml')

try:
    tree = ET.parse(lang_file)
    keys = {el.tag for el in tree.getroot()}
except Exception as e:
    print(f"Failed to parse translations: {e}", file=sys.stderr)
    sys.exit(1)

used = set()
translate_re = re.compile(r'Translate\(\s*"([A-Za-z0-9_\.]+)"')
for dirpath, _, files in os.walk(os.path.join(root, 'Source')):
    for f in files:
        if not f.endswith('.cs'):
            continue
        with open(os.path.join(dirpath, f), encoding='utf-8', errors='ignore') as fh:
            for line in fh:
                for m in translate_re.finditer(line):
                    used.add(m.group(1))

missing = sorted(k for k in used if k not in keys)
if missing:
    print("Missing translation keys (used in code but not in Languages/English/Keyed/LandingZone.xml):", file=sys.stderr)
    for k in missing:
        print(f"  - {k}", file=sys.stderr)
    sys.exit(1)

# Hardcoded UI string check: crude scan for Label/ButtonText with raw literals (UI folder only)
ui_dir = os.path.join(root, 'Source', 'Core', 'UI')
hardcoded = []
pattern = re.compile(r'(Label|ButtonText)\(\s*"([^"]*[A-Za-z][^"]*)"')
for dirpath, _, files in os.walk(ui_dir):
    for f in files:
        if not f.endswith('.cs'):
            continue
        path = os.path.join(dirpath, f)
        with open(path, encoding='utf-8', errors='ignore') as fh:
            for i, line in enumerate(fh, 1):
                m = pattern.search(line)
                if m and 'Translate(' not in line:
                    hardcoded.append(f"{os.path.relpath(path, root)}:{i}: {m.group(0).strip()}")

if hardcoded:
    print("Hardcoded UI strings detected (use Translate()):", file=sys.stderr)
    for h in hardcoded:
        print(f"  - {h}", file=sys.stderr)
    sys.exit(1)

PY
}

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

  # Strip dev-only warning block from description in release About.xml
  if [[ -f "$DST_ROOT/About/About.xml" ]]; then
    perl -0pi -e "s#\n\s*\[DEVELOPMENT VERSION - DO NOT USE FOR NORMAL PLAY\]\s+This is the development version of LandingZone with Source/, Managed/, and other dev files. Use the regular \"LandingZone\" mod for normal gameplay\.##s" "$DST_ROOT/About/About.xml"
    # Normalize dependency to Harmony (avoid self-dependency)
    perl -0pi -e "s#<modDependencies>.*?<packageId>[^<]+</packageId>#<modDependencies>\n    <li>\n      <packageId>brrainz.harmony</packageId>#s" "$DST_ROOT/About/About.xml"
  fi
fi

lint_release_tree

lint_translations() {
  python3 - <<'PY'
import os, re, sys, xml.etree.ElementTree as ET

root = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
lang_file = os.path.join(root, 'Languages', 'English', 'Keyed', 'LandingZone.xml')

try:
    tree = ET.parse(lang_file)
    keys = {el.tag for el in tree.getroot()}
except Exception as e:
    print(f"Failed to parse translations: {e}", file=sys.stderr)
    sys.exit(1)

used = set()
translate_re = re.compile(r'Translate\(\s*"([A-Za-z0-9_\.]+)"')
for dirpath, _, files in os.walk(os.path.join(root, 'Source')):
    for f in files:
        if not f.endswith('.cs'):
            continue
        with open(os.path.join(dirpath, f), encoding='utf-8', errors='ignore') as fh:
            for line in fh:
                for m in translate_re.finditer(line):
                    used.add(m.group(1))

missing = sorted(k for k in used if k not in keys)
if missing:
    print("Missing translation keys (used in code but not in Languages/English/Keyed/LandingZone.xml):", file=sys.stderr)
    for k in missing:
        print(f"  - {k}", file=sys.stderr)
    sys.exit(1)

# Hardcoded UI string check: crude scan for Label/ButtonText with raw literals (UI folder only)
ui_dir = os.path.join(root, 'Source', 'Core', 'UI')
hardcoded = []
pattern = re.compile(r'(Label|ButtonText)\(\s*"([^"]*[A-Za-z][^"]*)"')
for dirpath, _, files in os.walk(ui_dir):
    for f in files:
        if not f.endswith('.cs'):
            continue
        path = os.path.join(dirpath, f)
        with open(path, encoding='utf-8', errors='ignore') as fh:
            for i, line in enumerate(fh, 1):
                m = pattern.search(line)
                if m and 'Translate(' not in line:
                    hardcoded.append(f"{os.path.relpath(path, root)}:{i}: {m.group(0).strip()}")

if hardcoded:
    print("Hardcoded UI strings detected (use Translate()):", file=sys.stderr)
    for h in hardcoded:
        print(f"  - {h}", file=sys.stderr)
    sys.exit(1)

PY
}

install_into_app_bundle() {
  local app_mods="/Users/will/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods"
  mkdir -p "$app_mods"
  echo
  echo "Deploying LandingZone into RimWorldMac.app/Mods"
  rsync "${RSYNC_FLAGS[@]}" "$DST_ROOT/" "$app_mods/LandingZone/"
}

if [[ $APPLY -eq 1 && $INSTALL_APP -eq 1 ]]; then
  install_into_app_bundle
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

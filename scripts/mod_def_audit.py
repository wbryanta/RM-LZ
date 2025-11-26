#!/usr/bin/env python3
"""
Audit workshop mods for key def types (BiomeDef, WorldFeatureDef, WorldObjectDef, TerrainDef, natural rocks).
Outputs JSON to stdout.
Usage: python3 scripts/mod_def_audit.py [--mods id1 id2 ...] [--base PATH]
Defaults: base=~/Library/Application Support/Steam/steamapps/workshop/content/294100
"""
import os, re, glob, json, argparse

DEFAULT_BASE = os.path.expanduser('~/Library/Application Support/Steam/steamapps/workshop/content/294100')

TAGS = {
    'BiomeDef': r'<BiomeDef>',
    'WorldFeatureDef': r'<WorldFeatureDef>',
    'WorldObjectDef': r'<WorldObjectDef>',
    'TerrainDef': r'<TerrainDef>',
    'ThingDef': r'<ThingDef',
}

def extract_defnames(text, tag=None, filter_fn=None):
    out = []
    if tag and tag not in text:
        return out
    for block in re.split(r'<', text):
        if 'defName>' in block:
            m = re.search(r'defName>\s*([^<\s]+)', block)
            if m:
                dn = m.group(1)
                if not filter_fn or filter_fn(block):
                    out.append(dn)
    return out

def audit_mod(modpath):
    buckets = {k: set() for k in ['BiomeDef', 'WorldFeatureDef', 'WorldObjectDef', 'TerrainDef', 'NaturalRockThingDef']}
    for xml in glob.glob(os.path.join(modpath, '**/*.xml'), recursive=True):
        try:
            text = open(xml, encoding='utf-8', errors='ignore').read()
        except Exception:
            continue
        if TAGS['BiomeDef'] in text:
            buckets['BiomeDef'].update(extract_defnames(text, 'BiomeDef'))
        if TAGS['WorldFeatureDef'] in text:
            buckets['WorldFeatureDef'].update(extract_defnames(text, 'WorldFeatureDef'))
        if TAGS['WorldObjectDef'] in text:
            buckets['WorldObjectDef'].update(extract_defnames(text, 'WorldObjectDef'))
        if TAGS['TerrainDef'] in text:
            buckets['TerrainDef'].update(extract_defnames(text, 'TerrainDef'))
        if '<isNaturalRock>' in text:
            for block in re.split(r'<ThingDef', text)[1:]:
                if '<isNaturalRock>true' in block:
                    m = re.search(r'<defName>\s*([^<\s]+)', block)
                    if m:
                        buckets['NaturalRockThingDef'].add(m.group(1))
    return {k: sorted(v) for k, v in buckets.items() if v}

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--base', default=DEFAULT_BASE, help='Workshop content base path')
    ap.add_argument('--mods', nargs='*', help='Specific workshop IDs to scan')
    args = ap.parse_args()
    base = os.path.expanduser(args.base)
    if not os.path.isdir(base):
        raise SystemExit(f"Base path not found: {base}")
    mods = args.mods or [m for m in os.listdir(base) if m.isdigit()]
    report = {}
    for mid in sorted(mods):
        modpath = os.path.join(base, mid)
        if not os.path.isdir(modpath):
            continue
        data = audit_mod(modpath)
        if data:
            report[mid] = data
    print(json.dumps(report, indent=2))

if __name__ == '__main__':
    main()

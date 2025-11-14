# Versioning Guide

## Version Format

LandingZone follows [Semantic Versioning 2.0.0](https://semver.org/):

```
major.minor.patch(-prerelease)
```

**Example versions:**
- `0.1.0-beta` - First beta release
- `0.1.1-beta` - Beta with bug fixes
- `0.2.0-beta` - Beta with new features
- `1.0.0` - First stable release
- `1.0.1` - Stable hotfix
- `1.1.0` - Stable with new features

## Version Components

- **Major** (0): Breaking changes, major rewrites
- **Minor** (1): New features, significant improvements
- **Patch** (1): Bug fixes, small improvements
- **Prerelease** (beta): Development stage indicator

## Pre-release Labels

- `-alpha`: Early development, experimental features
- `-beta`: Feature-complete, undergoing testing and polish
- `-rc`: Release candidate, nearly ready for production
- *(none)*: Stable release

## Current Development Phase

**Phase**: Beta (0.1.x-beta)
- Core features implemented (filtering, scoring, UI)
- Stabilizing and polishing for 1.0.0 release
- Bug fixes and UX improvements

## Incrementing Versions

### During Beta (0.1.x-beta)

**Patch increment** when completing:
- Bug fixes
- UI improvements
- Performance optimizations
- Documentation updates

**Minor increment** (0.x.0-beta) when:
- Adding major new features (e.g., bookmark system, advanced mode)
- Significant architecture changes
- New filter types

### Release Schedule

Update version when:
1. Completing a task from `tasks.json`
2. Merging a feature branch
3. Creating a release build

## Single Source of Truth

**`About/About.xml`** is the canonical version location:

```xml
<modVersion>0.1.1-beta</modVersion>
```

**Code reads from About.xml at runtime:**
```csharp
// LandingZoneMod.cs reads version from ModContentPack
Version = content.ModMetaData.ModVersion;
```

**Never hardcode version** in:
- ❌ Source code files (`.cs`)
- ❌ Commit messages (use git tags instead)
- ❌ Documentation (reference About.xml or use `{version}` placeholder)

## Workflow

### 1. Update Version

Edit `About/About.xml`:
```xml
<modVersion>0.1.2-beta</modVersion>
```

### 2. Commit Changes

```bash
git add About/About.xml
git commit -m "chore: bump version to 0.1.2-beta"
```

### 3. Tag Release

```bash
git tag -a v0.1.2-beta -m "Version 0.1.2-beta: UI consolidation and bug fixes"
git push origin v0.1.2-beta
```

### 4. Verify

- Check RimWorld mod list shows correct version
- Check log output: `[LandingZone] LandingZone 0.1.2-beta bootstrapped...`

## Version History

| Version | Date | Description |
|---------|------|-------------|
| 0.1.1-beta | 2025-11-13 | Mod settings (scoring presets, logging levels), UI consolidation, C/F temperature support |
| 0.1.0-beta | 2025-11-XX | First beta: Membership scoring, Basic/Advanced UI, 40+ filters |
| 0.0.3-alpha | 2025-11-XX | Alpha: Core filtering architecture, performance optimizations |

## Future Milestones

- **0.2.0-beta**: Bookmark system, map feature filters
- **0.3.0-beta**: Advanced ranking UI, filter presets
- **0.9.0-rc**: Release candidate testing
- **1.0.0**: First stable release

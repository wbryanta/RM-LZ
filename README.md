# LandingZone

LandingZone is a fresh take on RimWorld's landing-site preparation tools. Instead of forking **Prepare Landing**, we are rebuilding the experience with a cleaner codebase and modern RimWorld 1.5 support. The first milestone is a feature-parity implementation of smart tile filtering, highlighting, and preset management so players can quickly find ideal colony spots without juggling multiple menus.

## Goals
- **Modern architecture** – decouple game data, filtering logic, and UI from day one.
- **Modular filters** – compose biome, terrain, temperature, and world-feature filters without a monolithic class.
- **Responsive UX** – keep the world map interactive while filters run, with clear logging/debug info.
- **Extendable** – make it easy to add new filters, overlays, and presets without Harmony surgery.

## Current Status
- Repository scaffolded with RimWorld mod folders (`About/`, `Assemblies/`, `Defs/`, `Patches/`, `Source/`).
- Documentation folder for design notes and learnings from the original Prepare Landing mod.
- Next step: bring in the initial C# project, copy MIT license, and start codifying the architecture plan.

## Credits
Prepare Landing (neitsa, m00nl1ght, contributors) is licensed under MIT, which allows us to reuse ideas. We'll reference their work where appropriate while writing new code.
# RM-LZ

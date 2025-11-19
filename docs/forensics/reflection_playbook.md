## Reverse Engineering Playbook

This runbook documents the repeatable workflow for inspecting RimWorld’s closed
assembly APIs (e.g., mutator workers) without checking any tooling into the repo.
Codex and DevAgent should reference this whenever the game keeps filter data
behind non-public Reflection gates.

### Why this exists
- MineralRich and Stockpile mutators expose critical data (ore/loot types) only
  via private worker methods (`GetMineableThingDefForTile`, `GetStockpileType`).
- Future features (e.g., animal habitats, plant groves, covert-op stealth
  metrics) rely on the same hidden workers.
- Reusing a documented process avoids ad-hoc reverse engineering and keeps us in
  compliance with the RimWorld EULA (personal-use decompilation is permitted).

### Prerequisites
1. RimWorld installed locally (Steam/Mac build tested).
2. .NET SDK (10.x per repo tooling).
3. The repo itself stays clean: create all reflection helpers under `/tmp/`
   or another throwaway directory. **Do not** commit these helpers.

### TL;DR Workflow
1. Locate `Assembly-CSharp.dll`:
   ```bash
   DLL="$HOME/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Contents/Resources/Data/Managed/Assembly-CSharp.dll"
   ```
2. Scaffold a throwaway console app (outside the repo) and point `DOTNET_CLI_HOME`
   to a temporary directory so the real home folder stays untouched:
   ```bash
   export DOTNET_CLI_HOME=/tmp/dotnet
   mkdir -p /tmp/rw_reflect && cd /tmp/rw_reflect
   dotnet new console -n RWReflector -f net10.0
   ```
3. Replace `Program.cs` with a reflection script (sample below) that:
   - Registers an `AssemblyResolve` handler so Unity’s modules load from the same
     folder as the DLL.
   - Filters for the worker types of interest (e.g., `RimWorld.TileMutatorWorker_*`,
     `RimWorld.Planet.WorldFeatureWorker_*`, etc.).
   - Prints public + non-public methods/fields/properties, including nested enums.
4. Run the tool against the DLL and save the output for analysis:
   ```bash
   cd /tmp/rw_reflect/RWReflector
   dotnet run -c Release -- "$DLL" > /tmp/mutator_dump.txt
   ```
5. Mine the dump for actionable APIs:
   - `GetStockpileType(PlanetTile)` → stockpile loot enumeration.
   - `GetAnimalKind(PlanetTile)` / `AnimalCommonalityFactorFor(...)` →
     flagship animal data.
   - `GetPlantKind(PlanetTile)` / `AdditionalWildPlants(PlanetTile)` →
     high-density plant species.
   - `SecondaryBiome(PlanetTile, PlanetLayer)` → true mixed-biome info.
   - Any other `PlanetTile`/`Map` helper revealing data not exposed elsewhere.
6. Update the relevant caches (e.g., `MineralStockpileCache`) via Harmony’s
   `AccessTools.Method/Field` to call the discovered API from in-game code.
7. Archive findings in the appropriate doc (e.g., `docs/data/filter_variables_catalog.md`)
   and cite the reflection source when filing tasks or instructions.

### Sample Reflection Snippet
```csharp
var asm = Assembly.LoadFile(asmPath);
var baseType = asm.GetType("RimWorld.TileMutatorWorker");
var workers = asm.GetTypes()
    .Where(t => baseType.IsAssignableFrom(t) && !t.IsAbstract && t != baseType);
foreach (var worker in workers)
{
    Console.WriteLine($"=== {worker.FullName} ===");
    foreach (var method in worker.GetMethods(BindingFlags.Instance |
             BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
             BindingFlags.DeclaredOnly))
    {
        Console.WriteLine($"{method.ReturnType.Name} {method.Name}({string.Join(", ",
            method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
    }
}
```

### When to re-run
- New RimWorld release (DLL version bump) – regenerate dumps to catch API shifts.
- Adding any new filter/preset that references a mutator or biome feature.
- Investigating external mods (e.g., CovertOps) that rely on hidden stats like
  stealth or infiltration difficulty.

### Operational Notes
- Keep all reverse-engineering artifacts outside the repo. This ensures
  licensing compliance and avoids polluting Git history.
- When Codex references reflection findings, cite this runbook plus the log
  filename (e.g., `/tmp/mutator_dump.txt`) in analysis notes.
- DevAgent should attach relevant excerpts to commits or docs whenever the
  implementation relies on these hidden APIs, so reviewers have provenance.

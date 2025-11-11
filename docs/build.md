# Building LandingZone

1. Copy RimWorld's managed assemblies into `Managed/` (or symlink the folder). The project currently
   references `Assembly-CSharp.dll`, `UnityEngine.dll`, and `UnityEngine.CoreModule.dll`. (Verse lives
   inside `Assembly-CSharp.dll` on modern RimWorld builds, so no separate DLL is required.)
2. Run the helper script from the repo root:
   ```bash
   python scripts/build.py        # Debug build
   python scripts/build.py -c Release
   ```
   The script runs `dotnet restore` + `dotnet build` under `Source/` and copies the resulting
   `LandingZone.dll` into `Assemblies/` automatically.
3. If you prefer manual control, `cd Source` and run `dotnet restore && dotnet build -c Debug`
   (or Release) then copy `bin/<config>/net472/LandingZone.dll` to `../Assemblies/` yourself.
4. Keep RimWorld's major/minor version in sync with the assemblies you reference; mismatched DLLs will
   trigger type load exceptions at runtime.

## Future Automation
- Add a helper to pull the managed assemblies from a configurable RimWorld install path.
- Wire CI (GitHub Actions) once we can legally cache or stub the dependencies.

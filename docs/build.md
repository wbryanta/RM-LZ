# Building LandingZone

1. Copy RimWorld's managed assemblies into `Managed/` (or symlink the folder). The project looks for
   `Assembly-CSharp.dll`, `Verse.dll`, and `UnityEngine*.dll` there.
2. `cd Source` and run `dotnet build`. The resulting `LandingZone.dll` should be copied to `Assemblies/`.
3. Keep RimWorld's major/minor version in sync with the assemblies you reference; mismatched DLLs will
   trigger type load exceptions at runtime.

## Future Automation
- Add a Cake/PowerShell script to pull the managed assemblies from a configurable path.
- Add a post-build target to drop the compiled DLL straight into `../Assemblies`.
- Wire CI (GitHub Actions) once we can legally cache the public artifacts or stub them.

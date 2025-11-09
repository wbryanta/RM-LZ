using Verse;

namespace LandingZone
{
    /// <summary>
    /// Entry point for the LandingZone RimWorld mod. The actual filtering UI and logic will live in
    /// feature-specific assemblies, but we keep a thin bootstrapper to register services with Harmony
    /// and load user settings.
    /// </summary>
    public class LandingZoneMod : Mod
    {
        public LandingZoneMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<LandingZoneSettings>();
        }

        public static LandingZoneMod Instance { get; private set; }

        public LandingZoneSettings Settings { get; }

        public override string SettingsCategory() => "LandingZone";

        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            // Settings UI placeholder. We'll expose filter defaults and telemetry toggles here later.
        }
    }
}

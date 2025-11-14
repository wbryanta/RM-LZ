using HarmonyLib;
using LandingZone.Core;
using LandingZone.Data;
using UnityEngine;
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
        /// <summary>
        /// Version read from About/About.xml at runtime.
        /// Single source of truth for mod version.
        /// </summary>
        public static string Version { get; private set; } = "unknown";

        private readonly Harmony _harmony;

        public LandingZoneMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<LandingZoneSettings>();

            // Read version from About.xml (single source of truth)
            Version = content.ModMetaData.ModVersion;

            var state = GameStateFactory.CreateDefault();
            _harmony = new Harmony("com.landingzone.mod");
            _harmony.PatchAll();

            LandingZoneContext.Initialize(state, _harmony);
            LongEventHandler.ExecuteWhenFinished(RefreshCaches);
            LandingZoneContext.LogMessage($"LandingZone {Version} bootstrapped and waiting for world data.");
        }

        public static LandingZoneMod Instance { get; private set; } = null!;

        public LandingZoneSettings Settings { get; }

        public override string SettingsCategory() => "LandingZone";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoSettingsWindowContents(inRect);
        }

        public static bool UseFahrenheit => Prefs.TemperatureMode == TemperatureDisplayMode.Fahrenheit;

        private static void RefreshCaches()
        {
            LandingZoneContext.RefreshDefinitions();
            if (Find.World != null)
            {
                LandingZoneContext.RefreshWorldCache(force: true);
                if (Instance.Settings.AutoRunSearchOnWorldLoad)
                {
                    LandingZoneContext.RequestEvaluation(EvaluationRequestSource.Auto, focusOnComplete: false);
                }
                else
                {
                    LandingZoneContext.LogMessage("World snapshot refreshed. Auto-run disabled; launch a search manually when ready.");
                }
            }
            else
            {
                LandingZoneContext.LogMessage("Definitions refreshed. World data not yet available.");
            }
        }
    }
}

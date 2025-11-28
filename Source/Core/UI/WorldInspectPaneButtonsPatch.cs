using HarmonyLib;
using RimWorld.Planet;

namespace LandingZone.Core.UI
{
    /// <summary>
    /// No-op patch. DEV button is shown in the world bottom ribbon only.
    /// See LandingZoneBottomButtonDrawer in SelectStartingSiteButtonsPatch.cs.
    /// </summary>
    [HarmonyPatch(typeof(WorldInspectPane), nameof(WorldInspectPane.DoInspectPaneButtons))]
    internal static class WorldInspectPaneButtonsPatch
    {
        public static void Postfix()
        {
            // Intentionally empty - button removed from inspect pane.
            // DEV tools are accessed via the bottom ribbon [DEV] button.
        }
    }
}

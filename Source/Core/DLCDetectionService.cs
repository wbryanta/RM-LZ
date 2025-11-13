using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace LandingZone.Core
{
    /// <summary>
    /// Service for detecting installed RimWorld DLCs and filtering content by DLC availability.
    /// Used to show/hide DLC-specific features in the UI and apply DLC labels.
    /// </summary>
    public static class DLCDetectionService
    {
        /// <summary>
        /// Checks if Royalty DLC is installed and active.
        /// </summary>
        public static bool IsRoyaltyAvailable => ModsConfig.RoyaltyActive;

        /// <summary>
        /// Checks if Ideology DLC is installed and active.
        /// </summary>
        public static bool IsIdeologyAvailable => ModsConfig.IdeologyActive;

        /// <summary>
        /// Checks if Biotech DLC is installed and active.
        /// </summary>
        public static bool IsBiotechAvailable => ModsConfig.BiotechActive;

        /// <summary>
        /// Checks if Anomaly DLC is installed and active.
        /// </summary>
        public static bool IsAnomalyAvailable => ModsConfig.AnomalyActive;

        /// <summary>
        /// Gets a human-readable label for the DLC (e.g., "Royalty", "Core").
        /// </summary>
        public static string GetDLCLabel(string modContentPackId)
        {
            if (string.IsNullOrEmpty(modContentPackId))
                return "Core";

            if (modContentPackId.IndexOf("Royalty", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Royalty";
            if (modContentPackId.IndexOf("Ideology", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Ideology";
            if (modContentPackId.IndexOf("Biotech", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Biotech";
            if (modContentPackId.IndexOf("Anomaly", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Anomaly";

            // Fallback for other mods
            return modContentPackId;
        }

        /// <summary>
        /// Gets a human-readable label for the DLC based on the Def's mod content pack.
        /// </summary>
        public static string GetDLCLabel(Def def)
        {
            if (def?.modContentPack == null)
                return "Core";

            return GetDLCLabel(def.modContentPack.PackageId);
        }

        /// <summary>
        /// Checks if a specific DLC is available based on its label.
        /// </summary>
        public static bool IsDLCAvailable(string dlcLabel)
        {
            return dlcLabel.ToLowerInvariant() switch
            {
                "core" => true,
                "royalty" => IsRoyaltyAvailable,
                "ideology" => IsIdeologyAvailable,
                "biotech" => IsBiotechAvailable,
                "anomaly" => IsAnomalyAvailable,
                _ => true // Unknown DLCs are assumed available (could be other mods)
            };
        }

        /// <summary>
        /// Filters a list of defs to only include those from available DLCs.
        /// </summary>
        public static IEnumerable<T> FilterByDLC<T>(IEnumerable<T> defs) where T : Def
        {
            foreach (var def in defs)
            {
                string dlcLabel = GetDLCLabel(def);
                if (IsDLCAvailable(dlcLabel))
                {
                    yield return def;
                }
            }
        }

        /// <summary>
        /// Gets a display name for a def with DLC label prefix if it's from a DLC.
        /// Example: "(Anomaly) Ancient Heat Vent" or "Granite" for core content.
        /// </summary>
        public static string GetDefDisplayName(Def def, bool showCoreLabel = false)
        {
            if (def == null)
                return "Unknown";

            string dlcLabel = GetDLCLabel(def);
            bool isCore = dlcLabel == "Core";

            if (isCore && !showCoreLabel)
                return def.LabelCap.ToString();

            return $"({dlcLabel}) {def.LabelCap}";
        }

        /// <summary>
        /// Gets a summary of installed DLCs for debugging/logging.
        /// </summary>
        public static string GetInstalledDLCSummary()
        {
            var installed = new List<string> { "Core" };
            if (IsRoyaltyAvailable) installed.Add("Royalty");
            if (IsIdeologyAvailable) installed.Add("Ideology");
            if (IsBiotechAvailable) installed.Add("Biotech");
            if (IsAnomalyAvailable) installed.Add("Anomaly");

            return string.Join(", ", installed);
        }
    }
}

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

        // ===== MOD DETECTION =====

        /// <summary>
        /// Cache for mod name checks to avoid repeated lookups.
        /// </summary>
        private static Dictionary<string, bool>? _modActiveCache;

        /// <summary>
        /// Checks if a mod is active based on name substring or known package IDs.
        /// Supports common mod naming variations.
        /// </summary>
        public static bool IsModActive(string modName)
        {
            if (string.IsNullOrEmpty(modName))
                return true; // No requirement = always available

            // Initialize cache on first use
            _modActiveCache ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            // Check cache first
            if (_modActiveCache.TryGetValue(modName, out bool cached))
                return cached;

            bool isActive = CheckModActive(modName);
            _modActiveCache[modName] = isActive;
            return isActive;
        }

        private static bool CheckModActive(string modName)
        {
            // Normalize common abbreviations
            string normalizedName = modName.ToLowerInvariant();

            // Special handling for known mods
            if (normalizedName.Contains("geo") && normalizedName.Contains("landform"))
            {
                // Check for Geological Landforms by package ID
                return ModLister.GetActiveModWithIdentifier("m00nl1ght.GeologicalLandforms") != null;
            }

            if (normalizedName.Contains("alpha") && normalizedName.Contains("biome"))
            {
                return ModLister.GetActiveModWithIdentifier("sarg.alphabiomes") != null;
            }

            if (normalizedName.Contains("biomes") && normalizedName.Contains("core"))
            {
                return ModLister.GetActiveModWithIdentifier("BiomesTeam.BiomesCore") != null;
            }

            // Generic fallback: search by name substring
            foreach (var mod in ModsConfig.ActiveModsInLoadOrder)
            {
                if (mod.Name != null && mod.Name.IndexOf(modName, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Clears the mod active cache (call when mod list might have changed).
        /// </summary>
        public static void ClearModCache()
        {
            _modActiveCache = null;
        }
    }
}

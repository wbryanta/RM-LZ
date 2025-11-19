using LandingZone;
using Verse;

namespace LandingZone.Core
{
    /// <summary>
    /// Centralizes logging decisions based on the configured verbosity level.
    /// Prevents runaway log spam while still allowing targeted diagnostics.
    /// Tiers: Minimal (start/end only) | Standard (summaries + top-3 dump) | Verbose (everything)
    /// </summary>
    public static class LandingZoneLogger
    {
        public static bool IsVerbose => LandingZoneSettings.LogLevel == LoggingLevel.Verbose;
        public static bool IsStandardOrVerbose => LandingZoneSettings.LogLevel == LoggingLevel.Standard ||
                                                   LandingZoneSettings.LogLevel == LoggingLevel.Verbose;
        public static bool IsMinimal => LandingZoneSettings.LogLevel == LoggingLevel.Minimal;

        /// <summary>
        /// Logs only in Verbose mode. Use for deep diagnostics: progress ticks, stack traces, per-tile logs.
        /// </summary>
        public static void LogVerbose(string message)
        {
            if (IsVerbose)
            {
                Log.Message(message);
            }
        }

        /// <summary>
        /// Logs in Standard and Verbose modes. Use for phase summaries, filter dumps, candidate counts.
        /// </summary>
        public static void LogStandard(string message)
        {
            if (IsStandardOrVerbose)
            {
                Log.Message(message);
            }
        }

        /// <summary>
        /// Logs in all modes (Minimal, Standard, Verbose). Use for start/complete lines only.
        /// </summary>
        public static void LogMinimal(string message)
        {
            Log.Message(message);
        }

        public static void LogWarning(string message)
        {
            Log.Warning(message);
        }

        public static void LogError(string message)
        {
            Log.Error(message);
        }
    }
}

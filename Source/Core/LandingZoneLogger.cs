using LandingZone;
using Verse;

namespace LandingZone.Core
{
    /// <summary>
    /// Centralizes logging decisions based on the configured verbosity level.
    /// Prevents runaway log spam while still allowing targeted diagnostics.
    /// </summary>
    public static class LandingZoneLogger
    {
        public static bool IsVerbose => LandingZoneSettings.LogLevel == LoggingLevel.Verbose;
        public static bool IsStandardOrVerbose => LandingZoneSettings.LogLevel != LoggingLevel.Brief;

        public static void LogVerbose(string message)
        {
            if (IsVerbose)
            {
                Log.Message(message);
            }
        }

        public static void LogStandard(string message)
        {
            if (IsStandardOrVerbose)
            {
                Log.Message(message);
            }
        }

        public static void LogBrief(string message)
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

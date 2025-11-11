using Verse;

namespace LandingZone.Core.UI
{
    internal static class LandingZoneResultsController
    {
        private static LandingZoneResultsWindow? _window;

        public static void Toggle()
        {
            if (_window != null && Find.WindowStack.IsOpen<LandingZoneResultsWindow>())
            {
                Find.WindowStack.TryRemove(_window, true);
                _window = null;
                return;
            }

            _window = new LandingZoneResultsWindow();
            Find.WindowStack.Add(_window);
        }

        public static void NotifyClosed(LandingZoneResultsWindow window)
        {
            if (_window == window)
                _window = null;
        }
    }
}

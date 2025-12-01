namespace LandingZone.Core
{
    public enum EvaluationRequestSource
    {
        Legacy,
        Auto,
        Preferences,
        ShowBestSites,
        ResultsWindow,
        Manual,
        /// <summary>
        /// Relaxed search: MustHave gates are demoted to Priority for car-builder fallback.
        /// </summary>
        RelaxedSearch
    }
}

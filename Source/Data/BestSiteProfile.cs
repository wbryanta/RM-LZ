using Verse;

namespace LandingZone.Data
{
    /// <summary>
    /// Defines the scoring profile used when highlighting "best" tiles.
    /// </summary>
    public sealed class BestSiteProfile
    {
        public bool RequireCoastal { get; set; }
        public bool RequireRiver { get; set; }
        public bool RequireFeature { get; set; }
        public bool PreferCoastal { get; set; }
        public bool PreferRiver { get; set; }
        public FloatRange PreferredTemperature { get; set; } = new FloatRange(10f, 26f);
        public FloatRange PreferredRainfall { get; set; } = new FloatRange(800f, 2000f);

        public static BestSiteProfile CreateDefault() => new BestSiteProfile();
    }
}

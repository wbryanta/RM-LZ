using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Data
{
    /// <summary>
    /// Holds cached references to common RimWorld definitions so we can query them quickly.
    /// </summary>
    public sealed class DefCache
    {
        public List<BiomeDef> Biomes { get; } = new List<BiomeDef>();
        public List<RiverDef> Rivers { get; } = new List<RiverDef>();
        public List<RoadDef> Roads { get; } = new List<RoadDef>();
        public List<FeatureDef> WorldFeatures { get; } = new List<FeatureDef>();

        public void Refresh()
        {
            Biomes.Clear();
            Biomes.AddRange(DefDatabase<BiomeDef>.AllDefsListForReading);

            Rivers.Clear();
            Rivers.AddRange(DefDatabase<RiverDef>.AllDefsListForReading);

            Roads.Clear();
            Roads.AddRange(DefDatabase<RoadDef>.AllDefsListForReading);

            WorldFeatures.Clear();
            WorldFeatures.AddRange(DefDatabase<FeatureDef>.AllDefsListForReading);
        }
    }
}

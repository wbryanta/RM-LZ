using UnityEngine;
using Verse;

namespace LandingZone
{
    public class LandingZoneSettings : ModSettings
    {
        public bool AutoRunSearchOnWorldLoad = false;
        public int EvaluationChunkSize = 250;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref AutoRunSearchOnWorldLoad, "AutoRunSearchOnWorldLoad", true);
            Scribe_Values.Look(ref EvaluationChunkSize, "EvaluationChunkSize", 250);
            EvaluationChunkSize = Mathf.Clamp(EvaluationChunkSize, 50, 1000);
        }
    }
}

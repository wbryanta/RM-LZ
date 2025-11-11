using Verse;

namespace LandingZone.Core
{
    public sealed class LandingZoneEvaluationComponent : GameComponent
    {
        public LandingZoneEvaluationComponent(Game game)
        {
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            LandingZoneContext.StepEvaluation();
        }
    }
}

using UnityEngine;
using Verse;

namespace LandingZone.Core
{
    /// <summary>
    /// GameComponent for ticking evaluation during active gameplay.
    /// NOTE: On the world selection screen (Page_SelectStartingSite), evaluation is ticked
    /// via SelectStartingSiteDoWindowContentsPatch instead, since GameComponent doesn't run there.
    /// </summary>
    public sealed class LandingZoneEvaluationComponent : GameComponent
    {
        public LandingZoneEvaluationComponent(Game game)
        {
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            // Tick evaluation for in-game searches (e.g., when colonists are looking for new sites)
            LandingZoneContext.StepEvaluation();
        }
    }
}

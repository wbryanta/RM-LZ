using Verse;

namespace LandingZone.Core
{
    [StaticConstructorOnStartup]
    internal static class LandingZoneBootstrap
    {
        static LandingZoneBootstrap()
        {
            LongEventHandler.ExecuteWhenFinished(EnsureGameComponent);
        }

        private static void EnsureGameComponent()
        {
            if (Current.Game == null)
                return;

            if (Current.Game.GetComponent<LandingZoneEvaluationComponent>() == null)
            {
                var component = new LandingZoneEvaluationComponent(Current.Game);
                Current.Game.components.Add(component);
                LandingZoneContext.LogMessage("Attached LandingZoneEvaluationComponent to game.");
            }
        }
    }
}

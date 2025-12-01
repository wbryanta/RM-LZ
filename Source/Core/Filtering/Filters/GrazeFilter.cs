using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by whether animals can graze now.
    /// Uses tri-state logic: On (must support grazing), Off (ignored), Partial (preferred).
    /// Uses RimWorld's VirtualPlantsUtility - extremely cheap (0.0003ms/tile).
    /// </summary>
    public sealed class GrazeFilter : ISiteFilter
    {
        public string Id => "graze";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;  // Changed from Heavy - VirtualPlantsUtility is cached

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            var importance = filters.GrazeImportance;

            // Only hard gates (MustHave/MustNotHave) filter in Apply phase
            if (!importance.IsHardGate())
                return inputTiles;

            return inputTiles.Where(id =>
            {
                bool canGraze = TileCanGrazeNow(id);
                return importance == FilterImportance.MustNotHave ? !canGraze : canGraze;
            });
        }

        public string Describe(FilterContext context)
        {
            var filters = context.Filters;
            return filters.GrazeImportance switch
            {
                FilterImportance.Ignored => "Grazing not considered",
                FilterImportance.Critical => "Must support grazing now",
                FilterImportance.Preferred => "Grazing preferred",
                _ => "Any"
            };
        }

        /// <summary>
        /// Checks if animals can graze on this tile in the current season.
        /// This is an expensive check that evaluates virtual plant availability.
        /// </summary>
        private static bool TileCanGrazeNow(int tileId)
        {
            var world = Find.World;
            if (world?.grid == null)
                return false;

            var planetTile = new PlanetTile(tileId, world.grid.Surface);
            return VirtualPlantsUtility.EnvironmentAllowsEatingVirtualPlantsNowAt(planetTile);
        }

        public float Membership(int tileId, FilterContext context)
        {
            // Direct check - VirtualPlantsUtility is extremely fast (0.0003ms/tile)
            bool canGraze = TileCanGrazeNow(tileId);
            return MembershipFunctions.Binary(canGraze);
        }
    }
}

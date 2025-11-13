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
    /// </summary>
    public sealed class GrazeFilter : ISiteFilter
    {
        public string Id => "graze";
        public FilterHeaviness Heaviness => FilterHeaviness.Heavy;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.State.Preferences.Filters;
            if (filters.GrazeImportance == FilterImportance.Ignored)
                return inputTiles;

            // K-of-N architecture: Apply() only filters for Critical.
            // Preferred is handled by scoring phase.
            if (filters.GrazeImportance != FilterImportance.Critical)
                return inputTiles;

            return inputTiles.Where(id => TileCanGrazeNow(id));
        }

        public string Describe(FilterContext context)
        {
            var filters = context.State.Preferences.Filters;
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
    }
}

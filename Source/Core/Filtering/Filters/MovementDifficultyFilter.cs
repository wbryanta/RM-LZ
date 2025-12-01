using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by movement difficulty (terrain traversal cost).
    /// Lower values = easier travel, higher values = difficult terrain (swamps, mountains).
    /// Uses RimWorld's pre-cached WorldPathGrid.layerMovementDifficulty for instant O(1) access.
    /// </summary>
    public sealed class MovementDifficultyFilter : ISiteFilter
    {
        public string Id => "movement_difficulty";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;  // Changed from Heavy - uses RimWorld's cache

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            var importance = filters.MovementDifficultyImportance;

            // Only hard gates (MustHave/MustNotHave) filter in Apply phase
            if (!importance.IsHardGate())
                return inputTiles;

            var range = filters.MovementDifficultyRange;
            var world = Find.World;
            var pathGrid = world.pathGrid;
            var worldGrid = world.grid;

            return inputTiles.Where(id =>
            {
                // Direct access to RimWorld's pre-cached movement difficulty array (O(1))
                var planetTile = new PlanetTile(id, worldGrid.Surface);
                var movementDiff = pathGrid.PerceivedMovementDifficultyAt(planetTile);
                bool inRange = movementDiff >= range.min && movementDiff <= range.max;
                return importance == FilterImportance.MustNotHave ? !inRange : inRange;
            });
        }

        public string Describe(FilterContext context)
        {
            var filters = context.Filters;
            if (filters.MovementDifficultyImportance == FilterImportance.Ignored)
                return "Any movement difficulty";

            var range = filters.MovementDifficultyRange;
            string importanceLabel = filters.MovementDifficultyImportance == FilterImportance.Critical ? " (required)" : " (preferred)";
            return $"Movement difficulty {range.min:F1} - {range.max:F1}{importanceLabel}";
        }

        public float Membership(int tileId, FilterContext context)
        {
            var range = context.Filters.MovementDifficultyRange;

            // Direct access to RimWorld's pre-cached movement difficulty array (O(1))
            var world = Find.World;
            var planetTile = new PlanetTile(tileId, world.grid.Surface);
            var movementDiff = world.pathGrid.PerceivedMovementDifficultyAt(planetTile);

            return MembershipFunctions.Trapezoid(movementDiff, range.min, range.max);
        }
    }
}

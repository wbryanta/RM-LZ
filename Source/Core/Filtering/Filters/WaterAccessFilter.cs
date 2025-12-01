using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Lightweight helper filter for water access (coastal OR any river).
    /// Created specifically to support symmetric water requirements for Aquatic/Desert Oasis presets.
    /// </summary>
    public class WaterAccessFilter : ISiteFilter
    {
        public string Id => "water_access";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;

        public string Describe(FilterContext context)
        {
            var importance = context.Filters.WaterAccessImportance;
            return importance switch
            {
                FilterImportance.Critical => "Water access required (coastal OR river)",
                FilterImportance.Preferred => "Water access preferred",
                _ => "Water access ignored"
            };
        }

        public float Membership(int tileId, FilterContext context)
        {
            var world = Find.World;
            if (world?.grid == null) return 0.0f;

            var tile = world.grid[tileId];
            if (tile == null) return 0.0f;

            // Check coastal access
            if (world.CoastDirectionAt(tileId).IsValid)
                return 1.0f; // Has coastal access

            // Check river access
            if (tile.Rivers != null && tile.Rivers.Count > 0)
                return 1.0f; // Has at least one river

            return 0.0f; // No water access
        }

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var importance = context.State.Preferences.GetActiveFilters().WaterAccessImportance;

            // Only hard gates (MustHave/MustNotHave) filter in Apply phase
            if (!importance.IsHardGate())
                return inputTiles;

            var world = Find.World;
            if (world?.grid == null)
                return inputTiles;

            return inputTiles.Where(tileId =>
            {
                var tile = world.grid[tileId];

                // Check coastal access
                bool hasCoastal = world.CoastDirectionAt(tileId).IsValid;

                // Check river access
                bool hasRiver = tile?.Rivers != null && tile.Rivers.Count > 0;

                bool hasWaterAccess = hasCoastal || hasRiver;
                return importance == FilterImportance.MustNotHave ? !hasWaterAccess : hasWaterAccess;
            });
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LandingZone.Core.Filtering.Filters
{
    /// <summary>
    /// Filters tiles by whether they have a proper named landmark.
    /// Landmarks are world features with specific names (e.g., "Mount Erebus", "Lake Victoria").
    /// Uses Preferred/Critical importance.
    /// </summary>
    public sealed class LandmarkFilter : ISiteFilter
    {
        public string Id => "landmark";
        public FilterHeaviness Heaviness => FilterHeaviness.Light;

        public IEnumerable<int> Apply(FilterContext context, IEnumerable<int> inputTiles)
        {
            var filters = context.Filters;
            var importance = filters.LandmarkImportance;

            Log.Message($"[LandingZone] LandmarkFilter.Apply: importance={importance}");

            if (importance == FilterImportance.Ignored)
                return inputTiles;

            // K-of-N architecture: Apply() only filters for Critical.
            // Preferred is handled by scoring phase.
            if (importance != FilterImportance.Critical)
            {
                Log.Message($"[LandingZone] LandmarkFilter: Preferred importance, passing all tiles through to scoring");
                return inputTiles;
            }

            var inputList = inputTiles.ToList();
            Log.Message($"[LandingZone] LandmarkFilter: input tiles: {inputList.Count}");

            // Build landmark tile lookup once for efficiency
            var landmarkTiles = BuildLandmarkTileLookup();
            var result = inputList.Where(id => landmarkTiles.Contains(id)).ToList();

            Log.Message($"[LandingZone] LandmarkFilter: Filtered {inputList.Count} -> {result.Count} tiles");
            return result;
        }

        public string Describe(FilterContext context)
        {
            var filters = context.Filters;
            var importance = filters.LandmarkImportance;

            return importance switch
            {
                FilterImportance.Ignored => "Landmarks not considered",
                FilterImportance.Critical => "Must have landmark",
                FilterImportance.Preferred => "Landmarks preferred",
                _ => "Any"
            };
        }

        /// <summary>
        /// Builds a HashSet of all tile IDs that have individual tile landmarks.
        /// Landmarks are distinct from WorldFeatures - they're single-tile markers like "Ruined Coni", "Black Ghost Town", etc.
        /// This is done once per filter application for efficiency.
        /// </summary>
        private static HashSet<int> BuildLandmarkTileLookup()
        {
            var landmarkTiles = new HashSet<int>();
            var world = Find.World;

            if (world?.grid == null)
            {
                Log.Warning("[LandingZone] LandmarkFilter: Find.World.grid is null");
                return landmarkTiles;
            }

            int tilesWithLandmarks = 0;
            int tilesChecked = 0;
            int reflectionErrors = 0;

            // Check each tile for a Landmark property
            for (int tileId = 0; tileId < world.grid.TilesCount; tileId++)
            {
                var tile = world.grid[tileId];
                if (tile == null) continue;

                tilesChecked++;

                // Access the Landmark property via reflection (RimWorld 1.6+)
                try
                {
                    var landmarkProp = tile.GetType().GetProperty("Landmark");
                    if (landmarkProp != null)
                    {
                        var landmark = landmarkProp.GetValue(tile);
                        if (landmark != null)
                        {
                            // Try multiple name properties
                            string landmarkName = null;

                            // Try "name" property
                            var nameProp = landmark.GetType().GetProperty("name");
                            if (nameProp != null)
                            {
                                landmarkName = nameProp.GetValue(landmark) as string;
                            }

                            // Try "Name" property (capital N)
                            if (string.IsNullOrEmpty(landmarkName))
                            {
                                var nameCapProp = landmark.GetType().GetProperty("Name");
                                if (nameCapProp != null)
                                {
                                    landmarkName = nameCapProp.GetValue(landmark) as string;
                                }
                            }

                            // Try "label" property
                            if (string.IsNullOrEmpty(landmarkName))
                            {
                                var labelProp = landmark.GetType().GetProperty("label");
                                if (labelProp != null)
                                {
                                    landmarkName = labelProp.GetValue(landmark) as string;
                                }
                            }

                            if (!string.IsNullOrEmpty(landmarkName))
                            {
                                landmarkTiles.Add(tileId);
                                tilesWithLandmarks++;

                                // Log first few for debugging
                                if (tilesWithLandmarks <= 5)
                                {
                                    Log.Message($"[LandingZone] LandmarkFilter: Found landmark at tile {tileId}: '{landmarkName}'");
                                }
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    reflectionErrors++;
                    if (reflectionErrors <= 3)
                    {
                        Log.Warning($"[LandingZone] LandmarkFilter: Reflection error on tile {tileId}: {ex.Message}");
                    }
                }
            }

            Log.Message($"[LandingZone] LandmarkFilter: Checked {tilesChecked} tiles, found {tilesWithLandmarks} with landmarks, {reflectionErrors} errors");
            return landmarkTiles;
        }

        public float Membership(int tileId, FilterContext context)
        {
            var tile = Find.World.grid[tileId];
            if (tile == null) return 0.0f;

            // Binary membership: 1.0 if has landmark, 0.0 if not
            bool hasLandmark = false;

            try
            {
                var landmarkProp = tile.GetType().GetProperty("Landmark");
                if (landmarkProp != null)
                {
                    var landmark = landmarkProp.GetValue(tile);
                    hasLandmark = landmark != null;
                }
            }
            catch
            {
                // If reflection fails, no landmark
            }

            return MembershipFunctions.Binary(hasLandmark);
        }
    }
}

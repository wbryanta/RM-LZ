using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using LandingZone.Core.Filtering;
using RimWorld.Planet;
using Verse;

namespace LandingZone.Core.Highlighting
{
    public sealed class HighlightState
    {
        private bool _showBestSites;
        private readonly List<TileScore> _topMatches = new List<TileScore>();

        public HighlightLayer BestSitesLayer { get; } = new HighlightLayer();
        public IReadOnlyList<TileScore> TopMatches => _topMatches;

        public bool ShowBestSites
        {
            get => _showBestSites;
            set
            {
                if (_showBestSites == value)
                    return;

                _showBestSites = value;
                MarkLayerDirty();
            }
        }

        public void Update(IReadOnlyList<TileColor> colors, IReadOnlyList<TileScore> topMatches)
        {
            BestSitesLayer.Update(colors);
            _topMatches.Clear();
            _topMatches.AddRange(topMatches);
            MarkLayerDirty();
        }

        private static void MarkLayerDirty()
        {
            var world = Find.World;
            if (world == null)
                return;

            var renderer = world.renderer;
            var grid = world.grid;
            if (renderer == null || grid == null)
                return;

            if (TryGetOverlayLayer(grid, out var overlay))
            {
                overlay.SetDirty();
            }
        }

        private static readonly System.Reflection.FieldInfo? GlobalLayersField = AccessTools.Field(typeof(WorldGrid), "globalLayers");

        private static bool TryGetOverlayLayer(WorldGrid grid, out WorldDrawLayer_LandingZoneBestSites overlay)
        {
            overlay = null!;
            foreach (var layer in grid.GlobalLayers)
            {
                if (layer is WorldDrawLayer_LandingZoneBestSites existing)
                {
                    overlay = existing;
                    return true;
                }
            }

            if (GlobalLayersField?.GetValue(grid) is not IList rawList)
                return false;

            overlay = new WorldDrawLayer_LandingZoneBestSites();
            rawList.Add(overlay);
            return true;
        }
    }
}

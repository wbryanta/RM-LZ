#nullable enable
using System.Collections.Generic;
using LandingZone.Data;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LandingZone.Core
{
    /// <summary>
    /// Renders bookmark markers on the world map.
    /// Shows colored POI markers (teardrop/pin style) for bookmarked tiles.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class WorldLayerBookmarks
    {
        private static Material? _bookmarkMaterial;
        private static Mesh? _cachedMesh;
        private static bool _meshDirty = true;
        private static float _lastAltitude = -1f;
        private static bool _hasLoggedDraw = false;

        static WorldLayerBookmarks()
        {
            // Initialize material on main thread using LongEventHandler
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                try
                {
                    // Clone RimWorld's SelectedTile material - guaranteed to work with world rendering
                    _bookmarkMaterial = new Material(WorldMaterials.SelectedTile)
                    {
                        color = Color.white
                    };
                    _bookmarkMaterial.renderQueue = 3410; // Render after selected tile (3400) but before UI
                }
                catch (System.Exception ex)
                {
                    Log.Error($"[LandingZone] Exception creating bookmark material in static constructor: {ex}");
                }
            });
        }

        private const float BaseMarkerSize = 0.5f; // Base size of the marker in world units
        private const float MarkerHeight = 0.015f; // Height above world surface
        private const float MinZoomScale = 0.4f; // Minimum scale when zoomed out
        private const float MaxZoomScale = 1.5f; // Maximum scale when zoomed in
        private const float MinAltitude = 80f; // Camera altitude for max zoom in (markers largest)
        private const float MaxAltitude = 250f; // Camera altitude for max zoom out (markers smallest)
        private const float AltitudeChangeThreshold = 5f; // Only regenerate mesh if altitude changes by this much

        /// <summary>
        /// Gets the bookmark material initialized in the static constructor.
        /// </summary>
        private static Material? BookmarkMaterial => _bookmarkMaterial;

        /// <summary>
        /// Called when bookmarks change to mark mesh as needing regeneration.
        /// </summary>
        public static void MarkDirty()
        {
            _meshDirty = true;
        }

        /// <summary>
        /// Draws all bookmark markers on the world map.
        /// Called from WorldRenderer patch.
        /// </summary>
        public static void Draw()
        {
            var manager = BookmarkManager.Get();
            if (manager == null)
            {
                Log.Warning("[LandingZone] BookmarkManager.Get() returned null in WorldLayerBookmarks.Draw()");
                return;
            }

            if (manager.Bookmarks.Count == 0)
            {
                // No bookmarks to draw - this is normal
                return;
            }

            var worldGrid = Find.WorldGrid;
            if (worldGrid == null)
            {
                Log.Warning("[LandingZone] Find.WorldGrid is null in WorldLayerBookmarks.Draw()");
                return;
            }

            // Check if material initialization succeeded
            var material = BookmarkMaterial;
            if (material == null)
            {
                // Material init failed - only draw labels, skip mesh rendering
                if (!_hasLoggedDraw)
                {
                    Log.Message($"[LandingZone] Bookmark material is null, drawing {manager.Bookmarks.Count} labels only");
                    _hasLoggedDraw = true;
                }
                DrawBookmarkLabels(manager, worldGrid, Find.WorldCameraDriver.altitude);
                return;
            }

            if (!_hasLoggedDraw)
            {
                Log.Message($"[LandingZone] Drawing {manager.Bookmarks.Count} bookmark markers with material: {material.shader.name}");
                _hasLoggedDraw = true;
            }

            // Check if zoom level changed significantly
            float currentAltitude = Find.WorldCameraDriver.altitude;
            bool altitudeChanged = Mathf.Abs(currentAltitude - _lastAltitude) > AltitudeChangeThreshold;

            // Regenerate mesh if needed (bookmarks changed or zoom changed)
            if (_meshDirty || _cachedMesh == null || altitudeChanged)
            {
                RegenerateMesh(manager, worldGrid, currentAltitude);
                _meshDirty = false;
                _lastAltitude = currentAltitude;
            }

            // Draw the mesh
            if (_cachedMesh != null && _cachedMesh.vertexCount > 0)
            {
                Graphics.DrawMesh(_cachedMesh, Matrix4x4.identity, material, 0);
            }

            // NOTE: Labels cannot be drawn here - WorldRenderer.DrawWorldLayers is not in GUI context
            // Labels would need to be drawn in a separate OnGUI patch, but that adds complexity
            // For now, markers alone provide sufficient visual indication
        }

        /// <summary>
        /// Calculates marker scale based on camera altitude (zoom level).
        /// Returns a value between MinZoomScale and MaxZoomScale.
        /// </summary>
        private static float CalculateZoomScale(float altitude)
        {
            // Clamp altitude to our zoom range
            float clampedAltitude = Mathf.Clamp(altitude, MinAltitude, MaxAltitude);

            // Invert: lower altitude (zoomed in) = larger scale
            float t = (clampedAltitude - MinAltitude) / (MaxAltitude - MinAltitude);
            float scale = Mathf.Lerp(MaxZoomScale, MinZoomScale, t);

            return scale;
        }

        /// <summary>
        /// Draws text labels for bookmarks that have ShowTitleOnGlobe enabled.
        /// Also handles tooltips (notes if title visible, title if title hidden).
        /// </summary>
        private static void DrawBookmarkLabels(BookmarkManager manager, WorldGrid worldGrid, float altitude)
        {
            // Calculate text scale based on zoom
            float zoomScale = CalculateZoomScale(altitude);
            float textScale = Mathf.Lerp(0.7f, 1.2f, zoomScale / MaxZoomScale); // Scale text size with zoom

            // Save current GUI state
            var prevFont = Text.Font;
            var prevAnchor = Text.Anchor;
            var prevColor = GUI.color;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;

            foreach (var bookmark in manager.Bookmarks)
            {
                if (bookmark.TileId < 0 || bookmark.TileId >= worldGrid.TilesCount)
                    continue;

                // Get world position for this tile
                Vector3 tileCenter = worldGrid.GetTileCenter(bookmark.TileId);
                Vector3 screenPos = GenWorldUI.WorldToUIPosition(tileCenter);

                // Offset text above the marker
                float yOffset = -20f * zoomScale; // Offset scales with zoom
                screenPos.y += yOffset;

                // Draw label if ShowTitleOnGlobe is enabled and label is not empty
                if (bookmark.ShowTitleOnGlobe && !string.IsNullOrWhiteSpace(bookmark.Label))
                {
                    // Calculate label rect
                    float labelWidth = Text.CalcSize(bookmark.Label).x * textScale;
                    float labelHeight = 20f * textScale;
                    Rect labelRect = new Rect(screenPos.x - labelWidth / 2f, screenPos.y - labelHeight / 2f, labelWidth, labelHeight);

                    // Draw text with outline for visibility
                    GUI.color = Color.black;
                    Widgets.Label(labelRect.ExpandedBy(1f), bookmark.Label);
                    GUI.color = bookmark.MarkerColor;
                    Widgets.Label(labelRect, bookmark.Label);

                    // Tooltip: Show notes if available
                    if (!string.IsNullOrWhiteSpace(bookmark.Notes))
                    {
                        TooltipHandler.TipRegion(labelRect, bookmark.Notes);
                    }
                }
                else
                {
                    // Title not shown on globe - create invisible tooltip region over marker
                    float markerSize = BaseMarkerSize * zoomScale * 25f; // Convert to screen space
                    Rect markerRect = new Rect(screenPos.x - markerSize / 2f, screenPos.y - markerSize / 2f, markerSize, markerSize);

                    // Tooltip: Show title (or coordinates if no title)
                    string tooltipText = !string.IsNullOrWhiteSpace(bookmark.Label)
                        ? bookmark.Label
                        : $"Tile {bookmark.TileId}";

                    TooltipHandler.TipRegion(markerRect, tooltipText);
                }
            }

            // Restore GUI state
            GUI.color = prevColor;
            Text.Anchor = prevAnchor;
            Text.Font = prevFont;
        }

        private static void RegenerateMesh(BookmarkManager manager, WorldGrid worldGrid, float altitude)
        {
            if (_cachedMesh == null)
            {
                _cachedMesh = new Mesh();
            }
            else
            {
                _cachedMesh.Clear();
            }

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = new List<Color>();

            // Calculate zoom scale for this altitude
            float zoomScale = CalculateZoomScale(altitude);
            float markerSize = BaseMarkerSize * zoomScale;

            foreach (var bookmark in manager.Bookmarks)
            {
                if (bookmark.TileId < 0 || bookmark.TileId >= worldGrid.TilesCount)
                    continue;

                Vector3 tileCenter = worldGrid.GetTileCenter(bookmark.TileId);
                AddBookmarkMarker(vertices, triangles, colors, tileCenter, bookmark.MarkerColor, markerSize);
            }

            _cachedMesh.SetVertices(vertices);
            _cachedMesh.SetTriangles(triangles, 0);
            _cachedMesh.SetColors(colors);
            _cachedMesh.RecalculateNormals();
            _cachedMesh.RecalculateBounds();
        }

        private static void AddBookmarkMarker(List<Vector3> vertices, List<int> triangles, List<Color> colors, Vector3 position, Color color, float markerSize)
        {
            // Create a teardrop-shaped marker using a simple diamond
            // Offset position slightly above the world surface
            Vector3 markerPos = position.normalized * (position.magnitude + MarkerHeight);

            // Calculate marker vertices in local tangent space
            Vector3 up = markerPos.normalized;
            Vector3 north = Vector3.Cross(up, Vector3.right).normalized;
            if (north == Vector3.zero) // Handle poles
                north = Vector3.Cross(up, Vector3.forward).normalized;
            Vector3 east = Vector3.Cross(up, north).normalized;

            // Diamond vertices (top, left, bottom, right) - scaled by markerSize
            Vector3 top = markerPos + north * markerSize * 0.8f;
            Vector3 left = markerPos - east * markerSize * 0.5f;
            Vector3 bottom = markerPos - north * markerSize * 1.2f; // Elongated downward (teardrop shape)
            Vector3 right = markerPos + east * markerSize * 0.5f;

            int vertexCount = vertices.Count;

            // Add vertices
            vertices.Add(top);
            vertices.Add(left);
            vertices.Add(bottom);
            vertices.Add(right);

            // Add colors (with slight glow)
            Color glowColor = new Color(
                Mathf.Min(1f, color.r * 1.2f),
                Mathf.Min(1f, color.g * 1.2f),
                Mathf.Min(1f, color.b * 1.2f),
                color.a
            );
            for (int i = 0; i < 4; i++)
            {
                colors.Add(glowColor);
            }

            // Add triangles to form diamond
            // Top-left-right triangle
            triangles.Add(vertexCount + 0);
            triangles.Add(vertexCount + 1);
            triangles.Add(vertexCount + 3);

            // Left-bottom-right triangle
            triangles.Add(vertexCount + 1);
            triangles.Add(vertexCount + 2);
            triangles.Add(vertexCount + 3);

            // Add dark outline for contrast
            AddMarkerOutline(vertices, triangles, colors, markerPos, north, east, color, markerSize);
        }

        private static void AddMarkerOutline(List<Vector3> vertices, List<int> triangles, List<Color> colors,
            Vector3 markerPos, Vector3 north, Vector3 east, Color color, float markerSize)
        {
            Color outlineColor = new Color(0.1f, 0.1f, 0.1f, color.a); // Dark outline
            float outlineOffset = markerSize * 0.06f;

            // Outer vertices (slightly larger)
            Vector3 topOuter = markerPos + north * (markerSize * 0.8f + outlineOffset);
            Vector3 leftOuter = markerPos - east * (markerSize * 0.5f + outlineOffset);
            Vector3 bottomOuter = markerPos - north * (markerSize * 1.2f + outlineOffset);
            Vector3 rightOuter = markerPos + east * (markerSize * 0.5f + outlineOffset);

            // Inner vertices (match marker)
            Vector3 topInner = markerPos + north * markerSize * 0.8f;
            Vector3 leftInner = markerPos - east * markerSize * 0.5f;
            Vector3 bottomInner = markerPos - north * markerSize * 1.2f;
            Vector3 rightInner = markerPos + east * markerSize * 0.5f;

            // Add outline edges as thin quads
            AddOutlineEdge(vertices, triangles, colors, topOuter, topInner, rightOuter, rightInner, outlineColor);
            AddOutlineEdge(vertices, triangles, colors, rightOuter, rightInner, bottomOuter, bottomInner, outlineColor);
            AddOutlineEdge(vertices, triangles, colors, bottomOuter, bottomInner, leftOuter, leftInner, outlineColor);
            AddOutlineEdge(vertices, triangles, colors, leftOuter, leftInner, topOuter, topInner, outlineColor);
        }

        private static void AddOutlineEdge(List<Vector3> vertices, List<int> triangles, List<Color> colors,
            Vector3 outer1, Vector3 inner1, Vector3 outer2, Vector3 inner2, Color color)
        {
            int vertexCount = vertices.Count;

            // Add 4 vertices for this edge quad
            vertices.Add(outer1);
            vertices.Add(inner1);
            vertices.Add(outer2);
            vertices.Add(inner2);

            // Colors
            for (int i = 0; i < 4; i++)
                colors.Add(color);

            // Two triangles for the quad
            triangles.Add(vertexCount + 0);
            triangles.Add(vertexCount + 1);
            triangles.Add(vertexCount + 2);

            triangles.Add(vertexCount + 1);
            triangles.Add(vertexCount + 3);
            triangles.Add(vertexCount + 2);
        }
    }
}

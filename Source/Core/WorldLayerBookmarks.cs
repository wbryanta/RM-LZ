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
        private static readonly Material BookmarkMaterial;
        private static Mesh _cachedMesh;
        private static bool _meshDirty = true;

        private const float MarkerSize = 0.5f; // Size of the marker in world units
        private const float MarkerHeight = 0.015f; // Height above world surface

        static WorldLayerBookmarks()
        {
            // Initialize Material in static constructor (required for Unity assets)
            BookmarkMaterial = new Material(Shader.Find("Standard"))
            {
                color = Color.white
            };
        }

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
            if (manager == null || manager.Bookmarks.Count == 0)
                return;

            var worldGrid = Find.WorldGrid;
            if (worldGrid == null)
                return;

            // Regenerate mesh if needed
            if (_meshDirty || _cachedMesh == null)
            {
                RegenerateMesh(manager, worldGrid);
                _meshDirty = false;
            }

            // Draw the mesh
            if (_cachedMesh != null && _cachedMesh.vertexCount > 0)
            {
                Graphics.DrawMesh(_cachedMesh, Matrix4x4.identity, BookmarkMaterial, 0);
            }
        }

        private static void RegenerateMesh(BookmarkManager manager, WorldGrid worldGrid)
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

            foreach (var bookmark in manager.Bookmarks)
            {
                if (bookmark.TileId < 0 || bookmark.TileId >= worldGrid.TilesCount)
                    continue;

                Vector3 tileCenter = worldGrid.GetTileCenter(bookmark.TileId);
                AddBookmarkMarker(vertices, triangles, colors, tileCenter, bookmark.MarkerColor);
            }

            _cachedMesh.SetVertices(vertices);
            _cachedMesh.SetTriangles(triangles, 0);
            _cachedMesh.SetColors(colors);
            _cachedMesh.RecalculateNormals();
            _cachedMesh.RecalculateBounds();
        }

        private static void AddBookmarkMarker(List<Vector3> vertices, List<int> triangles, List<Color> colors, Vector3 position, Color color)
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

            // Diamond vertices (top, left, bottom, right)
            Vector3 top = markerPos + north * MarkerSize * 0.8f;
            Vector3 left = markerPos - east * MarkerSize * 0.5f;
            Vector3 bottom = markerPos - north * MarkerSize * 1.2f; // Elongated downward (teardrop shape)
            Vector3 right = markerPos + east * MarkerSize * 0.5f;

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
            AddMarkerOutline(vertices, triangles, colors, markerPos, north, east, color);
        }

        private static void AddMarkerOutline(List<Vector3> vertices, List<int> triangles, List<Color> colors,
            Vector3 markerPos, Vector3 north, Vector3 east, Color color)
        {
            Color outlineColor = new Color(0.1f, 0.1f, 0.1f, color.a); // Dark outline
            float outlineOffset = MarkerSize * 0.06f;

            // Outer vertices (slightly larger)
            Vector3 topOuter = markerPos + north * (MarkerSize * 0.8f + outlineOffset);
            Vector3 leftOuter = markerPos - east * (MarkerSize * 0.5f + outlineOffset);
            Vector3 bottomOuter = markerPos - north * (MarkerSize * 1.2f + outlineOffset);
            Vector3 rightOuter = markerPos + east * (MarkerSize * 0.5f + outlineOffset);

            // Inner vertices (match marker)
            Vector3 topInner = markerPos + north * MarkerSize * 0.8f;
            Vector3 leftInner = markerPos - east * MarkerSize * 0.5f;
            Vector3 bottomInner = markerPos - north * MarkerSize * 1.2f;
            Vector3 rightInner = markerPos + east * MarkerSize * 0.5f;

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

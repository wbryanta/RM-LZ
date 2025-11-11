using System.Collections;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using LandingZone.Core;

namespace LandingZone.Core.Highlighting
{
    /// <summary>
    /// Renders an outline around tiles considered "best" landing sites.
    /// </summary>
    public class WorldDrawLayer_LandingZoneBestSites : WorldDrawLayerBase
    {
        private const float EdgeLift = 0.01f;
        private const float OverlayAlpha = 0.9f;
        private const float LineThickness = 0.03f;

        private readonly System.Collections.Generic.List<Vector3> _vertices = new();
        private readonly System.Collections.Generic.Dictionary<Color32, Material> _materialCache = new();

        public override IEnumerable Regenerate()
        {
            foreach (var result in base.Regenerate())
                yield return result;

            var highlightState = LandingZoneContext.HighlightState;
            if (highlightState == null || !highlightState.ShowBestSites)
                yield break;

            var colors = highlightState.BestSitesLayer.Colors;
            if (colors.Count == 0)
                yield break;

            var grid = Find.WorldGrid;
            if (grid == null)
                yield break;

            foreach (var entry in colors)
            {
                if (entry.TileId < 0)
                    continue;

                var material = GetOrCreateMaterial(entry.Color);
                var subMesh = GetSubMesh(material);
                subMesh.finalized = false;

                grid.GetTileVertices(entry.TileId, _vertices);
                if (_vertices.Count < 2)
                    continue;

                for (int i = 0; i < _vertices.Count; i++)
                {
                    if (i % 500 == 0)
                        yield return null;

                    if (subMesh.verts.Count > 39000)
                    {
                        subMesh = GetSubMesh(material);
                        subMesh.finalized = false;
                    }

                    var a = Lift(_vertices[i]);
                    var b = Lift(_vertices[(i + 1) % _vertices.Count]);
                    AppendEdgeQuad(subMesh, a, b);
                }

                subMesh.FinalizeMesh(MeshParts.Verts | MeshParts.Tris);
            }
        }

        private Material GetOrCreateMaterial(Color color)
        {
            var quantized = Quantize(color);
            if (_materialCache.TryGetValue(quantized, out var material))
                return material;

            var unityColor = new Color(quantized.r / 255f, quantized.g / 255f, quantized.b / 255f, OverlayAlpha);
            material = new Material(WorldMaterials.SelectedTile)
            {
                color = unityColor
            };
            material.renderQueue = 3400;
            _materialCache.Add(quantized, material);
            return material;
        }

        private static Color32 Quantize(Color color)
        {
            const float step = 0.1f;
            byte QuantizeComponent(float value)
            {
                var quantized = Mathf.Clamp01(Mathf.Round(value / step) * step);
                return (byte)(quantized * 255f);
            }

            return new Color32(QuantizeComponent(color.r), QuantizeComponent(color.g), QuantizeComponent(color.b), 255);
        }

        private static Vector3 Lift(Vector3 vertex) => vertex + vertex.normalized * EdgeLift;

        private void AppendEdgeQuad(LayerSubMesh subMesh, Vector3 a, Vector3 b)
        {
            var edgeDir = (b - a).normalized;
            var normal = ((a + b) * 0.5f).normalized;
            var outward = Vector3.Cross(edgeDir, normal).normalized;
            var offset = outward * LineThickness;

            var v0 = a + offset;
            var v1 = a - offset;
            var v2 = b - offset;
            var v3 = b + offset;

            var startIndex = subMesh.verts.Count;
            subMesh.verts.Add(v0);
            subMesh.verts.Add(v1);
            subMesh.verts.Add(v2);
            subMesh.verts.Add(v3);

            subMesh.tris.Add(startIndex);
            subMesh.tris.Add(startIndex + 1);
            subMesh.tris.Add(startIndex + 2);
            subMesh.tris.Add(startIndex);
            subMesh.tris.Add(startIndex + 2);
            subMesh.tris.Add(startIndex + 3);
        }
    }
}

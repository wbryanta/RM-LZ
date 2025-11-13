using System.Collections.Generic;
using LandingZone.Core.Filtering;
using UnityEngine;

namespace LandingZone.Core.Highlighting
{
    public sealed class HeatmapOverlay
    {
        private readonly List<TileColor> _buffer = new();

        public IReadOnlyList<TileColor> BuildColors(IReadOnlyList<TileScore> scores)
        {
            _buffer.Clear();
            if (scores.Count == 0)
                return _buffer;

            foreach (var tile in scores)
            {
                var color = GetScoreTierColor(tile.Score);
                _buffer.Add(new TileColor(tile.TileId, color));
            }

            return _buffer;
        }

        private static Color GetScoreTierColor(float score)
        {
            if (score >= 0.90f) return new Color(0.3f, 0.9f, 0.3f); // Excellent - bright green
            if (score >= 0.75f) return new Color(0.3f, 0.85f, 0.9f); // Good - cyan
            if (score >= 0.60f) return new Color(0.95f, 0.9f, 0.3f); // Acceptable - yellow
            return new Color(1.0f, 0.6f, 0.2f); // Poor - orange
        }
    }

    public readonly struct TileColor
    {
        public TileColor(int tileId, Color color)
        {
            TileId = tileId;
            Color = color;
        }

        public int TileId { get; }
        public Color Color { get; }
    }
}

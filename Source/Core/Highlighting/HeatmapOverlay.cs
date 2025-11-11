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

            var maxScore = scores[0].Score;
            var minScore = scores[scores.Count - 1].Score;
            var range = maxScore - minScore;
            if (range <= 0.0001f)
                range = 1f;

            foreach (var tile in scores)
            {
                var normalized = (tile.Score - minScore) / range;
                var color = Color.Lerp(Color.yellow, Color.green, normalized);
                _buffer.Add(new TileColor(tile.TileId, color));
            }

            return _buffer;
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

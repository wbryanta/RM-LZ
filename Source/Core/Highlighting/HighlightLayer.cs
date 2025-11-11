using System.Collections.Generic;
using UnityEngine;

namespace LandingZone.Core.Highlighting
{
    public sealed class HighlightLayer
    {
        private readonly List<TileColor> _colors = new();

        public void Update(IReadOnlyList<TileColor> colors)
        {
            _colors.Clear();
            _colors.AddRange(colors);
        }

        public IReadOnlyList<TileColor> Colors => _colors;
        public bool Enabled { get; set; }
    }
}

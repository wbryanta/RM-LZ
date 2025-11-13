using System;
using UnityEngine;
using Verse;

namespace LandingZone.Data
{
    /// <summary>
    /// Represents a bookmarked tile on the world map.
    /// Persists with the save game so players can track interesting locations.
    /// </summary>
    public class TileBookmark : IExposable
    {
        public int TileId;
        public Color MarkerColor = Color.yellow;
        public string Label = string.Empty;
        public long CreatedTicks; // Game ticks when bookmark was created

        public TileBookmark()
        {
            // Parameterless constructor required for IExposable
        }

        public TileBookmark(int tileId, Color color, string label = "")
        {
            TileId = tileId;
            MarkerColor = color;
            Label = label ?? string.Empty;
            CreatedTicks = Find.TickManager?.TicksGame ?? 0;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref TileId, "tileId", -1);
            Scribe_Values.Look(ref MarkerColor, "markerColor", Color.yellow);
            Scribe_Values.Look(ref Label, "label", string.Empty);
            Scribe_Values.Look(ref CreatedTicks, "createdTicks", 0L);
        }

        /// <summary>
        /// Gets the tile's coordinates as a display string.
        /// </summary>
        public string GetCoordinatesText()
        {
            var grid = Find.WorldGrid;
            if (grid == null || TileId < 0 || TileId >= grid.TilesCount)
                return "Invalid";

            Vector2 tileCenter = grid.LongLatOf(TileId);
            // Format coordinates (latitude, longitude)
            return $"{tileCenter.y:F1}°, {tileCenter.x:F1}°";
        }

        /// <summary>
        /// Gets display text for this bookmark (label or coordinates).
        /// </summary>
        public string GetDisplayText()
        {
            if (!string.IsNullOrWhiteSpace(Label))
                return Label;

            return $"Tile {TileId} ({GetCoordinatesText()})";
        }
    }

    /// <summary>
    /// Predefined bookmark colors for quick selection.
    /// </summary>
    public static class BookmarkColors
    {
        public static readonly Color Yellow = new Color(1f, 0.95f, 0.2f);      // Default
        public static readonly Color Red = new Color(0.9f, 0.2f, 0.2f);        // High priority
        public static readonly Color Blue = new Color(0.2f, 0.6f, 1f);         // Mining/resources
        public static readonly Color Green = new Color(0.3f, 0.9f, 0.3f);      // Farming/fertile
        public static readonly Color Orange = new Color(1f, 0.6f, 0.2f);       // Settlement candidate
        public static readonly Color Purple = new Color(0.8f, 0.4f, 0.9f);     // Special feature
        public static readonly Color Cyan = new Color(0.2f, 0.9f, 0.9f);       // Coastal/water
        public static readonly Color White = new Color(0.95f, 0.95f, 0.95f);   // Note/reminder

        public static readonly Color[] AllColors = new[]
        {
            Yellow, Red, Blue, Green, Orange, Purple, Cyan, White
        };

        public static readonly string[] ColorNames = new[]
        {
            "Yellow (Default)", "Red (Priority)", "Blue (Resources)", "Green (Farming)",
            "Orange (Settlement)", "Purple (Special)", "Cyan (Coastal)", "White (Note)"
        };
    }
}

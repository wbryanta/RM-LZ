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
        public Color MarkerColor = BookmarkColors.Red; // Default to pure red
        public string Label = string.Empty;
        public string Notes = string.Empty; // Multiline notes for detailed information
        public bool ShowTitleOnGlobe = true; // Whether to display title text on the world map
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
            Notes = string.Empty;
            ShowTitleOnGlobe = true;
            CreatedTicks = Find.TickManager?.TicksGame ?? 0;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref TileId, "tileId", -1);
            Scribe_Values.Look(ref MarkerColor, "markerColor", BookmarkColors.Red);
            Scribe_Values.Look(ref Label, "label", string.Empty);
            Scribe_Values.Look(ref Notes, "notes", string.Empty);
            Scribe_Values.Look(ref ShowTitleOnGlobe, "showTitleOnGlobe", true);
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
    /// High-visibility colors chosen to avoid conflicts with game faction/feature markers.
    /// </summary>
    public static class BookmarkColors
    {
        public static readonly Color Red = new Color(1f, 0.05f, 0.05f);        // Pure red - Default
        public static readonly Color Yellow = new Color(1f, 0.95f, 0.2f);      // Bright yellow
        public static readonly Color Blue = new Color(0.2f, 0.6f, 1f);         // Sky blue - Mining/resources
        public static readonly Color Green = new Color(0.3f, 0.9f, 0.3f);      // Lime green - Farming/fertile
        public static readonly Color Orange = new Color(1f, 0.6f, 0.2f);       // Bright orange - Settlement candidate
        public static readonly Color Purple = new Color(0.8f, 0.4f, 0.9f);     // Violet - Special feature
        public static readonly Color Cyan = new Color(0.2f, 0.9f, 0.9f);       // Bright cyan - Coastal/water
        public static readonly Color Magenta = new Color(1f, 0.2f, 0.8f);      // Hot pink - High priority
        public static readonly Color White = new Color(0.95f, 0.95f, 0.95f);   // Bright white - Note/reminder

        public static readonly Color[] AllColors = new[]
        {
            Red, Yellow, Blue, Green, Orange, Purple, Cyan, Magenta, White
        };

        public static readonly string[] ColorNames = new[]
        {
            "Red", "Yellow", "Blue", "Green",
            "Orange", "Purple", "Cyan", "Magenta", "White"
        };
    }
}

# Bookmark API Documentation

## Overview

LandingZone provides a public bookmark API that allows other mods to access, create, and manage tile bookmarks. This document describes the API surfaces available for mod integration.

## Getting Started

### Prerequisites

Your mod must reference LandingZone.dll and ensure it loads after LandingZone in the mod load order.

### Basic Usage

```csharp
using LandingZone.Core;
using LandingZone.Data;

// Get the bookmark manager for the current game
var manager = BookmarkManager.Get();
if (manager == null)
{
    // No game running or manager not initialized
    return;
}

// Check if a tile is bookmarked
bool isBookmarked = manager.IsBookmarked(tileId);

// Get all bookmarks
foreach (var bookmark in manager.Bookmarks)
{
    Log.Message($"Bookmark at tile {bookmark.TileId}: {bookmark.Label}");
}
```

## BookmarkManager API

### Static Methods

#### `BookmarkManager.Get()`
Returns the BookmarkManager instance for the current game, or null if no game is running.

```csharp
var manager = BookmarkManager.Get();
if (manager == null)
{
    Log.Warning("No active game - bookmark manager unavailable");
    return;
}
```

#### `BookmarkManager.GetOrCreate()`
Returns the BookmarkManager instance for the current game, creating it if it doesn't exist. Returns null if no game is running.

```csharp
var manager = BookmarkManager.GetOrCreate();
```

### Instance Properties

#### `Bookmarks` (IReadOnlyList<TileBookmark>)
Read-only list of all bookmarks in the current game.

```csharp
var manager = BookmarkManager.Get();
int bookmarkCount = manager.Bookmarks.Count;

foreach (var bookmark in manager.Bookmarks)
{
    // Process each bookmark
}
```

### Instance Methods

#### `IsBookmarked(int tileId) : bool`
Checks if a specific tile is bookmarked.

```csharp
bool isBookmarked = manager.IsBookmarked(12345);
```

#### `GetBookmark(int tileId) : TileBookmark`
Gets the bookmark for a specific tile, or null if not bookmarked.

```csharp
var bookmark = manager.GetBookmark(tileId);
if (bookmark != null)
{
    Log.Message($"Bookmark found: {bookmark.Label}");
}
```

#### `AddBookmark(int tileId, Color color, string label = "") : bool`
Adds or updates a bookmark for a tile. Returns true if successful, false if at maximum capacity.

**Parameters:**
- `tileId` - The world tile ID to bookmark
- `color` - Marker color (see BookmarkColors for presets)
- `label` - Optional title/label for the bookmark

```csharp
using UnityEngine;
using LandingZone.Data;

// Add bookmark with preset color
bool success = manager.AddBookmark(
    tileId: 12345,
    color: BookmarkColors.Red,
    label: "Ancient Danger Site"
);

if (!success)
{
    Log.Warning("Failed to add bookmark - maximum capacity reached");
}
```

#### `RemoveBookmark(int tileId) : bool`
Removes a bookmark from a tile. Returns true if a bookmark was removed, false if none existed.

```csharp
bool removed = manager.RemoveBookmark(tileId);
if (removed)
{
    Log.Message($"Removed bookmark from tile {tileId}");
}
```

#### `ToggleBookmark(int tileId, Color? defaultColor = null) : bool`
Toggles a bookmark on/off for a tile. Returns true if added, false if removed.

```csharp
bool wasAdded = manager.ToggleBookmark(tileId, BookmarkColors.Blue);
if (wasAdded)
{
    Log.Message("Bookmark added");
}
else
{
    Log.Message("Bookmark removed");
}
```

#### `UpdateBookmark(int tileId, string label = null, string notes = null, Color? color = null, bool? showTitleOnGlobe = null) : bool`
Updates properties of an existing bookmark. Only provided parameters are updated. Returns true if successful, false if bookmark doesn't exist.

```csharp
bool updated = manager.UpdateBookmark(
    tileId: 12345,
    label: "Updated Label",
    notes: "Some additional notes",
    color: BookmarkColors.Green,
    showTitleOnGlobe: false
);
```

## TileBookmark Data Structure

Represents a bookmarked tile on the world map.

### Public Fields

```csharp
public class TileBookmark
{
    public int TileId;                     // World tile ID
    public Color MarkerColor;              // Marker color (default: Red)
    public string Label;                   // Title/label (empty string if none)
    public string Notes;                   // Multiline notes (empty string if none)
    public bool ShowTitleOnGlobe;          // Whether to show label on world map
    public long CreatedTicks;              // Game tick when bookmark was created
}
```

### Methods

#### `GetCoordinatesText() : string`
Returns formatted coordinates string for the tile (e.g., "45.2°, -12.7°").

```csharp
var bookmark = manager.GetBookmark(tileId);
string coords = bookmark.GetCoordinatesText();
// Example output: "45.2°, -12.7°"
```

#### `GetDisplayText() : string`
Returns display text for the bookmark (label if set, otherwise "Tile {id} (coordinates)").

```csharp
string display = bookmark.GetDisplayText();
// Example: "Ancient Danger Site" or "Tile 12345 (45.2°, -12.7°)"
```

## BookmarkColors

Predefined color constants for bookmark markers.

```csharp
public static class BookmarkColors
{
    public static readonly Color Red;      // Pure red (default)
    public static readonly Color Yellow;   // Bright yellow
    public static readonly Color Blue;     // Sky blue (resources)
    public static readonly Color Green;    // Lime green (farming)
    public static readonly Color Orange;   // Bright orange (settlement)
    public static readonly Color Purple;   // Violet (special feature)
    public static readonly Color Cyan;     // Bright cyan (coastal/water)
    public static readonly Color Magenta;  // Hot pink (high priority)
    public static readonly Color White;    // Bright white (note/reminder)

    public static readonly Color[] AllColors;      // Array of all colors
    public static readonly string[] ColorNames;    // Descriptive names
}
```

### Usage Example

```csharp
using LandingZone.Data;

// Use preset color
manager.AddBookmark(tileId, BookmarkColors.Blue, "Mining Site");

// Iterate through all available colors
for (int i = 0; i < BookmarkColors.AllColors.Length; i++)
{
    Color color = BookmarkColors.AllColors[i];
    string name = BookmarkColors.ColorNames[i];
    Log.Message($"{name}: {color}");
}
```

## Events and Notifications

When bookmarks are added, removed, or updated, `WorldLayerBookmarks.MarkDirty()` is automatically called to refresh the world map rendering. Your mod does not need to handle this manually.

## Example: CovertOps Integration

```csharp
using LandingZone.Core;
using LandingZone.Data;
using UnityEngine;

public static class CovertOpsBookmarkIntegration
{
    /// <summary>
    /// Mark infiltration targets with bookmarks.
    /// </summary>
    public static void MarkInfiltrationTarget(int tileId, string targetName)
    {
        var manager = BookmarkManager.Get();
        if (manager == null)
        {
            Log.Warning("[CovertOps] Cannot mark target - no active game");
            return;
        }

        // Add bookmark with magenta color (high priority)
        bool success = manager.AddBookmark(
            tileId: tileId,
            color: BookmarkColors.Magenta,
            label: $"Target: {targetName}"
        );

        if (success)
        {
            // Add detailed notes
            var bookmark = manager.GetBookmark(tileId);
            if (bookmark != null)
            {
                manager.UpdateBookmark(
                    tileId: tileId,
                    notes: $"Infiltration target: {targetName}\\nMarked by CovertOps mod",
                    showTitleOnGlobe: true
                );
            }

            Log.Message($"[CovertOps] Marked target '{targetName}' at tile {tileId}");
        }
        else
        {
            Log.Warning($"[CovertOps] Failed to mark target - bookmark limit reached");
        }
    }

    /// <summary>
    /// Get all infiltration targets (bookmarks with magenta color).
    /// </summary>
    public static List<TileBookmark> GetInfiltrationTargets()
    {
        var manager = BookmarkManager.Get();
        if (manager == null)
            return new List<TileBookmark>();

        return manager.Bookmarks
            .Where(b => ColorsMatch(b.MarkerColor, BookmarkColors.Magenta))
            .ToList();
    }

    private static bool ColorsMatch(Color a, Color b)
    {
        const float threshold = 0.01f;
        return Mathf.Abs(a.r - b.r) < threshold &&
               Mathf.Abs(a.g - b.g) < threshold &&
               Mathf.Abs(a.b - b.b) < threshold;
    }
}
```

## Best Practices

1. **Always check for null**: `BookmarkManager.Get()` returns null when no game is running
2. **Respect capacity limits**: Maximum 20 bookmarks per game (may become configurable in future)
3. **Use descriptive labels**: Help users identify bookmarks at a glance
4. **Choose appropriate colors**: Use BookmarkColors presets for consistency with user expectations
5. **Add contextual notes**: Use the Notes field to provide additional information that appears in tooltips
6. **Check return values**: Methods like `AddBookmark()` return false on failure

## Version Compatibility

This API was introduced in LandingZone v0.0.3-alpha. Future versions will maintain backward compatibility for these public API surfaces. Breaking changes (if any) will be documented with migration guides.

## Support

For issues, questions, or feature requests related to bookmark API integration, please open an issue on the LandingZone GitHub repository.

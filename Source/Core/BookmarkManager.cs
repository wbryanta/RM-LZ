using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using RimWorld;
using UnityEngine;
using Verse;

namespace LandingZone.Core
{
    /// <summary>
    /// Game component that manages tile bookmarks.
    /// Persists bookmarks with the save game.
    /// </summary>
    public class BookmarkManager : GameComponent
    {
        private const int MaxBookmarks = 20; // Development cap, will be configurable later

        private List<TileBookmark> _bookmarks = new List<TileBookmark>();

        public BookmarkManager(Game game)
        {
        }

        /// <summary>
        /// All bookmarks for the current game.
        /// </summary>
        public IReadOnlyList<TileBookmark> Bookmarks => _bookmarks;

        /// <summary>
        /// Adds or updates a bookmark for a tile.
        /// </summary>
        public bool AddBookmark(int tileId, Color color, string label = "")
        {
            // Check if bookmark already exists
            var existing = _bookmarks.FirstOrDefault(b => b.TileId == tileId);
            if (existing != null)
            {
                // Update existing bookmark
                existing.MarkerColor = color;
                if (!string.IsNullOrEmpty(label))
                    existing.Label = label;

                Log.Message($"[LandingZone] Updated bookmark for tile {tileId}");
                WorldLayerBookmarks.MarkDirty();
                return true;
            }

            // Check capacity
            if (_bookmarks.Count >= MaxBookmarks)
            {
                Messages.Message($"Cannot add bookmark: maximum of {MaxBookmarks} bookmarks reached.",
                    MessageTypeDefOf.RejectInput, false);
                return false;
            }

            // Add new bookmark
            var bookmark = new TileBookmark(tileId, color, label);
            _bookmarks.Add(bookmark);

            Log.Message($"[LandingZone] Added bookmark for tile {tileId} (total: {_bookmarks.Count})");
            Messages.Message($"Bookmarked tile {tileId}", MessageTypeDefOf.SilentInput, false);

            WorldLayerBookmarks.MarkDirty();
            return true;
        }

        /// <summary>
        /// Removes a bookmark for a tile.
        /// </summary>
        public bool RemoveBookmark(int tileId)
        {
            var bookmark = _bookmarks.FirstOrDefault(b => b.TileId == tileId);
            if (bookmark == null)
                return false;

            _bookmarks.Remove(bookmark);
            Log.Message($"[LandingZone] Removed bookmark for tile {tileId} (remaining: {_bookmarks.Count})");
            Messages.Message($"Removed bookmark from tile {tileId}", MessageTypeDefOf.SilentInput, false);

            WorldLayerBookmarks.MarkDirty();
            return true;
        }

        /// <summary>
        /// Checks if a tile is bookmarked.
        /// </summary>
        public bool IsBookmarked(int tileId)
        {
            return _bookmarks.Any(b => b.TileId == tileId);
        }

        /// <summary>
        /// Gets the bookmark for a specific tile, if it exists.
        /// </summary>
        public TileBookmark GetBookmark(int tileId)
        {
            return _bookmarks.FirstOrDefault(b => b.TileId == tileId);
        }

        /// <summary>
        /// Toggles a bookmark for a tile.
        /// </summary>
        public bool ToggleBookmark(int tileId, Color? defaultColor = null)
        {
            if (IsBookmarked(tileId))
            {
                RemoveBookmark(tileId);
                return false; // Removed
            }
            else
            {
                AddBookmark(tileId, defaultColor ?? BookmarkColors.Yellow);
                return true; // Added
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref _bookmarks, "bookmarks", LookMode.Deep);

            // Ensure list is never null after loading
            if (Scribe.mode == LoadSaveMode.LoadingVars && _bookmarks == null)
            {
                _bookmarks = new List<TileBookmark>();
            }
        }

        /// <summary>
        /// Gets the BookmarkManager instance for the current game.
        /// </summary>
        public static BookmarkManager Get()
        {
            var game = Current.Game;
            if (game == null)
                return null;

            return game.GetComponent<BookmarkManager>();
        }

        /// <summary>
        /// Gets or creates the BookmarkManager instance for the current game.
        /// </summary>
        public static BookmarkManager GetOrCreate()
        {
            var game = Current.Game;
            if (game == null)
                return null;

            var manager = game.GetComponent<BookmarkManager>();
            if (manager == null)
            {
                manager = new BookmarkManager(game);
                game.components.Add(manager);
            }

            return manager;
        }
    }
}

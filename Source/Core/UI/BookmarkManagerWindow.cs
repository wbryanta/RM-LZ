using System.Linq;
using LandingZone.Data;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LandingZone.Core.UI
{
    /// <summary>
    /// Window that displays and manages all bookmarks.
    /// Allows viewing, editing, deleting, and jumping to bookmarked tiles.
    /// </summary>
    public class BookmarkManagerWindow : Window
    {
        private const float RowHeight = 50f;
        private const float ColorSwatchSize = 30f;
        private const float ButtonWidth = 80f;
        private const float ButtonHeight = 30f;
        private const float ScrollbarWidth = 16f;

        private Vector2 _scrollPosition;
        private TileBookmark _selectedBookmark;

        public BookmarkManagerWindow()
        {
            doCloseX = true;
            doCloseButton = false;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
        }

        public override Vector2 InitialSize => new Vector2(700f, 600f);

        public override void DoWindowContents(Rect inRect)
        {
            var manager = BookmarkManager.Get();
            if (manager == null)
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(inRect, "No active game - bookmarks unavailable");
                Text.Font = GameFont.Small;
                return;
            }

            // Header
            Rect headerRect = new Rect(inRect.x, inRect.y, inRect.width, 40f);
            DrawHeader(headerRect, manager);

            // Bookmark list
            Rect listRect = new Rect(inRect.x, headerRect.yMax + 10f, inRect.width, inRect.height - headerRect.height - 20f);
            DrawBookmarkList(listRect, manager);
        }

        private void DrawHeader(Rect rect, BookmarkManager manager)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, 300f, rect.height), $"Bookmarks ({manager.Bookmarks.Count}/20)");
            Text.Font = GameFont.Small;

            // Clear all button (on the right)
            Rect clearAllRect = new Rect(rect.xMax - ButtonWidth, rect.y + 5f, ButtonWidth, ButtonHeight);
            if (manager.Bookmarks.Count > 0 && Widgets.ButtonText(clearAllRect, "Clear All"))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Are you sure you want to delete all bookmarks? This cannot be undone.",
                    () => ClearAllBookmarks(manager),
                    destructive: true
                ));
            }
        }

        private void DrawBookmarkList(Rect rect, BookmarkManager manager)
        {
            if (manager.Bookmarks.Count == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                Widgets.Label(rect, "No bookmarks yet.\n\nClick the star icon in the action bar to add bookmarks,\nor use the 'BM' button in the Results window.");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // Calculate content height
            float contentHeight = manager.Bookmarks.Count * (RowHeight + 5f);
            Rect viewRect = new Rect(0f, 0f, rect.width - ScrollbarWidth, contentHeight);
            Rect scrollRect = rect;

            Widgets.BeginScrollView(scrollRect, ref _scrollPosition, viewRect);

            float y = 0f;
            var bookmarksToRemove = new System.Collections.Generic.List<int>();

            foreach (var bookmark in manager.Bookmarks)
            {
                Rect rowRect = new Rect(0f, y, viewRect.width, RowHeight);

                if (DrawBookmarkRow(rowRect, bookmark, manager))
                {
                    bookmarksToRemove.Add(bookmark.TileId);
                }

                y += RowHeight + 5f;
            }

            Widgets.EndScrollView();

            // Remove bookmarks marked for deletion
            foreach (var tileId in bookmarksToRemove)
            {
                manager.RemoveBookmark(tileId);
            }
        }

        /// <summary>
        /// Draws a single bookmark row.
        /// Returns true if the bookmark should be deleted.
        /// </summary>
        private bool DrawBookmarkRow(Rect rect, TileBookmark bookmark, BookmarkManager manager)
        {
            bool shouldDelete = false;

            // Background
            Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.1f, 0.5f));
            if (_selectedBookmark == bookmark)
            {
                Widgets.DrawHighlight(rect);
            }

            Rect contentRect = rect.ContractedBy(5f);
            float x = contentRect.x;

            // Color swatch
            Rect colorRect = new Rect(x, contentRect.y + (contentRect.height - ColorSwatchSize) / 2f, ColorSwatchSize, ColorSwatchSize);
            Widgets.DrawBoxSolid(colorRect, bookmark.MarkerColor);
            Widgets.DrawBox(colorRect);
            x += ColorSwatchSize + 10f;

            // Bookmark info (title, coordinates, notes preview)
            Rect infoRect = new Rect(x, contentRect.y, contentRect.width - ColorSwatchSize - 10f - (ButtonWidth * 3 + 20f), contentRect.height);
            DrawBookmarkInfo(infoRect, bookmark);
            x = contentRect.xMax - (ButtonWidth * 3 + 20f);

            // Edit button
            Rect editRect = new Rect(x, contentRect.y + (contentRect.height - ButtonHeight) / 2f, ButtonWidth, ButtonHeight);
            if (Widgets.ButtonText(editRect, "Edit"))
            {
                Find.WindowStack.Add(new BookmarkEditDialog(bookmark.TileId));
            }
            x += ButtonWidth + 5f;

            // Jump button
            Rect jumpRect = new Rect(x, contentRect.y + (contentRect.height - ButtonHeight) / 2f, ButtonWidth, ButtonHeight);
            if (Widgets.ButtonText(jumpRect, "Jump To"))
            {
                JumpToBookmark(bookmark);
            }
            x += ButtonWidth + 5f;

            // Delete button
            Rect deleteRect = new Rect(x, contentRect.y + (contentRect.height - ButtonHeight) / 2f, ButtonWidth, ButtonHeight);
            if (Widgets.ButtonText(deleteRect, "Delete"))
            {
                shouldDelete = true;
            }

            // Click to select
            if (Mouse.IsOver(rect) && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                _selectedBookmark = bookmark;
                Event.current.Use();
            }

            return shouldDelete;
        }

        private void DrawBookmarkInfo(Rect rect, TileBookmark bookmark)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            float y = rect.y;

            // Title or "Untitled"
            string displayTitle = !string.IsNullOrWhiteSpace(bookmark.Label)
                ? bookmark.Label
                : $"<i>Tile {bookmark.TileId}</i>";

            Rect titleRect = new Rect(rect.x, y, rect.width, 20f);
            if (!string.IsNullOrWhiteSpace(bookmark.Label))
            {
                Widgets.Label(titleRect, bookmark.Label);
            }
            else
            {
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                Widgets.Label(titleRect, $"Tile {bookmark.TileId}");
                GUI.color = Color.white;
            }
            y += 18f;

            // Coordinates + Show on Globe indicator
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            string coordText = $"{bookmark.GetCoordinatesText()}";
            if (bookmark.ShowTitleOnGlobe)
            {
                coordText += " • Title visible on map";
            }
            Rect coordRect = new Rect(rect.x, y, rect.width, 15f);
            Widgets.Label(coordRect, coordText);
            y += 13f;

            // Notes preview (first 60 chars)
            if (!string.IsNullOrWhiteSpace(bookmark.Notes))
            {
                string notesPreview = bookmark.Notes.Length > 60
                    ? bookmark.Notes.Substring(0, 60) + "..."
                    : bookmark.Notes;
                notesPreview = notesPreview.Replace("\n", " ").Replace("\r", "");

                Rect notesRect = new Rect(rect.x, y, rect.width, 15f);
                Widgets.Label(notesRect, $"Notes: {notesPreview}");
            }

            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void JumpToBookmark(TileBookmark bookmark)
        {
            // Close this window
            Close();

            // Focus camera on the tile
            if (Find.WorldCameraDriver != null)
            {
                var worldGrid = Find.WorldGrid;
                if (worldGrid != null && bookmark.TileId >= 0 && bookmark.TileId < worldGrid.TilesCount)
                {
                    Vector3 tileCenter = worldGrid.GetTileCenter(bookmark.TileId);
                    Find.WorldCameraDriver.JumpTo(tileCenter);

                    Log.Message($"[LandingZone] Jumped to bookmark at tile {bookmark.TileId}");
                }
            }
        }

        private void ClearAllBookmarks(BookmarkManager manager)
        {
            var bookmarksToRemove = manager.Bookmarks.Select(b => b.TileId).ToList();
            foreach (var tileId in bookmarksToRemove)
            {
                manager.RemoveBookmark(tileId);
            }
            _selectedBookmark = null;
            Log.Message($"[LandingZone] Cleared all bookmarks");
        }
    }
}

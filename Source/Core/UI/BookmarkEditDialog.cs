using LandingZone.Data;
using RimWorld;
using UnityEngine;
using Verse;

namespace LandingZone.Core.UI
{
    /// <summary>
    /// Dialog for editing bookmark properties (title, notes, color, visibility).
    /// </summary>
    public class BookmarkEditDialog : Window
    {
        private const float ColorSwatchSize = 40f;
        private const float ColorSwatchSpacing = 10f;

        private readonly int _tileId;
        private string _title;
        private string _notes;
        private Color _selectedColor;
        private bool _showTitleOnGlobe;

        private bool _cancelled = false;

        public BookmarkEditDialog(int tileId)
        {
            _tileId = tileId;

            doCloseX = true;
            doCloseButton = false;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;

            // Load current bookmark data
            var manager = BookmarkManager.Get();
            var bookmark = manager?.GetBookmark(tileId);
            if (bookmark != null)
            {
                _title = bookmark.Label ?? string.Empty;
                _notes = bookmark.Notes ?? string.Empty;
                _selectedColor = bookmark.MarkerColor;
                _showTitleOnGlobe = bookmark.ShowTitleOnGlobe;
            }
            else
            {
                _title = string.Empty;
                _notes = string.Empty;
                _selectedColor = BookmarkColors.Red;
                _showTitleOnGlobe = true;
            }
        }

        public override void PreClose()
        {
            base.PreClose();

            // Auto-save on close unless explicitly cancelled
            if (!_cancelled)
            {
                var manager = BookmarkManager.Get();
                if (manager != null)
                {
                    SaveChanges(manager);
                }
            }
        }

        public override Vector2 InitialSize => new Vector2(500f, 500f);

        public override void DoWindowContents(Rect inRect)
        {
            var manager = BookmarkManager.Get();
            if (manager == null)
            {
                Close();
                return;
            }

            var listing = new Listing_Standard { ColumnWidth = inRect.width };
            listing.Begin(inRect);

            // Header
            Text.Font = GameFont.Medium;
            listing.Label("LandingZone_EditBookmark".Translate(_tileId));
            Text.Font = GameFont.Small;
            listing.GapLine();

            // Coordinates (read-only info)
            var bookmark = manager.GetBookmark(_tileId);
            if (bookmark != null)
            {
                GUI.color = new Color(0.8f, 0.8f, 0.8f);
                listing.Label("LandingZone_BookmarkLocation".Translate(bookmark.GetCoordinatesText()));
                GUI.color = Color.white;
                listing.Gap(10f);
            }

            // Title field
            listing.Label("LandingZone_TitleLabel".Translate());
            _title = listing.TextEntry(_title);
            listing.Gap(10f);

            // Notes field (multiline)
            listing.Label("LandingZone_NotesLabel".Translate());
            Rect notesRect = listing.GetRect(120f);
            _notes = GUI.TextArea(notesRect, _notes ?? string.Empty);
            listing.Gap(10f);

            // Show title on globe toggle
            Rect toggleRect = listing.GetRect(30f);
            Widgets.CheckboxLabeled(toggleRect, "LandingZone_ShowTitleOnWorldMap".Translate(), ref _showTitleOnGlobe);
            listing.Gap(10f);

            // Color picker
            listing.Label("LandingZone_MarkerColor".Translate());
            listing.Gap(5f);
            DrawColorPicker(listing.GetRect(ColorSwatchSize * 2 + 20f));
            listing.Gap(10f);

            // Action buttons
            listing.Gap(20f);
            Rect buttonRect = listing.GetRect(35f);
            DrawActionButtons(buttonRect, manager);

            listing.End();
        }

        private void DrawColorPicker(Rect rect)
        {
            float x = rect.x;
            float y = rect.y;
            int colorsPerRow = 5;

            for (int i = 0; i < BookmarkColors.AllColors.Length; i++)
            {
                Color color = BookmarkColors.AllColors[i];
                string colorName = BookmarkColors.ColorNames[i];

                // Calculate position
                if (i > 0 && i % colorsPerRow == 0)
                {
                    x = rect.x;
                    y += ColorSwatchSize + ColorSwatchSpacing;
                }

                Rect swatchRect = new Rect(x, y, ColorSwatchSize, ColorSwatchSize);

                // Draw color swatch
                Widgets.DrawBoxSolid(swatchRect, color);

                // Highlight selected color
                if (ColorsEqual(_selectedColor, color))
                {
                    Widgets.DrawBox(swatchRect, 3);
                }
                else
                {
                    Widgets.DrawBox(swatchRect);
                }

                // Click to select
                if (Widgets.ButtonInvisible(swatchRect))
                {
                    _selectedColor = color;
                }

                // Tooltip with color name
                TooltipHandler.TipRegion(swatchRect, colorName);

                x += ColorSwatchSize + ColorSwatchSpacing;
            }
        }

        private void DrawActionButtons(Rect rect, BookmarkManager manager)
        {
            float buttonWidth = (rect.width - 10f) / 2f;

            // Save button
            Rect saveRect = new Rect(rect.x, rect.y, buttonWidth, rect.height);
            if (Widgets.ButtonText(saveRect, "LandingZone_Save".Translate()))
            {
                SaveChanges(manager);
                Close();
            }

            // Cancel button
            Rect cancelRect = new Rect(rect.x + buttonWidth + 10f, rect.y, buttonWidth, rect.height);
            if (Widgets.ButtonText(cancelRect, "LandingZone_Cancel".Translate()))
            {
                _cancelled = true;
                Close();
            }
        }

        private void SaveChanges(BookmarkManager manager)
        {
            bool success = manager.UpdateBookmark(
                _tileId,
                label: _title,
                notes: _notes,
                color: _selectedColor,
                showTitleOnGlobe: _showTitleOnGlobe
            );

            if (success)
            {
                Messages.Message("LandingZone_BookmarkUpdated".Translate(_tileId), MessageTypeDefOf.SilentInput, false);
            }
            else
            {
                Log.Warning($"[LandingZone] Failed to update bookmark for tile {_tileId}");
            }
        }

        /// <summary>
        /// Checks if two colors are approximately equal (handles floating point precision).
        /// </summary>
        private bool ColorsEqual(Color a, Color b)
        {
            float threshold = 0.01f;
            return Mathf.Abs(a.r - b.r) < threshold &&
                   Mathf.Abs(a.g - b.g) < threshold &&
                   Mathf.Abs(a.b - b.b) < threshold &&
                   Mathf.Abs(a.a - b.a) < threshold;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace LandingZone.Data
{
    /// <summary>
    /// Logic mode for multi-select filters.
    /// </summary>
    public enum FilterLogicMode : byte
    {
        /// <summary>
        /// At least one selected item must match (OR logic).
        /// Example: "Has Granite OR Limestone"
        /// </summary>
        Any = 0,

        /// <summary>
        /// All selected items must match (AND logic).
        /// Example: "Has Granite AND Limestone"
        /// </summary>
        All = 1
    }

    /// <summary>
    /// Generic container for multi-select filters with AND/OR logic support.
    /// Provides Reset/All/None helper methods for UI convenience.
    /// </summary>
    /// <typeparam name="T">The type of items in the selection (typically string or Def).</typeparam>
    [Serializable]
    public class MultiSelectFilterContainer<T>
    {
        /// <summary>
        /// The set of selected items.
        /// </summary>
        public HashSet<T> SelectedItems { get; set; } = new HashSet<T>();

        /// <summary>
        /// Logic mode for combining selected items.
        /// </summary>
        public FilterLogicMode LogicMode { get; set; } = FilterLogicMode.Any;

        /// <summary>
        /// Gets whether any items are selected.
        /// </summary>
        public bool HasSelection => SelectedItems.Count > 0;

        /// <summary>
        /// Gets the count of selected items.
        /// </summary>
        public int Count => SelectedItems.Count;

        /// <summary>
        /// Checks if a specific item is selected.
        /// </summary>
        public bool IsSelected(T item)
        {
            return SelectedItems.Contains(item);
        }

        /// <summary>
        /// Toggles selection of an item.
        /// </summary>
        public void Toggle(T item)
        {
            if (SelectedItems.Contains(item))
                SelectedItems.Remove(item);
            else
                SelectedItems.Add(item);
        }

        /// <summary>
        /// Selects an item.
        /// </summary>
        public void Select(T item)
        {
            SelectedItems.Add(item);
        }

        /// <summary>
        /// Deselects an item.
        /// </summary>
        public void Deselect(T item)
        {
            SelectedItems.Remove(item);
        }

        /// <summary>
        /// Clears all selections (Reset).
        /// </summary>
        public void Reset()
        {
            SelectedItems.Clear();
        }

        /// <summary>
        /// Selects all items from the provided collection.
        /// </summary>
        public void SelectAll(IEnumerable<T> allItems)
        {
            SelectedItems.Clear();
            foreach (var item in allItems)
            {
                SelectedItems.Add(item);
            }
        }

        /// <summary>
        /// Toggles the logic mode between Any and All.
        /// </summary>
        public void ToggleLogicMode()
        {
            LogicMode = LogicMode == FilterLogicMode.Any ? FilterLogicMode.All : FilterLogicMode.Any;
        }

        /// <summary>
        /// Gets a human-readable description of the logic mode.
        /// </summary>
        public string GetLogicModeLabel()
        {
            return LogicMode == FilterLogicMode.Any ? "OR" : "AND";
        }

        /// <summary>
        /// Evaluates whether a collection of candidate items matches the filter criteria.
        /// </summary>
        /// <param name="candidateItems">The items to check against selection.</param>
        /// <returns>True if matches based on logic mode, false otherwise.</returns>
        public bool Matches(IEnumerable<T> candidateItems)
        {
            // If nothing is selected, always match (filter is inactive)
            if (!HasSelection)
                return true;

            var candidateSet = candidateItems as HashSet<T> ?? new HashSet<T>(candidateItems);

            return LogicMode switch
            {
                FilterLogicMode.Any => SelectedItems.Any(item => candidateSet.Contains(item)),  // At least one match
                FilterLogicMode.All => SelectedItems.All(item => candidateSet.Contains(item)),  // All must match
                _ => true
            };
        }

        /// <summary>
        /// Evaluates whether a single candidate item matches the filter criteria.
        /// Used for filters where only one value is checked at a time.
        /// </summary>
        /// <param name="candidateItem">The item to check.</param>
        /// <returns>True if matches, false otherwise.</returns>
        public bool Matches(T candidateItem)
        {
            // If nothing is selected, always match (filter is inactive)
            if (!HasSelection)
                return true;

            // For single item, just check if it's in the selection
            // Logic mode doesn't apply to single items
            return SelectedItems.Contains(candidateItem);
        }

        /// <summary>
        /// Calculates a match score (0-1) based on how many selected items are present.
        /// Useful for scoring systems rather than hard pass/fail.
        /// </summary>
        /// <param name="candidateItems">The items to check.</param>
        /// <returns>Score from 0 (no matches) to 1 (all matches).</returns>
        public float CalculateMatchScore(IEnumerable<T> candidateItems)
        {
            if (!HasSelection)
                return 1.0f;  // No filter = perfect match

            var candidateSet = candidateItems as HashSet<T> ?? new HashSet<T>(candidateItems);
            int matchCount = SelectedItems.Count(item => candidateSet.Contains(item));

            return (float)matchCount / SelectedItems.Count;
        }

        /// <summary>
        /// Creates a copy of this container.
        /// </summary>
        public MultiSelectFilterContainer<T> Clone()
        {
            return new MultiSelectFilterContainer<T>
            {
                SelectedItems = new HashSet<T>(SelectedItems),
                LogicMode = LogicMode
            };
        }

        public override string ToString()
        {
            if (!HasSelection)
                return "No selection";

            string itemsText = SelectedItems.Count <= 3
                ? string.Join(", ", SelectedItems)
                : $"{SelectedItems.Count} items";

            return $"{itemsText} ({GetLogicModeLabel()})";
        }
    }

    /// <summary>
    /// Extension methods for MultiSelectFilterContainer.
    /// </summary>
    public static class MultiSelectFilterContainerExtensions
    {
        /// <summary>
        /// Gets a display-friendly list of selected item names.
        /// </summary>
        public static string GetSelectedItemsDisplay<T>(this MultiSelectFilterContainer<T> container, int maxItems = 3)
        {
            if (!container.HasSelection)
                return "None selected";

            var items = container.SelectedItems.Take(maxItems + 1).ToList();

            if (items.Count <= maxItems)
                return string.Join(", ", items);

            return $"{string.Join(", ", items.Take(maxItems))}, +{container.Count - maxItems} more";
        }
    }
}

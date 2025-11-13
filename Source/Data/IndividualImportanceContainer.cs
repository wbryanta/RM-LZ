using System;
using System.Collections.Generic;
using System.Linq;

namespace LandingZone.Data
{
    /// <summary>
    /// Container for filters where each item can have its own importance level.
    /// Example: "Marble is Critical, Granite is Preferred, Sandstone is Ignored"
    /// Replaces the old pattern of "select items + global importance".
    /// </summary>
    /// <typeparam name="T">The type of items (typically string for defNames).</typeparam>
    [Serializable]
    public class IndividualImportanceContainer<T> where T : notnull
    {
        /// <summary>
        /// Maps each item to its importance level.
        /// Items not in this dictionary are considered Ignored.
        /// </summary>
        public Dictionary<T, FilterImportance> ItemImportance { get; set; } = new Dictionary<T, FilterImportance>();

        /// <summary>
        /// Gets the importance of a specific item.
        /// Returns Ignored if item is not set.
        /// </summary>
        public FilterImportance GetImportance(T item)
        {
            return ItemImportance.TryGetValue(item, out var importance) ? importance : FilterImportance.Ignored;
        }

        /// <summary>
        /// Sets the importance of a specific item.
        /// </summary>
        public void SetImportance(T item, FilterImportance importance)
        {
            if (importance == FilterImportance.Ignored)
            {
                // Remove from dictionary to save space
                ItemImportance.Remove(item);
            }
            else
            {
                ItemImportance[item] = importance;
            }
        }

        /// <summary>
        /// Gets all items with a specific importance level.
        /// </summary>
        public IEnumerable<T> GetItemsByImportance(FilterImportance importance)
        {
            if (importance == FilterImportance.Ignored)
            {
                // This would require knowing all possible items, which we don't have
                // Return empty for now
                return Enumerable.Empty<T>();
            }

            return ItemImportance.Where(kvp => kvp.Value == importance).Select(kvp => kvp.Key);
        }

        /// <summary>
        /// Gets all items marked as Critical.
        /// </summary>
        public IEnumerable<T> GetCriticalItems() => GetItemsByImportance(FilterImportance.Critical);

        /// <summary>
        /// Gets all items marked as Preferred.
        /// </summary>
        public IEnumerable<T> GetPreferredItems() => GetItemsByImportance(FilterImportance.Preferred);

        /// <summary>
        /// Checks if any items are set to Critical.
        /// </summary>
        public bool HasCritical => ItemImportance.Any(kvp => kvp.Value == FilterImportance.Critical);

        /// <summary>
        /// Checks if any items are set to Preferred.
        /// </summary>
        public bool HasPreferred => ItemImportance.Any(kvp => kvp.Value == FilterImportance.Preferred);

        /// <summary>
        /// Checks if any items have non-Ignored importance.
        /// </summary>
        public bool HasAnyImportance => ItemImportance.Any();

        /// <summary>
        /// Gets the count of items with non-Ignored importance.
        /// </summary>
        public int Count => ItemImportance.Count;

        /// <summary>
        /// Counts items by importance level.
        /// </summary>
        public int CountByImportance(FilterImportance importance)
        {
            if (importance == FilterImportance.Ignored)
                return 0;  // Can't count ignored without knowing all possible items

            return ItemImportance.Count(kvp => kvp.Value == importance);
        }

        /// <summary>
        /// Resets all items to Ignored (clears the dictionary).
        /// </summary>
        public void Reset()
        {
            ItemImportance.Clear();
        }

        /// <summary>
        /// Sets all provided items to the specified importance.
        /// </summary>
        public void SetAllTo(IEnumerable<T> items, FilterImportance importance)
        {
            foreach (var item in items)
            {
                SetImportance(item, importance);
            }
        }

        /// <summary>
        /// Checks if a tile's items match the Critical requirements.
        /// Returns true if all Critical items are present in tileItems.
        /// </summary>
        public bool MeetsCriticalRequirements(IEnumerable<T> tileItems)
        {
            if (!HasCritical)
                return true;  // No Critical requirements

            var tileSet = tileItems as HashSet<T> ?? new HashSet<T>(tileItems);
            return GetCriticalItems().All(criticalItem => tileSet.Contains(criticalItem));
        }

        /// <summary>
        /// Calculates how many Preferred items are present in tileItems.
        /// Returns the count of matching Preferred items.
        /// </summary>
        public int CountPreferredMatches(IEnumerable<T> tileItems)
        {
            if (!HasPreferred)
                return 0;

            var tileSet = tileItems as HashSet<T> ?? new HashSet<T>(tileItems);
            return GetPreferredItems().Count(preferredItem => tileSet.Contains(preferredItem));
        }

        /// <summary>
        /// Creates a copy of this container.
        /// </summary>
        public IndividualImportanceContainer<T> Clone()
        {
            return new IndividualImportanceContainer<T>
            {
                ItemImportance = new Dictionary<T, FilterImportance>(ItemImportance)
            };
        }

        public override string ToString()
        {
            if (!HasAnyImportance)
                return "No items configured";

            var criticalCount = CountByImportance(FilterImportance.Critical);
            var preferredCount = CountByImportance(FilterImportance.Preferred);

            var parts = new List<string>();
            if (criticalCount > 0) parts.Add($"{criticalCount} Critical");
            if (preferredCount > 0) parts.Add($"{preferredCount} Preferred");

            return string.Join(", ", parts);
        }
    }
}

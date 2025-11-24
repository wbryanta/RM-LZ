using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace LandingZone.Data
{
    /// <summary>
    /// Operator for combining multiple critical items in a filter.
    /// </summary>
    public enum ImportanceOperator
    {
        /// <summary>
        /// Tile must have ALL critical items (default for Advanced mode multi-select).
        /// Example: Rivers with AND = tile must have creek AND stream AND river AND huge river
        /// </summary>
        AND,

        /// <summary>
        /// Tile must have ANY critical item (Default mode "Any" selections).
        /// Example: Rivers with OR = tile must have creek OR stream OR river OR huge river
        /// </summary>
        OR
    }

    /// <summary>
    /// Container for filters where each item can have its own importance level.
    /// Example: "Marble is Critical, Granite is Preferred, Sandstone is Ignored"
    /// Replaces the old pattern of "select items + global importance".
    /// </summary>
    /// <typeparam name="T">The type of items (typically string for defNames).</typeparam>
    [Serializable]
    public class IndividualImportanceContainer<T> : IExposable where T : notnull
    {
        /// <summary>
        /// Maps each item to its importance level.
        /// Items not in this dictionary are considered Ignored.
        /// </summary>
        public Dictionary<T, FilterImportance> ItemImportance { get; set; } = new Dictionary<T, FilterImportance>();

        /// <summary>
        /// Operator for combining critical items.
        /// - AND (default): Tile must have ALL critical items (Advanced mode multi-select)
        /// - OR: Tile must have ANY critical item (Default mode "Any" selections)
        ///
        /// Example: Rivers set to Critical with OR operator = "tile must have creek OR stream OR river OR huge river"
        /// Example: Rivers set to Critical with AND operator = "tile must have ALL river types" (rare, but supported)
        ///
        /// Advanced UI exposes operator toggle. Default mode sets OR for "Any" selections.
        /// </summary>
        public ImportanceOperator Operator { get; set; } = ImportanceOperator.AND;

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
        /// Resets all items to Ignored (clears the dictionary) and resets operator to AND.
        /// </summary>
        public void Reset()
        {
            ItemImportance.Clear();
            Operator = ImportanceOperator.AND;
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
        /// Respects the Operator setting:
        /// - AND: Returns true if ALL critical items are present
        /// - OR: Returns true if ANY critical item is present
        /// </summary>
        public bool MeetsCriticalRequirements(IEnumerable<T> tileItems)
        {
            if (!HasCritical)
                return true;  // No Critical requirements

            var tileSet = tileItems as HashSet<T> ?? new HashSet<T>(tileItems);

            if (Operator == ImportanceOperator.OR)
            {
                // OR: Tile must have AT LEAST ONE critical item
                return GetCriticalItems().Any(criticalItem => tileSet.Contains(criticalItem));
            }
            else
            {
                // AND: Tile must have ALL critical items (original behavior)
                return GetCriticalItems().All(criticalItem => tileSet.Contains(criticalItem));
            }
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
        /// Computes proportional critical satisfaction for membership scoring.
        /// Respects the Operator setting:
        /// - OR: Returns 1.0 if ANY critical item present, 0.0 otherwise
        /// - AND: Returns fraction of critical items present (proportional)
        /// Used by Membership() methods to align with Apply() phase logic.
        /// </summary>
        public float GetCriticalSatisfaction(IEnumerable<T> tileItems)
        {
            if (!HasCritical)
                return 1.0f;  // No requirements = fully satisfied

            var tileSet = tileItems as HashSet<T> ?? new HashSet<T>(tileItems);
            var criticals = GetCriticalItems().ToList();

            if (criticals.Count == 0)
                return 1.0f;

            int matches = criticals.Count(critical => tileSet.Contains(critical));

            if (Operator == ImportanceOperator.OR)
            {
                // OR: Binary - any match = 1.0, no match = 0.0
                return matches > 0 ? 1.0f : 0.0f;
            }
            else
            {
                // AND: Proportional satisfaction (fraction of required items present)
                return (float)matches / criticals.Count;
            }
        }

        /// <summary>
        /// Creates a copy of this container.
        /// </summary>
        public IndividualImportanceContainer<T> Clone()
        {
            return new IndividualImportanceContainer<T>
            {
                ItemImportance = new Dictionary<T, FilterImportance>(ItemImportance),
                Operator = Operator
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

        /// <summary>
        /// Serialization support for RimWorld save/load system.
        /// </summary>
        public void ExposeData()
        {
            // Use local variables since Scribe requires ref to variables, not properties
            var itemImportance = ItemImportance;
            var op = Operator;

            // Serialize dictionary and operator
            Scribe_Collections.Look(ref itemImportance, "itemImportance", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref op, "operator", ImportanceOperator.AND);

            // Write back to properties after loading
            if (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ItemImportance = itemImportance ?? new Dictionary<T, FilterImportance>();
                Operator = op;
            }
        }
    }
}

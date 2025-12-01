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
        /// Gets all items marked as MustHave.
        /// </summary>
        public IEnumerable<T> GetMustHaveItems() => GetItemsByImportance(FilterImportance.MustHave);

        /// <summary>
        /// Gets all items marked as MustNotHave.
        /// </summary>
        public IEnumerable<T> GetMustNotHaveItems() => GetItemsByImportance(FilterImportance.MustNotHave);

        /// <summary>
        /// Gets all items marked as Priority.
        /// </summary>
        public IEnumerable<T> GetPriorityItems() => GetItemsByImportance(FilterImportance.Priority);

        /// <summary>
        /// Gets all items marked as Preferred.
        /// </summary>
        public IEnumerable<T> GetPreferredItems() => GetItemsByImportance(FilterImportance.Preferred);

        /// <summary>
        /// Gets all items that are hard gates (MustHave or MustNotHave).
        /// </summary>
        public IEnumerable<T> GetHardGateItems() =>
            ItemImportance.Where(kvp => kvp.Value.IsHardGate()).Select(kvp => kvp.Key);

        /// <summary>
        /// Gets all items that contribute to scoring (Priority or Preferred).
        /// </summary>
        public IEnumerable<T> GetScoringItems() =>
            ItemImportance.Where(kvp => kvp.Value.IsScoring()).Select(kvp => kvp.Key);

        /// <summary>
        /// Checks if any items are set to MustHave.
        /// </summary>
        public bool HasMustHave => ItemImportance.Any(kvp => kvp.Value == FilterImportance.MustHave);

        /// <summary>
        /// Checks if any items are set to MustNotHave.
        /// </summary>
        public bool HasMustNotHave => ItemImportance.Any(kvp => kvp.Value == FilterImportance.MustNotHave);

        /// <summary>
        /// Checks if any items are set to Priority.
        /// </summary>
        public bool HasPriority => ItemImportance.Any(kvp => kvp.Value == FilterImportance.Priority);

        /// <summary>
        /// Checks if any items are set to Preferred.
        /// </summary>
        public bool HasPreferred => ItemImportance.Any(kvp => kvp.Value == FilterImportance.Preferred);

        /// <summary>
        /// Checks if any items have hard gate importance (MustHave or MustNotHave).
        /// </summary>
        public bool HasHardGates => ItemImportance.Any(kvp => kvp.Value.IsHardGate());

        /// <summary>
        /// Checks if any items have scoring importance (Priority or Preferred).
        /// </summary>
        public bool HasScoring => ItemImportance.Any(kvp => kvp.Value.IsScoring());

        // Legacy compatibility properties and methods
        /// <summary>
        /// [LEGACY] Alias for HasMustHave. Checks if any items are set to MustHave (formerly Critical).
        /// </summary>
        public bool HasCritical => HasMustHave;

        /// <summary>
        /// [LEGACY] Alias for GetMustHaveItems. Gets all items marked as MustHave (formerly Critical).
        /// </summary>
        public IEnumerable<T> GetCriticalItems() => GetMustHaveItems();

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
        /// Demotes all MustHave items to Priority for relaxed search (car-builder fallback).
        /// MustNotHave items remain unchanged.
        /// </summary>
        public void RelaxMustHaveToPriority()
        {
            var keysToUpdate = ItemImportance
                .Where(kvp => kvp.Value == FilterImportance.MustHave)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToUpdate)
            {
                ItemImportance[key] = FilterImportance.Priority;
            }
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
        /// Checks if a tile's items match the MustHave requirements.
        /// Respects the Operator setting:
        /// - AND: Returns true if ALL MustHave items are present
        /// - OR: Returns true if ANY MustHave item is present
        /// </summary>
        public bool MeetsMustHaveRequirements(IEnumerable<T> tileItems)
        {
            if (!HasMustHave)
                return true;  // No MustHave requirements

            var tileSet = tileItems as HashSet<T> ?? new HashSet<T>(tileItems);

            if (Operator == ImportanceOperator.OR)
            {
                // OR: Tile must have AT LEAST ONE MustHave item
                return GetMustHaveItems().Any(item => tileSet.Contains(item));
            }
            else
            {
                // AND: Tile must have ALL MustHave items
                return GetMustHaveItems().All(item => tileSet.Contains(item));
            }
        }

        /// <summary>
        /// Checks if a tile's items satisfy the MustNotHave requirements.
        /// Returns false if any MustNotHave item is present in tileItems.
        /// </summary>
        public bool MeetsMustNotHaveRequirements(IEnumerable<T> tileItems)
        {
            if (!HasMustNotHave)
                return true;  // No MustNotHave requirements

            var tileSet = tileItems as HashSet<T> ?? new HashSet<T>(tileItems);

            // Tile must NOT have ANY of the MustNotHave items
            return !GetMustNotHaveItems().Any(item => tileSet.Contains(item));
        }

        /// <summary>
        /// Checks if a tile passes all hard gate requirements (MustHave AND MustNotHave).
        /// </summary>
        public bool MeetsHardGateRequirements(IEnumerable<T> tileItems)
        {
            return MeetsMustHaveRequirements(tileItems) && MeetsMustNotHaveRequirements(tileItems);
        }

        /// <summary>
        /// [LEGACY] Alias for MeetsMustHaveRequirements.
        /// </summary>
        public bool MeetsCriticalRequirements(IEnumerable<T> tileItems) => MeetsMustHaveRequirements(tileItems);

        /// <summary>
        /// Calculates how many Preferred items are present in tileItems.
        /// Returns the count of matching Preferred items.
        /// </summary>
        public int CountPreferredMatches(IEnumerable<T> tileItems)
        {
            if (!HasPreferred)
                return 0;

            var tileSet = tileItems as HashSet<T> ?? new HashSet<T>(tileItems);
            return GetPreferredItems().Count(item => tileSet.Contains(item));
        }

        /// <summary>
        /// Calculates how many Priority items are present in tileItems.
        /// Returns the count of matching Priority items.
        /// </summary>
        public int CountPriorityMatches(IEnumerable<T> tileItems)
        {
            if (!HasPriority)
                return 0;

            var tileSet = tileItems as HashSet<T> ?? new HashSet<T>(tileItems);
            return GetPriorityItems().Count(item => tileSet.Contains(item));
        }

        /// <summary>
        /// Calculates weighted score for scoring items (Priority and Preferred).
        /// Priority items have higher weight than Preferred items.
        /// </summary>
        /// <param name="tileItems">Items present on the tile.</param>
        /// <param name="priorityWeight">Weight multiplier for Priority items (default 2.0).</param>
        /// <param name="preferredWeight">Weight multiplier for Preferred items (default 1.0).</param>
        /// <returns>Weighted score based on matching items.</returns>
        public float GetScoringScore(IEnumerable<T> tileItems, float priorityWeight = 2.0f, float preferredWeight = 1.0f)
        {
            if (!HasScoring)
                return 0f;

            var tileSet = tileItems as HashSet<T> ?? new HashSet<T>(tileItems);
            float score = 0f;

            foreach (var item in GetPriorityItems())
            {
                if (tileSet.Contains(item))
                    score += priorityWeight;
            }

            foreach (var item in GetPreferredItems())
            {
                if (tileSet.Contains(item))
                    score += preferredWeight;
            }

            return score;
        }

        /// <summary>
        /// Computes proportional MustHave satisfaction for membership scoring.
        /// Respects the Operator setting:
        /// - OR: Returns 1.0 if ANY MustHave item present, 0.0 otherwise
        /// - AND: Returns fraction of MustHave items present (proportional)
        /// Used by Membership() methods to align with Apply() phase logic.
        /// </summary>
        public float GetMustHaveSatisfaction(IEnumerable<T> tileItems)
        {
            if (!HasMustHave)
                return 1.0f;  // No requirements = fully satisfied

            var tileSet = tileItems as HashSet<T> ?? new HashSet<T>(tileItems);
            var mustHaves = GetMustHaveItems().ToList();

            if (mustHaves.Count == 0)
                return 1.0f;

            int matches = mustHaves.Count(item => tileSet.Contains(item));

            if (Operator == ImportanceOperator.OR)
            {
                // OR: Binary - any match = 1.0, no match = 0.0
                return matches > 0 ? 1.0f : 0.0f;
            }
            else
            {
                // AND: Proportional satisfaction (fraction of required items present)
                return (float)matches / mustHaves.Count;
            }
        }

        /// <summary>
        /// [LEGACY] Alias for GetMustHaveSatisfaction.
        /// </summary>
        public float GetCriticalSatisfaction(IEnumerable<T> tileItems) => GetMustHaveSatisfaction(tileItems);

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

            var mustHaveCount = CountByImportance(FilterImportance.MustHave);
            var mustNotHaveCount = CountByImportance(FilterImportance.MustNotHave);
            var priorityCount = CountByImportance(FilterImportance.Priority);
            var preferredCount = CountByImportance(FilterImportance.Preferred);

            var parts = new List<string>();
            if (mustHaveCount > 0) parts.Add($"{mustHaveCount} MustHave");
            if (mustNotHaveCount > 0) parts.Add($"{mustNotHaveCount} MustNotHave");
            if (priorityCount > 0) parts.Add($"{priorityCount} Priority");
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

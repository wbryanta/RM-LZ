#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using LandingZone.Data;
using UnityEngine;
using Verse;

namespace LandingZone.Core.UI
{
    /// <summary>
    /// Manages the 4-bucket workspace for Advanced mode filter configuration.
    /// Buckets: Must Have (gate), Must NOT Have (gate), Priority (2x weight), Preferred (1x weight).
    /// Each bucket contains clauses that are ORed together; within a clause, items are ANDed.
    /// Supports OR grouping within clauses for "any of" logic.
    /// </summary>
    public class BucketWorkspace
    {
        // Bucket definitions - labels/tooltips are localization keys resolved at render time
        public static readonly ImportanceBucket[] AllBuckets = new[]
        {
            new ImportanceBucket(FilterImportance.MustHave, "LandingZone_Workspace_Bucket_MustHave", "LandingZone_Workspace_Bucket_MustHaveTooltip", new Color(0.95f, 0.35f, 0.35f)),
            new ImportanceBucket(FilterImportance.MustNotHave, "LandingZone_Workspace_Bucket_MustNotHave", "LandingZone_Workspace_Bucket_MustNotHaveTooltip", new Color(0.8f, 0.4f, 0.8f)),
            new ImportanceBucket(FilterImportance.Priority, "LandingZone_Workspace_Bucket_Priority", "LandingZone_Workspace_Bucket_PriorityTooltip", new Color(0.4f, 0.7f, 0.95f)),
            new ImportanceBucket(FilterImportance.Preferred, "LandingZone_Workspace_Bucket_Preferred", "LandingZone_Workspace_Bucket_PreferredTooltip", new Color(0.5f, 0.7f, 0.5f))
        };

        /// <summary>
        /// Represents a bucket that holds filter chips.
        /// </summary>
        public class ImportanceBucket
        {
            public FilterImportance Importance { get; }
            public string LabelKey { get; }
            public string TooltipKey { get; }
            public Color Color { get; }

            /// <summary>
            /// Gets the localized label for this bucket.
            /// </summary>
            public string Label => LabelKey.Translate();

            /// <summary>
            /// Gets the localized tooltip for this bucket.
            /// </summary>
            public string Tooltip => TooltipKey.Translate();

            public ImportanceBucket(FilterImportance importance, string labelKey, string tooltipKey, Color color)
            {
                Importance = importance;
                LabelKey = labelKey;
                TooltipKey = tooltipKey;
                Color = color;
            }
        }

        /// <summary>
        /// Represents a filter chip in the workspace.
        /// </summary>
        public class FilterChip
        {
            public string FilterId { get; }
            public string Label { get; }
            public string? ValueDisplay { get; set; }
            public bool IsHeavy { get; }
            public string Category { get; }
            public int? OrGroupId { get; set; }
            public int ClauseId { get; set; }

            public FilterChip(string filterId, string label, bool isHeavy, string category, string? valueDisplay = null)
            {
                FilterId = filterId;
                Label = label;
                IsHeavy = isHeavy;
                Category = category;
                ValueDisplay = valueDisplay;
                OrGroupId = null;
                ClauseId = 0;
            }
        }

        /// <summary>
        /// Represents an OR group within a clause.
        /// Chips in an OR group are combined with OR logic; groups AND together.
        /// </summary>
        public class OrGroup
        {
            public int GroupId { get; }
            public int ClauseId { get; set; }
            public List<FilterChip> Chips { get; } = new();

            public OrGroup(int groupId)
            {
                GroupId = groupId;
            }

            public string GetDisplayLabel()
            {
                if (Chips.Count == 0) return "LandingZone_Workspace_OrGroup_Empty".Translate();
                if (Chips.Count == 1) return GetChipDisplayText(Chips[0]);
                return "LandingZone_Workspace_OrGroup_AnyOf".Translate(string.Join(" | ", Chips.Select(c => GetChipDisplayText(c))));
            }

            private static string GetChipDisplayText(FilterChip chip)
            {
                if (string.IsNullOrEmpty(chip.ValueDisplay))
                    return chip.Label;
                return $"{chip.Label} ({chip.ValueDisplay})";
            }
        }

        /// <summary>
        /// Represents a clause within a bucket.
        /// Clauses are ORed together; items within a clause are ANDed.
        /// </summary>
        public class Clause
        {
            public int ClauseId { get; }
            public FilterImportance BucketImportance { get; }
            public List<FilterChip> Chips { get; } = new();

            public Clause(int clauseId, FilterImportance importance)
            {
                ClauseId = clauseId;
                BucketImportance = importance;
            }

            public bool IsEmpty => Chips.Count == 0;

            public string GetDisplayLabel(int clauseNumber)
            {
                return "LandingZone_Workspace_Clause_Label".Translate(clauseNumber);
            }
        }

        // State
        private int _nextOrGroupId = 1;
        private int _nextClauseId = 1;
        private readonly Dictionary<FilterImportance, List<Clause>> _bucketClauses = new();
        private readonly Dictionary<int, OrGroup> _orGroups = new();

        /// <summary>
        /// Indicates if the workspace has changed since last sync.
        /// Set by mutation methods, cleared by ClearDirty().
        /// </summary>
        public bool IsDirty { get; private set; }

        /// <summary>
        /// Clears the dirty flag. Called after syncing to FilterSettings.
        /// </summary>
        public void ClearDirty() => IsDirty = false;

        public BucketWorkspace()
        {
            // Initialize buckets with one empty clause each
            foreach (var bucket in AllBuckets)
            {
                _bucketClauses[bucket.Importance] = new List<Clause>
                {
                    new Clause(_nextClauseId++, bucket.Importance)
                };
            }
        }

        /// <summary>
        /// Gets all clauses in a bucket.
        /// </summary>
        public IReadOnlyList<Clause> GetClausesInBucket(FilterImportance importance)
        {
            return _bucketClauses.TryGetValue(importance, out var clauses) ? clauses : new List<Clause>();
        }

        /// <summary>
        /// Gets a specific clause by ID.
        /// </summary>
        public Clause? GetClause(int clauseId)
        {
            foreach (var clauses in _bucketClauses.Values)
            {
                var clause = clauses.FirstOrDefault(c => c.ClauseId == clauseId);
                if (clause != null) return clause;
            }
            return null;
        }

        /// <summary>
        /// Adds a new clause to a bucket.
        /// </summary>
        public Clause AddClause(FilterImportance importance)
        {
            if (!_bucketClauses.ContainsKey(importance))
                _bucketClauses[importance] = new List<Clause>();

            var clause = new Clause(_nextClauseId++, importance);
            _bucketClauses[importance].Add(clause);
            IsDirty = true;
            return clause;
        }

        /// <summary>
        /// Removes a clause from a bucket. If it's the last clause, keeps it but empties it.
        /// </summary>
        public void RemoveClause(int clauseId)
        {
            foreach (var kvp in _bucketClauses)
            {
                var clause = kvp.Value.FirstOrDefault(c => c.ClauseId == clauseId);
                if (clause != null)
                {
                    // Remove all chips from the clause first
                    foreach (var chip in clause.Chips.ToList())
                    {
                        if (chip.OrGroupId.HasValue)
                        {
                            RemoveFromOrGroup(chip.FilterId);
                        }
                    }
                    clause.Chips.Clear();

                    // If more than one clause, remove it; otherwise keep the empty clause
                    if (kvp.Value.Count > 1)
                    {
                        kvp.Value.Remove(clause);
                    }
                    IsDirty = true;
                    return;
                }
            }
        }

        /// <summary>
        /// Gets all chips in a bucket across all clauses.
        /// </summary>
        public IReadOnlyList<FilterChip> GetChipsInBucket(FilterImportance importance)
        {
            if (!_bucketClauses.TryGetValue(importance, out var clauses))
                return new List<FilterChip>();

            return clauses.SelectMany(c => c.Chips).ToList();
        }

        /// <summary>
        /// Gets all chips in a specific clause.
        /// </summary>
        public IReadOnlyList<FilterChip> GetChipsInClause(int clauseId)
        {
            var clause = GetClause(clauseId);
            return clause?.Chips ?? new List<FilterChip>();
        }

        /// <summary>
        /// Adds a chip to a specific clause.
        /// </summary>
        public void AddChipToClause(FilterChip chip, int clauseId)
        {
            // Remove from any existing location first
            RemoveChip(chip.FilterId);

            var clause = GetClause(clauseId);
            if (clause != null)
            {
                chip.ClauseId = clauseId;
                clause.Chips.Add(chip);
                IsDirty = true;
            }
        }

        /// <summary>
        /// Adds a chip to the first clause of a bucket (for backward compatibility).
        /// </summary>
        public void AddChip(FilterChip chip, FilterImportance importance)
        {
            var clauses = GetClausesInBucket(importance);
            if (clauses.Count == 0)
            {
                AddClause(importance);
                clauses = GetClausesInBucket(importance);
            }

            AddChipToClause(chip, clauses[0].ClauseId);
        }

        /// <summary>
        /// Moves a chip to a different bucket (first clause of target bucket).
        /// </summary>
        public void MoveChip(string filterId, FilterImportance newImportance)
        {
            var (chip, _, _) = FindChip(filterId);
            if (chip == null) return;

            // Remove from current location (handles OR group cleanup)
            RemoveChip(filterId);

            // Add to first clause of new bucket
            var targetClauses = GetClausesInBucket(newImportance);
            if (targetClauses.Count == 0)
            {
                AddClause(newImportance);
                targetClauses = GetClausesInBucket(newImportance);
            }

            chip.ClauseId = targetClauses[0].ClauseId;
            chip.OrGroupId = null;
            targetClauses[0].Chips.Add(chip);
            IsDirty = true;
        }

        /// <summary>
        /// Moves a chip to a specific clause.
        /// </summary>
        public void MoveChipToClause(string filterId, int targetClauseId)
        {
            var (chip, sourceClause, _) = FindChip(filterId);
            if (chip == null || sourceClause == null) return;

            var targetClause = GetClause(targetClauseId);
            if (targetClause == null) return;

            // Same clause - no action
            if (sourceClause.ClauseId == targetClauseId) return;

            // Remove from OR group if moving clauses
            if (chip.OrGroupId.HasValue)
            {
                RemoveFromOrGroup(filterId);
            }

            sourceClause.Chips.Remove(chip);
            chip.ClauseId = targetClauseId;
            targetClause.Chips.Add(chip);
            IsDirty = true;
        }

        /// <summary>
        /// Finds a chip and its containing clause.
        /// </summary>
        public (FilterChip? Chip, Clause? Clause, FilterImportance? Importance) FindChip(string filterId)
        {
            foreach (var kvp in _bucketClauses)
            {
                foreach (var clause in kvp.Value)
                {
                    var chip = clause.Chips.FirstOrDefault(c => c.FilterId == filterId);
                    if (chip != null)
                    {
                        return (chip, clause, kvp.Key);
                    }
                }
            }
            return (null, null, null);
        }

        /// <summary>
        /// Removes a chip from the workspace entirely.
        /// </summary>
        public void RemoveChip(string filterId)
        {
            foreach (var clauses in _bucketClauses.Values)
            {
                foreach (var clause in clauses)
                {
                    var chip = clause.Chips.FirstOrDefault(c => c.FilterId == filterId);
                    if (chip != null)
                    {
                        if (chip.OrGroupId.HasValue)
                        {
                            RemoveFromOrGroup(filterId);
                        }
                        clause.Chips.Remove(chip);
                        IsDirty = true;
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Creates an OR group from selected chips within the same clause.
        /// </summary>
        public int CreateOrGroup(IEnumerable<string> filterIds)
        {
            var groupId = _nextOrGroupId++;
            var group = new OrGroup(groupId);
            _orGroups[groupId] = group;

            int? clauseId = null;

            foreach (var filterId in filterIds)
            {
                var (chip, clause, _) = FindChip(filterId);
                if (chip != null && clause != null)
                {
                    // Ensure all chips are in the same clause
                    if (clauseId == null)
                    {
                        clauseId = clause.ClauseId;
                        group.ClauseId = clause.ClauseId;
                    }
                    else if (clause.ClauseId != clauseId)
                    {
                        continue; // Skip chips from different clauses
                    }

                    chip.OrGroupId = groupId;
                    group.Chips.Add(chip);
                }
            }

            IsDirty = true;
            return groupId;
        }

        /// <summary>
        /// Removes a chip from its OR group.
        /// </summary>
        public void RemoveFromOrGroup(string filterId)
        {
            var (chip, _, _) = FindChip(filterId);
            if (chip != null && chip.OrGroupId.HasValue)
            {
                var groupId = chip.OrGroupId.Value;
                chip.OrGroupId = null;

                if (_orGroups.TryGetValue(groupId, out var group))
                {
                    group.Chips.Remove(chip);
                    // If group has 1 or fewer chips, dissolve it
                    if (group.Chips.Count <= 1)
                    {
                        foreach (var remaining in group.Chips)
                        {
                            remaining.OrGroupId = null;
                        }
                        _orGroups.Remove(groupId);
                    }
                }
                IsDirty = true;
            }
        }

        /// <summary>
        /// Gets the OR group by ID.
        /// </summary>
        public OrGroup? GetOrGroup(int groupId)
        {
            return _orGroups.TryGetValue(groupId, out var group) ? group : null;
        }

        /// <summary>
        /// Gets all OR groups in a clause.
        /// </summary>
        public IEnumerable<OrGroup> GetOrGroupsInClause(int clauseId)
        {
            var clause = GetClause(clauseId);
            if (clause == null) return Enumerable.Empty<OrGroup>();

            var groupIds = clause.Chips.Where(c => c.OrGroupId.HasValue).Select(c => c.OrGroupId!.Value).Distinct();
            return groupIds.Select(id => _orGroups.TryGetValue(id, out var g) ? g : null).Where(g => g != null)!;
        }

        /// <summary>
        /// Gets all OR groups in a bucket (across all clauses).
        /// </summary>
        public IEnumerable<OrGroup> GetOrGroupsInBucket(FilterImportance importance)
        {
            var chips = GetChipsInBucket(importance);
            var groupIds = chips.Where(c => c.OrGroupId.HasValue).Select(c => c.OrGroupId!.Value).Distinct();
            return groupIds.Select(id => _orGroups.TryGetValue(id, out var g) ? g : null).Where(g => g != null)!;
        }

        /// <summary>
        /// Checks if a chip is in the workspace.
        /// </summary>
        public bool ContainsChip(string filterId)
        {
            return FindChip(filterId).Chip != null;
        }

        /// <summary>
        /// Gets the importance/bucket for a chip, or null if not in workspace.
        /// </summary>
        public FilterImportance? GetChipImportance(string filterId)
        {
            return FindChip(filterId).Importance;
        }

        /// <summary>
        /// Gets the clause ID for a chip, or null if not in workspace.
        /// </summary>
        public int? GetChipClauseId(string filterId)
        {
            return FindChip(filterId).Clause?.ClauseId;
        }

        /// <summary>
        /// Generates a plain-text logic summary of the current configuration.
        /// </summary>
        public string GetLogicSummary()
        {
            var parts = new List<string>();

            // Must Have
            var mustHave = FormatBucketLogic(FilterImportance.MustHave);
            if (!string.IsNullOrEmpty(mustHave))
                parts.Add("LandingZone_Workspace_LogicSummary_Require".Translate(mustHave));

            // Must NOT Have
            var mustNot = FormatBucketLogic(FilterImportance.MustNotHave);
            if (!string.IsNullOrEmpty(mustNot))
                parts.Add("LandingZone_Workspace_LogicSummary_Exclude".Translate(mustNot));

            // Priority
            var priority = FormatBucketLogic(FilterImportance.Priority);
            if (!string.IsNullOrEmpty(priority))
                parts.Add("LandingZone_Workspace_LogicSummary_Priority".Translate(priority));

            // Preferred
            var preferred = FormatBucketLogic(FilterImportance.Preferred);
            if (!string.IsNullOrEmpty(preferred))
                parts.Add("LandingZone_Workspace_LogicSummary_Preferred".Translate(preferred));

            return parts.Count > 0 ? string.Join("; ", parts) : (string)"LandingZone_Workspace_LogicSummary_Empty".Translate();
        }

        private string FormatBucketLogic(FilterImportance importance)
        {
            var clauses = GetClausesInBucket(importance);
            var nonEmptyClauses = clauses.Where(c => c.Chips.Count > 0).ToList();

            if (nonEmptyClauses.Count == 0) return "";

            var clauseStrings = new List<string>();

            foreach (var clause in nonEmptyClauses)
            {
                var clauseLogic = FormatClauseLogic(clause);
                if (!string.IsNullOrEmpty(clauseLogic))
                {
                    clauseStrings.Add(clauseLogic);
                }
            }

            if (clauseStrings.Count == 0) return "";
            if (clauseStrings.Count == 1) return clauseStrings[0];

            // Multiple clauses - wrap each in parens and OR them
            return string.Join(" OR ", clauseStrings.Select(s => $"({s})"));
        }

        private string FormatClauseLogic(Clause clause)
        {
            if (clause.Chips.Count == 0) return "";

            var groups = GetOrGroupsInClause(clause.ClauseId).ToList();
            var ungroupedChips = clause.Chips.Where(c => !c.OrGroupId.HasValue).ToList();

            var terms = new List<string>();

            // Add OR groups as parenthesized terms
            foreach (var group in groups)
            {
                if (group.Chips.Count > 1)
                    terms.Add($"({string.Join(" OR ", group.Chips.Select(c => GetChipDisplayText(c)))})");
                else if (group.Chips.Count == 1)
                    terms.Add(GetChipDisplayText(group.Chips[0]));
            }

            // Add ungrouped chips
            terms.AddRange(ungroupedChips.Select(c => GetChipDisplayText(c)));

            return string.Join(" AND ", terms);
        }

        /// <summary>
        /// Gets the full display text for a chip, including its value display if present.
        /// Example: "Hilliness" + "Mtn" => "Hilliness (Mtn)"
        /// </summary>
        private static string GetChipDisplayText(FilterChip chip)
        {
            if (string.IsNullOrEmpty(chip.ValueDisplay))
                return chip.Label;
            return $"{chip.Label} ({chip.ValueDisplay})";
        }

        /// <summary>
        /// Syncs the workspace state to FilterSettings.
        /// NOTE: This method is not called - actual syncing happens via:
        /// 1. SyncSettingsFromWorkspace() in AdvancedModeUI_Workspace.cs for simple filters
        /// 2. SyncMutatorFromWorkspace() for MapFeatures container (individual mutator chips)
        /// 3. Popup dialogs for Rivers/Stones/etc. containers (direct FilterSettings modification)
        /// </summary>
        public void SyncToFilterSettings(FilterSettings settings)
        {
            // Stub - kept for potential future use if workspace chip dragging is extended
            // to support multi-item containers like Rivers, Stones, etc.
        }

        /// <summary>
        /// Loads workspace state from FilterSettings.
        /// </summary>
        public void LoadFromFilterSettings(FilterSettings settings)
        {
            // Clear current state
            foreach (var clauses in _bucketClauses.Values)
            {
                foreach (var clause in clauses)
                    clause.Chips.Clear();
            }
            _orGroups.Clear();

            // Load filters based on their current importance in settings
            // This syncs the workspace with the underlying filter state
        }

        /// <summary>
        /// Gets chips organized for rendering within a clause (grouped chips together).
        /// </summary>
        public IEnumerable<object> GetRenderableItemsInClause(int clauseId)
        {
            var clause = GetClause(clauseId);
            if (clause == null) yield break;

            var renderedGroupIds = new HashSet<int>();

            foreach (var chip in clause.Chips)
            {
                if (chip.OrGroupId.HasValue)
                {
                    if (!renderedGroupIds.Contains(chip.OrGroupId.Value))
                    {
                        renderedGroupIds.Add(chip.OrGroupId.Value);
                        if (_orGroups.TryGetValue(chip.OrGroupId.Value, out var group))
                        {
                            yield return group;
                        }
                    }
                }
                else
                {
                    yield return chip;
                }
            }
        }

        /// <summary>
        /// Gets chips organized for rendering (grouped chips together) - backward compatible.
        /// Returns all items across all clauses in a bucket.
        /// </summary>
        public IEnumerable<object> GetRenderableItems(FilterImportance importance)
        {
            var clauses = GetClausesInBucket(importance);
            foreach (var clause in clauses)
            {
                foreach (var item in GetRenderableItemsInClause(clause.ClauseId))
                {
                    yield return item;
                }
            }
        }

        /// <summary>
        /// Gets the number of non-empty clauses in a bucket.
        /// </summary>
        public int GetNonEmptyClauseCount(FilterImportance importance)
        {
            return GetClausesInBucket(importance).Count(c => c.Chips.Count > 0);
        }
    }
}

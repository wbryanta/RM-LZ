using System;
using UnityEngine;

namespace LandingZone.Data
{
    /// <summary>
    /// Represents a filter with three states: On, Off, or Partial.
    /// Similar to Prepare Landing's tri-state filters with visual indicators.
    /// </summary>
    public enum FilterState : byte
    {
        /// <summary>
        /// Filter is disabled/off - criterion is ignored.
        /// </summary>
        Off = 0,

        /// <summary>
        /// Filter is partially enabled - some flexibility allowed.
        /// </summary>
        Partial = 1,

        /// <summary>
        /// Filter is fully enabled/on - criterion must match.
        /// </summary>
        On = 2
    }

    /// <summary>
    /// Base class for tri-state filters that can be On, Off, or Partial.
    /// Provides visual state management and serialization support.
    /// </summary>
    [Serializable]
    public abstract class TriStateFilter
    {
        /// <summary>
        /// Current state of the filter.
        /// </summary>
        public FilterState State { get; set; } = FilterState.Off;

        /// <summary>
        /// Gets whether the filter is active (On or Partial).
        /// </summary>
        public bool IsActive => State != FilterState.Off;

        /// <summary>
        /// Gets whether the filter is fully enabled (On).
        /// </summary>
        public bool IsFullyEnabled => State == FilterState.On;

        /// <summary>
        /// Gets whether the filter is partially enabled.
        /// </summary>
        public bool IsPartial => State == FilterState.Partial;

        /// <summary>
        /// Gets the display color for this filter state.
        /// </summary>
        public Color GetStateColor()
        {
            return State switch
            {
                FilterState.On => new Color(0.29f, 0.95f, 0.29f),      // Bright green
                FilterState.Partial => new Color(0.95f, 0.9f, 0.3f),   // Yellow
                FilterState.Off => new Color(0.5f, 0.5f, 0.5f),        // Grey
                _ => Color.white
            };
        }

        /// <summary>
        /// Gets the display icon for this filter state.
        /// </summary>
        public string GetStateIcon()
        {
            return State switch
            {
                FilterState.On => "✓",      // Check mark
                FilterState.Partial => "~",  // Wave/tilde (like PL's yellow wave)
                FilterState.Off => "✗",      // X mark
                _ => "?"
            };
        }

        /// <summary>
        /// Cycles to the next state (Off -> Partial -> On -> Off).
        /// </summary>
        public void CycleState()
        {
            State = State switch
            {
                FilterState.Off => FilterState.Partial,
                FilterState.Partial => FilterState.On,
                FilterState.On => FilterState.Off,
                _ => FilterState.Off
            };
        }

        /// <summary>
        /// Resets the filter to Off state.
        /// </summary>
        public virtual void Reset()
        {
            State = FilterState.Off;
        }

        /// <summary>
        /// Gets a human-readable description of the current state.
        /// </summary>
        public string GetStateDescription()
        {
            return State switch
            {
                FilterState.On => "Required",
                FilterState.Partial => "Preferred",
                FilterState.Off => "Ignored",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Evaluates whether a boolean value matches the filter criteria.
        /// </summary>
        /// <param name="hasFeature">Whether the tile has the feature.</param>
        /// <returns>True if matches filter criteria, false otherwise.</returns>
        public bool Matches(bool hasFeature)
        {
            return State switch
            {
                FilterState.Off => true,           // Ignored - always matches
                FilterState.Partial => true,       // Partial - always matches (but may affect scoring)
                FilterState.On => hasFeature,      // On - must have the feature
                _ => true
            };
        }

        /// <summary>
        /// Calculates a score multiplier based on filter state and whether feature is present.
        /// Used for scoring systems where partial matches affect score but don't eliminate tiles.
        /// </summary>
        /// <param name="hasFeature">Whether the tile has the feature.</param>
        /// <param name="partialPenalty">Penalty to apply when feature is missing in Partial state (0-1).</param>
        /// <returns>Score multiplier (0-1).</returns>
        public float GetScoreMultiplier(bool hasFeature, float partialPenalty = 0.2f)
        {
            return State switch
            {
                FilterState.Off => 1.0f,                            // No effect on score
                FilterState.Partial => hasFeature ? 1.0f : (1.0f - partialPenalty),  // Small penalty if missing
                FilterState.On => hasFeature ? 1.0f : 0.0f,         // Eliminate if missing
                _ => 1.0f
            };
        }
    }

    /// <summary>
    /// Concrete tri-state filter for boolean features (e.g., has cave, is coastal, has river).
    /// </summary>
    [Serializable]
    public class BooleanTriStateFilter : TriStateFilter
    {
        /// <summary>
        /// The name of the feature this filter checks.
        /// </summary>
        public string FeatureName { get; set; }

        public BooleanTriStateFilter(string featureName)
        {
            FeatureName = featureName;
        }

        public override string ToString()
        {
            return $"{FeatureName}: {GetStateDescription()}";
        }
    }
}

using System.Collections;
using LandingZone.Data;

namespace LandingZone.Core.Filtering
{
    /// <summary>
    /// Abstraction for k-of-n symmetric filter evaluation.
    /// Predicates return which tiles match their criteria, allowing
    /// aggregate counting instead of sequential elimination.
    /// </summary>
    public interface IFilterPredicate
    {
        /// <summary>
        /// Unique identifier for this predicate.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Filter importance level (Critical, Preferred, Ignored).
        /// </summary>
        FilterImportance Importance { get; }

        /// <summary>
        /// Whether this predicate requires expensive TileDataCache access.
        /// </summary>
        bool IsHeavy { get; }

        /// <summary>
        /// Evaluates which tiles match this predicate.
        /// Returns a BitArray where bit[i] = true if tile i matches.
        /// </summary>
        /// <param name="context">Filter context with game state and cache</param>
        /// <param name="tileCount">Total number of tiles to evaluate</param>
        /// <returns>BitArray indicating which tiles match</returns>
        BitArray Evaluate(FilterContext context, int tileCount);

        /// <summary>
        /// Human-readable description of what this predicate filters for.
        /// </summary>
        string Describe(FilterContext context);
    }
}

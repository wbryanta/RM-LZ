using System.Collections.Generic;
using LandingZone.Core.Filtering;
using LandingZone.Data;
using UnityEngine;

namespace LandingZone.Core.Highlighting
{
    public sealed class HighlightService
    {
        private readonly FilterService _filters;
        private readonly HeatmapOverlay _overlay = new();
        private readonly HighlightState _state = new();
        private readonly List<TileScore> _scores = new();
        private readonly List<TileScore> _topMatches = new();

        private HighlightService(FilterService filters)
        {
            _filters = filters;
        }

        public static HighlightService Create(FilterService filters) => new HighlightService(filters);

        public IReadOnlyList<TileScore> Update(GameState state, IReadOnlyList<TileScore>? cachedScores = null)
        {
            var results = cachedScores ?? _filters.Evaluate(state);
            _scores.Clear();
            _scores.AddRange(results);
            _topMatches.Clear();
            var limit = Mathf.Clamp(state.Preferences.Filters.MaxResults, 1, FilterSettings.MaxResultsLimit);
            var topCount = Mathf.Min(limit, _scores.Count);
            for (var i = 0; i < topCount; i++)
            {
                _topMatches.Add(_scores[i]);
            }
            var colors = _overlay.BuildColors(_scores);
            _state.Update(colors, _topMatches);
            return _scores;
        }

        public IReadOnlyList<TileScore> Scores => _scores;
        public HighlightState State => _state;
    }
}

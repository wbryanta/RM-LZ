#nullable enable
using System.Diagnostics;
using LandingZone.Data;
using Verse;

namespace LandingZone.Core
{
    /// <summary>
    /// GameComponent that pre-caches all tile data on world load.
    /// Processes tiles incrementally in chunks to avoid UI freeze.
    /// Chunk size is tuned for responsive UI (500-1000 tiles per frame).
    /// </summary>
    public class TileCachePrecomputationComponent : GameComponent
    {
        /// <summary>
        /// Tiles processed per frame. Tuned for UI responsiveness on large worlds.
        /// Growing days calculation is ~2-3ms per tile, so 500 tiles ≈ 1-1.5 seconds per frame.
        /// </summary>
        private const int TilesPerFrame = 500;

        /// <summary>
        /// Log progress every N tiles to show the user it's still working.
        /// </summary>
        private const int ProgressLogInterval = 50000;

        private TileDataCache? _cache;
        private int _totalTiles;
        private int _processedTiles;
        private int _lastProgressLog;
        private bool _completed;
        private Stopwatch? _stopwatch;

        public TileCachePrecomputationComponent(Game game)
        {
        }

        /// <summary>
        /// Starts the pre-caching process.
        /// </summary>
        public void StartPrecomputation(TileDataCache cache)
        {
            _cache = cache;
            _totalTiles = Find.WorldGrid?.TilesCount ?? 0;
            _processedTiles = 0;
            _lastProgressLog = 0;
            _completed = false;
            _stopwatch = Stopwatch.StartNew();

            Log.Message($"[LandingZone] Tile cache precomputation START: {_totalTiles} tiles, chunk size {TilesPerFrame}");
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            // Only process if we have work to do
            if (_completed || _cache == null || _totalTiles == 0)
                return;

            // Process a chunk of tiles
            int startTile = _processedTiles;
            int endTile = UnityEngine.Mathf.Min(_processedTiles + TilesPerFrame, _totalTiles);

            for (int tileId = startTile; tileId < endTile; tileId++)
            {
                // This triggers GetOrCompute which caches the tile data
                _cache.GetOrCompute(tileId);
            }

            _processedTiles = endTile;

            // Log progress periodically for large worlds
            if (_processedTiles - _lastProgressLog >= ProgressLogInterval)
            {
                _lastProgressLog = _processedTiles;
                var elapsedSec = (_stopwatch?.ElapsedMilliseconds ?? 0) / 1000f;
                Log.Message($"[LandingZone] Tile cache precomputation: {_processedTiles}/{_totalTiles} ({Progress:P0}) - {elapsedSec:F1}s elapsed");
            }

            // Update status through LandingZoneContext
            LandingZoneContext.UpdateTileCacheStatus(Progress, _processedTiles, false);

            // Check if we're done
            if (_processedTiles >= _totalTiles)
            {
                _completed = true;
                _stopwatch?.Stop();
                var elapsedMs = _stopwatch?.ElapsedMilliseconds ?? 0;
                Log.Message($"[LandingZone] Tile cache precomputation COMPLETE: {_totalTiles} tiles cached in {elapsedMs}ms ({elapsedMs / 1000f:F1}s)");
                LandingZoneContext.UpdateTileCacheStatus(1f, _processedTiles, true);
            }
        }

        public float Progress => _totalTiles == 0 ? 1f : _processedTiles / (float)_totalTiles;
        public bool IsCompleted => _completed;
        public int ProcessedTiles => _processedTiles;
        public int TotalTiles => _totalTiles;
    }
}

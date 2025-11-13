using System.Diagnostics;
using LandingZone.Data;
using Verse;

namespace LandingZone.Core
{
    /// <summary>
    /// GameComponent that pre-caches all tile data on world load.
    /// Processes tiles incrementally to avoid UI freeze.
    /// </summary>
    public class TileCachePrecomputationComponent : GameComponent
    {
        private const int TilesPerFrame = 10000; // Process 10k tiles per frame (very fast)

        private TileDataCache _cache;
        private int _totalTiles;
        private int _processedTiles;
        private bool _completed;
        private Stopwatch _stopwatch;

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
            _completed = false;
            _stopwatch = Stopwatch.StartNew();

            Log.Message($"[LandingZone] Starting tile cache precomputation for {_totalTiles} tiles...");
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

            // Update status through LandingZoneContext
            LandingZoneContext.UpdateTileCacheStatus(Progress, _processedTiles, false);

            // Check if we're done
            if (_processedTiles >= _totalTiles)
            {
                _completed = true;
                _stopwatch.Stop();
                Log.Message($"[LandingZone] Tile cache precomputation complete: {_totalTiles} tiles cached in {_stopwatch.ElapsedMilliseconds}ms");
                LandingZoneContext.UpdateTileCacheStatus(1f, _processedTiles, true);
            }
        }

        public float Progress => _totalTiles == 0 ? 1f : _processedTiles / (float)_totalTiles;
        public bool IsCompleted => _completed;
        public int ProcessedTiles => _processedTiles;
        public int TotalTiles => _totalTiles;
    }
}

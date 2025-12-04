#!/usr/bin/env python3
"""
RimWorld Growing Days Calculator
================================

Calculates growing days from LandingZone cache files WITHOUT calling the game.
Algorithm reverse-engineered from RimWorld's Assembly-CSharp.dll using monodis.

VALIDATED: Results match LandingZone mod exactly (tested 2025-12-02).

Usage:
    python3 calculate_growing_days.py --help           Show this help
    python3 calculate_growing_days.py --about          Show algorithm details
    python3 calculate_growing_days.py --test           Run validation tests
    python3 calculate_growing_days.py <cache_file>     Analyze cache (summary only)
    python3 calculate_growing_days.py <cache_file> --filter   Find specific tiles

Filter Examples:
    # Default filter: River + Road + Mountainous + Year-round (60 days)
    python3 calculate_growing_days.py cache.txt --filter

    # Custom filters (edit FILTER_CRITERIA in script or extend CLI)

Cache File Location:
    macOS:   ~/Library/Application Support/RimWorld/Config/LandingZone_FullCache_*.txt
    Windows: %USERPROFILE%/AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios/Config/

    Generate via: Dev Mode > Landing Zone Preferences > [DEV] Dump FULL World Cache
"""

import argparse
import math
import sys
import re
from typing import Tuple, Optional, List, Dict, Any

# =============================================================================
# CONSTANTS (from RimWorld IL disassembly)
# =============================================================================

TICKS_PER_DAY = 60000           # 2500 ticks/hour * 24 hours
DAYS_PER_YEAR = 60              # RimWorld year = 60 days
TWELFTHS_PER_YEAR = 12          # 12 "twelfths" per year
DAYS_PER_TWELFTH = 5            # 60 / 12 = 5 days per twelfth
TICKS_PER_TWELFTH = 300000      # 60000 * 5

# Growing temperature thresholds (from TileDataCache.cs)
MIN_GROWTH_TEMP = 6.0           # °C - MinOptimalGrowthTemperature
MAX_GROWTH_TEMP = 42.0          # °C - MaxOptimalGrowthTemperature

# Seasonal Variation Curve (from TemperatureTuning.cs IL lines 393653-393672)
# Maps distFromEquatorNormalized -> seasonal amplitude (°C swing from mean)
SEASONAL_CURVE = [
    (0.0, 3.0),    # At equator: ±3°C swing
    (0.1, 4.0),    # 10% from equator: ±4°C swing
    (1.0, 28.0),   # At poles: ±28°C swing
]

# Default filter criteria (matches LandingZone test case)
DEFAULT_FILTER_CRITERIA = {
    'hilliness': 'Mountainous',
    'has_river': True,
    'has_road': True,
    'growing_days_min': 60,  # Year-round
}


# =============================================================================
# ALGORITHM DOCUMENTATION
# =============================================================================

ABOUT_TEXT = """
================================================================================
RimWorld Growing Days Algorithm (Reverse-Engineered)
================================================================================

SOURCE: Assembly-CSharp.dll decompiled via monodis (2025-12-02)
IL LOCATIONS:
  - GenTemperature.OffsetFromSeasonCycle:        lines 391284-391314
  - GenTemperature.SeasonalShiftAmplitudeAt:     lines 391376-391407
  - GenTemperature.AverageTemperatureAtTileForTwelfth: lines 390626-390703
  - GenTemperature.TwelfthsInAverageTemperatureRange:  lines 391411-391504
  - TemperatureTuning.SeasonalTempVariationCurve:      lines 393653-393672

--------------------------------------------------------------------------------
SEASONAL TEMPERATURE MODEL
--------------------------------------------------------------------------------

RimWorld uses a sinusoidal temperature model:

    temp(tick) = base_temp + offset(tick)

Where offset is calculated from:

    1. year_pct = (tick / 60000 % 60) / 60     # Position in 60-day year [0,1)
    2. phase = 10/12 = 0.8333                   # Winter peak at Decembary
    3. angle = 2π × (year_pct - phase)
    4. offset = cos(angle) × (-amplitude)

The amplitude depends on distance from equator (SeasonalTempVariationCurve):

    Distance from Equator | Seasonal Amplitude
    ----------------------|-------------------
           0.0 (equator)  |     ±3°C
           0.1            |     ±4°C
           1.0 (poles)    |    ±28°C

Southern hemisphere negates amplitude for 6-month phase shift.

--------------------------------------------------------------------------------
GROWING DAYS CALCULATION
--------------------------------------------------------------------------------

1. For each of 12 "twelfths" (5-day periods):
   - Sample temperature at 120 evenly-spaced ticks
   - Calculate average temperature for that twelfth

2. Count twelfths where: 6°C ≤ avg_temp ≤ 42°C

3. Growing days = count × 5

--------------------------------------------------------------------------------
YEAR-ROUND GROWING (60 days)
--------------------------------------------------------------------------------

Quick check from cache data:

    Year-round if: MinTemperature ≥ 6°C AND MaxTemperature ≤ 42°C

This works because MinTemperature/MaxTemperature in the cache represent
the seasonal extremes (coldest winter day, hottest summer day).

--------------------------------------------------------------------------------
CACHE FILE FORMAT
--------------------------------------------------------------------------------

The LandingZone_FullCache_*.txt file contains per-tile data:

    TILE 48105
      temperature: 26.06295        # Base/mean temperature
      MinTemperature: 21.83317     # Coldest seasonal temperature
      MaxTemperature: 30.29361     # Hottest seasonal temperature
      hilliness: Mountainous       # Flat, SmallHills, LargeHills, Mountainous
      Rivers: [...]                # "[...]" = has river, "null" = no river
      Roads: [...]                 # "[...]" = has road, "null" = no road

IMPORTANT: "Rivers: null" means NO river (not a missing key).

--------------------------------------------------------------------------------
VALIDATION
--------------------------------------------------------------------------------

Tested against LandingZone mod (2025-12-02):

    Filter: River + Road + Mountainous + Year-round
    Script result: 2 tiles (48105, 182107)
    LZ result:     2 tiles (48105, 182107)

    MATCH: 100%

================================================================================
"""


# =============================================================================
# CORE ALGORITHM (from GenTemperature IL)
# =============================================================================

def lerp_curve(curve: List[Tuple[float, float]], x: float) -> float:
    """Linearly interpolate a SimpleCurve (RimWorld's piecewise linear curves)."""
    if x <= curve[0][0]:
        return curve[0][1]
    if x >= curve[-1][0]:
        return curve[-1][1]
    for i in range(len(curve) - 1):
        x0, y0 = curve[i]
        x1, y1 = curve[i + 1]
        if x0 <= x < x1:
            t = (x - x0) / (x1 - x0)
            return y0 + t * (y1 - y0)
    return curve[-1][1]


def seasonal_shift_amplitude(dist_from_equator: float, latitude: float) -> float:
    """
    SeasonalShiftAmplitudeAt - determines temperature swing magnitude.
    (IL lines 391376-391407)

    Returns the seasonal amplitude (half of peak-to-peak swing).
    Southern hemisphere negates for 6-month phase shift.
    """
    amplitude = lerp_curve(SEASONAL_CURVE, dist_from_equator)
    return amplitude if latitude >= 0 else -amplitude


def offset_from_season_cycle(abs_tick: int, dist_from_equator: float, latitude: float) -> float:
    """
    OffsetFromSeasonCycle - calculates seasonal temperature offset at a given tick.
    (IL lines 391284-391314)

    Formula from IL:
      yearPct = (absTick / 60000.0 % 60.0) / 60.0
      winterMidPct = 10/12 (middle of Decembary for northern hemisphere)
      angle = 2π * (yearPct - winterMidPct)
      offset = cos(angle) * (-amplitude)
    """
    # Position in year as fraction [0, 1)
    year_pct = (abs_tick / float(TICKS_PER_DAY) % DAYS_PER_YEAR) / DAYS_PER_YEAR

    # Winter's middle twelfth (Decembary) starts at 10/12 of year for northern hemisphere
    # This sets the phase so winter peak is coldest
    WINTER_MID_TWELFTH_PCT = 10.0 / 12.0  # ≈ 0.8333

    angle = 2 * math.pi * (year_pct - WINTER_MID_TWELFTH_PCT)
    amplitude = seasonal_shift_amplitude(dist_from_equator, latitude)

    # Cosine wave: coldest at winter peak (cos=1), warmest at summer (cos=-1)
    # Negating amplitude makes cold offset negative and hot offset positive
    return math.cos(angle) * (-amplitude)


def get_temp_at_tick(abs_tick: int, tile_temp: float, dist_from_equator: float, latitude: float) -> float:
    """
    GetTemperatureFromSeasonAtTile - temperature at specific tick.
    (IL lines 391318-391344)
    """
    return tile_temp + offset_from_season_cycle(abs_tick, dist_from_equator, latitude)


def average_temp_for_twelfth(tile_temp: float, dist_from_equator: float, latitude: float, twelfth: int) -> float:
    """
    AverageTemperatureAtTileForTwelfth - average temp for a 5-day twelfth.
    (IL lines 390626-390703)

    Samples 120 evenly spaced ticks within the twelfth period.
    """
    BASE_TICK = 30000  # Half a day offset to sample mid-points
    TWELFTH_START = TICKS_PER_TWELFTH * twelfth
    SAMPLES = 120

    total = 0.0
    for i in range(SAMPLES):
        # Evenly distribute samples across the twelfth's tick range
        tick = TWELFTH_START + BASE_TICK + int(i * TICKS_PER_TWELFTH / SAMPLES)
        total += get_temp_at_tick(tick, tile_temp, dist_from_equator, latitude)

    return total / SAMPLES


def count_growing_twelfths(tile_temp: float, dist_from_equator: float, latitude: float) -> int:
    """
    TwelfthsInAverageTemperatureRange for growth range [6°C, 42°C].
    (IL lines 391411-391504)
    """
    count = 0
    for twelfth in range(TWELFTHS_PER_YEAR):
        avg_temp = average_temp_for_twelfth(tile_temp, dist_from_equator, latitude, twelfth)
        if MIN_GROWTH_TEMP <= avg_temp <= MAX_GROWTH_TEMP:
            count += 1
    return count


def calculate_growing_days(tile_temp: float, dist_from_equator: float, latitude: float) -> int:
    """
    Calculate growing days (0-60) for a tile.

    Args:
        tile_temp: Base/mean temperature from cache
        dist_from_equator: Distance from equator [0, 1]
        latitude: Tile latitude (negative for southern hemisphere)

    Returns:
        Number of growing days (0, 5, 10, 15, ... 55, 60)
    """
    growing_twelfths = count_growing_twelfths(tile_temp, dist_from_equator, latitude)
    return growing_twelfths * DAYS_PER_TWELFTH


# =============================================================================
# CACHE FILE UTILITIES
# =============================================================================

def amplitude_to_dist_from_equator(amplitude: float) -> float:
    """
    Inverse of SeasonalTempVariationCurve.
    Given seasonal amplitude, estimate distFromEquatorNormalized.
    """
    amplitude = abs(amplitude)

    if amplitude <= 3.0:
        return 0.0
    elif amplitude <= 4.0:
        # Interpolate between (0, 3) and (0.1, 4)
        return 0.1 * (amplitude - 3.0) / (4.0 - 3.0)
    else:
        # Interpolate between (0.1, 4) and (1.0, 28)
        return 0.1 + 0.9 * (amplitude - 4.0) / (28.0 - 4.0)


def estimate_latitude_from_amplitude(amplitude: float, is_southern: bool = False) -> float:
    """
    Rough estimate of latitude from seasonal amplitude.
    This is approximate - actual latitude depends on world generation.
    """
    dist = amplitude_to_dist_from_equator(amplitude)
    lat = dist * 90.0  # Max latitude is ~90°
    return -lat if is_southern else lat


def calculate_from_min_max(min_temp: float, max_temp: float, latitude: Optional[float] = None) -> int:
    """
    Calculate growing days from cache MinTemperature/MaxTemperature.

    The cache stores seasonal min/max temperatures, so:
      amplitude = (max - min) / 2
      base_temp = (max + min) / 2

    If latitude not provided, assumes northern hemisphere (positive).
    """
    base_temp = (min_temp + max_temp) / 2
    amplitude = (max_temp - min_temp) / 2
    dist_from_equator = amplitude_to_dist_from_equator(amplitude)

    # Use provided latitude or estimate from amplitude
    if latitude is None:
        latitude = estimate_latitude_from_amplitude(amplitude, is_southern=False)

    return calculate_growing_days(base_temp, dist_from_equator, latitude)


def is_year_round_growing(min_temp: float, max_temp: float) -> bool:
    """
    Quick check: year-round growing if seasonal extremes stay in [6, 42]°C range.

    This is the fast path - no need to calculate all 12 twelfths if we know
    the min/max are within bounds.
    """
    return min_temp >= MIN_GROWTH_TEMP and max_temp <= MAX_GROWTH_TEMP


# =============================================================================
# CACHE FILE PARSER
# =============================================================================

def parse_cache_file(filepath: str) -> Dict[int, Dict[str, Any]]:
    """
    Parse a LandingZone_FullCache file and extract tile data.

    Returns dict mapping tile_id -> {temperature, MinTemperature, MaxTemperature, ...}

    Cache format:
        TILE 123
          temperature: 27.5
          MinTemperature: 20.0
          MaxTemperature: 35.0
          hilliness: Mountainous
          Rivers: null              # "null" = no river
          Roads: [RimWorld...]      # "[...]" = has road
    """
    tiles = {}
    current_tile = None
    current_data = {}

    with open(filepath, 'r') as f:
        for line in f:
            line = line.rstrip()

            # New tile header: "TILE 123"
            tile_match = re.match(r'^TILE\s+(\d+)', line)
            if tile_match:
                if current_tile is not None:
                    tiles[current_tile] = current_data
                current_tile = int(tile_match.group(1))
                current_data = {}
                continue

            # Property line: "  temperature: 27.5"
            prop_match = re.match(r'^\s+(\w+):\s*(.+)', line)
            if prop_match and current_tile is not None:
                key, value = prop_match.groups()
                # Parse numeric values
                try:
                    if '.' in value:
                        current_data[key] = float(value)
                    else:
                        current_data[key] = int(value)
                except ValueError:
                    current_data[key] = value

    # Don't forget last tile
    if current_tile is not None:
        tiles[current_tile] = current_data

    return tiles


def analyze_cache(filepath: str, filter_criteria: Optional[Dict[str, Any]] = None, verbose: bool = False):
    """
    Analyze a cache file and calculate growing days for all tiles.

    Args:
        filepath: Path to LandingZone_FullCache_*.txt file
        filter_criteria: Optional dict with filter conditions:
            {
                'hilliness': 'Mountainous',  # Flat, SmallHills, LargeHills, Mountainous
                'has_river': True,           # Tile must have a river
                'has_road': True,            # Tile must have a road
                'growing_days_min': 60,      # Minimum growing days
            }
        verbose: Print detailed info for each matching tile

    Returns:
        List of matching tile dicts if filter_criteria, else all tiles dict
    """
    tiles = parse_cache_file(filepath)
    print(f"Loaded {len(tiles):,} tiles from cache")

    results = []
    year_round_count = 0
    stats = {
        'with_river': 0,
        'with_road': 0,
        'mountainous': 0,
    }

    for tile_id, data in tiles.items():
        temp = data.get('temperature')
        min_t = data.get('MinTemperature')
        max_t = data.get('MaxTemperature')

        if temp is None or min_t is None or max_t is None:
            continue

        growing_days = calculate_from_min_max(min_t, max_t)

        if growing_days == 60:
            year_round_count += 1

        # Gather stats
        rivers_value = data.get('Rivers')
        roads_value = data.get('Roads')
        if rivers_value and rivers_value != 'null':
            stats['with_river'] += 1
        if roads_value and roads_value != 'null':
            stats['with_road'] += 1
        if data.get('hilliness') == 'Mountainous':
            stats['mountainous'] += 1

        # Apply filters if specified
        if filter_criteria:
            # Hilliness check (lowercase 'hilliness' in cache)
            if filter_criteria.get('hilliness'):
                tile_hilliness = data.get('hilliness')
                if tile_hilliness != filter_criteria['hilliness']:
                    continue

            # River check: "Rivers: null" means no river, "Rivers: [...]" means has river
            if filter_criteria.get('has_river'):
                if rivers_value is None or rivers_value == 'null':
                    continue

            # Road check: same logic as rivers
            if filter_criteria.get('has_road'):
                if roads_value is None or roads_value == 'null':
                    continue

            # Growing days check
            if filter_criteria.get('growing_days_min') and growing_days < filter_criteria['growing_days_min']:
                continue

            results.append({
                'tile_id': tile_id,
                'growing_days': growing_days,
                'temp': temp,
                'min_temp': min_t,
                'max_temp': max_t,
                **data
            })

    # Print summary statistics
    print(f"\nWorld Statistics:")
    print(f"  Year-round growing (60 days): {year_round_count:,} tiles ({100*year_round_count/len(tiles):.1f}%)")
    print(f"  With river: {stats['with_river']:,} tiles ({100*stats['with_river']/len(tiles):.1f}%)")
    print(f"  With road: {stats['with_road']:,} tiles ({100*stats['with_road']/len(tiles):.1f}%)")
    print(f"  Mountainous: {stats['mountainous']:,} tiles ({100*stats['mountainous']/len(tiles):.1f}%)")

    if filter_criteria:
        print(f"\nFilter: {filter_criteria}")
        print(f"Tiles matching filter: {len(results)}")
        return results

    return tiles


def run_tests():
    """Validate the algorithm against expected behavior."""
    print("=" * 60)
    print("Running Validation Tests")
    print("=" * 60)
    print()

    all_passed = True

    # Test 1: Equatorial tile (minimal seasonal variation)
    print("Test 1: Equatorial tile (amplitude ≈ 3°C)")
    days = calculate_growing_days(tile_temp=25.0, dist_from_equator=0.0, latitude=0.0)
    print(f"  Base temp: 25°C, Growing days: {days}")
    if days == 60:
        print("  ✓ PASS: Year-round growing at equator with moderate temp")
    else:
        print(f"  ✗ FAIL: Expected 60, got {days}")
        all_passed = False
    print()

    # Test 2: Polar tile (large seasonal variation)
    print("Test 2: Polar tile (amplitude ≈ 28°C)")
    days = calculate_growing_days(tile_temp=0.0, dist_from_equator=1.0, latitude=90.0)
    print(f"  Base temp: 0°C, Growing days: {days}")
    if days < 60:
        print(f"  ✓ PASS: Limited growing season at poles ({days} days)")
    else:
        print(f"  ✗ FAIL: Expected < 60, got {days}")
        all_passed = False
    print()

    # Test 3: Year-round check via min/max
    print("Test 3: Year-round from MinTemp/MaxTemp")

    test_cases = [
        (10.0, 35.0, True, "should be year-round"),
        (5.0, 35.0, False, "NOT year-round (too cold in winter)"),
        (10.0, 45.0, False, "NOT year-round (too hot in summer)"),
    ]

    for min_t, max_t, expected, desc in test_cases:
        result = is_year_round_growing(min_t, max_t)
        status = "✓ PASS" if result == expected else "✗ FAIL"
        print(f"  MinTemp={min_t}, MaxTemp={max_t} -> {desc}")
        print(f"  {status}")
        if result != expected:
            all_passed = False
    print()

    # Test 4: Calculate from min/max
    print("Test 4: Calculate from MinTemp/MaxTemp")
    days = calculate_from_min_max(min_temp=10.0, max_temp=35.0)
    print(f"  MinTemp=10, MaxTemp=35 -> {days} growing days")
    if days == 60:
        print("  ✓ PASS")
    else:
        print(f"  ✗ FAIL: Expected 60, got {days}")
        all_passed = False
    print()

    # Test 5: Cold tile
    print("Test 5: Cold tile")
    days = calculate_from_min_max(min_temp=-20.0, max_temp=15.0)
    print(f"  MinTemp=-20, MaxTemp=15 -> {days} growing days")
    if days < 60:
        print(f"  ✓ PASS: Limited growing in cold climate")
    else:
        print(f"  ✗ FAIL: Expected < 60, got {days}")
        all_passed = False
    print()

    print("=" * 60)
    if all_passed:
        print("All tests passed!")
    else:
        print("Some tests FAILED!")
        sys.exit(1)


# =============================================================================
# MAIN
# =============================================================================

def main():
    parser = argparse.ArgumentParser(
        description='RimWorld Growing Days Calculator - Offline cache analysis tool',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  %(prog)s --about                    Show algorithm details
  %(prog)s --test                     Run validation tests
  %(prog)s cache.txt                  Analyze cache (summary only)
  %(prog)s cache.txt --filter         Find tiles matching default filter
  %(prog)s cache.txt --filter -v      Verbose output for matching tiles

Default Filter (--filter):
  River (any) AND Road (any) AND Mountainous AND Year-round growing (60 days)

Cache File Location:
  macOS:   ~/Library/Application Support/RimWorld/Config/LandingZone_FullCache_*.txt
  Windows: %%USERPROFILE%%/AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios/Config/

  Generate via: Dev Mode > Landing Zone Preferences > [DEV] Dump FULL World Cache
"""
    )

    parser.add_argument('cache_file', nargs='?', help='Path to LandingZone_FullCache_*.txt file')
    parser.add_argument('--about', action='store_true', help='Show detailed algorithm documentation')
    parser.add_argument('--test', action='store_true', help='Run validation tests')
    parser.add_argument('--filter', action='store_true',
                        help='Apply default filter (River + Road + Mountainous + Year-round)')
    parser.add_argument('-v', '--verbose', action='store_true', help='Verbose output')

    args = parser.parse_args()

    if args.about:
        print(ABOUT_TEXT)
        return

    if args.test:
        run_tests()
        return

    if not args.cache_file:
        parser.print_help()
        sys.exit(1)

    # Analyze cache file
    if args.filter:
        results = analyze_cache(args.cache_file, DEFAULT_FILTER_CRITERIA, args.verbose)

        if results:
            print(f"\nMatching tiles:")
            for r in results[:50]:  # Show first 50
                biome = r.get('PrimaryBiome', 'Unknown')
                print(f"  Tile {r['tile_id']}: {r['growing_days']} days, "
                      f"temp {r['temp']:.1f}°C ({r['min_temp']:.1f}-{r['max_temp']:.1f}), "
                      f"biome: {biome}")

            if len(results) > 50:
                print(f"  ... and {len(results) - 50} more tiles")
    else:
        analyze_cache(args.cache_file, verbose=args.verbose)


if __name__ == "__main__":
    main()

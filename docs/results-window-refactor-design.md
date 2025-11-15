# Results Window Refactor - Design Document

## Goals
1. **Transparency**: Show WHY each tile scored what it did
2. **Visual hierarchy**: Critical failures stand out, perfect matches shine
3. **Rarity awareness**: Highlight statistically rare/lucky finds

---

## Card Layout (Before vs After)

### BEFORE (Current - v0.1.3-beta)
```
┌─────────────────────────────────────────────┐
│ #1 • Tile 12345                    [Focus] │
│                                             │
│ 93% Excellent                               │
│ Temperate Forest                            │
│                                             │
│ 22°C • Rain: 1200mm • Growing: 55d         │ ← Colored by match
│ Large Hills • Granite/Limestone/Slate      │
│ Rainfall: 950mm (wanted 1000-2200mm)       │ ← Only shows failures
└─────────────────────────────────────────────┘
```

### AFTER (Proposed)
```
┌═════════════════════════════════════════════┐ ← Gold border if score >= 1.0
│ #1 • Tile 12345        [🏆] [⭐]    [Focus] │ ← 🏆 = Perfect, ⭐ = Rare
│                                             │
│ Score: 0.933 (Excellent)                    │ ← Raw score, not %
│ Temperate Forest                            │
│                                             │
│ ✓ Matched (6)                               │ ← Collapsible section
│   Temperature (0.98), Rainfall (0.92),     │ ← Membership scores shown
│   Growing (1.00), Caves, Rivers, Granite   │
│                                             │
│ ✗ Missed (2)                                │ ← Critical misses bold/red
│   Roads [CRITICAL, -0.15] ❗               │ ← Penalty shown
│   Marble [near miss, -0.03]                │ ← "Near miss" tag
│                                             │
│ ⚡ Modifiers (+0.08)                        │ ← Mutator breakdown
│   Fish_Increased (+0.05)                    │
│   Fertile (+0.06)                           │
│   Polluted (-0.03)                          │
└─────────────────────────────────────────────┘
```

---

## Visual Indicators

### 1. Perfect Match (score >= 1.0)
- **Gold stroke** around entire card (2px, color: `#FFD700`)
- **Trophy icon** 🏆 next to tile ID
- **Tooltip**: "Perfect Match - Exceeds all requirements"

**Rationale**: Scores >= 1.0 mean all criticals are perfect (1.0 membership) AND preferred/mutators boost it further. These are unicorns.

### 2. Rarity Badge (Low/VeryLow likelihood)
- **Star icon** ⭐ when `LikelihoodCategory` is `Low` or `VeryLow`
- **Colors**:
  - VeryLow (< 1 expected): Platinum star ⭐ (#E5E4E2)
  - Low (1-10 expected): Gold star ⭐ (#FFD700)
- **Tooltip**: "Rare Find - Only ~3 tiles match these filters"

**Rationale**: If the estimator predicted <10 matches and user found one, that's statistically lucky. Celebrate it!

**Implementation**: We already have `MatchLikelihoodEstimator` - just need to:
1. Store the `LikelihoodCategory` in evaluation context
2. Compare actual tile count to estimate
3. Show badge if tile is in top results AND category is Low/VeryLow

### 3. Section Icons
- **✓** Green checkmark for Matched
- **✗** Red X for Missed
- **⚡** Lightning bolt for Modifiers
- **❗** Warning triangle for Critical misses

---

## Matched Section Logic

**Show if**: Filter has importance (Critical/Preferred) AND tile meets it

**Format**:
- Range filters: `Temperature (0.98)` ← membership score in parens
- Boolean filters: `Caves` ← just the name (no score, it's binary)
- Multi-item: `Granite, Limestone` ← list matched stones

**Sorting**:
1. Critical matches first (bold)
2. Preferred matches second (normal weight)
3. Within each group: alphabetical

**Collapsible**: Default expanded if <=8 items, collapsed if >8

---

## Missed Section Logic

**Show if**: Filter has importance AND tile FAILS it (membership < threshold)

**Format**:
- `Rainfall [CRITICAL, -0.15] ❗` ← Critical miss, bold red, show penalty
- `Marble [near miss, -0.03]` ← Near miss tag, orange, smaller penalty

**Near Miss Detection**:
- Range filters: Within 10% of range boundary
  - Example: `RainfallRange = 1000-2200`, tile has 950mm → 950 is 5% below 1000 → "near miss"
- Boolean: N/A (you either have it or don't)
- Stone count: `required=3, matched=2` → "near miss"

**Penalty Calculation**:
```
penalty = (1 - membership)^2 * importance_weight
```
We already compute this in `ScoringWeights.ComputePenalty()` - just need to expose per-filter.

**Sorting**:
1. Critical misses first (❗ icon)
2. Preferred misses second
3. By penalty magnitude (worst first)

---

## Modifiers Section (Mutators)

**Show if**: Tile has mutators with non-zero rating

**Format**:
- Positive: `Fish_Increased (+0.05)` ← green text
- Negative: `Polluted (-0.03)` ← red text
- Neutral: (don't show, clutter reduction)

**Contribution Calculation**:
Each mutator's contribution to final score:
```
contribution = mutator_quality * mutator_weight * (1/num_mutators)
```

**Sorting**: By absolute contribution (largest impact first)

**Collapsible**: Default collapsed if >5 mutators

---

## Color Palette

| Element | Color | Usage |
|---------|-------|-------|
| Perfect border | `#FFD700` (gold) | score >= 1.0 |
| Excellent bg | `rgba(76, 230, 76, 0.15)` (green) | score >= 0.9 |
| Good bg | `rgba(76, 217, 230, 0.15)` (cyan) | score >= 0.75 |
| Acceptable bg | `rgba(242, 230, 76, 0.15)` (yellow) | score >= 0.6 |
| Poor bg | `rgba(255, 153, 51, 0.15)` (orange) | score < 0.6 |
| Matched text | `#4ae64a` (bright green) | Critical matched |
| Matched text | `#c4d14a` (yellow-green) | Preferred matched |
| Missed critical | `#ff4545` (red) + bold | Critical failed |
| Missed preferred | `#ffab45` (orange) | Preferred failed |
| Near miss | `#ffcc66` (light orange) | Close but not quite |
| Mutator positive | `#66ff66` (lime green) | +contribution |
| Mutator negative | `#ff6666` (salmon red) | -contribution |

---

## Implementation Priority

**Phase 1: Data collection** (must complete first)
- Enhance `MatchBreakdown` with per-filter memberships
- Store mutator list and individual contributions
- Update `FilterEvaluationJob` to populate real data

**Phase 2: UI rendering**
- Refactor `DrawMatchRow()` to use new sections
- Add collapsible section helpers
- Implement icon rendering

**Phase 3: Special badges**
- Perfect match detection + gold border
- Rarity badge logic (compare to likelihood estimate)
- Tooltip explanations

**Phase 4: Polish**
- Near miss detection
- Penalty breakdown
- Smooth animations for expand/collapse

---

## Example Cards

### Example 1: Perfect Tundra Base (score 1.05)
```
┌═════════════════════════════════════════════┐ ← GOLD BORDER
│ #1 • Tile 54321        [🏆]         [Focus] │
│                                             │
│ Score: 1.05 (Perfect)                       │
│ Tundra                                      │
│                                             │
│ ✓ Matched (5)                               │
│   Temperature (1.00), Growing (1.00),       │
│   Granite, Marble, Limestone                │
│                                             │
│ ⚡ Modifiers (+0.05)                        │
│   MineralRich (+0.08), Cold (-0.03)         │
└─────────────────────────────────────────────┘
```

### Example 2: Rare Tropical Paradise (score 0.88, Low likelihood)
```
┌─────────────────────────────────────────────┐
│ #2 • Tile 99999        [⭐]         [Focus] │ ← RARITY STAR
│                                             │
│ Score: 0.88 (Good)                          │
│ Tropical Rainforest                         │
│                                             │
│ ✓ Matched (8)                               │
│   Temperature (0.95), Rainfall (1.00),      │
│   Growing (1.00), Fish, Caves, River,       │
│   Fertile, WildTropicalPlants               │
│                                             │
│ ✗ Missed (1)                                │
│   Granite [Preferred, -0.04]                │
│                                             │
│ ⚡ Modifiers (+0.12)                        │
│   Fish_Increased (+0.05), Fertile (+0.06)   │
└─────────────────────────────────────────────┘
```

### Example 3: Flawed Desert (score 0.67, Critical miss)
```
┌─────────────────────────────────────────────┐
│ #18 • Tile 11111                   [Focus] │
│                                             │
│ Score: 0.67 (Acceptable)                    │
│ Desert                                      │
│                                             │
│ ✓ Matched (3)                               │
│   Temperature (0.98), Granite, Limestone    │
│                                             │
│ ✗ Missed (3)                                │
│   Rainfall [CRITICAL, -0.22] ❗            │ ← Bold red, big penalty
│   Growing [near miss, -0.08]                │
│   River [Preferred, -0.03]                  │
│                                             │
│ ⚡ Modifiers (-0.01)                        │
│   Sandy (0.00), Oasis (+0.04), Dry (-0.05) │
└─────────────────────────────────────────────┘
```

---

## Technical Notes

### Membership Score Display
- Show 2 decimal places: `(0.98)`
- Only show for range filters (Temperature, Rainfall, Growing, etc.)
- Binary filters (Caves, River, Coastal) just show name

### Collapsible Sections
```csharp
private static bool _matchedExpanded = true; // Per-card state
private static bool _missedExpanded = true;
private static bool _modifiersExpanded = false; // Collapsed by default

// Click header to toggle
if (Widgets.ButtonInvisible(headerRect))
{
    _matchedExpanded = !_matchedExpanded;
}
```

### Rarity Detection
```csharp
// Store during evaluation
public class EvaluationResults
{
    public List<TileScore> Tiles { get; }
    public MatchLikelihood Likelihood { get; } // ← ADD THIS
}

// Display logic
bool isRare = context.Likelihood.Category <= LikelihoodCategory.Low
              && tileScore.Rank <= 10; // Top 10 results
```

---

## Open Questions

1. **Expand/collapse state**: Per-card or global?
   - **Recommendation**: Global toggle in toolbar ("Expand All" / "Collapse All")

2. **Score threshold for "near miss"**: 10% of range?
   - **Recommendation**: Yes, 10% is intuitive

3. **Mutator contribution**: Show absolute (+0.05) or relative to total (12%)?
   - **Recommendation**: Absolute is clearer, but add tooltip showing relative

4. **Perfect match threshold**: >= 1.0 or exactly 1.0?
   - **Recommendation**: >= 1.0 (means all criticals perfect + bonus)

---

## Estimated Implementation Time

- **Phase 1 (Data)**: 3-4 hours
- **Phase 2 (UI core)**: 4-5 hours
- **Phase 3 (Badges)**: 2-3 hours
- **Phase 4 (Polish)**: 2-3 hours
- **Total**: ~12-15 hours

**Incremental delivery**: Can ship Phase 1+2 first (basic breakdown), then add badges later.

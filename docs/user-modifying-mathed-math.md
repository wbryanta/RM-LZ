Cool, so let’s forget about implementation details for a second and just answer:

> Given 80+ variables, some numeric, some binary, some “world mutators”, how do we turn user preferences (Critical / Preferred + ordering) into a **single score in [0,1] per tile** that:
>
> * punishes critical misses hard
> * respects “how bad” numeric misses are
> * respects the number of critical vs preferred
> * uses drag‑and‑drop ranking
> * and lets mutators shift quality of life even if not explicitly filtered?

Here’s a concrete scoring model you can implement.

---

## 1. Core idea: everything becomes a μᵢ(t) in [0,1]

For every active filter (i) and tile (t), compute a **match score**

[
\mu_i(t) \in [0,1]
]

* 1 = perfect match for that filter
* 0 = completely fails that filter
* values in between for “near misses” on ranges, ordered categories, etc.

You only consider filters marked **Critical** or **Preferred**. Everything else is ignored for matching (but mutators are handled separately).

### 1.1 Numeric filters (ranges and deviation)

User gives you an ideal range ([L_i, U_i]) for property (x) (rainfall, temp, etc.), and optionally you choose a **soft margin** (d^-_i, d^+_i) outside that range.

Membership is a trapezoid:

```text
x = tile value

μ_i(x) = 0                           if x <= L_i - d^-_i
       = 1 - (L_i - x)/d^-_i         if L_i - d^-_i < x < L_i
       = 1                           if L_i <= x <= U_i
       = 1 - (x - U_i)/d^+_i         if U_i < x < U_i + d^+_i
       = 0                           if x >= U_i + d^+_i
```

This gives:

* **Perfect** 1.0 inside the desired interval
* **Smooth drop‑off** outside, so someone 5°C too cold is “better” than 25°C too cold.

How to pick margins (d^\pm_i)?

* Easy defaults:

  * (d^\pm_i = k \cdot (U_i - L_i)) (e.g. k = 0.3)
  * OR based on world stats: 1× standard deviation from your report per property.

You can later expose a “fuzziness” slider that multiplies (d^\pm_i).

---

### 1.2 Ordinal categories (hilliness, etc.)

Some categories are naturally ordered (`Flat < SmallHills < LargeHills < Mountainous < Impassable`).

Assign integer codes:

```text
Flat = 0, SmallHills = 1, LargeHills = 2, Mountainous = 3, Impassable = 4
```

User picks allowed set S (e.g. SmallHills, LargeHills, Mountainous).

Compute distance to the closest allowed option:

[
d_i(t) = \min_{s \in S} |\text{code}(x_i(t)) - \text{code}(s)|
]

Then membership:

[
\mu_i(t) = \max\Big(0,, 1 - \frac{d_i(t)}{D_i}\Big)
]

Where (D_i) is max tolerated distance (e.g. 2 steps → 0 score).

* If tile is in the allowed set → (d=0) → (\mu=1)
* One step away → maybe (\mu=0.5)
* Two or more → (\mu=0)

---

### 1.3 Binary / nominal categories (coastal, biome sets, specific mutators as filters)

For “has X / doesn’t have X” or “category in allowed set”:

* If tile matches: (\mu_i = 1)
* If tile doesn’t match: (\mu_i = 0)

You *could* make a special case where “preferred, but not matched” gets e.g. 0.2 instead of 0, but 0/1 is simplest and intuitive.

---

## 2. Use drag‑and‑drop ranks to assign weights wᵢ

Users rank their Criticals and Preferreds. Let’s turn rank into weights.

For each group separately:

* Let:

  * Critical list: (C), ordered by rank (r_i = 1,2,\dots, n_C)
  * Preferred list: (P), ordered (r_j = 1,\dots,n_P)

### 2.1 Position weight inside a group

Use a simple **monotonic decreasing function** of rank. Good options:

**(A) Linear**

[
p_i = \frac{n_g - r_i + 1}{n_g} \quad\text{for group }g \in {C,P}
]

Top ranked (r=1) gets ~1, bottom (r=n) gets ~1/n.

**(B) Geometric / exponential** (stronger separation):

[
p_i = \rho^{,(r_i - 1)}, \quad \rho \in (0,1)
]

e.g. ρ=0.7: ranks: 1 →1.0, 2→0.7, 3→0.49, etc.

### 2.2 Group‑internal weights

Now turn those into actual weights for the group average (we’ll normalize them later):

[
w_i^{(C)} = p_i \quad (i \in C) \
w_j^{(P)} = p_j \quad (j \in P)
]

You don’t even need to normalize here, because the group averages will divide by the sum of weights.

If you want an exposed slider per group, you can later multiply by a global factor, but for pure scoring:

* Inside each group, rank controls **relative importance**.

---

## 3. Group scores S_C(t) and S_P(t)

Given (\mu_i(t)) and (w_i), define:

[
S_C(t) =
\begin{cases}
\frac{\sum_{i \in C} w_i^{(C)} \mu_i(t)}{\sum_{i \in C} w_i^{(C)}} & \text{if } |C|>0\
1 & \text{if } |C|=0
\end{cases}
]

[
S_P(t) =
\begin{cases}
\frac{\sum_{j \in P} w_j^{(P)} \mu_j(t)}{\sum_{j \in P} w_j^{(P)}} & \text{if } |P|>0\
0 & \text{if } |P|=0
\end{cases}
]

These are both in [0,1].

Also track the **worst critical**:

[
W_C(t) =
\begin{cases}
\min_{i \in C} \mu_i(t) & |C|>0 \
1 & |C|=0
\end{cases}
]

That’s the “how badly did the single worst critical get violated?” knob we’ll use to punish tiles.

---

## 4. Mutator quality score Sₘᵤₜ(t)

You have 83 mutators; you want them to nudge scores even if not explicitly filtered.

Give each mutator (k) a **base quality rating** in [-10, 0, +10]:

* +10 very good (Fertile valley, HotSprings, etc.)
* 0 neutral
* -10 very bad (ToxicLake, Pollution_Increased, AncientInfestedSettlement, etc.)

Map to [-1,1]:

[
q_k = \frac{\text{rating}_k}{10}
]

For a tile t with mutators (M(t)):

[
Q_{raw}(t) = \sum_{k \in M(t)} q_k
]

Compress this raw sum into [0,1] via a squashing function (to avoid one crazy tile dominating):

For example, using tanh:

[
S_{mut}(t) = \frac{1}{2}\left(1 + \tanh(\beta \cdot Q_{raw}(t))\right)
]

* (\beta) controls how sensitive it is:

  * choose (\beta) so that (|Q_{raw}|) around 3–4 already pushes S_mut close to 0.1 or 0.9.
* Tile with **no mutators** ⇒ (Q_{raw}=0) ⇒ (S_{mut}=0.5) (neutral baseline).

You can expose (\beta) and/or the overall mutator weight in mod settings.

---

## 5. Critical vs Preferred vs Mutators: global group weights λ

Now we have three subscores:

* (S_C(t)): how well it hits **Critical**
* (S_P(t)): how well it hits **Preferred**
* (S_{mut}(t)): intrinsic QOL from mutators

We want weights (\lambda_C, \lambda_P, \lambda_{mut}) such that:

[
\lambda_C + \lambda_P + \lambda_{mut} = 1
]

and:

* Critical is inherently stronger than Preferred.
* Relationship adapts a bit to how many of each are selected.
* Mutators are a smaller but independent influence.

### 5.1 Count‑aware scheme

Let:

* (n_C = |C|): # of critical filters
* (n_P = |P|): # of preferred filters

Define “per-filter” base importance:

* `critBase` (e.g. 4)
* `prefBase` (e.g. 1)

So one critical filter is ~4× a preferred one.

[
\alpha = \text{critBase} \cdot n_C \
\beta = \text{prefBase} \cdot n_P
]

Temporary normalized weights ignoring mutators:

[
\tilde{\lambda}_C = \frac{\alpha}{\alpha + \beta}, \quad
\tilde{\lambda}_P = \frac{\beta}{\alpha + \beta}
]

Reserve some weight for mutators: (\lambda_{mut} = \gamma) (e.g. 0.1).

Final:

[
\lambda_C = (1 - \gamma)\tilde{\lambda}_C, \quad
\lambda_P = (1 - \gamma)\tilde{\lambda}*P, \quad
\lambda*{mut} = \gamma
]

Example: 4 Critical, 12 Preferred, critBase=4, prefBase=1, (\gamma=0.1):

* (\alpha = 4\cdot4 = 16), (\beta = 1\cdot12 = 12)
* (\tilde{\lambda}_C \approx 0.571), (\tilde{\lambda}_P \approx 0.429)
* (\lambda_C \approx 0.514), (\lambda_P \approx 0.386), (\lambda_{mut} = 0.1)

So **~51%** of the combined score comes from criticals, **39%** from preferred, **10%** from mutators.

You can expose `critBase`, `prefBase`, and `γ` as advanced settings.

---

## 6. “Critical misses hurt a lot”: penalty term P_C(t)

We’ve weighted criticals more, but you also want:

> “critical misses much more potent in the algo”

We can do that with a **nonlinear penalty** based on the worst critical (W_C(t)).

Define:

[
P_C(t) = \alpha_{pen} + (1 - \alpha_{pen})\cdot W_C(t)^{\gamma_{pen}}
]

* (\alpha_{pen} \in [0,1]): minimum fraction of score that survives even with terrible criticals.

  * If you set (\alpha_{pen} = 0), then a tile with W_C=0 gets final score 0.
* (\gamma_{pen} > 1): how sharp the punishment is.

Example: (\alpha_{pen} = 0.1), (\gamma_{pen} = 3):

* If worst critical is 1.0 → (P_C=1.0) (no penalty)
* If worst critical is 0.7 → (P_C ≈ 0.41)
* If worst critical is 0.3 → (P_C ≈ 0.124)
* If worst critical is 0.0 → (P_C = 0.1)

Tiles that bomb a critical get nuked, but you can decide how *utterly* nuked via those parameters.

You can also add a **hard gate** option:

* If `strictCriticals` is enabled and (W_C(t) = 0), then **do not consider this tile at all** (score=0, skip from Top‑N).

---

## 7. Final scoring formula

Putting it together:

1. Compute (\mu_i(t)) per active filter for tile t.
2. Compute:

   * (S_C(t)) – weighted average of critical µs
   * (S_P(t)) – weighted average of preferred µs
   * (W_C(t)) – worst critical µ
   * (S_{mut}(t)) – mutator score
3. Compute group weights (\lambda_C,\lambda_P,\lambda_{mut}).
4. Compute penalty (P_C(t)).

### 7.1 Base combination

[
S_{base}(t) = \lambda_C S_C(t) + \lambda_P S_P(t) + \lambda_{mut} S_{mut}(t)
]

### 7.2 Final score

[
S(t) = P_C(t) \cdot S_{base}(t)
]

Everything is in [0,1]. Sort tiles descending by (S(t)) and take top 3 / 5 / 10 / 20 / 50 / 100.

---

## 8. Where your planned features plug in

* **User‑changeable weighting**:

  * Expose:

    * `critBase`, `prefBase`, `gamma` (mutator share)
    * `alpha_pen`, `gamma_pen` (how brutal critical failures are)
    * Fuzziness multiplier for numeric margins
  * These just tweak constants in the formulas; the structure stays the same.

* **Drag‑and‑drop ranking**:

  * Directly feeds into rank (r_i) per filter.
  * Rank → (p_i) (via linear or exponential scheme) → (w_i^{(C/P)}).
  * Higher‑ranked filters move the needle more inside their group.

* **Partial scoring on ranges**:

  * Already handled via (\mu_i(t)) trapezoids (and/or ordinal distance for things like hilliness).

* **Mutators always matter**:

  * (S_{mut}(t)) is computed regardless of whether they were explicitly filtered, then weighted in via (\lambda_{mut}).

---

## 9. TL;DR in almost‑code

Conceptually, your scoring loop over tiles is:

```csharp
foreach (var tile in tiles)
{
    float sumCrit = 0, wCritSum = 0, worstCrit = 1;
    float sumPref = 0, wPrefSum = 0;

    foreach (var filter in activeFilters)
    {
        float mu = filter.Membership(tile); // [0,1]

        if (filter.IsCritical)
        {
            sumCrit += filter.Weight * mu;
            wCritSum += filter.Weight;
            if (mu < worstCrit) worstCrit = mu;
        }
        else if (filter.IsPreferred)
        {
            sumPref += filter.Weight * mu;
            wPrefSum += filter.Weight;
        }
    }

    float S_C   = (wCritSum > 0 ? sumCrit / wCritSum : 1f);
    float S_P   = (wPrefSum > 0 ? sumPref / wPrefSum : 0f);
    float W_C   = (wCritSum > 0 ? worstCrit : 1f);
    float S_mut = ComputeMutatorScore(tile.Mutators); // [0,1]

    float S_base = lambdaC * S_C + lambdaP * S_P + lambdaMut * S_mut;
    float P_C    = alphaPen + (1 - alphaPen) * Mathf.Pow(W_C, gammaPen);

    float finalScore = P_C * S_base;
    InsertIntoTopN(tile, finalScore);
}
```

All the “user tuning” you want to support is in the constants and in the membership functions, not in the structure of the algorithm.

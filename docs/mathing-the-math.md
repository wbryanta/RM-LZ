Think of every tile as a little 80‑dimensional Pokémon card: a bunch of stats (numbers + tags), and every colonist has opinions about which stats are vital vs just nice‑to‑have. We want to turn that into a **single score** per tile that:

* treats **Critical vs Preferred** sanely
* punishes **critical misses** hard, but not purely binary
* uses **distance from the target range** when we can
* includes **mutators** as always‑on quality-of-life nudges
* works for ~250k tiles without weird artifacts

Below is a complete scoring model that does that, with concrete math and examples.

---

## 1. Unify everything into “how well does this tile match this filter?”

We start by turning every user filter (critical or preferred) into a **membership function**:

[
m_i(t) \in [0,1]
]

* (m_i(t) = 1): tile (t) is a perfect match for filter (i)
* (m_i(t) = 0): tile badly misses the filter
* values in between reflect *how close* it is

We do this separately for **binary/categorical** and **numeric** filters.

---

### 1.1 Binary / categorical filters

Examples: `IsCoastal`, `HasRiver`, `Biome in {TemperateForest, AridShrubland}`, `Has Mutator: Mountain`.

For a **positive** filter “must / prefer X”:

[
m_i(t) =
\begin{cases}
1 & \text{if tile has X} \
0 & \text{otherwise}
\end{cases}
]

For a **negative** filter “must / prefer NOT X”:

[
m_i(t) =
\begin{cases}
1 & \text{if tile does not have X} \
0 & \text{otherwise}
\end{cases}
]

For a **set**: “Biome in {TemperateForest, Grasslands, AridShrubland}”:

[
m_i(t) =
\begin{cases}
1 & \text{if biome(t) ∈ S} \
0 & \text{otherwise}
\end{cases}
]

You *can* get fancier (similar biomes, etc.), but this 0/1 definition is enough to plug into the rest of the math.

---

### 1.2 Numeric filters: turn deviation into a smooth penalty

Now the good stuff: rainfall, temp, pollution, elevation, etc.

We want:

* 1.0 inside the user’s “ideal” range or near their target
* smooth falloff as we move away
* the falloff scale to be per‑property so rainfall vs temperature are comparable

Let’s define a **normalized distance** from what the user wants.

#### Case A: user specifies a target *range* [L, U]

Example: rainfall between **1500–2500** is ideal.

We pick a “soft margin” (\delta_i) for that filter (how fast it should decay). This can be:

* a global fraction of that property’s world range, e.g.
  (\delta_{\text{rain}} = 0.25 \times (max_{world} - min_{world})),
* or a fraction of the user range width, e.g.
  (\delta_{\text{rain}} = 0.5 \times (U-L)).

Now, for tile value (x):

[
d_i(t) =
\begin{cases}
0 & \text{if } L \le x \le U \
\frac{L - x}{\delta_i} & \text{if } x < L \
\frac{x - U}{\delta_i} & \text{if } x > U
\end{cases}
]

Then convert distance to membership using a smooth curve, e.g.:

[
m_i(t) = \frac{1}{1 + d_i(t)^p}
]

* (p = 1) → soft penalty
* (p = 2) → harsher penalty outside the range

So:

* inside [L, U] ⇒ (d=0 → m=1)
* one “soft margin” away ((d=1)) ⇒ (m = 1/(1+1)=0.5)
* two margins away ((d=2), (p=2)) ⇒ (m = 1/(1+4)=0.2)

#### Case B: “at least” or “at most”

“Min growing days 30”, “Max pollution 0.2”, etc.

For “x ≥ M”:

[
d_i(t) =
\begin{cases}
0 & x \ge M \
\frac{M - x}{\delta_i} & x < M
\end{cases}
\qquad
m_i(t) = \frac{1}{1 + d_i(t)^p}
]

For “x ≤ M” just mirror it.

#### Case C: “target value” (peak at T, falloff around it)

Example: ideal temperature **18°C**, with tolerance ±8.

[
d_i(t) = \frac{|x - T|}{\delta_i}
,\qquad
m_i(t) = \frac{1}{1 + d_i(t)^p}
]

You can implement all numeric filters in terms of “compute a normalized distance (d_i) from what the user wants” → then `m = 1 / (1 + d^p)`.

---

## 2. Aggregate Critical vs Preferred

Now each active filter (i) has:

* a **membership** (m_i(t) \in [0,1])
* a **category**: Critical or Preferred
* an optional **weight** (w_i) (defaults to 1)

Let:

* (C) = set of critical filters the user turned on
* (P) = set of preferred filters the user turned on

We need:

* A critical score (S_C(t))
* A preferred score (S_P(t))
* Then one final score combining them

---

### 2.1 Critical: mean + “worst miss” (min) mixed

For criticals, we care a lot about the **worst miss**. A tile with:

* “perfect on 3 criticals, catastrophically bad on 1”
  should be heavily penalized.

So we compute two summaries:

[
\text{mean}*C(t) = \frac{\sum*{i \in C} w_i , m_i(t)}{\sum_{i \in C} w_i}
]

[
\text{min}*C(t) = \min*{i \in C} m_i(t)
]

Then blend them:

[
S_C(t) = (1 - \alpha_C) \cdot \text{mean}_C(t) ;+; \alpha_C \cdot \text{min}_C(t)
]

* (\alpha_C \in [0,1]) controls how “brutal” critical misses are
* (\alpha_C = 0.7) is a nice “very harsh but not fully min” default

**Examples (4 criticals):**

* (m = [1.0, 1.0, 1.0, 1.0])

  * mean = 1, min = 1 → ( S_C = 1 )

* (m = [1.0, 1.0, 1.0, 0.0])

  * mean = 0.75, min = 0
  * (S_C = 0.3 \cdot 0.75 + 0.7 \cdot 0 = 0.225)
    → one fully failed critical drags you down hard

* (m = [1.0, 0.8, 0.9, 0.7])

  * mean ≈ 0.85, min = 0.7
  * (S_C ≈ 0.3·0.85 + 0.7·0.7 = 0.745)

So **big misses on any critical** dominate, but small misses are smoothed by the mean.

If the user has **no criticals** (|C| = 0), we define:

[
S_C(t) = 1
]

so criticals don’t affect scoring at all in that case.

---

### 2.2 Preferred: softer mean + (optional) gentle min

For preferred filters, we want more “nice, but not deal‑breakers.”

Compute:

[
\text{mean}*P(t) = \frac{\sum*{j \in P} w_j , m_j(t)}{\sum_{j \in P} w_j}
]

[
\text{min}*P(t) = \min*{j \in P} m_j(t)
]

Then:

[
S_P(t) = (1 - \alpha_P)\cdot \text{mean}_P(t) ;+; \alpha_P \cdot \text{min}_P(t)
]

* (\alpha_P) much smaller, e.g. 0 or 0.2

  * (\alpha_P = 0) → pure average (simplest)
  * (\alpha_P = 0.2) → tiny nudge to avoid tiles that bomb one preferred

If |P| = 0, define (S_P(t) = 0).

---

## 3. Let the number of critical vs preferred filters influence weighting

We don’t want “12 preferred” to swamp “4 critical,” but we *do* want more criticals to matter more than just one.

Define:

* (c = |C|), (p = |P|).
* A normalized “critical share”:

[
r_C =
\begin{cases}
1 & \text{if } c > 0 \text{ and } p = 0 \
0 & \text{if } c = 0 \text{ and } p > 0 \
\frac{c^q}{c^q + p^q} & \text{otherwise}
\end{cases}
]

* (q ≥ 1) controls how quickly the ratio sharpens.

  * (q=1): linear
  * (q>1): heavily favors the bigger side

Now define a **baseline critical dominance** (\kappa_{base}), e.g. 0.5 or 0.7, and compute:

[
\kappa = \kappa_{base} + (1 - \kappa_{base}) \cdot r_C
]

* If you have only criticals: (r_C = 1 → \kappa = 1)
* If you have only preferred: (r_C = 0 → \kappa = \kappa_{base}), but then we special‑case C empty so it doesn’t matter
* If C=4, P=12, q=1 →
  (r_C = 4/(4+12) = 0.25). With (\kappa_{base} = 0.5):
  (\kappa = 0.5 + 0.5·0.25 = 0.625)

So here, **even with 4 critical vs 12 preferred, criticals get 62.5% of the “importance budget.”**

---

## 4. Combine them so criticals gate the whole thing

This is the key part: we want **critical performance to gate** how much preferred can help.

Define the **match score**:

[
S_{\text{match}}(t) = S_C(t) \cdot \left[\kappa + (1-\kappa)\cdot S_P(t)\right]
]

Intuition:

* If (S_C = 1):
  (S_{\text{match}} = \kappa + (1-\kappa)S_P), so preferred moves you between (\kappa) and 1.
* If (S_C) is small, everything is scaled down.
  Preferred can’t “rescue” a tile that bombs its criticals.

**Example: 4 critical, 12 preferred**

* C=4, P=12 → (\kappa ≈ 0.625)

* Tile A: perfect everywhere

  * (S_C = 1.0), (S_P = 1.0)
  * (S_{\text{match}} = 1.0)

* Tile B: perfect criticals, misses some preferred (say (S_P = 0.6))

  * (S_{\text{match}} = 1 * [0.625 + 0.375 * 0.6] = 0.625 + 0.225 = 0.85)

* Tile C: misses one critical badly (min_C = 0, mean_C = 0.75, (\alpha_C = 0.7))

  * (S_C = 0.3*0.75+0.7*0 ≈ 0.225)
  * even if (S_P = 1),
    (S_{\text{match}} = 0.225 * [0.625 + 0.375 * 1] = 0.225 * 1 = 0.225)

So:

> **Criticals dominate the score, and Preferred only refines among “already good” tiles.**

---

## 5. Mutators: world quality score that always nudges the result

Mutators are like hidden buffs/debuffs to quality of life. You said:

> Even if those AREN'T selected, their presence should move the needle a bit … weight all of them on a -10/0/10 scale.

Perfect. Treat them as a separate **mutator QoL score** (S_M(t)).

1. Assign each mutator (k) a base weight (q_k \in [-10, 0, 10]).

   * e.g. `Mountain = +5`, `Fertile = +6`, `Pollution_Increased = -8`, etc.

2. For a tile (t) with mutators (K(t)):

[
s_M(t) = \sum_{k \in K(t)} q_k
]

3. Convert to [0,1] via a sigmoid, so it saturates at really good/bad combos:

[
S_M(t) = \frac{1}{1 + \exp(-\beta \cdot s_M(t))}
]

* Pick (\beta) so typical extremes map nicely, e.g.
  If we expect s_M mostly in [-30,30], (\beta \approx 0.1) makes ±30 → ~0.047 / 0.953.

4. **Avoid double counting:** If the user explicitly filters on a mutator as Preferred or Critical:

* use it in (S_C) / (S_P)
* **exclude it from (S_M)** for that run.

Finally, incorporate mutators as a **small offset** around the main match:

[
S_{\text{final}}(t) = \text{clamp}*{0}^{1}\Big(
S*{\text{match}}(t) + \lambda_M \cdot [S_M(t) - 0.5]
\Big)
]

* (S_M = 0.5) (neutral) → no change
* (S_M > 0.5) → little positive bump
* (S_M < 0.5) → little negative bump
* (\lambda_M) small, e.g. 0.2, so QoL moves the needle but doesn’t overpower filters

Example:

* Tile A: (S_{\text{match}} = 0.85), (S_M = 0.9) → bump +0.2*(0.4)=+0.08 → 0.93
* Tile B: (S_{\text{match}} = 0.85), (S_M = 0.2) → bump +0.2*(-0.3)=-0.06 → 0.79

So two tiles equally good on filters are separated by mutators.

---

## 6. Full example: 4 critical, 12 preferred

Let’s plug in actual shapes.

### Setup

* Criticals:

  * C1: `IsCoastal = True` (binary)
  * C2: `HasRiver = True` (binary)
  * C3: rainfall in [1500, 2500] mm (range)
  * C4: temperature in [10, 20] °C (range)

* Preferred:

  * 6 binary (e.g. `Biome ∈ {TemperateForest, Grasslands}`, `HasRoad`, etc.)
  * 6 numeric (e.g. low pollution, moderate elevation, high plant density, etc.)

* Parameters:

  * (\alpha_C = 0.7,; \alpha_P = 0.0)
  * (\kappa_{base} = 0.5,; q=1)
  * (c=4,; p=12 \Rightarrow r_C = 4/(4+12) = 0.25)
  * (\kappa = 0.5 + 0.5·0.25 = 0.625)
  * (\lambda_M = 0.2)

### Tile X

Suppose tile X has:

* C1 (Coastal): yes → (m_{C1}=1)
* C2 (River): no → (m_{C2}=0)
* C3 (Rainfall): 1300mm, user range [1500,2500], δ=500, p=1

  * (d = (1500 - 1300)/500 = 0.4) → (m_{C3}=1/(1+0.4)=0.714)
* C4 (Temp): 14°C, ideal [10,20] → inside range → (m_{C4}=1)

Critical vector: ([1, 0, 0.714, 1])

* mean_C = (1+0+0.714+1)/4 = 0.6785
* min_C = 0

[
S_C = 0.3 * 0.6785 + 0.7 * 0 = 0.2036
]

Tile X nails 3 criticals but **completely lacks the river** flag: critical score ~0.2.

Preferred: imagine it does okay: say average membership (S_P = 0.7).

Then:

[
S_{\text{match}} = S_C \cdot [0.625 + 0.375·S_P]
= 0.2036 · [0.625 + 0.2625]
= 0.2036 · 0.8875
≈ 0.18
]

Even with pretty good preferred matches, it’s dragged down by missing a critical (river).

Now if the user didn’t mark River as **critical** but as **preferred**:

* C1, C3, C4 only. Suppose all 3 match well (Rain/Temp/Coastal all ~1) → (S_C ≈ 1)
* River moves into preferred side; S_P might still be ~0.7 if it misses river but nails others.

Then:

[
S_{\text{match}} ≈ 1 · [0.625 + 0.375·0.7] = 0.625 + 0.2625 = 0.8875
]

Huge difference: the **designation** Critical vs Preferred fundamentally changes how hard a miss hurts.

This is exactly what you want.

---

## 7. Optional sophistication: rarity‑based weights

Because you have global stats (e.g. **coastal tiles are 6.2%**, landmarks <1%, etc.), you can automatically give **rarer things more “value”** per filter:

For filter (i) that applies to (n_i) tiles out of N settleable tiles:

[
w_i = 1 + \rho \cdot \log\left( \frac{N}{n_i + 1} \right)
]

* (\rho \in [0,1]) controls how aggressive rarity should matter
* For common stuff: (n_i) ~ N → log term small → weight ≈ 1
* For rare stuff (landmarks, special mutators): n_i ≪ N → larger log term → bigger weight.

You plug (w_i) into the mean/min formulas above. This makes scores naturally prefer tiles that match **rare** desires.

---

## 8. Implementation sketch (conceptual)

For each search:

1. **Build active filters:** translate the user’s UI choices into objects with:

   * category: Critical / Preferred
   * type: Binary / Numeric / Set
   * parameters: target range, margin, etc.
   * precomputed weight (w_i)

2. **For each tile t (≈137k settleable):**

   * For each active filter i:

     * compute (m_i(t))
   * Compute:

     * `mean_C`, `min_C` ⇒ `S_C`
     * `mean_P`, `min_P` ⇒ `S_P`
   * Compute `kappa` from |C| and |P|
   * Compute `S_match(t)`
   * Compute `S_M(t)` from mutators ⇒ `S_final(t)`

3. **Select top X:**

   * Keep a Top‑N heap for X in {3,5,10,20,50,100}.

Because everything is just array math and simple nonlinear functions, this is easily fast enough on your cached world data.

---

## 9. TL;DR formula set

For a tile (t):

1. Per‑filter membership:

[
m_i(t) \in [0,1] \quad \text{(binary or numeric via distance→membership)}
]

2. Critical summary:

[
\text{mean}*C = \frac{\sum*{i \in C} w_i m_i}{\sum_{i \in C} w_i},\quad
\text{min}*C = \min*{i \in C} m_i
]
[
S_C =
\begin{cases}
1 & \text{if } |C| = 0 \
(1-\alpha_C) \cdot \text{mean}_C + \alpha_C \cdot \text{min}_C & \text{otherwise}
\end{cases}
]

3. Preferred summary:

[
\text{mean}*P = \frac{\sum*{j \in P} w_j m_j}{\sum_{j \in P} w_j},\quad
\text{min}*P = \min*{j \in P} m_j
]
[
S_P =
\begin{cases}
0 & \text{if } |P| = 0 \
(1-\alpha_P) \cdot \text{mean}_P + \alpha_P \cdot \text{min}_P & \text{otherwise}
\end{cases}
]

4. Critical dominance:

[
r_C =
\begin{cases}
1 & c>0, p=0 \
0 & c=0, p>0 \
\frac{c^q}{c^q + p^q} & \text{otherwise}
\end{cases}
]
[
\kappa = \kappa_{base} + (1-\kappa_{base}) \cdot r_C
]

5. Filter match:

[
S_{\text{match}} =
\begin{cases}
S_P & \text{if } |C| = 0 \
S_C \cdot [\kappa + (1-\kappa) S_P] & \text{otherwise}
\end{cases}
]

6. Mutators:

[
s_M = \sum_{k \in K(t)\setminus \text{(userFilteredMutators)}} q_k
,\qquad
S_M = \frac{1}{1+\exp(-\beta s_M)}
]

7. Final score:

[
S_{\text{final}} = \text{clamp}*{0}^{1}\left(
S*{\text{match}} + \lambda_M (S_M - 0.5)
\right)
]



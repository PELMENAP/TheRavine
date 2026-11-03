# TheRavine Optimization Guide

The scale of life is the world's main resource. Every millisecond saved becomes one more creature, one more connection, one more step of adaptation.

This file is a set of principles and techniques. When working on code, the AI checks it in order: first the principles, then the "symptom → technique" catalog, then the procedure at the end.

---

## 1. Budget Before Code

A frame is 16.6 ms. The world tick is shared by all creatures, so before writing code, estimate how much of that budget one creature gets.

- Estimate complexity against the creature count before implementing: O(N²) on 10⁴ agents is 10⁸ operations, and the frame is already lost.
- Rule of thumb: about 10⁸ simple operations per second per core; Burst with SIMD gives several times more.
- If the estimate does not fit, change the algorithm instead of micro-optimizing.
- Measure on the worst-case scenario: maximum creatures, maximum events, worst density.
- A small brute force is fine when its size is bounded and known: depth 4 with branching 12 is ~2·10⁴ calls. Compute the bound, do not guess it.

## 2. The Hot Path Is Sacred

Hot paths: agent tick, Decide / ForwardPass / Train, perception, Ribosome.Step, movement, instincts, colony blackboard.

- Zero allocations: no LINQ, `new[]`, `ToArray()`, `List`, closures, or boxing.
- Unity.Mathematics instead of Mathf.
- Buffers are allocated once and reused.
- `NativeArray` / `NativeList`: explicit `Dispose` and an `IsCreated` check.
- Virtual calls and interfaces in the tick only where unavoidable.
- Prefer branchless code in Burst loops: `math.select`, masks, and arithmetic instead of unpredictable `if`.

## 3. Data Shaped by Computation

A data structure is defined by how it is traversed, not by how it is thought about.

- Mass per-agent computations (instincts, boids, colony fields): SoA `NativeArray` by ManagerIndex + Burst job.
- Dense indices instead of dictionaries keyed by id. On removal: swap-back and index synchronization across every SoA array.
- Flags and sets are bitmasks (`uint`, `ulong`); count with `math.countbits`, intersect with `&`.
- Hot fields separate from cold ones: only what the tick reads goes into the cache line.
- Neural networks: weights in one contiguous buffer per layer, no per-neuron objects; batch many agents' ForwardPass into one job.
- Group by key with a counting sort (e.g. agents by grid cell) instead of a hash map of lists.

## 4. Do Not Recompute What Did Not Change

The world changes locally. Recomputation must be just as local.

- An aggregate is stored and patched by delta: when a creature leaves, subtract only its contribution.
- Dirty flag instead of recomputing every frame.
- Each object enters and leaves a structure O(1) times; a repeated event is a no-op.
- Rarely changing data is computed rarely: heavy decisions are spread across frames (time-slicing); some agents think this frame, others the next.
- Lazy evaluation with timestamps: store the value and the time it was last updated; when read, catch up with a closed form (e.g. decay `v · exp(−k·Δt)`) instead of updating every tick.
- Hysteresis on state switches (two thresholds instead of one) so agents do not thrash between states every frame.

## 5. Invariants Beat Simulation

Often the answer is determined not by the sequence of steps but by what is conserved.

- Before simulating a process, find the quantity that characterizes it: sum, counters, parity, monotonicity.
- If the result depends only on a histogram, store the histogram, not the history.
- Find which part of the state stops influencing the future and do not store it.
- A wave spreading over a grid from one source with no obstacles is a metric, not a simulation: 8-neighborhood gives Chebyshev distance max(|dx|, |dy|), 4-neighborhood gives Manhattan |dx| + |dy|. Rings of equal distance alternate phases by parity.

## 6. Prune the Hopeless

Brute force is acceptable if it is small and smart.

- Branch and bound: if an optimistic estimate of a branch is no better than the current result, do not expand it.
- Monotone constraints are checked as soon as an element is added: a violation will not disappear later.
- Do not permute identical objects among themselves: enumerate in increasing index order.
- Exit early as soon as the maximum possible result is reached.
- Greedy is acceptable when proven by an exchange argument: "swapping for something smaller does not break the solution".
- Check the cheap necessary conditions first (parity, sums, minimum size): rejection in O(1) before any search or construction.

## 7. Integers and Thresholds

- Double or scale coordinates so that checks stay in integers.
- Compare squared distances, no `sqrt`.
- Saturate counters at a threshold: above it the exact value is not needed, and the state space shrinks.
- Expensive functions with a small domain go into a table (LUT).
- Estimate the maximum magnitude of a value before choosing its type. `float` holds integers exactly only up to 2²⁴ ≈ 1.7·10⁷; distant coordinates and large counters go into `int` / `long` or get recentered.
- Check sums and products for overflow on worst-case inputs.

## 8. Precision Is Not the Goal

The world must be alive, not reproducible.

- Determinism is not required: RavineRandom, parallel reductions with floating order, and `FloatMode.Fast` are allowed.
- Seeds are not threaded through the system.
- An approximate solution that fits in the frame beats an exact one that does not.
- Creatures far from the observer may live more coarsely, but by the same rules (equality comes from the world's philosophy).
- Stochastic updates are fine: update an expensive quantity with probability p and scale the effect by 1/p.

## 9. Verify the Fast with the Slow

- Every non-trivial optimization has a naive reference version.
- Stress test: random inputs, compare the Burst version against the managed version.
- A dedicated benchmark scenario with worst-case load; regressions are seen in measurements, not by eye.
- Hand-check the edge cases: empty input, a single element, values exactly on thresholds, boundaries of the domain.

## 10. Frame of Reference and Symmetry

If the answer depends only on relative position, absolute coordinates are not needed.

- Move the computation into the frame of the source or observer (floating origin): values get smaller, floats get more precise, formulas get simpler.
- Reflect the problem into one quadrant or octant and compute only that. Count axes and the origin exactly once.
- Cut a region along its symmetry axes and reduce each piece to one canonical case.

## 11. Contribution Instead of Scenarios

- A sum over all orders or scenarios equals Σ over states × the number of scenarios in which that state occurs. For a subset S of size k that is "the first k of an order", that number is k!·(m−k)!.
- First find what the result actually depends on: the set, not the order; the size, not the composition.
- Compute each element's contribution separately and add them up (linearity), instead of enumerating combinations.

## 12. The Bottleneck Decides Everything

- If the rest of the system always completes itself, check only the limiting resource.
- Flow along a chain (road, conveyor, resource transfer through a colony) equals the minimum over cuts and is computed in one left-to-right pass, without a flow algorithm.
- Between neighboring points of change a value is constant: store segments between events, not every position.

## 13. Closed Forms over Regions

- If a count over a quadrant [0..X]×[0..Y] has a formula F(X, Y), a rectangle query is four calls of F with inclusion–exclusion: F(r₁,r₂) − F(l₁−1,r₂) − F(r₁,l₂−1) + F(l₁−1,l₂−1). No table is needed, so it works on an unbounded world.
- A ring-structured count splits into full rings (summed in closed form) plus clipped rings (a constant width each).

## 14. Construction: Regular Base plus Local Patch

- To generate a structure with prescribed properties (graph degrees, connections, layouts), build a regular base first and then patch it locally: e.g. a ring with neighbors at distances 1..k plus a matching to raise selected degrees.
- Prove that the base satisfies the hard constraints (no duplicates, connectivity) once, instead of validating at runtime.

## 15. Many Agents, One Computation

- Many agents moving to the same goal: one BFS / Dijkstra from the goal builds a flow field that every agent reads in O(1).
- Several sources at once: multi-source BFS, a single pass instead of one per source.
- Influence maps (danger, food, scent) are fields updated once per tick and sampled by agents, not recomputed per agent.
- Interest management: agents react only to what is inside their perception radius; spatial hash cell size equals that radius, so only 9 cells are checked in 2D.

---

## Catalog: Symptom → Technique

| Symptom | Technique |
|---|---|
| All-pairs checks (perception, boids, collisions) | Uniform grid / spatial hash with cell size = interaction radius: O(N·k) instead of O(N²) |
| "Are A and B connected", "same colony", "reachable" | DSU (disjoint set union) |
| Connectivity under a threshold (passability by creature weight or size) | Sort edges by threshold, offline DSU with a pointer; the threshold only changes monotonically |
| Sum or density over an area of a colony field | 2D prefix sums: O(1) per query |
| Mass addition of a value over a range or area | Difference array, one pass at the end of the tick |
| Dynamic sums, minimums, priorities by index | Fenwick tree / segment tree |
| Range minimum over static data | Sparse table, O(1) per query |
| Overlap of orthogonal zones (influence, fog, territories) | Sweep line over strips, coordinate compression, two pointers over intervals |
| An aggregate over all creatures is recomputed every frame | Incremental delta on add / remove |
| Enumerating combinations (abilities, slots, plans) | Bitmask; for larger sizes, meet-in-the-middle |
| Meet-in-the-middle where the halves must interact | Split by a sorted key so that the halves interact through a single number |
| Sum of minimums / maximums over all pairs from two groups | Sort both groups + two pointers + prefix sums |
| Sum over all orders or scenarios | Switch to subsets with a k!·(m−k)! weight; count per-element contributions |
| Planner (GOAP / HTN) expands too much | Branch and bound, symmetry breaking, early exit |
| Finding the threshold or parameter at which a condition starts to hold | Binary search on the answer (if the check is monotone) |
| A linear process many ticks ahead (growth, decay) | Closed form or matrix exponentiation |
| A value that decays or grows while nobody reads it | Timestamp + closed-form catch-up on read |
| Ancestors and paths in hierarchies (tasks, lineages) | Binary lifting |
| Queries over events in time | Sorting + two pointers / sliding window |
| Many timers or scheduled events | Bucketed timing wheel; heap with lazy deletion |
| Expensive activation function or instinct curve | LUT with interpolation |
| Repeated removal / ban events | "Already processed" flag, amortized O(1) per object |
| Agent decisions do not fit in a frame | Time-slicing: some agents think this frame |
| Agents flip between two states every frame | Hysteresis: separate enter and exit thresholds |
| Signal / sound / panic spreading over a grid from a source, no obstacles | No BFS: Chebyshev distance for 8-neighborhood, Manhattan for 4-neighborhood; the wave is rings |
| Counting over a rectangle on a huge or unbounded field | Analytic F(quadrant) + inclusion–exclusion over 4 corners |
| A property alternates by rings or steps (wave parity, phases) | Closed form over rings: full rings + clipped rings |
| Throughput of a chain with bypass routes | Minimum over cuts, greedy pass along the DAG |
| Many agents pathfinding to the same goal | Flow field from one BFS / Dijkstra at the goal |
| Weighted random choice in the tick | Alias method: O(1) per sample after O(n) setup |
| Picking k random items from a stream | Reservoir sampling, no buffer of the whole stream |
| Generating a graph or network with prescribed degrees | Necessary conditions check → regular base → matching |
| Large world coordinates lose float precision | Recenter around the observer (floating origin) or store in integers |
| Parallel update reads values that are being written | Double buffering: read previous state, write next state |

---

## Procedure for the AI

1. Determine whether the code is on a hot path (section 2). If it is, the constraints of section 2 are mandatory.
2. Estimate complexity at the maximum creature count (section 1).
3. Look for an invariant, symmetry, or closed form that makes simulation unnecessary (sections 5, 10, 13).
4. Find the symptom in the catalog and take the technique.
5. Check value ranges and types (section 7).
6. Write in the style of the surrounding code: no `#region`, no XML summaries, no comments.
7. If the optimization is non-trivial, keep the naive version for comparison (section 9).
8. Priority order on conflict: performance → correctness → extensibility.

---

*Every millisecond the algorithm does not eat goes to life.*

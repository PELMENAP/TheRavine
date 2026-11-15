# Skill graph and neuroplasticity — design

Source: the v3.0 math model, translated to English and bound to the code of this repository.
Implemented in `Assets/Scripts/EntitySystem/AI/Skills/`; map: `docs/map/ai.md`, section Skills.
Every number below is a default of a `SimulationRules` field or of the atlas asset, never a literal in logic.

## 1. Facts of the codebase the design is built on

1. Two creature families exist. Agents are `EntityModel`, indexed by `ManagerIndex`, ticked by `EntityManager`.
   The player is `PlayerEntity` (`AEntity`), ticked by `EntitySystem.FixedUpdate`, and has no `ManagerIndex`.
   They already share rules through static code (`CreatureCore`). The skill graph follows the same pattern.
2. The genome is the flat float struct `GeneticParameters` (`Assets/Scripts/EntitySystem/AI/GeneticParameters.cs`):
   per-gene pick from a parent in `Crossover`, then `Mutate` inside `ParameterRanges`. Skill genes are new fields there.
3. Selection works inside a colony (`EvolutionService.EvolveColony`, `SelectMate`); every wild creature shares the
   single `ColonyState` `_wild`. Talent is therefore a gene of the individual; topology belongs to `ColonyState`.
4. An agent's effective stats are `EntityTuning.Express(base, genes, casteModifiers)`, recomputed on spawn and on a
   caste change (`EntityModel.cs` near lines 293 and 613), not per tick. `Tuning` is the existing multiplier cache.
5. A completed agent action passes one place: `EntityCommandRunner.cs` near line 85 (`stats.RecordCompleted(action)`).
6. Periodic colony work runs in `EntityManager.CloseTickWindow` (near line 586), outside the per-agent loops.
7. Swap-back removal is one place: `ColonyPopulation.RemoveEntitySwapBack` (near line 269); registry in
   `docs/OPTIMIZATION.md` section 14; tests in `Assets/Tests/EditMode/SoaSwapBackTests.cs`.
8. Simulation time is `SimulationClock.Time` (seconds, float) and `TimeD`; there is no integer tick counter.
9. `Assets/Scripts/Model/EntitySkills/` already holds ability skills (`ISkill`, `FlyingSkill`, `SwimmingSkill`) in an
   assembly named `SkillSystem`. New types use the prefix `SkillGraph` / `SkillAtlas`; no type is named `SkillSystem`.
10. The project has no UI Toolkit code yet (no UXML, USS or `UIDocument`); the module `com.unity.modules.uielements`
    is installed. `UnityEditor.Experimental.GraphView` is editor-only and must not be used in the game.
11. A remote player acts through intent RPCs in `PlayerController`, each with a server budget and a row in
    `docs/SECURITY.md` section 14.

## 2. Deviations from the v3.0 text, and why

| v3.0 | Here | Reason |
|---|---|---|
| `CachedMultipliers[maxPop * N]` read in the tick | Skills produce a `SkillModifiers` struct consumed by `EntityTuning.Express`; tuning is re-expressed when an integer level changes | Fact 4: the cache already exists; no new per-tick read |
| `p_origin`, `w_plast` in a separate SoA, interpolated on crossover | Six new `GeneticParameters` fields, existing `Crossover` and `Mutate` | Fact 2: one genome, one mutation path; the landscape stays continuous through mutation |
| `uint LastUsedTicks` | `float` seconds of `SimulationClock.Time` | Fact 8 |
| Lazy decay on every read | Stored levels are refreshed by a periodic Burst sweep; readers use stored values | No `exp` outside the sweep and the XP event |
| Player data in the agent SoA | Each `PlayerEntity` owns a store of capacity 1 and its own topology | Fact 1 |
| Type `SkillSystemSoA`, constant stride 64 | `SkillGraphStore`, stride `SkillAtlas.Count` | Fact 9; N is data |
| A lone wild creature is a colony of one | The wild `ColonyState` owns one topology like any colony | Fact 3; a per-individual wild topology is deferred |

## 3. Data layers

### 3.1 `SkillAtlas` (ScriptableObject, one per world, static after load)

- `Count` N, 16 ≤ N ≤ 64. `float2 Positions[N]` in the unit square. `ulong Potential[N]`: adjacency rows of the
  potential frame (Delaunay edges plus accepted second diagonals). `float MeanEdgeLength`.
- Per skill: display name, `SkillChannel` (which `SkillModifiers` field it scales, or `None`), `IsDivider`,
  curve `(p, L0, c)`, XP cost `(BaseXp, q)`, `SkillNeed` channel for the solver.
- XP sources: for every `EntityAction` up to two `(skillId, xp)` pairs. Player events map to the same `EntityAction`
  values, so one table serves every creature.
- Building (editor menu and `SkillAtlas.BuildDefault()` used when no asset is bound):
  1. Poisson-disc points with radius 0.83 / sqrt(N), oversampled and thinned at random to exactly N.
  2. Delaunay triangulation (Bowyer–Watson is enough for N ≤ 64).
  3. For every convex quad of two adjacent triangles compute a cocircularity measure; take the best 15 %; add the
     second diagonal unless one of its two triangles already belongs to an accepted quad.
  4. K5 validator: for every 4-clique `{a,b,c,d}` require `Potential[a] & Potential[b] & Potential[c] & Potential[d] == 0`;
     a failing second diagonal is dropped. The same check is an EditMode test.

### 3.2 `SkillGraphTopology` (one per `ColonyState`, one per `PlayerEntity`)

- `NativeArray<ulong> Active` (N rows), `ulong IslandMask` (immutable), `ulong PrunedMask`,
  `FixedList32Bytes<ushort> PrunedBridges` (packed `(u << 8) | v`, at most 15),
  `NativeArray<byte> CliqueSize`, `NativeArray<float> CycleFactor`, `NativeArray<ulong> ComponentOf` (component mask
  per vertex), `float PhiPeak`, `byte SpentEdgePoints`, `float4 ForestGenes`, `int Revision` (incremented on any change).
- Forest at creation: edge weight `w = |xu − xv| · (1 + Σk gk · exp(−2 · |m − ck|²))`, `m` the edge midpoint, `ck` the
  four corners, `g ∈ [−0.25, 0.25]⁴`. Kruskal over the potential frame gives the tree.
- Islands, independent of any root: for each tree edge `s(e)` is the size of the smaller side; candidates have
  `2 ≤ s(e) ≤ max(3, N / 6)`, sorted by weight descending; accept up to `m_drop ∈ {1, 2}` edges whose smaller sides
  are disjoint from the islands already taken; fewer or zero is valid. Their union is `IslandMask`.
- After every change of `Active`: one Tarjan pass yields bridges and biconnected blocks; then `CliqueSize`,
  `CycleFactor`, `ComponentOf` are rebuilt. Clique search by masks: a new K4 always contains the new edge.

### 3.3 `SkillGraphStore` (per-agent SoA, `IDisposable`, exposes `RemoveSwapBack(int index, int last)`)

Stride N buffers: `Levels` (continuous, as of `LastUsed`), `Peaks`, `LastUsed` (seconds), `Talent`.
Stride 1 buffers: `Root` (byte), `Dirty` (byte, set when an integer level changed).

### 3.4 Genes (new `GeneticParameters` fields with ranges)

`SkillOriginX`, `SkillOriginY` in [0, 1]; `PlastHebb`, `PlastStruct`, `PlastExplore`, `PlastNeed` in [0, 2].

## 4. Rules (all in a static `SkillGraphMath`, shared by agents and players)

Notation: `L` continuous level, `P` peak, `⌊L⌋` the level used by effects.

1. Root of an individual: the vertex outside `IslandMask` nearest to `(SkillOriginX, SkillOriginY)`.
2. Talent, computed at birth and when genes are overwritten:
   `d = 0.65 · |xv − origin| / sqrt(2) + 0.35 · dTree(root, v) / treeDiameter`, tree distances on the full tree before
   island removal; `tau(v) = tauMin + (1 − tauMin) · exp(−lambdaTau · d)`, `tauMin = 0.25`, `lambdaTau = 2.5`.
3. Open: `P(v) ≥ 1` or `v = root`. Available: in the root's component of `Active`, not in `PrunedMask`, and open or
   adjacent in `Active` to an open vertex. XP for an unavailable skill is dropped.
4. Visible but not available (fog of war): for an open `v`, every potential edge `(v, w)` not in `Active`, and `w` as
   a silhouette, also when `w` is on an island. Such an edge is a build candidate.
5. Edge budget: `phi = Σv ln(1 + P² / (P + Lref²))`, `Lref = 6`. For a colony `phi` uses the mean peak per skill
   over living members. `PhiPeak = max(PhiPeak, phi)`. `earned = floor((PhiPeak / beta)^(1 / gamma))`, `beta = 3.5`,
   `gamma = 1.35`. `available = earned − SpentEdgePoints`. Build costs 1, restore of a pruned bridge costs 2.
6. Clique bonus on XP: `CliqueSize` 3 → 1.10, 4 → 1.40, else 1. Purely topological.
7. Decay factor: for the block `B` of `v` with the largest `s = (|E_B| − |V_B| + 1) · ln(1 + |V_B|)`:
   `R(v) = Rmin + (1 − Rmin) / (1 + eta · s)`, `Rmin = 0.25`, `eta = 0.35`; `R = 1` outside cycles.
8. Decay: `floor = alpha · P`, `alpha = 0.25`; `L(t) = floor + (L(t0) − floor) · exp(−lambda0 · R · (t − t0))`.
   Applied by the sweep and at the start of an XP event; pruned vertices do not decay.
9. Pull: `dEnv = max(P − L, max over active neighbours u of (MeanEdgeLength / |xu − xv|) · max(0, Lu − L))`;
   `Blogic = 1 + kappa · ln(1 + dEnv / (L + L0logic))`, `kappa = 0.60`, `L0logic = 10`.
10. Focus: `Bprune = 1 + mu · popcount(PrunedMask) / N`, `mu = 0.45`.
11. XP event: materialise `L`, then `L += xp / (BaseXp · (L + 4)^q) · tau · Bclique · Blogic · Bprune`, `q = 0.55`;
    `P = max(P, L)`; `LastUsed = now`; if `⌊L⌋` changed, set `Dirty`.
12. Effect: `M(L) = 1 + c · ((⌊L⌋ + L0)^p − L0^p)`; a divider channel uses `1 / M`. A skill that is pruned or
    outside the root's component gives 1. Profiles: A `(0.80, 6, 0.12)`, B `(0.75, 8, 0.12)`, C `(0.45, 4, 0.22)`.
13. Topology change (build, prune, restore): first materialise the levels of every member with the old `R`, then
    change `Active`, run Tarjan, rebuild the caches, increment `Revision`, mark members `Dirty`.
14. Prune: allowed for a bridge of the main component whose far side (the side without the acting root; for a
    colony, the smaller side) is not an island. Its vertices enter `PrunedMask`; levels are frozen. Restore clears
    them and sets `LastUsed = now`.
15. Solver (`SkillGraphSolver`, the same code for a colony and for a player with auto mode on), per candidate edge
    `(u, v)` with `u` open for the colony mean:
    `H = ln(1+Lu) · ln(1+Lv) / ln²(1 + Lnorm)`, `Lnorm = 100`;
    `S = (BcliqueNew − 1) + (Rold(u) − Rnew(u))`;
    `E = [Lv < 1] · tauMean(v) · (1 + 0.5 · [v on island])`;
    `C = saturate(need(v))` from existing `ColonyStats` fields selected by the skill's `SkillNeed`;
    `U = wH · H + wS · S + wE · E + wC · C`, weights are the mean `Plast*` genes of living members.
    It builds the best edge while a point is available. Under survival stress it prunes a bridge whose far side has
    at least 4 vertices and a summed mean level below `SkillPruneUnusedLevel`.

## 5. Proposed starting atlas (N = 18; author tunes it in the asset)

| Skill | Channel | Profile | XP from `EntityAction` |
|---|---|---|---|
| Locomotion | `Speed` | B | `Wander`, `GoToPoint`, `ReturnNest`, `Follow` |
| Sprint | `RunSpeed` | B | `Flee` |
| Endurance | `MoveEnergyCost` (divider) | B | `GoToPoint`, `ReturnNest` |
| Strike | `Attack` | A | `Attack` |
| Tempo | `Cooldown` (divider) | B | `Attack` |
| Menace | `GroupBonus` | B | `Threaten` |
| Toughness | `DamageTaken` (divider) | B | `Threaten`, `Flee` |
| Vitality | `Health` | A | `Rest` |
| Recovery | `RestHeal` | B | `Rest`, `Groom` |
| Reserves | `EnergyCapacity` | A | `Eat` |
| Digestion | `EatSpeed` | B | `Eat` |
| Scent | `Detection` | C | `ApproachFood`, `RememberPoint` |
| Hauling | `Carry` | B | `PickUp` |
| Storekeeping | `StoreEfficiency` | B | `StoreFood` |
| Nursing | `Feed` | B | `ShareFood` |
| Fertility | `ReproduceCost` (divider) | B | `Reproduce` |
| Flight | `None` (reserved for `FlyingSkill`) | B | — |
| Swimming | `None` (reserved for `SwimmingSkill`) | B | — |

Channels are the multiplier fields that `CasteModifiers` and `EntityTuning.Express` already apply.

## 6. In-game graph view (UI Toolkit)

- `SkillGraphSnapshot` (EntitySystem assembly): preallocated arrays filled by `SkillGraphQuery.Fill(source, snapshot)`:
  per vertex position, `⌊L⌋`, fraction to the next level, peak, floor, talent, state
  (`Open`, `Available`, `Silhouette`, `Hidden`, `Island`, `Pruned`), clique size, decay factor; per edge kind
  (`Active`, `Bridge`, `Potential`); budget `earned`, `spent`, `PhiPeak`; `Revision`.
  Hidden vertices and edges are not written to the snapshot at all: the fog rule lives in the query, not in the view.
- `SkillGraphPanel` (UIInventory assembly, `Assets/Scripts/UI/SkillGraph/`): a `MonoBehaviour` that owns a
  `UIDocument`; visual tree built in C#; styles in one USS file.
- `SkillGraphCanvas : VisualElement`: edges drawn in `generateVisualContent` with `Painter2D` (solid active, thicker
  bridge, dashed potential built from short segments); pan by drag, zoom by wheel through a content transform.
- `SkillNodeElement : VisualElement`: one per visible vertex, absolutely positioned; level ring by `Painter2D.Arc`;
  colour by talent; tooltip card with name, level, peak, floor, talent, bonuses.
- Commands: click a potential edge or silhouette → build; click a bridge → prune (with confirmation); click a pruned
  bridge → restore; a toggle switches the solver's auto mode. Every command goes through `ISkillGraphCommands`,
  which validates with the same `SkillGraphMath` checks and, for a remote player, sends the intent RPC.
- Refresh: `schedule.Execute` at a fixed UI period compares `Revision` and a level checksum; the tree is rebuilt
  only on a change. Nothing runs while the panel is closed.

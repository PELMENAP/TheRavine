# Agent AI — local rules

Map: `docs/map/ai.md`. Hot methods and tick order: `docs/map/HOT_PATHS.md`. Before a hot-path change: `docs/OPTIMIZATION.md`.

- Agents tick only from `EntityManager.Update`. Do not add a second tick path, a coroutine or a UniTask loop for agents.
- Time is `SimulationClock`. `Time.time`, `Time.deltaTime` and `UniTask.Delay` are forbidden here.
- Rules are read in the tick through `SimulationRules.Frame` by `ref readonly`. A new constant goes into `SimulationRules`,
  plus a `RulesFrame` field if the tick reads it.
- Per-agent arrays are indexed by `EntityModel.ManagerIndex`. A new per-agent system exposes `RemoveSwapBack(int index, int last)`,
  is called from `ColonyPopulation.RemoveEntitySwapBack`, and is added to `SoaSwapBackTests` and to `docs/OPTIMIZATION.md` section 14.
- Never cache an index into a per-agent array across a tick window.
- A change of `InputVectorizer.VectorSize`, `ActionCatalog.Count`, `PlanCatalog.Count` or any layer size increments
  `SharedBrainSnapshot.Version`; say in the report that saved brains are incompatible.
- Reuse before writing: `EntitySpatialGrid`, `ChunkFoodIndex`, `NestState`, `ContextSlabs`, `FloatSlabPool`, `TickWindowBudget`.
- Jobs: schedule the batch, combine handles, one `Complete`. No `Complete` inside a per-agent loop.
- No `Transform` read in the tick: positions come from `MotionSystem.PositionOf`.
- No scripted roles: behaviour comes from the brain, instincts and rules, not from a per-caste or per-player branch.
- Agent code never reads `ViewOrientation`.
- New hot code gets a test through `AllocationAssert.Zero`.
- Debug views in `Assets/Scripts/EntitySystem/AI/Debug/` cost nothing while they are off.

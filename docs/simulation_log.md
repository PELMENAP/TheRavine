# Simulation log: column dictionary

The file is `simulation_log_<yyyyMMdd_HHmmss>.csv` in `Application.persistentDataPath/NeuralModels/` (`NeuralModelStorage.BaseDirectory`). It is written by `Assets/Scripts/EntitySystem/AI/Debug/SimulationLog.cs`.

## When rows are written

- Logging is off by default. Turn it on with `SimulationRules.simulationLogEnabled`. The interval is `simulationLogIntervalSeconds` (default 10 s, at least 1 s).
- Only the authority instance writes, because `EntityManager.Update` returns early elsewhere.
- The first sample fires when the simulation starts. Each sample writes one `world` row, then one `colony` row for every colony with `MemberCount > 0`. The wild (predator) colony is included.
- Counter columns are **window deltas**: the cumulative `ColonyStats` counter now, minus its value at the previous sample. The first sample's window starts at zero, so it includes the initial spawns.
- The window length is the difference of `time_s` between two consecutive samples of the same scope. It equals `simulationLogIntervalSeconds`, up to frame granularity.
- The `world` row sums the deltas of **all** colonies, including colonies that have no members now. Snapshot columns (`mean_fill`, `rest_at_nest_share`, `storage_share`) are member-weighted means over colonies with members.
- Numbers use the invariant culture; floats use format `G6`. World-only columns are empty in colony rows.

## Columns

| Column | Scope | Unit | Meaning and computation |
|---|---|---|---|
| `time_s` | both | s | `SimulationClock.TimeD` at the sample |
| `season` | both | −1…1 | `SeasonClock.Value(time)` |
| `winter` | both | 0/1 | `SeasonClock.IsWinter(time)` |
| `scope` | both | text | `world` or `colony` |
| `colony_id` | both | id | `ColonyState.ColonyId`; `-1` for the world row |
| `alive` | both | creatures | world: `EntityManager.Entities.Count`; colony: `MemberCount`. A snapshot, not a delta |
| `born` | both | creatures | Δ `ColonyStats.Born` (every spawn, including initial, reseed and predator spawns) |
| `died` | both | creatures | Δ `ColonyStats.Died` |
| `deaths_killed` | both | creatures | Δ `DeathsByCause[Killed]` |
| `deaths_starved` | both | creatures | Δ `DeathsByCause[Starved]` |
| `deaths_virus` | both | creatures | Δ `DeathsByCause[Virus]` |
| `deaths_toxic` | both | creatures | Δ `DeathsByCause[Toxic]` |
| `deaths_age` | both | creatures | Δ `DeathsByCause[Age]` |
| `deaths_burn` | both | creatures | Δ `DeathsByCause[Burn]` |
| `mean_lifespan_s` | both | s | Δ `LifespanSum` / Δ `Died` over the window; 0 when nobody died |
| `<action>_started` | both | commands | Δ `ActionsStarted[action]`, counted in `EntityCommandRunner.Start` |
| `<action>_completed` | both | commands | Δ `ActionsCompleted[action]`, counted in `EntityCommandRunner.Finish` with status `Completed` |
| `<action>_failed` | both | commands | Δ `ActionsFailed[action]`, counted in `EntityCommandRunner.Finish` with status `Failed` |
| `plans_started` | both | plans | Δ of the sum over plan kinds of `PlansStarted`. **Counted when a plan ends** (`PlanRunner.cs:175`), so it is "plans ended in the window" |
| `plans_completed` | both | plans | Δ of the sum over plan kinds of `PlansCompleted`: plans that ended with status `Completed` |
| `brain_share` | both | 0…1 | Δ `CommandsBySource[Brain]` / Δ of all `CommandsBySource`; 0 when no command started |
| `instinct_share` | both | 0…1 | Δ `CommandsBySource[Instinct]` / Δ of all `CommandsBySource` |
| `plan_share` | both | 0…1 | Δ `CommandsBySource[Plan]` / Δ of all `CommandsBySource` |
| `relocations` | both | moves | Δ `ColonyStats.Relocations`, counted in `ColonyState.Relocate` |
| `attacks_own` | both | strikes | Δ `Attacks[Own]` (victim of the same colony) |
| `attacks_foreign` | both | strikes | Δ `Attacks[Foreign]` |
| `attacks_player` | both | strikes | Δ `Attacks[Player]` |
| `foreign_harm_ticks` | both | ticks | Δ `ForeignHarmTicks`: agent ticks in which the nearest foreign creature lost HP |
| `warmth_draw_ticks` | both | ticks | Δ `WarmthDrawTicks`: agent ticks in which a creature paid basal drain from warmth |
| `mean_fill` | both | 0…1 | snapshot of `ColonyStats.MeanFill` (mean stomach fill at the last nest tick); world: member-weighted mean |
| `rest_at_nest_share` | both | 0…1 | snapshot of `RestAtNestShare`; world: member-weighted mean |
| `storage_share` | both | 0…1 | snapshot of `StorageShare` (nest storage / `NestStorageNorm`); world: member-weighted mean |
| `ledger_residual` | world | energy | Δ `EnergyLedger.ResidualSum` over the window: the sum of per-sample residuals |
| `fitness_p10` | world | fitness | `EvolutionService.FitnessP10` at the last fitness update |
| `fitness_p50` | world | fitness | `EvolutionService.FitnessP50` |
| `fitness_p90` | world | fitness | `EvolutionService.FitnessP90` |
| `fitness_max` | world | fitness | `EvolutionService.FitnessMax` |
| `fitness_outliers` | world | creatures | `EvolutionService.FitnessOutliers`: fitness above `median + OutlierSigma · spread` |
| `avg_fitness` | world | fitness | `EvolutionService.AvgFitness` |
| `avg_entropy` | world | nats | `EvolutionService.AvgEntropy`, mean coordinator policy entropy |

`<action>` is every `EntityAction` name in enum order, in snake case: `idle`, `wander`, `remember_point`, `go_to_point`, `attack`, `flee`, `eat`, `reproduce`, `speech`, `mimic`, `rest`, `threaten`, `share_food`, `approach_food`, `return_nest`, `pick_up`, `store_food`, `follow`, `move_nest`, `groom`. That makes 60 columns. The full header has 97 columns.

## Success metrics

All ratios are computed inside one window, from the deltas of one row.

| Metric | Formula |
|---|---|
| eat success rate | `eat_completed / eat_started` |
| action success rate (any action) | `<action>_completed / <action>_started` |
| action failure rate | `<action>_failed / <action>_started` |
| plan completion rate | `plans_completed / plans_started` |
| idle share | `idle_started / Σ <action>_started` |
| share of an action | `<action>_started / Σ <action>_started` |
| deaths per minute | `died / window length × 60` |
| death cause share | `deaths_<cause> / died` |
| births per minute | `born / window length × 60` |
| net growth | `born − died` |
| attacks on own colony share | `attacks_own / (attacks_own + attacks_foreign + attacks_player)` |
| decision source mix | `brain_share`, `instinct_share`, `plan_share` (they sum to 1 when any command started) |

### Notes on reading the metrics

- A command started near the end of a window can finish in the next one, so per window `completed + failed` can differ from `started`.
- Interrupted commands are counted in `ColonyStats.ActionsInterrupted` but have no column. The remainder `started − completed − failed` approximates interrupted plus still running.
- A command abandoned because its creature was destroyed (`EntityCommandRunner.Abandon`) is not counted as any outcome.
- `plans_started` counts plans when they end, so the plan completion rate has no lag between numerator and denominator.

## Columns added in revision 2

| Column | Scope | Unit | Meaning and computation |
|---|---|---|---|
| `<action>_interrupted` | both | commands | Δ `ColonyStats.ActionsInterrupted[action]`, one per action in enum order, after `<action>_failed`. A command that is cancelled by a higher-priority reflex, a plan change or a watchdog ends here. Started = completed + failed + interrupted + still running at the sample |
| `night` | both | 0/1 | `CreatureCore.IsNightPhase(DayPhase, RulesFrame)` at the sample time. A snapshot, not a window share |
| `ledger_residual` | world | energy | Δ of `EnergyLedger.ResidualSum` over the window. Non-zero means energy that the flows do not explain |
| `flow_<name>` | world | energy | Sum over the log window from `EnergyLedger.Window`: every ecology sample (`EcologyTickInterval`) adds its `Last` into `Window`, the log resets it after the world row. One column per `EnergyFlows` field in declaration order (`flow_food_spawn` … `flow_metabolism_credit`) |
| `flow_inflow`, `flow_outflow` | world | energy | `EnergyFlows.Inflow` and `EnergyFlows.Outflow` of `EnergyLedger.Window`, summed over the log window |
| `stock_entities` | world | energy | Snapshot of `EnergyLedger.StockEntities`: body energy of living creatures |
| `stock_nest` | world | energy | Snapshot of `EnergyLedger.StockNest`: nest storage of all colonies |
| `stock_food` | world | energy | Snapshot of `EnergyLedger.StockFood`: energy of food chunks. This is the food supply the creatures can reach |
| `stock_corpses` | world | energy | Snapshot of `EnergyLedger.StockCorpses` |
| `stock_soil` | world | energy | Snapshot of `EnergyLedger.StockSoil` |
| `stock_warmth` | world | energy | Snapshot of `EnergyLedger.StockWarmth` |
| `stock_total` | world | energy | Snapshot of `EnergyLedger.Stock`, the sum of the six stocks above |

Metrics added with these columns:

| Metric | Formula |
|---|---|
| interrupted share of an action | `<action>_interrupted / <action>_started` |
| food per creature in stock | `stock_food / alive` (world row) |
| energy residual share | `ledger_residual / flow_outflow` (world row) |
| unexplained growth share | `(stock_total − stock_total_prev − flow_inflow + flow_outflow) / flow_outflow` (world row) |

## Columns added in revision 3

The four columns are appended at the end of every row, after `stock_total`, so no earlier column changes position. They are written in both scopes: in colony rows they follow the empty world-only columns.

| Column | Scope | Unit | Meaning and computation |
|---|---|---|---|
| `attack_fail_no_target` | both | commands | Δ `ColonyStats.AttackFailNoTarget`: the attack began without a valid hunt target (`AttackCommand.OnBegin`) |
| `attack_fail_target_lost` | both | commands | Δ `ColonyStats.AttackFailTargetLost`: the target became invalid during the command (outside the lurk check below), or was invalid at the strike |
| `attack_fail_low_energy` | both | commands | Δ `ColonyStats.AttackFailLowEnergy`: energy below the chase budget at the start (`AttackCommand.EnergyBudget`), or below `AttackEnergyCost` at the strike |
| `attack_fail_lurk_timeout` | both | commands | Δ `ColonyStats.AttackFailLurkTimeout`: the ambush lurk ended in failure because the target left `DetectionRadius` before the trigger distance was reached. A lurk that times out (`AmbushMaxHold` × patience) with the target still in range completes and is not counted in any failure column |

Every `Failed` status of `AttackCommand` records exactly one of the four reasons, so per window `attack_fail_no_target + attack_fail_target_lost + attack_fail_low_energy + attack_fail_lurk_timeout = attack_failed`, up to commands that straddle a window boundary.

## Columns added in revision 4

| Column | Scope | Unit | Meaning and computation |
|---|---|---|---|
| `<action>_started_brain`, `<action>_started_instinct`, `<action>_started_plan` | both | commands | Δ `ColonyStats.ActionsStartedBySource[action × CommandSource.Count + source]`, counted in `EntityCommandRunner.Start`. Actions, in column order: `flee`, `attack`, `approach_food`, `wander`, `return_nest`, `eat`, `rest`. The block of 21 columns follows `attack_fail_lurk_timeout` |

The three columns of an action sum to `<action>_started` in the same window.

| Metric | Formula |
|---|---|
| attack failure reason share | `attack_fail_<reason> / attack_failed` |

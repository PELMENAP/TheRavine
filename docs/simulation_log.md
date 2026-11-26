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
| `deaths_wind` | both | creatures | Δ `DeathsByCause[Wind]` |
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

`<action>` is every `EntityAction` name in enum order, in snake case: `idle`, `move_to`, `attack`, `eat`, `reproduce`, `signal`, `rest`, `give`, `take`, `put`, `move_nest`. That makes 33 columns for started, completed and failed; the interrupted columns follow in the same order. The header is built by `SimulationLog.BuildHeader`.

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
| `<action>_started_brain`, `<action>_started_instinct`, `<action>_started_plan` | both | commands | Δ `ColonyStats.ActionsStartedBySource[action × CommandSource.Count + source]`, counted in `EntityCommandRunner.Start`. Actions, in column order: `move_to`, `attack`, `eat`, `rest`, `signal`, `take`, `put`. The block of 21 columns follows `attack_fail_lurk_timeout` |

The three columns of an action sum to `<action>_started` in the same window.

| Metric | Formula |
|---|---|
| attack failure reason share | `attack_fail_<reason> / attack_failed` |

## Columns added in revision 5

The seven columns are appended at the end of every row, after the last `plan_<name>_completed` column, so no earlier column changes position. They are written in both scopes.

| Column | Scope | Unit | Meaning and computation |
|---|---|---|---|
| `eat_fail_no_food` | both | commands | Δ `ColonyStats.EatFails[NoFood]`: `EatCommand.OnBegin` found no food within `EatRange` and no nest food to eat |
| `eat_fail_claim` | both | commands | Δ `EatFails[Claim]`: `TryClaim` refused the cached cell (another creature took it first) and the one retry on the next nearest food within `EatRange` also found nothing or was refused. Finishes with the neutral failure reward (0), not `FailureReward` |
| `eat_fail_empty` | both | commands | Δ `EatFails[Empty]`: the portion to eat was zero (`StartEating`) |
| `eat_fail_large_no_partner` | both | commands | Δ `EatFails[LargeNoPartner]`: large food was claimed, no partner arrived within `LargeFoodWindow` |
| `take_fail_no_food` | both | commands | Δ `TakeFails[NoFood]`: `TakeCommand` found no cached food within `EatRange` |
| `take_fail_kind` | both | commands | Δ `TakeFails[Kind]`: the food kind cannot be taken (large food, or the cell has no kind any more), or the targeted portable object was out of reach, removed or no longer eligible |
| `take_fail_claim` | both | commands | Δ `TakeFails[Claim]`: `TryClaim` refused the cell (neutral failure reward), or the claim gave zero energy (`FailureReward`) |

Every `Failed` status of `EatCommand` records exactly one of its four reasons and every `Failed` status of `TakeCommand` exactly one of its three, so per window `Σ eat_fail_<reason> = eat_failed` and `Σ take_fail_<reason> = take_failed`, up to commands that straddle a window boundary.

| Metric | Formula |
|---|---|
| eat failure reason share | `eat_fail_<reason> / eat_failed` |
| take failure reason share | `take_fail_<reason> / take_failed` |

## Columns added in revision 6

The three columns are appended at the end of every row, after `take_fail_claim`, so no earlier column changes position. They are written in both scopes. The ten gradient columns follow them only when `SimulationRules.gradientConflictEnabled` is on at the moment the file is opened.

| Column | Scope | Unit | Meaning and computation |
|---|---|---|---|
| `arm` | colony | text | `ColonyArms.Name(ColonyState.Arm)`: `control`, `speech_muted`, `speech_shuffled` or `null_policy`. Drawn once at colony creation from `armSpeechMutedFraction`, `armSpeechShuffledFraction`, `armNullPolicyFraction` (all 0 by default). The wild colony is `control`. Empty in the world row |
| `speech_mi_bits` | both | bits | Mutual information between the speaker's `SpeechContextBits` context (64 values) and the emitted token, from the window's context×token count table of the colony (one sample per token slot of every non-reflex utterance, counted in `SpeechBus.ApplyArm` before mute or shuffle changes it). Plug-in estimate minus the Miller–Madow term `(Kc−1)(Kt−1)/(2N ln 2)` (Kc, Kt: used contexts and tokens), clamped at 0; 0 for an empty table. World row: the sum of the colony tables |
| `listener_shift` | both | 0…1 | Total variation distance `½ Σ |P(action | fresh token) − P(action | no token)|` over the actions started by agents of the colony in the window. "Fresh token" is `SpeechComponent.HasLoudest` at the moment the command starts (`EntityCommandRunner.Start`, every source); 0 when either side has no sample. World row: the sum of the colony tables. In a `speech_muted` colony the colony's own speech never arrives, so it measures only what other colonies' speech does |
| `grad_cos_<a>_<b>` | world | −1…1 | Only with `gradientConflictEnabled`. Cosine similarity of the summed executor gradients of caste `<a>` and caste `<b>` (`worker`, `soldier`, `scout`, `nurse`; pairs in the order worker–soldier, worker–scout, worker–nurse, soldier–scout, soldier–nurse, scout–nurse), taken over all four executors as one vector. Measured in one training application out of `gradientConflictInterval`; the world value is the mean over colonies measured since the previous row; empty when none was measured or a caste had no contribution |
| `grad_norm_<caste>` | world | gradient units | Only with `gradientConflictEnabled`. L2 norm of the caste's summed executor gradients (already scaled by the clip and the learning-rate multiplier of the ticket) divided by the caste's number of contributing tickets; same sampling and averaging as the cosines |

The split gradients are a diagnostic copy made in the train job; the applied update is the unchanged sum of all contributions. Adult agents are grouped by `Caste`; juveniles before `AssignCaste` and the wild colony are not in any group.

| Metric | Formula |
|---|---|
| speech sensitivity of a colony | `listener_shift` of a `control` colony against the same column of `speech_shuffled` and `null_policy` colonies |
| speech information against the zero model | `speech_mi_bits` of `control` against `speech_shuffled` (should be near 0 for the shuffled channel as listeners see it, but the column counts the speakers' intent, so it stays comparable to `control`) |

## Behavioural descriptor file

With `simulationLogEnabled` and `behaviourDescriptorsEnabled`, every `simulation_log_<stamp>.csv` sample is followed by one row per living agent in `behaviour_descriptors_<stamp>.csv` (same directory, same stamp). Counters are window counters of the agent: they start at spawn and are cleared at each sample. With the flag off nothing is allocated and no row is written.

| Column | Unit | Meaning and computation |
|---|---|---|
| `time_s` | s | `SimulationClock.TimeD` at the sample |
| `colony_id`, `arm` | id, text | `ColonyState.ColonyId`, `ColonyArms.Name(ColonyState.Arm)` |
| `caste` | text | `worker`, `soldier`, `scout`, `nurse`; `juvenile` before the caste is fixed or while `IsJuvenile`; `wild` for the predator colony |
| `age_s` | s | `EntityModel.TimeAlive` |
| `<action>_share` | 0…1 | commands of this action started in the window / all commands started (all sources); 0 when none started. One column per `EntityAction` in enum order |
| `goal_<goal>_share` | 0…1 | share of the started commands by `Brain.Context.CurrentGoal` at the start (`survive`, `hunt`, `forage`, `social`) |
| `distance` | cells | path length: sum of the distances between consecutive cycle positions (`RunCycleTail`) in the window; the first cycle after spawn adds 0 |
| `speech_rate` | 1/s | utterances emitted in the window / `min(window length, age)` |
| `attacks` | strikes | `FitnessEvent.DamageDealt` events in the window |
| `food_eaten` | bites | `FitnessEvent.FoodEaten` events in the window |
| `energy_surplus` | 0…1 | `SharedHierarchicalBrain.EnergySurplus(En / MaxEnergy, CuriosityEnergyFloor)` at the sample |

## Offline analysis

`python3 tools/simlog_analyze.py simulation_log_<stamp>.csv [behaviour_descriptors_<stamp>.csv]` (standard library and numpy). Per arm it prints the population curve (sum of `alive` of the arm's colonies), births, deaths, mean lifespan, entropy of the action mix (bits, from the summed `<action>_started`), `brain_share`, mean `speech_mi_bits` and `listener_shift`, and the mean gradient-conflict columns of the world rows. With the descriptor file it z-scores the behavioural columns (`log1p` of `distance`, `attacks`, `food_eaten`; wild rows dropped unless `--include-wild`), runs k-means (k-means++ start, 5 restarts) for k = 2…8 on each arm with the silhouette score, prints the silhouette difference to the `null_policy` arm at each k, and then clusters all arms together at the k with the best mean silhouette and prints each arm's cluster mix with its total variation distance from the null arm. Silhouette uses at most `--max-rows` rows per arm (random sample, `--seed`).

## Columns added in revision 7 (stage 2 experiments)

Appended after `listener_shift` and before the optional `grad_*` columns, in this order; written in both scopes (colony rows leave the saturation columns empty).

| Column | Scope | Unit | Meaning and computation |
|---|---|---|---|
| `h_saturated_<arm>` | world | 0…1 | One column per arm name in enum order (`control`, `speech_muted`, `speech_shuffled`, `null_policy`, `bounded_ltc`, `progress_novelty`, `orthogonal_reservoir`). Share of hidden states `h` of the coordinator and the four executors with `|h| > 0.95`, over all living agents of colonies of that arm, at the sample (`PerceptronContext.CountSaturated`). Empty when the arm has no agent. In `bounded_ltc` colonies `|h| ≤ 1` always |
| `predisposition_dist` | both | distance | Mean Euclidean distance between the inherited predisposition vectors of colony members: all pairs of at most 64 members taken at an even stride (`EvolutionService.MeanPredispositionDistance`); world row: member-weighted mean over colonies with at least 2 members |

The descriptor file gets `predisposition_0 … predisposition_<PredispositionSize−1>` after `energy_surplus`. `tools/simlog_analyze.py` prints `h_sat` and `pre_dist` per arm, and, with the descriptor file, the correlation ratio η² of each predisposition dimension on the shared cluster label (pooled and per arm) and its strongest Pearson correlation with a behavioural feature.

## Columns added in revision 8 (stage 3: evolutionary dynamics)

The columns are the last ones of every row, after the optional `grad_*` columns, in the order below. World-only columns are empty in colony rows and the other way round.

| Column | Scope | Unit | Meaning and computation |
|---|---|---|---|
| `colony_lr`, `colony_grad_norm`, `colony_entropy_reg` | colony | — | `ColonyState.Genome`: `BaseLearningRate`, `MaxGradientNorm`, `EntropyRegularization` that train the colony's shared brain. Empty in the world row |
| `perturbation_active` | both | entries | `WorldCodexService.PerturbationActive` at the sample: schedule entries whose window contains the time |
| `is_wild` | colony | 0/1 | `ColonyState.IsWild`; empty in the world row |
| `strike_success` | both | 0…1 | (Δ `Attacks[Foreign]` + Δ `Attacks[Player]`) / Δ `attack_started` of the row; empty when no attack started. In the wild colony row it is the predators' success |
| `escape_rate` | both | 0…1 | (Δ `attack_fail_target_lost` + Δ `attack_fail_lurk_timeout`) / Δ `attack_started`: attack commands that ended because the target was lost or left the detection radius; empty when no attack started |
| `gene_speed_mean`, `gene_detect_mean` | colony | multiplier | Mean `MoveSpeedMul` and `DetectionRadiusMul` genes of the colony's living agents at the sample |
| `virus_strains`, `virus_lineages` | world | count | Distinct `StrainId` and `LineageId` of non-endogenous segments over the hosts in `EntityManager.Entities` (`VirologyCensus`) |
| `virus_strains_new`, `virus_strains_lost` | world | count | Strains present now and not at the previous sample, and the reverse |
| `virus_dominant_strain` | world | id | `StrainId` carried by the most hosts (lowest id on a tie); 0 when none |
| `virus_dominant_prevalence` | world | 0…1 | Hosts carrying the dominant strain / hosts |
| `virus_immune_dominant_share` | world | 0…1 | Hosts for which `VirologyComponent.IsImmune(signature of the dominant strain)` / hosts |
| `virus_mean_integrity` | world | 0…1 | Mean `Segment.Integrity` of non-endogenous segments |
| `virus_infections`, `virus_recoveries`, `virus_infections_blocked` | world | count | Window deltas of `VirologyEvents`: viral segments inserted, hosts left without a viral segment by an excision, insertions refused by immunity |

## Life log

With `lifeLogEnabled` the file `life_log_<yyyyMMdd_HHmmss>.csv` has one row per event: `event` (`birth` or `death`), `time_s`, `agent_id` (`EntityModel.EntityId`), `parent_id` and `mate_id` (births from reproduction; empty for founders, reseeds and predator spawns), `colony_id`, `cause` (`DeathCause` on death), `age_s` (death), then one column per gene of `GeneticParameters` except the three colony learning genes, including `Predisposition0…2`; gene columns are filled in birth rows only. The descriptor cluster of an agent is not in the file: clusters are computed offline from `behaviour_descriptors_<stamp>.csv`.

## Offline analysis of stage 3

`python3 tools/simlog_analyze.py simulation_log.csv [descriptors.csv] [--life life_log.csv]` prints, in addition to the earlier sections:

- predator–prey: lagged cross-correlation `corr(a[t], b[t+lag])`, lags ±`--max-lag` samples, of the predators' `strike_success` with `escape_rate` and with the civil colonies' `deaths_killed` per member, and the slope per 1000 s of the predator and prey trait means (prey is the member-weighted mean of civil colonies); both slopes positive marks an arms race;
- virus–immune: the lagged correlation of `virus_immune_dominant_share` with `virus_dominant_prevalence`, strain turnover (share of consecutive samples in which the dominant strain changed, mean dominant tenure, mean `(new + lost) / alive`);
- perturbation: for each window with `perturbation_active > 0`, the recovery time after the window end of each colony's population and of the world `avg_fitness` to stay within `--band` of the mean of the `--pre-window` samples before the shock for `--hold` samples, medians per arm; "not recovered" when it never does;
- Bedau–Packard (with `--life`): gene values are cut into `--bins` equal bins between their observed minimum and maximum, a component is a (gene, bin) pair; on a grid of `--life-steps` times the activity of a present component grows by one per step, `new activity` counts components whose activity first exceeds `--activity-threshold`, diversity is the number of present components, and the abundance-weighted activity adds the share of living agents carrying the component. The neutral shadow replays the same births and deaths of the same agent slots by colony, but every newborn with a known parent copies a random living agent of its colony and re-draws each gene bin with the per-gene rate at which real children differ from their parents. Real and shadow are printed side by side.

## Training, reproduction, codex and interactions

- `civil_alive` and `civil_limit` give the world population and cap. `brain_origin` and `wide_social` describe the current brain of each colony.
- `train_coord` and `train_<goal>` count actual training steps in the window. The colony total continues across brain replacements.
- `birth_fail_*` counts rejected spawn requests. `flow_birth_transfer` is the internal transfer, excluded from inflow and outflow. `flow_reproduce` is the energy cost that remains after the transfer. In legacy mode the full debit and initial child inflow are reported separately.
- `codex_nest_stock` and `codex_nest_threshold` are civil colony values. World columns `codex_law_count`, `codex_price_windows` and `codex_multiplier_*` are snapshots. `codex_proposals`, `codex_accepted`, `codex_refused_*` and `flow_codex` are window totals. Proposal counters include terminal and colony calls. Effective multipliers include active scheduled perturbations.
- `interaction_*` counts positive gifts, groomings, utterances, new marks, threats and cargo deliveries, with separate feed and groom refusal reasons. Gift and delivery energy are totals. Divide `interaction_delivery_seconds` by `interaction_delivered` for mean food carry duration.
- `interaction_rare_action_*` reports direct brain commands selected by the rare branch and their command rewards. Started and completed describe commands, not the full plan.
- `interaction_objects_taken` and `interaction_objects_placed` count portable fuel stacks picked up and deposited. `interaction_object_delivery_energy` measures heat released in the nest. `flow_fuel_heat` includes any fuel released at death when a stack cannot be placed on the map.

For a birth comparison, use matching worlds with `reproductionTransferEnabled` on and off, and enable `lifeLogEnabled`. Keep the same population cap. For exploration, set `rareActionChance` in the rules asset. A value of 0.05 gives up to 5% additional selection probability at full energy; the chance falls to zero at `curiosityEnergyFloor`. Selection frequencies are tracked per plan and only use opportunities where a verb is in the current mask.

For an architecture comparison, set `wideSocialExecutor` in the rules asset. Its Social widths are 96, 48 and 48. Set `useSavedBrain` off in EntityManager for a fresh start. Compare the two profiles at equal `train_social` totals, including task metrics, survival, and agent tick time. Wide model filenames include `_wide_social`; they cannot be loaded by the standard profile.

## Columns added in revision 9 (stage 0: baseline telemetry)

The thirteen columns are the last ones of every row, after the `interaction_*` columns, in the order below. "Window" columns are sums or ratios over the interval since the previous sample of the same scope; "snapshot" columns are read at the sample. World-only columns are empty in colony rows.

| Column | Scope | Unit | Kind | Meaning and computation |
|---|---|---|---|---|
| `tick_ms_mean` | world | ms | window | Mean wall time of one `EntityManager.TickAgents` call, one sample per frame after the tick loop has started (`SimulationLog.NoteTick`, timed in `EntityManager.Update` around the call, only while the log exists). Frames in which the window budget lets no agent tick are included, so the value is per frame, not per agent batch. Empty when no frame was timed |
| `tick_ms_p95` | world | ms | window | 95th percentile of the same samples from a 0.1 ms histogram (0…100 ms, the last bin collects the rest); the value is the upper edge of the bin, so the resolution is 0.1 ms |
| `gc_alloc_bytes` | world | bytes | window | `GC.GetAllocatedBytesForCurrentThread` difference over the window: main-thread allocation (not net heap growth, not worker threads). The allocation of the log's own sample is excluded: the counter is re-read after the rows are written |
| `stress_mean` | both | 0…1, dimensionless | snapshot | Mean `EntityModel.Stress` of the living agents of the colony; world: civil colonies only (the wild colony is excluded). Empty with no agents |
| `stress_p90` | both | 0…1, dimensionless | snapshot | 90th percentile of `Stress` from a 100-bin histogram, upper edge of the bin (resolution 0.01) |
| `stress_high_share` | both | 0…1 | snapshot | Share of agents with `Stress >= SimulationRules.stressHighThreshold` (default 0.6) |
| `social_action_share` | both | 0…1 | window | Δ `give_started` / Σ Δ `<action>_started` of the row; empty when no command started |
| `delivery_energy_per_agent_min` | both | energy per agent per minute | window | Δ `interaction_delivered_energy` / agents / window length in minutes. Agents: colony row `MemberCount`, world row the civil population at the sample. The window length is the difference of sample times (the first window starts at 0). Empty when there are no agents |
| `starvation_death_share` | both | 0…1 | window | Δ `deaths_starved` / Δ `died`; empty when nobody died |
| `population_capped` | both | 0/1 | window | 1 when Δ `birth_fail_capacity` is above 0: at least one spawn request was refused in the window because the population cap (`maxPopulation`, or the slot capacity, or the predator count for the wild colony) left no free slot |
| `anomaly_transfer_loops` | world | loops | window | A transfer A→B followed by transfers B→A within `anomalyLoopWindow` seconds whose amounts add up to at least the first one (net transfer A→B not positive). Fed by `GiveCommand` (energy actually ingested by the receiver) through `AnomalyDetector.NoteTransfer`; each return amount cancels the open transfers of the opposite direction, newest first, and one cancelled transfer counts once |
| `anomaly_relief_farm` | world | pairs | window | Ordered pairs whose accumulated relief (`AnomalyDetector.NoteRelief`) in the window exceeded `anomalyReliefPairThreshold`; a pair counts once per window. |
| `anomaly_vote_concentration` | world | 1/n…1, dimensionless | snapshot | Mean over living laws of the Herfindahl index `Σ s²` of the authors' shares `s` of the energy invested in the law (proposal cost plus `TryInvest`, recorded by `WorldCodexService.Anomalies`). Laws with less than `anomalyMinInvestment` in total are skipped; empty without laws. Only investments recorded since startup are counted |

The anomaly counters only count: they never block a transfer, relief or vote. Their tables are preallocated with `anomalyPairCapacity` entries; entries beyond it are dropped (`AnomalyDetector.DroppedPairs`, `DroppedVotes`).

Reading a stall: `population_capped = 1` with a low `starvation_death_share`, low `hungry_share` and non-zero `birth_fail_capacity` is a cap stall; `population_capped = 0` with a high `starvation_death_share`, high `hungry_share` and low `stock_food` per creature is a food shortage.

## Run file

When the log is opened (first sample), `run_<yyyyMMdd_HHmmss>.json` is written next to `simulation_log_<same stamp>.csv`. Fields: `stamp`, `scenario` (`ExperimentScenario` asset name, `none` without one), `flags` (the ten system flags and signal masks of `SimulationRules` as `name=0/1`), `time_s`, `season_value` (−1…1), `season_phase` (0…1 of `seasonPeriod`), `winter`, `brain_origin` (`fresh`, `loaded`, `mixed` or `none` over the civil colonies; per colony the raw `ColonyState.BrainOrigin`), and `colonies`: `colony_id`, `wild`, `nest_x`, `nest_z`, `arm`, `brain_origin` and, for civil colonies of a scenario, the food modifier of that colony (`food_multiplier`, `food_radius`, `food_start_s`, `food_active_s`, `food_inactive_s`).

## Columns added in revision 10 (stage 1a: stress)

The six columns follow `anomaly_vote_concentration`. Window columns are deltas of `ColonyStats` since the previous sample; the world row sums all colonies, the wild colony included.

| Column | Scope | Unit | Kind | Meaning and computation |
|---|---|---|---|---|
| `stress_src_hunger`, `stress_src_cold`, `stress_src_lonely`, `stress_src_task`, `stress_src_threat` | both | stress units (dimensionless, sum of 0..1 increments) | window | Stress actually added to creatures by the source, summed over creatures: after saturation at 1, so a creature at 1 adds 0. `threat` is every older source (startle, fire, outnumbered, foreign harm, kin death, leader succession). The five columns add up to the total stress added in the window. `hunger`, `cold`, `lonely` and `task` are 0 while `stressSourcesEnabled` is off |
| `stress_high_time_share` | both | 0…1 | window | Creature-seconds at `StressLevel` 2 / creature-seconds measured. Measured once per cycle while `stressSourcesEnabled` is on; empty otherwise |


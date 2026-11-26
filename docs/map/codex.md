# Codex: laws over rule axes
Root: `Assets/Scripts/EntitySystem/AI/Codex/`. Assembly: `EntitySystem`. Rules: `Assets/Scripts/EntitySystem/AI/Rules/SimulationRules.Codex.cs`.
Tests: `Assets/Tests/EditMode/CodexMathTests.cs`, `Assets/Tests/EditMode/WorldCodexServiceTests.cs`.

## Entry points
| Type | File | Role |
|---|---|---|
| `WorldCodexService` | `Assets/Scripts/EntitySystem/AI/Codex/WorldCodexService.cs` | Laws, opposition, θ; `TryPropose`, `TryInvest`, `TryOppose`, `Step`, `Capture`, `Restore`; registered in `ServiceLocator`; `Anomalies` is an optional read-only sink of successful investments (`AnomalyDetector`) |
| `CodexMath` | `Assets/Scripts/EntitySystem/AI/Codex/CodexMath.cs` | Pure formulas: `Aggregate`, `Cost`, `VoteGain`, `Decay`, `Multipliers` |
| `LawRecord` | `Assets/Scripts/EntitySystem/AI/Codex/LawRecord.cs` | Blittable law: delta per `VarianceAxis`, author, support, creation time |
| `CodexPriceEstimator` | `Assets/Scripts/EntitySystem/AI/Codex/CodexPriceEstimator.cs` | Recursive least squares of net energy balance on θ |
| `ColonyLegislator` | `Assets/Scripts/EntitySystem/AI/Codex/ColonyLegislator.cs` | SPSA per civil colony over `WorldCodexService` public API |
| `EntityCodexPurse`, `ICodexPurse` | `Assets/Scripts/EntitySystem/AI/Codex/EntityCodexPurse.cs` | Whose energy pays: colony nest storage, creature, player |
| `CodexAuthor` | `Assets/Scripts/EntitySystem/AI/Codex/CodexAuthor.cs` | Author id ranges: player < 0, creature > 0, colony ≥ `ColonyBase` |
| `CodexError`, `CodexErrorText` | `Assets/Scripts/EntitySystem/AI/Codex/CodexError.cs` | Refusal codes and their messages |
| `CodexSave` | `Assets/Scripts/Core/Settings/WorldData/CodexSave.cs` | Persisted codex inside `WorldState.codex` |
| `CodexCommand` | `Assets/Scripts/Terminal/CodexCommand.cs` | `~codex list / explain / propose / vote` |

## Data flow
1. A law is a log-multiplier vector over `VarianceAxis`; `θ_k = clamp(Σ S_j δ_jk / (S0_k + inertia + Σ S_j), L_k, U_k)`; the multiplier is `exp(θ)`.
2. `TryPropose` and `TryInvest` debit the author through `ICodexPurse` once and add the same amount to `EnergyFlows.Codex`; support grows by `codexVoteScale · sqrt(energy)`.
3. `EntityManager.TickAgents` calls `Step` only when a tick window opens, before `CaptureFrame`: decay, prune, aggregate, `SimulationRules.ApplyLaws` on the law-applied instance.
4. `SimulationRules.CreateLawApplied` makes one instance per world; `ApplyLaws` copies the axis fields from its base (the world variant) and runs `ApplyVariance`; all-ones θ is a plain copy.
5. Price per axis: `EntityManager.SampleEnergy` feeds `NoteEnergy` (net balance over turnover, own spending excluded); at each `codexWindow` the pair (mean θ, balance) goes to the estimator; until `codexPriceMinSamples` the prior is used.
6. A law is admissible if every delta is inside `[L, U]` and `Cost = p·δ ≤ codexFreeBudget`; the same check runs again on `TryInvest`.
7. `ColonyLegislator.Tick` runs at window close: per window it compares `ColonyState.Score`, proposes a Rademacher perturbation, invests while the trend is positive, otherwise tries the mirrored perturbation once.
8. Save: world checkpoint hook writes `WorldState.codex`; on load `Restore` validates and θ is recomputed by the first `Step`. A missing or invalid block loads an empty codex.

## Perturbation schedule
`SimulationRules.PerturbationSchedule` (`PerturbationEntry`: `startTime`, `duration`, `axis`, `multiplier`; empty by default) is applied inside `WorldCodexService.Step`, the same window-open path as the laws: `UpdatePerturbation` sums `log(multiplier)` of the entries active at `SimulationClock.Time` per axis, `Reaggregate` applies `exp(θ + perturbation)` through `SimulationRules.ApplyLaws`. Entries with a non-positive or non-finite multiplier or an unknown axis are ignored. The perturbation is not part of `Theta`, of the laws or of the price estimate. `WorldCodexService.PerturbationActive` is the number of active entries (log column `perturbation_active`).

## Invariants
- The frame never changes inside a tick window: `Step` is the only writer of the law-applied rules and runs at window open.
- Spent energy leaves the investor and appears once in `EnergyFlows.Codex`; a refused call changes neither energy nor laws.
- Bounds, `codexMaxDeltaPerAxis` and `codexFreeBudget` are read from rules and no law can change them.
- Players and creatures use the same methods; only `CodexAuthor` differs.
- `EnergySamplingRequired` is true while any law exists, so prices cost nothing in a world without laws.

## Hot paths
None. `Step` allocates nothing after warm-up; `Capture` and `Restore` allocate and run on save and load.

## Open issues
None recorded here.

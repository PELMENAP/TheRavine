# Biosphere: chemistry and cellular organisms
Root: `Assets/Scripts/EntitySystem/Biosphere/`. Assembly: `EntitySystem`. Namespace: `TheRavine.EntityControl.Biosphere`.
Tests: `Assets/Tests/EditMode/BiosphereTests.cs`.

## Entry points
| Type | File | Role |
|---|---|---|
| `BiosphereRules` | `Assets/Scripts/EntitySystem/Biosphere/BiosphereRules.cs` | ScriptableObject: flag, budgets, substances, reactions, light, uptake, genome mutation |
| `CellBiosphere` | `Assets/Scripts/EntitySystem/Biosphere/CellBiosphere.cs` | Cells and organisms in SoA; `Step`, `SeedAt`, `TotalEnergy`, `TotalElements`; registered in `ServiceLocator` |
| `ChemistryTable` | `Assets/Scripts/EntitySystem/Biosphere/ChemistryTable.cs` | Substances over 4 elements, balanced reactions only, energy delta per reaction |
| `ReactionSolver` | `Assets/Scripts/EntitySystem/Biosphere/ReactionSolver.cs` | Bounded sequential reactions of one pool; conserves elements and energy |
| `ChemicalField` | `Assets/Scripts/EntitySystem/Biosphere/ChemicalField.cs` | Elements per map cell; ambient profile from noise; buffered diffusion, weathering |
| `BiosphereView` | `Assets/Scripts/EntitySystem/Biosphere/BiosphereView.cs` | Optional MonoBehaviour: instanced cubes per `CellType`; appearance only |
| `EntityManager.Biosphere` | `Assets/Scripts/EntitySystem/AI/EntityManager.Biosphere.cs` | `biosphereRules` field; create, step after `ColonyFood.Tick`, release |

## Data flow
1. `EntityManager.Awake` creates `CellBiosphere` when `biosphereRules.simulationEnabled`; `Start` binds the `ChunkFoodIndex`.
2. `StepBiosphere` runs on the authority each agent tick; `Step` works every `stepInterval` of `SimulationClock`.
3. Order inside `Step`: field diffusion and weathering, up to `cellsPerStep` cells round-robin, metabolism of every organism, seeding.
4. A cell reads its morphogens, neighbour count, age, organism energy, local elements and a bias; 10 genes are linear threshold units.
5. A gene above zero produces a morphogen or fires an action: divide, die, become leaf, root, fruit or digest, release a spore.
6. Leaf gains light (`EnergyFlows.BiosphereLight`); root takes elements from the field and soil energy (`ChunkFoodIndex.TakeSoil`).
7. Digest claims world food in reach (`TryClaim`, not `Large`); fruit places plant food (`ChunkFoodIndex.TryAddFood`).
8. Division costs structure substance and energy; the child cell holds that structure and returns it to the pool on death.
9. Metabolism runs `ReactionSolver` with the organism's inherited catalyst activities; excess elements are excreted to the field.
10. A spore makes a new organism with a mutated copy of the genome and a quarter of the pool.
11. Death decomposes pool and structure into elements for the field; energy goes to soil (`ReturnToSoil`), the rest is a loss.
12. `EntityManager.SampleEnergy` adds `TotalEnergy` (free energy, bond energy, structure) to the ledger stock.

## Invariants
- Every reaction is balanced in elements; unbalanced rows are dropped at `ChemistryTable` construction.
- No negative amounts; upkeep never takes more energy than the organism has, a deficit removes a cell.
- Ledger: light and seeding are inflows, upkeep and catabolic loss are outflows, soil uptake is booked in `BiosphereSoil`.
- Creatures and the player meet the biosphere only through world food and soil, by the same rules as any eater.

## Hot paths
None of the agent tick. `Step` runs on the main thread with preallocated arrays.

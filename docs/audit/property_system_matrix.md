# Property-to-system connectivity matrix

Snapshot: commit `98e99d5` (branch `claude/practical-mendel-c5lbxd`, rebased onto `4babbc0`). Offline audit, no runtime code.

## Method

1. For every row a regex of the property name and its direct aliases was run over every file of every column (patterns below).
2. Every hit was read. A hit counts only if the code reads or writes the property itself or a value that is a direct copy or normalisation of it (for example `VectorizerFrame.Energy`, which is `Stats.En`, see `AI/Entity/EntityModel.Cycle.cs:165`).
3. `R`: the system reads the property. `W`: the system writes it (assignment, increment, a mutating call such as `Ingest`, `TakeCarried`, `TakeDamage`). `RW`: both. `none found`: no hit, or only the false positives listed at the end.
4. Line numbers are 1-based at the snapshot commit. Paths are relative to `Assets/Scripts/EntitySystem/`.

| Row | Regex used |
|---|---|
| `En` | `\.En\b`, `\bEn\b`, `EnergyFraction`, `\bEnergy\b`, `BodyEnergy`, `EffectiveEnergyNorm` |
| `Hp` | `\.Hp\b`, `HpFraction`, `\bHealth\b`, `TakeDamage`, `MaxHealth` |
| `Stomach` | `Stomach`, `\.Fill\b`, `Ingest(`, `Withdraw(`, `TakeAll(`, `IsSated` |
| `WellFed` | `WellFed` |
| `Carrying` | `Carrying`, `Carry(`, `TakeCarried`, `CarryCapacity` |
| `NestStorage` | `\.Storage\b`, `NestStorage`, `CanEatFromNest`, `NestFood\b` |
| `ViralLoad` | `ViralLoad`, `TryGetViralInputs`, `\bLoad\b`, `IsSick`, `_sick`, `\bSick\b` (plus manual follow of `FindSickestKin`, `Groom`) |
| `Stress` | `Stress` |
| `ColonyHunger` | `RelieveHunger`, `\.Hunger\b`, `ColonyHunger`, `RaiseHunger`, `HungerEma` |
| Terrain height, slope | `TerrainSample`, `LastTerrain`, `HeightNorm`, `\bSlope\b`, `RelativeHeight`, `HeightRaw`, `TrySample` |
| `Biome` | `Biome` |
| `FoodKind` | `FoodKind`, `FoodToxic`, `FoodLarge`, `FoodMeat` |
| `Colony` | `\.Colony\b`, `ColonyId`, `ColonyIndex`, `\bColony\b` (plus manual `excludeColony`, `tabooColony`) |
| `Caste` | `\bCaste\b`, `CasteMods`, `CasteFixed`, `AssignCaste` |
| `Season` | `Season`, `Winter` (plus manual lowercase `season` parameter) |
| PoI `EnergyEma` | `EnergyEma`, `CreditEnergy` |

Column files:

| Column | File |
|---|---|
| InputVectorizer | `AI/InputVectorizer.cs` |
| InstinctSystem | `AI/Instinct/InstinctSystem.cs` |
| DigestionComponent | `AI/Entity/Components/DigestionComponent.cs` |
| StatsComponent | `AI/Entity/Components/StatsComponent.cs` |
| VirologyComponent | `Virology/VirologyComponent.cs` |
| ChunkFoodIndex | `AI/Perception/ChunkFoodIndex.cs` |
| ColonyStats | `AI/Colony/ColonyStats.cs` |
| EvolutionService | `AI/Population/EvolutionService.cs` |
| SimulationRules | `AI/Rules/SimulationRules*.cs` (all partial files) |
| `<Name>` command | `AI/Entity/Commands/<Name>Command.cs` |

## Matrix A: core systems

Cell format: `code file-line(s)` in the column file.

| Property | InputVectorizer | InstinctSystem | DigestionComponent | StatsComponent | VirologyComponent | ChunkFoodIndex | ColonyStats | EvolutionService | SimulationRules |
|---|---|---|---|---|---|---|---|---|---|
| `En` | R 87, 91, 94 | R 27, 144 | RW 60 (R), 64 (W) | RW 25 (owner), 52, 56, 96, 119 | RW 231 (R), 251 (W), 264 | none found | none found | none found | none found |
| `Hp` | R 87, 89, 93 | R 26, 149, 150 | none found | RW 19 (owner), 51, 95, 120 | RW 210, 230 (R), 250 (W), 263 | none found | none found | none found | none found |
| `Stomach` | R 122 (`Fill`) | R 28, 154, 186 | RW 5 (owner), 27, 29, 35, 36, 43, 57, 58, 63 | none found | none found | none found | R 25, 76-81 (`MeanFill`, mean `Fill` passed in by `EntityManager.TickNests`) | none found | none found |
| `WellFed` | R 123 | none found | RW 7 (owner), 15, 68, 69 | none found | none found | none found | none found | none found | none found |
| `Carrying` | R 128 | R 29, 172, 174 | none found | none found | none found | none found | none found | none found | none found |
| `NestStorage` | R 129 | R 46, 159 (`NestFood`, derived from `Storage` in `AI/Entity/EntityModel.Social.cs:73`) | none found | none found | none found | none found | R 23-24, 27, 73-74, 76-81 (flows and share passed in) | none found | none found |
| `ViralLoad` | R 90, 133 | R 17, 41, 164, 177 (`Sick`, derived at `AI/Entity/EntityModel.Cycle.cs:204`) | none found | none found | W 739-762 (computes `load`, 759), 315-330 (`Groom` lowers integrity) | none found | none found | none found | none found |
| `Stress` | R 134 | R 31, 147 | none found | none found | none found | none found | none found | none found | none found |
| `ColonyHunger` | R 159 | none found | none found | none found | none found | none found | none found | none found | none found |
| Terrain height, slope | R 148-150, 182-186, 202 | none found | none found | none found | none found | R 534 (`HeightRaw`, water check) | none found | none found | none found |
| `Biome` | R 195-198 | none found | none found | none found | none found | R 685, 699, 709 | none found | none found | R `SimulationRules.cs:535-536`, `1156-1159` (`BiomeFertility`, `BiomeToxicity` lookups) |
| `FoodKind` | R 124-126 | R 47, 155 | none found | none found | none found | RW W 526-553, 613, 685, 696-702; R 411, 426, 430, 440, 442, 732, 733, 761, 762, 799 | none found | none found | R `SimulationRules.cs:537` (`FoodFlammability` lookup) |
| `Colony` | none found | none found | none found | none found | none found | R 320, 323, 381 (`excludeColony`), 593, 617 (`tabooColony`) | none found | R 68, 70, 127 | none found |
| `Caste` | R 169 | R 44, 182 | none found | none found | none found | none found | R 19, 60-71 (`RecordCastePlan`, `TopPlan`) | none found | none found |
| `Season` | R 177, 178 | none found | none found | none found | none found | R 633, 670 (`season` factor of capacity) | none found | none found | none found |
| PoI `EnergyEma` | none found | none found | none found | none found | none found | none found | none found | none found | none found |

SimulationRules holds only parameters of these properties, never their per-agent value. Parameter evidence, for reference: `En` `SimulationRules.Ecology.cs:10,12`; `Hp` `SimulationRules.Ecology.cs:9`; `Stomach` `SimulationRules.Ecology.cs:51`; `WellFed` `SimulationRules.Ecology.cs:52`; `Carrying` `SimulationRules.Ecology.cs:20`; `NestStorage` `SimulationRules.Colony.cs:153`; `ViralLoad` `SimulationRules.Virology.cs:63`; `Stress` `SimulationRules.Agent.cs:24`; `ColonyHunger` `SimulationRules.Ecology.cs:18`; terrain `SimulationRules.Movement.cs:14-16`; `Biome` `SimulationRules.Ecology.cs:84-85`; `FoodKind` `SimulationRules.Ecology.cs:71,74`; `Colony` `SimulationRules.Colony.cs:82`; `Caste` `SimulationRules.Colony.cs:101`, `SimulationRules.cs:1087`; `Season` `SimulationRules.Ecology.cs:66`, `SimulationRules.Variance.cs:99-100`; `EnergyEma` `SimulationRules.Colony.cs:9`.

## Matrix B: commands, part 1

| Property | ApproachFood | Attack | Eat | Flee | Follow | GoToPoint | Groom | Idle | Mimic | MoveNest | PickUp |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `En` | none found | RW 16, 91, 93 (R), 94 (W), 111, 114, 115 (W) | none found | none found | none found | none found | none found | none found | none found | none found | none found |
| `Hp` | none found | W 125 (victim `TakeDamage`) | W 100 (toxic `TakeDamage`) | none found | none found | none found | none found | none found | none found | none found | none found |
| `Stomach` | none found | none found | RW 18 (R, `IsSated`), 110 (W, `Ingest`) | none found | none found | none found | none found | none found | none found | none found | none found |
| `WellFed` | none found | none found | none found | none found | none found | none found | none found | none found | none found | none found | none found |
| `Carrying` | none found | none found | RW 25 (R), 26 (W, `TakeCarried`) | none found | none found | none found | none found | none found | none found | none found | RW 10 (R), 26 (W, `Carry`) |
| `NestStorage` | none found | none found | RW 32, 35 (R), 36 (W) | none found | none found | none found | none found | none found | none found | none found | none found |
| `ViralLoad` | none found | none found | none found | none found | none found | none found | RW 10 (R, `FindSickestKin` reads load, `AI/Entity/EntityModel.Social.cs:206`), 14 (W, `Virology.Groom`) | none found | none found | none found | none found |
| `Stress` | none found | none found | none found | none found | none found | none found | none found | none found | none found | none found | none found |
| `ColonyHunger` | none found | none found | none found | none found | none found | none found | none found | none found | none found | none found | none found |
| Terrain height, slope | none found | none found | none found | none found | none found | none found | none found | none found | none found | none found | none found |
| `Biome` | none found | none found | none found | none found | none found | none found | none found | none found | none found | none found | none found |
| `FoodKind` | none found | none found | R 62 | none found | none found | none found | none found | none found | none found | none found | R 19 |
| `Colony` | none found | R 130, 131 | R 37 | none found | none found | R 8, 23, 24, 28 | none found | none found | none found | R 8, 9, 13 | none found |
| `Caste` | none found | R 120 (`CasteMods`) | R 34, 75 (`CasteMods`) | none found | none found | none found | none found | none found | none found | none found | none found |
| `Season` | none found | none found | none found | none found | none found | none found | none found | none found | none found | none found | none found |
| PoI `EnergyEma` | none found | none found | W 80 (`Points.CreditEnergy`, lerp at `AI/Entity/Components/PointsOfInterestComponent.cs:138`) | none found | none found | none found | none found | none found | none found | none found | none found |

## Matrix B: commands, part 2

| Property | PlannedMove | RememberPoint | Reproduce | Rest | ReturnNest | ShareFood | Speech | StoreFood | Threaten | Wander |
|---|---|---|---|---|---|---|---|---|---|---|
| `En` | none found | none found | RW 8 (R), 13 (W) | RW 34 (R), 47 (W) | none found | R 18, 19 | RW 19 (R), 20 (W), 74 (R) | none found | RW 22 (R), 23 (W) | none found |
| `Hp` | none found | none found | RW 9 (R), 15 (W) | RW 33, 43 (R), 46 (W) | none found | none found | R 74 | none found | none found | none found |
| `Stomach` | none found | none found | none found | none found | none found | RW 24 (W, `Withdraw`), 27, 29 (W, `Ingest`) | none found | none found | none found | none found |
| `WellFed` | none found | none found | none found | none found | none found | none found | none found | none found | none found | none found |
| `Carrying` | none found | none found | none found | none found | none found | RW 23 (R, W `TakeCarried`) | none found | RW 7, 12 (R), 14 (W, `TakeCarried`) | none found | none found |
| `NestStorage` | none found | none found | none found | none found | none found | none found | none found | W 15 | none found | none found |
| `ViralLoad` | none found | none found | none found | none found | none found | none found | none found | none found | none found | none found |
| `Stress` | none found | none found | none found | none found | none found | none found | none found | none found | none found | none found |
| `ColonyHunger` | none found | none found | none found | none found | none found | W 31 (`RelieveHunger`) | none found | none found | none found | none found |
| Terrain height, slope | none found | none found | none found | none found | none found | none found | none found | none found | none found | R 47 (`LastTerrain`) |
| `Biome` | none found | none found | none found | none found | none found | none found | none found | none found | none found | none found |
| `FoodKind` | none found | none found | none found | none found | none found | none found | none found | none found | none found | none found |
| `Colony` | none found | R 12 | none found | R 67 | none found | none found | none found | R 17 | none found | R 72 |
| `Caste` | none found | R 11, 12 | none found | none found | none found | R 15, 24 | none found | R 14 (`StoreEfficiency`) | none found | R 71 |
| `Season` | none found | none found | none found | none found | none found | none found | none found | none found | none found | none found |
| PoI `EnergyEma` | none found | none found | none found | none found | none found | none found | none found | none found | none found | none found |

## Rows without a reader (sources without a sink)

Within the listed systems:

- PoI `EnergyEma`: written only by `Eat` (`EatCommand.cs:80`). No listed system reads it. Readers exist outside the listed columns: `AI/Entity/Components/PointsOfInterestComponent.cs:94,115` (eviction order and PoI choice) and `AI/Entity/EntityModel.Social.cs:31` (offer to the colony PoI board). It never reaches InputVectorizer or InstinctSystem directly; only the PoI direction does.

No other row is without a reader.

## Rows without a writer (sinks without a source)

Within the listed systems:

- `Stress`: read by InputVectorizer and InstinctSystem; written only outside the listed columns: decay `AI/Entity/EntityModel.Cycle.cs:82`, `EntityModel.AddStress` (`AI/Entity/EntityModel.cs:65`) called from `AI/Population/ColonyPopulation.cs` (`BroadcastDeath`, `HandleLeaderDeath`).
- `Colony`: membership is set outside the listed columns (`EntityModel.Configure`, `ColonyState.AddMember`, `ColonyPopulation.SpawnEntity`).
- `Caste`: written outside the listed columns (`EntityModel.AssignCaste`, called from `EntityManager.TickColonies`). VirologyComponent only produces the `CasteShift` modifier; the switch itself is in `EntityManager`.
- `Season`: source is `SeasonClock` (outside the listed columns).
- Terrain height, slope and `Biome`: world data produced by the map generator; no simulation system writes them. This is expected for world inputs.

`ColonyHunger` has one listed writer (`ShareFood`, decrease only). Its increase (`NestState.RaiseHunger` from `ColonyPopulation.BroadcastDeath`, and `NestState.Tick`) is outside the listed columns.

## False positives excluded

- `ChunkFoodIndex.cs:24,32,39,460,464,481,491`: `Energy` is the energy of a food claim or corpse bite, not `Stats.En`.
- `EatCommand.cs:62`, `PickUpCommand.cs:22,26`: `claim.Energy` is food energy, not `Stats.En` (it reaches `Stomach` or `Carrying`, which are marked).
- `VirologyComponent.cs:622`: `Array.Fill` matched the `Fill` pattern.
- `ShareFoodCommand.cs:31`, `StoreFoodCommand.cs:21`: `NestStorageNorm` is a parameter, not nest storage.
- `InputVectorizer.cs:61,62`: `SeasonOffset` constants, not a read of season.
- `SimulationRules.Variance.cs:99,100`: scaling of the season parameters, not season state.

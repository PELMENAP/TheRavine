# Entities: base classes, components, mobs, small modules

Root: `Assets/Scripts/Model/`, `Assets/Scripts/EntitySystem/` without its AI, Virology and Job folders.
Assemblies: `BehavioursMap`, `EventSystem`, `SkillSystem`, `EntitySystem`.
Tests: `Assets/Tests/EditMode/MimicLegJobTests.cs`.

AI agents are a separate family (`EntityModel`): `docs/map/ai.md`. The player: `docs/map/player-net.md`.

## Entry points

| Type | File | Role |
|---|---|---|
| `AEntity` | `Assets/Scripts/Model/BehavioursMap/AEntity.cs` | Base entity: component dictionary by type, `IsActive`, `OnUpdate` |
| `IComponent` | `Assets/Scripts/Model/BehavioursMap/IComponent.cs` | Component contract |
| `EntitySystem` | `Assets/Scripts/EntitySystem/EntitySystem.cs` | Scene service: `CreateMob`, list `global` of ticked entities, boids set-up |
| `MobGenerator` | `Assets/Scripts/EntitySystem/MobGenerator.cs` | Chunk-based spawn and despawn of mobs by player position |
| `AEntityViewModel` | `Assets/Scripts/EntitySystem/AEntityViewModel.cs` | `NetworkBehaviour`; subscribes to `Entity.IsActive` and `Entity.OnUpdate` |
| `AEntityView` | `Assets/Scripts/EntitySystem/AEntityView.cs` | UI bindings to the view model |
| `StatePatternComponent` | `Assets/Scripts/EntitySystem/StatePatternComponent.cs` | Entity FSM: current `AState` plus `CommandScheduler` |
| `CommandScheduler` | `Assets/Scripts/EntitySystem/Command/CommandScheduler.cs` | Async command queue of a state |

## Components

| Component | File | Holds |
|---|---|---|
| `MainComponent` | `Assets/Scripts/Model/BehavioursMap/EntityBaseComponents/MainComponent.cs` | name, prefab id, client id |
| `TransformComponent` | `Assets/Scripts/Model/BehavioursMap/EntityBaseComponents/TransformComponent.cs` | entity and model transforms |
| `MovementComponent` | `Assets/Scripts/Model/BehavioursMap/Movement/MovementComponent.cs` | base speed, acceleration, deceleration, velocity |
| `EnergyComponent` | `Assets/Scripts/Model/BehavioursMap/Energy/EnergyComponent.cs` | energy, max energy (reactive) |
| `CurrencyComponent` | `Assets/Scripts/Model/BehavioursMap/Currency/CurrencyComponent.cs` | currency (`SafeInt`, reactive) |
| `AimComponent` | `Assets/Scripts/Model/BehavioursMap/Aim/AimComponent.cs` | crosshair distance, pick distance |
| `EventBusComponent` | `Assets/Scripts/Model/ActionsEvents/EventBusComponent.cs` | per-entity event bus |
| `SkillComponent` | `Assets/Scripts/Model/EntitySkills/SkillComponent.cs` | marker component added to the player entity |
| `CameraComponent` | `Assets/Scripts/EntitySystem/Entities/CameraComponent.cs` | smoothed pivot with aim offset, rigid orbit by yaw, pitch and distance; local player only |

## Concrete entities

| Entity | Folder | Notes |
|---|---|---|
| `PlayerEntity` | `Assets/Scripts/EntitySystem/Entities/Player/` | Initialised in `PlayerModelView.OnNetworkSpawn` |
| `MobEntity` | `Assets/Scripts/EntitySystem/Entities/Mob/` | Movement through `RoamMoveController` and `MobController`; body size from `EntityInfo` (`BodyRadius`, `BodyHeight`, `GroundOffset`) |
| `Mimic` | `Assets/Scripts/EntitySystem/Entities/Mimic/Scripts/` | Procedural legs: `LegPlanner`, `LegStore`, `MimicLegJob` (Burst), `SurfaceMotor` |

## Data flow

1. `MobGenerator` diffs the chunks around the player against the spawned set (`_diffBuf`, `_pruneBuf`, no allocation).
2. It calls `EntitySystem.CreateMob`, which instantiates the prefab and calls `AEntity.Init`.
3. The entity's view model subscribes to the entity; despawn clears the subscriptions (`DisposableBag`).
4. `EntitySystem.FixedUpdate` ticks the entities in `global`; today these are players only.
5. Entity commands: `IScheduledCommand` in `Assets/Scripts/EntitySystem/Command/IScheduledCommand.cs`, with `MoveAlongPathCommand`, `PrintMessageCommand`.

## Shared types

- Events and their payload structs: `Assets/Scripts/Model/ActionsEvents/EventNames.cs` (several types in one file).
- `ViewSector`, `ViewOrientation`, `IViewOriented`, `ViewOrientedTransform`: `Assets/Scripts/Model/BehavioursMap/View/`; described in `docs/map/world.md`.
- `IController`: `Assets/Scripts/Model/BehavioursMap/Movement/IController.cs`.

## Small modules

| Module | Folder | Contents |
|---|---|---|
| Casino | `Assets/Scripts/EntitySystem/Casino/` | `CasinoSlots`, `CalculateWin`, `UICasinoSlots` |
| Static NPC | `Assets/Scripts/EntitySystem/StaticNPC/` | `IsaNPCchatBot.cs` (one line, empty) |
| Interaction | `Assets/Scripts/EntitySystem/Info/InteractionComponent/` | `InteractionComponent`, `BindInfo` |
| Skills | `Assets/Scripts/Model/EntitySkills/` | `SkillComponent`, `SkillData` |
| Experiments | `Assets/Scripts/Test/` | `PointDraw`, `PriceChart`, `Market`, `MarketCore`, `MarketSimulator`; referenced from a test scene and a prefab |
| Editor tools | `Assets/Scripts/Editor/` | `TreeMeshBaker`, `SimulationRulesEditor`, `StrainCodonTableBuilder`, grass bakers |

## Invariants

- Three unrelated command contracts: `IScheduledCommand` (entity commands here), `IEntityCommand` (agent commands, `EntityCommandRunner`), `ICommand` (terminal, `Assets/Scripts/Terminal/ICommand.cs`).
- R3 stays where it is (`AEntity.IsActive`, `OnUpdate`, `EnergyComponent`, `CurrencyComponent`); agent hot paths do not use it.
- A disposed `PlayerEntity` removes itself from `ServiceLocator.Players`; `EntitySystem` skips disposed entities.

## Mob collision

- `MoveMobsJob` resolves a mob's new position with `ObstacleQuery.ResolveSwept` and takes its height from terrain plus `ObstacleQuery.Support`; no PhysX.
- `MobController` keeps SoA per mob: `mobBodies` (radius, height, ground offset from `EntityInfo`) and `mobPositions`, both swap-back with the rest.
- `RoamMoveController` still uses `Rigidbody2D`, `CircleCollider2D` and `OnTriggerEnter2D`: the trigger is a probe ahead of the mob (`colliderDistance`)
  that replans the route backwards on contact with another 2D collider. It never meets the 3D world and was left in place.

## Hot paths

`MimicLegJob` and the mob movement jobs. See `docs/map/HOT_PATHS.md`.

## Open issues

- Spawn desync: `EntitySystem.CreateMob` finds the view model with `GetComponentInChildren`, `MobGenerator` with `GetComponent` on the root.
  If the two disagree, the entity is created but not reported as spawned.

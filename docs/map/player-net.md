# Player and networking

Root: `Assets/Scripts/EntitySystem/Entities/Player/`, `Assets/Scripts/GameSceneManagerSystem/`.
Assembly: `EntitySystem`; the connection classes are in `Assembly-CSharp`.
Tests: `Assets/Tests/EditMode/RateBudgetTests.cs`, `Assets/Tests/EditMode/JumpChargeLimitTests.cs`, `Assets/Tests/EditMode/PlayerSkillGraphTests.cs`.

Read `docs/SECURITY.md` before changing an RPC. Its section 14 is the RPC registry and the only list of RPCs; do not copy it here.
Open security findings: `docs/audit/security_audit.md`.

## Entry points

| Type | File | Role |
|---|---|---|
| `AgentSnapshotStream` | `Assets/Scripts/EntitySystem/AI/Population/AgentSnapshotStream.cs` | Server: agents by area of interest to every remote client |
| `PlayerController` | `Assets/Scripts/EntitySystem/Entities/Player/PlayerController.cs` | All player RPCs, server movement integration, world edits |
| `PlayerModelView` | `Assets/Scripts/EntitySystem/Entities/Player/PlayerModelView.cs` | Player view model; `OnNetworkSpawn` initialisation; camera request |
| `PlayerEntity` | `Assets/Scripts/EntitySystem/Entities/Player/PlayerEntity.cs` | Player entity; metabolism on the authority; `ApplyServerVitals` on clients |
| `PlayerItemLedger` | `Assets/Scripts/EntitySystem/Entities/Player/Components/PlayerItemLedger.cs` | Server-side item ledger of a remote player (session only) |
| `RateBudget` | `Assets/Scripts/EntitySystem/Entities/Player/Components/RateBudget.cs` | GCRA rate limit per player |
| `JumpChargeLimit` | `Assets/Scripts/EntitySystem/Entities/Player/Components/JumpChargeLimit.cs` | Caps a jump charge by server time |
| `PlayerSkillGraph` | `Assets/Scripts/EntitySystem/Entities/Player/Components/PlayerSkillGraph.cs` | Player skill graph: store of capacity 1, own topology, commands, replication packets, save |
| `PlayerStamina` | `Assets/Scripts/EntitySystem/Entities/Player/Components/PlayerStamina.cs` | Stamina integration |
| `TerrainPhysicsResolver` | `Assets/Scripts/EntitySystem/Entities/Player/Components/TerrainPhysicsResolver.cs` | Terrain contact for the player body |
| `PCController` | `Assets/Scripts/EntitySystem/Entities/Player/PCController.cs` | Mouse and keyboard `IController` |
| `JoistickController` | `Assets/Scripts/EntitySystem/Entities/Player/JoystickPack/JoistickController.cs` | Touch `IController` |
| `CameraInputController` | `Assets/Scripts/EntitySystem/CameraInputController.cs` | Camera zoom and 90В° turns |
| `NetworkConnectionManager` | `Assets/Scripts/GameSceneManagerSystem/NetworkConnectionManager.cs` | Starts host / server / client |
| `NetworkTransportConfig` | `Assets/Scripts/GameSceneManagerSystem/NetworkTransportConfig.cs` | Transport settings |
| `NetworkUIController` | `Assets/Scripts/GameSceneManagerSystem/NetworkUIController.cs` | Mode and address UI |
| `WorldEdits` | `Assets/Scripts/ObjectSystem/Map/WorldGeneration/WorldCommandBuffer.cs` | `TryPlace`, `TryPickUp` validation on the server |

`PlayerController.cs` is over 500 lines: take the member's line from `docs/map/LARGE_FILES.md`.

## Agent replication

- Agents are not `NetworkObject`s (`EntityManager.snapshotStream`): the server sends each remote client the agents inside `snapshotRadius` of its player.
- `AgentSnapshotStream` (server, `Assets/Scripts/EntitySystem/AI/Population/AgentSnapshotStream.cs`): named message `ravine.agents`, unreliable sequenced, at most 64 agents of 12 bytes each.
- `AgentSnapshotCodec` v2 uses a 22-byte header with a 32-bit lifetime in milliseconds;
  `AgentPuppets` (client) validates every message, keeps a bounded pool of bodies and removes a body when its time to live passes.
- The host sees the real bodies. Clients send nothing: the area of interest is taken from the server's own position of the player.

## Data flow

1. Architecture: host = server + client (listen server), Netcode for GameObjects.
2. A client sends intent only: direction, charge, cell, recipe index. The server computes the result.
3. `MoveServerRpc` stores the validated intent; the server integrates stamina, acceleration and velocity in `FixedUpdate`.
4. Intent older than `intentTimeout` counts as zero; state is broadcast every `sendInterval`, skipped when unchanged.
5. `StopMoveServerRpc` clears the intent; speed decays at the server `Deceleration`.
6. `JumpServerRpc(charge)`: force from `baseJumpForce` and `jumpForceCurve`; the charge is capped by `JumpChargeLimit`.
7. Place and pick-up: reach check against the server position, `editRate` / `editBurst` budget, then `WorldEdits`.
8. A remote player's placement is debited from `PlayerItemLedger`; pick-ups and `CraftRequestServerRpc` credit it.
9. Health and energy are published by the server; clients apply them through `PlayerEntity.ApplyServerVitals`.
10. Refusals are counted; one summary line per `refuseLogInterval`, no formatting per refusal.

## Skill graph

- `PlayerSkillGraph` is an `IComponent` of `PlayerEntity`; the rules are those of `SkillGraphMath`, `SkillGraphSolver` and `SkillGraphXp` that agents use (`docs/map/ai.md`, Skills).
- The graph is authoritative where `WorldAuthority.IsAuthority`; a client owner holds a mirror filled by `SkillTopologyClientRpc` and `SkillLevelsClientRpc`.
- `PlayerEntity.UpdateEntityCycle` runs `PlayerSkillGraph.Tick` (decay sweep, solver in auto mode) and re-applies `SkillModifiers` to maximum health and energy.
- `PlayerStress` (authority, `playerStressEnabled`): the same `CreatureCore` stress rates as agents, contagion from `EntitySpatialGrid.CountAlarmed`; raises basal drain.
- `PlayerController.StepServerMovement` multiplies speed by the `Speed` and `RunSpeed` modifiers; reach compensation uses the same scale.
- XP: movement distance (`skillMoveXpDistance`) gives `MoveTo`, a placement `Put`, a pick-up `Take`, speech `Signal`.
- Commands: `PlayerController.RequestSkillCommand`; the host applies them directly through `PlayerSkillGraph.TryExecute`, a client sends `SkillGraphCommandServerRpc`.
- Save: `WorldState.playerSkills` (`PlayerSkillSave`), written by `PlayerEntity.CaptureSkills` before autosave for the host player only.

## Player tick

- `TerrainPhysicsResolver.Resolve` first pushes the body out of object shapes (`MapGenerator.ResolveObstacles`, swept from the previous resolved position), then resolves the ground.
- Terrain contact samples the same triangle split as the near terrain mesh. The resolver checks intervening positions
  before resolving vertical contact. An uphill crossing into a steep face or into terrain above the allowed step or airborne trajectory clips horizontal movement.
- Ground is `max(terrain surface, MapGenerator.SupportObstacles)`. Nearby walkable ground can follow an already grounded body down small drops.
  An unloaded terrain sample stops movement at the last resolved position until data is available.
- Landing on walkable ground or on an obstacle top grants jump support. The server can consume it once, including within `coyoteTime` after leaving support.
  Contact with a steep terrain face does not grant support.
- Feet for both queries are `pos.y - halfHeight + GroundOffset`: the surface height at the body centre, the same frame as the shape bottoms.
- A displacement beyond `ObstacleQuery.SweepReach` or a `SetActive` call counts as a relocation: no sweep, the previous position is dropped.
- `StepServerMovement` removes the velocity component directed into the obstacle or terrain face (`ClipVelocity`), so the player slides along it.
- Body size: `bodyRadius` and `characterHalfHeight` of `PlayerController`. Collision data: `docs/map/world.md`, section Collision.

- `PlayerEntity` ticks in `EntitySystem.FixedUpdate`: every player on the server, only its own on a client.
- Metabolism runs only where `WorldAuthority.IsAuthority`; behaviour runs only for the owner.
- Player behaviours: `Assets/Scripts/EntitySystem/Entities/Player/PlayerBehaviours/`.

## Camera

- One camera per owner, requested through `PlayerModelView.RequestCameraServerRpc`; a repeated request is ignored.
- `CameraComponent` exists only for the local player and is the only writer of `ViewOrientation`:
  yaw, pitch and distance, a rigid orbit around a smoothed pivot (player plus `AimAddition`), never below the terrain.
- The mouse wheel changes only the camera distance (`CameraInputController`); `WheelCaptured` and `ConsumeWheelStep` serve the aim level.
- Dragging the left button (on mobile a single touch that did not start on the joystick) inside the central square of 0.9 of the short screen side turns the view; a press over UI is not a drag.
  The rules are in `ViewDragGesture` and are tested.
- A press that stays within `CameraDragThresholdPixels` is a click: `ViewClick` on release triggers `PlayerController.AimPlace`; a drag suppresses it.
- Tuning is in `SimulationRules`: `CameraYawDegreesPerSide`, `CameraPitchDegreesPerSide`, `CameraPitchMin`, `CameraPitchMax`, `CameraDistanceMax`, `CameraDistanceMin`,
  `CameraTerrainClearance`, `CameraPivotHeight` (height of the look-at point above the player), `CameraInvertDragYaw`, `CameraInvertDragPitch`.
- Movement goes to the server as a world vector (`ViewOrientation.ToWorld` on the client); the server does not know the view.
- What the view changes in the world: `docs/map/world.md`, section Camera view.

## Invariants

- RPC arguments are intent, never values such as force, position or currency.
- Identity comes from the transport (`OwnerClientId`), never from an argument.
- Every RPC has a server budget and its row in `docs/SECURITY.md` section 14.
- The player gets no special path: the same rules as any creature (`PHILOSOPHY.md`).
- Player input and physics may use Unity time where the file already does; agent logic may not.
- `DayCycle` and currency network variables are written by the server only.

## Hot paths

RPC handlers are a server hot path: no allocation and no string formatting on the success path.

## Open issues

- Player identity: `PlayerEntity` is given `NetworkManager.LocalClientId` instead of `OwnerClientId`.
  `PlayerContainer` therefore registers only the first player on a machine, and UI relies on the first entry being local.
- On a remote client `PlayerController.SetUp` may run before the camera RPC arrives; the controller then has no camera.
- Remote inventories and remote skill graphs cannot be persisted in the host's world without a stable player identity.
- World edits reach only the editing client (audit finding 27); `NetworkManager` prefab settings (finding 12).

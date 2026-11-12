# Player and networking

Root: `Assets/Scripts/EntitySystem/Entityes/Player/`, `Assets/Scripts/GameSceneManagerSystem/`.
Assembly: `EntitySystem`; the connection classes are in `Assembly-CSharp`.
Tests: `Assets/Tests/EditMode/RateBudgetTests.cs`, `Assets/Tests/EditMode/JumpChargeLimitTests.cs`.

Read `docs/SECURITY.md` before changing an RPC. Its section 14 is the RPC registry and the only list of RPCs; do not copy it here.
Open security findings: `docs/audit/security_audit.md`.

## Entry points

| Type | File | Role |
|---|---|---|
| `PlayerController` | `Assets/Scripts/EntitySystem/Entityes/Player/PlayerController.cs` | All player RPCs, server movement integration, world edits |
| `PlayerModelView` | `Assets/Scripts/EntitySystem/Entityes/Player/PlayerModelView.cs` | Player view model; `OnNetworkSpawn` initialisation; camera request |
| `PlayerEntity` | `Assets/Scripts/EntitySystem/Entityes/Player/PlayerEntity.cs` | Player entity; metabolism on the authority; `ApplyServerVitals` on clients |
| `PlayerItemLedger` | `Assets/Scripts/EntitySystem/Entityes/Player/Components/PlayerItemLedger.cs` | Server-side item ledger of a remote player (session only) |
| `RateBudget` | `Assets/Scripts/EntitySystem/Entityes/Player/Components/RateBudget.cs` | GCRA rate limit per player |
| `JumpChargeLimit` | `Assets/Scripts/EntitySystem/Entityes/Player/Components/JumpChargeLimit.cs` | Caps a jump charge by server time |
| `PlayerStamina` | `Assets/Scripts/EntitySystem/Entityes/Player/Components/PlayerStamina.cs` | Stamina integration |
| `TerrainPhysicsResolver` | `Assets/Scripts/EntitySystem/Entityes/Player/Components/TerrainPhysicsResolver.cs` | Terrain contact for the player body |
| `PCController` | `Assets/Scripts/EntitySystem/Entityes/Player/PCController.cs` | Mouse and keyboard `IController` |
| `JoistickController` | `Assets/Scripts/EntitySystem/Entityes/Player/Joystick Pack/JoistickController.cs` | Touch `IController` |
| `CameraInputController` | `Assets/Scripts/EntitySystem/CameraInputController.cs` | Camera zoom and 90° turns |
| `NetworkConnectionManager` | `Assets/Scripts/GameSceneManagerSystem/NetworkConnectionManager.cs` | Starts host / server / client |
| `NetworkTransportConfig` | `Assets/Scripts/GameSceneManagerSystem/NetworkTransportConfig.cs` | Transport settings |
| `NetworkUIController` | `Assets/Scripts/GameSceneManagerSystem/NetworkUIController.cs` | Mode and address UI |
| `CurrencyNetworkComponent` | `Assets/Scripts/EntitySystem/Currency/CurrencyNetworkComponent.cs` | Server-written currency; `TryServerAdd` |
| `WorldEdits` | `Assets/Scripts/ObjectSystem/Map/WorldGeneration/WorldCommandBuffer.cs` | `TryPlace`, `TryPickUp` validation on the server |

`PlayerController.cs` is over 500 lines: take the member's line from `docs/map/LARGE_FILES.md`.

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

## Player tick

- `TerrainPhysicsResolver.Resolve` first pushes the body out of object shapes (`MapGenerator.ResolveObstacles`, swept from the previous resolved position), then resolves the ground.
- Ground is `max(terrain height, MapGenerator.SupportObstacles)` with `maxStepHeight` as the step-up tolerance; a body on a shape top keeps its height until it leaves the shape.
- Feet for both queries are `pos.y - halfHeight + GroundOffset`: the surface height at the body centre, the same frame as the shape bottoms.
- A displacement beyond `ObstacleQuery.SweepReach` or a `SetActive` call counts as a relocation: no sweep, the previous position is dropped.
- `StepServerMovement` removes the velocity component directed into the obstacle (`ClipVelocity`), so the player slides along it.
- Body size: `bodyRadius` and `characterHalfHeight` of `PlayerController`. Collision data: `docs/map/world.md`, section Collision.

- `PlayerEntity` ticks in `EntitySystem.FixedUpdate`: every player on the server, only its own on a client.
- Metabolism runs only where `WorldAuthority.IsAuthority`; behaviour runs only for the owner.
- Player behaviours: `Assets/Scripts/EntitySystem/Entityes/Player/PlayerBehaviours/`.

## Camera

- One camera per owner, requested through `PlayerModelView.RequestCameraServerRpc`; a repeated request is ignored.
- `CameraComponent` exists only for the local player and is the only writer of `ViewOrientation`.
- The mouse wheel zooms; Mouse4 and Mouse5 turn the view by 90°, one step per press.
- Debounce and turn time: `SimulationRules.CameraFlipDebounce`, `SimulationRules.CameraRotateSeconds`.
- Smooth or instant turn: `GlobalSettings.smoothCameraRotation`.
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
- Remote inventories cannot be persisted in the host's world without a stable player identity.
- World edits reach only the editing client (audit finding 27); `NetworkManager` prefab settings (finding 12).

# Security audit

Snapshot: commit `fc9cb3f` (branch `claude/practical-mendel-c5lbxd`). Offline audit, no code change. Principles: [../SECURITY.md](../SECURITY.md). Paths are relative to `Assets/Scripts/` unless they start with `Assets/`.

Severity: **Critical** — a remote client breaks equality or the world directly. **High** — remote DoS or a cheat that needs only RPC spam. **Medium** — needs a crafted file, a shared session with a debug command, or a hostile script. **Low** — local only or hardening.

## 1. Findings

| # | Severity | Where | Problem | SECURITY.md | Status |
|---|---|---|---|---|---|
| 1 | Critical | `EntitySystem/Currency/CurrencyNetworkComponent.cs:25-30` | `RequestUpdateRpc(int newValue)` with `InvokePermission.Everyone`: any client sets the currency of any object to any non-negative value. Client-supplied absolute value, no ownership. | §2 | Fixed, see §4 |
| 2 | Critical | `EntitySystem/Entityes/Player/PlayerController.cs:425` | `JumpServerRpc(float force)`: force is chosen by the client, not checked for finiteness or range. Arbitrary vertical launch; NaN into the Rigidbody. | §2, §4 | Fixed, see §4 |
| 3 | High | `EntitySystem/Entityes/Player/PlayerModelView.cs:75` | `RequestCameraServerRpc(ulong clientId)`: owner id taken from an argument; no limit on calls. Every call `Instantiate` + `SpawnWithOwnership` a network object: spawn spam, and cameras spawned for another client id. | §2, §6 | Fixed, see §4 |
| 4 | High | `EntitySystem/Entityes/Player/PlayerController.cs:346`, `:371` | `MoveServerRpc` integrates stamina and acceleration with the client's `sendInterval` per call. No server-side rate limit: sending the RPC N× more often gives N× regen / acceleration. | §5 | Fixed, see §4 |
| 5 | High | `EntitySystem/Entityes/Player/PlayerController.cs:610`, `:623` | Place / PickUp RPCs have no server-side rate limit (`placeObjectDelay` is client-only). Each accepted edit calls `generator.ExtraUpdate()`. | §5, §6 | Fixed, see §4 |
| 6 | High | `EntitySystem/Entityes/Player/PlayerController.cs:697`, `Core/RavineLogger.cs:121` | Every refused edit formats a string and writes it with synchronous `File.AppendAllText`. RPC spam → main-thread file I/O and unbounded log growth on the server. | §6 | Fixed, see §4 |
| 7 | Medium | `Terminal/Rive/RiveExecutor.cs:31` | `ExecuteAsync` resets `_operationCount` on every call, including nested script-to-script calls (`RiveRuntime.CallFunctionAsync`). No recursion depth limit: self-recursion bypasses `MAX_OPERATIONS` and can overflow the stack. | §8 | Open |
| 8 | Medium | `Terminal/Rive/RiveExecutor.cs:137` | `for` counts operations only inside body statements: an empty body iterates up to 2³¹ times on the main thread without yielding. `i <= e` with `e == int.MaxValue` never terminates (overflow). | §8 | Open |
| 9 | Medium | `Terminal/SetPlayerCommand.cs:46`, `:99` | `~tp` and `~set speed` modify the local entity directly. On the host this changes the server's entity: teleport and speed up to 100 for the host player only. No release / multiplayer gate in the code. Rive scripts can issue these commands too. | §3 | Open |
| 10 | Medium | `EntitySystem/AI/LSTM/LSTMMemoryStorage.cs:29`, `EntitySystem/AI/DelayedPerceptron/DelayedPerceptronStorage.cs:66` | Network sizes read from the file are used to allocate before they are compared with the expected architecture; weights are not checked for NaN / Inf. A crafted model file → huge allocation or NaN brain. (`SharedBrainSnapshot.ReadBlock` bounds blocks correctly.) | §7 | Open |
| 11 | Medium | `ObjectSystem/Map/WorldGeneration/ChunkStore.cs:204-206`, `:238` | Terrain edit heights and object positions from chunk files are not checked for finiteness. | §7 | Open |
| 12 | Medium | `Assets/Prefabs/MainPrefabs/Netcore/NetworkManager.prefab:58`, `:62`, `:92`, `:100` | `ConnectionApproval: 0`, `ProtocolVersion: 0`, `EnsureNetworkVariableLengthSafety: 0`, `m_UseEncryption: 0`, public IP `185.16.139.218 ` (with a trailing space) hardcoded, listen on `::`. | §9, §10 | Open |
| 13 | Low | `Core/Storage/EncryptionUtility.cs:10`, `:95` | Key is a constant in the client (obfuscation only); HMAC compared byte by byte, not in constant time. Local saves only, no remote timing. | §9 | Open |
| 14 | Low | `Core/Settings/WorldRegistry.cs:299`, `:393` | World ids read back from storage are not re-validated before `Directory.Delete(path, true)`; a tampered list with `..` deletes outside `worlds/`. Local only. | §7 | Open |
| 15 | Low | `Core/RavineLogger.cs:58` | `loggedErrors` grows without bound. | §6 | Open |
| 16 | Low | `Terminal/Rive/RiveExecutor.cs:166` | Variable substitution into terminal commands uses substring `Replace`: a variable named `i` rewrites every `i` in the command. | §8 | Open |

## 2. What already follows the principles

- `MoveServerRpc` (`PlayerController.cs:336`): finite check, direction normalized, speed saturated; speed computed on the server.
- `SpeakServerRpc` (`PlayerController.cs:574`): alive, length, bit width, cooldown, energy cost — reference handler for §3–§5.
- Place / PickUp: reach checked against the server position with bounded latency compensation (`PlayerController.cs:658-694`); object id, placeability and level validated in `WorldEdits.TryPlace` / `TryPickUp` (`ObjectSystem/Map/WorldGeneration/WorldCommandBuffer.cs:221-262`); authoritative result returned to the owner.
- `ChunkStore`: magic, format, seed and key in the header; counts and indices bounded; temp file + atomic move.
- `SharedBrainSnapshot`: magic + version, block length checked against the stream.
- `WorldRegistry.IsValidWorldName`: length, `.`/`..`, invalid characters, separators.
- Player `NetworkTransform` is server-authoritative (`Assets/Prefabs/MainPrefabs/Player.prefab:192`, `AuthorityMode: 0`); `DayCycle` and currency `NetworkVariable`s are server-written.
- Rive: integer-only values, no file / network / reflection access, single `wait` capped at 60 s, operation limit per statement list.

## 3. Not checked

- Whether the terminal is reachable in release builds and in client sessions (depends on scenes and build settings, not on code).
- Other RPC-like paths outside `NetworkBehaviour` (custom messages) — none found by `grep` for `ServerRpc|ClientRpc|[Rpc`.
- `LLMItemDescriptionService` output rendering: prompt is built from item and player data only (`UI/Inventory/Extensions/LLMItemDescriptionService.cs:74`); whether the result is rendered with rich text was not traced.

## 4. Fixes

Findings 1–6 are fixed in the commit that adds this section. Line references in §1 point to the snapshot before the fix.

1. `CurrencyNetworkComponent`: the client RPC is removed. The server changes the value through `TryServerAdd(int delta)`, which works only on the server, rejects a negative result and `int` overflow.
2. `JumpServerRpc(float charge)`: the client sends the charge, not the force. The server rejects non-finite values, saturates the charge to [0, 1] and computes the force from its own `baseJumpForce` and `jumpForceCurve`.
3. `RequestCameraServerRpc()`: no argument; the owner is `OwnerClientId`. One camera per player: a repeated request while the camera is spawned is ignored. The client handler uses `TryGetValue` instead of the indexer.
4. Movement: `MoveServerRpc` only stores the validated intent (direction, speed, sprint, server time of arrival). Stamina, acceleration and velocity are integrated in the server's `FixedUpdate` with `Time.fixedDeltaTime`, so the RPC rate does not change the result. Intent older than `intentTimeout` (0.6 s) is treated as zero. State is broadcast at the server cadence `sendInterval`, skipped when position and velocity did not change; `staminaRatio` is written at the same cadence. `StopMoveServerRpc` and `JumpServerRpc` no longer broadcast per call.
5. Place / PickUp: per-player GCRA limit on the server (`editRate` = 20/s, `editBurst` = 16), checked first in `TryResolveEditor`. A limited request still gets a refusal reply, so the client's pending queues stay in order.
6. Refusals: `Refuse` takes an enum reason and result codes, increments a counter and writes at most one summary line per `refuseLogInterval` (5 s) per player; no string formatting for suppressed refusals. `RavineLogger` buffers file writes: flush on error / critical, at 8 K characters or once per second, and on `Application.quitting`; file logging stops at 16 M characters with one final line.

Not verified in the Unity Editor: the container has no Unity; the changed files were checked only by a C# syntax parse. Behaviour to check in play mode: movement feel (acceleration now integrates per physics step instead of per RPC), sprint drain / regen rates, area pick-up (9 requests per press fits the burst), host and remote client.

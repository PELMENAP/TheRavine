# Security audit

Snapshot: commit `57e3fd9` (branch `claude/practical-mendel-c5lbxd`). Offline audit, no code change. Principles: [../SECURITY.md](../SECURITY.md). Paths are relative to `Assets/Scripts/` unless they start with `Assets/`.

Severity: **Critical** — a remote client breaks equality or the world directly. **High** — remote DoS or a cheat that needs only RPC spam. **Medium** — needs a crafted file, a shared session with a debug command, or a hostile script. **Low** — local only or hardening.

## 1. Findings

| # | Severity | Where | Problem | SECURITY.md |
|---|---|---|---|---|
| 1 | Critical | `EntitySystem/Currency/CurrencyNetworkComponent.cs:25-30` | `RequestUpdateRpc(int newValue)` with `InvokePermission.Everyone`: any client sets the currency of any object to any non-negative value. Client-supplied absolute value, no ownership. | §2 |
| 2 | Critical | `EntitySystem/Entityes/Player/PlayerController.cs:425` | `JumpServerRpc(float force)`: force is chosen by the client, not checked for finiteness or range. Arbitrary vertical launch; NaN into the Rigidbody. | §2, §4 |
| 3 | High | `EntitySystem/Entityes/Player/PlayerModelView.cs:75` | `RequestCameraServerRpc(ulong clientId)`: owner id taken from an argument; no limit on calls. Every call `Instantiate` + `SpawnWithOwnership` a network object: spawn spam, and cameras spawned for another client id. | §2, §6 |
| 4 | High | `EntitySystem/Entityes/Player/PlayerController.cs:346`, `:371` | `MoveServerRpc` integrates stamina and acceleration with the client's `sendInterval` per call. No server-side rate limit: sending the RPC N× more often gives N× regen / acceleration. | §5 |
| 5 | High | `EntitySystem/Entityes/Player/PlayerController.cs:610`, `:623` | Place / PickUp RPCs have no server-side rate limit (`placeObjectDelay` is client-only). Each accepted edit calls `generator.ExtraUpdate()`. | §5, §6 |
| 6 | High | `EntitySystem/Entityes/Player/PlayerController.cs:697`, `Core/RavineLogger.cs:121` | Every refused edit formats a string and writes it with synchronous `File.AppendAllText`. RPC spam → main-thread file I/O and unbounded log growth on the server. | §6 |
| 7 | Medium | `Terminal/Rive/RiveExecutor.cs:31` | `ExecuteAsync` resets `_operationCount` on every call, including nested script-to-script calls (`RiveRuntime.CallFunctionAsync`). No recursion depth limit: self-recursion bypasses `MAX_OPERATIONS` and can overflow the stack. | §8 |
| 8 | Medium | `Terminal/Rive/RiveExecutor.cs:137` | `for` counts operations only inside body statements: an empty body iterates up to 2³¹ times on the main thread without yielding. `i <= e` with `e == int.MaxValue` never terminates (overflow). | §8 |
| 9 | Medium | `Terminal/SetPlayerCommand.cs:46`, `:99` | `~tp` and `~set speed` modify the local entity directly. On the host this changes the server's entity: teleport and speed up to 100 for the host player only. No release / multiplayer gate in the code. Rive scripts can issue these commands too. | §3 |
| 10 | Medium | `EntitySystem/AI/LSTM/LSTMMemoryStorage.cs:29`, `EntitySystem/AI/DelayedPerceptron/DelayedPerceptronStorage.cs:66` | Network sizes read from the file are used to allocate before they are compared with the expected architecture; weights are not checked for NaN / Inf. A crafted model file → huge allocation or NaN brain. (`SharedBrainSnapshot.ReadBlock` bounds blocks correctly.) | §7 |
| 11 | Medium | `ObjectSystem/Map/WorldGeneration/ChunkStore.cs:191` | Terrain edit heights and object positions from chunk files are not checked for finiteness. | §7 |
| 12 | Medium | `Assets/Prefabs/MainPrefabs/Netcore/NetworkManager.prefab:58`, `:62`, `:92`, `:100` | `ConnectionApproval: 0`, `ProtocolVersion: 0`, `EnsureNetworkVariableLengthSafety: 0`, `m_UseEncryption: 0`, public IP `185.16.139.218 ` (with a trailing space) hardcoded, listen on `::`. | §9, §10 |
| 13 | Low | `Core/Storage/EncryptionUtility.cs:10`, `:95` | Key is a constant in the client (obfuscation only); HMAC compared byte by byte, not in constant time. Local saves only, no remote timing. | §9 |
| 14 | Low | `Core/Settings/WorldRegistry.cs:299`, `:393` | World ids read back from storage are not re-validated before `Directory.Delete(path, true)`; a tampered list with `..` deletes outside `worlds/`. Local only. | §7 |
| 15 | Low | `Core/RavineLogger.cs:58` | `loggedErrors` grows without bound. | §6 |
| 16 | Low | `Terminal/Rive/RiveExecutor.cs:166` | Variable substitution into terminal commands uses substring `Replace`: a variable named `i` rewrites every `i` in the command. | §8 |

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

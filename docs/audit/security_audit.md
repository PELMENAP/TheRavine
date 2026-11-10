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
| 7 | Medium | `Terminal/Rive/RiveExecutor.cs:31` | `ExecuteAsync` resets `_operationCount` on every call, including nested script-to-script calls (`RiveRuntime.CallFunctionAsync`). No recursion depth limit: self-recursion bypasses `MAX_OPERATIONS` and can overflow the stack. | §8 | Fixed, see §6 |
| 8 | Medium | `Terminal/Rive/RiveExecutor.cs:137` | `for` counts operations only inside body statements: an empty body iterates up to 2³¹ times on the main thread without yielding. `i <= e` with `e == int.MaxValue` never terminates (overflow). | §8 | Fixed, see §6 |
| 9 | Medium | `Terminal/SetPlayerCommand.cs:46`, `:99` | `~tp` and `~set speed` modify the local entity directly. On the host this changes the server's entity: teleport and speed up to 100 for the host player only. No release / multiplayer gate in the code. Rive scripts can issue these commands too. | §3 | Fixed, see §6 |
| 10 | Medium | `EntitySystem/AI/LSTM/LSTMMemoryStorage.cs:29`, `EntitySystem/AI/DelayedPerceptron/DelayedPerceptronStorage.cs:66` | Network sizes read from the file are used to allocate before they are compared with the expected architecture; weights are not checked for NaN / Inf. A crafted model file → huge allocation or NaN brain. (`SharedBrainSnapshot.ReadBlock` bounds blocks correctly.) | §7 | Fixed, see §6 |
| 11 | Medium | `ObjectSystem/Map/WorldGeneration/ChunkStore.cs:204-206`, `:238` | Terrain edit heights and object positions from chunk files are not checked for finiteness. | §7 | Fixed, see §6 |
| 12 | Medium | `Assets/Prefabs/MainPrefabs/Netcore/NetworkManager.prefab:58`, `:62`, `:92`, `:100` | `ConnectionApproval: 0`, `ProtocolVersion: 0`, `EnsureNetworkVariableLengthSafety: 0`, `m_UseEncryption: 0`, public IP `185.16.139.218 ` (with a trailing space) hardcoded, listen on `::`. | §9, §10 | Open |
| 13 | Low | `Core/Storage/EncryptionUtility.cs:10`, `:95` | Key is a constant in the client (obfuscation only); HMAC compared byte by byte, not in constant time. Local saves only, no remote timing. | §9 | Open |
| 14 | Low | `Core/Settings/WorldRegistry.cs:299`, `:393` | World ids read back from storage are not re-validated before `Directory.Delete(path, true)`; a tampered list with `..` deletes outside `worlds/`. Local only. | §7 | Fixed, see §6 |
| 15 | Low | `Core/RavineLogger.cs:58` | `loggedErrors` grows without bound. | §6 | Open |
| 16 | Low | `Terminal/Rive/RiveExecutor.cs:166` | Variable substitution into terminal commands uses substring `Replace`: a variable named `i` rewrites every `i` in the command. | §8 | Fixed, see §6 |
| 17 | High | `ObjectSystem/Map/WorldGeneration/WorldCommandBuffer.cs:244`, `UI/Inventory/UIInventory.cs:217-222` | The server does not know a remote player's inventory: `TryPlace` checks only that the object is placeable; ownership is checked on the client. A patched client places any placeable object without limit. Crafting and the inventory live only in the client's local save. | §2 | Fixed, see §5 |
| 18 | High | `EntitySystem/Entityes/Player/PlayerModelView.cs:62-66`, `EntitySystem/Entityes/Player/PlayerEntity.cs:79-86` | A remote player's `PlayerEntity` is not ticked on the server (`AddToGlobal` only for the owner): no metabolism, `MortalityComponent` never dies, server-side `player dead` checks always pass. Hunger and death are decided by the client. | §2, §3 | Fixed, see §5 |
| 19 | Medium | `Core/Storage/EncryptionUtility.cs:10`, `ProjectSettings/ProjectSettings.asset:725-727` | The save key is a `const string`: in IL2CPP it is stored in plain text in `global-metadata.dat`; Android builds use Mono (`scriptingBackend: Android: 0`), so `Assembly-CSharp.dll` decompiles directly. | §9 | Open |
| 20 | Medium | `Model/BehavioursMap/Currency/SafeInt.cs`, `Model/BehavioursMap/Currency/CurrencyComponent.cs:27` | `SafeInt` only hides the value from memory scanners; a patched getter (`GetRaw`, `operator int`) returns any value. The initial amount comes from the local `WorldState.currency`; `CurrencyNetworkComponent.Initialize` is never called. Dormant: currency is not spent anywhere yet. | §2 | Open |
| 21 | Low | `EntitySystem/Entityes/Player/PlayerController.cs` (`JumpServerRpc`) | The charge is not checked against time: a patched client always sends `charge = 1` and gets the maximum jump without holding. | §5 | Open |
| 22 | Low | design | Listen-server: the host process runs server code, so a host who patches it bypasses every check. Only a dedicated server fixes this. | §3 | Accepted |
| 23 | Medium | `Terminal/Rive/RiveRuntime.cs`, `Terminal/Rive/Core/RiveExecutor.cs` | One `RiveExecutor` held shared `_variableScopes` and `_operationCount`: a second run (after `wait` / `get`) pushed scopes onto the same stack and reset the counter; callees read and overwrote caller variables. | §8 | Fixed, see §6 |
| 24 | Medium | `Terminal/Rive/Core/RiveBuiltInFunctions.cs` | `pow` loops `exp` times on the main thread without op accounting: `pow(2, 2147483647)` runs 2³¹ iterations. | §8 | Fixed, see §6 |
| 25 | Medium | `Terminal/Rive/Core/RiveParser.cs` | Recursive descent has no depth tracking; source length, token count, array literal length and call argument count are unbounded: uncatchable stack overflow. | §6, §8 | Fixed, see §6 |
| 26 | Medium | `Terminal/Rive/Core/InputStreamManager.cs`, `Terminal/Rive/Core/RiveExecutor.cs`, `Terminal/Terminal.cs` | `get` waits forever; `UniTask.Delay` has no cancellation, scripts outlive the world; only a single `wait` is capped, total wait is not. | §6, §8 | Fixed, see §6 |
| 27 | Medium | `EntitySystem/Entityes/Player/PlayerController.cs`, `ObjectSystem/Map/WorldGeneration/WorldCommandBuffer.cs` | Edit results go only to `OwnerTarget`: other clients and late joiners never see world edits. | §2 | Open |
| 28 | Low | `EntitySystem/AI/NeuralModelStorage.cs`, `Terminal/PDollar/Scripts/GestureRepository.cs` | Direct `FileMode.Create` / `File.WriteAllBytes` (crash mid-write destroys the previous save); unbounded `File.ReadAllBytes`; gesture count and points not capped. | §7 | Open |
| 29 | Low | `EntitySystem/Entityes/Player/PlayerController.cs` (`StopMoveServerRpc`) | Sets `CurrentSpeed = 0` and zeroes velocity directly, bypassing server `Deceleration`; no rate limit. | §5 | Open |
| 30 | Low | `EntitySystem/Entityes/Player/PlayerController.cs` (`SpeakServerRpc`) | Two `GetEntityComponent` lookups run before token length, bit width and cooldown are checked. | §4, §5 | Open |
| 31 | Low | `EntitySystem/Entityes/Player/PlayerController.cs` | Move, Stop, Jump and Craft ServerRpcs have no per-client rate limit. | §5, §6 | Open |

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

## 5. Fixes for findings 17–18

Source of findings 17–22: client patching as in the article on IL2CPP reversing (a getter patched to return `INT_MAX`); the question is which values the client decides.

17. Server-side item ledger for remote players (`EntitySystem/Entityes/Player/Components/PlayerItemLedger.cs`), one per player on the server instance of `PlayerController`.
    - Credited by a server-validated pick-up (`PickUpRequestServerRpc`: item of the picked object, its amount).
    - Debited by a placement: `PlaceRequestServerRpc` refuses with the new `WorldEditResult.NotOwned` when the ledger has no such item.
    - Crafting goes through `CraftRequestServerRpc(recipeIndex)`: the server checks the ingredients in the ledger, consumes the non-catalysts, credits the result, and allows the next craft only after `TimeToComplete × craftTimeTolerance` (0.9). Recipe indices come from one catalog for both sides, `CraftModel.Shared`; `CraftService` now uses it too.
    - The host is not checked against the ledger: its inventory already lives in the server process (finding 22).
    - Limitations: the ledger lives for the session and is lost on disconnect. Items a remote player brings from their own save, and items crafted while the server refused the craft, are visible in their inventory but cannot be placed. Persisting remote inventories in the host's world needs a stable player identity, which the project does not have yet.
18. The server ticks every player.
    - `PlayerModelView` adds the player to `EntitySystem` on the server for every player, and on a client only for its own player.
    - `PlayerEntity.UpdateEntityCycle` runs metabolism only where `WorldAuthority.IsAuthority`, and the behaviour only for the owner.
    - The server publishes health and energy through `NetworkVariable`s every `sendInterval`; a client applies them to its local `StatsComponent` (`PlayerEntity.ApplyServerVitals`), so death on the client follows the server. Initial values are NaN, so nothing is applied before the first publish.
    - Needed for this: `PlayerController.SetUp` sets up the camera, controls and autosave only for the owner (it used to throw for other players' instances without a `CameraComponent`, and every instance on the host wrote its position into the host's save); the spawn position is set only on the server, from the save only for the host's player; `PlayerModelView` creates `CameraComponent` only for the local player.

Not verified in the Unity Editor: the container has no Unity; changed files were checked only by a C# syntax parse. To check in play mode with a host and a remote client: pick-up then place, place without the item (expected refusal `NotOwned`), crafting then placing the result, hunger and death of the remote player.

## 6. Fixes for findings 7, 8, 16 and 23+

7. Recursion limits (`RiveExecutor.ExecuteAsync`, checked on every script-to-script call): at most 32 nested script calls per run (`MaxCallDepth`); a breach sets the run's abort message, so self- and mutual recursion end with `Call depth limit exceeded` in the top-level result. Besides, `RiveRun.StackDepth` counts nested `ExecuteStatementsAsync` / `EvaluateExpressionAsync` levels across all frames of a run and is capped at 128 (`MaxStackDepth`): this bounds the real stack for a deep call chain with deep expressions and for a long left-deep chain (`1+1+…`), which the parser does not bound (finding 25). With 4 concurrent runs the worst synchronous nesting is 4 × 128 levels.

8. `for` loop (`ExecuteForLoopAsync`): every iteration costs one operation, also with an empty body, and an abort or the operation limit ends the loop; the counter is `long`, so a bound of `int.MaxValue` terminates. A loop with an empty body yields every 10 operations. Behaviour change: a loop with a body now costs 1 + body statements per iteration, so the 10000-operation budget allows about half as many iterations of a one-statement body as before.

16. Terminal commands from scripts: `{name}` placeholders are replaced in one left-to-right pass (`SubstituteVariables`), by the whole placeholder; text that is not a placeholder of a defined variable stays as written, a substituted value is not scanned again, and the scan is linear (a placeholder is an identifier directly between braces). Previously the variable name was replaced as a substring anywhere in the command and the braces stayed (`{i}` → `{7}`, and every `i` of `item` too).

9. Debug commands (`~tp`, `~set`, `~debug`, `~showSLRS`): the classes and their registration in `Terminal.Setup` exist only under `UNITY_EDITOR || DEVELOPMENT_BUILD`, so in a release build the commands are unknown, also to Rive scripts. `Validate` of each calls `DebugCommandGate.TryEnter`, which refuses with a message while `WorldAuthority.HasRemotePeers` is true (a client of someone else's server, or a host with another client connected). Both the typed path (`Terminal.ProcessInput`) and the script path (`Terminal.ExecuteTerminalCommandAsync`) call `Validate`. `~rotate` was not in the finding and is unchanged.

10. Neural model loaders: `LSTMMemory.Deserialize` and `DelayedPerceptron.Deserialize` read the header first and refuse (`InvalidDataException`) before any allocation unless the dimensions are in bounds (LSTM input ≤ 4096, hidden ≤ 1024, layer size ≤ 4096, exactly 5 layers), match the expected architecture when one is given, and the dimensions imply exactly the byte length of the block, so an allocation is never larger than the data already read. `ReadArray` rejects any weight that is not finite (`math.isfinite`). `SharedBrainSnapshot.ReadModels` passes the expected architecture: LSTM input `InputVectorizer.VectorSize`, then layer sizes from `BuildCoordSizes` / `BuildExecSizes` of `input + hidden` of the LSTM that was just read; models already built are disposed when a later block is refused. A refused block makes `SharedBrainSnapshot.Deserialize` log the existing «повреждённые данные» error and return `null`.

11. `ChunkStore.TryRead`: an object position or a terrain edit height that is not finite rejects the whole file (`TryRestore` returns `false`, the chunk keeps its procedurally generated state, a warning is logged). `TryRestore` takes an optional `ObjectInfoRegistry`; `MapGenerator` passes the live catalog, and objects whose id is not in it are skipped (counted in one warning), the rest of the chunk is restored. A file larger than the worst legal chunk (`64 + TotalCells × 72` bytes) is refused before `ReadAllBytes`. Secondary cell indices were already range-checked by `ChunkData.TryAddObject`.

14. World directories: `WorldRegistry.TryGetSafeWorldDirectory` accepts a name only if it passes `IsValidWorldName`, is already trimmed, and `Path.GetFullPath` of `worlds/<name>` is a direct child of the full `worlds/` root. `DeleteWorldAsync` and `RenameWorldAsync` use it before `Directory.Delete` / `Directory.Move` (rename also refuses an unsafe old name; delete of an unsafe name still removes the stored entry and the list item, but not a directory). `RefreshWorldListAsync` drops stored ids that fail the check, so a tampered list never reaches the list the UI works with.

23. Per-run Rive state.
    - `RiveRun` (`Terminal/Rive/Core/RiveRun.cs`) holds the operation counter, call depth and abort message of one top-level run; `RiveFrame` holds the scope stack of one script call and a reference to its run.
    - `RiveExecutor` has no mutable fields: every `Execute*` / `Evaluate*` method takes the frame. A top-level `RiveRuntime.ExecuteScriptAsync` creates a new `RiveRun`; a script-to-script call (`RiveRuntime.CallFunctionAsync`) gets a new `RiveFrame` (parameters and own locals only) that shares the caller's `RiveRun`.
    - A budget breach sets `RiveRun.AbortMessage`; every statement list checks it, so a callee that ran out of budget stops the caller too instead of returning `0` into it.
    - Finding 7: the shared counter is this fix; the call depth limit is item 7 above.

24. `pow` uses exponentiation by squaring: at most 31 steps for any `int` exponent, wrapping multiplication (identical results to the old loop). `abs(int.MinValue)` wraps instead of throwing `OverflowException`. Cost of the other built-ins does not depend on argument values; `min`, `max`, `sum`, `avg` are linear in the argument count, which is bounded by the parser (finding 25).

25. Parser limits (`RiveParser`): source length 65536 characters (checked before lexing), 16384 tokens, nesting depth 128 (entered in `ParseStatements`, `ParseExpression` and `ParsePrimary`, so a parenthesis costs 2, a unary operator or a block 1), 256 array literal elements, 16 call arguments and 16 parameters. A breach is a `RiveParseException`; `LoadFile` logs it and leaves the file unloaded. Also fixed: the `>>` check no longer copies the rest of the line per character (quadratic), a number out of `int` range is a parse error instead of `OverflowException`, and an error during tokenizing no longer reads the token list of the previous parse (a `NullReferenceException` on the first parse). Evaluation depth of a long left-deep chain (`1+1+1+…`, up to the token limit) is not bounded by the parser; it is bounded at run time, see finding 7.

26. Input, cancellation and wait budget.
    - `RiveRuntime` owns a `CancellationTokenSource`; every top-level run takes its token (`RiveRun.Token`). `CancelAll()` cancels it and installs a fresh one; `Dispose()` cancels for good. `Terminal` calls `CancelAll()` whenever `WorldRegistry.CurrentWorldId` changes (world unload / switch) and `Dispose()` in `OnDestroy`. The token is passed to `UniTask.Delay` (`wait`), `UniTask.Yield` (every 10 operations) and `InputStreamManager.GetAsync`; a cancelled run ends with `Script cancelled`, and the abort message stops the callers of a cancelled callee.
    - `get` times out after 60 s (`RiveExecutor.MaxInputWaitMs`) and aborts the run. The timeout is a `UniTask.Delay` on the player loop, not a thread-pool timer, so the continuation stays on the main thread.
    - `RiveRun.WaitedMilliseconds` counts every `wait` (requested time, charged before waiting) and every `get` (measured time); the total per run is capped at 300 s (`RiveExecutor.MaxTotalWaitMs`), a `get` never waits longer than the remaining budget.
    - At most 4 top-level runs execute at once (`RiveRuntime.MaxConcurrentRuns`); the next one returns `Too many running scripts`. Terminal commands issued from a script start new top-level runs, so this is also what bounds `~run` recursion through the terminal.

Tests: `Assets/Tests/EditMode/RiveInterpreterSecurityTests.cs`, `Assets/Tests/EditMode/ModelLoaderFuzzTests.cs`, `Assets/Tests/EditMode/WorldRegistryPathTests.cs`, `Assets/Tests/EditMode/WorldAuthorityTests.cs` (only the no-session case: `Terminal` lives in Assembly-CSharp, which the test assembly cannot reference, so the command gate itself is not covered by a test). The interpreter now lives in its own `Rive` assembly (`Terminal/Rive/Core/Rive.asmdef`) so EditMode tests can reference it.

Not verified in the Unity Editor: the container has no Unity; the changed files were not compiled and the tests were not run.

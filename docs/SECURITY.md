# TheRavine Security Guide

Freedom without protection is the freedom of the strongest. Security in the Ravine does not guard the developer's control over the world: it guards equality. No creature, human or AI, local or remote, may gain a hidden advantage, take what belongs to the shared world, or stop the world from living.

This file is a set of principles and techniques. When working on code, the AI checks it in order: first the principles (sections 1–13), then the project registers (sections 14–17), then the "threat → defense" catalog, then the procedure at the end. Known gaps of the current code are listed in [audit/security_audit.md](audit/security_audit.md); a gap found while working is added there, not only fixed.

---

## 1. Threat Model Before Code

Before writing code that accepts anything from outside, name who sends it and what they gain by lying.

- **Remote client**: untrusted. Can send any bytes, any argument values, at any rate, in any order, and pretend to be anyone it can name.
- **Host player**: a player like any other. Running the server grants no privileges in the world (PHILOSOPHY §2).
- **Local player on their own machine**: owns their machine and their single-player saves. Tampering with one's own offline world is not an attack; tampering that reaches other players is.
- **Scripts (Rive) and future mods**: creatures changing the rules. Treated as untrusted code with a budget.
- **Files from strangers**: shared worlds, chunks, neural models, gesture libraries. A file is a message from an unknown sender.
- **Generated text**: LLM output, speech, names, `~print`. Text may contain markup and instructions.
- **Third-party code**: vendored packages and native libraries (`Assets/Packages`, `Assets/LLMUnity`). Runs with full trust, so it is trusted only at a known version (section 16).

A trust boundary is wherever one of these enters the simulation: RPC handler, file reader, script interpreter, text renderer.

## 2. The Server Is the World

There is one world and the server keeps it. A client sends intent, never results.

- ServerRpc arguments are intentions: direction, target cell, token sequence, object id from the inventory. Never values: currency, force, speed, position, health, damage.
- The server computes the consequence with its own rules and its own state, then replicates it.
- Identity comes from the transport: `RpcParams.Receive.SenderClientId` / `OwnerClientId`, never from an argument.
- `[ServerRpc]` keeps `RequireOwnership = true`. `RpcInvokePermission.Everyone` only for requests that do not touch per-object state.
- `NetworkVariable` write permission is `Server`. A `NetworkVariable` written from a client-supplied absolute value is a client-authoritative variable in disguise.
- `NetworkTransform` stays server-authoritative. Client-side prediction is a view, not a fact.
- Client-side cooldowns and delays are UX only. Every limit that matters is enforced again on the server.
- Replication is complete: the consequence of an accepted request reaches every client that observes that part of the world, not only the requester. A reply to the owner is an acknowledgement, not replication.
- A client that joins later receives the current state of what it observes (edits, spawned objects, vitals), not the state the world had at generation.
- A request that only resets state (stop, cancel, release) is still a request: its effect goes through the same server integration as the action it cancels and cannot skip a rate the server enforces (deceleration, cooldown).

## 3. Equality Is a Security Property

The philosophy allows any creature to change the rules. It forbids a creature changing them only for itself.

- A power either exists for everyone through the same server path with the same costs (energy, cooldown, reach), or it is a developer tool that is compiled out of release builds (`#if UNITY_EDITOR || DEVELOPMENT_BUILD`) and refused while a session has remote clients.
- The host's local input goes through the same validation as a remote RPC. "It is the host" is never a reason to skip a check.
- AI agents and humans use the same action API and the same validation. `SpeakServerRpc` is the reference: alive check, length, bit range, cooldown, energy cost, then emit.
- Rule changes are data (PHILOSOPHY §7.3): validated like any other input, applied by the server, visible to all.
- A place where the code knowingly departs from this section is listed in section 15 with its reason. An exemption that is not listed there is a defect.

## 4. Validate at the Boundary, Then Trust

Validation happens once, at the trust boundary. Inside the simulation, data is trusted and hot paths stay clean (OPTIMIZATION §2).

- Floats: `math.isfinite` on every component before any arithmetic. NaN propagates silently through physics and neural networks.
- Continuous intent is clamped: direction normalized, speed `math.saturate`d, force from a server-side curve.
- Discrete values are rejected, not repaired: enum in range, id present in the catalog, level within `ObjectInfo.MaxLevels`, length within the constant, bitmask fits its width.
- Spatial requests are checked against the server's own position of the actor (reach + tolerance + bounded latency compensation).
- Cheap checks first: range and sign in O(1) before any lookup. Argument checks and the cooldown come before component lookups, catalog lookups and service resolution.
- Validation is zero-alloc: no string formatting on the success path; RPC handlers are a server hot path.
- Do not re-validate inside the tick what the boundary already guaranteed.

## 5. Server Clock, Not Client Cadence

A client controls how often it calls. The server must not let frequency become power.

- Never integrate with an interval the client implies (`sendInterval` per call). Measure `dt` on the server since the actor's last accepted request and clamp it.
- Every request kind that changes the world has a per-client budget: cooldown timestamp (as `SpeechBus.TryStartCooldown`) or a token bucket.
- Rate limits are per actor, stored in the actor's SoA slot or component; checking is O(1) and allocation-free.
- Extra requests are dropped silently or counted; they are not logged one by one.
- Every RPC has a budget, including the cheap ones. A handler that is O(1) still costs deserialization and a call per message; "it only stores intent" is a reason for a generous budget, not for none.
- A value the client reports about its own past (how long a button was held, how long a craft took) is checked against server time between the two requests.

## 6. Budget Everything the Adversary Can Spend

Section 1 of OPTIMIZATION budgets the frame for creatures. The same budget must hold against a hostile creature.

- Every quantity an untrusted input controls has an upper bound known in advance: spawned objects per client, bytes per message, allocations per request, log lines per second, file size, script operations, recursion depth, total wait time.
- Spawning on request: at most one instance per owner (check before `Instantiate`).
- Amplification is checked: one cheap request must not cause expensive work (world rebuild, file I/O, broadcast to all clients) without a budget.
- Logs are a resource: refused requests increment counters; a rate-limited summary is written, not a line per event. File writes are not synchronous per message.
- Collections that grow from external events (logged errors, pending requests) are bounded rings, not lists.

## 7. Files Are Messages from Strangers

A save may be shared, edited, truncated, or crafted. Loading never crashes the world and never allocates what the file asks for without checking.

- Header first: magic, format version, expected seed / key, then data. Unknown version → refuse.
- Every length is checked against the remaining stream and against the expected shape before `new`: a layer size from the file is compared with the architecture constant, not used to allocate.
- Floats read from files pass `math.isfinite`; ids are checked against the catalog at load, not at first use.
- Writes are atomic: temp file + move (as `ChunkStore`). A crash mid-write leaves the previous version.
- MemoryPack and `BinaryReader` only for the project's own concrete types; no polymorphic or type-name-driven deserialization of untrusted data.
- Failure means refuse to load that piece, log once, keep the world running.
- Paths: every name that becomes a path is validated (`WorldRegistry.IsValidWorldName`), including names read back from storage. Before `Directory.Delete` / `Move`, `Path.GetFullPath` must stay under the expected root.
- The rules above hold for every reader and writer, not only for chunks: neural models (`NeuralModelStorage`), gesture libraries (`GestureRepository`), scripts and world lists in `PlayerPrefs`. A new writer uses the same temp-file-and-move helper; a direct `FileMode.Create` or `File.WriteAllBytes` on the target file is a defect.
- The file size is checked against a per-format maximum before the file is read into memory. `File.ReadAllBytes` on a file of unknown size is an allocation the file asked for.
- Collection lengths inside MemoryPack payloads are bounded by the format's maximum (gestures per library, points per gesture, scripts per list) after deserialization and before use.

## 8. Scripts Are Creatures with a Budget

Rive scripts are how creatures change the world's rules. The interpreter is a sandbox, and its limits are the world's laws for code.

- The language has no access to files, network, reflection, or Unity objects. New capabilities are added as explicit, validated built-ins or interactors.
- One operation budget per top-level execution, shared by nested calls; never reset on a nested call.
- Recursion depth limit, checked on every script-to-script call.
- Loops count an operation per iteration even with an empty body; loop bounds are overflow-safe (`i <= int.MaxValue` never terminates).
- Arithmetic overflow is defined (wraps or is rejected), never a crash.
- Total wall time and total wait per execution are bounded, not only a single `wait`.
- A script runs with the authority of its author: a terminal command issued from a script goes through the same permission and server path as if the author typed it.
- Variable substitution into commands is token-based, not substring `Replace`.
- Execution state belongs to one execution: operation counter, scope stack and call depth are created per top-level run and passed down. Two scripts running at the same time (one is in `wait` or `get`) never share a counter or a scope.
- Scopes are lexical: a called script sees its parameters and its own variables, not the caller's.
- Built-in functions are inside the budget: a built-in whose cost depends on an argument (`pow`) is O(log n) or charges operations per step.
- The parser has limits checked before and during parsing: source length, token count, nesting depth of expressions and blocks, array literal length. Exceeding one is a parse error, never a stack overflow.
- Waiting for input (`get`) has a timeout, and every wait takes a cancellation token that fires when the world unloads. No script outlives the world it runs in.
- Script output is text from a stranger (section 11): bounded line length, rendered without rich text.

## 9. Secrets and What Is Not a Secret

- A key shipped in the client is obfuscation, not security. `EncryptionUtility` protects saves from accidental corruption and casual editing; it does not protect fairness. Fairness is protected by server authority (§2).
- No real secrets in the repository: API keys, tokens, private server addresses, certificates. Server addresses come from configuration, not from prefabs.
- When a remote party can measure timing, compare MACs with `CryptographicOperations.FixedTimeEquals`.
- Do not invent crypto: AES + HMAC (encrypt-then-MAC) or the platform's DTLS, nothing hand-made.

## 10. Transport and Session

- Connection approval is on: protocol version check, payload size limit, ban list, maximum players.
- `ProtocolVersion` changes with every incompatible change of RPCs or NetworkVariables, so mismatched clients are refused instead of desynced.
- Internet sessions use transport encryption (`UseEncryption` with server certificate). LAN-only builds may skip it consciously.
- `EnsureNetworkVariableLengthSafety` on in development builds, so malformed variable payloads are caught.
- The server listens on the interface it needs, not on every interface by default.
- A disconnecting client's pending requests, spawned objects, and budgets are cleaned up.
- Identity across sessions: `OwnerClientId` identifies a connection, not a player. Anything that must survive a reconnect (inventory ledger, bans, ownership of placed objects) needs a stable player identity verified at connection approval; until it exists, such state is session-only and this is stated where it is stored.

## 11. Text Is Code for Someone

- TextMeshPro rich text parses `<...>` tags. Text from other creatures (names, speech, LLM output, script output) is escaped or rendered with `richText = false`.
- Text sent over the network has a byte limit before it is decoded.
- LLM prompts treat player and world text as data; LLM output is never parsed into commands, ids, or rule changes without the same validation as any other input.
- Log messages built from untrusted input are truncated and stripped of control characters.

## 12. Fail Closed, Keep Living

The world keeps living even without an observer (PHILOSOPHY §3). It must also keep living with a hostile one.

- Invalid input → refuse, reply with a result code (`WorldEditResult`), continue.
- Exceptions in RPC handlers, loaders, and the interpreter are caught at the boundary; one client never takes down the server.
- No path from remote input to `LogCritical` / `StopApplication`.
- On refusal, the client receives the authoritative state (as `PlaceResultClientRpc`), so it cannot drift into an assumed state.

## 13. Verify the Boundary with Malice

- Every RPC handler has tests with NaN, ±Inf, `int.MinValue`, huge values, wrong ids, out-of-range levels, and calls from a non-owner.
- Every deserializer is fuzzed: random bytes, truncated files, huge lengths, wrong version. Expected result: refusal, no exception escaping, no large allocation.
- Every interpreter limit has a test: infinite loop, empty-body loop to `int.MaxValue`, self-recursion, mutual recursion, maximum waits.
- A spam scenario: one client sends each request kind at 100× the normal rate; the server's frame time and log size stay within budget.
- A replication scenario with three peers (host and two clients): an accepted edit by one client is visible to the other, and to a client that joins afterwards.
- A concurrency scenario for scripts: two executions interleaved through `wait`; neither sees the other's variables or resets its budget.
- Tests live in `Assets/Tests/EditMode`. Today that folder covers the simulation, chunk store and command buffer; it has no tests for RPC handlers, the Rive interpreter or loader fuzzing. A change at one of those boundaries adds the first test for it instead of waiting for a suite.
- Static checks that must return nothing, run before a commit that touches networking or storage:
  - `InvokePermission.Everyone` or `RequireOwnership = false` on an RPC outside the registry of section 14;
  - a `ServerRpc` with a `float`, `Vector2` or `Vector3` parameter and no `math.isfinite` in its body;
  - `File.WriteAll*` or `FileMode.Create` on a save path outside the atomic-write helper;
  - `richText = true` on a label that shows text from another creature;
  - a terminal command that changes the world and has no build and session gate (section 3).

---

## 14. RPC Registry

Every RPC of the project is in this table. A new RPC is added here in the same commit; an RPC that is not in the table does not pass review. "Budget" is the server-side limit per client.

| RPC | Arguments (intent) | Server checks | Budget | Result goes to |
|---|---|---|---|---|
| `PlayerController.MoveServerRpc` | direction, input speed, sprint flag | finite; direction clamped to unit length; speed saturated; intent expires after `intentTimeout` | movement budget: `moveRateMargin / sendInterval` per second, burst `moveBurst`, shared with Stop and Jump; excess dropped silently (counted in `refusedEdits`) | all, at `sendInterval` |
| `PlayerController.StopMoveServerRpc` | — | clears the intent; the last direction is kept as the brake direction, speed decays at `Deceleration` | the movement budget of Move | all, at `sendInterval` |
| `PlayerController.JumpServerRpc` | charge 0..1 | finite; saturated; capped by the server time since the last jump (+ `jumpChargeSlack`); grounded, ground consumed | one per ground contact; the movement budget of Move | all |
| `PlayerController.SpeakServerRpc` | tokens, length | length; bit width; cooldown (taken before the energy check); active; alive; energy cost | `SpeechCooldown` | listeners through `SpeechBus` |
| `PlayerController.CraftRequestServerRpc` | recipe index | remote players only; index in `CraftModel.Shared`; ingredients in the ledger | `craftRate` / `craftBurst`; recipe time × `craftTimeTolerance` | nobody (ledger only) |
| `PlayerController.PlaceRequestServerRpc` | cell, object id, level | active; alive; reach + latency compensation; item in the ledger; `WorldEdits.TryPlace` | `editRate` / `editBurst` | owner only |
| `PlayerController.PickUpRequestServerRpc` | cell, level | active; alive; reach + latency compensation; `WorldEdits.TryPickUp` | `editRate` / `editBurst` | owner only |
| `PlayerModelView.RequestCameraServerRpc` | — | owner from transport | one camera per owner | all |
| `PlayerController.UpdateClientPositionClientRpc` | server → clients | — | `sendInterval`, skipped when unchanged | all |
| `PlayerController.PlaceResultClientRpc` | server → client | — | one per request | owner |
| `PlayerController.PickUpResultClientRpc` | server → client | — | one per request | owner |
| `PlayerModelView.NotifyCameraCreatedClientRpc` | server → clients | — | one per camera | all |

Rows that do not meet the principles yet (edit results reaching only the owner) are open gaps, tracked in the audit. The table states what the code does, not what it should do.

## 15. Accepted Exceptions

An exception is a decision, with a reason and a condition under which it ends. Everything else that departs from sections 1–13 is a defect.

| Exception | Where | Reason | Ends when |
|---|---|---|---|
| The host is not checked against the item ledger | `PlayerController.UsesLedger` | The host's inventory already lives in the server process; a check there protects nothing | A dedicated server exists |
| The host process can be patched to bypass every server check | design (listen-server) | Server code runs on a player's machine | A dedicated server exists |
| The remote inventory ledger is lost on disconnect | `PlayerItemLedger` | No stable player identity (section 10) | Identity is verified at connection approval |

## 16. Build and Supply Chain

- Release builds use IL2CPP on every platform. A Mono build decompiles to source; it is acceptable for development only.
- The terminal, debug commands and debug dashboards are compiled out of release builds or gated as section 3 requires. "Not reachable from the UI" is not a gate.
- Vendored packages and native libraries are updated deliberately: the version and the source are recorded when a package is added or changed, and a binary without a known origin is not committed.
- A model or asset downloaded at runtime is a file from a stranger (section 7): fixed host, size limit, checksum when the publisher provides one.
- Logs are data that leaves the machine when a player shares them: no secrets, no full paths of the user profile, no contents of other players' messages beyond what section 11 allows.

## 17. Reporting a Vulnerability

A vulnerability that affects other players (anything from the Critical and High rows of the audit scale) is reported privately to the maintainer, not in a public issue, with the steps to reproduce and the commit it was seen on. The fix lands together with its line in the audit and its test from section 13. Gaps that need a crafted local file or a local-only action may be filed as ordinary issues.

---

## Catalog: Threat → Defense

| Threat | Defense |
|---|---|
| Client sends an absolute value (currency, force, position, speed) | Send intent only; server computes value from its own state and rules |
| Client sends NaN / Inf | `math.isfinite` on every float argument before use |
| Client sends an id or level out of range | Reject against catalog and constants; reply with a refusal code |
| Client names another client in an argument | Use `SenderClientId` / `OwnerClientId`; ignore identity arguments |
| Non-owner calls a state-changing RPC | `RequireOwnership = true`; no `InvokePermission.Everyone` on per-object state |
| RPC spam multiplies speed, regen, or acceleration | Server-measured `dt`, clamped; per-client cooldown or token bucket |
| RPC spam spawns objects | One instance per owner; check before `Instantiate` |
| RPC spam fills logs or disk | Counters + rate-limited summary; async buffered log writes |
| Cheap request triggers expensive work | Budget per client per tick; coalesce into dirty flags |
| Edit far from the actor | Reach check on the server's position + tolerance + bounded latency compensation |
| Host uses a debug command in a shared session | Debug commands compiled out of release; refused while remote clients are connected |
| Crafted file with huge length | Compare with remaining stream and expected shape before allocating |
| Crafted file with NaN weights or heights | `math.isfinite` at load; refuse the block |
| Crafted file with unknown ids | Catalog check at load |
| Truncated save after a crash | Temp file + atomic move |
| Path traversal through a name | Validate the name; `GetFullPath` under root before delete or move |
| Script infinite loop or empty-body loop | Per-iteration operation count; overflow-safe bounds |
| Script recursion | Depth limit; one shared operation budget per top-level execution |
| Script long-running through waits | Total wall-time budget per execution |
| Script gains terminal powers of the local player | Commands from scripts go through the author's permissions and the server path |
| Rich-text tags in names, speech, LLM output | Escape `<` or `richText = false` |
| LLM output used as an instruction | Treat as untrusted text; validate like any other input |
| Mismatched client version | Connection approval with `ProtocolVersion` |
| Eavesdropping on an internet session | Transport encryption (DTLS) |
| Secret or private address in the repository | Configuration outside the repo; rotate if leaked |
| Timing attack on a MAC check | `CryptographicOperations.FixedTimeEquals` |
| Accepted edit is visible only to the requester | Replicate the consequence to every observer; reply to the owner is only an acknowledgement |
| Late joiner sees the generated world, not the edited one | Send current state of observed chunks on join |
| Reset request (stop, cancel) skips a server-enforced rate | Route it through the same integration as the action it cancels |
| Cheap RPC flooded without limit | Per-client budget on every RPC, generous for cheap ones |
| Client lies about elapsed time (charge, craft duration) | Compare with server time between the two requests |
| Built-in function with cost proportional to an argument | O(log n) algorithm or operations charged per step |
| Two scripts share interpreter state | Counter, scopes and depth per top-level execution; lexical scopes |
| Deeply nested or huge script source | Limits on length, tokens, nesting and literal size; parse error instead of stack overflow |
| Script waits for input forever or outlives the world | Timeout on input; cancellation token tied to the world |
| Crash in the middle of saving a model, gesture library or script | Temp file + atomic move for every writer |
| Huge file read into memory before any check | Size limit per format before reading |
| Session-only state treated as a player's property | Stable identity at connection approval; otherwise state it is session-only |
| Decompiled client build | IL2CPP for release; no reliance on client secrecy |
| Debug command present in a release build | Compiled out or gated by build and session |
| Unknown binary in a vendored package | Recorded version and source; no binaries of unknown origin |

---

## Procedure for the AI

1. Determine whether the code is at a trust boundary (section 1): RPC handler, file reader, interpreter, text renderer. If it is not, the data is trusted; do not add checks to hot paths.
2. If it is an RPC: confirm the argument is intent, not a result (section 2); identity from the transport; ownership required; the consequence reaches every observer, not only the owner. Add or update its row in the RPC registry (section 14).
3. Check that no power is given to one creature only (section 3), including the host and debug commands. An exemption is either in the table of section 15 or it is removed.
4. Validate every argument at the boundary: finite, range, catalog, length (section 4); zero-alloc on the success path.
5. Make sure frequency cannot become power: server clock and per-client budget (section 5), for cheap and reset requests too.
6. Bound every resource the input controls: spawns, allocations, logs, file sizes, script operations (section 6).
7. For loaders and writers of any format: size limit before reading, header, lengths before allocation, finite floats, atomic writes, path under root (section 7).
8. For the interpreter: state per execution, shared budget across nested calls, depth limit, per-iteration count, budgeted built-ins, parser limits, bounded total time with cancellation (section 8).
9. On failure: refuse, return the authoritative state, keep the world running (section 12).
10. Add malicious-input tests for the boundary that changed and run the static checks (section 13).
11. Record the outcome: a fixed gap gets its status in the audit; a gap found and not fixed gets a new line there.
12. Write in the style of the surrounding code: no `#region`, no XML summaries, no comments.
13. Priority order on conflict: performance → correctness → extensibility. Boundary checks are O(1) and count as correctness; they are never traded away, they are made cheap.

---

*The world belongs to everyone who lives in it, and therefore to no one alone.*

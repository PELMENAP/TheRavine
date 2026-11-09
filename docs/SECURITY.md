# TheRavine Security Guide

Freedom without protection is the freedom of the strongest. Security in the Ravine does not guard the developer's control over the world: it guards equality. No creature, human or AI, local or remote, may gain a hidden advantage, take what belongs to the shared world, or stop the world from living.

This file is a set of principles and techniques. When working on code, the AI checks it in order: first the principles, then the "threat → defense" catalog, then the procedure at the end. Known gaps of the current code are listed in [audit/security_audit.md](audit/security_audit.md).

---

## 1. Threat Model Before Code

Before writing code that accepts anything from outside, name who sends it and what they gain by lying.

- **Remote client**: untrusted. Can send any bytes, any argument values, at any rate, in any order, and pretend to be anyone it can name.
- **Host player**: a player like any other. Running the server grants no privileges in the world (PHILOSOPHY §2).
- **Local player on their own machine**: owns their machine and their single-player saves. Tampering with one's own offline world is not an attack; tampering that reaches other players is.
- **Scripts (Rive) and future mods**: creatures changing the rules. Treated as untrusted code with a budget.
- **Files from strangers**: shared worlds, chunks, neural models, gesture libraries. A file is a message from an unknown sender.
- **Generated text**: LLM output, speech, names, `~print`. Text may contain markup and instructions.

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

## 3. Equality Is a Security Property

The philosophy allows any creature to change the rules. It forbids a creature changing them only for itself.

- A power either exists for everyone through the same server path with the same costs (energy, cooldown, reach), or it is a developer tool that is compiled out of release builds (`#if UNITY_EDITOR || DEVELOPMENT_BUILD`) and refused while a session has remote clients.
- The host's local input goes through the same validation as a remote RPC. "It is the host" is never a reason to skip a check.
- AI agents and humans use the same action API and the same validation. `SpeakServerRpc` is the reference: alive check, length, bit range, cooldown, energy cost, then emit.
- Rule changes are data (PHILOSOPHY §7.3): validated like any other input, applied by the server, visible to all.

## 4. Validate at the Boundary, Then Trust

Validation happens once, at the trust boundary. Inside the simulation, data is trusted and hot paths stay clean (OPTIMIZATION §2).

- Floats: `math.isfinite` on every component before any arithmetic. NaN propagates silently through physics and neural networks.
- Continuous intent is clamped: direction normalized, speed `math.saturate`d, force from a server-side curve.
- Discrete values are rejected, not repaired: enum in range, id present in the catalog, level within `ObjectInfo.MaxLevels`, length within the constant, bitmask fits its width.
- Spatial requests are checked against the server's own position of the actor (reach + tolerance + bounded latency compensation).
- Cheap checks first: range and sign in O(1) before any lookup.
- Validation is zero-alloc: no string formatting on the success path; RPC handlers are a server hot path.
- Do not re-validate inside the tick what the boundary already guaranteed.

## 5. Server Clock, Not Client Cadence

A client controls how often it calls. The server must not let frequency become power.

- Never integrate with an interval the client implies (`sendInterval` per call). Measure `dt` on the server since the actor's last accepted request and clamp it.
- Every request kind that changes the world has a per-client budget: cooldown timestamp (as `SpeechBus.TryStartCooldown`) or a token bucket.
- Rate limits are per actor, stored in the actor's SoA slot or component; checking is O(1) and allocation-free.
- Extra requests are dropped silently or counted; they are not logged one by one.

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

---

## Procedure for the AI

1. Determine whether the code is at a trust boundary (section 1): RPC handler, file reader, interpreter, text renderer. If it is not, the data is trusted; do not add checks to hot paths.
2. If it is an RPC: confirm the argument is intent, not a result (section 2); identity from the transport; ownership required.
3. Check that no power is given to one creature only (section 3), including the host and debug commands.
4. Validate every argument at the boundary: finite, range, catalog, length (section 4); zero-alloc on the success path.
5. Make sure frequency cannot become power: server clock and per-client budget (section 5).
6. Bound every resource the input controls: spawns, allocations, logs, file sizes, script operations (section 6).
7. For loaders: header, lengths before allocation, finite floats, atomic writes, path under root (section 7).
8. For the interpreter: shared budget, depth limit, per-iteration count, bounded total time (section 8).
9. On failure: refuse, return the authoritative state, keep the world running (section 12).
10. Add malicious-input tests for the boundary that changed (section 13).
11. Write in the style of the surrounding code: no `#region`, no XML summaries, no comments.
12. Priority order on conflict: performance → correctness → extensibility. Boundary checks are O(1) and count as correctness; they are never traded away, they are made cheap.

---

*The world belongs to everyone who lives in it, and therefore to no one alone.*

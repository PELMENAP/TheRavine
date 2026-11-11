# Player — local rules

Map: `docs/map/player-net.md`. Before changing an RPC: `docs/SECURITY.md`; the RPC registry is its section 14.

- The player is one of the creatures. No rule, bonus or shortcut that exists only for the player or only for the host.
- An RPC argument is intent (direction, charge, cell, index), never a value (force, speed, position, currency, health).
- Identity is `OwnerClientId` or the sender id from the transport, never an argument.
- Order in a handler: rate budget, cheap argument checks (`math.isfinite`, range, length), then lookups, then the effect.
- Every ServerRpc has a `RateBudget` or a cooldown. An excess request is dropped and counted, not logged one by one.
- No allocation and no string formatting on the success path of a handler.
- The server integrates movement in `FixedUpdate` with its own clock; nothing is integrated per incoming call.
- A refused edit still gets a result reply, so the client's pending queue stays in order.
- A new or changed RPC updates its row in `docs/SECURITY.md` section 14 in the same commit.
- `PlayerItemLedger` is session-only until a stable player identity exists; do not present it as persistent.
- Unity time (`Time.deltaTime`, `Time.unscaledTime`) is allowed here only where the file already uses it.
- Tuning values are `[SerializeField]` next to the existing fields; name the exception in the report.
- Logic inside a `NetworkBehaviour` cannot be tested in EditMode: extract pure helpers (as `RateBudget`, `JumpChargeLimit`) and test those.

# Virology

Root: `Assets/Scripts/EntitySystem/Virology/`. Assembly: `EntitySystem`. Namespace: `TheRavine.EntityControl.Virology`.
Tests: `Assets/Tests/EditMode/ViralPayloadPoolTests.cs`, `Assets/Tests/EditMode/CorpseBiteTests.cs`.

## Entry points

| Type | File | Role |
|---|---|---|
| `VirologyComponent` | `Assets/Scripts/EntitySystem/Virology/VirologyComponent.cs` | Per-host genome: tape, segments, immunity; `Step` once per host tick |
| `Ribosome` | `Assets/Scripts/EntitySystem/Virology/Ribosome.cs` | Static `Step`: translates the codon under the head into an effect on the host |
| `InfectionService` | `Assets/Scripts/EntitySystem/Virology/InfectionService.cs` | Transmission between hosts: contact, bite, gift, payload; recombination |
| `VirologyRuntime` | `Assets/Scripts/EntitySystem/Virology/VirologyRuntime.cs` | Static tables built once: `Descriptors`, `CellBias`, `Prototype` |
| `TranslationTable` | `Assets/Scripts/EntitySystem/Virology/TranslationTable.cs` | Codon → protein lookup per lineage; LUT built by a Burst job; shared through `TranslationTableRegistry` |
| `ProteinTable` | `Assets/Scripts/EntitySystem/Virology/ProteinTable.cs` | `ProteinAction` enum, `ProteinDescriptor`, energy cost |

`VirologyComponent.cs` is over 500 lines: take the member's line from `docs/map/LARGE_FILES.md`.

## Data

- `Tape` (`Assets/Scripts/EntitySystem/Virology/Tape.cs`): codon array, `Length`, `Head`; `TryInsert`, `Remove`, `Advance`.
- `Segment` (`Assets/Scripts/EntitySystem/Virology/Segment.cs`): a range of the tape with `StrainId`, `LineageId`, `Signature`, `Integrity`, `Tamed`.
- A segment with `StrainId` zero is endogenous (`IsEndogenous`).
- `ViralPayload` is declared in `Assets/Scripts/EntitySystem/AI/Perception/ChunkFoodIndex.cs`; its buffers come from `ViralPayloadPool`.
- `EffectModifiers`, `HostState`, `TranslationResult` are declared in `Ribosome.cs`.
- `CodonEmbedding`: codon ↔ embedding. `StrainCodonTable`, `StrainComposer`: build a strain from a recipe of `ProteinAction`.
- `ViralMutator`: mutation on transmission and strain id.

## Data flow

1. `EntityModel.BeginCycle` reaches `CreatureCore.StepVirology`, which calls `VirologyComponent.Step` for the host.
2. `VirologyComponent.Step` advances the tape and calls `Ribosome.Step` for the codon under the head.
3. `Ribosome.Step` returns a `TranslationResult`; the effect is applied to the host's stats and `EffectModifiers`.
4. After a tick batch `EntityManager.TickAgents` calls `InfectionCoordinator.Spread`, which runs `InfectionService.ProcessSpread` on that batch.
5. Food carries payloads: `ChunkFoodIndex.TryTakePayload`, `DropPayload`.
6. Players use the same component through `CreatureCore`; there is no player-only path.

## Tools and debug

- `VirologyProbe`, `VirologyDebugSeeder`: capture and manual infection, debug only.
- `InfectionView`: read-only view struct for UI.
- Editor: `Assets/Scripts/Editor/StrainCodonTableBuilder.cs`.
- Rules: `Assets/Scripts/EntitySystem/AI/Rules/SimulationRules.Virology.cs`.

## Invariants

- `Ribosome.Step` and `VirologyComponent.Step` are on the agent tick: zero allocation.
- Payload buffers are rented and returned (`ViralPayloadPool.Rent`, `Return`, `Release`); a payload is never copied by reference into two owners.
- A `TranslationTable` is reference-counted (`Acquire`, `Share`, `Release`); a table without a release leaks native memory.

## Hot paths

`docs/map/HOT_PATHS.md`, section Agent tick.

## Open issues

- Corpses, infection payloads and food taboo marks of `ChunkFoodIndex` live only in the session's memory; they are not saved with chunks.

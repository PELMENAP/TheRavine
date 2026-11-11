# Terminal, Rive, gestures

Root: `Assets/Scripts/Terminal/`. Assemblies: `Rive` for `Assets/Scripts/Terminal/Rive/Core/`; everything else is `Assembly-CSharp`.
Namespace: `TheRavine.Base`. Tests: `Assets/Tests/EditMode/RiveInterpreterSecurityTests.cs`.

Read `docs/SECURITY.md` section 8 before changing the interpreter.
`RiveParser.cs` is over 500 lines: take the member's line from `docs/map/LARGE_FILES.md`.

## Entry points

| Type | File | Role |
|---|---|---|
| `Terminal` | `Assets/Scripts/Terminal/Terminal.cs` | Console UI with history; registers commands in `Setup`; `ProcessInput` |
| `CommandManager` | `Assets/Scripts/Terminal/CommandManager.cs` | Commands by name and alias |
| `ICommand`, `IValidatedCommand` | `Assets/Scripts/Terminal/ICommand.cs` | Terminal command contract (not the entity `ICommand`) |
| `CommandContext` | `Assets/Scripts/Terminal/CommandContext.cs` | What a command may reach |
| `RiveRuntime` | `Assets/Scripts/Terminal/Rive/Core/RiveRuntime.cs` | Program registry, run limits, cancellation |
| `RiveParser` | `Assets/Scripts/Terminal/Rive/Core/RiveParser.cs` | Source → AST, with limits |
| `RiveExecutor` | `Assets/Scripts/Terminal/Rive/Core/RiveExecutor.cs` | Executes the AST; no mutable fields |
| `RiveRun`, `RiveFrame` | `Assets/Scripts/Terminal/Rive/Core/RiveRun.cs` | State of one top-level run; scopes of one script call |
| `GestureRecognizer` | `Assets/Scripts/Terminal/PDollar/Scripts/GestureRecognizer.cs` | Drawn gesture → command |
| `GameInitializer` | `Assets/Scripts/Terminal/GameInitializer.cs` | Global start-up; described in `docs/map/core.md` |

## Commands

- Base: `HelpCommand`, `ClearCommand`, `PrintCommand` in `Assets/Scripts/Terminal/BaseCommands.cs`.
- Debug, compiled only in the editor and development builds: `TeleportCommand`, `SetValueCommand` (`SetPlayerCommand.cs`), `DebugCommand`.
- Debug commands call `DebugCommandGate.TryEnter`, which refuses while `WorldAuthority.HasRemotePeers`.
- `RotateCommand`: `Assets/Scripts/Terminal/GeneratorCommands.cs`.
- Script commands: `Assets/Scripts/Terminal/Rive/RiveRedactorCommands.cs` (eight classes), `Assets/Scripts/Terminal/Rive/InteractorCommands.cs`.
- Script files: `ScriptFileManager`; editor UI: `ScriptEditorPresenter`, `RiveInputUI`.

## Rive

- Values are `int` and `int` arrays. No access to files, network, reflection or Unity objects.
- Constructs: `if` / `else` / `end`, `for`, `wait`, `log`, `get`, `send`; a line starting with `~` runs a terminal command.
- AST node types are all in `Assets/Scripts/Terminal/Rive/Core/RiveHelpStructs.cs`.
- Built-ins: `Assets/Scripts/Terminal/Rive/Core/RiveBuiltInFunctions.cs`. Puzzles (`InteractorRegistry`): `RiveInteractors.cs` next to it.
- Input for `get`: `InputStreamManager`.

## Limits (names of the constants)

- Parser: `MaxSourceLength`, `MaxTokens`, `MaxNestingDepth`, `MaxArrayLiteralLength`, `MaxCallArguments`.
- Executor: `MAX_OPERATIONS`, `MaxCallDepth`, `MaxStackDepth`, `MaxInputWaitMs`, `MaxTotalWaitMs`.
- Runtime: `MaxConcurrentRuns`.

## Data flow

1. `Terminal.ProcessInput` validates and runs a typed command; scripts reach commands through `ExecuteTerminalCommandAsync`.
2. `RiveRuntime.ExecuteScriptAsync` creates a `RiveRun`; a script-to-script call gets a new `RiveFrame` sharing that run.
3. A budget breach sets `RiveRun.AbortMessage`; every statement list checks it, so callers stop too.
4. `RiveRuntime.CancelAll` fires when the current world changes; `Dispose` when the terminal is destroyed.

## Gestures (`Assets/Scripts/Terminal/PDollar/Scripts/`)

- `PointCloudRecognizerPlus` and `Geometry` in `PointCloudRecognizer.cs`; `GestureRepository` loads and saves the library with limits.
- `GestureCommandBus` dispatches a recognised gesture.

## Invariants

- One operation budget per top-level run, shared by nested calls, never reset.
- Execution state belongs to a run; two runs never share a counter or a scope.
- A script acts with its author's authority: its terminal commands pass the same `Validate` as typed ones.
- Script output is text from a stranger: bounded length, no rich text.

## Hot paths

None. The interpreter is budgeted, not optimised.

## Open issues

- `Terminal` and `GestureRepository` are in `Assembly-CSharp`, so the command gate and gesture validation have no EditMode test.

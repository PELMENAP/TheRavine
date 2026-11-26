# Inventory, craft, items, UI

Root: `Assets/Scripts/Model/InventoryModel/`, `Assets/Scripts/UI/`, `Assets/Scripts/ScriptableData/Item/`.
Assemblies: `Inventory`, `UIInventory`, `Info`. Tests: `Assets/Tests/EditMode/CraftModelTests.cs`.

## Entry points

| Type | File | Role |
|---|---|---|
| `InventoryModel` | `Assets/Scripts/Model/InventoryModel/InventoryModel.cs` | Pure model: slots, stacks |
| `EventDrivenInventoryProxy` | `Assets/Scripts/Model/InventoryModel/EventDrivenInventoryProxy.cs` | Wrapper that raises added / removed / changed events |
| `CraftModel` | `Assets/Scripts/Model/InventoryModel/CraftModel.cs` | Recipes (`CraftRecipe`), matching; shared catalogue `CraftModel.Shared` |
| `UIInventory` | `Assets/Scripts/UI/Inventory/UIInventory.cs` | Inventory view (`ISetAble`); listens to the player's place and pick-up events |
| `WorldEditFeedback` | `Assets/Scripts/UI/Inventory/WorldEditFeedback.cs` | Local human only: refusal marker with a short reason and `UISoundSystem.PlayDeny`; pick-up icon flight to the slot |
| `CraftService` | `Assets/Scripts/UI/CraftService.cs` | Checks recipes, drives the progress bar, reports a remote player's craft to the server |
| `CraftPresenter` | `Assets/Scripts/UI/CraftPresenter.cs` | Presenter of the craft panel |
| `UIDragger` | `Assets/Scripts/UI/Inventory/UIDragger.cs` | Drag and drop, mouse and touch |
| `LLMItemDescriptionService` | `Assets/Scripts/UI/Inventory/Extensions/LLMItemDescriptionService.cs` | Item descriptions from the local LLM, debounced |
| `ItemDescriptionRegistry` | `Assets/Scripts/ScriptableData/Item/Descriprtion/ItemDescriptionRegistry.cs` | Cache of generated descriptions |
| `InfoManager` | `Assets/Scripts/ScriptableData/Item/InfoManager.cs` | Access to item data assets |

## Data flow

1. The player's `EventBusComponent` raises place and pick-up events; `UIInventory` updates the model through the proxy.
2. `CraftService` matches the craft grid against `CraftModel.Shared`; recipe indices are the same on client and server.
3. For a remote player the craft is also sent with `PlayerEntity.RequestCraft`; the server validates it against `PlayerItemLedger`.
4. The server side of placement and pick-up is in `docs/map/player-net.md`.

## Item data

- Item types and slots: `InventoryItemInfo.cs`, `InventorySlot.cs`, `SlotKind.cs`, `InventoryItemState.cs` in the model folder.
- Item assets: `Assets/Scripts/ScriptableData/Item/ItemInfo/`. Classes: `Assets/Scripts/ScriptableData/Item/DataItems.cs`, `ItemClass.cs`.
- Description, title and icon live in the `InventoryItemInfo` asset; an item without own behaviour needs no class.
- `InventoryTypeCache.Resolve` maps `InventoryItemInfo.id` to a class of that name, else to `DefaultItem` (`Assets/Scripts/ScriptableData/Item/DefaultItem.cs`).
- A class of its own is needed only for behaviour (`ItemClass.TryUse`); its name must equal the item id.
- Editor tools: `Assets/Scripts/Editor/ItemPipelineValidator.cs` (menu Tools/Ravine/Items, runs before play and build), `ItemWizardWindow.cs`.
- Validate And Fix fills safe gaps: registry, DataItems, ids, titles, stack size; missing prefab or icon gets a placeholder from `ItemPipelineStubs`.
- `LegacyObjectInfoMigration` restores prefab and item of ObjectInfo assets saved with the old field names (`_prefab`, `_iteminfo`).
- A spawn profile missing from MapGenerator is only reported: removing it from the list is how a profile is switched off.
- The prompt for descriptions is built by `Assets/Scripts/UI/Inventory/Extensions/ItemPromptBuilder.cs` from item and player data only.

## Other UI

- Settings and world list: `Assets/Scripts/UI/Settings/` — `SettingsUI`, `SettingsCarousel`, `WorldManagerUI`, `WorldSettingsUI`, `WorldItemUI`.
- Pause: `Assets/Scripts/GameSceneManagerSystem/Menu/PauseUI.cs`.
- Console input focus: `Assets/Scripts/UI/Console/AlwaysFocusedInput.cs`; the console itself is `docs/map/terminal.md`.

## Invariants

- The inventory model has no Unity or UI dependency.
- Item identity is `InventoryItemInfo.id`, not the C# type: stacking, `HasItem`, `Remove` and saves compare ids; ids are never renamed after release.
- `InfoManager` keys `DataItems` by id; an asset missing from `DataItems` cannot be loaded from a save.
- Recipe indices are part of the client / server contract: reordering `CraftModel.Shared` breaks remote crafting.
- LLM output is untrusted text: never parsed into ids or commands (`docs/SECURITY.md` section 11).
- UI is not a hot path; clarity over speed (`docs/OPTIMIZATION.md` section 17).

## Hot paths

None.

## Open issues

- Whether LLM descriptions are rendered with rich text was not traced (`docs/audit/security_audit.md` section 3).

## Skill graph view

- Runtime UI Toolkit (the only one in the project, the rest is uGUI): `Assets/Scripts/UI/SkillGraph/`, assembly `UIInventory`.
  Style: `Assets/Resources/UI/SkillGraph.uss` over the tokens of `Assets/Resources/UI/DesignTokens.uss`, loaded first; rules: `docs/UI_STYLE.md`.
- `SkillGraphPanel`: MonoBehaviour created and destroyed by the `~skills` command; `UIDocument` with a fallback `PanelSettings` when none is assigned; the tree is built in C#.
- `SkillGraphCanvas`: pan by drag with pointer capture, zoom by wheel, edges in `generateVisualContent` (`Painter2D`), nearest-edge picking through `SkillGraphGeometry`.
- `SkillNodeElement`: pooled node, progress ring by `Painter2D.Arc`, state classes `skill-node--open`, `--available`, `--silhouette`, `--island`, `--pruned`.
- `SkillGraphMenu`: context menu opened by a click on a node or an edge (build, restore, prune, with the cost and the reason a command is refused);
  `SkillGraphText`: effect and XP-source lines of the card.
- Node labels show the skill name; the corner block shows how many links can be added and the peak sum of the next edge point.
- `SkillGraphPalette`: pixel sizes and the `Painter2D` colours, a mirror of the graph tokens; no simulation constant is repeated there.
- Data: `SkillGraphQuery.Fill(ISkillGraphSource, SkillGraphSnapshot)` in `Assets/Scripts/EntitySystem/AI/Skills/`; the fog rule lives there, hidden vertices and edges are never written.
- Commands go through `ISkillGraphCommands`, implemented by `PlayerSkillGraph` (host: direct, client: `SkillGraphCommandServerRpc`).
- Refresh: a scheduled item every 250 ms compares `Revision`, `StateVersion` and a level checksum; closing the panel destroys it, so nothing runs while it is closed.
- Open: key `I` and a corner button (`SkillGraphLauncher`, created once by `Terminal.Setup`, DontDestroyOnLoad, the key is a runtime `InputAction`, not part of `InputAction.inputactions`),
  or the `~skills` command.
- The key and the button act only while the Gameplay map is active and a local player graph exists; opening disables all maps and enables UI, closing returns to Gameplay.
- `PanelSettings` fallback carries an empty `ThemeStyleSheet`; controls are styled in the USS file (class `skill-button`).

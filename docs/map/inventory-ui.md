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
- The prompt for descriptions is built by `Assets/Scripts/UI/Inventory/Extensions/ItemPromptBuilder.cs` from item and player data only.

## Other UI

- Settings and world list: `Assets/Scripts/UI/Settings/` — `SettingsUI`, `SettingsCarousel`, `WorldManagerUI`, `WorldSettingsUI`, `WorldItemUI`.
- Pause: `Assets/Scripts/GameSceneManagerSystem/Menu/PauseUI.cs`.
- Console input focus: `Assets/Scripts/UI/Console/AlwaysFocusedInput.cs`; the console itself is `docs/map/terminal.md`.

## Invariants

- The inventory model has no Unity or UI dependency.
- Recipe indices are part of the client / server contract: reordering `CraftModel.Shared` breaks remote crafting.
- LLM output is untrusted text: never parsed into ids or commands (`docs/SECURITY.md` section 11).
- UI is not a hot path; clarity over speed (`docs/OPTIMIZATION.md` section 17).

## Hot paths

None.

## Open issues

- Whether LLM descriptions are rendered with rich text was not traced (`docs/audit/security_audit.md` section 3).

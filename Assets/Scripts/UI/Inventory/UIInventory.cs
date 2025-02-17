using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Collections.Generic;

using TheRavine.Base;
using TheRavine.InventoryElements;
using TheRavine.Generator;
using TheRavine.ObjectControl;
using TheRavine.Extensions;
using TheRavine.Services;
using TheRavine.EntityControl;
using TheRavine.Events;
using TheRavine.Security;

namespace TheRavine.Inventory
{
    public class UIInventory : MonoBehaviour, ISetAble
    {
        [SerializeField] private GameObject grid, mobileInput;
        [SerializeField] private bool filling;
        [SerializeField] private UIInventorySlot[] activeCells;
        [SerializeField] private PlayerInput input;
        [SerializeField] private RectTransform cell;
        [SerializeField] private InputActionReference enter, quit, digitAction;
        [SerializeField] private CraftService craftService;
        [SerializeField] private DataItems dataItems;
        private byte activeCell = 1;
        public InventoryWithSlots inventory => tester.inventory;
        public InfoManager infoManager => tester.infoManager;
        public bool HasItem(string title) => tester.HasItem(title);
        private UIInventoryTester tester;
        private PlayerEntity playerData;
        private MapGenerator generator;
        private ObjectSystem objectSystem;
        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            playerData = locator.GetService<PlayerModelView>().playerEntity;
            generator = locator.GetService<MapGenerator>();
            objectSystem = locator.GetService<ObjectSystem>();
            var uiSlot = GetComponentsInChildren<UIInventorySlot>();
            var slotList = new List<UIInventorySlot>();
            slotList.AddRange(uiSlot);
            slotList.AddRange(activeCells);
            tester = new UIInventoryTester(slotList.ToArray(), dataItems);

            if(DataStorage.cycleCount == 0) tester.FillSlots(filling);
            else 
            {
                var loadedData = SaveLoad.LoadEncryptedData<SerializableList<SerializableInventorySlot>>(nameof(SerializableList<SerializableInventorySlot>));
                tester.SetDataFromSerializableList(loadedData);
            }

            craftService.SetUp(null, locator);

            grid.SetActive(false);

            enter.action.performed += ChangeInventoryState;
            quit.action.performed += ChangeInventoryState;

            inventory.OnInventoryStateChangedEventOnce += OnInventoryStateChanged;

            EventBusByName playerEventBus = playerData.GetEntityComponent<EventBusComponent>().EventBus;
            playerEventBus.Subscribe<Vector2Int>(nameof(PlaceEvent), PlaceObjectEvent);
            playerEventBus.Subscribe<Vector2Int>(nameof(PickUpEvent), PickUpEvent);

            digitAction.action.performed += SetActionCell;

            callback?.Invoke();
        }

        private void SetActionCell(InputAction.CallbackContext c)
        {
            SetActiveCell((byte)c.ReadValue<float>());
        }

        private void SetActiveCell(byte index)
        {
            activeCell = index;
            cell.anchoredPosition = new Vector2(55 + 120 * (index - 1), -50);
        }


        private void PlaceObjectEvent(Vector2Int position)
        {
            IInventorySlot slot = activeCells[activeCell - 1].slot;
            if (slot.isEmpty) return;
            IInventoryItem item = activeCells[activeCell - 1]._uiInventoryItem.item;
            if(!item.info.isPlaceable) return;
            if (objectSystem.TryAddToGlobal(position, item.info.prefab.GetInstanceID(), 1, InstanceType.Inter))
            {
                item.state.amount--;
                if (slot.amount <= 0) slot.Clear();
                activeCells[activeCell - 1].Refresh();
                generator.TryToAddPositionToChunk(position);
            }
            generator.ExtraUpdate();
        }
        private void OnInventoryStateChanged(object sender)
        {
            craftService.OnInventoryCraftCheck(sender);
        }

        private void PickUpEvent(Vector2Int position)
        {
            ObjectInstInfo objectInstInfo = objectSystem.GetGlobalObjectInstInfo(position);
            ObjectInfo data = objectSystem.GetGlobalObjectInfo(position);
            if (data == null) return;
            if (inventory.TryToAdd(this, infoManager.GetInventoryItemByInfo(data.iteminfo.id, data.iteminfo, objectInstInfo.amount)))
            {
                objectSystem.RemoveFromGlobal(position);
                SpreadPattern pattern = data.pickUpPattern;
                if (pattern != null)
                {
                    objectSystem.TryAddToGlobal(position, pattern.main.prefab.GetInstanceID(), pattern.main.amount, pattern.main.iType, (position.x + position.y) % 2 == 0);
                    if (pattern.other.Length != 0)
                    {
                        for (byte i = 0; i < pattern.other.Length; i++)
                        {
                            Vector2Int newPos = Extension.GetRandomPointAround(position, pattern.factor);
                            objectSystem.TryAddToGlobal(newPos, pattern.other[i].prefab.GetInstanceID(), pattern.other[i].amount, pattern.other[i].iType, newPos.x < position.x);
                        }
                    }
                }
            }
            generator.ExtraUpdate();
        }

        private bool isactive = false;
        private void ChangeInventoryState(InputAction.CallbackContext context)
        {
            OnInventoryStateChanged(this);
            isactive = !isactive;
            if (isactive)
            {
                if (input.currentActionMap.name != "Gameplay")
                {
                    isactive = !isactive;
                    return;
                }
                playerData.SetBehaviourSit();
                input.SwitchCurrentActionMap("Inventory");
            }
            else
            {
                playerData.SetBehaviourIdle();
                input.SwitchCurrentActionMap("Gameplay");
            }
            if(Settings._controlType == ControlType.Mobile) mobileInput.SetActive(!isactive);
            grid.SetActive(isactive);
        }

        public void ChangeInventoryState()
        {
            OnInventoryStateChanged(this);
            isactive = !isactive;
            if (isactive)
            {
                if (input.currentActionMap.name != "Gameplay")
                {
                    isactive = !isactive;
                    return;
                }
                playerData.SetBehaviourSit();
                input.SwitchCurrentActionMap("Inventory");
            }
            else
            {
                playerData.SetBehaviourIdle();
                input.SwitchCurrentActionMap("Gameplay");
            }
            if(Settings._controlType == ControlType.Mobile) mobileInput.SetActive(!isactive);
            grid.SetActive(isactive);
        }
        public void BreakUp(ISetAble.Callback callback)
        {
            var dataToSave = inventory.GetSerializableList();
            SaveLoad.SaveEncryptedData(nameof(SerializableList<SerializableInventorySlot>), dataToSave);

            inventory.OnInventoryStateChangedEvent -= OnInventoryStateChanged;

            enter.action.performed -= ChangeInventoryState;
            quit.action.performed -= ChangeInventoryState;

            digitAction.action.performed -= SetActionCell;

            callback?.Invoke();
        }
    }
}
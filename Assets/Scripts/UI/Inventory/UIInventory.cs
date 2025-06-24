using UnityEngine;
using System.Collections.Generic;

using TheRavine.Base;
using TheRavine.InventoryElements;
using TheRavine.Generator;
using TheRavine.ObjectControl;
using TheRavine.Extensions;
using TheRavine.EntityControl;
using TheRavine.Events;
using Cysharp.Threading.Tasks;

namespace TheRavine.Inventory
{
    public class UIInventory : MonoBehaviour, ISetAble
    {
        [SerializeField] private bool filling;
        [SerializeField] private UIInventorySlot[] activeCells;
        [SerializeField] private RectTransform cell;
        [SerializeField] private DataItems dataItems;
        [SerializeField] private CraftService craftService;
        [SerializeField] private UIDragger uIDragger;
        [SerializeField] private InventoryInputHandler inventoryInputHandler;
        private InventoryTester tester;
        private EventDrivenInventoryProxy eventDrivenInventoryProxy;
        private MapGenerator generator;
        private ObjectSystem objectSystem;
        private InfoManager infoManager;
        private EncryptedPlayerPrefsStorage encryptedPlayerPrefsStorage;
        private IWorldManager worldManager;
        public bool HasItem(InventoryItemInfo info) => eventDrivenInventoryProxy.HasItem(infoManager.GetItemType(info));
        public void SetUp(ISetAble.Callback callback)
        {
            worldManager = ServiceLocator.GetService<IWorldManager>();
            generator = ServiceLocator.GetMonoService<MapGenerator>();
            objectSystem = ServiceLocator.GetMonoService<ObjectSystem>();
            encryptedPlayerPrefsStorage = new EncryptedPlayerPrefsStorage();

            var playerData = ServiceLocator.GetMonoService<PlayerModelView>().playerEntity;
            var gameSettings = ServiceLocator.GetService<ISettingsModel>().GameSettings.CurrentValue;
            inventoryInputHandler.RegisterInput(playerData, gameSettings);

            var uiSlot = GetComponentsInChildren<UIInventorySlot>();
            var slotList = new List<UIInventorySlot>();
            slotList.AddRange(uiSlot);
            slotList.AddRange(activeCells);

            var inventoryModel = new InventoryModel(slotList.Count);
            eventDrivenInventoryProxy = new EventDrivenInventoryProxy(inventoryModel);

            infoManager = new InfoManager(dataItems);

            tester = new InventoryTester(slotList.ToArray(), infoManager, eventDrivenInventoryProxy);

            OnInventoryDataLoaded().Forget();

            uIDragger.SetUp(inventoryModel);
            craftService.SetUp(infoManager, inventoryModel);

            eventDrivenInventoryProxy.OnInventoryStateChangedEventOnce += OnInventoryStateChanged;

            EventBusByName playerEventBus = playerData.GetEntityComponent<EventBusComponent>().EventBus;
            playerEventBus.Subscribe<Vector2Int>(nameof(PlaceEvent), PlaceObjectEvent);
            playerEventBus.Subscribe<Vector2Int>(nameof(PickUpEvent), PickUpEvent);

            callback?.Invoke();
        }

        private async UniTaskVoid OnInventoryDataLoaded()
        {
            WorldInfo worldInfo = await worldManager.GetWorldInfoAsync(worldManager.CurrentWorldName);
            if (worldInfo.CycleCount == 0) tester.FillSlots(filling);
            else
            {
                var loadedData = await encryptedPlayerPrefsStorage.LoadAsync<SerializableList<SerializableInventorySlot>>(nameof(SerializableList<SerializableInventorySlot>));
                tester.SetDataFromSerializableList(loadedData);
            }
        }

        private void PlaceObjectEvent(Vector2Int position)
        {
            IInventorySlot slot = activeCells[inventoryInputHandler.ActiveCellIndex - 1].slot;
            if (slot.isEmpty) return;
            IInventoryItem item = activeCells[inventoryInputHandler.ActiveCellIndex - 1]._uiInventoryItem.item;
            if(!item.info.isPlaceable) return;
            if (objectSystem.TryAddToGlobal(position, item.info.prefab.GetInstanceID(), 1, InstanceType.Inter))
            {
                item.state.amount--;
                if (slot.amount <= 0) slot.Clear();
                activeCells[inventoryInputHandler.ActiveCellIndex - 1].Refresh();
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

            IInventoryItem item = infoManager.GetInventoryItemByInfo(data.iteminfo.id, data.iteminfo, objectInstInfo.amount);
            if (eventDrivenInventoryProxy.TryToAdd(this, item))
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
        public void BreakUp(ISetAble.Callback callback)
        {
            var dataToSave = tester.Serialize();
            encryptedPlayerPrefsStorage.SaveAsync(nameof(SerializableList<SerializableInventorySlot>), dataToSave).Forget();

            uIDragger.BreakUp();
            craftService.BreakUp();

            eventDrivenInventoryProxy.Dispose();

            inventoryInputHandler.UnregisterInput();

            callback?.Invoke();
        }
    }
}
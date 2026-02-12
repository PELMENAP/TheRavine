using TheRavine.Base;
using TheRavine.InventoryElements;
using TheRavine.Generator;
using TheRavine.ObjectControl;
using TheRavine.Extensions;
using TheRavine.EntityControl;
using TheRavine.Events;

using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;

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
        public bool HasItem(InventoryItemInfo info) => eventDrivenInventoryProxy.HasItem(infoManager.GetItemType(info));
        public void SetUp(ISetAble.Callback callback)
        {
            ServiceLocator.Services.Register(this);
            
            generator = ServiceLocator.GetService<MapGenerator>();
            objectSystem = ServiceLocator.GetService<ObjectSystem>();
            encryptedPlayerPrefsStorage = new EncryptedPlayerPrefsStorage();

            var playerData = ServiceLocator.GetService<PlayerModelView>().PlayerEntity;
            var gameSettings = ServiceLocator.GetService<SettingsMediator>().Global.CurrentValue;

            ServiceLocator.GetService<AutosaveSystem>().AddSaveAction(SaveInventoryData);

            var uiSlot = GetComponentsInChildren<UIInventorySlot>();
            var slotList = new List<UIInventorySlot>();
            slotList.AddRange(uiSlot);
            slotList.AddRange(activeCells);

            var inventoryModel = new InventoryModel(slotList.Count);
            eventDrivenInventoryProxy = new EventDrivenInventoryProxy(inventoryModel);

            infoManager = new InfoManager(dataItems);
            tester = new InventoryTester(slotList.ToArray(), infoManager, eventDrivenInventoryProxy);
            uIDragger.SetUp(inventoryModel);
            craftService.SetUp(infoManager, inventoryModel);

            inventoryInputHandler.RegisterInput(playerData, gameSettings);
            eventDrivenInventoryProxy.OnInventoryStateChangedEventOnce += OnInventoryStateChanged;
            OnInventoryDataLoaded(playerData, ServiceLocator.GetService<WorldRegistry>()).Forget();

            callback?.Invoke();
        }

        private async UniTaskVoid OnInventoryDataLoaded(PlayerEntity playerData, WorldRegistry worldRegistry)
        {
            (WorldState worldData, WorldConfiguration worldConfiguration) = await worldRegistry.LoadCurrentWorldData();
            
            if (worldData.IsDefault() || worldConfiguration == null) return;
            if (worldData.cycleCount == 0)
            {
                tester.FillSlots(filling);
            }
            else
            {
                var loadedData = await encryptedPlayerPrefsStorage.LoadAsync<SerializableList<SerializableInventorySlot>>(nameof(SerializableList<SerializableInventorySlot>));
                tester.SetDataFromSerializableList(loadedData);
            }

            EventBus playerEventBus = playerData.GetEntityComponent<EventBusComponent>().EventBus;
            playerEventBus.Subscribe<PlaceEvent>(PlaceObjectEvent);
            playerEventBus.Subscribe<PickUpEvent>(PickUpEvent);
        }

        private void PlaceObjectEvent(AEntity entity, PlaceEvent e)
        {
            IInventorySlot slot = activeCells[inventoryInputHandler.ActiveCellIndex - 1].slot;
            if (slot.isEmpty) return;
            IInventoryItem item = activeCells[inventoryInputHandler.ActiveCellIndex - 1]._uiInventoryItem.item;
            if(!item.info.isPlaceable) return;
            if (objectSystem.TryAddToGlobal(e.Position, generator.GetRealPosition(e.Position), item.info.prefab.GetInstanceID(), 1, InstanceType.Interactable))
            {
                item.state.amount--;
                if (slot.amount <= 0) slot.Clear();
                activeCells[inventoryInputHandler.ActiveCellIndex - 1].Refresh();
                generator.TryToAddPositionToChunk(e.Position);
            }
            generator.ExtraUpdate();
        }
        private void OnInventoryStateChanged(object sender)
        {
            craftService.OnInventoryCraftCheck(sender);
        }

        private void PickUpEvent(AEntity entity, PickUpEvent e)
        {
            ObjectInstInfo objectInstInfo = objectSystem.GetGlobalObjectInstInfo(e.Position);
            ObjectInfo data = objectSystem.GetGlobalObjectInfo(e.Position);
            if (data == null) return;

            IInventoryItem item = infoManager.GetInventoryItemByInfo(data.InventoryItemInfo.id, data.InventoryItemInfo, objectInstInfo.Amount);
            if (eventDrivenInventoryProxy.TryToAdd(this, item))
            {
                objectSystem.RemoveFromGlobal(e.Position);
                SpreadPattern pattern = data.OnPickUpPattern;
                if (pattern != null)
                {
                    objectSystem.TryAddToGlobal(e.Position, generator.GetRealPosition(e.Position), pattern.main.ObjectPrefab.GetInstanceID(), pattern.main.DefaultAmount, pattern.main.InstanceType);
                    if (pattern.other.Length != 0)
                    {
                        for (byte i = 0; i < pattern.other.Length; i++)
                        {
                            Vector2Int newPos = Extension.GetRandomPointAround(e.Position, pattern.factor);
                            objectSystem.TryAddToGlobal(newPos, generator.GetRealPosition(newPos),  pattern.other[i].ObjectPrefab.GetInstanceID(), pattern.other[i].DefaultAmount, pattern.other[i].InstanceType);
                        }
                    }
                }
            }
            generator.ExtraUpdate();
        }

        public async UniTask<bool> SaveInventoryData()
        {
            try
            {
                var dataToSave = tester.Serialize();
                await encryptedPlayerPrefsStorage.SaveAsync(nameof(SerializableList<SerializableInventorySlot>), dataToSave);
                
                Debug.Log($"Состояние инвентаря сохранено");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.Log($"Ошибка сохранения: {ex.Message}");
                return false;
            }
        }
        public void BreakUp(ISetAble.Callback callback)
        {
            var dataToSave = tester.Serialize();
            encryptedPlayerPrefsStorage.SaveAsync(nameof(SerializableList<SerializableInventorySlot>), dataToSave).Forget();
            tester.Dispose();

            uIDragger.BreakUp();
            craftService.BreakUp();

            eventDrivenInventoryProxy.Dispose();

            inventoryInputHandler.UnregisterInput();

            callback?.Invoke();
        }
    }
}
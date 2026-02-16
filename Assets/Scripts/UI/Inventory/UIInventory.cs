using TheRavine.Base;
using TheRavine.InventoryElements;
using TheRavine.Generator;
using TheRavine.ObjectControl;
using TheRavine.Extensions;
using TheRavine.EntityControl;
using TheRavine.Events;

using UnityEngine;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace TheRavine.Inventory
{
    public class UIInventory : MonoBehaviour, ISetAble
    {
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
        private WorldRegistry worldRegistry;
        private RavineLogger logger;
        public bool HasItem(InventoryItemInfo info) => 
            eventDrivenInventoryProxy.HasItem(infoManager.GetItemType(info));

        public void SetUp(ISetAble.Callback callback)
        {            
            logger = ServiceLocator.GetService<RavineLogger>();
            generator = ServiceLocator.GetService<MapGenerator>();
            objectSystem = ServiceLocator.GetService<ObjectSystem>();
            worldRegistry = ServiceLocator.GetService<WorldRegistry>();

            var uiSlot = GetComponentsInChildren<UIInventorySlot>();
            var slotList = new List<UIInventorySlot>();
            slotList.AddRange(uiSlot);
            slotList.AddRange(activeCells);

            eventDrivenInventoryProxy = new EventDrivenInventoryProxy(slotList.Count);
            ServiceLocator.Services.Register(eventDrivenInventoryProxy);

            infoManager = new InfoManager(dataItems);
            tester = new InventoryTester(slotList.ToArray(), infoManager, eventDrivenInventoryProxy);
            uIDragger.SetUp(eventDrivenInventoryProxy);
            craftService.SetUp(infoManager, eventDrivenInventoryProxy);

            var playerData = ServiceLocator.GetService<PlayerModelView>().PlayerEntity;
            var gameSettings = ServiceLocator.GetService<GlobalSettingsController>().Settings.CurrentValue;
            inventoryInputHandler.RegisterInput(playerData, gameSettings);

            eventDrivenInventoryProxy.OnInventoryStateChangedEventOnce += OnInventoryStateChanged;
            
            LoadInventoryDataAsync().Forget();

            callback?.Invoke();
        }

        private async UniTaskVoid LoadInventoryDataAsync()
        {
            try
            {
                var state = worldRegistry.GetCurrentState();
                logger.LogInfo($"[UIInventory] Загрузка инвентаря, cycleCount: {state.cycleCount}");
                
                if (state.cycleCount < 1)
                {
                    tester.FillSlots(true);
                    
                    worldRegistry.UpdateState(s => 
                    {
                        s.cycleCount++;
                        s.inventory = tester.Serialize();
                    });
                    
                    await worldRegistry.SaveCurrentWorldAsync();
                    logger.LogInfo("[UIInventory] Начальное состояние сохранено");
                }
                else
                {
                    if (state.inventory != null && state.inventory.Length > 0)
                    {
                        tester.SetDataFromSerializableList(state.inventory);
                    }
                }

                var playerData = ServiceLocator.GetService<PlayerModelView>().PlayerEntity;
                EventBus playerEventBus = playerData.GetEntityComponent<EventBusComponent>().EventBus;
                playerEventBus.Subscribe<PlaceEvent>(PlaceObjectEvent);
                playerEventBus.Subscribe<PickUpEvent>(PickUpEvent);
            }
            catch (Exception ex)
            {
                logger.LogError($"[UIInventory] Ошибка загрузки инвентаря: {ex.Message}");
            }
        }

        private void PlaceObjectEvent(AEntity entity, PlaceEvent e)
        {
            InventorySlot slot = activeCells[inventoryInputHandler.ActiveCellIndex - 1].slot;
            if (slot.isEmpty) return;

            IInventoryItem item = activeCells[inventoryInputHandler.ActiveCellIndex - 1]._uiInventoryItem.item;
            if (!item.info.isPlaceable) return;

            if (objectSystem.TryAddToGlobal(e.Position, generator.GetRealPosition(e.Position), 
                item.info.prefab.GetInstanceID(), 1, InstanceType.Interactable))
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
            SaveInventory();
        }

        private void PickUpEvent(AEntity entity, PickUpEvent e)
        {
            ObjectInstInfo objectInstInfo = objectSystem.GetGlobalObjectInstInfo(e.Position);
            ObjectInfo data = objectSystem.GetGlobalObjectInfo(e.Position);
            if (data == null) return;

            IInventoryItem item = infoManager.GetInventoryItemByInfo(
                data.InventoryItemInfo.id, 
                data.InventoryItemInfo, 
                objectInstInfo.Amount);

            if (eventDrivenInventoryProxy.TryToAdd(this, item))
            {
                objectSystem.RemoveFromGlobal(e.Position);
                SpreadPattern pattern = data.OnPickUpPattern;
                
                if (pattern != null)
                {
                    objectSystem.TryAddToGlobal(e.Position, generator.GetRealPosition(e.Position), 
                        pattern.main.ObjectPrefab.GetInstanceID(), pattern.main.DefaultAmount, pattern.main.InstanceType);
                    
                    if (pattern.other.Length != 0)
                    {
                        for (byte i = 0; i < pattern.other.Length; i++)
                        {
                            Vector2Int newPos = Extension.GetRandomPointAround(e.Position, pattern.factor);
                            objectSystem.TryAddToGlobal(newPos, generator.GetRealPosition(newPos), 
                                pattern.other[i].ObjectPrefab.GetInstanceID(), 
                                pattern.other[i].DefaultAmount, 
                                pattern.other[i].InstanceType);
                        }
                    }
                }
            }

            generator.ExtraUpdate();
        }

        private void SaveInventory()
        {
            try
            {   
                worldRegistry.UpdateState(state =>
                {
                    state.inventory = tester.Serialize();
                });
            }
            catch (Exception ex)
            {
                logger.LogError($"[UIInventory] Ошибка сохранения инвентаря: {ex.Message}");
            }
        }

        public void BreakUp(ISetAble.Callback callback)
        {
            uIDragger.BreakUp();
            craftService.BreakUp();

            eventDrivenInventoryProxy.OnInventoryStateChangedEventOnce -= OnInventoryStateChanged;
            eventDrivenInventoryProxy.Dispose();
            inventoryInputHandler.UnregisterInput();

            callback?.Invoke();
        }
    }
}
using UnityEngine;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

using TheRavine.Base;
using TheRavine.InventoryElements;
using TheRavine.Generator;
using TheRavine.ObjectControl;
using TheRavine.Extensions;
using TheRavine.EntityControl;
using TheRavine.Events;

namespace TheRavine.Inventory
{
    public class UIInventory : MonoBehaviour, ISetAble, IPlacementSource
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
        private PlayerEntity player;
        private readonly Queue<PendingPlace> pendingPlaces = new();
        private readonly HashSet<long> pendingPickUps = new();

        private struct PendingPlace
        {
            public UIInventorySlot Cell;
            public IInventoryItem Item;
            public long Position;
        }

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
            ServiceLocator.Register(eventDrivenInventoryProxy);
            ServiceLocator.Register<IPlacementSource>(this);

            infoManager = new InfoManager(dataItems);
            tester = new InventoryTester(slotList.ToArray(), infoManager, eventDrivenInventoryProxy);
            

            PlayerEntity playerData = (PlayerEntity) ServiceLocator.Players.GetAllPlayers()[0];
            player = playerData;
            var gameSettings = ServiceLocator.GetService<GlobalSettingsController>().Settings.CurrentValue;
            inventoryInputHandler.RegisterInput(playerData, gameSettings);

    

            uIDragger.SetUp(eventDrivenInventoryProxy, playerData.GetEntityComponent<MainComponent>());


            craftService.SetUp(infoManager, eventDrivenInventoryProxy);

            eventDrivenInventoryProxy.OnInventoryStateChangedEventOnce += OnInventoryStateChanged;
            
            LoadInventoryDataAsync(playerData).Forget();

            callback?.Invoke();
        }

        private async UniTaskVoid LoadInventoryDataAsync(PlayerEntity playerData)
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

                EventBus playerEventBus = playerData.GetEntityComponent<EventBusComponent>().EventBus;
                playerEventBus.Subscribe<PlaceEvent>(PlaceObjectEvent);
                playerEventBus.Subscribe<PickUpEvent>(PickUpEvent);
                playerEventBus.Subscribe<PlaceResultEvent>(OnPlaceResult);
                playerEventBus.Subscribe<PickUpResultEvent>(OnPickUpResult);
            }
            catch (Exception ex)
            {
                logger.LogError($"[UIInventory] Ошибка загрузки инвентаря: {ex.Message}");
            }
        }

        private bool TryGetActiveCell(out UIInventorySlot cell)
        {
            int index = inventoryInputHandler.ActiveCellIndex - 1;
            if (index < 0 || index >= activeCells.Length)
            {
                cell = null;
                return false;
            }

            cell = activeCells[index];
            return cell != null && cell.slot != null && !cell.slot.isEmpty;
        }

        public bool HasPlaceableInActiveCell() =>
            TryGetActiveCell(out UIInventorySlot cell) && cell.slot.item.info.isPlaceable;

        private void PlaceObjectEvent(AEntity entity, PlaceEvent e)
        {
            if (!TryGetActiveCell(out UIInventorySlot cell)) return;

            InventorySlot slot = cell.slot;
            IInventoryItem item = slot.item;
            if (!item.info.isPlaceable) return;

            if (!objectSystem.TryGetIdByPrefab(item.info.prefab, out int placeId))
            {
                logger.LogWarning($"[UIInventory] Нет ObjectInfo для префаба предмета {item.info.id}");
                return;
            }

            int reserved = 0;
            foreach (PendingPlace pending in pendingPlaces)
                if (pending.Item == item) reserved++;
            if (reserved >= slot.amount) return;

            if (player.RequestPlace(e.Position, placeId))
                pendingPlaces.Enqueue(new PendingPlace { Cell = cell, Item = item, Position = e.Position });
        }

        private void OnPlaceResult(AEntity entity, PlaceResultEvent e)
        {
            if (pendingPlaces.Count == 0) return;

            PendingPlace pending = pendingPlaces.Dequeue();
            if (!e.Ok)
            {
                logger.LogInfo($"[UIInventory] Размещение в {Position2Int.UnpackToVector(e.Position)} отклонено: {(WorldEditResult)e.Edit}/{(PlacementResult)e.Placement}");
                return;
            }
            if (pending.Position != e.Position) return;

            InventorySlot slot = pending.Cell.slot;
            if (slot == null || slot.item != pending.Item) return;

            pending.Item.state.amount--;
            if (slot.amount <= 0) slot.Clear();
            pending.Cell.Refresh();
        }

        private void OnInventoryStateChanged(object sender)
        {
            craftService.OnInventoryCraftCheck(sender);
            SaveInventory();
        }

        private void PickUpEvent(AEntity entity, PickUpEvent e)
        {
            if (pendingPickUps.Contains(e.Position)) return;
            if (!generator.TryGetObject(e.Position, out ObjectInstInfo objectInstInfo)) return;

            ObjectInfo data = objectSystem.GetInfo(objectInstInfo.PrefabID);
            if (data == null || data.InventoryItemInfo == null) return;
            if (eventDrivenInventoryProxy.isFull && !HasItem(data.InventoryItemInfo)) return;

            if (player.RequestPickUp(e.Position))
                pendingPickUps.Add(e.Position);
        }

        private void OnPickUpResult(AEntity entity, PickUpResultEvent e)
        {
            pendingPickUps.Remove(e.Position);
            if (!e.Ok) return;

            ObjectInfo data = objectSystem.GetInfo(e.PrefabId);
            if (data == null || data.InventoryItemInfo == null) return;

            IInventoryItem item = infoManager.GetInventoryItemByInfo(
                data.InventoryItemInfo.id,
                data.InventoryItemInfo,
                e.Amount);

            if (!eventDrivenInventoryProxy.TryToAdd(this, item))
                logger.LogWarning($"[UIInventory] Нет места для {data.InventoryItemInfo.id}, подобранный предмет потерян");
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

            ServiceLocator.Services.Unregister<IPlacementSource>(this);
            eventDrivenInventoryProxy.OnInventoryStateChangedEventOnce -= OnInventoryStateChanged;
            eventDrivenInventoryProxy.Dispose();
            inventoryInputHandler.UnregisterInput();

            callback?.Invoke();
        }
    }
}
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
        private readonly Dictionary<(long Position, byte Level), PendingPickUp> pendingPickUps = new();
        private readonly PickUpReservation pickUpReservation = new();

        private struct PendingPickUp
        {
            public IInventoryItemInfo Info;
            public int Amount;
        }

        private struct PendingPlace
        {
            public UIInventorySlot Cell;
            public IInventoryItem Item;
            public long Position;
            public byte Level;
        }

        public bool HasItem(InventoryItemInfo info) => 
            eventDrivenInventoryProxy.HasItem(infoManager.GetItemType(info));

        public void SetUp(ISetAble.Callback callback)
        {            
            logger = ServiceLocator.GetService<RavineLogger>();
            generator = ServiceLocator.GetService<MapGenerator>();
            objectSystem = ServiceLocator.GetService<ObjectSystem>();
            worldRegistry = ServiceLocator.GetService<WorldRegistry>();

            var slotList = new List<UIInventorySlot>();
            var uiSlot = GetComponentsInChildren<UIInventorySlot>();
            for (int i = 0; i < uiSlot.Length; i++) AddUniqueSlot(slotList, uiSlot[i]);
            for (int i = 0; i < activeCells.Length; i++)
            {
                if (activeCells[i] == null) continue;
                activeCells[i].SetKind(SlotKind.Hotbar);
                AddUniqueSlot(slotList, activeCells[i]);
            }
            var craftCells = craftService.CraftCells;
            for (int i = 0; i < craftCells.Count; i++)
            {
                if (craftCells[i] == null) continue;
                craftCells[i].SetKind(SlotKind.Craft);
                AddUniqueSlot(slotList, craftCells[i]);
            }
            if (craftService.ResultCell != null)
            {
                craftService.ResultCell.SetKind(SlotKind.CraftResult);
                AddUniqueSlot(slotList, craftService.ResultCell);
            }

            var kinds = new SlotKind[slotList.Count];
            for (int i = 0; i < kinds.Length; i++) kinds[i] = slotList[i].Kind;

            eventDrivenInventoryProxy = new EventDrivenInventoryProxy(kinds);
            ServiceLocator.Register(eventDrivenInventoryProxy);
            ServiceLocator.Register<IPlacementSource>(this);

            infoManager = new InfoManager(dataItems);
            tester = new InventoryTester(slotList.ToArray(), infoManager, eventDrivenInventoryProxy);
            

            PlayerEntity playerData = (PlayerEntity) ServiceLocator.Players.GetAllPlayers()[0];
            player = playerData;
            inventoryInputHandler.RegisterInput(playerData);

    

            uIDragger.SetUp(eventDrivenInventoryProxy, playerData.GetEntityComponent<MainComponent>(), playerData);


            craftService.SetUp(infoManager, eventDrivenInventoryProxy);

            eventDrivenInventoryProxy.OnInventoryStateChangedEventOnce += OnInventoryStateChanged;
            
            LoadInventoryDataAsync(playerData).Forget();

            callback?.Invoke();
        }

        private static void AddUniqueSlot(List<UIInventorySlot> list, UIInventorySlot slot)
        {
            if (!list.Contains(slot)) list.Add(slot);
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

        public int ActivePlaceableLevels
        {
            get
            {
                if (!TryGetActiveCell(out UIInventorySlot cell)) return 1;
                IInventoryItemInfo itemInfo = cell.slot.item.info;
                if (!itemInfo.isPlaceable || itemInfo.prefab == null) return 1;
                if (!objectSystem.TryGetIdByPrefab(itemInfo.prefab, out int placeId)) return 1;
                ObjectInfo info = objectSystem.GetInfo(placeId);
                return info != null ? Mathf.Max(1, info.Levels) : 1;
            }
        }

        public object ActivePlaceable =>
            TryGetActiveCell(out UIInventorySlot cell) && cell.slot.item.info.isPlaceable ? cell.slot.item : null;

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

            if (player.RequestPlace(e.Position, placeId, e.Level))
                pendingPlaces.Enqueue(new PendingPlace { Cell = cell, Item = item, Position = e.Position, Level = e.Level });
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
            if (pending.Position != e.Position || pending.Level != e.Level) return;

            InventorySlot slot = pending.Cell.slot;
            if (slot == null || slot.item != pending.Item) return;

            eventDrivenInventoryProxy.ConsumeFromSlot(this, slot, 1);
        }

        private void OnInventoryStateChanged(object sender)
        {
            craftService.OnInventoryCraftCheck(sender);
            SaveInventory();
        }

        private void PickUpEvent(AEntity entity, PickUpEvent e)
        {
            if (pendingPickUps.ContainsKey((e.Position, e.Level))) return;
            if (!generator.TryGetObject(e.Position, e.Level, out ObjectInstInfo objectInstInfo)) return;

            ObjectInfo data = objectSystem.GetInfo(objectInstInfo.PrefabID);
            if (data == null || data.InventoryItemInfo == null) return;

            IInventoryItemInfo info = data.InventoryItemInfo;
            int amount = objectInstInfo.Amount;
            if (!pickUpReservation.TryReserve(info, amount, eventDrivenInventoryProxy.FreeRoomFor(info))) return;

            if (player.RequestPickUp(e.Position, e.Level))
                pendingPickUps.Add((e.Position, e.Level), new PendingPickUp { Info = info, Amount = amount });
            else
                pickUpReservation.Release(info, amount);
        }

        private void OnPickUpResult(AEntity entity, PickUpResultEvent e)
        {
            if (pendingPickUps.Remove((e.Position, e.Level), out PendingPickUp pendingPickUp))
                pickUpReservation.Release(pendingPickUp.Info, pendingPickUp.Amount);
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
            pendingPickUps.Clear();
            pendingPlaces.Clear();
            pickUpReservation.Clear();

            ServiceLocator.Services.Unregister<IPlacementSource>(this);
            eventDrivenInventoryProxy.OnInventoryStateChangedEventOnce -= OnInventoryStateChanged;
            eventDrivenInventoryProxy.Dispose();
            inventoryInputHandler.UnregisterInput();

            callback?.Invoke();
        }
    }
}
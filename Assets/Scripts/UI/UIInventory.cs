using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System;

using TheRavine.InventoryElements;
using TheRavine.Generator;
using TheRavine.ObjectControl;
using TheRavine.Extentions;
using TheRavine.Services;
using TheRavine.EntityControl;

namespace TheRavine.Inventory
{
    public class UIInventory : MonoBehaviour, ISetAble
    {
        [SerializeField] private GameObject grid;
        [SerializeField] private bool filling;
        [SerializeField] private UIInventorySlot[] activeCells, craftCells;
        [SerializeField] private PlayerInput input;
        [SerializeField] private RectTransform cell;
        [SerializeField] private InventoryCraftInfo[] CraftInfo;
        [SerializeField] private InputActionReference enter, quit, digitAction;
        private int activeCell = 1;
        public InventoryWithSlots inventory => tester.inventory;
        private UIInventoryTester tester;
        [HideInInspector] public PlayerEntity playerData;
        private MapGenerator generator;
        private ObjectSystem objectSystem;
        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            playerData = locator.GetService<PlayerEntity>();
            generator = locator.GetService<MapGenerator>();
            objectSystem = locator.GetService<ObjectSystem>();
            var uiSlot = GetComponentsInChildren<UIInventorySlot>();
            var slotList = new List<UIInventorySlot>();
            slotList.AddRange(uiSlot);
            slotList.AddRange(activeCells);
            tester = new UIInventoryTester(slotList.ToArray());
            tester.FillSlots(filling);
            CraftInfo = InfoManager.GetAllCraftRecepts();
            grid.SetActive(false);
            OnInventoryStateChanged(this);

            enter.action.performed += ChangeInventoryState;
            quit.action.performed += ChangeInventoryState;

            inventory.OnInventoryStateChangedEvent += OnInventoryStateChanged;
            playerData.placeObject += PlaceObject;
            playerData.aimRaise += AimRaise;

            digitAction.action.performed += SetActionCell;

            callback?.Invoke();
        }

        private void SetActionCell(InputAction.CallbackContext c)
        {
            SetActiveCell((int)c.ReadValue<float>());
        }

        private void SetActiveCell(int index)
        {
            activeCell = index;
            cell.anchoredPosition = new Vector2(55 + 120 * (index - 1), -50);
        }


        private void PlaceObject(Vector2 position)
        {
            IInventorySlot slot = activeCells[activeCell - 1].slot;
            if (slot.isEmpty)
                return;
            IInventoryItem item = activeCells[activeCell - 1]._uiInventoryItem.item;
            if (objectSystem.TryAddToGlobal(position, item.info.prefab.GetInstanceID(), 1, InstanceType.Inter))
            {
                item.state.amount--;
                if (slot.amount <= 0)
                    slot.Clear();
                activeCells[activeCell - 1].Refresh();
                if (generator.TryToAddPositionToChunk(position))
                    generator.ExtraUpdate();
            }
        }

        [SerializeField] private string[] names = new string[8], sortedNames, sortedIngr;
        [SerializeField] private UIInventorySlot result;
        [SerializeField] private GameObject craftLine;
        private void OnInventoryStateChanged(object sender)
        {
            names = new string[8];
            for (int i = 0; i < craftCells.Length; i++)
                names[i] = craftCells[i].slot.isEmpty ? "" : craftCells[i]._uiInventoryItem.item.info.id;
            for (int i = 0; i < CraftInfo.Length; i++)
            {
                bool ispossible = true;
                if (CraftInfo[i].isorder)
                {
                    for (int j = 0; j < CraftInfo[i].ingr.Length; j++)
                        if (CraftInfo[i].ingr[j] != null && String.Compare(CraftInfo[i].ingr[j].id, names[j], StringComparison.OrdinalIgnoreCase) != 0)
                            ispossible = false;
                }
                else
                {
                    InventoryItemInfo[] itemInfoArray = (InventoryItemInfo[])CraftInfo[i].ingr.Clone();
                    sortedIngr = new string[8];
                    for (int j = 0; j < itemInfoArray.Length; j++)
                        if (itemInfoArray[j] != null)
                            sortedIngr[j] = itemInfoArray[j].id;
                    sortedNames = (string[])names.Clone();
                    Array.Sort(sortedIngr);
                    Array.Reverse(sortedIngr);
                    Array.Sort(sortedNames);
                    Array.Reverse(sortedNames);
                    for (int j = 0; j < CraftInfo[i].ingr.Length; j++)
                        if (String.Compare(sortedIngr[j], sortedNames[j], StringComparison.OrdinalIgnoreCase) != 0)
                            ispossible = false;
                        else
                        {
                            // print(sortedIngr[j] + " " + sortedNames[j]);
                            // print(j);
                        }
                }
                if (ispossible)
                {
                    print("is posible");
                    CraftPosible(true);
                    break;
                }
                else
                    CraftPosible(false);
            }
        }

        private void CraftPosible(bool isActive)
        {
            craftLine.SetActive(isActive);
        }

        private void AimRaise(Vector2 position)
        {
            ObjectInstInfo objectInstInfo = objectSystem.GetGlobalObjectInfo(position);
            if (objectInstInfo.prefabID == 0 || objectInstInfo.objectType != InstanceType.Inter)
                return;
            ObjectInfo data = objectSystem.GetPrefabInfo(objectInstInfo.prefabID);
            if (data.iteminfo == null)
                return;
            if (inventory.TryToAdd(this, InfoManager.GetInventoryItemByInfo(data.iteminfo.id, data.iteminfo, objectInstInfo.amount)))
            {
                objectSystem.RemoveFromGlobal(position);
                print("rised");
                SpreadPattern pattern = data.pickUpPattern;
                if (pattern != null)
                {
                    objectSystem.TryAddToGlobal(position, pattern.main.prefab.GetInstanceID(), pattern.main.amount, pattern.main.iType, (position.x + position.y) % 2 == 0);
                    if (pattern.other.Length != 0)
                    {
                        for (byte i = 0; i < pattern.other.Length; i++)
                        {
                            Vector2 newPos = Extention.GetRandomPointAround(position, pattern.factor);
                            objectSystem.TryAddToGlobal(newPos, pattern.other[i].prefab.GetInstanceID(), pattern.other[i].amount, pattern.other[i].iType, newPos.x < position.x);
                        }
                    }
                    print("leave pattern");
                }
                generator.ExtraUpdate();
            }
        }


        private bool isactive = false;
        public void ChangeInventoryState(InputAction.CallbackContext context)
        {
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
            grid.SetActive(isactive);
        }

        public void ChangeInventoryState()
        {
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
            grid.SetActive(isactive);
        }
        public void BreakUp()
        {
            inventory.OnInventoryStateChangedEvent -= OnInventoryStateChanged;
            playerData.placeObject -= PlaceObject;
            playerData.aimRaise -= AimRaise;

            enter.action.performed -= ChangeInventoryState;
            quit.action.performed -= ChangeInventoryState;

            digitAction.action.performed -= SetActionCell;
        }
    }
}
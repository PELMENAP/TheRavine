using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System;

public class UIInventory : MonoBehaviour, ISetAble
{
    [SerializeField] private GameObject grid;
    [SerializeField] private bool filling;
    [SerializeField] private UIInventorySlot[] activeCells, craftCells;
    [SerializeField] private RectTransform cell;
    [SerializeField] private InventoryCraftInfo[] CraftInfo;
    private bool isActive;
    private int activeCell = 1;
    public InventoryWithSlots inventory => tester.inventory;
    private UIInventoryTester tester;
    private PlayerData PData;
    public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
    {
        PData = locator.GetService<PlayerData>();
        var uiSlot = GetComponentsInChildren<UIInventorySlot>();
        var slotList = new List<UIInventorySlot>();
        slotList.AddRange(uiSlot);
        slotList.AddRange(activeCells);
        // var uiSlotBar = activeBar.GetComponentsInChildren<UIInventorySlot>();
        // // uiSlot = uiSlot.Concat(uiSlotBar).ToArray();
        tester = new UIInventoryTester(slotList.ToArray());
        tester.FillSlots(filling);
        CraftInfo = InfoManager.GetAllCraftRecepts();
        isActive = false;
        grid.SetActive(isActive);
        inventory.OnInventoryStateChangedEvent += OnInventoryStateChanged;
        PData.placeObject += PlaceObject;
        PData.aimRaise += AimRaise;
        callback?.Invoke();
    }

    // private IEnumerator SetUpCanvas()
    // {
    //     yield return new WaitForSeconds(3f);
    // }

    private void Update()
    {
        if (Input.GetKeyUp("e") && !Input.GetMouseButton(0))
        {
            isActive = !isActive;
            grid.SetActive(isActive);
            if (isActive)
                PData.SetBehaviourSit();
            else
                PData.SetBehaviourIdle();
        }
        if (Input.GetKeyDown("1"))
            SetActiveCell(1);
        else if (Input.GetKeyDown("2"))
            SetActiveCell(2);
        else if (Input.GetKeyDown("3"))
            SetActiveCell(3);
        else if (Input.GetKeyDown("4"))
            SetActiveCell(4);
        else if (Input.GetKeyDown("5"))
            SetActiveCell(5);
        else if (Input.GetKeyDown("6"))
            SetActiveCell(6);
        else if (Input.GetKeyDown("7"))
            SetActiveCell(7);
        else if (Input.GetKeyDown("8"))
            SetActiveCell(8);
    }

    private void SetActiveCell(int index)
    {
        activeCell = index;
        cell.anchoredPosition = new Vector2(55 + 120 * (index - 1), -50);
    }


    private void PlaceObject(Vector3 position)
    {
        IInventorySlot slot = activeCells[activeCell - 1].slot;
        if (slot.isEmpty)
            return;
        IInventoryItem item = activeCells[activeCell - 1]._uiInventoryItem.item;
        // print(item.state.amount);
        // if (PoolManager.inst.SetObjectByPosition(new Vector2(position.x, position.y), item.info.id, 1, item.info.prefab.GetInstanceID()))
        // {
        //     item.state.amount--;
        //     if (slot.amount <= 0)
        //         slot.Clear();
        //     activeCells[activeCell - 1].Refresh();
        // }

    }

    [SerializeField] private string[] names = new string[8], sortedNames, sortedIngr;
    [SerializeField] private UIInventorySlot result;
    [SerializeField] private GameObject craftLine;
    private void OnInventoryStateChanged(object sender)
    {
        print(" start shecking");
        names = new string[8];
        for (int i = 0; i < craftCells.Length; i++)
        {
            IInventorySlot slot = craftCells[i].slot;
            if (!slot.isEmpty)
                names[i] = craftCells[i]._uiInventoryItem.item.info.id;
            else
                names[i] = "";
        }
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
                        print(sortedIngr[j] + " " + sortedNames[j]);
                        print(j);
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

    private void AimRaise(Vector3 position)
    {
        // Triple<string, int, ObjectInstance> triple = PoolManager.inst.GetObjectByPosition(new Vector2(position.x, position.y));
        // if (triple == null)
        //     return;
        // IInventoryItem item = InfoManager.GetInventoryItem(triple.First, triple.Second);
        // if (item == null)
        //     return;
        // inventory.TryToAdd(triple.Third.gameObject, item);
        // PoolManager.inst.SetObjectByPosition(new Vector2(position.x, position.y), triple.First, -triple.Second, triple.Third);
    }

    private void OnDisable()
    {
        inventory.OnInventoryStateChangedEvent -= OnInventoryStateChanged;
        PData.placeObject -= PlaceObject;
        PData.aimRaise -= AimRaise;
    }
}

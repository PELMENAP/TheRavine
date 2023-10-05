using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System;

public class UIInventory : MonoBehaviour
{
    [SerializeField] private GameObject grid, activeBar, buttons, inputField;
    [SerializeField] private bool filling;
    [SerializeField] private UIInventorySlot[] activeCells;
    [SerializeField]
    private RectTransform cell;
    private bool isActive;
    private int activeCell = 1;
    public InventoryWithSlots inventory => tester.inventory;

    private UIInventoryTester tester;

    private void Start()
    {
        var uiSlot = GetComponentsInChildren<UIInventorySlot>();
        // var uiSlotBar = activeBar.GetComponentsInChildren<UIInventorySlot>();
        // // uiSlot = uiSlot.Concat(uiSlotBar).ToArray();
        tester = new UIInventoryTester(uiSlot);
        tester.FillSlots(filling);
        isActive = false;
        grid.SetActive(isActive);
        activeBar.SetActive(isActive);
        buttons.SetActive(isActive);
        inputField.SetActive(isActive);
        PlayerData.instance.placeObject += PlaceObject;
        PlayerData.instance.aimRaise += AimRaise;
        StartCoroutine(SetUpCanvas());
    }

    private IEnumerator SetUpCanvas(){
        yield return new WaitForSeconds(3f);
        activeBar.SetActive(true);
        yield return new WaitForSeconds(3f);
        buttons.SetActive(true);
        yield return new WaitForSeconds(3f);
        inputField.SetActive(true);
    }

    private void Update()
    {
        if (Input.GetKeyUp("e") && !Input.GetMouseButton(0) && !PlayerData.instance.dialog.activeSelf)
        {
            isActive = !isActive;
            grid.SetActive(isActive);
            if (isActive)
                PlayerData.instance.SetBehaviourSit();
            else
                PlayerData.instance.SetBehaviourIdle();
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
        try
        {
            IInventorySlot slot = activeCells[activeCell - 1].slot;
            if (!slot.isEmpty)
            {
                IInventoryItem item = activeCells[activeCell - 1]._uiInventoryItem.item;
                // print(item.state.amount);
                GameObject plob = item.info.prefab;
                if (plob != null)
                {
                    if(InterObjectManager.instance.SetObjectByPosition(new Vector2(position.x, position.y), item.info.id, 1, plob)){
                        item.state.amount--;
                        if (slot.amount <= 0)
                            slot.Clear();
                        activeCells[activeCell - 1].Refresh();
                    }

                    // RaycastHit2D[] hits = Physics2D.RaycastAll(position, transform.forward);
                    // for (int i = 0; i < hits.Length; i++)
                    // {
                    //     PickUpRequire component = hits[i].collider.gameObject.GetComponent<PickUpRequire>();
                    //     print(component.id);
                    //     print(item.info.id);
                    //     if (component != null && component.id == item.info.id)
                    //     {
                    //         component.amount++;
                    //         return;
                    //     }
                    // }
                }
            }
        }
        catch
        {

        }
    }

    private void AimRaise(Vector3 position)
    {
        Triple<string, int, GameObject> triple = InterObjectManager.instance.GetObjectByPosition(new Vector2(position.x, position.y));
        if(triple != null){
            PickUpRequire component = triple.Third.GetComponent<PickUpRequire>();
            if(component != null)
            {
                IInventoryItem item = InfoManager.GetInventoryItem(triple.First, triple.Second);
                inventory.TryToAdd(triple.Third, item);
                InterObjectManager.instance.SetObjectByPosition(new Vector2(position.x, position.y), triple.First, -triple.Second, triple.Third);
            }
            else
                throw new Exception("There's no pickable component");
        }
        // GameObject pickedObject;
        // for (int i = 0; i < hits.Length; i++)
        // {
        //     pickedObject = hits[i].collider.gameObject;
        //     if(pickedObject.CompareTag("Player"))
        //         continue;
        //     PickUpRequire component = pickedObject.GetComponent<PickUpRequire>();
        //     if(component != null)
        //     {
        //         IInventoryItem item = InfoManager.GetInventoryItem(component.id, component.itemInfo, component.amount);
        //         inventory.TryToAdd(pickedObject, item);
        //         try
        //         {
        //             // EndlessTerrain.instance.loosers.Add(new Vector2(pickedObject.transform.position.x, pickedObject.transform.position.y));
        //             pickedObject.SetActive(false);
        //         }
        //         catch
        //         {
        //         }
        //         Destroy(pickedObject);
        //         break;
        //     }
        //     else
        //     {
        //         throw new Exception("There's no pickable component");
        //     }
        // }
    }
}

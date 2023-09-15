using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class UIInventory : MonoBehaviour
{
    [SerializeField] private GameObject grid, activeBar;
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
        PlayerController.instance.placeObject += PlaceObject;
    }

    private void Update()
    {
        if (Input.GetKeyUp("e") && !Input.GetMouseButton(0) && PlayerController.instance.moving && !PlayerController.instance.dialog.activeSelf)
        {
            isActive = !isActive;
            grid.SetActive(isActive);
            if (isActive)
                PlayerController.instance.SetBehaviourSit();
            else
                PlayerController.instance.SetBehaviourIdle();
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
        AimRaise();
    }

    private void PlaceObject(Vector3 position)
    {
        try
        {
            IInventorySlot slot = activeCells[activeCell - 1].slot;
            if (!slot.isEmpty)
            {
                IInventoryItem item = activeCells[activeCell - 1]._uiInventoryItem.item;
                print(item.state.amount);
                GameObject plob = item.info.prefab;
                if (plob != null)
                {
                    item.state.amount--;
                    if (slot.amount <= 0)
                        slot.Clear();
                    activeCells[activeCell - 1].Refresh();
                    RaycastHit2D[] hits = Physics2D.RaycastAll(position, transform.forward);
                    for (int i = 0; i < hits.Length; i++)
                    {
                        PickUpRequire component = hits[i].collider.gameObject.GetComponent<PickUpRequire>();
                        if (component != null && component.id == item.info.id)
                        {
                            component.amount++;
                            return;
                        }
                    }
                    Instantiate(plob, position, Quaternion.identity);
                }
            }
        }
        catch
        {

        }
    }

    private void SetActiveCell(int index)
    {
        activeCell = index;
        cell.anchoredPosition = new Vector2(55 + 120 * (index - 1), -50);
    }
    private void AimRaise()
    {
        if (Input.GetKey("f"))
        {
            RaycastHit2D[] hits;
            if (Input.GetMouseButton(1))
                hits = Physics2D.RaycastAll(PlayerController.instance.crosshair.position, transform.forward);
            else
                hits = Physics2D.RaycastAll(PlayerController.entityTrans.position, transform.forward);
            GameObject pickedObject;
            for (int i = 0; i < hits.Length; i++)
            {
                pickedObject = hits[i].collider.gameObject;
                try
                {
                    PickUpRequire component = pickedObject.GetComponent<PickUpRequire>();
                    if (component.mayPickUp)
                    {
                        inventory.TryToAdd(pickedObject, PData.pdata.GetItem(component.id, component.amount));
                        try
                        {
                            EndlessTerrain.instance.loosers.Add(new Vector2(pickedObject.transform.position.x, pickedObject.transform.position.y));
                            pickedObject.SetActive(false);
                        }
                        catch
                        {

                        }
                        Destroy(pickedObject);
                    }
                }
                catch
                {

                }
            }
        }
    }
}

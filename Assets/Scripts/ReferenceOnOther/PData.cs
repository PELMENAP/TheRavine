using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D;

public class PData : MonoBehaviour
{
    public static PData pdata;
    // public TaskManager taskManager;
    public Text description;
    public GameObject dushParent;
    public Image dushImage, infoImage;

    // public InventoryItemInfo[] infoAboutItems;

    // // public IInventoryItem GetItem(string id, int amount)
    // // {
    // //     switch (id)
    // //     {
    // //         case "porchini":
    // //             var porchini = new Porchini(infoAboutItems[0]);
    // //             porchini.state.amount = amount;
    // //             return porchini;
    // //         case "toadstool":
    // //             var toadstool = new Toadstool(infoAboutItems[1]);
    // //             toadstool.state.amount = amount;
    // //             return toadstool;
    // //         case "boletus":
    // //             var boletus = new Boletus(infoAboutItems[2]);
    // //             boletus.state.amount = amount;
    // //             return boletus;
    // //         case "brownboletus":
    // //             var brownboletus = new BrownBoletus(infoAboutItems[3]);
    // //             brownboletus.state.amount = amount;
    // //             return brownboletus;
    // //         default:
    // //             return null;
    // //     }
    // // }

    // public IInventoryItem GetItem(int id, int amount)
    // {
    //     switch (id)
    //     {
    //         case 0:
    //             var porchini = new Porchini(infoAboutItems[0]);
    //             porchini.state.amount = amount;
    //             return porchini;
    //         case 1:
    //             var toadstool = new Toadstool(infoAboutItems[1]);
    //             toadstool.state.amount = amount;
    //             return toadstool;
    //         case 2:
    //             var boletus = new Boletus(infoAboutItems[2]);
    //             boletus.state.amount = amount;
    //             return boletus;
    //         case 3:
    //             var brownboletus = new BrownBoletus(infoAboutItems[3]);
    //             brownboletus.state.amount = amount;
    //             return brownboletus;
    //         default:
    //             return null;
    //     }
    // }

    private void Awake()
    {
        pdata = this;
    }
}

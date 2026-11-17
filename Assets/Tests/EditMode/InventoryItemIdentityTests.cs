using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using TheRavine.Inventory;

public class InventoryItemIdentityTests
{
    private InventoryItemInfo _birch, _aspen;

    [SetUp]
    public void SetUp()
    {
        _birch = Make("BirchBolete");
        _aspen = Make("AspenBolete");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_birch);
        Object.DestroyImmediate(_aspen);
    }

    private static InventoryItemInfo Make(string id)
    {
        var info = ScriptableObject.CreateInstance<InventoryItemInfo>();
        var so = new SerializedObject(info);
        so.FindProperty("_id").stringValue = id;
        so.FindProperty("_maxItemsInInventorySlot").intValue = 10;
        so.ApplyModifiedPropertiesWithoutUndo();
        return info;
    }

    private static IInventoryItem Item(InventoryItemInfo info, int amount)
    {
        var item = new DefaultItem(info);
        item.state.amount = amount;
        return item;
    }

    [Test]
    public void DefaultItemsWithDifferentIdsDoNotStack()
    {
        var model = new InventoryModel(2);
        Assert.IsTrue(model.TryToAdd(Item(_birch, 3)));
        Assert.IsTrue(model.TryToAdd(Item(_aspen, 2)));
        Assert.AreEqual("BirchBolete", model.Slots[0].itemId);
        Assert.AreEqual("AspenBolete", model.Slots[1].itemId);
        Assert.AreEqual(3, model.GetItemAmount("BirchBolete"));
        Assert.AreEqual(2, model.GetItemAmount("AspenBolete"));
    }

    [Test]
    public void SameIdStacks()
    {
        var model = new InventoryModel(2);
        Assert.IsTrue(model.TryToAdd(Item(_birch, 3)));
        Assert.IsTrue(model.TryToAdd(Item(_birch, 4)));
        Assert.AreEqual(7, model.Slots[0].amount);
        Assert.IsTrue(model.Slots[1].isEmpty);
    }

    [Test]
    public void FullSlotRejectsOtherId()
    {
        var model = new InventoryModel(1);
        Assert.IsTrue(model.TryToAdd(Item(_birch, 3)));
        Assert.IsFalse(model.TryToAdd(Item(_aspen, 1)));
    }

    [Test]
    public void HasItemOfNullIdIsFalseWithEmptySlots()
    {
        var model = new InventoryModel(2);
        Assert.IsFalse(model.HasItem((string)null));
    }

    [Test]
    public void RemoveById()
    {
        var model = new InventoryModel(2);
        model.TryToAdd(Item(_birch, 3));
        model.TryToAdd(Item(_aspen, 2));
        Assert.IsTrue(model.Remove("BirchBolete", 3));
        Assert.IsFalse(model.HasItem("BirchBolete"));
        Assert.IsTrue(model.HasItem("AspenBolete"));
    }
}

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static partial class ItemPipelineValidator
{
    private static void ValidateItemDatabases(Database db, Report report, bool fix)
    {
        for (int d = 0; d < db.ItemDatabases.Length; d++)
        {
            DataItems database = db.ItemDatabases[d];
            var so = new SerializedObject(database);
            SerializedProperty list = so.FindProperty("_data");
            var seen = new HashSet<InventoryItemInfo>();

            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                var item = list.GetArrayElementAtIndex(i).objectReferenceValue as InventoryItemInfo;
                if (item != null && seen.Add(item)) continue;

                string message = $"{database.name}: entry {i} is {(item == null ? "missing" : $"duplicate of {item.name}")}";
                if (!fix)
                {
                    report.Error(message, database);
                    continue;
                }
                list.GetArrayElementAtIndex(i).objectReferenceValue = null;
                list.DeleteArrayElementAtIndex(i);
                so.ApplyModifiedPropertiesWithoutUndo();
                report.Fixed($"{message}, removed", database);
            }
        }
    }

    public static string ToId(string source)
    {
        var chars = new System.Text.StringBuilder(source.Length);
        bool upper = true;
        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            if (!char.IsLetterOrDigit(c))
            {
                upper = true;
                continue;
            }
            chars.Append(upper ? char.ToUpperInvariant(c) : c);
            upper = false;
        }
        return chars.ToString();
    }

    private static ObjectInfo FindOwner(Database db, InventoryItemInfo item)
    {
        for (int i = 0; i < db.Infos.Length; i++)
            if (db.Infos[i].InventoryItemInfo == item && db.Infos[i].ObjectPrefab != null) return db.Infos[i];
        return null;
    }

    private static void SetItemField(InventoryItemInfo item, string field, Action<SerializedProperty> apply)
    {
        var so = new SerializedObject(item);
        apply(so.FindProperty(field));
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ValidateItems(Database db, Report report, bool fix)
    {
        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < db.Items.Length; i++)
            if (!string.IsNullOrEmpty(db.Items[i].id)) usedIds.Add(db.Items[i].id);

        var byId = new Dictionary<string, InventoryItemInfo>(StringComparer.Ordinal);
        var itemsWithAsset = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < db.Items.Length; i++)
        {
            InventoryItemInfo item = db.Items[i];
            ValidateItemId(item, usedIds, report, fix);

            if (!string.IsNullOrEmpty(item.id))
            {
                itemsWithAsset.Add(item.id);
                if (!byId.TryAdd(item.id, item))
                    report.Error($"{item.name}: id {item.id} already used by {byId[item.id].name}, rename one of them by hand", item);
            }

            if (string.IsNullOrWhiteSpace(item.title))
            {
                if (fix)
                {
                    SetItemField(item, "_title", p => p.stringValue = item.name);
                    report.Fixed($"{item.name}: empty title set to asset name", item);
                }
                else report.Warn($"{item.name}: empty title", item);
            }

            if (string.IsNullOrWhiteSpace(item.description)) report.Warn($"{item.name}: empty description", item);

            if (item.maxItemsInInventorySlot <= 0)
            {
                if (fix)
                {
                    SetItemField(item, "_maxItemsInInventorySlot", p => p.intValue = DefaultStackSize);
                    report.Fixed($"{item.name}: maxItemsInInventorySlot set to {DefaultStackSize}", item);
                }
                else report.Error($"{item.name}: maxItemsInInventorySlot = {item.maxItemsInInventorySlot}", item);
            }

            if (item.spriteIcon == null)
            {
                if (fix)
                {
                    Sprite icon = ItemPipelineStubs.GetIcon();
                    SetItemField(item, "_spriteIcon", p => p.objectReferenceValue = icon);
                    report.Fixed($"{item.name}: no icon, placeholder icon assigned", item);
                }
                else report.Warn($"{item.name}: no icon", item);
            }
            else if (ItemPipelineStubs.IsStub(item.spriteIcon))
            {
                report.Warn($"{item.name}: placeholder icon", item);
            }

            if (item.isPlaceable) ValidatePlacement(db, item, report, fix);

            if (InAnyItemDatabase(db, item)) continue;

            if (fix && db.ItemDatabases.Length == 1)
            {
                AddToItemDatabase(db.ItemDatabases[0], item);
                report.Fixed($"{item.name}: added to {db.ItemDatabases[0].name}", item);
            }
            else report.Error($"{item.name}: not in DataItems, InfoManager will not find it", item);
        }

        foreach (KeyValuePair<string, Type> pair in db.ItemTypes)
            if (pair.Value != typeof(TheRavine.Inventory.DefaultItem) && !itemsWithAsset.Contains(pair.Key))
                report.Warn($"class {pair.Value.FullName} has no InventoryItemInfo with id {pair.Key}", null);
    }

    private static void ValidateItemId(InventoryItemInfo item, HashSet<string> usedIds, Report report, bool fix)
    {
        string id = item.id;
        bool empty = string.IsNullOrEmpty(id);
        bool spaced = !empty && (id.Trim() != id || id.IndexOf(' ') >= 0);
        if (!empty && !spaced) return;

        string problem = empty ? "empty id" : $"id '{id}' contains spaces";
        string candidate = ToId(empty ? item.name : id);

        if (fix && candidate.Length > 0 && !usedIds.Contains(candidate))
        {
            SetItemField(item, "_id", p => p.stringValue = candidate);
            if (!empty) usedIds.Remove(id);
            usedIds.Add(candidate);
            report.Fixed($"{item.name}: {problem}, id set to {candidate}", item);
            return;
        }

        report.Error($"{item.name}: {problem}", item);
    }

    private static void ValidatePlacement(Database db, InventoryItemInfo item, Report report, bool fix)
    {
        if (item.prefab == null)
        {
            if (!fix)
            {
                report.Error($"{item.name}: placeable without prefab", item);
                return;
            }

            ObjectInfo owner = FindOwner(db, item);
            GameObject prefab = owner != null ? owner.ObjectPrefab : ItemPipelineStubs.GetPrefab(ReferenceSpriteMaterial(db));
            SetItemField(item, "_prefab", p => p.objectReferenceValue = prefab);
            report.Fixed($"{item.name}: placeable without prefab, {(owner != null ? $"prefab of {owner.name}" : "placeholder prefab")} assigned", item);
        }

        if (IsPlacedByRegistered(db, item.prefab)) return;

        if (!fix)
        {
            report.Error($"{item.name}: prefab {item.prefab.name} is not used by any registered ObjectInfo, placement will fail", item);
            return;
        }

        if (db.Registries.Length != 1)
        {
            report.Error($"{item.name}: prefab {item.prefab.name} is not used by any registered ObjectInfo, {db.Registries.Length} registries found", item);
            return;
        }

        ObjectInfo created = CreateObjectInfo(item);
        AddToRegistry(db.Registries[0], created);
        db.Registries[0].EditorAssignMissingIds();
        Array.Resize(ref db.Infos, db.Infos.Length + 1);
        db.Infos[db.Infos.Length - 1] = created;
        report.Fixed($"{item.name}: created and registered ObjectInfo {created.name} (id {created.Id}) for prefab {item.prefab.name}", created);
    }

    private static bool IsPlacedByRegistered(Database db, GameObject prefab)
    {
        for (int i = 0; i < db.Infos.Length; i++)
            if (db.Infos[i].ObjectPrefab == prefab && InAnyRegistry(db, db.Infos[i])) return true;
        return false;
    }

    private static ObjectInfo CreateObjectInfo(InventoryItemInfo item)
    {
        var info = ScriptableObject.CreateInstance<ObjectInfo>();
        AssetDatabase.CreateAsset(info, AssetDatabase.GenerateUniqueAssetPath($"{DefaultInfoFolder}/{item.name}.asset"));
        var so = new SerializedObject(info);
        so.FindProperty("objectPrefab").objectReferenceValue = item.prefab;
        so.FindProperty("inventoryItemInfo").objectReferenceValue = item;
        so.FindProperty("instanceType").intValue = (int)InstanceType.Interactable;
        so.FindProperty("defaultAmount").intValue = 1;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(info);
        return info;
    }

    private static void ValidateCrafts(Database db, Report report)
    {
        for (int i = 0; i < db.Crafts.Length; i++)
        {
            InventoryCraftInfo craft = db.Crafts[i];
            InventoryItemInfo[] ingr = craft.Ingr;

            if (craft.Res == null) report.Error($"{craft.name}: no result", craft);
            else if (!InAnyItemDatabase(db, craft.Res)) report.Error($"{craft.name}: result {craft.Res.name} is not in DataItems", craft);

            if (ingr == null || ingr.Length == 0)
            {
                report.Warn($"{craft.name}: no ingredients", craft);
                continue;
            }

            if (craft.AmountIngr == null || craft.AmountIngr.Length != ingr.Length)
                report.Error($"{craft.name}: amountIngr length differs from ingr length", craft);
            if (craft.CatalystIngr != null && craft.CatalystIngr.Length != 0 && craft.CatalystIngr.Length != ingr.Length)
                report.Error($"{craft.name}: catalystIngr length differs from ingr length", craft);

            for (int k = 0; k < ingr.Length; k++)
            {
                if (ingr[k] == null) report.Error($"{craft.name}: ingr[{k}] is empty", craft);
                else if (!InAnyItemDatabase(db, ingr[k])) report.Error($"{craft.name}: ingr[{k}] {ingr[k].name} is not in DataItems", craft);
            }
        }
    }
}

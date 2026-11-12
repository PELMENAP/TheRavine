using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ItemDescriptionMigration
{
    private static readonly Dictionary<string, string> Descriptions = new()
    {
        { "Porchini", "Всегда белый\r\n- тонищирующее действие\r\n- предпочитает мох и лишайник\r\n- самый главный гриб\r\n80 ккал" },
        { "Toadstool", "Маленькая зловредная\r\n- неприятный сладковатый запах\r\n- предпочитает плодородные почвы\r\n- токсины не убрать\r\n30 ккал + смертельное отравление" },
        { "BirchBolete", "Невзрачная нога\r\n- хоть как можно есть\r\n- частый экземпляр\r\n- главный любитель берёз\r\n50 ккал" },
        { "AspenBolete", "Оранжевая шапочка\r\n- вполне сьедобен\r\n- предпочитает лиственные леса\r\n- цвет осенних листьев\r\n60 ккал" },
        { "Saffron", "Рыжий \r\n- интересно, почему он рыжий? \r\n- Засолил бы? \r\n30 ккал" },
        { "Battery", "Несколько соединенных \r\nмежду собой гальванических элементов \r\n2000 mAh" },
        { "Politics", "Государство \r\rобщественные институты и отдельные личности реализуют свои интересы при помощи власти" },
        { "Kirieshky", "Ржано-пшеничные сухарики \r\nсо вкусом сыра \r\n60 грамм" },
        { "Ticket", "Билет в один конец - домой \r\nномер 124115" },
        { "Isa", "Добрый день! \r\nЧем могу помочь?" },
        { "AndrewNovichenko", "норм \r\nкак ты это скрафтил? \r\nпоёдем калимбасика дадим \r\nили на зуб" },
        { "Jirinovskiy", "Это не Германия, это не Афганистан. \r\nДжордж, твоих солдат здесь порвут на части  \r\n250 тысяч отборных солдат Ирака!  \r\nОни все разнесут. " },
        { "PlayerItem", "что блять, это че за хуйня? \r\nкак ты это скрафтил? \r\nхарактер нормисный \r\nне женат" }
    };

    static ItemDescriptionMigration()
    {
        EditorApplication.delayCall += () => Apply(false);
    }

    [MenuItem("Tools/Ravine/Items/Migrate Descriptions From Classes")]
    private static void ApplyMenu() => Apply(true);

    private static void Apply(bool verbose)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        int changed = 0;
        InventoryItemInfo[] items = ItemPipelineValidator.LoadAll<InventoryItemInfo>();
        for (int i = 0; i < items.Length; i++)
        {
            InventoryItemInfo item = items[i];
            if (string.IsNullOrEmpty(item.id) || !string.IsNullOrEmpty(item.description)) continue;
            if (!Descriptions.TryGetValue(item.id, out string text)) continue;

            var so = new SerializedObject(item);
            so.FindProperty("_description").stringValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            changed++;
        }

        if (changed > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"[ItemDescriptionMigration] descriptions written to {changed} InventoryItemInfo assets");
        }
        else if (verbose)
        {
            Debug.Log("[ItemDescriptionMigration] nothing to migrate");
        }
    }
}

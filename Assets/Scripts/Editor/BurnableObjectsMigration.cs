using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TheRavine.ObjectControl;

public static class BurnableObjectsMigration
{
    private const string Tag = "[BurnableObjectsMigration]";

    [MenuItem("Tools/Ravine/World/Mark Plant Food As Burnable")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning($"{Tag} not available in play mode");
            return;
        }

        var seen = new HashSet<ObjectInfo>();
        int marked = 0;
        int total = 0;
        ObjectInfoRegistry[] registries = ItemPipelineValidator.LoadAll<ObjectInfoRegistry>();

        for (int r = 0; r < registries.Length; r++)
        {
            List<ObjectInfo> list = registries[r].objectInfos;
            for (int i = 0; i < list.Count; i++)
            {
                ObjectInfo info = list[i];
                if (info == null || !seen.Add(info)) continue;
                total++;
                if (info.ObjectCanBurn || !info.IsFood || info.FoodKind == FoodKind.Meat) continue;

                Undo.RecordObject(info, "Mark Object As Burnable");
                info.EditorAssignCanBurn(true);
                EditorUtility.SetDirty(info);
                marked++;
                Debug.Log($"{Tag} {info.name}: ObjectCanBurn", info);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} marked {marked} of {total} ObjectInfo assets");
    }
}

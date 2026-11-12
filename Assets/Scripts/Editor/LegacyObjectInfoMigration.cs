using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class LegacyObjectInfoMigration
{
    private const string Marker = "\n  _iType:";

    public static bool IsLegacy(ObjectInfo info)
    {
        if (info.ObjectPrefab != null || info.InventoryItemInfo != null) return false;
        string path = AssetDatabase.GetAssetPath(info);
        return !string.IsNullOrEmpty(path) && File.ReadAllText(path).Contains(Marker);
    }

    public static bool Migrate(ObjectInfo info)
    {
        string text = File.ReadAllText(AssetDatabase.GetAssetPath(info));
        var so = new SerializedObject(info);

        GameObject prefab = Resolve<GameObject>(text, "_prefab");
        InventoryItemInfo item = Resolve<InventoryItemInfo>(text, "_iteminfo");
        if (prefab != null) so.FindProperty("objectPrefab").objectReferenceValue = prefab;
        if (item != null) so.FindProperty("inventoryItemInfo").objectReferenceValue = item;
        if (TryInt(text, "_iType", out int instanceType)) so.FindProperty("instanceType").intValue = instanceType;
        if (TryInt(text, "_bType", out int behaviourType)) so.FindProperty("behaviourType").intValue = behaviourType;
        if (TryInt(text, "_poolSize", out int poolSize)) so.FindProperty("initialPoolSize").intValue = Mathf.Max(1, poolSize);
        if (TryInt(text, "_amount", out int amount)) so.FindProperty("defaultAmount").intValue = Mathf.Max(0, amount);

        bool changed = so.ApplyModifiedPropertiesWithoutUndo();
        if (changed) EditorUtility.SetDirty(info);
        return changed && (prefab != null || item != null);
    }

    private static bool TryInt(string text, string key, out int value)
    {
        Match match = Regex.Match(text, $@"^  {key}: (-?\d+)", RegexOptions.Multiline);
        value = 0;
        return match.Success && int.TryParse(match.Groups[1].Value, out value);
    }

    private static T Resolve<T>(string text, string key) where T : Object
    {
        Match match = Regex.Match(text, $@"^  {key}: \{{fileID: (-?\d+), guid: ([0-9a-f]{{32}})", RegexOptions.Multiline);
        if (!match.Success) return null;

        string path = AssetDatabase.GUIDToAssetPath(match.Groups[2].Value);
        if (string.IsNullOrEmpty(path)) return null;

        long fileId = long.Parse(match.Groups[1].Value);
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
            if (assets[i] is T typed && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(assets[i], out string _, out long local) && local == fileId)
                return typed;

        return AssetDatabase.LoadAssetAtPath<T>(path);
    }
}

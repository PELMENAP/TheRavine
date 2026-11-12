using System.IO;
using UnityEditor;
using UnityEngine;

public static class ItemPipelineStubs
{
    public const string Folder = "Assets/Prefabs/Stubs";
    public const string PrefabPath = Folder + "/MissingObjectStub.prefab";
    public const string IconPath = Folder + "/MissingIcon.png";
    private const int IconSize = 16;

    public static bool IsStub(GameObject prefab) => prefab != null && AssetDatabase.GetAssetPath(prefab) == PrefabPath;

    public static bool IsStub(Sprite sprite) => sprite != null && AssetDatabase.GetAssetPath(sprite) == IconPath;

    public static Sprite GetIcon()
    {
        Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        if (icon != null) return icon;

        EnsureFolder();
        var texture = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
        var magenta = new Color32(255, 0, 255, 255);
        var black = new Color32(0, 0, 0, 255);
        for (int y = 0; y < IconSize; y++)
            for (int x = 0; x < IconSize; x++)
                texture.SetPixel(x, y, ((x >> 2) + (y >> 2) & 1) == 0 ? magenta : black);
        File.WriteAllBytes(IconPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = IconSize;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
    }

    public static GameObject GetPrefab(Material spriteMaterial)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null) return prefab;

        Sprite icon = GetIcon();
        var root = new GameObject("MissingObjectStub");
        var view = new GameObject("View");
        view.transform.SetParent(root.transform, false);
        view.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        var renderer = view.AddComponent<SpriteRenderer>();
        renderer.sprite = icon;
        if (spriteMaterial != null) renderer.sharedMaterial = spriteMaterial;

        prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void EnsureFolder()
    {
        if (AssetDatabase.IsValidFolder(Folder)) return;
        AssetDatabase.CreateFolder(Path.GetDirectoryName(Folder).Replace('\\', '/'), Path.GetFileName(Folder));
    }
}

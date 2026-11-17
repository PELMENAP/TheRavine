using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using TheRavine.Generator;

public sealed class ItemWizardWindow : EditorWindow
{
    private const string DefaultItemFolder = "Assets/Scripts/ScriptableData/Item/ItemInfo/Environment";
    private const string DefaultProfileFolder = "Assets/Scripts/ObjectSystem/Map/WorldGeneration/ObjectGenerator/SpawnProfiles";
    private static readonly Regex IdPattern = new("^[A-Z][A-Za-z0-9_]*$");

    private string itemId = "";
    private string itemTitle = "";
    private string description = "";
    private GameObject prefab;
    private Sprite icon;
    private ObjectInfo template;
    private bool createProfile = true;
    private ObjectSpawnProfileSO profileTemplate;

    [MenuItem("Tools/Ravine/Items/New Item Wizard")]
    private static void Open() => GetWindow<ItemWizardWindow>("Item Wizard");

    private void OnGUI()
    {
        template = (ObjectInfo)EditorGUILayout.ObjectField("Template ObjectInfo", template, typeof(ObjectInfo), false);
        prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", prefab, typeof(GameObject), false);
        icon = (Sprite)EditorGUILayout.ObjectField("Icon", icon, typeof(Sprite), false);
        itemId = EditorGUILayout.TextField("Id", itemId);
        itemTitle = EditorGUILayout.TextField("Title", itemTitle);
        EditorGUILayout.LabelField("Description");
        description = EditorGUILayout.TextArea(description, GUILayout.MinHeight(60));
        createProfile = EditorGUILayout.Toggle("Create Spawn Profile", createProfile);
        if (createProfile)
            profileTemplate = (ObjectSpawnProfileSO)EditorGUILayout.ObjectField("Profile Template", profileTemplate, typeof(ObjectSpawnProfileSO), false);

        if (prefab != null && string.IsNullOrEmpty(itemId))
            itemId = ItemPipelineValidator.ToId(prefab.name);
        if (prefab != null && string.IsNullOrEmpty(itemTitle))
            itemTitle = prefab.name;

        string error = Check();
        if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);

        using (new EditorGUI.DisabledScope(error != null))
            if (GUILayout.Button("Create")) Create();
    }

    private string Check()
    {
        if (prefab == null) return "Prefab is required";
        if (!IdPattern.IsMatch(itemId)) return "Id must be PascalCase letters and digits without spaces";
        if (string.IsNullOrWhiteSpace(itemTitle)) return "Title is required";
        return null;
    }

    private static string FolderOf(Object asset, string fallback)
    {
        if (asset == null) return fallback;
        string path = AssetDatabase.GetAssetPath(asset);
        return string.IsNullOrEmpty(path) ? fallback : Path.GetDirectoryName(path).Replace('\\', '/');
    }

    private void Create()
    {
        ItemPipelineValidator.Database db = ItemPipelineValidator.Load();

        for (int i = 0; i < db.Items.Length; i++)
        {
            if (db.Items[i].id != itemId) continue;
            EditorUtility.DisplayDialog("Item Wizard", $"Item with id {itemId} already exists: {db.Items[i].name}", "OK");
            return;
        }
        if (db.ItemDatabases.Length != 1 || db.Registries.Length != 1)
        {
            EditorUtility.DisplayDialog("Item Wizard", "Expected exactly one DataItems and one ObjectInfoRegistry", "OK");
            return;
        }

        InventoryItemInfo templateItem = template != null ? template.InventoryItemInfo : null;
        string itemFolder = FolderOf(templateItem, DefaultItemFolder);

        InventoryItemInfo item = templateItem != null ? Instantiate(templateItem) : CreateInstance<InventoryItemInfo>();
        AssetDatabase.CreateAsset(item, AssetDatabase.GenerateUniqueAssetPath($"{itemFolder}/{itemTitle}.asset"));
        var itemSo = new SerializedObject(item);
        itemSo.FindProperty("_id").stringValue = itemId;
        itemSo.FindProperty("_title").stringValue = itemTitle;
        itemSo.FindProperty("_description").stringValue = description;
        itemSo.FindProperty("_prefab").objectReferenceValue = prefab;
        if (icon != null) itemSo.FindProperty("_spriteIcon").objectReferenceValue = icon;
        if (templateItem == null)
        {
            itemSo.FindProperty("_isPlaceable").boolValue = true;
            itemSo.FindProperty("_maxItemsInInventorySlot").intValue = 99;
        }
        itemSo.ApplyModifiedPropertiesWithoutUndo();

        ObjectInfo info = template != null ? Instantiate(template) : CreateInstance<ObjectInfo>();
        info.EditorAssignId(0);
        AssetDatabase.CreateAsset(info, AssetDatabase.GenerateUniqueAssetPath($"{FolderOf(template, ItemPipelineValidator.DefaultInfoFolder)}/{itemTitle}.asset"));
        var infoSo = new SerializedObject(info);
        infoSo.FindProperty("objectPrefab").objectReferenceValue = prefab;
        infoSo.FindProperty("inventoryItemInfo").objectReferenceValue = item;
        if (template == null) infoSo.FindProperty("instanceType").enumValueIndex = (int)InstanceType.Interactable;
        infoSo.ApplyModifiedPropertiesWithoutUndo();

        ItemPipelineValidator.AddToItemDatabase(db.ItemDatabases[0], item);
        ItemPipelineValidator.AddToRegistry(db.Registries[0], info);
        db.Registries[0].EditorAssignMissingIds();

        if (createProfile)
        {
            ObjectSpawnProfileSO profile = profileTemplate != null ? Instantiate(profileTemplate) : CreateInstance<ObjectSpawnProfileSO>();
            profile.objectInfo = info;
            AssetDatabase.CreateAsset(profile, AssetDatabase.GenerateUniqueAssetPath($"{FolderOf(profileTemplate, DefaultProfileFolder)}/{itemId}SpawnProfile.asset"));
            if (db.Generators.Length == 1) ItemPipelineValidator.AddToGenerator(db.Generators[0], profile);
            else Debug.LogWarning($"[ItemWizard] {db.Generators.Length} MapGenerator prefabs found, add {profile.name} to spawnProfiles manually", profile);
        }

        AssetDatabase.SaveAssets();
        Selection.activeObject = info;
        Debug.Log($"[ItemWizard] created {itemId}: item {item.name}, object {info.name} (id {info.Id})", info);

        itemId = "";
        itemTitle = "";
        description = "";
        prefab = null;
        icon = null;
    }
}

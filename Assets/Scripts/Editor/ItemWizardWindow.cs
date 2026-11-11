using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using TheRavine.Generator;

public sealed class ItemWizardWindow : EditorWindow
{
    private const string DefaultItemFolder = "Assets/Scripts/ScriptableData/Item/ItemInfo/Environment";
    private const string DefaultInfoFolder = "Assets/Scripts/ObjectSystem/ObjectInfo";
    private const string DefaultProfileFolder = "Assets/Scripts/ObjectSystem/Map/WorldGeneration/ObjectGenerator/SpawnProfiles";
    private static readonly Regex ClassNamePattern = new("^[A-Z][A-Za-z0-9_]*$");

    private string className = "";
    private string title = "";
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
        className = EditorGUILayout.TextField("Class / id", className);
        title = EditorGUILayout.TextField("Title", title);
        EditorGUILayout.LabelField("Description");
        description = EditorGUILayout.TextArea(description, GUILayout.MinHeight(60));
        createProfile = EditorGUILayout.Toggle("Create Spawn Profile", createProfile);
        if (createProfile)
            profileTemplate = (ObjectSpawnProfileSO)EditorGUILayout.ObjectField("Profile Template", profileTemplate, typeof(ObjectSpawnProfileSO), false);

        if (prefab != null && string.IsNullOrEmpty(className))
            className = ToClassName(prefab.name);
        if (prefab != null && string.IsNullOrEmpty(title))
            title = prefab.name;

        string error = Check();
        if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);

        using (new EditorGUI.DisabledScope(error != null))
            if (GUILayout.Button("Create")) Create();
    }

    private static string ToClassName(string source)
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

    private string Check()
    {
        if (prefab == null) return "Prefab is required";
        if (!ClassNamePattern.IsMatch(className)) return "Class / id must be a PascalCase C# identifier without spaces";
        if (string.IsNullOrWhiteSpace(title)) return "Title is required";
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
            if (db.Items[i].id != className) continue;
            EditorUtility.DisplayDialog("Item Wizard", $"Item with id {className} already exists: {db.Items[i].name}", "OK");
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
        AssetDatabase.CreateAsset(item, AssetDatabase.GenerateUniqueAssetPath($"{itemFolder}/{title}.asset"));
        var itemSo = new SerializedObject(item);
        itemSo.FindProperty("_id").stringValue = className;
        itemSo.FindProperty("_title").stringValue = title;
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
        AssetDatabase.CreateAsset(info, AssetDatabase.GenerateUniqueAssetPath($"{FolderOf(template, DefaultInfoFolder)}/{title}.asset"));
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
            AssetDatabase.CreateAsset(profile, AssetDatabase.GenerateUniqueAssetPath($"{FolderOf(profileTemplate, DefaultProfileFolder)}/{className}SpawnProfile.asset"));
            if (db.Generators.Length == 1) ItemPipelineValidator.AddToGenerator(db.Generators[0], profile);
            else Debug.LogWarning($"[ItemWizard] {db.Generators.Length} MapGenerator prefabs found, add {profile.name} to spawnProfiles manually", profile);
        }

        if (!db.ItemTypes.ContainsKey(className))
        {
            string classPath = $"{itemFolder}/{className}.cs";
            File.WriteAllText(classPath, BuildClass(className, description));
            AssetDatabase.ImportAsset(classPath);
        }

        AssetDatabase.SaveAssets();
        Selection.activeObject = info;
        Debug.Log($"[ItemWizard] created {className}: item {item.name}, object {info.name} (id {info.Id})", info);

        className = "";
        title = "";
        description = "";
        prefab = null;
        icon = null;
    }

    private static string BuildClass(string name, string text)
    {
        string escaped = text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r\n", "\n").Replace("\n", "\\r\\n");
        return
$@"namespace TheRavine.Inventory
{{
    public class {name} : ItemClass
    {{
        public string description = ""{escaped}"";
        public {name}(IInventoryItemInfo info) : base(info)
        {{
            this.info.description = description;
        }}

        public override IInventoryItem Clone()
        {{
            var clonedItem = new {name}(this.info);
            clonedItem.CopyFrom(this);
            return clonedItem;
        }}
    }}
}}
";
    }
}

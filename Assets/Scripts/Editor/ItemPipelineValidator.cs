using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TheRavine.Generator;
using TheRavine.ObjectControl;

[InitializeOnLoad]
public static partial class ItemPipelineValidator
{
    private const string Tag = "[ItemValidator]";
    private const string ValidateOnPlayKey = "TheRavine.ItemValidator.OnPlay";
    private const string ValidateOnPlayMenu = "Tools/Ravine/Items/Validate On Play";
    public const string DefaultInfoFolder = "Assets/Scripts/ObjectSystem/ObjectInfo";
    private const int DefaultStackSize = 99;

    public sealed class Report
    {
        public int Errors;
        public int Warnings;
        public int FixedCount;
        public bool Changed;

        public void Error(string message, UnityEngine.Object context)
        {
            Errors++;
            Debug.LogError($"{Tag} {message}", context);
        }

        public void Warn(string message, UnityEngine.Object context)
        {
            Warnings++;
            Debug.LogWarning($"{Tag} {message}", context);
        }

        public void Fixed(string message, UnityEngine.Object context)
        {
            FixedCount++;
            Changed = true;
            if (context != null) EditorUtility.SetDirty(context);
            Debug.Log($"{Tag} fixed: {message}", context);
        }
    }

    public sealed class Database
    {
        public InventoryItemInfo[] Items;
        public ObjectInfo[] Infos;
        public DataItems[] ItemDatabases;
        public ObjectInfoRegistry[] Registries;
        public ObjectSpawnProfileSO[] Profiles;
        public InventoryCraftInfo[] Crafts;
        public MapGenerator[] Generators;
        public Dictionary<string, Type> ItemTypes;
    }

    static ItemPipelineValidator()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.ExitingEditMode || !EditorPrefs.GetBool(ValidateOnPlayKey, true)) return;
        Report report = Run(false);
        if (report.Errors > 0)
            Debug.LogError($"{Tag} {report.Errors} errors before entering play mode. Tools/Ravine/Items/Validate And Fix may resolve some of them.");
    }

    [MenuItem("Tools/Ravine/Items/Validate")]
    private static void ValidateMenu() => Run(false);

    [MenuItem("Tools/Ravine/Items/Validate And Fix")]
    private static void FixMenu() => Run(true);

    [MenuItem("Tools/Ravine/Items/Add Unused Spawn Profiles To MapGenerator")]
    private static void AddUnusedProfilesMenu()
    {
        Database db = Load();
        if (db.Generators.Length != 1)
        {
            Debug.LogError($"{Tag} expected one MapGenerator prefab, found {db.Generators.Length}");
            return;
        }

        int added = 0;
        for (int i = 0; i < db.Profiles.Length; i++)
        {
            if (InAnyGenerator(db, db.Profiles[i])) continue;
            AddToGenerator(db.Generators[0], db.Profiles[i]);
            Debug.Log($"{Tag} {db.Profiles[i].name} added to {AssetDatabase.GetAssetPath(db.Generators[0])}", db.Profiles[i]);
            added++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} {added} spawn profiles added");
    }

    [MenuItem(ValidateOnPlayMenu)]
    private static void ToggleValidateOnPlay() => EditorPrefs.SetBool(ValidateOnPlayKey, !EditorPrefs.GetBool(ValidateOnPlayKey, true));

    [MenuItem(ValidateOnPlayMenu, true)]
    private static bool ToggleValidateOnPlayCheck()
    {
        Menu.SetChecked(ValidateOnPlayMenu, EditorPrefs.GetBool(ValidateOnPlayKey, true));
        return true;
    }

    public static Report Run(bool fix)
    {
        var report = new Report();
        Database db = Load();

        if (db.ItemDatabases.Length == 0) report.Error("no DataItems asset found", null);
        if (db.Registries.Length == 0) report.Error("no ObjectInfoRegistry asset found", null);
        if (db.Generators.Length == 0) report.Warn("no prefab with MapGenerator found, spawn profiles are not checked against generator", null);

        MigrateLegacyInfos(db, report, fix);
        ValidateItemDatabases(db, report, fix);
        ValidateObjectInfos(db, report, fix);
        ValidateRegistries(db, report, fix);
        ValidateItems(db, report, fix);
        ValidateReferences(db, report);
        ValidateProfiles(db, report);
        ValidateCrafts(db, report);

        if (report.Changed) AssetDatabase.SaveAssets();

        string summary = $"{Tag} {db.Items.Length} items, {db.Infos.Length} object infos, {db.Profiles.Length} spawn profiles: {report.Errors} errors, {report.Warnings} warnings, {report.FixedCount} fixed";
        if (report.Errors > 0) Debug.LogError(summary);
        else if (report.Warnings > 0) Debug.LogWarning(summary);
        else Debug.Log(summary);

        return report;
    }

    public static Database Load()
    {
        var db = new Database
        {
            Items = LoadAll<InventoryItemInfo>(),
            Infos = LoadAll<ObjectInfo>(),
            ItemDatabases = LoadAll<DataItems>(),
            Registries = LoadAll<ObjectInfoRegistry>(),
            Profiles = LoadAll<ObjectSpawnProfileSO>(),
            Crafts = LoadAll<InventoryCraftInfo>(),
            Generators = LoadGenerators(),
            ItemTypes = new Dictionary<string, Type>(StringComparer.Ordinal)
        };

        foreach (Type type in TypeCache.GetTypesDerivedFrom<IInventoryItem>())
        {
            if (type.IsAbstract || type.IsInterface) continue;
            if (!db.ItemTypes.TryAdd(type.Name, type))
                Debug.LogError($"{Tag} two item classes share name {type.Name}: {db.ItemTypes[type.Name].FullName} and {type.FullName}. InventoryTypeCache keys by short name.");
        }

        return db;
    }

    public static T[] LoadAll<T>() where T : UnityEngine.Object
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        var result = new List<T>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (asset != null) result.Add(asset);
        }
        return result.ToArray();
    }

    public static MapGenerator[] LoadGenerators()
    {
        string scriptPath = null;
        foreach (string guid in AssetDatabase.FindAssets("MapGenerator t:MonoScript"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (script != null && script.GetClass() == typeof(MapGenerator))
            {
                scriptPath = path;
                break;
            }
        }

        var result = new List<MapGenerator>();
        if (scriptPath == null) return result.ToArray();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Array.IndexOf(AssetDatabase.GetDependencies(path, false), scriptPath) < 0) continue;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            MapGenerator generator = prefab.GetComponentInChildren<MapGenerator>(true);
            if (generator != null && !PrefabUtility.IsPartOfPrefabInstance(generator)) result.Add(generator);
        }
        return result.ToArray();
    }

    private static bool Contains<T>(T[] array, T value) where T : class => Array.IndexOf(array, value) >= 0;

    private static bool InAnyItemDatabase(Database db, InventoryItemInfo item)
    {
        for (int i = 0; i < db.ItemDatabases.Length; i++)
            if (db.ItemDatabases[i].data != null && db.ItemDatabases[i].data.Contains(item)) return true;
        return false;
    }

    private static bool InAnyRegistry(Database db, ObjectInfo info)
    {
        for (int i = 0; i < db.Registries.Length; i++)
            if (db.Registries[i].objectInfos.Contains(info)) return true;
        return false;
    }

    private static bool InAnyGenerator(Database db, ObjectSpawnProfileSO profile)
    {
        for (int i = 0; i < db.Generators.Length; i++)
        {
            ObjectSpawnProfileSO[] profiles = db.Generators[i].Settings?.spawnProfiles;
            if (profiles != null && Contains(profiles, profile)) return true;
        }
        return false;
    }

    public static void AddToItemDatabase(DataItems target, InventoryItemInfo item)
    {
        var so = new SerializedObject(target);
        SerializedProperty list = so.FindProperty("_data");
        int index = list.arraySize;
        list.InsertArrayElementAtIndex(index);
        list.GetArrayElementAtIndex(index).objectReferenceValue = item;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    public static void AddToRegistry(ObjectInfoRegistry registry, ObjectInfo info)
    {
        Undo.RecordObject(registry, "Register ObjectInfo");
        registry.objectInfos.Add(info);
        EditorUtility.SetDirty(registry);
    }

    public static void AddToGenerator(MapGenerator generator, ObjectSpawnProfileSO profile)
    {
        var so = new SerializedObject(generator);
        SerializedProperty list = so.FindProperty("chunkGenerationSettings.spawnProfiles");
        int index = list.arraySize;
        list.InsertArrayElementAtIndex(index);
        list.GetArrayElementAtIndex(index).objectReferenceValue = profile;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(generator);
        PrefabUtility.SavePrefabAsset(generator.transform.root.gameObject);
    }
}

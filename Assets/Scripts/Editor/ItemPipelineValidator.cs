using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TheRavine.Generator;
using TheRavine.ObjectControl;

[InitializeOnLoad]
public static class ItemPipelineValidator
{
    private const string Tag = "[ItemValidator]";
    private const string ValidateOnPlayKey = "TheRavine.ItemValidator.OnPlay";
    private const string ValidateOnPlayMenu = "Tools/Ravine/Items/Validate On Play";

    public sealed class Report
    {
        public int Errors;
        public int Warnings;
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

        ValidateItemDatabases(db, report, fix);
        ValidateRegistries(db, report, fix);
        ValidateItems(db, report, fix);
        ValidateObjectInfos(db, report);
        ValidateProfiles(db, report, fix);
        ValidateCrafts(db, report);

        if (report.Changed) AssetDatabase.SaveAssets();

        string summary = $"{Tag} {db.Items.Length} items, {db.Infos.Length} object infos, {db.Profiles.Length} spawn profiles: {report.Errors} errors, {report.Warnings} warnings";
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
            if (generator != null) result.Add(generator);
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

                report.Error($"{database.name}: entry {i} is {(item == null ? "missing" : $"duplicate of {item.name}")}", database);
                if (!fix) continue;
                list.GetArrayElementAtIndex(i).objectReferenceValue = null;
                list.DeleteArrayElementAtIndex(i);
                report.Changed = true;
            }

            if (so.ApplyModifiedProperties()) EditorUtility.SetDirty(database);
        }
    }

    private static void ValidateRegistries(Database db, Report report, bool fix)
    {
        for (int r = 0; r < db.Registries.Length; r++)
        {
            ObjectInfoRegistry registry = db.Registries[r];
            var seen = new HashSet<ObjectInfo>();

            for (int i = registry.objectInfos.Count - 1; i >= 0; i--)
            {
                ObjectInfo info = registry.objectInfos[i];
                if (info != null && seen.Add(info)) continue;

                report.Error($"{registry.name}: entry {i} is {(info == null ? "missing" : $"duplicate of {info.name}")}", registry);
                if (!fix) continue;
                Undo.RecordObject(registry, "Clean Registry");
                registry.objectInfos.RemoveAt(i);
                EditorUtility.SetDirty(registry);
                report.Changed = true;
            }
        }

        for (int i = 0; i < db.Infos.Length; i++)
        {
            ObjectInfo info = db.Infos[i];
            if (InAnyRegistry(db, info)) continue;

            report.Error($"{info.name}: not in ObjectInfoRegistry, it will never spawn or be picked up", info);
            if (!fix || db.Registries.Length != 1) continue;
            AddToRegistry(db.Registries[0], info);
            report.Changed = true;
        }

        for (int r = 0; r < db.Registries.Length; r++)
        {
            ObjectInfoRegistry registry = db.Registries[r];
            var used = new Dictionary<int, ObjectInfo>();
            bool broken = false;

            for (int i = 0; i < registry.objectInfos.Count; i++)
            {
                ObjectInfo info = registry.objectInfos[i];
                if (info == null) continue;

                if (info.Id == ObjectInfoRegistry.NoneId || info.Id >= ObjectInfoRegistry.FirstReservedId)
                {
                    report.Error($"{info.name}: invalid id {info.Id}", info);
                    broken = true;
                }
                else if (!used.TryAdd(info.Id, info))
                {
                    report.Error($"{info.name}: id {info.Id} already used by {used[info.Id].name}", info);
                    broken = true;
                }
            }

            if (!broken || !fix) continue;
            registry.EditorAssignMissingIds();
            report.Changed = true;
        }
    }

    private static void ValidateItems(Database db, Report report, bool fix)
    {
        var byId = new Dictionary<string, InventoryItemInfo>(StringComparer.Ordinal);
        var placeablePrefabs = new HashSet<GameObject>();
        for (int i = 0; i < db.Infos.Length; i++)
            if (db.Infos[i].ObjectPrefab != null && InAnyRegistry(db, db.Infos[i]))
                placeablePrefabs.Add(db.Infos[i].ObjectPrefab);

        var itemsWithAsset = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < db.Items.Length; i++)
        {
            InventoryItemInfo item = db.Items[i];

            if (string.IsNullOrEmpty(item.id))
            {
                report.Error($"{item.name}: empty id", item);
            }
            else
            {
                itemsWithAsset.Add(item.id);

                if (!byId.TryAdd(item.id, item))
                    report.Error($"{item.name}: id {item.id} already used by {byId[item.id].name}", item);

                if (item.id.Trim() != item.id || item.id.IndexOf(' ') >= 0)
                    report.Error($"{item.name}: id '{item.id}' contains spaces", item);
            }

            if (string.IsNullOrWhiteSpace(item.title)) report.Warn($"{item.name}: empty title", item);
            if (string.IsNullOrWhiteSpace(item.description)) report.Warn($"{item.name}: empty description", item);
            if (item.maxItemsInInventorySlot <= 0) report.Error($"{item.name}: maxItemsInInventorySlot = {item.maxItemsInInventorySlot}", item);
            if (item.spriteIcon == null) report.Warn($"{item.name}: no icon", item);

            if (item.isPlaceable)
            {
                if (item.prefab == null)
                    report.Error($"{item.name}: placeable without prefab", item);
                else if (!placeablePrefabs.Contains(item.prefab))
                    report.Error($"{item.name}: prefab {item.prefab.name} is not used by any registered ObjectInfo, placement will fail", item);
            }

            if (InAnyItemDatabase(db, item)) continue;

            report.Error($"{item.name}: not in DataItems, InfoManager will not find it", item);
            if (!fix || db.ItemDatabases.Length != 1) continue;
            AddToItemDatabase(db.ItemDatabases[0], item);
            report.Changed = true;
        }

        foreach (KeyValuePair<string, Type> pair in db.ItemTypes)
            if (pair.Value != typeof(TheRavine.Inventory.DefaultItem) && !itemsWithAsset.Contains(pair.Key))
                report.Warn($"class {pair.Value.FullName} has no InventoryItemInfo with id {pair.Key}", null);
    }

    private static void ValidateObjectInfos(Database db, Report report)
    {
        var byPrefab = new Dictionary<GameObject, ObjectInfo>();

        for (int i = 0; i < db.Infos.Length; i++)
        {
            ObjectInfo info = db.Infos[i];

            if (info.ObjectPrefab == null)
            {
                report.Error($"{info.name}: no objectPrefab", info);
            }
            else if (!byPrefab.TryAdd(info.ObjectPrefab, info))
            {
                report.Warn($"{info.name}: prefab {info.ObjectPrefab.name} is shared with {byPrefab[info.ObjectPrefab].name}, prefab -> id lookup picks the first one", info);
            }

            InventoryItemInfo item = info.InventoryItemInfo;
            if (info.InstanceType == InstanceType.Interactable && item == null)
                report.Warn($"{info.name}: interactable without InventoryItemInfo, pick up gives nothing", info);

            if (item != null && item.isPlaceable && item.prefab != null && info.ObjectPrefab != null && item.prefab != info.ObjectPrefab)
                report.Warn($"{info.name}: item {item.name} places {item.prefab.name}, but this object uses {info.ObjectPrefab.name}", info);

            if (info.BehaviourType == BehaviourType.NAL && info.NalInfo == null)
                report.Warn($"{info.name}: NAL behaviour without nalInfo", info);

            if (info.BehaviourType == BehaviourType.GROW && info.EvolutionStep != null && !InAnyRegistry(db, info.EvolutionStep))
                report.Error($"{info.name}: evolutionStep {info.EvolutionStep.name} is not registered", info);

            CheckPattern(db, report, info, info.OnDeathPattern, "onDeathPattern");
            CheckPattern(db, report, info, info.OnPickUpPattern, "onPickUpPattern");
        }
    }

    private static void CheckPattern(Database db, Report report, ObjectInfo owner, SpreadPattern pattern, string field)
    {
        if (pattern == null) return;
        if (pattern.main != null && !InAnyRegistry(db, pattern.main))
            report.Error($"{owner.name}: {field}.main {pattern.main.name} is not registered", owner);
        if (pattern.other == null) return;
        for (int k = 0; k < pattern.other.Length; k++)
        {
            if (pattern.other[k] == null)
                report.Warn($"{owner.name}: {field}.other[{k}] is empty", owner);
            else if (!InAnyRegistry(db, pattern.other[k]))
                report.Error($"{owner.name}: {field}.other[{k}] {pattern.other[k].name} is not registered", owner);
        }
    }

    private static void ValidateProfiles(Database db, Report report, bool fix)
    {
        for (int g = 0; g < db.Generators.Length; g++)
        {
            ObjectSpawnProfileSO[] profiles = db.Generators[g].Settings?.spawnProfiles;
            if (profiles == null) continue;
            var seen = new HashSet<ObjectSpawnProfileSO>();
            for (int i = 0; i < profiles.Length; i++)
            {
                if (profiles[i] == null) report.Warn($"{db.Generators[g].name}: spawnProfiles[{i}] is empty", db.Generators[g]);
                else if (!seen.Add(profiles[i])) report.Warn($"{db.Generators[g].name}: {profiles[i].name} listed twice", db.Generators[g]);
            }
        }

        for (int i = 0; i < db.Profiles.Length; i++)
        {
            ObjectSpawnProfileSO profile = db.Profiles[i];

            if (profile.objectInfo == null)
                report.Error($"{profile.name}: no objectInfo, profile is skipped by SpawnConfigBaker", profile);
            else if (!InAnyRegistry(db, profile.objectInfo))
                report.Error($"{profile.name}: objectInfo {profile.objectInfo.name} is not registered", profile);

            if (profile.density <= 0f) report.Warn($"{profile.name}: density {profile.density}", profile);
            if (profile.mask.heightMin >= profile.mask.heightMax || profile.mask.tempMin >= profile.mask.tempMax || profile.mask.moistMin >= profile.mask.moistMax)
                report.Warn($"{profile.name}: empty height/temp/moist range", profile);

            for (int j = 0; j < i; j++)
            {
                ObjectSpawnProfileSO other = db.Profiles[j];
                if (other.layer == profile.layer && other.mask.Equals(profile.mask))
                    report.Warn($"{profile.name}: same layer and mask as {other.name}, both compete for the same noise spots", profile);
            }

            if (db.Generators.Length == 0 || InAnyGenerator(db, profile)) continue;

            report.Error($"{profile.name}: not in MapGenerator.chunkGenerationSettings.spawnProfiles, object will not spawn", profile);
            if (!fix || db.Generators.Length != 1) continue;
            AddToGenerator(db.Generators[0], profile);
            report.Changed = true;
        }
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

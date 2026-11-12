using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using TheRavine.Generator;
using TheRavine.ObjectControl;

public static partial class ItemPipelineValidator
{
    private const int MaxLineageDepth = 32;

    private static void MigrateLegacyInfos(Database db, Report report, bool fix)
    {
        for (int i = 0; i < db.Infos.Length; i++)
        {
            ObjectInfo info = db.Infos[i];
            if (!LegacyObjectInfoMigration.IsLegacy(info)) continue;

            if (fix && LegacyObjectInfoMigration.Migrate(info))
                report.Fixed($"{info.name}: restored prefab {NameOf(info.ObjectPrefab)} and item {NameOf(info.InventoryItemInfo)} from legacy fields", info);
            else
                report.Error($"{info.name}: saved in an old ObjectInfo format (_prefab, _iteminfo, _iType), current fields are empty. Validate And Fix restores them", info);
        }
    }

    private static string NameOf(Object asset) => asset != null ? asset.name : "none";

    private static Material ReferenceSpriteMaterial(Database db)
    {
        for (int i = 0; i < db.Infos.Length; i++)
        {
            GameObject prefab = db.Infos[i].ObjectPrefab;
            if (prefab == null || ItemPipelineStubs.IsStub(prefab)) continue;
            SpriteRenderer renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer != null && renderer.sharedMaterial != null) return renderer.sharedMaterial;
        }
        return null;
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

                string message = $"{registry.name}: entry {i} is {(info == null ? "missing" : $"duplicate of {info.name}")}";
                if (!fix)
                {
                    report.Error(message, registry);
                    continue;
                }
                Undo.RecordObject(registry, "Clean Registry");
                registry.objectInfos.RemoveAt(i);
                report.Fixed($"{message}, removed", registry);
            }
        }

        for (int i = 0; i < db.Infos.Length; i++)
        {
            ObjectInfo info = db.Infos[i];
            if (InAnyRegistry(db, info)) continue;

            if (fix && db.Registries.Length == 1)
            {
                AddToRegistry(db.Registries[0], info);
                report.Fixed($"{info.name}: added to {db.Registries[0].name}", info);
            }
            else report.Error($"{info.name}: not in ObjectInfoRegistry, it will never spawn or be picked up", info);
        }

        for (int r = 0; r < db.Registries.Length; r++)
        {
            ObjectInfoRegistry registry = db.Registries[r];
            var used = new Dictionary<int, ObjectInfo>();
            var broken = new List<ObjectInfo>();

            for (int i = 0; i < registry.objectInfos.Count; i++)
            {
                ObjectInfo info = registry.objectInfos[i];
                if (info == null) continue;

                if (info.Id == ObjectInfoRegistry.NoneId || info.Id >= ObjectInfoRegistry.FirstReservedId || !used.TryAdd(info.Id, info))
                    broken.Add(info);
            }

            if (broken.Count == 0) continue;

            if (!fix)
            {
                for (int i = 0; i < broken.Count; i++)
                    report.Error($"{broken[i].name}: id {broken[i].Id} is missing, reserved or already used", broken[i]);
                continue;
            }

            registry.EditorAssignMissingIds();
            for (int i = 0; i < broken.Count; i++)
                report.Fixed($"{broken[i].name}: id assigned {broken[i].Id}", broken[i]);
        }
    }

    private static void ValidateObjectInfos(Database db, Report report, bool fix)
    {
        var byPrefab = new Dictionary<GameObject, ObjectInfo>();

        for (int i = 0; i < db.Infos.Length; i++)
        {
            ObjectInfo info = db.Infos[i];
            InventoryItemInfo item = info.InventoryItemInfo;

            if (info.ObjectPrefab == null)
            {
                if (fix)
                {
                    bool fromItem = item != null && item.prefab != null;
                    GameObject prefab = fromItem ? item.prefab : ItemPipelineStubs.GetPrefab(ReferenceSpriteMaterial(db));
                    var so = new SerializedObject(info);
                    so.FindProperty("objectPrefab").objectReferenceValue = prefab;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    report.Fixed($"{info.name}: no objectPrefab, {(fromItem ? $"prefab of item {item.name}" : "placeholder prefab")} assigned", info);
                }
                else report.Error($"{info.name}: no objectPrefab", info);
            }

            if (info.ObjectPrefab != null)
            {
                if (ItemPipelineStubs.IsStub(info.ObjectPrefab))
                    report.Warn($"{info.name}: placeholder prefab", info);
                else if (!byPrefab.TryAdd(info.ObjectPrefab, info))
                    report.Warn($"{info.name}: prefab {info.ObjectPrefab.name} is shared with {byPrefab[info.ObjectPrefab].name}, prefab -> id lookup picks the first one", info);
            }

            if (info.InstanceType == InstanceType.Interactable && item == null)
                report.Warn($"{info.name}: interactable without InventoryItemInfo, pick up gives nothing", info);

            if (item != null && item.isPlaceable && item.prefab != null && info.ObjectPrefab != null && item.prefab != info.ObjectPrefab)
                report.Warn($"{info.name}: item {item.name} places {item.prefab.name}, but this object uses {info.ObjectPrefab.name}", info);

            if (info.BehaviourType == BehaviourType.NAL && info.NalInfo == null)
                report.Warn($"{info.name}: NAL behaviour without nalInfo", info);

        }
    }

    private static void ValidateReferences(Database db, Report report)
    {
        for (int i = 0; i < db.Infos.Length; i++)
        {
            ObjectInfo info = db.Infos[i];
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

    private static bool EvolvesInto(ObjectInfo from, ObjectInfo to)
    {
        ObjectInfo step = from;
        for (int depth = 0; step != null && depth < MaxLineageDepth; depth++)
        {
            if (step == to) return true;
            step = step.EvolutionStep;
        }
        return false;
    }

    private static bool SameLineage(ObjectInfo a, ObjectInfo b) =>
        a != null && b != null && (EvolvesInto(a, b) || EvolvesInto(b, a));

    private static string GeneratorPaths(Database db)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < db.Generators.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            ObjectSpawnProfileSO[] profiles = db.Generators[i].Settings?.spawnProfiles;
            sb.Append(AssetDatabase.GetAssetPath(db.Generators[i])).Append(" (").Append(profiles?.Length ?? 0).Append(" profiles)");
        }
        return sb.ToString();
    }

    private static void ValidateProfiles(Database db, Report report)
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

        string generators = null;

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
                if (other.layer != profile.layer || !other.mask.Equals(profile.mask) || SameLineage(other.objectInfo, profile.objectInfo)) continue;
                report.Warn($"{profile.name}: same layer and mask as {other.name}, both compete for the same noise spots", profile);
            }

            if (db.Generators.Length == 0 || InAnyGenerator(db, profile)) continue;

            generators ??= GeneratorPaths(db);
            report.Warn($"{profile.name}: not in spawnProfiles of {generators}, object will not spawn. If unintended: Tools/Ravine/Items/Add Unused Spawn Profiles To MapGenerator", profile);
        }
    }
}

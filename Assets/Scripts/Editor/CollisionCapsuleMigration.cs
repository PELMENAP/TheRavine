using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TheRavine.Generator;
using TheRavine.ObjectControl;

public static class CollisionCapsuleMigration
{
    private const string Tag = "[CollisionCapsuleMigration]";
    private const float VerticalDot = 0.999f;

    private struct Shape
    {
        public float Radius;
        public float Height;
        public Vector3 OffsetSteps;
    }

    private struct Plan
    {
        public ObjectInfo Info;
        public GameObject Prefab;
        public Shape Shape;
    }

    [MenuItem("Tools/Ravine/World/Convert Capsule Colliders To Collision Shapes")]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning($"{Tag} not available in play mode");
            return;
        }

        if (!TryReadSteps(out float buildStep, out float levelHeight)) return;

        var infos = CollectInfos();
        var plans = new List<Plan>(infos.Count);
        var skipped = new List<string>();
        var byPrefab = new Dictionary<GameObject, Shape>();

        for (int i = 0; i < infos.Count; i++)
            Inspect(infos[i], buildStep, levelHeight, byPrefab, plans, skipped);

        int converted = Apply(plans);

        if (skipped.Count > 0)
            Debug.Log($"{Tag} skipped {skipped.Count}:\n{string.Join("\n", skipped)}");
        Debug.Log($"{Tag} converted {converted} of {infos.Count} ObjectInfo assets, buildStep {buildStep}, levelHeight {levelHeight}");
    }

    private static bool TryReadSteps(out float buildStep, out float levelHeight)
    {
        buildStep = 0f;
        levelHeight = 0f;

        MapGenerator[] generators = ItemPipelineValidator.LoadGenerators();
        if (generators.Length == 0 || generators[0].Settings == null)
        {
            Debug.LogError($"{Tag} no prefab with a MapGenerator and ChunkGenerationSettings found, nothing converted");
            return false;
        }

        buildStep = generators[0].Settings.buildStep;
        levelHeight = generators[0].Settings.levelHeight;

        for (int i = 1; i < generators.Length; i++)
        {
            ChunkGenerationSettings other = generators[i].Settings;
            if (other != null && (other.buildStep != buildStep || other.levelHeight != levelHeight))
                Debug.LogWarning($"{Tag} {generators[i].name} has buildStep {other.buildStep} and levelHeight {other.levelHeight}, values of {generators[0].name} are used", generators[i]);
        }
        return true;
    }

    private static List<ObjectInfo> CollectInfos()
    {
        var result = new List<ObjectInfo>();
        var seen = new HashSet<ObjectInfo>();
        ObjectInfoRegistry[] registries = ItemPipelineValidator.LoadAll<ObjectInfoRegistry>();

        for (int r = 0; r < registries.Length; r++)
        {
            List<ObjectInfo> list = registries[r].objectInfos;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && seen.Add(list[i])) result.Add(list[i]);
        }
        return result;
    }

    private static void Inspect(ObjectInfo info, float buildStep, float levelHeight, Dictionary<GameObject, Shape> byPrefab,
        List<Plan> plans, List<string> skipped)
    {
        GameObject prefab = info.ObjectPrefab;
        if (prefab == null)
        {
            skipped.Add($"{info.name}: no prefab");
            return;
        }

        CapsuleCollider[] capsules = prefab.GetComponentsInChildren<CapsuleCollider>(true);
        if (capsules.Length == 0)
        {
            skipped.Add($"{info.name}: prefab {prefab.name} has no capsule");
            return;
        }

        if (info.CollisionKind != CollisionKind.None)
        {
            skipped.Add($"{info.name}: shape already set, capsule stays in {prefab.name}");
            return;
        }

        if (byPrefab.TryGetValue(prefab, out Shape shared))
        {
            plans.Add(new Plan { Info = info, Prefab = prefab, Shape = shared });
            return;
        }

        if (capsules.Length > 1)
        {
            skipped.Add($"{info.name}: {capsules.Length} capsules in {prefab.name}, set the shape by hand");
            return;
        }

        CapsuleCollider capsule = capsules[0];
        if (capsule.isTrigger)
        {
            skipped.Add($"{info.name}: trigger collider in {prefab.name}");
            return;
        }

        if (PrefabUtility.GetCorrespondingObjectFromSource(capsule) != null)
        {
            skipped.Add($"{info.name}: capsule of {prefab.name} comes from another prefab, convert it in the source prefab");
            return;
        }

        if (!TryMeasure(capsule, buildStep, levelHeight, out Shape shape))
        {
            skipped.Add($"{info.name}: non-vertical capsule in {prefab.name}");
            return;
        }

        byPrefab.Add(prefab, shape);
        plans.Add(new Plan { Info = info, Prefab = prefab, Shape = shape });
    }

    private static bool TryMeasure(CapsuleCollider capsule, float buildStep, float levelHeight, out Shape shape)
    {
        shape = default;

        Transform root = capsule.transform.root;
        Transform t = capsule.transform;
        int axis = capsule.direction;
        Vector3 axisLocal = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
        Vector3 axisInRoot = Quaternion.Inverse(root.rotation) * (t.rotation * axisLocal);
        if (Mathf.Abs(axisInRoot.y) < VerticalDot) return false;

        Vector3 scale = t.lossyScale;
        float axisScale = Mathf.Abs(scale[axis]);
        float radiusScale = Mathf.Max(Mathf.Abs(scale[(axis + 1) % 3]), Mathf.Abs(scale[(axis + 2) % 3]));

        float radius = capsule.radius * radiusScale;
        float height = Mathf.Max(capsule.height * axisScale, 2f * radius);
        if (radius <= 0f || height <= 0f) return false;

        Vector3 center = Vector3.Scale(root.InverseTransformPoint(t.TransformPoint(capsule.center)), root.localScale);
        float bottom = center.y - 0.5f * height;

        shape = new Shape
        {
            Radius = radius,
            Height = height,
            OffsetSteps = new Vector3(center.x / buildStep, bottom / levelHeight, center.z / buildStep)
        };
        return true;
    }

    private static int Apply(List<Plan> plans)
    {
        var removed = new HashSet<GameObject>();
        var failed = new HashSet<GameObject>();
        int converted = 0;

        for (int i = 0; i < plans.Count; i++)
        {
            Plan plan = plans[i];
            if (failed.Contains(plan.Prefab)) continue;

            if (!removed.Contains(plan.Prefab))
            {
                if (!RemoveCapsule(plan.Prefab))
                {
                    failed.Add(plan.Prefab);
                    continue;
                }
                removed.Add(plan.Prefab);
            }

            Shape s = plan.Shape;
            Undo.RecordObject(plan.Info, "Convert Capsule To Collision Shape");
            plan.Info.EditorAssignCollision(CollisionKind.Cylinder, s.Radius, s.Height, s.OffsetSteps);
            EditorUtility.SetDirty(plan.Info);
            converted++;

            Debug.Log($"{Tag} {plan.Info.name}: Cylinder radius {s.Radius:0.###}, height {s.Height:0.###}, offset steps {s.OffsetSteps} from {plan.Prefab.name}", plan.Info);
        }

        AssetDatabase.SaveAssets();
        return converted;
    }

    private static bool RemoveCapsule(GameObject prefab)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError($"{Tag} {prefab.name} is not an asset, capsule not removed, shape not written");
            return false;
        }

        GameObject contents = null;
        try
        {
            contents = PrefabUtility.LoadPrefabContents(path);
            CapsuleCollider[] capsules = contents.GetComponentsInChildren<CapsuleCollider>(true);
            for (int i = 0; i < capsules.Length; i++) Object.DestroyImmediate(capsules[i]);
            PrefabUtility.SaveAsPrefabAsset(contents, path);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"{Tag} {path}: capsule not removed, shape not written: {e.Message}");
            return false;
        }
        finally
        {
            if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
        }
    }
}

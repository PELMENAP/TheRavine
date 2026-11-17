using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class RenderingPerformancePreset
{
    private const string PipelinePath = "Assets/Settings/URP-HighFidelity.asset";
    private const string ProfilePath = "Assets/Settings/SampleSceneProfile.asset";
    private const string FogMaterialPath = "Assets/Material/Effects/Volumetric Fog.mat";
    private const string WaterShaderName = "The Ravine/Water/WaterShader";

    private const float ShadowDistance = 300f;
    private const int AdditionalShadowResolution = 1024;
    private const int BloomIterations = 4;
    private const float FogSteps = 12f;
    private const float FogCutoff = 0.01f;
    private const float SsrSteps = 12f;
    private const float SsrRefine = 3f;

    [MenuItem("Tools/Ravine/Rendering/Apply Performance Preset")]
    public static void Apply()
    {
        ApplyPipeline();
        ApplyBloom();
        ApplyFog();
        ApplyWater();
        AssetDatabase.SaveAssets();
    }

    private static void ApplyPipeline()
    {
        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (asset == null) { Debug.LogWarning($"Missing {PipelinePath}"); return; }

        var so = new SerializedObject(asset);
        SerializedProperty distance = so.FindProperty("m_ShadowDistance");
        SerializedProperty additional = so.FindProperty("m_AdditionalLightsShadowmapResolution");

        Log("Shadow Distance", distance.floatValue, ShadowDistance);
        Log("Additional Shadow Resolution", additional.intValue, AdditionalShadowResolution);

        distance.floatValue = Mathf.Min(distance.floatValue, ShadowDistance);
        additional.intValue = Mathf.Min(additional.intValue, AdditionalShadowResolution);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }

    private static void ApplyBloom()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null || !profile.TryGet(out Bloom bloom)) { Debug.LogWarning($"No Bloom in {ProfilePath}"); return; }

        Log("Bloom Max Iterations", bloom.maxIterations.value, BloomIterations);
        bloom.maxIterations.value = Mathf.Min(bloom.maxIterations.value, BloomIterations);
        EditorUtility.SetDirty(profile);
    }

    private static void ApplyFog()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(FogMaterialPath);
        if (material == null) { Debug.LogWarning($"Missing {FogMaterialPath}"); return; }

        SetMaterial(material, "_RayStepCount", FogSteps, false);
        SetMaterial(material, "_TransmittanceCutoff", FogCutoff, true);
    }

    private static void ApplyWater()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Material"))
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (material == null || material.shader == null || material.shader.name != WaterShaderName) continue;

            SetMaterial(material, "SSRStepCount", SsrSteps, false);
            SetMaterial(material, "SSRRefineCount", SsrRefine, false);
        }
    }

    private static void SetMaterial(Material material, string property, float value, bool raise)
    {
        if (!material.HasProperty(property)) { Debug.LogWarning($"{material.name}: no {property}"); return; }

        float old = material.GetFloat(property);
        float next = raise ? Mathf.Max(old, value) : Mathf.Min(old, value);
        Log($"{material.name}.{property}", old, next);
        material.SetFloat(property, next);
        EditorUtility.SetDirty(material);
    }

    private static void Log(string name, float from, float to) => Debug.Log($"[PerformancePreset] {name}: {from} -> {to}");
}

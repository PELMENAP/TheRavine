using UnityEditor;
using UnityEngine;
using TheRavine.EntityControl.Virology;

public static class StrainCodonTableBuilder
{
    private const string AssetPath = "Assets/Resources/StrainCodonTable.asset";

    [MenuItem("Tools/Ravine/Rebuild Strain Codon Table")]
    public static void Rebuild()
    {
        if (!VirologyRuntime.IsReady && !BindRulesAndInitialize()) return;
        if (!VirologyRuntime.IsReady)
        {
            Debug.LogError("[StrainCodonTableBuilder] VirologyRuntime не готов");
            return;
        }

        int n = ProteinTable.ActionCount;
        var variants = new ushort[n * StrainComposer.VariantsPerAction];
        var counts   = new byte[n];
        var amps     = new float[n];

        StrainComposer.BuildBruteForce(variants, counts, amps);

        var asset = AssetDatabase.LoadAssetAtPath<StrainCodonTable>(AssetPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<StrainCodonTable>();
            AssetDatabase.CreateAsset(asset, AssetPath);
        }

        asset.Store(VirologyRuntime.PrototypeSeed, variants, counts, amps);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();

        Debug.Log($"[StrainCodonTableBuilder] Таблица пересобрана: {AssetPath}");
    }

    private static bool BindRulesAndInitialize()
    {
        string[] guids = AssetDatabase.FindAssets("t:SimulationRules");
        SimulationRules rules = guids.Length > 0
            ? AssetDatabase.LoadAssetAtPath<SimulationRules>(AssetDatabase.GUIDToAssetPath(guids[0]))
            : null;
        if (rules == null)
        {
            Debug.LogError("[StrainCodonTableBuilder] Ассет SimulationRules не найден");
            return false;
        }

        SimulationRules.Bind(rules);
        VirologyRuntime.Initialize();
        if (!Application.isPlaying) SimulationRules.Bind(null);
        return true;
    }
}
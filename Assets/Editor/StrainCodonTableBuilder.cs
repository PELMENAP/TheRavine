using UnityEditor;
using UnityEngine;
using TheRavine.EntityControl.Virology;

public static class StrainCodonTableBuilder
{
    private const string AssetPath = "Assets/Resources/StrainCodonTable.asset";

    [MenuItem("Tools/Ravine/Rebuild Strain Codon Table")]
    public static void Rebuild()
    {
        VirologyRuntime.Initialize();
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
}
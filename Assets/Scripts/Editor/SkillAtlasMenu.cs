using UnityEditor;
using UnityEngine;

public static class SkillAtlasMenu
{
    private const string AssetPath = "Assets/Scripts/EntitySystem/AI/SkillAtlas.asset";

    [MenuItem("Tools/Ravine/Skills/Build Atlas")]
    public static void Build()
    {
        var built = SkillAtlasBuilder.BuildDefault();
        if (!built.Validate(out string error))
        {
            Debug.LogError("SkillAtlas build failed: " + error);
            Object.DestroyImmediate(built);
            return;
        }

        var existing = AssetDatabase.LoadAssetAtPath<SkillAtlas>(AssetPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(built, AssetPath);
            existing = built;
        }
        else
        {
            EditorUtility.CopySerialized(built, existing);
            Object.DestroyImmediate(built);
            EditorUtility.SetDirty(existing);
        }
        AssetDatabase.SaveAssets();
        Selection.activeObject = existing;
        Debug.Log("SkillAtlas written to " + AssetPath);
    }
}

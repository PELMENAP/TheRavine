using UnityEditor;
using UnityEngine;

public static class ExperimentRulesMenu
{
    private const string SourcePath = "Assets/Scripts/EntitySystem/AI/SimulationRules.asset";
    private const string TargetPath = "Assets/Scripts/EntitySystem/AI/SimulationRules.Experiment.asset";

    private static readonly string[] EnabledFlags =
    {
        "stressSourcesEnabled", "socialReliefEnabled", "helpLogEnabled", "simulationLogEnabled",
    };

    [MenuItem("Tools/Ravine/Rules/Create or Update Experiment Rules")]
    public static void CreateOrUpdate()
    {
        if (AssetDatabase.LoadAssetAtPath<SimulationRules>(SourcePath) == null)
        {
            Debug.LogError("Experiment rules: the base asset is missing at " + SourcePath);
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SimulationRules>(TargetPath) == null && !AssetDatabase.CopyAsset(SourcePath, TargetPath))
        {
            Debug.LogError("Experiment rules: cannot copy " + SourcePath + " to " + TargetPath);
            return;
        }

        var target = AssetDatabase.LoadAssetAtPath<SimulationRules>(TargetPath);
        var so = new SerializedObject(target);
        for (int i = 0; i < EnabledFlags.Length; i++)
        {
            var property = so.FindProperty(EnabledFlags[i]);
            if (property == null)
            {
                Debug.LogError("Experiment rules: unknown flag " + EnabledFlags[i]);
                return;
            }
            property.boolValue = true;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssets();
        Selection.activeObject = target;
        Debug.Log("Experiment rules written to " + TargetPath + ": " + target.SystemFlagsLabel());
    }
}

#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;

public class TreeMeshBaker : EditorWindow
{
    private MtreeComponent _source;
    private TreeMeshCollection _targetCollection;
    private int _count = 8;
    private string _savePath = "Assets/Trees/Baked";

    [MenuItem("Tools/Tree Mesh Baker")]
    private static void Open() => GetWindow<TreeMeshBaker>("Tree Mesh Baker");

    private void OnGUI()
    {
        _source = (MtreeComponent)EditorGUILayout.ObjectField("MtreeComponent", _source, typeof(MtreeComponent), true);
        _targetCollection = (TreeMeshCollection)EditorGUILayout.ObjectField("Collection", _targetCollection, typeof(TreeMeshCollection), false);
        _count = EditorGUILayout.IntField("Mesh Count", _count);

        EditorGUILayout.BeginHorizontal();
        _savePath = EditorGUILayout.TextField("Save Path", _savePath);
        if (GUILayout.Button("...", GUILayout.Width(30)))
        {
            string path = EditorUtility.OpenFolderPanel("Save folder", "Assets", "");
            if (!string.IsNullOrEmpty(path))
                _savePath = "Assets" + path.Substring(Application.dataPath.Length);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginDisabledGroup(_source == null || _targetCollection == null);
        if (GUILayout.Button("Bake Meshes"))
            Bake();
        EditorGUI.EndDisabledGroup();
    }

    private void Bake()
    {
        if (!Directory.Exists(_savePath))
            Directory.CreateDirectory(_savePath);

        var entries = new Mesh[_count];

        for (int i = 0; i < _count; i++)
        {
            var rng = new System.Random(i);
            foreach (var func in _source.treeFunctionsAssets)
                func.seed = rng.Next(0, 10000);

            Mesh mesh = _source.GenerateTree();
            Mesh meshCopy = Object.Instantiate(mesh);

            string meshPath = $"{_savePath}/tree_mesh_{i}.mesh";
            AssetDatabase.CreateAsset(meshCopy, meshPath);

            entries[i] = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);

            EditorUtility.DisplayProgressBar("Baking meshes", $"{i + 1}/{_count}", (float)(i + 1) / _count);
        }

        EditorUtility.ClearProgressBar();

        _targetCollection.SetEntries(entries);
        EditorUtility.SetDirty(_targetCollection);
        AssetDatabase.SaveAssets();

        Debug.Log($"Baked {_count} tree meshes to {_savePath}");
    }
}
#endif
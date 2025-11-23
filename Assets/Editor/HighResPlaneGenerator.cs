using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.IO;

public class HighResPlaneGenerator : EditorWindow
{
    private string meshName = "HighResPlane";
    private int cols = 100; // вершин по X
    private int rows = 100; // вершин по Z
    private float width = 1f;
    private float length = 1f;
    private bool generateNormals = true;
    private bool generateTangents = false;
    private bool use32bit = true;
    private string outputFolder = "Assets/GeneratedMeshes";

    [MenuItem("Window/Mesh Tools/HighRes Plane Generator")]
    static void OpenWindow() => GetWindow<HighResPlaneGenerator>("HighRes Plane");

    void OnGUI()
    {
        GUILayout.Label("High-Resolution Plane Mesh Generator", EditorStyles.boldLabel);
        meshName = EditorGUILayout.TextField("Mesh Name", meshName);
        width = EditorGUILayout.FloatField("Width (X)", width);
        length = EditorGUILayout.FloatField("Length (Z)", length);
        cols = EditorGUILayout.IntField("Vertices (cols, X)", cols);
        rows = EditorGUILayout.IntField("Vertices (rows, Z)", rows);
        cols = Mathf.Clamp(cols, 2, 65536);
        rows = Mathf.Clamp(rows, 2, 65536);

        use32bit = EditorGUILayout.Toggle("Force 32-bit indices", use32bit);
        generateNormals = EditorGUILayout.Toggle("Generate Normals", generateNormals);
        generateTangents = EditorGUILayout.Toggle("Generate Tangents", generateTangents);

        EditorGUILayout.Space();
        GUILayout.Label("Output", EditorStyles.boldLabel);
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);

        if (GUILayout.Button("Create & Save"))
        {
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            var mesh = BuildPlaneMesh(width, length, cols, rows, generateNormals, generateTangents, use32bit);
            string path = Path.Combine(outputFolder, meshName + ".asset");
            path = path.Replace("\\", "/");
            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Done", $"Mesh saved to {path}", "OK");
        }
    }

    public static Mesh BuildPlaneMesh(float width, float length, int cols, int rows,
                                     bool genNormals = true, bool genTangents = false, bool force32bit = false)
    {
        int vertCount = cols * rows;
        // create mesh
        Mesh mesh = new Mesh();
        mesh.name = "HighResPlane";

        if (force32bit || vertCount > 65000)
            mesh.indexFormat = IndexFormat.UInt32;

        Vector3[] vertices = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        Vector3[] normals = genNormals ? new Vector3[vertCount] : null;

        float halfW = width * 0.5f;
        float halfL = length * 0.5f;

        for (int z = 0; z < rows; z++)
        {
            for (int x = 0; x < cols; x++)
            {
                int i = z * cols + x;
                float u = (cols == 1) ? 0f : (float)x / (cols - 1);
                float v = (rows == 1) ? 0f : (float)z / (rows - 1);
                vertices[i] = new Vector3(Mathf.Lerp(-halfW, halfW, u), 0f, Mathf.Lerp(-halfL, halfL, v));
                uvs[i] = new Vector2(u, v);
                if (genNormals) normals[i] = Vector3.up;
            }
        }

        // triangles: (cols-1)*(rows-1)*2 tris
        int quadCount = (cols - 1) * (rows - 1);
        int[] triangles = new int[quadCount * 6];
        int t = 0;
        for (int z = 0; z < rows - 1; z++)
        {
            for (int x = 0; x < cols - 1; x++)
            {
                int i = z * cols + x;
                int iRight = i + 1;
                int iTop = i + cols;
                int iTopRight = iTop + 1;

                // first tri
                triangles[t++] = i;
                triangles[t++] = iTop;
                triangles[t++] = iTopRight;

                // second tri
                triangles[t++] = i;
                triangles[t++] = iTopRight;
                triangles[t++] = iRight;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        if (genNormals) mesh.normals = normals;
        else mesh.RecalculateNormals();

        if (genTangents)
            mesh.RecalculateTangents();

        mesh.RecalculateBounds();
        return mesh;
    }
}
using UnityEngine;
using UnityEditor;

public class GrassBladeGenerator
{
    [MenuItem("Window/Mesh Tools/Grass Blade")]
    public static void CreateGrassBladeMesh()
    {
        Mesh mesh = GenerateGrassBlade(
            width: 0.1f,
            height: 2.0f,
            segments: 4,
            curvature: 0.1f
        );
        
        SaveMeshAsAsset(mesh, "GrassBlade");
    }
    
    public static Mesh GenerateGrassBlade(float width, float height, int segments, float curvature)
    {
        Mesh mesh = new Mesh();
        mesh.name = "GrassBlade";
        
        int vertexCount = (segments + 1) * 2;
        Vector3[] vertices = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float currentHeight = height * t;
            float currentWidth = width * (1f - t * 0.7f);
            
            float bend = curvature * t * t;
            
            vertices[i * 2] = new Vector3(-currentWidth * 0.5f, currentHeight, bend);
            normals[i * 2] = Vector3.back;
            uvs[i * 2] = new Vector2(0, t);
            
            vertices[i * 2 + 1] = new Vector3(currentWidth * 0.5f, currentHeight, bend);
            normals[i * 2 + 1] = Vector3.back;
            uvs[i * 2 + 1] = new Vector2(1, t);
        }
        
        int triangleCount = segments * 2;
        int[] triangles = new int[triangleCount * 3];
        
        for (int i = 0; i < segments; i++)
        {
            int baseIndex = i * 6;
            int vertexIndex = i * 2;
            
            triangles[baseIndex] = vertexIndex;
            triangles[baseIndex + 1] = vertexIndex + 2;
            triangles[baseIndex + 2] = vertexIndex + 1;
            
            triangles[baseIndex + 3] = vertexIndex + 1;
            triangles[baseIndex + 4] = vertexIndex + 2;
            triangles[baseIndex + 5] = vertexIndex + 3;
        }
        
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        
        return mesh;
    }
    
    private static void SaveMeshAsAsset(Mesh mesh, string name)
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Grass Blade Mesh",
            name,
            "asset",
            "Please enter a file name to save the mesh"
        );
        
        if (string.IsNullOrEmpty(path))
            return;
        
        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = mesh;
        
        Debug.Log($"Grass blade mesh saved to: {path}");
    }
}
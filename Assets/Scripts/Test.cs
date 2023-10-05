using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEditor;
using UnityEngine.UI;
using NaughtyAttributes;
using System.Linq;

public class Test : MonoBehaviour
{
    public string test;
    public string enter;

    public MeshFilter meshFilter;

    public MeshFilter meshFilterTest;

    // private void Awake() {
    //     Settings.isShadow = true;
    // }


    private Mesh GetQuadMeshMap(int[,] meshMap, int countOfQuads, int sizeMap, int scale, int xCord, int yCord)
    {
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[4 * countOfQuads];
        Vector2[] uv = new Vector2[4 * countOfQuads];
        int[] triangles = new int[countOfQuads * 6];
        int dotCount = 0;
        int trianglCount = 0;
        for (int x = 0; x < sizeMap; x++)
        {
            for (int y = 0; y < sizeMap; y++)
            {
                if (meshMap[x, y] == 1)
                {
                    vertices[dotCount] = new Vector3(xCord + x * scale, yCord + y * scale);
                    vertices[dotCount + 1] = new Vector3(xCord + x * scale, yCord + y * scale + scale);
                    vertices[dotCount + 2] = new Vector3(xCord + x * scale + scale, yCord + y * scale + scale);
                    vertices[dotCount + 3] = new Vector3(xCord + x * scale + scale, yCord + y * scale);

                    uv[dotCount] = new Vector2(0.05f, 0.05f);
                    uv[dotCount + 1] = new Vector2(0.05f, 0.95f);
                    uv[dotCount + 2] = new Vector2(0.95f, 0.95f);
                    uv[dotCount + 3] = new Vector2(0.95f, 0.05f);

                    triangles[trianglCount] = dotCount;
                    triangles[trianglCount + 1] = dotCount + 1;
                    triangles[trianglCount + 2] = dotCount + 2;
                    triangles[trianglCount + 3] = dotCount;
                    triangles[trianglCount + 4] = dotCount + 2;
                    triangles[trianglCount + 5] = dotCount + 3;

                    if (x + 1 >= sizeMap || meshMap[x + 1, y] == 0)
                    {
                        uv[dotCount + 2] += new Vector2(0.05f, 0);
                        uv[dotCount + 3] += new Vector2(0.05f, 0);
                    }

                    if (x - 1 < 0 || meshMap[x - 1, y] == 0)
                    {
                        uv[dotCount] -= new Vector2(0.05f, 0);
                        uv[dotCount + 1] -= new Vector2(0.05f, 0);
                    }

                    if (y + 1 >= sizeMap || meshMap[x, y + 1] == 0)
                    {
                        uv[dotCount + 1] += new Vector2(0, 0.05f);
                        uv[dotCount + 2] += new Vector2(0, 0.05f);
                    }

                    if (y - 1 < 0 || meshMap[x, y - 1] == 0)
                    {
                        uv[dotCount] -= new Vector2(0, 0.05f);
                        uv[dotCount + 3] -= new Vector2(0, 0.05f);
                    }

                    dotCount += 4;
                    trianglCount += 6;
                }
            }
        }
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        return mesh;
    }

    private void Start()
    {
        meshFilter.mesh = GetQuadMeshMap(new int[3, 3] { { 1, 0, 1 }, { 1, 1, 1 }, { 0, 1, 1 } }, 7, 3, 10, -50, -50);
    }

    [Button]
    private void TestFunction()
    {
        double similarity = Extentions.JaroWinklerSimilarity(test, enter);
        print(similarity);
    }
    [Button]

    private void TestFunction1()
    {
    }



}

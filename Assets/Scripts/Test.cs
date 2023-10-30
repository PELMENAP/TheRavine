using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEditor;
using UnityEngine.UI;
using NaughtyAttributes;
using System.Linq;
using System;

public class Test : MonoBehaviour
{
    public string test;
    public string enter;

    public Transform watert;
    public MeshFilter water;

    public Transform terraint;
    public MeshFilter terrain;

    public bool isEqual, fisting;

    [Button]
    private void TEst()
    {
        print(33 / 16);
    }

    // private void Awake() {
    //     Settings.isShadow = true;
    // }


    private Mesh GetQuadWaterMeshMap(int[,] meshMap, int countOfQuads, int sizeMap, int scale)
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
                    vertices[dotCount] = new Vector3(x * scale, y * scale);
                    vertices[dotCount + 1] = new Vector3(x * scale, y * scale + scale);
                    vertices[dotCount + 2] = new Vector3(x * scale + scale, y * scale + scale);
                    vertices[dotCount + 3] = new Vector3(x * scale + scale, y * scale);

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

    private Mesh GetTerrainMeshMap(int[,] heightMap, int sizeMap, int scale, bool isEqual = false)
    {
        Mesh mesh;
        Vector3[] vertices;
        Vector2[] uv;
        int[] triangles;

        if (isEqual)
        {
            mesh = new Mesh();
            vertices = new Vector3[4];
            uv = new Vector2[4];
            triangles = new int[6];
            vertices[0] = new Vector3(0, 0, heightMap[0, 0]);
            vertices[1] = new Vector3(0, sizeMap * scale, heightMap[0, 0]);
            vertices[2] = new Vector3(sizeMap * scale, sizeMap * scale, heightMap[0, 0]);
            vertices[3] = new Vector3(sizeMap * scale, 0, heightMap[0, 0]);
            triangles[0] = 0;
            triangles[1] = 1;
            triangles[2] = 2;
            triangles[3] = 0;
            triangles[4] = 2;
            triangles[5] = 3;
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            return mesh;
        }

        mesh = new Mesh();
        vertices = new Vector3[sizeMap * sizeMap];
        uv = new Vector2[sizeMap * sizeMap];
        triangles = new int[6 * (sizeMap - 1) * (sizeMap - 1)];

        int trianglCount = 0;

        for (int x = 0; x < sizeMap; x++)
            for (int y = 0; y < sizeMap; y++)
                vertices[x * sizeMap + y] = new Vector3(x * scale, y * scale, heightMap[x, y]);

        for (int x = 0; x < sizeMap - 1; x++)
        {
            for (int y = 0; y < sizeMap - 1; y++)
            {
                triangles[trianglCount] = x * sizeMap + y;
                triangles[trianglCount + 1] = (x + 1) * sizeMap + y;
                triangles[trianglCount + 2] = (x + 1) * sizeMap + y + 1;
                triangles[trianglCount + 3] = x * sizeMap + y;
                triangles[trianglCount + 4] = (x + 1) * sizeMap + y + 1;
                triangles[trianglCount + 5] = x * sizeMap + y + 1;
                trianglCount += 6;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        return mesh;
    }

    [Button]
    private void CreateMesh()
    {
        int[,] meshMap = new int[3, 3] {
            { 1, 0, 1 },
            { 1, 1, 1 },
            { 0, 1, 1 } };
        water.mesh = GetQuadWaterMeshMap(meshMap, 7, 3, 10);
        watert.position = new Vector3(-80, 50);

        const int mapSize = 7;
        int[,] heightMap0 = new int[mapSize, mapSize] {
        { 2, 2, 2, 2, 4, 2, 2 },
        { 4, 2, 2, 2, 4, 4, 4 },
        { 4, 2, 4, 2, 4, 4, 6 },
        { 6, 4, 4, 2, 4, 4, 4 },
        { 6, 4, 4, 2, 2, 2, 0 },
        { 6, 6, 4, 2, 2, 0, 0 },
        { 8, 6, 6, 2, 2, 2, 0 },
        };
        int[,] heightMap1 = new int[mapSize, mapSize] {
        { 2, 2, 2, 2, 4, 2, 2 },
        { 4, 2, 2, 2, 4, 4, 4 },
        { 4, 2, 4, 2, 4, 4, 6 },
        { 2, 4, 4, 2, 4, 4, 4 },
        { 0, 2, 4, 2, 2, 2, 0 },
        { 0, 2, 4, 2, 2, 0, 0 },
        { 0, 2, 4, 2, 2, 2, 0 },
        };
        int[,] heightMap2 = new int[mapSize, mapSize] {
        { 8, 8, 6, 4, 4, 2, 2 },
        { 6, 6, 4, 4, 4, 4, 4 },
        { 4, 2, 4, 2, 4, 4, 6 },
        { 6, 4, 4, 2, 4, 4, 4 },
        { 6, 4, 4, 2, 2, 2, 0 },
        { 6, 6, 4, 2, 2, 0, 0 },
        { 8, 6, 6, 2, 2, 2, 0 },
        };
        int[,] heightMap3 = new int[mapSize, mapSize] {
        { 2, 2, 2, 2, 4, 2, 2 },
        { 4, 2, 2, 2, 4, 4, 4 },
        { 4, 2, 4, 2, 4, 4, 6 },
        { 6, 4, 4, 2, 4, 4, 4 },
        { 6, 4, 4, 2, 2, 2, 0 },
        { 6, 6, 4, 2, 2, 0, 0 },
        { 8, 6, 6, 2, 2, 2, 0 },
        };
        //terrain.mesh = GetQuadTerrainMeshMap(heightMap, mapSize * mapSize, mapSize, 10, 2, isEqual);
        // terrain.mesh = GetTerrainMeshMap(heightMap, mapSize, 10, isEqual);
        // terraint.position = new Vector3(-50, 50);
        int chunkCount = 2;
        Chunk[,] map = new Chunk[chunkCount, chunkCount];
        map[0, 0].heightMap = heightMap0;
        map[0, 1].heightMap = heightMap1;
        map[1, 0].heightMap = heightMap2;
        map[1, 1].heightMap = heightMap3;

        TerrainGenerator(map, chunkCount, 10, 7);
    }

    private void TerrainGenerator(Chunk[,] map, int sizeMap, int scale, int cellCount)
    {
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
        CombineInstance[] combine = new CombineInstance[sizeMap * sizeMap];

        int count = 0;
        for (int i = 0; i < sizeMap; i++)
        {
            for (int j = 0; j < sizeMap; j++)
            {
                combine[count].mesh = GetTerrainMeshMap(map[i, j].heightMap, cellCount, scale);
                combine[count].transform = Matrix4x4.TRS(new Vector3(i * scale * (cellCount - 1), j * scale * (cellCount - 1), 0), Quaternion.identity, new Vector3(1, 1, 1)); ;
                count++;
            }
        }

        Mesh mesh = new Mesh();
        mesh.CombineMeshes(combine);
        terrain.mesh = mesh;
        // transform.gameObject.SetActive(true);
        terraint.position = new Vector3(-50, 50);
    }
    private void TestFunction()
    {
        double similarity = Extentions.JaroWinklerSimilarity(test, enter);
        print(similarity);
    }
    [Button]

    private void TestFunction1()
    {
    }

    struct Chunk
    {
        public int[,] heightMap;
    }

}

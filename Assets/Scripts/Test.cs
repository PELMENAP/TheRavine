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
    public Transform viewer;
    public MapGenerator generator;
    public string test;
    public string enter;

    public Transform watert;
    public MeshFilter water;

    public Transform terraint;
    public MeshFilter terrain;

    public bool isEqual, fisting, isShadow;
    public ControlType control;
    public GameObject prefab;

    [Button]
    private void TEst()
    {
        print(prefab.transform.rotation.z);
    }

    private Vector2 RoundVector(Vector2 vec) => new Vector2(Mathf.RoundToInt(vec.x), Mathf.RoundToInt(vec.y));

    private IEnumerator Testing()
    {
        int ID = prefab.GetInstanceID();
        // ObjectSystem.inst.PoolManagerBase.CreatePool(prefab, 3);
        int time = 30;
        for (int i = 0; i < time; i++)
        {
            // ObjectSystem.inst.PoolManagerBase.Reuse(ID, new Vector2(i * 2, 10));
            yield return new WaitForSeconds(1);
        }
    }

    private void Awake()
    {
        Settings.isShadow = isShadow;
        Settings._controlType = control;
    }
    private void TestFunction()
    {
        double similarity = Extentions.JaroWinklerSimilarity(test, enter);
        print(similarity);
    }

    Vector2 position;
    ChunkData map;

    [Button]
    private void TestFunction1()
    {
        position = RoundVector(new Vector2(viewer.position.x, viewer.position.y) / (5 * 16));
        print(position.x + " " + position.y);
        map = generator.GetMapData(new Vector2(-position.x - 1, position.y + 1));
        for (int x = 0; x < 16; x++)
        {
            string title = "";
            for (int y = 0; y < 16; y++)
            {
                title += Convert.ToString(map.heightMap[x, y]) + " ";
            }
            print(title);
        }
        foreach (var item in map.objectsToInst)
        {
            print(item);
        }
    }

    [Button]

    private void TestPosition()
    {
        Vector2 realPos = position - new Vector2(5 * 8, 5 * 8);
        Vector2 playerPos = RoundVector(new Vector2(viewer.position.x, viewer.position.y));
        Vector2 XYpos = (playerPos - realPos) / 5;
        print(XYpos);
    }

    [Button]

    private void TestPlacedObject()
    {
        Vector2 playerPos = RoundVector(new Vector2(viewer.position.x, viewer.position.y));
        Vector2 realChunk = RoundVector(new Vector2(viewer.position.x, viewer.position.y) / (5 * 16));
        ChunkData map = generator.GetMapData(new Vector2(-position.x - 1, position.y + 1));
        // if (map.objectsToInst.ContainsKey(playerPos))
        // {
        //     print(map.objectsToInst[playerPos]);
        // }
    }
}

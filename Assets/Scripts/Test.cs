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

    private Vector2 RoundVector(Vector2 vec) => new Vector2((int)vec.x, (int)vec.y);

    private void Awake()
    {
        Settings.isShadow = isShadow;
        Settings._controlType = control;
    }

    Vector2 position;
    ChunkData map;

    private void Update()
    {
        if (Input.GetKeyDown("f"))
        {
            position = RoundVector(new Vector2(viewer.position.x, viewer.position.y));
            if (ObjectSystem.inst.ContainsGlobal(position))
                return;
            generator.GetMapData(RoundVector((position + new Vector2(80, 80) * 3 / 2) / (5 * 16))).objectsToInst.Add(position);
            ushort id = (ushort)prefab.GetInstanceID();
            PrefabData data = ObjectSystem.inst.GetPrefabInfo(id);
            ObjectSystem.inst.AddToGlobal(position, id, data.name, data.amount, data.type);
            generator.ExtraUpdate();
        }

        if (Input.GetKeyDown("g"))
        {
            int size = 1;
            for (int x = -size; x <= size; x++)
            {
                for (int y = -size; y <= size; y++)
                {
                    position = RoundVector(new Vector2(viewer.position.x, viewer.position.y)) + new Vector2(x, y);
                    if (!ObjectSystem.inst.ContainsGlobal(position))
                        continue;
                    if (ObjectSystem.inst.GetGlobalObjectInfo(position).objectType == InstanceType.Inter)
                    {
                        ObjectSystem.inst.RemoveFromGlobal(position);
                        generator.GetMapData(RoundVector((position + new Vector2(80, 80) * 3 / 2) / (5 * 16))).objectsToInst.Remove(position);
                    }
                }
            }
            generator.ExtraUpdate();
        }
    }
    private void TestSimilarity()
    {
        double similarity = Extentions.JaroWinklerSimilarity(test, enter);
        print(similarity);
    }

    // [Button]
    // private void TestFunction1()
    // {
    //     position = RoundVector(new Vector2(viewer.position.x, viewer.position.y) / (5 * 16));
    //     print(position.x + " " + position.y);
    //     map = generator.GetMapData(new Vector2(position.x + 1, position.y + 1));
    //     for (int x = 0; x < 16; x++)
    //     {
    //         string title = "";
    //         for (int y = 0; y < 16; y++)
    //         {
    //             title += Convert.ToString(map.heightMap[x, y]) + " ";
    //         }
    //         print(title);
    //     }
    //     foreach (var item in map.objectsToInst)
    //     {
    //         print(item);
    //     }
    // }

    [Button]
    private void TestPosition()
    {
        position = new Vector2(viewer.position.x, viewer.position.y);
        Vector2 chunkPos = RoundVector((position + new Vector2(80, 80) * 3 / 2) / (5 * 16));
        Vector2 playerPos = RoundVector(position);
        Vector2 XYpos = (playerPos - chunkPos * (5 * 16)) / 5;
        print(chunkPos);
        print(XYpos);
    }

    // [Button]

    // private void TestPlacedObject()
    // {
    //     Vector2 playerPos = RoundVector(new Vector2(viewer.position.x, viewer.position.y));
    //     Vector2 realChunk = RoundVector(new Vector2(viewer.position.x, viewer.position.y) / (5 * 16));
    //     ChunkData map = generator.GetMapData(new Vector2(-position.x - 1, position.y + 1));
    //     // if (map.objectsToInst.ContainsKey(playerPos))
    //     // {
    //     //     print(map.objectsToInst[playerPos]);
    //     // }
    // }
}

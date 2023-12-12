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

    public bool isEqual, fisting, isShadow;
    public ControlType control;
    public GameObject prefab1;

    public SkillData flyingSkill;
    public SkillData swimmingSkill;

    private Vector2 RoundVector(Vector2 vec) => new Vector2((int)vec.x, (int)vec.y);

    private SkillFacade SkillSystem = new SkillFacade();
    AEntity playerEntity;
    private void Awake()
    {
        Settings.isShadow = isShadow;
        Settings._controlType = control;
        playerEntity = new AEntity("player", 100);
        SkillSystem.AddEntity(playerEntity);
        SkillSystem.AddSkillToEntity(playerEntity, SkillBuilder.CreateSkill(flyingSkill));
    }

    [Button]
    private void ShowPlayerEntityEnergy()
    {
        print(playerEntity.Energy);
    }
    [Button]
    private void UseSkillByFacade()
    {
        SkillSystem.GetEntitySkill(playerEntity, "FlyingSkill").Use(playerEntity);
    }

    [Button]
    private void UseSkillBySelf()
    {
        playerEntity.Skills["FlyingSkill"].Use(playerEntity);
    }
    Vector2 position;
    ChunkData map;

    private void Update()
    {
        if (Input.GetKeyDown("f"))
        {
            position = RoundVector(new Vector2(viewer.position.x, viewer.position.y));
            if (generator.objectSystem.ContainsGlobal(position))
                return;
            generator.GetMapData(RoundVector((position + MapGenerator.vectorOffset) / MapGenerator.generationSize)).objectsToInst.Add(position);
            int id = prefab1.GetInstanceID();
            PrefabData data = generator.objectSystem.GetPrefabInfo(id);
            generator.objectSystem.AddToGlobal(position, id, data.name, data.amount, data.itype);
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
                    if (!generator.objectSystem.ContainsGlobal(position))
                        continue;
                    if (generator.objectSystem.GetGlobalObjectInfo(position).objectType == InstanceType.Inter)
                    {
                        generator.objectSystem.RemoveFromGlobal(position);
                        generator.GetMapData(RoundVector((position + MapGenerator.vectorOffset) / MapGenerator.generationSize)).objectsToInst.Remove(position);
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
    public int seedM = 4133;
    public byte count = 0;
    public byte[] Count = new byte[9];
    public StructInfo structInfo;
    Dictionary<Vector2, byte> objects = new Dictionary<Vector2, byte>(9);
    private void ShowDictionary()
    {
        print(objects.Count);
        foreach (var item in objects)
        {
            print(item);
        }
    }
    [Button]
    private void WFCA()
    {
        print("start wfc");
        WFC(new Vector2(0, 0));
    }

    private void WFC(Vector2 startPosition)
    {
        int a = seedM + (int)startPosition.x + (int)startPosition.y;
        int b = a % structInfo.tileInfo.Length;
        BFS(startPosition, (byte)b);
        ShowDictionary();
    }

    private void BFS(Vector2 current, byte type)
    {
        if (count > structInfo.iteration)
            return;
        if (structInfo.tileInfo[type].MCount > Count[type])
        {
            print("new");
            objects[current] = type;
            Count[type]++;
        }
        Queue<Pair<Vector2, byte>> queue = new Queue<Pair<Vector2, byte>>();
        byte c = 0;
        for (sbyte x = -1; x <= 1; x++)
        {
            for (sbyte y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0)
                    continue;
                Vector2 newPos = current + new Vector2(x, y);
                byte field = structInfo.tileInfo[type].neight[c++];
                if (field == 0)
                    continue;
                if (objects.ContainsKey(newPos))
                    continue;
                print(newPos);
                queue.Enqueue(new Pair<Vector2, byte>(newPos, (byte)((int)field + 1)));
            }
        }
        count++;
        foreach (var item in queue)
        {
            BFS(item.First, item.Second);
        }
        return;
    }

    public class Pair<T, U>
    {
        public Pair(T first, U second)
        {
            this.First = first;
            this.Second = second;
        }
        public T First { get; set; }
        public U Second { get; set; }
    };

}

using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine;
using TMPro;
using UnityEditor;
using UnityEngine.UI;
using NaughtyAttributes;
using System.Linq;
using System;
using TheRavine.Generator;
using TheRavine.Extentions;

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
    [SerializeField] private PlayerInput input;
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

    private void PickUp()
    {
        if (Input.GetKeyDown("f"))
        {
            position = RoundVector(new Vector2(viewer.position.x, viewer.position.y));
            if (generator.objectSystem.ContainsGlobal(position))
                return;
            generator.GetMapData(RoundVector((position + MapGenerator.vectorOffset) / MapGenerator.generationSize)).objectsToInst.Add(position);
            int id = prefab1.GetInstanceID();
            ObjectInfo data = generator.objectSystem.GetPrefabInfo(id);
            generator.objectSystem.TryAddToGlobal(position, id, data.name, data.amount, data.iType);
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
                        // generator.GetMapData(RoundVector((position + MapGenerator.vectorOffset) / MapGenerator.generationSize)).objectsToInst.Remove(position);
                    }
                }
            }
            generator.ExtraUpdate();
        }
    }
    private void TestSimilarity()
    {
        double similarity = Extention.JaroWinklerSimilarity(test, enter);
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
        print(generator.GetMapHeight(position));
    }

    [Button]
    private void ShowCurrentInput()
    {
        Debug.Log(input.currentActionMap);
    }
}

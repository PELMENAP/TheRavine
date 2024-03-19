using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEditor;
using NaughtyAttributes;
using Cysharp.Threading.Tasks;

using TheRavine.Base;
using TheRavine.Generator;
using TheRavine.Extentions;
using TheRavine.ObjectControl;
using TheRavine.EntityControl;

using DS.ScriptableObjects;

public class Test : MonoBehaviour
{
    public Transform viewer;
    public MapGenerator generator;
    public ObjectSystem objectSystem;
    public string test;
    public string enter;

    public bool isEqual, fisting, isShadow;
    public ControlType control;
    public GameObject prefab1;

    public SkillData flyingSkill;
    public SkillData swimmingSkill;

    public EntityInfo entityInfo;

    private Vector2 RoundVector(Vector2 vec) => new Vector2((int)vec.x, (int)vec.y);

    private SkillFacade skillFacade = new SkillFacade();
    public GameObject player;
    AEntity playerEntity;
    [SerializeField] private PlayerInput input;
    [SerializeField] private InputActionReference point;
    private void Start()
    {
        rte().Forget();
    }

    private async UniTaskVoid rte()
    {
        await UniTask.Delay(5000);
        Settings.isShadow = isShadow;
        Settings._controlType = control;
        playerEntity = player.GetComponent<AEntity>();
        playerEntity.SetUpEntityData(entityInfo);
        skillFacade.AddEntity(playerEntity.GetEntityComponent<SkillComponent>());
        skillFacade.AddSkillToEntity(playerEntity.GetEntityComponent<SkillComponent>(), SkillBuilder.CreateSkill(flyingSkill));
        point.action.performed += mobile;
    }

    [Button]
    private void ShowPlayerEntity()
    {
        print(playerEntity.GetEntityComponent<MainComponent>().stats.energy);
        print(playerEntity.GetEntityComponent<SkillComponent>().Skills[flyingSkill.SkillName].GetRechargeTime());
    }
    [Button]
    private void UseSkillByFacade()
    {
        skillFacade.GetEntitySkill(playerEntity.GetEntityComponent<SkillComponent>(), flyingSkill.SkillName).Use(playerEntity.GetEntityComponent<MainComponent>());
    }

    [Button]
    private void UseSkillBySelf()
    {
        playerEntity.GetEntityComponent<SkillComponent>().Skills[flyingSkill.SkillName].Use(playerEntity.GetEntityComponent<MainComponent>());
    }
    Vector2 position;
    ChunkData map;

    [Button]
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

    private void mobile(InputAction.CallbackContext context)
    {
        Debug.Log(context.phase);
    }

    private void OnDisable()
    {
        point.action.performed -= mobile;
    }

    [Button]
    private void GenerateMap()
    {
        generator.TestGeneration();
    }

    [SerializeField] private DSDialogueContainerSO dialogData;
    [SerializeField] private PlayerDialogOutput output;
    [SerializeField] private string answer;

    [Button]
    private void Add()
    {
        output.AddAnswer(answer);
    }
}

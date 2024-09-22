using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Unity.Netcode;
using NaughtyAttributes;
using Cysharp.Threading.Tasks;

using TheRavine.Base;
using TheRavine.Generator;
using TheRavine.Extensions;
using TheRavine.EntityControl;

using DS.ScriptableObjects;

public class Test : MonoBehaviour
{
    public Transform viewer;
    public MapGenerator generator;
    public string test;
    public string enter;

    public bool isShadow;
    public ControlType control;

    public SkillData flyingSkill;
    public SkillData swimmingSkill;

    public EntityInfo entityInfo;
    private SkillFacade skillFacade = new SkillFacade();
    public GameObject player;
    AEntity playerEntity;
    [SerializeField] private PlayerInput input;
    [SerializeField] private InputActionReference point;

    public int cycleCount;
    [Button]
    private void SetCycleNumber(){
        DataStorage.cycleCount = cycleCount;
    }

    [Button]
    private void ShowCycleCount(){
        Debug.Log(DataStorage.cycleCount);;
    }
    private void Start()
    {
        rte().Forget();
    }

    private async UniTaskVoid rte()
    {
        await UniTask.Delay(5000);
        Settings.isShadow = isShadow;
        Settings._controlType = control;
        // playerEntity = player.GetComponent<AEntity>();
        // playerEntity.SetUpEntityData(entityInfo);
        // skillFacade.AddEntity(playerEntity.GetEntityComponent<SkillComponent>());
        // skillFacade.AddSkillToEntity(playerEntity.GetEntityComponent<SkillComponent>(), SkillBuilder.CreateSkill(flyingSkill));
        // point.action.performed += mobile;
    }

    public static void PrintMap(int[,] map)
    {
        int size = map.GetLength(0);
        for (int x = 0; x < size; x++)
        {
            string mes = "";
            for (int y = 0; y < size; y++)
            {
                mes += map[x, y] + " ";
            }
            Debug.Log(mes);
        }
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
        double similarity = Extension.JaroWinklerSimilarity(test, enter);
        print(similarity);
    }

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
        // point.action.performed -= mobile;
    }

    // [Button]
    // private void GenerateMap()
    // {
    //     generator.TestGeneration();
    // }

    [SerializeField] private DSDialogueContainerSO dialogData;
    [SerializeField] private PlayerDialogOutput output;
    [SerializeField] private string answer;

    [Button]
    private void Add()
    {
        output.AddAnswer(answer);
    }
}

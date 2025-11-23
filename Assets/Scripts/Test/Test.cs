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
    public GameObject player;
    AEntity playerEntity;
    [SerializeField] private PlayerInput input;
    [SerializeField] private InputActionReference point;

    public int cycleCount;
    // [Button]
    // private void SetCycleNumber(){
    //     DataStorage.cycleCount = cycleCount;
    // }

    // [Button]
    // private void ShowCycleCount(){
    //     Debug.Log(DataStorage.cycleCount);;
    // }
    // private void Start()
    // {
    //     rte().Forget();
    // }

    // private async UniTaskVoid rte()
    // {
    //     await UniTask.Delay(5000);
    //     ServiceLocator.GetSettings().GameSettings.CurrentValue.enableShadows = isShadow;
    //     ServiceLocator.GetSettings().GameSettings.CurrentValue.controlType = control;
    // }
    [Button]
    private void ShowPlayerEntity()
    {
        // print(playerEntity.GetEntityComponent<MainComponent>().GetEntityStats().energy);
        // print(playerEntity.GetEntityComponent<SkillComponent>().Skills[flyingSkill.SkillName].GetRechargeTime());
    }

    [Button]
    private void UseSkillBySelf()
    {
        // playerEntity.GetEntityComponent<SkillComponent>().Skills[flyingSkill.SkillName].Use(playerEntity.GetEntityComponent<MainComponent>());
    }
    Vector2Int position;

    [Button]
    private void TestSimilarity()
    {
        double similarity = Extension.JaroWinklerSimilarity(test, enter);
        print(similarity);
    }

    [Button]
    private void TestPosition()
    {
        position = new Vector2Int((int)viewer.position.x, (int)viewer.position.y);
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

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

using TheRavine.Base;
using TheRavine.Generator;
using TheRavine.EntityControl;


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
    [SerializeField] private PlayerDialogOutput output;
    [SerializeField] private string answer;

}

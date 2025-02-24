using UnityEngine;
using UnityEngine.InputSystem;

[CreateAssetMenu(fileName = "BindInfo", menuName = "Gameplay/Create New BindInfo")]
public class BindInfo : ScriptableObject
{
    public InputActionReference fastInteractAction, delayInteractAction;
}

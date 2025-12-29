using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class RightMapp : MonoBehaviour
{
    void Start()
    {
        var playerInput = GetComponent<PlayerInput>();
        playerInput.actions.Disable();
        
        playerInput.actions.FindActionMap("Gameplay").Enable();
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

public class ActivateTextField : MonoBehaviour
{
    [SerializeField] private GameObject window;
    [SerializeField] private PlayerInput input;
    [SerializeField] private InputActionReference EnterRef;
    [SerializeField] private InputActionReference OutRef;

    private void OnEnable()
    {
        EnterRef.action.performed += Enter;
        OutRef.action.performed += Out;
    }

    private void Enter(InputAction.CallbackContext obj)
    {
        window.SetActive(true);
        input.SwitchCurrentActionMap("TextInput");
    }
    private void Out(InputAction.CallbackContext obj)
    {
        window.SetActive(false);
        input.SwitchCurrentActionMap("Gameplay");
    }

    private void OnDisable()
    {
        EnterRef.action.performed -= Enter;
        OutRef.action.performed -= Out;
    }
}

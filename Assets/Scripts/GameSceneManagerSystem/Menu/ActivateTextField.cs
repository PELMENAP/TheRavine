using UnityEngine;
using UnityEngine.InputSystem;

using TheRavine.EntityControl;
using TheRavine.Base;

public class ActivateTextField : MonoBehaviour
{
    [SerializeField] private PlayerEntity playerData;
    [SerializeField] private GameObject window, stats, mobileInput;
    [SerializeField] private PlayerInput input;
    [SerializeField] private InputActionReference EnterRef;
    [SerializeField] private InputActionReference OutRef;

    private GlobalSettings gameSettings;
    private void Start()
    {
        gameSettings = ServiceLocator.GetService<SettingsMediator>().Global.CurrentValue;
        EnterRef.action.performed += ChangeTerminalState;
        OutRef.action.performed += ChangeTerminalState;
        window.SetActive(false);
        mobileInput.SetActive(gameSettings.controlType == ControlType.Mobile);
    }
    private bool isActive = false;

    public void ChangeTerminalState(InputAction.CallbackContext context)
    {
        isActive = !isActive;
        if (isActive)
        {
            if (input.currentActionMap.name != "Gameplay")
            {
                isActive = !isActive;
                return;
            }
            playerData.SetBehaviourSit();
            input.SwitchCurrentActionMap("TextInput");
        }
        else
        {
            playerData.SetBehaviourIdle();
            input.SwitchCurrentActionMap("Gameplay");
        }
        if (gameSettings.controlType == ControlType.Mobile) mobileInput.SetActive(!isActive);
        window.SetActive(isActive);
    }

    public void ChangeStatsState() => stats.SetActive(!stats.activeSelf);

    private void OnDisable()
    {
        EnterRef.action.performed -= ChangeTerminalState;
        OutRef.action.performed -= ChangeTerminalState;
    }
}

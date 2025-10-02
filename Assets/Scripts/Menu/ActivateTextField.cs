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

    private GameSettings gameSettings;
    private void OnEnable()
    {
        gameSettings = ServiceLocator.GetService<SettingsModel>().GameSettings.CurrentValue;
        EnterRef.action.performed += ChangeTerminalState;
        OutRef.action.performed += ChangeTerminalState;
        window.SetActive(false);
        mobileInput.SetActive(gameSettings.controlType == ControlType.Mobile ? true : false);
    }
    private bool isactive = false;

    public void ChangeTerminalState(InputAction.CallbackContext context)
    {
        isactive = !isactive;
        if (isactive)
        {
            if (input.currentActionMap.name != "Gameplay")
            {
                isactive = !isactive;
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
        if(gameSettings.controlType == ControlType.Mobile) mobileInput.SetActive(!isactive);
        window.SetActive(isactive);
    }

    public void ChangeStatsState()
    {
        stats.SetActive(!stats.activeSelf);
    }

    private void OnDisable()
    {
        EnterRef.action.performed -= ChangeTerminalState;
        OutRef.action.performed -= ChangeTerminalState;
    }
}

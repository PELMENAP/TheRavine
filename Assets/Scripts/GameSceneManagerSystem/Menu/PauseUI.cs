using UnityEngine;
using UnityEngine.InputSystem;

using TheRavine.EntityControl;
using TheRavine.Base;

public class PauseUI : MonoBehaviour, ISetAble
{
    [SerializeField] private GameObject window, stats, mobileInput;
    [SerializeField] private InputActionReference EnterRef;
    [SerializeField] private InputActionReference OutRef;
    private ActionMapController actionMapController;
    private PlayerEntity playerData;
    private GlobalSettings gameSettings;
    public void SetUp(ISetAble.Callback callback)
    {
        gameSettings = ServiceLocator.GetService<GlobalSettingsController>().GetCurrent();
        actionMapController = ServiceLocator.GetService<ActionMapController>();
        playerData = (PlayerEntity) ServiceLocator.Players.GetAllPlayers()[0];

        EnterRef.action.performed += ChangeTerminalState;
        OutRef.action.performed += ChangeTerminalState;
        
        window.SetActive(false);
        mobileInput.SetActive(gameSettings.controlType == ControlType.Mobile);

        callback?.Invoke();
    }
    private bool isActive = false;

    public void ChangeTerminalState(InputAction.CallbackContext context)
    {
        isActive = !isActive;
        if (isActive)
        {
            if (!actionMapController.IsGamePlayActive())
            {
                isActive = !isActive;
                return;
            }
            playerData.SetBehaviourSit();
            actionMapController.SwitchToPause();
        }
        else
        {
            playerData.SetBehaviourIdle();
            actionMapController.SwitchToGameplay();
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
    public void BreakUp(ISetAble.Callback callback)
    {
        OnDisable();

        callback?.Invoke();
    }
}

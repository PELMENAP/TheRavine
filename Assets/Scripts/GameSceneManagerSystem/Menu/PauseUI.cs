using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

using TheRavine.EntityControl;
using TheRavine.Base;

public class PauseUI : MonoBehaviour, ISetAble
{
    [SerializeField] private GameObject window, stats, mobileInput;
    [SerializeField] private Button enterPauseButton, quitPauseButton;
    [SerializeField] private InputActionReference EnterRef;
    [SerializeField] private InputActionReference QuitRef;
    private ActionMapController actionMapController;
    private PlayerEntity playerData;
    private GlobalSettings gameSettings;
    private InputBindingAdapter enterInputBindingAdapter, quitInputBindingAdapter;
    [SerializeField] private Slider cameraSlider;
    [SerializeField] private Toggle cameraFlip;
    public void SetUp(ISetAble.Callback callback)
    {
        gameSettings = ServiceLocator.GetService<GlobalSettingsController>().GetCurrent();
        actionMapController = ServiceLocator.GetService<ActionMapController>();
        playerData = (PlayerEntity) ServiceLocator.Players.GetAllPlayers()[0];
        cameraSlider.interactable = false;
        cameraFlip.interactable = false;

        // EnterRef.action.performed += ChangeTerminalState;
        // QuitRef.action.performed += ChangeTerminalState;

        enterInputBindingAdapter = InputBindingAdapter.Bind(enterPauseButton, EnterRef, ChangeTerminalState);
        quitInputBindingAdapter = InputBindingAdapter.Bind(quitPauseButton, QuitRef, ChangeTerminalState);
        
        window.SetActive(false);
        mobileInput.SetActive(gameSettings.controlType == ControlType.Mobile);

        callback?.Invoke();
    }

    private bool isActive = false;

    private void RefreshCameraIndicators()
    {
        if (!ServiceLocator.Services.TryGet(out CameraInputController camera)) return;

        cameraSlider.SetValueWithoutNotify(camera.Scale);
        cameraFlip.SetIsOnWithoutNotify(camera.Flipped);
    }

    public void ChangeTerminalState()
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
            RefreshCameraIndicators();
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

    private void OnDestroy()
    {
        // EnterRef.action.performed -= ChangeTerminalState;
        // QuitRef.action.performed -= ChangeTerminalState;

        enterInputBindingAdapter.Unbind();
        quitInputBindingAdapter.Unbind();
    }    
    public void BreakUp(ISetAble.Callback callback)
    {
        OnDestroy();

        callback?.Invoke();
    }
}

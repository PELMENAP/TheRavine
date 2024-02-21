using UnityEngine;
using UnityEngine.InputSystem;

using TheRavine.EntityControl;

public class ActivateTextField : MonoBehaviour
{
    [SerializeField] private PlayerEntity playerData;
    [SerializeField] private GameObject window, stats;
    [SerializeField] private PlayerInput input;
    [SerializeField] private InputActionReference EnterRef;
    [SerializeField] private InputActionReference OutRef;


    private void OnEnable()
    {
        EnterRef.action.performed += context => ChangeTerminalState();
        OutRef.action.performed += context => ChangeTerminalState();
        window.SetActive(false);
    }
    private bool isactive = false;
    public void ChangeTerminalState()
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
        window.SetActive(isactive);
    }

    public void ChangeStatsState()
    {
        stats.SetActive(!stats.activeSelf);
    }

    private void OnDisable()
    {
        EnterRef.action.performed -= context => ChangeTerminalState();
        OutRef.action.performed -= context => ChangeTerminalState();
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

public class GData : MonoBehaviour
{
    public static GameInput GInput;
    private void Awake()
    {
        GameInput gameInput = new GameInput();
        gameInput.Enable();
        GInput = gameInput;
    }

    private void OnDisable()
    {
        GInput.Disable();
    }
}

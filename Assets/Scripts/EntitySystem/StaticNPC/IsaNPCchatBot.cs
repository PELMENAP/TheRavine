using UnityEngine;
using LLMUnitySamples;
public class IsaNPCchatBot : MonoBehaviour, IDialogListener
{
    [SerializeField] private AsyncStartup asyncStartup;
    public void OnSpeechGet(string message)
    {
        asyncStartup.OnInputFieldSubmit(message);
        Debug.Log("Isa received: " + message);
    }

    public Vector3 GetCurrentPosition()
    {
        return transform.position;
    }

    void OnEnable()
    {
        DialogSystem.Instance.AddDialogListener(this);
    }

    void OnDisable()
    {
        DialogSystem.Instance.RemoveDialogListener(this);
    }
}

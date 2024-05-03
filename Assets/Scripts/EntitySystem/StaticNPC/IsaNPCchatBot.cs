using UnityEngine;
using LLMUnitySamples;
public class IsaNPCchatBot : MonoBehaviour, IDialogListener
{
    [SerializeField] private AsyncStartup asyncStartup;
    private IDialogSender currentSender;
    public void OnSpeechGet(IDialogSender sender, string message)
    {
        currentSender = sender;
        asyncStartup.OnInputFieldSubmit(message, SendCallbackToSender);
        // Debug.Log("Isa received: " + message);
    }

    public Vector3 GetCurrentPosition()
    {
        return transform.position;
    }

    public void SendCallbackToSender(){
        currentSender.OnDialogGetRequire();
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

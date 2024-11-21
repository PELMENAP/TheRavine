using UnityEngine;

public interface IDialogListener
{
    void OnSpeechGet(IDialogSender sender, string message);
    Vector3 GetCurrentPosition();
}

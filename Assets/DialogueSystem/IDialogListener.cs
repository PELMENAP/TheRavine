using UnityEngine;

public interface IDialogListener
{
    void OnSpeechGet(string message);
    Vector3 GetCurrentPosition();
}

using UnityEngine;

public interface IDialogListener
{
    void OnSpeechGet(Vector3 position, string label);
    Vector3 GetCurrentPosition();
}

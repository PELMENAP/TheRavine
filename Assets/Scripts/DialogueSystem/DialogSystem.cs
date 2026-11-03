using System.Collections.Generic;
using UnityEngine;

public class DialogSystem
{
    private static DialogSystem _instance;
    public static DialogSystem Instance => _instance ??= new DialogSystem();

    private readonly List<IDialogListener> _listeners = new();

    public void AddDialogListener(IDialogListener listener)
    {
        if (listener != null && !_listeners.Contains(listener)) _listeners.Add(listener);
    }

    public void RemoveDialogListener(IDialogListener listener) => _listeners.Remove(listener);

    public void Publish(Vector3 position, float distance, string label)
    {
        float r2 = distance * distance;
        for (int i = 0; i < _listeners.Count; i++)
        {
            var listener = _listeners[i];
            Vector3 p = listener.GetCurrentPosition();
            float dx = p.x - position.x;
            float dz = p.z - position.z;
            if (dx * dx + dz * dz > r2) continue;
            listener.OnSpeechGet(position, label);
        }
    }
}

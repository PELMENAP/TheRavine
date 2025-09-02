using System.Collections.Generic;
using UnityEngine;

public class DialogSystem
{
    private static DialogSystem _instance;
    public static DialogSystem Instance 
    {
        get
        {
            if (_instance == null) 
            {
                _instance = new DialogSystem();
                listeners = new List<IDialogListener>();
            }
            return _instance;
        }
    }
    private static List<IDialogListener> listeners;

    public void AddDialogListener(IDialogListener listener)
    {
        if (!listeners.Contains(listener))
        {
            listeners.Add(listener);
        }
    }

    public void RemoveDialogListener(IDialogListener listener)
    {
        if (listeners.Contains(listener))
        {
            listeners.Remove(listener);
        }
    }

    public void OnSpeechSend(IDialogSender sender, string message)
    {
        Vector3 senderPosition = sender.GetCurrentPosition();
        float distance = sender.GetDialogDistance();
        for(int i = 0; i < listeners.Count; i++)
        {
            if (Vector3.Distance(senderPosition, listeners[i].GetCurrentPosition()) <= distance)
            {
                listeners[i].OnSpeechGet(sender, message);
            }
        }
    }
}
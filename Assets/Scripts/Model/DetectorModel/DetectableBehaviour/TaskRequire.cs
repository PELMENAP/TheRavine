using System;
using UnityEngine;

[RequireComponent(typeof(DetectableObject))]
public class TaskRequire : MonoBehaviour
{
    [SerializeField] private string speechStart;
    [SerializeField] private string speechEnd;

    private bool alreadyCalled = true;

    public Action findOfDelivery;

    private IDetectableObject _idetectableobject;
    private void Awake()
    {
        _idetectableobject = this.GetComponent<DetectableObject>();
        _idetectableobject.OnGameObjectDetectedEvent += OnDetectedEvent;
        _idetectableobject.OnGameObjectDetectionReleasedEvent += OnDetectionReleasedEvent;
    }

    private void OnDetectedEvent(GameObject source, GameObject detectedObject)
    {
        if (source.CompareTag("Player") && alreadyCalled)
        {
            // StartCoroutine(PlayerDialogControoller.instance.TypeLine(speechStart));
        }
    }

    private void OnDetectionReleasedEvent(GameObject source, GameObject detectedObject)
    {
        if (source.CompareTag("Player") && alreadyCalled)
        {
            // StartCoroutine(PlayerDialogControoller.instance.TypeLine(speechEnd));
            findOfDelivery?.Invoke();
            alreadyCalled = false;
        }
    }
}

using UnityEngine;

public class DetectableObject : MonoBehaviour, IDetectableObject {
    public event ObjectDetectedHandler OnGameObjectDetectedEvent;
    public event ObjectDetectedHandler OnGameObjectDetectionReleasedEvent;

    public void Detected(GameObject detectionSource){
        //Debug.Log($"GameObject {name} was detected by {detectionSource.name}");
        OnGameObjectDetectedEvent?.Invoke(detectionSource, gameObject);
    }
    public void DetectionReleased(GameObject detectionSource){
        //Debug.Log($"GameObject {name} missed detection from {detectionSource.name}");
        OnGameObjectDetectionReleasedEvent?.Invoke(detectionSource, gameObject);
    }
}
using UnityEngine;

public interface IDetectableObject {
    event ObjectDetectedHandler OnGameObjectDetectedEvent;
    event ObjectDetectedHandler OnGameObjectDetectionReleasedEvent;

    GameObject gameObject {get;}

    void Detected(GameObject detectionSource);
    void DetectionReleased(GameObject detectionSource);
}
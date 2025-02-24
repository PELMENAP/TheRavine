using UnityEngine;
using UnityEngine.InputSystem;

using TheRavine.EntityControl;

[RequireComponent(typeof(Detector))]
public class InteractionRequire : MonoBehaviour
{
    [SerializeField] private PlayerModelView playerModelView;
    private IDetector _IDetector;
    private IInteractable _currentInteractable;
    private GameObject _currentDetectedObject;
    private void OnEnable()
    {
        _IDetector = this.GetComponent<Detector>();
        _IDetector.OnGameObjectDetectedEvent += OnDetectedEvent;
        _IDetector.OnGameObjectDetectionReleasedEvent += OnDetectionReleasedEvent;
    }

    private void OnDetectedEvent(GameObject source, GameObject detectedObject)
    {
        _currentDetectedObject = detectedObject;
        _currentInteractable = detectedObject.GetComponent<IInteractable>();

        if (_currentInteractable != null)
        {
            Debug.Log($"Interactable detected: {detectedObject.name}");
        }
    }

    private void OnDetectionReleasedEvent(GameObject source, GameObject detectedObject)
    {
        if (_currentDetectedObject == detectedObject)
        {
            _currentInteractable = null;
            _currentDetectedObject = null;
            Debug.Log($"Interaction ended with: {detectedObject.name}");
        }
    }
    private void OnDisable()
    {
        _IDetector.OnGameObjectDetectedEvent -= OnDetectedEvent;
        _IDetector.OnGameObjectDetectionReleasedEvent -= OnDetectionReleasedEvent;
    }
}
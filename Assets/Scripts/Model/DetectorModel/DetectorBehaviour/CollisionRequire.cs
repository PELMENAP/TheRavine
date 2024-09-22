using UnityEngine;

public class CollisionRequire : MonoBehaviour
{
    private IDetector _idetector;
    // private RoamMoveController controller;
    [SerializeField] private Transform detectCircle;

    private void Awake()
    {
        _idetector = GetComponentInParent<Detector>();
        // controller = GetComponentInParent<RoamMoveController>();
        _idetector.OnGameObjectDetectedEvent += OnDetectedEvent;
        // controller.setRandomPointComplete += OnSetRandomPointEvent;
        // controller.setRandomPointStart += OnAlreadySetRandomPointEvent;
    }

    private void OnSetRandomPointEvent()
    {
        detectCircle.gameObject.SetActive(true);
        // detectCircle.localPosition = controller.randomD;
    }

    private void OnAlreadySetRandomPointEvent()
    {
        detectCircle.gameObject.SetActive(false);
    }
    private void OnDetectedEvent(GameObject source, GameObject detectedObject)
    {
        // controller.UpdateRandomMove(true);
    }
}

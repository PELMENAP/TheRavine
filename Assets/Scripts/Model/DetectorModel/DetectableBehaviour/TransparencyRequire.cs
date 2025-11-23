using UnityEngine;

[RequireComponent(typeof(DetectableObject))]
public class TransparencyRequire : MonoBehaviour
{
    private IDetectableObject _idetectableobject;
    private MaterialPropertyBlock mat;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float fade;

    private void Awake()
    {
        _idetectableobject = this.GetComponent<DetectableObject>();
        _idetectableobject.OnGameObjectDetectedEvent += OnDetectedEvent;
        _idetectableobject.OnGameObjectDetectionReleasedEvent += OnDetectionReleasedEvent;
        mat = new MaterialPropertyBlock();
    }

    private void OnDetectedEvent(GameObject source, GameObject detectedObject)
    {
        if (source.CompareTag("Player"))
        {
            changeAlpha(fade);
        }
    }

    private void OnDetectionReleasedEvent(GameObject source, GameObject detectedObject)
    {
        if (source.CompareTag("Player"))
        {
            changeAlpha(1f);
        }
    }

    private void changeAlpha(float a)
    {
        mat.SetFloat("alpha", a);
        spriteRenderer.SetPropertyBlock(mat);
    }

    private void OnDestroy()
    {
        _idetectableobject.OnGameObjectDetectedEvent -= OnDetectedEvent;
        _idetectableobject.OnGameObjectDetectionReleasedEvent -= OnDetectionReleasedEvent;
    }
}

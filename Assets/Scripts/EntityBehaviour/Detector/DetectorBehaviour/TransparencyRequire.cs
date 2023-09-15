using UnityEngine;

[RequireComponent(typeof(DetectableObject))]
public class TransparencyRequire : MonoBehaviour
{
    private IDetectableObject _idetectableobject;
    private Material mat;

    private void Awake() {
        _idetectableobject = this.GetComponent<DetectableObject>();
        mat = this.transform.GetChild(0).gameObject.GetComponent<Renderer>().material;
        _idetectableobject.OnGameObjectDetectedEvent += OnDetectedEvent;
        _idetectableobject.OnGameObjectDetectionReleasedEvent += OnDetectionReleasedEvent;
    }

    private void OnDetectedEvent(GameObject source, GameObject detectedObject){
        if(source.tag == "Player"){
            changeAlpha(0.1f);
        }
    }

    private void OnDetectionReleasedEvent(GameObject source, GameObject detectedObject){
        if(source.tag == "Player"){
            changeAlpha(1f);
        }
    }

    private void changeAlpha(float a)
    {
        mat.SetFloat("alpha", a);
    }
}

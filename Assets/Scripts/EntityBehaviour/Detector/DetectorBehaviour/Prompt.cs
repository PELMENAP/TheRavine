using System.Collections;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(Detector))]
public class Prompt : MonoBehaviour
{
    [SerializeField] private TextMeshPro text;
    [SerializeField] private string hint;

    private bool isHinted;

    private IDetector _idetector;

    private void Awake()
    {
        isHinted = true;
        _idetector = (Detector)GetComponent("Detector");
        _idetector.OnGameObjectDetectedEvent += OnDetectedEvent;
    }

    private void OnDetectedEvent(GameObject source, GameObject detectedObject)
    {
        if (detectedObject.tag == "Dialoger" && isHinted)
        {
            text.text = hint;
            StartCoroutine(DialogProcess());
        }
    }

    private IEnumerator DialogProcess()
    {
        yield return new WaitForSeconds(3f);
        text.text = "";
        isHinted = false;
    }
}

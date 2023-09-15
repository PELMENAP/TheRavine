using System.Collections;
using UnityEngine;

[RequireComponent(typeof(DetectableObject))]
public class UpgradeSkill : MonoBehaviour
{
    private bool isUpgrade;
    [SerializeField] private float reloadSpeed, coolDown, power;
    [SerializeField] private string speech;
    [SerializeField] private Sprite sprite;
    private IDetectableObject _idetectableobject;

    private void Awake()
    {
        isUpgrade = true;
        _idetectableobject = (DetectableObject)GetComponent("DetectableObject");
        _idetectableobject.OnGameObjectDetectedEvent += OnDetectedEvent;
        _idetectableobject.OnGameObjectDetectionReleasedEvent += OnDetectionReleasedEvent;
    }

    private void OnDetectedEvent(GameObject source, GameObject detectedObject)
    {
        if (source.CompareTag("Player"))
        {
            StartCoroutine(WaitToUpgrade());
        }
    }

    private void OnDetectionReleasedEvent(GameObject source, GameObject detectedObject)
    {
        if (source.CompareTag("Player"))
        {
            StopCoroutine(WaitToUpgrade());
        }
    }

    private IEnumerator WaitToUpgrade()
    {
        while (isUpgrade)
        {
            yield return new WaitForSeconds(3);
            if (Input.GetKey("f"))
            {
                StartCoroutine(PlayerDialogControoller.instance.TypeLine(speech));
                PlayerController.instance.ui.RemoveSkill("Rush");
                PData.pdata.dushImage.sprite = sprite;
                PlayerController.instance.ui.AddSkill(new SkillRush(coolDown, reloadSpeed, power), PData.pdata.dushParent, PData.pdata.dushImage, "Rush");
                isUpgrade = false;
                yield return new WaitForSeconds(3);
                Destroy(this.gameObject);
            }
        }
    }
}

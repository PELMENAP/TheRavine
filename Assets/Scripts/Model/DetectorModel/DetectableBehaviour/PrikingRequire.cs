using UnityEngine;
using System.Threading.Tasks;

[RequireComponent(typeof(DetectableObject))]
public class PrikingRequire : MonoBehaviour
{
    [SerializeField] private SpriteRenderer render;
    [SerializeField] private Sprite normNettle;
    [SerializeField] private Sprite batteredNettle;
    private IDetectableObject _idetectableobject;
    private bool isDelay = true;

    private void Awake()
    {
        _idetectableobject = this.GetComponent<DetectableObject>();
        _idetectableobject.OnGameObjectDetectedEvent += OnDetectedEvent;
    }

    private void OnDetectedEvent(GameObject source, GameObject detectedObject)
    {
        if (source.tag == "Player")
        {
            if (isDelay)
            {
                // PlayerController.instance.Priking();
                render.sprite = batteredNettle;
                Delay();
            }
            else if (Random.Range(1, 10) == 1)
            {
                // PlayerController.instance.Priking();
            }
        }
    }

    private async void Delay()
    {
        isDelay = false;
        await Delation();
    }

    private async Task Delation()
    {
        await Task.Delay(100000);
        if (render != null)
        {
            render.sprite = normNettle;
        }
        isDelay = true;
    }
}

using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class SafeAreaContainer : MonoBehaviour
{
    private RectTransform _rectTransform;

    private void OnValidate()
        => _rectTransform ??= GetComponent<RectTransform>();

    private void Awake()
        => UpdateArea();

    private void UpdateArea()
    {
        Rect safeArea = Screen.safeArea;

        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;
        
        _rectTransform.anchorMin = anchorMin;
        _rectTransform.anchorMax = anchorMax;
    }
}

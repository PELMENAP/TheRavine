using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class SkillGraphLauncher : MonoBehaviour
{
    private const string KeyBinding = "<Keyboard>/i";
    private const int CanvasSortingOrder = 50;
    private const float CheckInterval = 0.5f;
    private static readonly Vector2 ButtonSize = new(150f, 44f);
    private static readonly Vector2 ButtonOffset = new(-16f, -16f);
    private static readonly Color ButtonColor = new(0.16f, 0.2f, 0.3f, 0.9f);

    private InputAction _key;
    private GameObject _button;
    private float _nextCheck;

    public static void Ensure()
    {
        if (FindFirstObjectByType<SkillGraphLauncher>() != null) return;

        var root = new GameObject("SkillGraphLauncher");
        DontDestroyOnLoad(root);
        root.AddComponent<SkillGraphLauncher>();
    }

    private void Awake()
    {
        _key = new InputAction("Skills", InputActionType.Button, KeyBinding);
        _key.performed += OnKey;
        _key.Enable();
        _button = BuildButton();
        _button.SetActive(false);
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + CheckInterval;
        _button.SetActive(SkillGraphPanel.TryGetLocalGraph(out _));
    }

    private void OnDestroy()
    {
        if (_key == null) return;
        _key.performed -= OnKey;
        _key.Dispose();
    }

    private static void OnKey(InputAction.CallbackContext context) => SkillGraphPanel.ToggleLocal();

    private GameObject BuildButton()
    {
        var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortingOrder;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);

        var buttonObject = new GameObject("SkillsButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(canvasObject.transform, false);
        var rect = (RectTransform)buttonObject.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = ButtonSize;
        rect.anchoredPosition = ButtonOffset;
        var image = buttonObject.GetComponent<Image>();
        image.color = ButtonColor;
        buttonObject.GetComponent<Button>().onClick.AddListener(SkillGraphPanel.ToggleLocal);

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        var labelRect = (RectTransform)labelObject.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = "Skills [I]";
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 22f;
        label.raycastTarget = false;
        return canvasObject;
    }
}

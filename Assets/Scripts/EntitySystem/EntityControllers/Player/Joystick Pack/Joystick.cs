using UnityEngine;
using UnityEngine.InputSystem;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

using TheRavine.Base;

public class Joystick : MonoBehaviour
{
    public float Horizontal { get { return (snapX) ? SnapFloat(input.x, AxisOptions.Horizontal) : input.x; } }
    public float Vertical { get { return (snapY) ? SnapFloat(input.y, AxisOptions.Vertical) : input.y; } }
    public Vector2 Direction { get { return new Vector2(Horizontal, Vertical); } }

    public float HandleRange
    {
        get { return handleRange; }
        set { handleRange = Mathf.Abs(value); }
    }

    public float DeadZone
    {
        get { return deadZone; }
        set { deadZone = Mathf.Abs(value); }
    }

    public AxisOptions AxisOptions { get { return AxisOptions; } set { axisOptions = value; } }
    public bool SnapX { get { return snapX; } set { snapX = value; } }
    public bool SnapY { get { return snapY; } set { snapY = value; } }

    [SerializeField] private InputActionReference point;
    [SerializeField] private float handleRange, deadZone;
    [SerializeField] private AxisOptions axisOptions = AxisOptions.Both;
    [SerializeField] private bool snapX = false, snapY = false;
    [SerializeField] private RectTransform background, handle;
    [SerializeField] private GameObject hoh;
    private RectTransform baseRect;

    private Canvas canvas;
    private Camera cam;

    private Vector2 input = Vector2.zero;
    private Vector2 radius;
    private Vector2 pivotOffset;

    private void Start()
    {
        HandleRange = handleRange;
        DeadZone = deadZone;
        baseRect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            Debug.LogError("The Joystick is not placed inside a canvas");

        Vector2 center = new Vector2(0.5f, 0.5f);
        background.pivot = center;
        handle.anchorMin = center;
        handle.anchorMax = center;
        handle.pivot = center;
        handle.anchoredPosition = Vector2.zero;
        radius = background.sizeDelta / 2;
        pivotOffset = baseRect.pivot * baseRect.sizeDelta;
        if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            cam = canvas.worldCamera;
        point.action.performed += OnDrag;
    }

    private bool isOver;

    private void HandleInput(float magnitude, Vector2 normalised, Vector2 radius)
    {
        if (magnitude < deadZone)
        {
            if (magnitude > 1)
                input = normalised;
            isOver = false;
        }
        else if (magnitude > deadZone + 1)
        {
            input = Vector2.zero;
            isOver = false;
        }
        else
        {
            input = normalised;
            isOver = true;
        }
    }

    private void FormatInput()
    {
        if (axisOptions == AxisOptions.Horizontal)
            input = new Vector2(input.x, 0f);
        else if (axisOptions == AxisOptions.Vertical)
            input = new Vector2(0f, input.y);
    }

    private float SnapFloat(float value, AxisOptions snapAxis)
    {
        if (value == 0)
            return value;

        if (axisOptions == AxisOptions.Both)
        {
            float angle = Vector2.Angle(input, Vector2.up);
            if (snapAxis == AxisOptions.Horizontal)
            {
                if (angle < 22.5f || angle > 157.5f)
                    return 0;
                else
                    return (value > 0) ? 1 : -1;
            }
            else if (snapAxis == AxisOptions.Vertical)
            {
                if (angle > 67.5f && angle < 112.5f)
                    return 0;
                else
                    return (value > 0) ? 1 : -1;
            }
            return value;
        }
        else
        {
            if (value > 0)
                return 1;
            else if (value < 0)
                return -1;
        }
        return 0;
    }
    Vector2 normal;

    // private void OnDrag() { }
    private void OnDrag(InputAction.CallbackContext context)
    {
        if (!DayCycle.closeThread)
            return;
        foreach (Touch touch in Touch.activeTouches)
        {
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    hoh.SetActive(false);
                    break;
                case TouchPhase.Moved:
                    Vector2 position = RectTransformUtility.WorldToScreenPoint(cam, background.position);
                    input = (touch.screenPosition - position) / (radius * canvas.scaleFactor);
                    normal = input.normalized;
                    FormatInput();
                    HandleInput(input.magnitude, normal, radius);
                    handle.anchoredPosition = input * radius * handleRange;
                    break;
                case TouchPhase.Ended:
                    input = Vector2.zero;
                    handle.anchoredPosition = Vector2.zero;
                    if (isOver)
                    {
                        hoh.SetActive(true);
                        input = normal;
                        isOver = !isOver;
                        handle.anchoredPosition = normal * radius * deadZone;
                    }
                    break;
            }
        }
    }
    private Vector2 ScreenPointToAnchoredPosition(Vector2 screenPosition)
    {
        Vector2 localPoint = Vector2.zero;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(baseRect, screenPosition, cam, out localPoint))
            return localPoint - (background.anchorMax * baseRect.sizeDelta) + pivotOffset;
        return Vector2.zero;
    }

    public void OnDisabling()
    {
        point.action.performed -= OnDrag;
    }
}

public enum AxisOptions { Both, Horizontal, Vertical }
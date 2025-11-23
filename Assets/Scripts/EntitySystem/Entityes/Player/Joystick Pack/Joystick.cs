using UnityEngine;
using UnityEngine.InputSystem;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public class Joystick : MonoBehaviour
{
    public Vector2 Aim => new(input.x, input.y);
    public Vector2 Movement => new(movement.x, movement.y);
    [SerializeField] private RectTransform background;
    [SerializeField] private RectTransform handle;
    [SerializeField] private float handleRange = 1f;
    [SerializeField] private float inputRange = 2f;
    private Vector2 input = Vector2.zero, movement = Vector2.zero;
    private Vector2 radius;
    
    private Camera cam;
    private Canvas canvas;

    [SerializeField] private InputActionReference point;
    [SerializeField] private GameObject hover;
    public void Activate()
    {
        canvas = GetComponentInParent<Canvas>();
        radius = background.sizeDelta / 2;
        cam = canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera : null;

        point.action.performed += OnDrag;
    }

    private bool isOver;
    private Vector2 normal;
    private void OnDrag(InputAction.CallbackContext context)
    {
        foreach (Touch touch in Touch.activeTouches)
        {
            switch (touch.phase)
            {
                case TouchPhase.Moved:
                    Vector2 position = RectTransformUtility.WorldToScreenPoint(cam, background.position);
                    Vector2 rawInput = (touch.screenPosition - position) / (radius * canvas.scaleFactor) * 2;
                    float magnitude = rawInput.magnitude;
                    normal = rawInput.normalized;

                    if(magnitude >= inputRange) return;

                    input = rawInput;
                    movement = rawInput;

                    if (magnitude > handleRange)
                    {
                        handle.anchoredPosition = normal * radius * handleRange;
                        isOver = true;
                    }

                    handle.anchoredPosition = input * radius;
                    break;
                case TouchPhase.Began:
                    hover.SetActive(false);
                    break;
                case TouchPhase.Ended:
                    input = Vector2.zero;
                    movement = Vector2.zero;
                    handle.anchoredPosition = Vector2.zero;
                    if (isOver)
                    {
                        hover.SetActive(true);
                        input = normal;
                        movement = normal;
                        isOver = !isOver;
                        handle.anchoredPosition = normal * radius * inputRange;
                    }
                    break;
            }
        }
    }

    public void OnDisabling()
    {
        point.action.performed -= OnDrag;
    }
}
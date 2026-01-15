using UnityEngine;
using UnityEngine.InputSystem;

public class PCController : IController
{
    private readonly InputActionReference MovementRef, RightClick;
    private readonly Mouse mouse;
    private readonly Camera cam;
    private readonly Transform playerTransform;
    private readonly float aimPlaneHeight;
    private readonly float maxAimDistance;

    public PCController(InputActionReference _MovementRef, InputActionReference _RightClick, Transform _playerTransform, float crosshairDistance, float planeHeightOffset = 0f)
    {
        mouse = Mouse.current;
        cam = Camera.main;
        MovementRef = _MovementRef;
        RightClick = _RightClick;
        playerTransform = _playerTransform;
        aimPlaneHeight = planeHeightOffset;
        maxAimDistance = crosshairDistance;
    }

    public Vector2 GetMove() => MovementRef.action.ReadValue<Vector2>();

    public Vector2 GetAim()
    {
        if (!RightClick.action.IsPressed()) return Vector2.zero;

        Vector3 playerPos = playerTransform.position;
        float planeY = playerPos.y + aimPlaneHeight;
        
        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        
        float denominator = ray.direction.y;
        if (Mathf.Abs(denominator) < 0.0001f) return Vector2.zero;

        float t = (planeY - ray.origin.y) / denominator;
        if (t < 0) return Vector2.zero;

        Vector3 hitPoint = ray.origin + ray.direction * t;
        Vector2 aimOffset = new(hitPoint.x - playerPos.x, hitPoint.z - playerPos.z);

        float distance = aimOffset.magnitude;
        if (distance > maxAimDistance)
        {
            aimOffset = aimOffset.normalized * maxAimDistance;
        }

        return aimOffset;
    }

    public void EnableView() { }
    public void DisableView() { }
    public void MeetEnds() { }
}

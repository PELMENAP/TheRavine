using UnityEngine;
using UnityEngine.InputSystem;

public class PCController : IController
{
    private readonly InputActionReference MovementRef;
    private readonly Mouse mouse;
    private readonly Camera cam;
    private readonly Transform playerTransform;
    private readonly float aimPlaneHeight;
    private readonly float maxAimDistance;

    public PCController(InputActionReference _MovementRef, Camera camera, Transform _playerTransform, float crosshairDistance, float planeHeightOffset = 0f)
    {
        mouse = Mouse.current;
        cam = camera;
        MovementRef = _MovementRef;
        playerTransform = _playerTransform;
        aimPlaneHeight = planeHeightOffset;
        maxAimDistance = crosshairDistance;
    }

    public Vector2 GetMove() => MovementRef.action.ReadValue<Vector2>();

    public Vector2 GetAim()
    {
        Vector3 playerPos = playerTransform.position;
        float planeY = playerPos.y + aimPlaneHeight;
        
        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        
        float denominator = ray.direction.y;
        if (Mathf.Abs(denominator) < 0.0001f) return Vector2.zero;

        float t = (planeY - ray.origin.y) / denominator;
        if (t < 0) return Vector2.zero;

        Vector3 hitPoint = ray.origin + ray.direction * t;

        Vector3 lineStart = hitPoint + Vector3.down * 10f;
        Vector3 lineEnd = hitPoint + Vector3.up * 10f; 
        Debug.DrawLine(lineStart, lineEnd, Color.red, 0.1f);

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

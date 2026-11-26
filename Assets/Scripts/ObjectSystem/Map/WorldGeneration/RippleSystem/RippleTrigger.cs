using UnityEngine;

public class RippleTrigger : MonoBehaviour
{
    [SerializeField] private float maxWaterRespondHeight = 5f;
    [SerializeField] private float minWaterRespondHeight = -2f;
    [SerializeField] private float minSpeedThreshold = 0.25f;
    [SerializeField] private float referenceSpeed = 6f;
    [SerializeField] private float baseRadius = 0.8f;
    [SerializeField] private float maxRadius = 1.6f;
    [SerializeField] private float maxStrength = 1f;
    [SerializeField] private float stampCooldown = 0.05f;

    private Vector3 _lastPos;
    private float _lastStampTime;

    private void OnEnable()
    {
        _lastPos = transform.position;
        _lastStampTime = -stampCooldown;
    }

    private void FixedUpdate()
    {
        Vector3 pos = transform.position;
        Vector3 delta = pos - _lastPos;
        _lastPos = pos;

        if (pos.y >= maxWaterRespondHeight || pos.y <= minWaterRespondHeight) return;

        float dt = Time.fixedDeltaTime;
        float speedSqr = delta.sqrMagnitude / (dt * dt);
        if (speedSqr < minSpeedThreshold * minSpeedThreshold) return;

        if (Time.time - _lastStampTime < stampCooldown) return;
        _lastStampTime = Time.time;

        var system = RippleStampSystem.Instance;
        if (system == null) return;

        float speed = Mathf.Sqrt(speedSqr);
        float t = Mathf.Clamp01(speed / Mathf.Max(referenceSpeed, 0.01f));
        float radius = Mathf.Lerp(baseRadius, maxRadius, t);
        float strength = Mathf.Lerp(0.15f, maxStrength, t);

        system.Stamp(pos, radius, strength);
    }
}
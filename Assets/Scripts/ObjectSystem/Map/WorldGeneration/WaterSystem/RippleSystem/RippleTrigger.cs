using UnityEngine;

public class RippleTrigger : MonoBehaviour
{

    private float _lastStampTime;
    private const float StampCooldown = 0.05f;

    private void FixedUpdate()
    {
        if (!transform.hasChanged || transform.position.y >= 4f) return;
        transform.hasChanged = false;
        if (Time.time - _lastStampTime < StampCooldown) return;
        _lastStampTime = Time.time;
        EmitRipple(1f, 1f);
    }

    private void EmitRipple(float radius, float strength)
    {
        RippleStampSystem.Instance.Stamp(transform.position, radius, strength);
    }
}
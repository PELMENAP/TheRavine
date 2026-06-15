using UnityEngine;

public class RippleSystem : MonoBehaviour
{

    private void FixedUpdate()
    {
        if (transform.hasChanged && transform.position.y < 4)
        {
            EmitRipple(1f, 1f);

            transform.hasChanged = false;
        }
    }

    private void EmitRipple(float radius, float strength)
    {
        RippleStampSystem.Instance.Stamp(transform.position, radius, strength);
    }
}
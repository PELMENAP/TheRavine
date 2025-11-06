using UnityEngine;

public class RippleSystem : MonoBehaviour
{
    [SerializeField] private ParticleSystem ripple;

    private bool inWater;

    private void Update()
    {
        if (transform.hasChanged)
        {
            transform.hasChanged = false;
        }
    }

    private void EmitRipple()
    {
        var emitParams = new ParticleSystem.EmitParams
        {
            position = transform.position,
            startSize = 5,
            startLifetime = 0.1f
        };
        ripple.Emit(emitParams, 1);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == 4 && !inWater)
        {
            inWater = true;
            EmitRipple();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer == 4 && inWater)
        {
            inWater = false;
            EmitRipple();
        }
    }
}
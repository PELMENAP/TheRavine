using UnityEngine;

public class PerceptionComponent : IComponent
{
    private const int BufferSize = 32;

    private readonly Collider[] _entityBuffer = new Collider[BufferSize];
    private readonly float _radius;
    private readonly float _radiusSqr;
    private readonly LayerMask _entityLayer;

    public PerceptionComponent(float radius, LayerMask entityLayer)
    {
        _radius = radius;
        _radiusSqr = radius * radius;
        _entityLayer = entityLayer;
    }

    public GameObject FindNearestEntity(Vector3 origin, GameObject self, out float distance)
    {
        int count = Physics.OverlapSphereNonAlloc(origin, _radius, _entityBuffer, _entityLayer);

        Collider best = null;
        float minSqr = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider c = _entityBuffer[i];
            if (c == null) continue;

            Vector3 p = c.transform.position;
            float dx = p.x - origin.x;
            float dy = p.y - origin.y;
            float dz = p.z - origin.z;
            float sqr = dx * dx + dy * dy + dz * dz;

            if (sqr > _radiusSqr || sqr >= minSqr) continue;
            if (c.gameObject == self) continue;

            minSqr = sqr;
            best = c;
        }

        distance = best != null ? Mathf.Sqrt(minSqr) : -1f;
        return best != null ? best.gameObject : null;
    }

    public void Dispose() { }
}
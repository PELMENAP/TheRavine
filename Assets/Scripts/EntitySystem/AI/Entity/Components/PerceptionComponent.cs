using UnityEngine;

public class PerceptionComponent : IComponent
{
    private static readonly Collider[] _buffer = new Collider[32];
    private readonly float _radius;
    private readonly LayerMask _entityLayer;
    private readonly LayerMask _foodLayer;

    public PerceptionComponent(float radius, LayerMask entityLayer, LayerMask foodLayer)
    {
        _radius = radius;
        _entityLayer = entityLayer;
        _foodLayer = foodLayer;
    }

    public GameObject FindNearestEntity(Vector3 origin, GameObject self, out float distance)
    {
        int count = Physics.OverlapSphereNonAlloc(origin, _radius, _buffer, _entityLayer);
        GameObject best = null;
        float minD = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (_buffer[i].gameObject == self) continue;
            float d = Vector3.Distance(origin, _buffer[i].transform.position);
            if (d < minD) { minD = d; best = _buffer[i].gameObject; }
        }
        distance = best != null ? minD : -1f;
        return best;
    }

    public Collider2D FindNearestFood(Vector2 origin) =>
        Physics2D.OverlapCircle(origin, _radius, _foodLayer);

    public void Dispose() { }
}
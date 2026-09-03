using UnityEngine;

public class PerceptionComponent : IComponent
{
    private static readonly Collider[] _entityBuffer = new Collider[32];
    private static readonly Collider[] _foodBuffer = new Collider[32];
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
        int count = Physics.OverlapSphereNonAlloc(origin, _radius, _entityBuffer, _entityLayer);
        GameObject best = null;
        float minD = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (_entityBuffer[i] == null) continue;
            var go = _entityBuffer[i].gameObject;
            if (go == self) continue;
            float d = Vector3.Distance(origin, go.transform.position);
            if (d < minD) { minD = d; best = go; }
        }
        distance = best != null ? minD : -1f;
        return best;
    }

    public GameObject FindNearestFood(Vector3 origin, out float distance)
    {
        int count = Physics.OverlapSphereNonAlloc(origin, _radius, _foodBuffer, _foodLayer);
        GameObject best = null;
        float minD = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (_foodBuffer[i] == null) continue;
            var go = _foodBuffer[i].gameObject;
            float d = Vector3.Distance(origin, go.transform.position);
            if (d < minD) { minD = d; best = go; }
        }
        distance = best != null ? minD : -1f;
        return best;
    }

    public void Dispose() { }
}
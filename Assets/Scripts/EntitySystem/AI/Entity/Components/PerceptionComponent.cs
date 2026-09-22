using UnityEngine;

public class PerceptionComponent : IComponent
{
    private readonly float _radius;
    private readonly EntitySpatialGrid _grid;

    public PerceptionComponent(float radius, EntitySpatialGrid grid)
    {
        _radius = radius;
        _grid   = grid;
    }

    public int FindEntitiesInRadius(Vector3 origin, GameObject self, GameObject[] result)
    {
        if (_grid == null)
        {
            for (int i = 0; i < result.Length; i++) result[i] = null;
            return 0;
        }
        return _grid.FindInRadius(origin, self, _radius, result);
    }

    public GameObject FindNearestEntity(Vector3 origin, GameObject self, out float distance)
    {
        if (_grid == null) { distance = -1f; return null; }
        return _grid.FindNearest(origin, self, _radius, out distance);
    }

    public void Dispose() { }
}
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

    public int FindEntitiesInRadius(Vector3 origin, EntityModel self, EntityModel[] result)
        => _grid != null ? _grid.FindInRadius(origin, self, _radius, result) : 0;

    public int FindEntitiesInRadius(Vector3 origin, EntityModel self, float radius, EntityModel[] result)
        => _grid != null ? _grid.FindInRadius(origin, self, radius, result) : 0;

    public EntityModel FindNearestEntity(Vector3 origin, EntityModel self, out float distance)
    {
        if (_grid == null) { distance = -1f; return null; }
        return _grid.FindNearest(origin, self, _radius, out distance);
    }

    public void Dispose() { }
}
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

    public EntityModel FindNearestEntity(Vector3 origin, EntityModel self, float radius, out float distance)
    {
        if (_grid == null) { distance = -1f; return null; }
        return _grid.FindNearest(origin, self, radius, out distance);
    }

    public EntityModel FindNearestEntity(Vector3 origin, EntityModel self, out float distance)
    {
        if (_grid == null) { distance = -1f; return null; }
        return _grid.FindNearest(origin, self, _radius, out distance);
    }

    public EntityModel Survey(Vector3 origin, EntityModel self, ColonyState colony,
        float nearestRadius, float crowdRadius, int crowdCap,
        out float nearestDistance, out int crowd, out int kin,
        out EntityModel foreign, out float foreignDistance,
        EntityModel lastVictim = null, float stickMul = 1f, float lowHpMul = 1f,
        StatsComponent familiarKey = null, float familiarMul = 1f)
    {
        if (_grid == null)
        {
            nearestDistance = -1f;
            foreignDistance = -1f;
            foreign = null;
            crowd = 0;
            kin   = 0;
            return null;
        }
        return _grid.Survey(origin, self, colony, nearestRadius, crowdRadius, crowdCap,
            out nearestDistance, out crowd, out kin, out foreign, out foreignDistance,
            lastVictim, stickMul, lowHpMul, familiarKey, familiarMul);
    }

    public void Dispose() { }
}
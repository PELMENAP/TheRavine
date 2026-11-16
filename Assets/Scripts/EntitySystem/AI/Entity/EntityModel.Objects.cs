using Unity.Mathematics;

using TheRavine.Generator;
using TheRavine.ObjectControl;

public partial class EntityModel
{
    public int  HeldObject { get; private set; } = ObjectInstInfo.PlaceholderId;
    public int  HeldAmount { get; private set; }
    public bool HasHeldObject => HeldObject != ObjectInstInfo.PlaceholderId;

    private MapGenerator       _map;
    private ObjectInfoRegistry _objectRegistry;
    private bool   _objectValid;
    private float2 _objectPos;
    private float  _objectDistance = -1f;

    public bool   CachedObjectValid    => _objectValid;
    public float2 CachedObjectPosition => _objectPos;
    public float  CachedObjectDistance => _objectDistance;

    private void ScanObject(in float2 self, in SimulationRules.RulesFrame r)
    {
        _objectValid = !HasHeldObject
            && ObjectSensor.TryFindNearest(_map, _objectRegistry, in self, r.ObjectSenseRadius, out _objectPos, out _objectDistance);
        if (!_objectValid) _objectDistance = -1f;
    }
}

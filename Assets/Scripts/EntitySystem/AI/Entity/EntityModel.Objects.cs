using Unity.Mathematics;

using TheRavine.Base;
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
    private int    _objectPrefab;
    private long   _heldOrigin;
    private float2 _objectPos;
    private float  _objectDistance = -1f;

    public bool   CachedObjectValid    => _objectValid;
    public float2 CachedObjectPosition => _objectPos;
    public float  CachedObjectDistance => _objectDistance;

    private void ScanObject(in float2 self, in SimulationRules.RulesFrame r)
    {
        _objectValid = !HasHeldObject && Nest != null && _warmth != null && r.ObjectFuelHeatPerUnit > 0f
            && ObjectSensor.TryFindNearest(_map, _objectRegistry, in self, r.ObjectSenseRadius,
                out _objectPos, out _objectDistance, out _objectPrefab);
        if (!_objectValid) _objectDistance = -1f;
    }

    public bool TryTakeObject()
    {
        if (!WorldAuthority.IsAuthority || HasHeldObject || !_objectValid || _map == null || _objectRegistry == null) return false;
        float reach = SimulationRules.Frame.EatRange;
        if (math.distancesq(Position2D, _objectPos) > reach * reach) return false;

        long cell = ChunkFoodIndex.CellOf(_objectPos.x, _objectPos.y);
        if (_map.Commands != null && _map.Commands.HasPendingRemove(cell)) return false;
        _objectValid = false;
        _objectDistance = -1f;
        if (!ObjectSensor.TryTakeFuel(_map, _objectRegistry, cell, _objectPrefab, out var item)) return false;

        HeldObject = item.PrefabID;
        HeldAmount = item.Amount;
        _heldOrigin = cell;
        Colony?.Stats.RecordInteraction(ColonyStats.Interaction.ObjectsTaken);
        return true;
    }

    public float DepositObject()
    {
        if (!WorldAuthority.IsAuthority || !HasHeldObject || !IsAtNest || _warmth == null) return 0f;
        float heat = ReleaseFuelHeat(Position2D);
        if (heat > 0f)
        {
            Colony?.Stats.RecordInteraction(ColonyStats.Interaction.ObjectsPlaced);
            Colony?.Stats.RecordInteraction(ColonyStats.Interaction.ObjectDeliveryEnergy, heat);
        }
        return heat;
    }

    public void DropHeldObject(float2 position)
    {
        if (!WorldAuthority.IsAuthority || !HasHeldObject || _map == null) return;
        var info = _objectRegistry?.Get(HeldObject);
        long cell = ChunkFoodIndex.CellOf(position.x, position.y);
        if (info != null && (_map.TryPlaceSnapped(cell, info, HeldAmount, 0) == PlacementResult.Ok ||
            (cell != _heldOrigin && _map.TryPlaceSnapped(_heldOrigin, info, HeldAmount, 0) == PlacementResult.Ok)))
        {
            ClearHeldObject();
            return;
        }
        if (_warmth != null) ReleaseFuelHeat(position);
    }

    private float ReleaseFuelHeat(float2 position)
    {
        float heat = HeldAmount * SimulationRules.Frame.ObjectFuelHeatPerUnit;
        if (!(heat > 0f) || !math.isfinite(heat)) return 0f;
        ClearHeldObject();
        _warmth.Add(_warmth.Index(position), heat);
        Ledger.Flows.FuelHeat += heat;
        Ledger.Flows.WarmthEmit += heat;
        return heat;
    }

    private void ClearHeldObject()
    {
        HeldObject = ObjectInstInfo.PlaceholderId;
        HeldAmount = 0;
    }
}

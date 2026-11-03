using Unity.Mathematics;

public partial class EntityModel
{
    public bool   HasHome      => Nest != null || _hasDen;
    public float2 HomePosition => Nest != null ? Nest.Position : _den;

    private bool ComputeAtHome(NestState nest, in float2 self, in SimulationRules.RulesFrame r)
    {
        if (nest != null)
            return nest.Contains(self, IsAtNest ? nest.Radius * math.max(r.NestExitRadiusMul, 1f) : nest.Radius);
        if (!_hasDen) return false;
        float radius = r.NestRadius * (IsAtNest ? math.max(r.NestExitRadiusMul, 1f) : 1f);
        return math.distancesq(self, _den) <= radius * radius;
    }

    private void SettleDen(int action, EntityCommandStatus status)
    {
        if (Nest != null || action != (int)EntityAction.Rest || status != EntityCommandStatus.Completed) return;
        if (_hasDen && IsAtNest) return;
        _den    = Position2D;
        _hasDen = true;
        IsAtNest = true;
    }
}

public readonly struct MoveResult
{
    public readonly float Distance;
    public readonly float PathCost;
    public readonly float EnergySpent;
    public readonly bool  Arrived;

    public MoveResult(float distance, float pathCost, float energySpent, bool arrived)
    {
        Distance    = distance;
        PathCost    = pathCost;
        EnergySpent = energySpent;
        Arrived     = arrived;
    }

    public float CostPerUnit => Distance > 1e-4f ? PathCost / Distance : 1f;

    public static readonly MoveResult None = new MoveResult(0f, 0f, 0f, false);
}
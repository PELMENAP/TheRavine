using UnityEngine;

public sealed partial class SimulationRules
{
    [SerializeField] private bool mateLociEnabled;
    [SerializeField] private float mateLocusMutation = 0.01f;
    [SerializeField] private int mateLociPairs = 24;
    [SerializeField] private float mateConflictLethality = 0.5f;
    [SerializeField] private int lineageCapacity = 16384;
    [SerializeField] private int lineageDepth = 4;

    public bool MateLociEnabled => mateLociEnabled;
    public float MateLocusMutation => mateLocusMutation;
    public int MateLociPairs => mateLociPairs;
    public float MateConflictLethality => mateConflictLethality;
    public int LineageCapacity => lineageCapacity;
    public int LineageDepth => lineageDepth;

    private void ValidateLineage()
    {
        mateLocusMutation = Mathf.Clamp01(mateLocusMutation);
        mateLociPairs = Mathf.Clamp(mateLociPairs, 0, MateLoci.Count * (MateLoci.Count - 1) / 2);
        mateConflictLethality = Mathf.Clamp01(mateConflictLethality);
        lineageCapacity = Mathf.Clamp(lineageCapacity, 16, 1 << 20);
        lineageDepth = Mathf.Clamp(lineageDepth, 0, LineageStore.MaxDepth);
    }
}

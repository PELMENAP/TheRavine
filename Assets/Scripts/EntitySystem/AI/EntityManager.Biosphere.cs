using Unity.Mathematics;
using UnityEngine;
using TheRavine.Base;
using TheRavine.EntityControl.Biosphere;

public partial class EntityManager
{
    [Header("Biosphere")]
    [SerializeField] private BiosphereRules biosphereRules;
    private CellBiosphere _biosphere;
    private readonly float2[] _biosphereAnchors = new float2[ColonyRegistry.MaxColonies];

    public CellBiosphere Biosphere => _biosphere;

    private void CreateBiosphere()
    {
        if (biosphereRules == null || !biosphereRules.simulationEnabled) return;
        _biosphere = new CellBiosphere(biosphereRules, _ledger);
        ServiceLocator.Register(_biosphere);
    }

    private void StepBiosphere()
    {
        if (_biosphere == null || !WorldAuthority.IsAuthority) return;
        int n = 0;
        for (int c = 0; c < _colonies.Count && n < _biosphereAnchors.Length; c++)
        {
            var colony = _colonies[c];
            if (colony.IsWild || colony.Nest == null) continue;
            _biosphereAnchors[n++] = colony.Nest.Position;
        }
        _biosphere.Step(SimulationClock.Time, new System.ReadOnlySpan<float2>(_biosphereAnchors, 0, n));
    }

    private void ReleaseBiosphere()
    {
        if (_biosphere == null) return;
        ServiceLocator.Services.Unregister(_biosphere);
        _biosphere = null;
    }
}

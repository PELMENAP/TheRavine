using TheRavine.Generator;
using TheRavine.EntityControl.Virology;

public sealed class ColonyFood
{
    private readonly int _maxFood;
    private ChunkFoodIndex _index;
    private float _lastEcologyTime;

    public ChunkFoodIndex Index => _index;
    public int FoodCount => _index != null ? _index.FoodCount : 0;

    public ColonyFood(int maxFood) => _maxFood = maxFood;

    public void Bind(ChunkFoodIndex index, float now)
    {
        _index           = index;
        _lastEcologyTime = now;
    }

    public void PruneUnloaded() => _index?.PruneUnloaded();

    public void Tick()
    {
        if (_index == null || _maxFood <= 0) return;

        var rules = SimulationRules.Active;
        float now = SimulationClock.Time;
        float dt  = now - _lastEcologyTime;
        if (dt < rules.EcologyTickInterval) return;
        _lastEcologyTime = now;
        if (dt > rules.EcologyMaxStep) dt = rules.EcologyMaxStep;

        float season = (1f + rules.SeasonAmplitude * SeasonClock.Value(now))
                     * (SeasonClock.IsWinter(now) ? rules.WinterGrowthMul : 1f);
        _index.TickEcology(dt, SimulationClock.TimeD, season, _maxFood);
    }

    public void DropCorpse(EntityModel model, DeathCause cause)
    {
        if (_index == null || model.Stats == null || model.Stats.IsDisposed) return;

        var virology = model.Virology;
        ViralPayload payload = default;
        bool infected = virology != null && virology.TryExtractPayload(out payload);

        var r = SimulationRules.Active;
        if (infected && virology.Modifiers.Liquefy >= r.LiquefyThreshold)
            _index.InfectNearby(model.DeathPosition, r.LiquefyRadiusCells, r.LiquefyMaxCells, in payload);

        bool tainted = cause == DeathCause.Virus || cause == DeathCause.Toxic;
        int  taboo   = tainted && model.Colony != null ? model.Colony.ColonyId : 0;
        _index.TryAddCorpse(model.DeathPosition, model.BodyEnergy, ref payload, infected, taboo);
    }
}

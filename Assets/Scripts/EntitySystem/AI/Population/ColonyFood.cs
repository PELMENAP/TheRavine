using TheRavine.Generator;
using TheRavine.EntityControl.Virology;

public sealed class ColonyFood
{
    private readonly int _maxFood;
    private readonly EnergyLedger _ledger;
    private ChunkFoodIndex _index;
    private float _lastEcologyTime;

    public ChunkFoodIndex Index => _index;
    public int FoodCount => _index != null ? _index.FoodCount : 0;

    public ColonyFood(int maxFood, EnergyLedger ledger)
    {
        _maxFood = maxFood;
        _ledger  = ledger;
    }

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
        _index.TickEcology(dt, SimulationClock.TimeD, season, _maxFood, out float spawned, out float matured, out float spoiled,
            out float soilReleased);
        ref var flows = ref _ledger.Flows;
        flows.FoodSpawn      += spawned;
        flows.FoodMaturation += matured;
        flows.CorpseSpoil    += spoiled;
        flows.SoilRelease    += soilReleased;
    }

    public void DropCorpse(EntityModel model, DeathCause cause)
    {
        if (model.Stats == null || model.Stats.IsDisposed) return;
        if (_index == null)
        {
            _ledger.Flows.DeathLoss += model.BodyEnergy;
            return;
        }

        var virology = model.Virology;
        ViralPayload payload = default;
        bool infected = virology != null && virology.TryExtractPayload(out payload);

        var r = SimulationRules.Active;
        if (infected && virology.Modifiers.Liquefy >= r.LiquefyThreshold)
            _index.InfectNearby(model.DeathPosition, r.LiquefyRadiusCells, r.LiquefyMaxCells, in payload);

        bool tainted = cause == DeathCause.Virus || cause == DeathCause.Toxic;
        int  taboo   = tainted && model.Colony != null ? model.Colony.ColonyId : 0;
        float body = model.BodyEnergy;
        if (_index.TryAddCorpse(model.DeathPosition, body, ref payload, infected, taboo, true)) return;

        var colony = model.Colony;
        if (body > 0f && colony != null && colony.Nest != null) colony.Nest.Storage += body;
        else _ledger.Flows.DeathLoss += body;
    }
}

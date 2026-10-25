using Unity.Mathematics;

public sealed class FitnessComponent : IComponent
{
    public int   FoodEaten      { get; private set; }
    public int   ReproduceCount { get; private set; }
    public float DamageDealt    { get; private set; }
    public float FinalFitness   { get; private set; }

    private float _cache;
    private float _cacheTime = float.NaN;
    private int   _epoch;
    private int   _cacheEpoch = -1;

    public void Register(EntityModel.FitnessEvent evt, float amount)
    {
        switch (evt)
        {
            case EntityModel.FitnessEvent.FoodEaten: FoodEaten++; break;
            case EntityModel.FitnessEvent.Reproduced: ReproduceCount++; break;
            case EntityModel.FitnessEvent.DamageDealt: DamageDealt += amount; break;
        }
        _epoch++;
    }

    public float Evaluate(float timeAlive)
    {
        if (_cacheEpoch == _epoch && _cacheTime == timeAlive)
            return _cache;

        ref readonly var rules = ref SimulationRules.Frame;

        float life     = timeAlive;
        float survival = rules.FitnessSurvivalWeight
                       * (1f - math.exp(-life / math.max(rules.FitnessSurvivalTau, 1e-3f)));

        float invTime = rules.FitnessRateWindow / math.max(life, rules.FitnessMinLifetime);

        float events = FoodEaten      * rules.FitnessFoodRateWeight
                     + ReproduceCount * rules.FitnessReproduceRateWeight
                     + DamageDealt    * rules.FitnessDamageRateWeight;

        _cache      = survival + events * invTime;
        _cacheTime  = timeAlive;
        _cacheEpoch = _epoch;
        return _cache;
    }

    public void Capture(float timeAlive) => FinalFitness = Evaluate(timeAlive);

    public void Dispose() { }
}

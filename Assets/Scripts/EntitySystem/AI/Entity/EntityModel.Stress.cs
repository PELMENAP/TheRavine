using Unity.Mathematics;

public partial class EntityModel
{
    private void UpdateStress(float dt, float environmentDrainMul, bool night, in SimulationRules.RulesFrame r)
    {
        if (!r.StressSourcesEnabled)
        {
            _stressSmooth = 0f;
            _aloneSeconds = 0f;
            StressLevel = CreatureCore.StressCalm;
            return;
        }

        _aloneSeconds = CreatureCore.StepAloneSeconds(_aloneSeconds, _kinNear > 0, dt);
        float energy = Stats.MaxEnergy > 0f ? Stats.En / Stats.MaxEnergy : 1f;
        bool exposed = !IsAtNest && (night || _winter);

        AddStress(CreatureCore.HungerStressRate(energy, in r) * dt, ColonyStats.StressSource.Hunger);
        AddStress(CreatureCore.ColdStressRate(environmentDrainMul, exposed, in r) * dt, ColonyStats.StressSource.Cold);
        AddStress(CreatureCore.LonelyStressRate(_aloneSeconds, in r) * dt, ColonyStats.StressSource.Lonely);

        _stressSmooth = CreatureCore.SmoothStress(_stressSmooth, Stress, dt, in r);
        StressLevel = CreatureCore.NextStressLevel(StressLevel, _stressSmooth, in r);
        Colony?.Stats.RecordStressTime(dt, StressLevel == CreatureCore.StressHigh);

        float effect = CreatureCore.StressEffect(_stressSmooth, StressLevel, in r);
        Brain.Context.SetStressScales(CreatureCore.StressLearningScale(effect, in r), CreatureCore.StressDiscountPower(effect, in r));
    }

    public void NoteTaskFailure()
    {
        ref readonly var r = ref SimulationRules.Frame;
        if (r.StressSourcesEnabled) AddStress(r.StressTaskFailure, ColonyStats.StressSource.Task);
    }
}

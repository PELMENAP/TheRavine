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
        AddStress(CreatureCore.ContagionStressRate(_alarmNear, _near, in r) * dt, ColonyStats.StressSource.Contagion);

        _stressSmooth = CreatureCore.SmoothStress(_stressSmooth, Stress, dt, in r);
        StressLevel = CreatureCore.NextStressLevel(StressLevel, _stressSmooth, in r);
        Colony?.Stats.RecordStressTime(dt, StressLevel == CreatureCore.StressHigh);

        float effect = CreatureCore.StressEffect(_stressSmooth, StressLevel, in r);
        Brain.Context.SetStressScales(CreatureCore.StressLearningScale(effect, in r), CreatureCore.StressDiscountPower(effect, in r));
    }

    public float RelieveStress(float amount, ColonyStats.ReliefKind kind)
    {
        float relieved = math.min(math.max(amount, 0f), Stress);
        if (!(relieved > 0f)) return 0f;
        Stress -= relieved;
        Colony?.Stats.RecordRelief(kind, relieved);
        return relieved;
    }

    internal void RestoreHelp(in TheRavine.Base.PopulationSnapshot.Agent saved)
    {
        if (_helpLog == null || saved.Help == null) return;
        float now = SimulationClock.Time;
        for (int i = 0; i < saved.Help.Length; i++)
        {
            var h = saved.Help[i];
            _helpLog.Restore(ManagerIndex, new HelpEntry
            {
                Partner = h.Partner, Balance = h.Balance, ComfortAccum = h.ComfortAccum,
                BalanceTime = Since(now, h.BalanceAge), ComfortTime = Since(now, h.ComfortAge),
                GaveTime = Since(now, h.GaveAge), ReceivedTime = Since(now, h.ReceivedAge), MetTime = Since(now, h.MetAge),
            });
        }
    }

    private static float Since(float now, float age) => age < 0f ? HelpLog.Never : now - age;

    public void NoteTaskFailure()
    {
        ref readonly var r = ref SimulationRules.Frame;
        if (r.StressSourcesEnabled) AddStress(r.StressTaskFailure, ColonyStats.StressSource.Task);
    }
}

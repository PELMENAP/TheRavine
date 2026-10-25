using UnityEngine;
using TheRavine.EntityControl;
using System;
using Unity.Mathematics;

using TheRavine.Base;
using TheRavine.Extensions;
using TheRavine.Generator;
using TheRavine.EntityControl.Virology;

public partial class EntityModel
{
    public void ApplyUtilityPrior(ReadOnlySpan<float> prior)
    {
        if (!_cycleActive || prior.Length < PlanCatalog.Count) return;
        float w = SimulationRules.Frame.UtilityPriorWeight;
        var bias = Brain.Context.CoordBias;
        for (int p = 0; p < PlanCatalog.Count; p++) bias[p] += w * prior[p];
    }

    public void ApplyInstinct(in InstinctOutput output)
    {
        InstinctLatches = output.Latches;
        if (!_cycleActive) return;

        if (output.Interrupt != 0 || output.Reflex != InstinctBits.NoReflex) _plan.Interrupt();
        if (output.Reflex == InstinctBits.NoReflex)
        {
            TrySummit();
            return;
        }

        int action = output.Reflex;
        float2 heading = float2.zero;
        var nest = Nest;
        if ((output.Mask & InstinctBits.Sick) != 0 && nest != null)
            heading = math.normalizesafe(Position2D - nest.Position, new float2(0f, 1f));
        var reflex = new BrainDecision(action, 0, 0, Brain.CurrentGoal, _cycleNow,
            ActionDurationTable.Max(action), in heading);
        TryStartCommand(action, in reflex, CommandSource.Instinct);
    }

    private void TrySummit()
    {
        ref readonly var r = ref SimulationRules.Frame;
        if (Virology == null || Virology.Modifiers.Summit < r.SummitThreshold) return;
        if (Stats.Hp / Stats.MaxHealth >= r.SummitHpFraction || !_lastTerrain.IsValid) return;
        if (_runner.IsRunning && _runner.CurrentSource == CommandSource.Instinct) return;

        float2 uphill = math.normalizesafe(new float2(_lastTerrain.GradX, _lastTerrain.GradZ));
        if (math.lengthsq(uphill) < 1e-6f) return;

        _plan.Interrupt();
        int action = (int)EntityAction.Wander;
        var climb = new BrainDecision(action, 0, 0, Brain.CurrentGoal, _cycleNow,
            ActionDurationTable.Max(action), in uphill);
        TryStartCommand(action, in climb, CommandSource.Instinct);
    }

    public bool TryStartCommand(int action, in BrainDecision decision, CommandSource source)
    {
        var cmd = (uint)action < (uint)_commands.Length ? _commands[action] : null;
        if (cmd == null) return false;

        if (!cmd.CanExecute())
        {
            float penalty = SimulationRules.Active.InfeasibleActionHealthPenalty;
            if (source == CommandSource.Brain && penalty > 0f) Stats.Hp -= penalty;
            return false;
        }

        SetLastAction(action);
        _runner.Start(cmd, in decision, source);
        return true;
    }

    public void OnCommandFinished(CommandSource source, EntityCommandStatus status, float reward, int action)
    {
        if (source == CommandSource.Instinct) return;
        _plan.OnStepFinished(status, reward, action);
    }

    public void OnPlanEnded(PlanKind plan, float total, bool achieved)
    {
        if (plan >= PlanKind.Count) return;
        ref readonly var r = ref SimulationRules.Frame;
        float a = r.PlanRewardEmaAlpha;
        _planRewardEma[(int)plan] = math.lerp(_planRewardEma[(int)plan], total, a);
        _dopamine = math.lerp(_dopamine, math.tanh(total), a);

        if (!CasteFixed || IsJuvenile || IsWild || _thresholds == null) return;
        if (r.PlanCasteDomain[(int)plan] != (int)Caste) return;
        _thresholds.Reinforce(ManagerIndex, Caste, achieved);
    }

    public void TakeDamage(float amount, EntityModel source, DeathCause cause = DeathCause.Killed)
    {
        if (amount <= 0f || Stats == null || Stats.IsDisposed) return;
        ref readonly var r = ref SimulationRules.Frame;

        if (IsAtNest && LastAction == EntityAction.Rest) amount *= r.NestRestDamageMul;
        amount *= _casteMods.DamageTaken;
        if (source != null) Points?.MarkNegative(Position2D, r.NegativePoiHit);
        Mortality?.NoteHarm(cause, Stats.Hp - amount);
        Stats.Hp -= amount;
        if (source != null && !ReferenceEquals(source, this))
        {
            _lastAttacker   = source;
            _lastAttackTime = SimulationClock.TimeD;
            _threatSource   = source.Position2D;
            _threatResolved = true;
        }

        var nest = Nest;
        if (nest == null) return;
        float norm = amount / Stats.MaxHealth;
        nest.Mark(ColonyChannel.Danger, Position2D, norm * r.NestDangerMark);
        if (IsAtNest) nest.RaiseAlarm(norm * r.AlarmFromDamage);

        double now = SimulationClock.TimeD;
        if (now - _lastAlarmCry < r.AlarmCryCooldown || IsDisposed || Stats.Hp <= 0f) return;
        _lastAlarmCry = now;
        Broadcast(r.AlarmCrySpeech);
        nest.RaiseAlarm(r.AlarmCryAmount);
    }

    public void Broadcast(float4 speech)
    {
        Speech.SetOwn(speech);
        if (Perception == null) return;

        ref readonly var r = ref SimulationRules.Frame;
        bool warn = _threatResolved && HasDirectThreat;
        float2 source = _threatSource;
        double until = SimulationClock.TimeD + r.WarnWindow;

        int found = Perception.FindEntitiesInRadius(Motor.Position(), this, r.SpeechRadius, Neighbors);
        for (int i = 0; i < found; i++)
        {
            var e = Neighbors[i];
            Neighbors[i] = null;
            if (e == null || e.IsDisposed || e.IsDeathPending) continue;
            e.Speech.ReceiveVector(speech);
            if (warn && e.Colony == Colony) e.Warn(until, source);
        }
    }
}

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
    private float SenescenceMul(in SimulationRules.RulesFrame r)
    {
        float onset = math.saturate(r.SenescenceOnset);
        float over  = math.saturate((AgeFraction - onset) / math.max(1f - onset, 1e-3f));
        return 1f + r.SenescenceUpkeep * over;
    }

    private void UpdateOrnsteinUhlenbeck(float dt, in SimulationRules.RulesFrame r)
    {
        float u1 = RavineRandom.RangeFloat(1e-4f, 1f);
        float u2 = RavineRandom.RangeFloat(0f, 1f);
        float n  = math.sqrt(-2f * math.log(u1)) * math.cos(2f * math.PI * u2);
        _ouAngle += -_ouAngle * dt / math.max(r.OuTau, 1e-3f) + r.OuSigma * math.sqrt(dt) * n;
    }

    private void UpdateHormones(float dt, in SimulationRules.RulesFrame r)
    {
        float k = 1f - math.exp(-dt / math.max(r.HormoneTau, 1e-3f));
        _cortisol = math.lerp(_cortisol, Stress, k);
        _oxytocin = math.lerp(_oxytocin, math.saturate(_kinCrowd / math.max(r.OxytocinNorm, 1e-3f)), k);
        _ghrelin  = math.lerp(_ghrelin, 1f - math.saturate(EffectiveEnergyNorm), k);
    }

    private float TemperatureScale(EntityBrainContext ctx, in SimulationRules.RulesFrame r)
    {
        float x = -r.TempDopamine * _dopamine + r.TempCortisol * _cortisol + r.TempSurprise * ctx.Surprise;
        return math.clamp(math.exp(x), r.TempMin, r.TempMax);
    }

    public float Drive()
    {
        ref readonly var r = ref SimulationRules.Frame;
        float stored = Digestion != null ? Digestion.Stomach : 0f;
        float en = math.saturate((Stats.En + stored * r.DigestEffActive) / Stats.MaxEnergy);
        float hp = math.saturate(Stats.Hp / Stats.MaxHealth);
        float de = 1f - en, dh = 1f - hp;
        return math.sqrt(r.DriveEnergyWeight * de * de + r.DriveHealthWeight * dh * dh);
    }

    public void BeginHomeostasis()
    {
        _decisionStart    = SimulationClock.TimeD;
        _decisionIntegral = _driveIntegral;
        _decisionDrive    = Stats != null && !Stats.IsDisposed ? Drive() : 0f;
    }

    public float HomeostaticReturn()
    {
        if (Stats == null || Stats.IsDisposed) return 0f;
        ref readonly var r = ref SimulationRules.Frame;

        float elapsed  = (float)(SimulationClock.TimeD - _decisionStart);
        float integral = (float)(_driveIntegral - _decisionIntegral);
        float shaping  = _decisionDrive - DelayedPerceptron.DiscountTime(r.ExecGammaPerSecond, elapsed) * Drive();
        return -r.DriveRewardWeight * integral + r.DriveShapingWeight * shaping;
    }
}

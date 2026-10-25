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
    public void DepositTrail()
    {
        var nest = Nest;
        if (nest == null || _trail == null) return;
        float amount = SimulationRules.Frame.TrailDeposit;
        int cap = _trail.Length;
        for (int i = 0; i < _trailCount; i++)
            nest.Mark(ColonyChannel.Trail, _trail[(_trailHead - 1 - i + cap) % cap], amount);
        _trailCount = 0;
    }

    public void ShareMemoryWithColony()
    {
        var colony = Colony;
        if (colony == null) return;
        for (int i = 0; i < Points.Count; i++)
        {
            ref readonly var p = ref Points.At(i);
            colony.Pois.Offer(p.Position, p.EnergyEma, 0f);
        }
    }

    private void SampleTrail(in float2 self, double now, in SimulationRules.RulesFrame r)
    {
        if (_trail == null || now < _nextTrailSample) return;
        _nextTrailSample = now + r.TrailSampleInterval;
        _trail[_trailHead] = self;
        _trailHead = (_trailHead + 1) % _trail.Length;
        if (_trailCount < _trail.Length) _trailCount++;
    }

    public EntityModel FindNearbyJuvenile(float radius)
    {
        int found = Perception.FindEntitiesInRadius(Motor.Position(), this, radius, ForeignScratch);
        EntityModel best = null;
        float bestFill = float.MaxValue;
        for (int i = 0; i < found; i++)
        {
            var e = ForeignScratch[i];
            ForeignScratch[i] = null;
            if (e == null || e.Colony != Colony || !e.IsJuvenile) continue;
            float fill = e.Digestion.Fill;
            if (fill >= bestFill) continue;
            bestFill = fill;
            best     = e;
        }
        return best;
    }

    private bool HasForeignEntities()
    {
        int populated = 0;
        for (int c = 0; c < _registry.Count; c++)
            if (_registry[c].MemberCount > 0 && ++populated > 1) return true;
        return false;
    }

    private bool ComputeNestFood(NestState nest, in SimulationRules.RulesFrame r)
    {
        if (nest == null || !IsAtNest) return false;
        float storage = nest.Storage;
        if (storage < r.NestEatPortion * r.NestFoodMinPortionFraction) return false;
        if (storage >= r.NestJuvenileReserve || IsJuvenile || Caste == Caste.Nurse) return true;
        var colony = Colony;
        if (colony == null || colony.JuvenileCount == 0) return true;
        return FindNearbyJuvenile(nest.Radius) == null;
    }

    private bool ResolveThreatSource(in float2 self, float localDanger, in SimulationRules.RulesFrame r, out float2 source)
    {
        var attacker = RecentAttacker;
        if (attacker != null)
        {
            source = attacker.Position2D;
            return true;
        }

        var foreign = ValidForeign;
        if (foreign != null && (!_playerValid || _nearestForeignDistance <= _playerDistance))
        {
            source = foreign.Position2D;
            return true;
        }
        if (_playerValid)
        {
            source = _playerPos;
            return true;
        }

        if (IsWarned)
        {
            source = _warnSource;
            return true;
        }

        source = default;
        var nest = Nest;
        if (nest == null || localDanger <= r.ThreatMinDanger) return false;

        float radius = Tuning.DetectionRadius * r.FleeDistanceMul;
        float spread = nest.SampleDirection(self, radius, r.WanderGradientSamples, 0f, 0f, -1f, out float2 toDanger);
        if (spread <= r.WanderGradientMin) return false;
        source = self + toDanger * radius;
        return true;
    }

    private EntityModel FindNearestForeign(Vector3 pos, float radius, out float distance)
    {
        distance = -1f;
        if (_registry == null) ServiceLocator.Services.TryGet(out _registry);
        if (_registry == null || !HasForeignEntities()) return null;

        int found = Perception.FindEntitiesInRadius(pos, this, radius, ForeignScratch);
        EntityModel best = null;
        float bestD = float.MaxValue;
        for (int i = 0; i < found; i++)
        {
            var e = ForeignScratch[i];
            ForeignScratch[i] = null;
            if (e == null || e.Colony == Colony) continue;
            float d = math.distancesq((float3)e.Motor.Position(), (float3)pos);
            if (d >= bestD) continue;
            bestD = d;
            best  = e;
        }
        if (best != null) distance = math.sqrt(bestD);
        return best;
    }

    private static int CountAndRelease(int found, ColonyState colony, out int kin)
    {
        kin = 0;
        for (int i = 0; i < found; i++)
        {
            var e = Neighbors[i];
            Neighbors[i] = null;
            if (e != null && e.Colony == colony) kin++;
        }
        return found;
    }

    private static int CountAndRelease(int found)
    {
        for (int i = 0; i < found; i++) Neighbors[i] = null;
        return found;
    }

    public int RecruitNearby(float2 target, int count, float radius)
    {
        if (count <= 0 || Perception == null) return 0;
        double until = SimulationClock.TimeD + SimulationRules.Frame.RecruitWindow;
        int found = Perception.FindEntitiesInRadius(Motor.Position(), this, radius, Neighbors);
        int n = 0;
        for (int i = 0; i < found; i++)
        {
            var e = Neighbors[i];
            Neighbors[i] = null;
            if (n >= count || e == null || e.Colony != Colony || e.IsDisposed || e.IsDeathPending) continue;
            if (e.IsJuvenile || e.IsHungry || e.IsRecruited || e.IsCommandRunning || e.Plan.IsActive) continue;
            e.Recruit(target, until);
            n++;
        }
        return n;
    }

    public EntityModel FindHungriestKin(float radius)
    {
        int found = Perception.FindEntitiesInRadius(Motor.Position(), this, radius, Neighbors);
        EntityModel best = null;
        float bestEnergy = float.MaxValue;
        for (int i = 0; i < found; i++)
        {
            var e = Neighbors[i];
            Neighbors[i] = null;
            if (e == null || e.Colony != Colony || e.IsDisposed || e.IsDeathPending) continue;
            float en = e.EffectiveEnergyNorm;
            if (en >= bestEnergy) continue;
            bestEnergy = en;
            best = e;
        }
        return best;
    }

    public EntityModel FindSickestKin(float radius, float minLoad)
    {
        int found = Perception.FindEntitiesInRadius(Motor.Position(), this, radius, Neighbors);
        EntityModel best = null;
        float bestLoad = minLoad;
        for (int i = 0; i < found; i++)
        {
            var e = Neighbors[i];
            Neighbors[i] = null;
            if (e == null || e.Colony != Colony || e.IsDisposed || e.IsDeathPending || e.Virology == null) continue;
            e.Virology.TryGetViralInputs(out float load, out _, out _);
            if (load < bestLoad) continue;
            bestLoad = load;
            best = e;
        }
        return best;
    }
}

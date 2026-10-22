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
    private void UpdateHeading()
    {
        float2 p = Position2D;
        if (_hasLastPos)
        {
            float2 d = p - _lastPos;
            if (math.lengthsq(d) > 1e-6f)
                Heading = math.normalizesafe(math.lerp(Heading, math.normalize(d), SimulationRules.Frame.HeadingEmaAlpha));
        }
        _lastPos    = p;
        _hasLastPos = true;
    }

    private void UpdateTerritory(NestState nest, in float2 self, float dt, in SimulationRules.RulesFrame r)
    {
        _onForeignTerritory = false;
        if (IsWild || nest == null) return;

        var foreign = ValidForeign;
        if (foreign != null && nest.FieldAt(ColonyChannel.Territory, foreign.Position2D) > r.TerritoryThreshold)
            nest.RaiseAlarm(r.TerritoryAlarm * dt);

        if (_registry == null) return;
        for (int c = 0; c < _registry.Count; c++)
        {
            var other = _registry[c];
            if (other == Colony || other.IsWild) continue;
            if (other.Nest.FieldAt(ColonyChannel.Territory, self) <= r.TerritoryThreshold) continue;
            _onForeignTerritory = true;
            return;
        }
    }

    private void ComputeDirections(NestState nest, in float2 self, in float2 foodPos, in SimulationRules.RulesFrame r)
    {
        _dirMask = 0;
        if (_foodValid) SetDirectionTarget(DirectionKind.Food, in self, in foodPos, in r);
        if (nest != null && nest.HasFoodPeak) SetDirectionTarget(DirectionKind.NestFood, in self, nest.FoodPeak, in r);
        if (Points.TryPickBest(in self, out float2 own)) SetDirectionTarget(DirectionKind.OwnPoi, in self, in own, in r);
        var colony = Colony;
        if (colony != null && colony.Pois.TryPickBest(self, out float2 shared, out _))
            SetDirectionTarget(DirectionKind.ColonyPoi, in self, in shared, in r);

        if (nest != null)
        {
            float phase = RavineRandom.RangeFloat(0f, 2f * math.PI);
            nest.SampleExtreme(self, r.FrontierRadius, r.WanderGradientSamples, phase, ColonyChannel.Explored, true, out float2 frontier);
            SetDirection(DirectionKind.Frontier, in frontier, r.FrontierRadius);
            if (nest.MigrationPressure > r.MigrationMinPressure)
                SetDirection(DirectionKind.Migration, math.normalizesafe(nest.Migration), r.MigrateLegRadius);
        }

        if (_flock.Neighbors > 0) SetDirection(DirectionKind.Flock, in _flock.CentroidDir, r.BoidRadius * r.FlockDirectionRadiusMul);
        _dirMask |= 1 << (int)DirectionKind.Levy;
    }

    private void SetDirectionTarget(DirectionKind kind, in float2 self, in float2 target, in SimulationRules.RulesFrame r)
    {
        float2 d = target - self;
        float  l = math.length(d);
        if (l < r.DirectionMinDistance) return;
        SetDirection(kind, d / l, math.min(l, r.PlannerRadiusMax));
    }

    private void SetDirection(DirectionKind kind, in float2 dir, float radius)
    {
        if (math.lengthsq(dir) < 1e-6f) return;
        _dirVec[(int)kind]    = dir;
        _dirRadius[(int)kind] = radius;
        _dirMask |= (byte)(1 << (int)kind);
    }

    public bool TryResolveDirection(byte kind, float offset, out float2 dir, out float radius)
    {
        ref readonly var r = ref SimulationRules.Frame;
        dir    = float2.zero;
        radius = 0f;
        if (kind >= (byte)DirectionKind.Count) return false;

        if (kind == (byte)DirectionKind.Levy)
        {
            float a = RavineRandom.RangeFloat(0f, 2f * math.PI);
            math.sincos(a, out float sa, out float ca);
            dir    = new float2(ca, sa);
            radius = math.min(LevyStep(in r), r.PlannerRadiusMax);
        }
        else
        {
            if ((_dirMask & (1 << kind)) == 0) return false;
            dir    = _dirVec[kind];
            radius = _dirRadius[kind];
        }

        math.sincos(offset + _ouAngle, out float s, out float c);
        dir = new float2(dir.x * c - dir.y * s, dir.x * s + dir.y * c);
        return true;
    }

    public float LevyStep(in SimulationRules.RulesFrame r)
    {
        float alpha = math.max(r.LevyAlpha * _casteMods.LevyAlpha, 1e-3f);
        float u = RavineRandom.RangeFloat(1e-4f, 1f);
        return math.clamp(r.LevyMinStep * math.pow(u, -1f / alpha), r.LevyMinStep, r.LevyMaxStep);
    }

    private static float2 DirectionTo(in float2 from, in float2 to)
    {
        float2 d  = to - from;
        float  l2 = math.lengthsq(d);
        return l2 > 1e-6f ? d * math.rsqrt(l2) : float2.zero;
    }
}

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
    public void TickWild()
    {
        if (!_cycleActive) return;
        ref readonly var r = ref SimulationRules.Frame;

        float2 self = Position2D;
        if (!_wildHomeSet)
        {
            _wildHome    = self;
            _wildHomeSet = true;
        }

        float hp     = Stats.Hp / Stats.MaxHealth;
        bool hungry  = EffectiveEnergyNorm < r.PredatorHungerOn;
        bool flee    = hp < r.PredatorFleeHp && _threatResolved;
        bool meat    = _foodValid && _foodKind == FoodKind.Meat;
        var  prey    = hungry ? CachedHuntTarget : default;
        bool urgent  = flee || (hungry && (meat || prey.IsValid));

        if (_runner.IsRunning)
        {
            int current = _runner.CurrentAction;
            bool calm = current == (int)EntityAction.Wander || current == (int)EntityAction.Idle
                     || current == (int)EntityAction.Rest;
            if (!calm || !urgent) return;
        }

        if (flee)                                        StartWild(EntityAction.Flee, ActionDurationTable.Max((int)EntityAction.Flee));
        else if (hungry && meat && _foodDistance <= r.EatRange && !IsSated) StartWild(EntityAction.Eat, ActionDurationTable.Max((int)EntityAction.Eat));
        else if (hungry && meat)                         StartWild(EntityAction.ApproachFood, ActionDurationTable.Max((int)EntityAction.ApproachFood));
        else if (prey.IsValid)
        {
            _wildHome    = prey.Position2D;
            _wildHasGoal = false;
            StartWild(EntityAction.Attack, ActionDurationTable.Max((int)EntityAction.Attack));
        }
        else if (!hungry && hp < 1f)                     StartWild(EntityAction.Rest, r.PredatorRestSeconds);
        else if (_wildLoiter)
        {
            _wildLoiter = false;
            StartWild(EntityAction.Idle, RavineRandom.RangeFloat(r.PredatorLoiterMin, math.max(r.PredatorLoiterMin, r.PredatorLoiterMax)));
        }
        else Roam(in self, in r);
    }

    private void Roam(in float2 self, in SimulationRules.RulesFrame r)
    {
        if (_wildHasGoal && math.distance(self, _wildGoal) <= r.PlanArriveRadius)
        {
            _wildHasGoal = false;
            _wildLoiter  = true;
            StartWild(EntityAction.Idle, RavineRandom.RangeFloat(r.PredatorLoiterMin, math.max(r.PredatorLoiterMin, r.PredatorLoiterMax)));
            return;
        }

        if (!_wildHasGoal)
        {
            float a = RavineRandom.RangeFloat(0f, 2f * math.PI);
            math.sincos(a, out float sa, out float ca);
            float2 goal = self + new float2(ca, sa) * r.PredatorRoamDistance;
            float2 fromHome = goal - _wildHome;
            if (math.lengthsq(fromHome) > r.PredatorHomeRange * r.PredatorHomeRange)
                goal = _wildHome + math.normalizesafe(fromHome) * math.max(r.PredatorHomeRange - r.PredatorRoamDistance, 0f);
            _wildGoal    = goal;
            _wildHasGoal = true;
        }

        float dist = math.distance(self, _wildGoal);
        float time = dist / math.max(Tuning.MoveSpeed * SpeedMul, r.TimingMinSpeed) * r.LegTimeSlack;
        var decision = new BrainDecision((int)EntityAction.Wander, 0, 0, SharedHierarchicalBrain.Goal.Hunt, _cycleNow,
            time, float2.zero, destination: _wildGoal, hasDestination: true);
        if (!TryStartCommand((int)EntityAction.Wander, in decision, CommandSource.Instinct)) _wildHasGoal = false;
    }

    private void StartWild(EntityAction action, float duration)
    {
        var decision = new BrainDecision((int)action, 0, 0, SharedHierarchicalBrain.Goal.Hunt, _cycleNow,
            duration, float2.zero);
        if (TryStartCommand((int)action, in decision, CommandSource.Instinct)) return;
        Roam(Position2D, in SimulationRules.Frame);
    }
}

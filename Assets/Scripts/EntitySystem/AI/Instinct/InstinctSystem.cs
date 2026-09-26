using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

public static class InstinctBits
{
    public const uint Hungry = 1u << 0;
    public const uint Panic  = 1u << 1;
    public const uint Night  = 1u << 2;
    public const uint Carry  = 1u << 3;
    public const uint Tether = 1u << 4;
    public const uint Eat    = 1u << 5;
    public const uint Leash  = 1u << 6;
    public const uint Seek   = 1u << 7;

    public const byte NoReflex = byte.MaxValue;
}

public struct InstinctInput
{
    public float HpFraction;
    public float EnergyFraction;
    public float Stomach;
    public float Carrying;
    public float LocalDanger;
    public float Stress;
    public float Alarm;
    public float EntityDistance;
    public float FoodDistance;
    public float ParentDistance;
    public float Age;
    public float NestDistance;
    public float Now;
    public byte  Caste;
    public byte  AtNest;
    public byte  NestFood;
    public byte  FoodToxic;
    public byte  Night;
    public byte  Warned;
    public byte  HasThreat;
    public byte  Busy;
    public byte  RunningAction;
    public byte  RunningInstinct;
    public byte  PlanActive;
    public byte  PlanKind;
}

public struct InstinctGenes
{
    public float HungerOn;
    public float HungerOff;
    public float SatedFill;
    public float PanicHp;
    public float PanicDanger;

    public static InstinctGenes From(in GeneticParameters g) => new()
    {
        HungerOn    = g.HungerOn,
        HungerOff   = g.HungerOff,
        SatedFill   = g.SatedFill,
        PanicHp     = g.PanicHp,
        PanicDanger = g.PanicDanger,
    };
}

public struct InstinctState
{
    public uint  Latches;
    public float NextHungerInterrupt;
}

public struct InstinctOutput
{
    public uint Mask;
    public uint Latches;
    public byte Reflex;
    public byte Interrupt;
    public byte NeedsDecision;
}

public struct InstinctRules
{
    public float EatRange;
    public float CarryFull;
    public float JuvenileTime;
    public float TetherRadius;
    public float PanicHpRelease;
    public float PanicDangerRelease;
    public float NurseLeash;
    public float HungerReinterrupt;

    public static InstinctRules Capture()
    {
        ref readonly var r = ref SimulationRules.Frame;
        return new InstinctRules
        {
            EatRange           = r.EatRange,
            CarryFull          = r.CarryFullFraction,
            JuvenileTime       = r.JuvenileTime,
            TetherRadius       = r.TetherRadius,
            PanicHpRelease     = r.PanicHpRelease,
            PanicDangerRelease = r.PanicDangerRelease,
            NurseLeash         = r.NurseLeash,
            HungerReinterrupt  = r.HungerReinterruptSeconds,
        };
    }
}

[BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
public struct InstinctJob : IJobFor
{
    public int Offset;
    public InstinctRules R;
    [ReadOnly] public NativeArray<InstinctInput> Inputs;
    [ReadOnly] public NativeArray<InstinctGenes> Genes;
    [NativeDisableParallelForRestriction] public NativeArray<InstinctState>  States;
    [NativeDisableParallelForRestriction] public NativeArray<InstinctOutput> Outputs;

    public void Execute(int index)
    {
        int i  = Offset + index;
        var x  = Inputs[i];
        var g  = Genes[i];
        var  state   = States[i];
        uint latches = state.Latches;

        bool wasHungry = (latches & InstinctBits.Hungry) != 0;
        bool wasPanic  = (latches & InstinctBits.Panic) != 0;

        bool hungry = wasHungry ? x.EnergyFraction <= g.HungerOff : x.EnergyFraction < g.HungerOn;

        float threat = math.max(x.LocalDanger, x.Stress);
        bool panic = wasPanic
            ? x.HpFraction < g.PanicHp + R.PanicHpRelease || threat > g.PanicDanger * R.PanicDangerRelease
            : x.HpFraction < g.PanicHp || threat > g.PanicDanger;

        latches = (hungry ? InstinctBits.Hungry : 0u) | (panic ? InstinctBits.Panic : 0u);

        bool sated      = x.Stomach >= g.SatedFill;
        bool edible     = x.FoodDistance >= 0f && x.FoodToxic == 0;
        bool foodNear   = edible && x.FoodDistance <= R.EatRange;
        bool foodFar    = edible && x.FoodDistance > R.EatRange;
        bool atNest     = x.AtNest != 0;
        bool nestFood   = atNest && x.NestFood != 0;
        bool hasThreat  = x.HasThreat != 0;
        bool juvenile   = x.Age < R.JuvenileTime && x.ParentDistance >= 0f;
        bool grazing    = x.PlanActive != 0 && (x.PlanKind == (byte)PlanKind.Graze || x.PlanKind == (byte)PlanKind.Harvest);

        uint mask   = 0u;
        byte reflex = InstinctBits.NoReflex;
        int  prio   = 0;

        if (panic && hasThreat)
            Pick(ref reflex, ref prio, ref mask, (byte)EntityAction.Flee, 6, InstinctBits.Panic);
        else if (x.Carrying >= R.CarryFull && !atNest)
            Pick(ref reflex, ref prio, ref mask, (byte)EntityAction.ReturnNest, 5, InstinctBits.Carry);
        else if (x.Carrying > 0f && atNest)
            Pick(ref reflex, ref prio, ref mask, (byte)EntityAction.StoreFood, 5, InstinctBits.Carry);
        else if (panic && !wasPanic && !atNest)
            Pick(ref reflex, ref prio, ref mask, (byte)EntityAction.ReturnNest, 4, InstinctBits.Panic);
        else if (x.Night != 0 && !atNest)
            Pick(ref reflex, ref prio, ref mask, (byte)EntityAction.ReturnNest, 4, InstinctBits.Night);
        else if (x.Caste == (byte)global::Caste.Nurse && !juvenile && x.NestDistance > R.NurseLeash)
            Pick(ref reflex, ref prio, ref mask, (byte)EntityAction.ReturnNest, 4, InstinctBits.Leash);
        else if (juvenile && x.ParentDistance > R.TetherRadius)
            Pick(ref reflex, ref prio, ref mask, (byte)EntityAction.Follow, 3, InstinctBits.Tether);
        else if (hungry && !sated && foodFar && !nestFood && !hasThreat && !grazing)
            Pick(ref reflex, ref prio, ref mask, (byte)EntityAction.ApproachFood, 2, InstinctBits.Seek);
        else if (hungry && !sated && (foodNear || nestFood))
            Pick(ref reflex, ref prio, ref mask, (byte)EntityAction.Eat, 1, InstinctBits.Eat);

        if (reflex != InstinctBits.NoReflex)
        {
            bool same      = x.Busy != 0 && x.RunningAction == reflex;
            bool dominated = x.Busy != 0 && x.RunningInstinct != 0 && Priority(x.RunningAction) >= prio;
            if (same || dominated)
            {
                reflex = InstinctBits.NoReflex;
                mask   = 0u;
            }
        }

        byte  interrupt  = 0;
        bool  front      = hungry && !wasHungry;
        float nextHunger = !hungry ? 0f : front ? x.Now + R.HungerReinterrupt : state.NextHungerInterrupt;
        if (reflex == InstinctBits.NoReflex && x.PlanActive != 0)
        {
            bool again = hungry && !front && x.Now >= nextHunger
                      && (x.PlanKind != (byte)PlanKind.Patrol || edible || nestFood);
            if (!grazing && (front || again))
            {
                interrupt  = 1;
                mask      |= InstinctBits.Hungry;
                nextHunger = x.Now + R.HungerReinterrupt;
            }
            if (panic && !wasPanic && x.PlanKind != (byte)PlanKind.Rest && x.PlanKind != (byte)PlanKind.Flee)
            {
                interrupt = 1;
                mask |= InstinctBits.Panic;
            }
        }

        States[i] = new InstinctState { Latches = latches, NextHungerInterrupt = nextHunger };

        bool idle = x.Busy == 0 && x.PlanActive == 0;
        Outputs[i] = new InstinctOutput
        {
            Mask          = mask,
            Latches       = latches,
            Reflex        = reflex,
            Interrupt     = interrupt,
            NeedsDecision = (byte)(reflex == InstinctBits.NoReflex && (interrupt != 0 || idle) ? 1 : 0),
        };
    }

    private static void Pick(ref byte reflex, ref int prio, ref uint mask, byte action, int priority, uint bit)
    {
        reflex = action;
        prio   = priority;
        mask  |= bit;
    }

    private static int Priority(byte action) => action switch
    {
        (byte)EntityAction.Flee         => 6,
        (byte)EntityAction.StoreFood    => 5,
        (byte)EntityAction.ReturnNest   => 4,
        (byte)EntityAction.Follow       => 3,
        (byte)EntityAction.ApproachFood => 2,
        (byte)EntityAction.Eat          => 1,
        _                             => 0,
    };
}

public sealed class InstinctSystem : IDisposable
{
    private NativeArray<InstinctInput>  _inputs;
    private NativeArray<InstinctGenes>  _genes;
    private NativeArray<InstinctState>  _states;
    private NativeArray<InstinctOutput> _outputs;

    public int Capacity => _inputs.IsCreated ? _inputs.Length : 0;

    public InstinctSystem(int capacity)
    {
        capacity = Math.Max(capacity, 1);
        _inputs  = new NativeArray<InstinctInput>(capacity, Allocator.Persistent);
        _genes   = new NativeArray<InstinctGenes>(capacity, Allocator.Persistent);
        _states  = new NativeArray<InstinctState>(capacity, Allocator.Persistent);
        _outputs = new NativeArray<InstinctOutput>(capacity, Allocator.Persistent);
    }

    public void Reset(int index, in InstinctGenes genes)
    {
        if ((uint)index >= (uint)Capacity) return;
        _inputs[index]  = default;
        _genes[index]   = genes;
        _states[index]  = default;
        _outputs[index] = new InstinctOutput { Reflex = InstinctBits.NoReflex, NeedsDecision = 1 };
    }

    public void SetGenes(int index, in InstinctGenes genes)
    {
        if ((uint)index < (uint)Capacity) _genes[index] = genes;
    }

    public void Write(int index, in InstinctInput input)
    {
        if ((uint)index < (uint)Capacity) _inputs[index] = input;
    }

    public InstinctOutput Output(int index)
        => (uint)index < (uint)Capacity
            ? _outputs[index]
            : new InstinctOutput { Reflex = InstinctBits.NoReflex, NeedsDecision = 1 };

    public void Evaluate(int start, int end)
    {
        if (start < 0) start = 0;
        if (end > Capacity) end = Capacity;
        if (end <= start) return;

        new InstinctJob
        {
            Offset  = start,
            R       = InstinctRules.Capture(),
            Inputs  = _inputs,
            Genes   = _genes,
            States  = _states,
            Outputs = _outputs,
        }.Run(end - start);
    }

    public void RemoveSwapBack(int index, int last)
    {
        if ((uint)index >= (uint)Capacity || (uint)last >= (uint)Capacity) return;
        if (index != last)
        {
            _inputs[index]  = _inputs[last];
            _genes[index]   = _genes[last];
            _states[index]  = _states[last];
            _outputs[index] = _outputs[last];
        }
        _inputs[last]  = default;
        _genes[last]   = default;
        _states[last]  = default;
        _outputs[last] = new InstinctOutput { Reflex = InstinctBits.NoReflex, NeedsDecision = 1 };
    }

    public void Dispose()
    {
        if (_inputs.IsCreated)  _inputs.Dispose();
        if (_genes.IsCreated)   _genes.Dispose();
        if (_states.IsCreated)  _states.Dispose();
        if (_outputs.IsCreated) _outputs.Dispose();
    }
}

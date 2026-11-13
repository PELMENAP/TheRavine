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
    public const uint Sick   = 1u << 8;
    public const uint Troph  = 1u << 9;
    public const uint Recruit = 1u << 10;
    public const uint Nursery = 1u << 11;

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
    public float HungerBias;
    public float PanicBias;
    public float FoodToxicity;
    public float Dt;
    public byte  Colonial;
    public byte  Sick;
    public byte  HungryKin;
    public byte  Recruited;
    public byte  Caste;
    public byte  AtNest;
    public byte  NestFood;
    public byte  Night;
    public byte  Warned;
    public byte  HasThreat;
    public byte  Busy;
    public byte  RunningAction;
    public byte  RunningDirection;
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
    public float Tether;
    public float Nursery;
    public float Home;
    public float PanicHome;
    public float CarryFull;
    public float Reinterrupt;

    public static InstinctGenes From(in GeneticParameters g) => new()
    {
        HungerOn    = g.HungerOn,
        HungerOff   = g.HungerOff,
        SatedFill   = g.SatedFill,
        PanicHp     = g.PanicHp,
        PanicDanger = g.PanicDanger,
        Tether      = g.TetherDrive,
        Nursery     = g.NurseryDrive,
        Home        = g.HomeDrive,
        PanicHome   = g.PanicHome,
        CarryFull   = g.CarryFullMul,
        Reinterrupt = g.ReinterruptMul,
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
    public byte Direction;
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
    public float TrophallaxisMinFill;
    public float NurseLeashRate;
    public float NurseryRadius;
    public float NurseryRate;
    public float TetherRate;
    public float NightHomeRate;

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
            TrophallaxisMinFill = r.TrophallaxisMinFill,
            NurseLeashRate     = r.NurseLeashRate,
            NurseryRadius      = r.NurseryRadius,
            NurseryRate        = r.NurseryRate,
            TetherRate         = r.TetherRate,
            NightHomeRate      = r.NightHomeRate,
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

        float hungerOn  = g.HungerOn + x.HungerBias;
        float hungerOff = math.max(g.HungerOff + x.HungerBias, hungerOn);
        bool hungry = wasHungry ? x.EnergyFraction <= hungerOff : x.EnergyFraction < hungerOn;

        float panicDanger = g.PanicDanger * math.max(0.1f, 1f + x.PanicBias);
        float threat = math.max(x.LocalDanger, x.Stress);
        bool panic = wasPanic
            ? x.HpFraction < g.PanicHp + R.PanicHpRelease || threat > panicDanger * R.PanicDangerRelease
            : x.HpFraction < g.PanicHp || threat > panicDanger;

        latches = (hungry ? InstinctBits.Hungry : 0u) | (panic ? InstinctBits.Panic : 0u);

        bool sated      = x.Stomach >= g.SatedFill;
        bool edible     = x.FoodDistance >= 0f && x.FoodToxicity <= 0f;
        bool foodNear   = edible && x.FoodDistance <= R.EatRange;
        bool foodFar    = edible && x.FoodDistance > R.EatRange;
        bool atNest     = x.AtNest != 0;
        bool nestFood   = atNest && x.NestFood != 0;
        bool hasThreat  = x.HasThreat != 0;
        bool young      = x.Age < R.JuvenileTime;
        bool juvenile   = young && x.ParentDistance >= 0f;
        bool grazing    = x.PlanActive != 0 && (x.PlanKind == (byte)PlanKind.Graze || x.PlanKind == (byte)PlanKind.Harvest
                                                || x.PlanKind == (byte)PlanKind.Seek);
        bool sick       = x.Sick != 0;
        bool forage     = hungry && edible && !hasThreat;
        var  rng        = new Unity.Mathematics.Random(math.hash(new uint2((uint)i, math.asuint(x.Now))) | 1u);

        uint mask   = 0u;
        byte reflex = InstinctBits.NoReflex;
        byte dir    = NoDirection;
        int  prio   = 0;

        if (panic && hasThreat)
            Pick(ref reflex, ref prio, ref mask, ref dir, Move, Escape, 7, InstinctBits.Panic);
        else if (x.Carrying >= math.min(R.CarryFull * g.CarryFull, 1f) && !atNest)
            Pick(ref reflex, ref prio, ref mask, ref dir, Move, Nest, 6, InstinctBits.Carry);
        else if (x.Carrying > 0f && atNest)
            Pick(ref reflex, ref prio, ref mask, ref dir, Put, NoDirection, 6, InstinctBits.Carry);
        else if (sick && atNest)
            Pick(ref reflex, ref prio, ref mask, ref dir, Move, NoDirection, 5, InstinctBits.Sick);
        else if (panic && !wasPanic && !atNest && !sick && x.NestDistance >= 0f && rng.NextFloat() < g.PanicHome)
            Pick(ref reflex, ref prio, ref mask, ref dir, Move, Nest, 5, InstinctBits.Panic);
        else if (x.Night != 0 && !atNest && !sick && !forage && x.NestDistance >= 0f
                 && Urge(ref rng, R.NightHomeRate * g.Home, x.Dt))
            Pick(ref reflex, ref prio, ref mask, ref dir, Move, Nest, 5, InstinctBits.Night);
        else if (x.Caste == (byte)global::Caste.Nurse && !juvenile && !sick && x.NestDistance > R.NurseLeash
                 && Urge(ref rng, R.NurseLeashRate * g.Home, x.Dt))
            Pick(ref reflex, ref prio, ref mask, ref dir, Move, Nest, 5, InstinctBits.Leash);
        else if (young && x.Colonial != 0 && !forage && !sick && x.NestDistance > R.NurseryRadius
                 && Urge(ref rng, R.NurseryRate * g.Nursery, x.Dt))
            Pick(ref reflex, ref prio, ref mask, ref dir, Move, Nest, 4, InstinctBits.Nursery);
        else if (juvenile && !forage && x.ParentDistance > R.TetherRadius
                 && Urge(ref rng, R.TetherRate * g.Tether, x.Dt))
            Pick(ref reflex, ref prio, ref mask, ref dir, Move, Kin, 4, InstinctBits.Tether);
        else if (atNest && !hungry && !sick && x.HungryKin != 0 && x.Stomach >= R.TrophallaxisMinFill
                 && x.PlanActive == 0 && x.Busy == 0)
            Pick(ref reflex, ref prio, ref mask, ref dir, Give, NoDirection, 3, InstinctBits.Troph);
        else if (x.Recruited != 0 && !hungry && !hasThreat && x.PlanActive == 0)
            Pick(ref reflex, ref prio, ref mask, ref dir, Move, OwnPoi, 3, InstinctBits.Recruit);
        else if (hungry && !sated && foodFar && !nestFood && !hasThreat && !grazing)
            Pick(ref reflex, ref prio, ref mask, ref dir, Move, Food, 2, InstinctBits.Seek);
        else if (hungry && !sated && (foodNear || nestFood))
            Pick(ref reflex, ref prio, ref mask, ref dir, Eat, NoDirection, 1, InstinctBits.Eat);

        if (reflex != InstinctBits.NoReflex)
        {
            bool same      = x.Busy != 0 && x.RunningAction == reflex && (reflex != Move || x.RunningDirection == dir);
            bool dominated = x.Busy != 0 && x.RunningInstinct != 0 && Priority(x.RunningAction, x.RunningDirection) >= prio;
            if (same || dominated)
            {
                reflex = InstinctBits.NoReflex;
                dir    = NoDirection;
                mask   = 0u;
            }
        }

        byte  interrupt  = 0;
        bool  front      = hungry && !wasHungry;
        float reinterrupt = R.HungerReinterrupt * g.Reinterrupt;
        float nextHunger = !hungry ? 0f : front ? x.Now + reinterrupt : state.NextHungerInterrupt;
        if (reflex == InstinctBits.NoReflex && x.PlanActive != 0)
        {
            bool again = hungry && !front && x.Now >= nextHunger
                      && (x.PlanKind != (byte)PlanKind.Patrol || edible || nestFood);
            if (!grazing && (front || again))
            {
                interrupt  = 1;
                mask      |= InstinctBits.Hungry;
                nextHunger = x.Now + reinterrupt;
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
            Direction     = dir,
            Interrupt     = interrupt,
            NeedsDecision = (byte)(reflex == InstinctBits.NoReflex && (interrupt != 0 || idle) ? 1 : 0),
        };
    }

    private static bool Urge(ref Unity.Mathematics.Random rng, float rate, float dt)
        => rate > 0f && rng.NextFloat() < 1f - math.exp(-rate * math.max(dt, 0f));

    private const byte Move        = (byte)EntityAction.MoveTo;
    private const byte Put         = (byte)EntityAction.Put;
    private const byte Give        = (byte)EntityAction.Give;
    private const byte Eat         = (byte)EntityAction.Eat;
    private const byte NoDirection = (byte)DirectionKind.None;
    private const byte Escape      = (byte)DirectionKind.Escape;
    private const byte Nest        = (byte)DirectionKind.Nest;
    private const byte Kin         = (byte)DirectionKind.Kin;
    private const byte OwnPoi      = (byte)DirectionKind.OwnPoi;
    private const byte Food        = (byte)DirectionKind.Food;

    private static void Pick(ref byte reflex, ref int prio, ref uint mask, ref byte direction,
        byte action, byte actionDirection, int priority, uint bit)
    {
        reflex    = action;
        direction = actionDirection;
        prio      = priority;
        mask     |= bit;
    }

    private static int Priority(byte action, byte direction)
    {
        if (action == Put) return 6;
        if (action == Give) return 3;
        if (action == Eat) return 1;
        if (action != Move) return 0;
        switch (direction)
        {
            case Escape:      return 7;
            case Nest:        return 5;
            case NoDirection: return 5;
            case Kin:         return 4;
            case OwnPoi:
            case (byte)DirectionKind.ColonyPoi: return 3;
            case Food:        return 2;
            default:          return 0;
        }
    }
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

    public JobHandle Schedule(int start, int end)
    {
        if (start < 0) start = 0;
        if (end > Capacity) end = Capacity;
        if (end <= start) return default;

        return new InstinctJob
        {
            Offset  = start,
            R       = InstinctRules.Capture(),
            Inputs  = _inputs,
            Genes   = _genes,
            States  = _states,
            Outputs = _outputs,
        }.ScheduleParallel(end - start, 64, default);
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

using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public enum MoveIntent : byte { Wander = 0, ApproachFood = 1, GoToPOI = 2, ReturnNest = 3, Flee = 4 }

public struct PlanRequest
{
    public float2 Origin;
    public float2 Direction;
    public float2 Target;
    public float2 Threat;
    public float2 Boid;
    public float  Radius;
    public float  Curvature;
    public float  Side;
    public uint   Seed;
    public byte   Intent;
    public byte   HasTarget;
    public byte   HasThreat;
    public byte   ColonyIndex;
}

public struct PlanResult
{
    public float2 End;
    public float2 Control;
}

public struct PlanWeights
{
    public int   Candidates;
    public float SectorHalf;
    public float MinRadiusFraction;
    public float MoveCost;
    public float Food;
    public float Danger;
    public float Target;
    public float Align;
    public float CurvatureMin;
    public float CurvatureScale;
    public float Trail;
    public float Explored;
    public float KinDeath;
    public float Boid;
}

[BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
public struct PlanJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<PlanRequest> Requests;
    [WriteOnly] public NativeArray<PlanResult> Results;
    public HeightAtlas Atlas;
    [ReadOnly] public NativeArray<ColonyFieldView> Colonies;
    public PlanWeights W;

    public void Execute(int index)
    {
        var q    = Requests[index];
        var nest = Colonies[q.ColonyIndex];

        float2 dir0   = math.normalizesafe(q.Direction, new float2(0f, 1f));
        float  radius = math.max(q.Radius, 0.5f);
        bool   target = q.HasTarget != 0;
        bool   flee   = q.Intent == (byte)MoveIntent.Flee;
        bool   wander = q.Intent == (byte)MoveIntent.Wander;

        if (target)
        {
            float2 to = q.Target - q.Origin;
            float  d  = math.length(to);
            if (d > 1e-3f) dir0 = to / d;
            radius = math.clamp(d, 0.5f, radius);
        }
        if (flee && q.HasThreat != 0)
            dir0 = math.normalizesafe(q.Origin - q.Threat, dir0);

        int   k     = math.max(1, W.Candidates);
        float best  = float.MinValue;
        float2 bestEnd = q.Origin + dir0 * radius;
        float2 bestDir = dir0;
        float  invR    = 1f / radius;

        for (int c = 0; c < k; c++)
        {
            uint  h  = math.hash(new uint2(q.Seed, (uint)c));
            float u  = (h & 0xFFFF) * (1f / 65535f);
            float t  = k == 1 ? 0f : c / (k - 1f) * 2f - 1f;
            float a  = t * W.SectorHalf + (u - 0.5f) * W.SectorHalf * 0.2f;

            math.sincos(a, out float sa, out float ca);
            float2 d   = new float2(dir0.x * ca - dir0.y * sa, dir0.x * sa + dir0.y * ca);
            float  len = target ? radius : radius * math.lerp(W.MinRadiusFraction, 1f, ((h >> 16) & 0xFFFF) * (1f / 65535f));
            float2 end = q.Origin + d * len;

            float sm    = Atlas.SpeedModifier(end.x, end.y, d);
            float cost  = len / math.max(sm, 0.05f) * invR;
            float food  = 0f;
            float dang  = 0f;
            float field = 0f;
            if (nest.TryIndex(end, out int cell))
            {
                food = nest.At(ColonyChannel.Food, cell);
                dang = nest.At(ColonyChannel.Danger, cell);
                if (!flee)
                    field = W.Trail * nest.At(ColonyChannel.Trail, cell)
                          - W.KinDeath * nest.At(ColonyChannel.KinDeath, cell)
                          - (wander ? W.Explored * nest.At(ColonyChannel.Explored, cell) : 0f);
            }
            float goal  = 0f;

            if (target) goal = -math.distance(end, q.Target) * invR;
            if (flee && q.HasThreat != 0) goal = math.distance(end, q.Threat) * invR;

            float score = -W.MoveCost * cost + W.Food * food - W.Danger * dang
                        + W.Target * goal + W.Align * math.dot(d, dir0)
                        + field + W.Boid * math.dot(d, q.Boid);

            if (score <= best) continue;
            best    = score;
            bestEnd = end;
            bestDir = d;
        }

        float2 chord = bestEnd - q.Origin;
        float  span  = math.length(chord);
        float2 perp  = new float2(-bestDir.y, bestDir.x);
        float  bend  = math.lerp(W.CurvatureMin, 1f, math.saturate(math.abs(q.Curvature))) * W.CurvatureScale;

        Results[index] = new PlanResult
        {
            End     = bestEnd,
            Control = q.Origin + chord * 0.5f + perp * (q.Side * bend * span),
        };
    }
}

public sealed class MovePlanner : IDisposable
{
    private NativeList<PlanRequest> _requests;
    private NativeArray<PlanResult> _results;
    private EntityModel[] _owners   = new EntityModel[32];
    private float[]       _speed    = new float[32];
    private float[]       _cost     = new float[32];
    private double[]      _deadline = new double[32];
    private float2[]      _points   = new float2[32];

    private readonly MotionSystem _motion;
    private ColonyRegistry _colonies;

    public int Pending => _requests.IsCreated ? _requests.Length : 0;

    public MovePlanner(MotionSystem motion)
    {
        _motion   = motion;
        _requests = new NativeList<PlanRequest>(32, Allocator.Persistent);
    }

    public void BindColonies(ColonyRegistry colonies) => _colonies = colonies;

    public void Enqueue(EntityModel owner, in PlanRequest request, float speed, float energyCost, double deadline)
    {
        int slot = owner.PlanSlot;
        if ((uint)slot >= (uint)_requests.Length || !ReferenceEquals(_owners[slot], owner))
        {
            slot = _requests.Length;
            _requests.Add(request);
            EnsureCapacity(slot + 1);
        }
        else _requests[slot] = request;

        _owners[slot]   = owner;
        _speed[slot]    = speed;
        _cost[slot]     = energyCost;
        _deadline[slot] = deadline;
        owner.PlanSlot  = slot;
    }

    public void Cancel(EntityModel owner)
    {
        int slot = owner.PlanSlot;
        owner.PlanSlot = -1;
        if ((uint)slot < (uint)_requests.Length && ReferenceEquals(_owners[slot], owner))
            _owners[slot] = null;
    }

    public void Flush()
    {
        int n = Pending;
        if (n == 0) return;
        if (_colonies == null || _colonies.Count == 0)
        {
            for (int i = 0; i < n; i++)
            {
                if (_owners[i] != null) _owners[i].PlanSlot = -1;
                _owners[i] = null;
            }
            _requests.Clear();
            return;
        }

        if (!_results.IsCreated || _results.Length < n)
        {
            if (_results.IsCreated) _results.Dispose();
            _results = new NativeArray<PlanResult>(math.ceilpow2(math.max(n, 32)), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        for (int i = 0; i < n; i++) _points[i] = _requests[i].Origin;

        ref readonly var f = ref SimulationRules.Frame;
        var atlas = _motion.PrepareAtlas(_points, n, f.PlannerRadiusMax);

        new PlanJob
        {
            Requests = _requests.AsArray(),
            Results  = _results,
            Atlas    = atlas,
            Colonies = _colonies.Views,
            W = new PlanWeights
            {
                Candidates        = f.PlannerCandidates,
                SectorHalf        = f.PlannerSectorHalf,
                MinRadiusFraction = f.PlannerMinRadiusFraction,
                MoveCost          = f.PlannerMoveCostWeight,
                Food              = f.PlannerFoodWeight,
                Danger            = f.PlannerDangerWeight,
                Target            = f.PlannerTargetWeight,
                Align             = f.PlannerAlignWeight,
                CurvatureMin      = f.PlannerCurvatureMin,
                CurvatureScale    = f.PlannerCurvatureScale,
                Trail             = f.PlannerTrailWeight,
                Explored          = f.PlannerExploredWeight,
                KinDeath          = f.PlannerKinDeathWeight,
                Boid              = f.PlannerBoidWeight,
            },
        }.Schedule(n, 8).Complete();

        for (int i = 0; i < n; i++)
        {
            var owner = _owners[i];
            _owners[i] = null;
            if (owner == null || owner.IsDisposed || owner.PlanSlot != i) continue;
            owner.PlanSlot = -1;

            var r = _results[i];
            float y = owner.Motor.Position().y;
            owner.Motor.BeginMove(new Vector3(r.End.x, y, r.End.y), new Vector3(r.Control.x, y, r.Control.y),
                _speed[i], _cost[i], _deadline[i]);
        }

        _requests.Clear();
    }

    private void EnsureCapacity(int n)
    {
        if (n <= _owners.Length) return;
        int cap = math.ceilpow2(n);
        Array.Resize(ref _owners, cap);
        Array.Resize(ref _speed, cap);
        Array.Resize(ref _cost, cap);
        Array.Resize(ref _deadline, cap);
        Array.Resize(ref _points, cap);
    }

    public void Dispose()
    {
        if (_requests.IsCreated) _requests.Dispose();
        if (_results.IsCreated)  _results.Dispose();
    }
}

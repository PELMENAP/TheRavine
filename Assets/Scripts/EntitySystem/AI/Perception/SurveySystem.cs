using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

public struct SurveyInput
{
    public float3 Position;
    public float  NearestRadius;
    public float  CrowdRadius;
    public float  KinRadius;
    public float  AlarmRadius;
    public int    ColonyIndex;
    public int    CrowdCap;
    public int    SelfSlot;
    public int    VictimSlot;
    public int    FamiliarSlot;
    public float  StickMul;
    public float  LowHpMul;
    public float  FamiliarMul;
    public byte   Active;
}

public struct SurveyOutput
{
    public int   NearestSlot;
    public int   ForeignSlot;
    public int   Crowd;
    public int   Kin;
    public int   KinNear;
    public int   Near;
    public int   AlarmNear;
    public float NearestDistance;
    public float ForeignDistance;

    public static SurveyOutput None => new SurveyOutput
    {
        NearestSlot     = -1,
        ForeignSlot     = -1,
        NearestDistance = -1f,
        ForeignDistance = -1f,
    };
}

[BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
public struct SurveyJob : IJobFor
{
    public int Offset;
    public int BucketMask;
    public float InvCell;
    [ReadOnly] public NativeArray<int>    CellEnd;
    [ReadOnly] public NativeArray<int>    Items;
    [ReadOnly] public NativeArray<int2>   Cell;
    [ReadOnly] public NativeArray<int>    Colony;
    [ReadOnly] public NativeArray<byte>   Alive;
    [ReadOnly] public NativeArray<byte>   Alarm;
    [ReadOnly] public NativeArray<float>  Hp;
    [ReadOnly] public NativeArray<float>  Px;
    [ReadOnly] public NativeArray<float>  Py;
    [ReadOnly] public NativeArray<float>  Pz;
    [NativeDisableParallelForRestriction] public NativeArray<SurveyInput>  Inputs;
    [NativeDisableParallelForRestriction] public NativeArray<SurveyOutput> Outputs;

    public void Execute(int index)
    {
        int i = Offset + index;
        var q = Inputs[i];
        if (q.Active == 0) return;
        q.Active = 0;
        Inputs[i] = q;

        float nr2  = q.NearestRadius * q.NearestRadius;
        float cr2  = q.CrowdRadius * q.CrowdRadius;
        float kr2  = q.KinRadius * q.KinRadius;
        float ar2  = q.AlarmRadius * q.AlarmRadius;
        int   ring = math.max(1, (int)math.ceil(math.max(math.max(math.max(q.NearestRadius, q.CrowdRadius), q.KinRadius), q.AlarmRadius) * InvCell));
        int   cx   = (int)math.floor(q.Position.x * InvCell);
        int   cz   = (int)math.floor(q.Position.z * InvCell);

        var result = SurveyOutput.None;
        float minSqr = float.MaxValue;
        float minForeignSqr = float.MaxValue;
        float minForeignScore = float.MaxValue;

        for (int dz = -ring; dz <= ring; dz++)
        for (int dx = -ring; dx <= ring; dx++)
        {
            int qx = cx + dx;
            int qz = cz + dz;
            int b  = EntitySpatialGrid.HashCell(qx, qz, BucketMask);
            int s  = b == 0 ? 0 : CellEnd[b - 1];
            int e  = CellEnd[b];

            for (int k = s; k < e; k++)
            {
                int n = Items[k];
                int2 c = Cell[n];
                if (c.x != qx || c.y != qz) continue;

                float ddx = Px[n] - q.Position.x;
                float ddy = Py[n] - q.Position.y;
                float ddz = Pz[n] - q.Position.z;
                float sqr = ddx * ddx + ddy * ddy + ddz * ddz;

                bool asNearest = sqr <= nr2 && sqr < minSqr;
                bool asCrowd   = sqr <= cr2 && result.Crowd < q.CrowdCap;
                bool asForeign = sqr <= nr2 && Colony[n] != q.ColonyIndex;
                bool asKin     = q.KinRadius > 0f && sqr <= kr2 && Colony[n] == q.ColonyIndex;
                bool asAlarm   = q.AlarmRadius > 0f && sqr <= ar2;
                if (!asNearest && !asCrowd && !asForeign && !asKin && !asAlarm) continue;
                if (n == q.SelfSlot || Alive[n] == 0) continue;

                if (asKin) result.KinNear++;
                if (asAlarm)
                {
                    result.Near++;
                    if (Alarm[n] != 0) result.AlarmNear++;
                }

                if (asNearest)
                {
                    minSqr = sqr;
                    result.NearestSlot = n;
                }
                if (asCrowd)
                {
                    result.Crowd++;
                    if (Colony[n] == q.ColonyIndex) result.Kin++;
                }
                if (asForeign)
                {
                    float score = math.sqrt(sqr) * (n == q.VictimSlot ? q.StickMul : 1f) * math.lerp(q.LowHpMul, 1f, Hp[n])
                                * (n == q.FamiliarSlot ? q.FamiliarMul : 1f);
                    if (score < minForeignScore)
                    {
                        minForeignScore = score;
                        minForeignSqr   = sqr;
                        result.ForeignSlot = n;
                    }
                }
            }
        }

        if (result.NearestSlot >= 0) result.NearestDistance = math.sqrt(minSqr);
        if (result.ForeignSlot >= 0) result.ForeignDistance = math.sqrt(minForeignSqr);
        Outputs[i] = result;
    }
}

public sealed class SurveySystem : IDisposable
{
    private static readonly ProfilerMarker ScheduleMarker = new("SurveySystem.Schedule");

    private NativeArray<SurveyInput>  _inputs;
    private NativeArray<SurveyOutput> _outputs;

    public int Capacity => _inputs.IsCreated ? _inputs.Length : 0;

    public SurveySystem(int capacity)
    {
        capacity = Math.Max(capacity, 1);
        _inputs  = new NativeArray<SurveyInput>(capacity, Allocator.Persistent);
        _outputs = new NativeArray<SurveyOutput>(capacity, Allocator.Persistent);
        for (int i = 0; i < capacity; i++) _outputs[i] = SurveyOutput.None;
    }

    public void Write(int index, in SurveyInput input)
    {
        if ((uint)index >= (uint)Capacity) return;
        var q = input;
        q.Active = 1;
        _inputs[index] = q;
    }

    public SurveyOutput Output(int index)
        => (uint)index < (uint)Capacity ? _outputs[index] : SurveyOutput.None;

    public JobHandle Schedule(EntitySpatialGrid grid, int start, int end)
    {
        using var scope = ScheduleMarker.Auto();
        if (start < 0) start = 0;
        if (end > Capacity) end = Capacity;
        if (end <= start) return default;

        if (grid == null || grid.Count == 0 || !grid.Items.IsCreated)
        {
            for (int i = start; i < end; i++)
            {
                if (_inputs[i].Active == 0) continue;
                var q = _inputs[i];
                q.Active = 0;
                _inputs[i] = q;
                _outputs[i] = SurveyOutput.None;
            }
            return default;
        }

        return new SurveyJob
        {
            Offset     = start,
            BucketMask = grid.BucketMask,
            InvCell    = grid.InvCell,
            CellEnd    = grid.CellEnd,
            Items      = grid.Items,
            Cell       = grid.Cell,
            Colony     = grid.Colony,
            Alive      = grid.Alive,
            Alarm      = grid.Alarm,
            Hp         = grid.HpFraction,
            Px         = grid.Px,
            Py         = grid.Py,
            Pz         = grid.Pz,
            Inputs     = _inputs,
            Outputs    = _outputs,
        }.ScheduleParallel(end - start, 16, default);
    }

    public void RemoveSwapBack(int index, int last)
    {
        if ((uint)index >= (uint)Capacity || (uint)last >= (uint)Capacity) return;
        if (index != last)
        {
            _inputs[index]  = _inputs[last];
            _outputs[index] = _outputs[last];
        }
        _inputs[last]  = default;
        _outputs[last] = SurveyOutput.None;
    }

    public void Dispose()
    {
        if (_inputs.IsCreated)  _inputs.Dispose();
        if (_outputs.IsCreated) _outputs.Dispose();
    }
}

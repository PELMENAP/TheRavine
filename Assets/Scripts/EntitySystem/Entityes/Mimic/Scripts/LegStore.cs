using System;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

public enum LegState : byte { Disabled, Growing, Supporting, Retracting }

public struct LegParams
{
    public LegState State;
    public float3 Foot;
    public float Progression;
    public float GrowTarget;
    public float LifeTimer;
    public float GrowCoef;
    public float LegHeight;
    public float RotationSpeed;
    public float OscillationSpeed;
    public float OscillationProgress;
}

public sealed unsafe class LegStore : IDisposable
{
    public const int HandleCount = 7;

    private NativeArray<LegParams> _legs;
    private NativeArray<float3> _handles;
    private NativeArray<float3> _offsets;
    private NativeArray<float3> _vertices;
    private NativeArray<float3> _normals;

    public int Capacity { get; private set; }
    public NativeArray<Vector3> VertexView { get; private set; }
    public NativeArray<Vector3> NormalView { get; private set; }

    public LegParams* LegsPtr => (LegParams*)_legs.GetUnsafePtr();
    public float3* HandlesPtr => (float3*)_handles.GetUnsafePtr();
    public float3* OffsetsPtr => (float3*)_offsets.GetUnsafePtr();
    public float3* VerticesPtr => (float3*)_vertices.GetUnsafePtr();
    public float3* NormalsPtr => (float3*)_normals.GetUnsafePtr();

    public LegStore(int capacity, int maxVertices)
    {
        Capacity = math.max(capacity, 1);
        maxVertices = math.max(maxVertices, 1);
        _legs = new NativeArray<LegParams>(Capacity, Allocator.Persistent);
        _handles = new NativeArray<float3>(Capacity * HandleCount, Allocator.Persistent);
        _offsets = new NativeArray<float3>(Capacity * HandleCount, Allocator.Persistent);
        _vertices = new NativeArray<float3>(maxVertices, Allocator.Persistent);
        _normals = new NativeArray<float3>(maxVertices, Allocator.Persistent);
        VertexView = _vertices.Reinterpret<Vector3>();
        NormalView = _normals.Reinterpret<Vector3>();
    }

    public MimicWork MakeWork(float3 position, float dt, float halfWidth, int samples, int ring, int resolution) => new MimicWork
    {
        Legs       = LegsPtr,
        Handles    = HandlesPtr,
        Offsets    = OffsetsPtr,
        Vertices   = VerticesPtr,
        Normals    = NormalsPtr,
        Position   = position,
        Dt         = dt,
        HalfWidth  = halfWidth,
        LegCount   = Capacity,
        Samples    = samples,
        Ring       = ring,
        Resolution = resolution,
    };

    public LegParams this[int index]
    {
        get => _legs[index];
        set => _legs[index] = value;
    }

    public int FindFree()
    {
        for (int i = 0; i < Capacity; i++)
        {
            if (_legs[i].State == LegState.Disabled) return i;
        }
        return -1;
    }

    public void Activate(int index, Mimic mimic, float3 footPos, float coef, float lifeTime)
    {
        _legs[index] = new LegParams
        {
            State = LegState.Growing,
            Foot = footPos,
            Progression = 0f,
            GrowTarget = 1f,
            GrowCoef = coef,
            LifeTimer = lifeTime,
            LegHeight = RavineRandom.RangeFloat(mimic.legMinHeight, mimic.legMaxHeight),
            RotationSpeed = RavineRandom.RangeFloat(mimic.minRotSpeed, mimic.maxRotSpeed),
            OscillationSpeed = RavineRandom.RangeFloat(mimic.minOscillationSpeed, mimic.maxOscillationSpeed),
            OscillationProgress = 0f,
        };

        int baseIndex = index * HandleCount;
        for (int i = 0; i < HandleCount; i++)
        {
            _offsets[baseIndex + i] = RavineRandom.UnitSphere() * RavineRandom.RangeFloat(mimic.handleOffsetMinRadius, mimic.handleOffsetMaxRadius);
        }
    }

    public void Dispose()
    {
        if (_legs.IsCreated) _legs.Dispose();
        if (_handles.IsCreated) _handles.Dispose();
        if (_offsets.IsCreated) _offsets.Dispose();
        if (_vertices.IsCreated) _vertices.Dispose();
        if (_normals.IsCreated) _normals.Dispose();
        VertexView = default;
        NormalView = default;
    }
}

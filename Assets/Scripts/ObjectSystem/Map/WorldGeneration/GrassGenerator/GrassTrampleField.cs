using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using TheRavine.Extensions;
using TheRavine.Generator;

[Serializable]
public sealed class GrassTrampleSettings
{
    [Min(0.5f)] public float texelMeters = 1f;
    [Min(0.1f)] public float halfLife = 12f;
    [Min(1)] public int stampCapacity = 1024;
    [Min(0.1f)] public float bodyRadius = 0.9f;
    [Min(0.1f)] public float referenceBodyEnergy = 100f;
    [Min(0.1f)] public float referenceSpeed = 3f;
    [Range(0f, 1f)] public float restFraction = 0.25f;
    [Min(0f)] public float pressPerSecond = 2f;
}

public sealed class GrassTrampleField : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct StampData
    {
        public float2 position;
        public float radius;
        public float strength;
    }

    private const int MaxRadiusTexels = 7;
    private const float MinRadiusTexels = 1.5f;
    private const float Quantum = 1f / 65535f;

    private static readonly int TextureId = Shader.PropertyToID("_GrassTrample");
    private static readonly int WindowId  = Shader.PropertyToID("_GrassTrampleWindow");
    private static readonly int EnabledId = Shader.PropertyToID("_GrassTrampleEnabled");

    private static readonly int SourceId = Shader.PropertyToID("Source");
    private static readonly int TargetId = Shader.PropertyToID("Target");
    private static readonly int StampsId = Shader.PropertyToID("Stamps");
    private static readonly int SizeId   = Shader.PropertyToID("Size");
    private static readonly int ShiftXId = Shader.PropertyToID("ShiftX");
    private static readonly int ShiftZId = Shader.PropertyToID("ShiftZ");
    private static readonly int KeepId   = Shader.PropertyToID("Keep");
    private static readonly int FloorId  = Shader.PropertyToID("Floor");

    private readonly GrassTrampleSettings _settings;
    private readonly ComputeShader _compute;
    private readonly int _size;
    private readonly float _texel;
    private readonly float _invTexel;
    private readonly float _invWorld;
    private readonly int _decayKernel;
    private readonly int _stampKernel;
    private readonly RenderTexture[] _textures = new RenderTexture[2];
    private NativeArray<StampData> _stamps;
    private GraphicsBuffer _stampBuffer;

    private int _current;
    private int _stampCount;
    private bool _hasOrigin;
    private bool _clearPending = true;
    private float2 _origin;
    private int2 _pendingShift;
    private bool _disposed;

    public bool IsDisposed => _disposed;
    public int Size => _size;
    public int StampCount => _stampCount;
    public float2 Origin => _origin;

    public GrassTrampleField(ComputeShader compute, GrassTrampleSettings settings)
    {
        _settings = settings ?? new GrassTrampleSettings();
        _compute  = compute;

        int chunkTexels = math.max(8, (int)math.round(MapGenerator.chunkSize / math.max(_settings.texelMeters, 0.5f)));
        _size     = chunkTexels * MapGenerator.WindowSide;
        _texel    = (float)MapGenerator.chunkSize / chunkTexels;
        _invTexel = 1f / _texel;
        _invWorld = 1f / (_size * _texel);

        _stamps = new NativeArray<StampData>(math.max(1, _settings.stampCapacity), Allocator.Persistent);
        if (_compute == null) return;

        _decayKernel = _compute.FindKernel("Decay");
        _stampKernel = _compute.FindKernel("Stamp");
        _stampBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _stamps.Length, Marshal.SizeOf<StampData>());

        RenderTextureFormat format = SystemInfo.SupportsRandomWriteOnRenderTextureFormat(RenderTextureFormat.R16)
            ? RenderTextureFormat.R16
            : RenderTextureFormat.RHalf;
        for (int i = 0; i < _textures.Length; i++)
        {
            var rt = new RenderTexture(_size, _size, 0, format, RenderTextureReadWrite.Linear)
            {
                enableRandomWrite = true,
                useMipMap = false,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "GrassTrample" + i
            };
            rt.Create();
            _textures[i] = rt;
        }
    }

    public void Recenter(long windowCenter)
    {
        Vector3 origin = MapGenerator.WindowOriginWorld(windowCenter);
        var next = new float2(origin.x, origin.z);
        if (_hasOrigin)
            _pendingShift += (int2)math.round((next - _origin) * _invTexel);
        _origin = next;
        _hasOrigin = true;
    }

    public void Stamp(float2 position, float radius, float strength)
    {
        if (_disposed || !_hasOrigin || _stampCount >= _stamps.Length || !(strength > 0f) || !(radius > 0f)) return;

        float2 local = (position - _origin) * _invTexel;
        float r = radius * _invTexel;
        if (r < MinRadiusTexels)
        {
            float ratio = r / MinRadiusTexels;
            strength *= ratio * ratio;
            r = MinRadiusTexels;
        }
        r = math.min(r, MaxRadiusTexels);
        if (local.x < -r || local.y < -r || local.x > _size + r || local.y > _size + r) return;

        _stamps[_stampCount++] = new StampData { position = local, radius = r, strength = math.min(strength, 1f) };
    }

    public void StampCreature(float2 position, float2 velocity, float maxEnergy, float dt)
    {
        float body = maxEnergy > 0f
            ? math.clamp(math.sqrt(maxEnergy / _settings.referenceBodyEnergy), 0.5f, 2.5f)
            : 1f;
        float walk = math.saturate(math.length(velocity) / _settings.referenceSpeed);
        float press = math.lerp(_settings.restFraction, 1f, walk);
        Stamp(position, _settings.bodyRadius * body, _settings.pressPerSecond * body * press * dt);
    }

    public void Tick(float dt)
    {
        if (_disposed || _compute == null || !_hasOrigin) return;

        int src = _current, dst = 1 - _current;
        float keep = _clearPending ? 0f : math.exp2(-math.max(dt, 0f) / _settings.halfLife);

        _compute.SetInt(SizeId, _size);
        _compute.SetInt(ShiftXId, _pendingShift.x);
        _compute.SetInt(ShiftZId, _pendingShift.y);
        _compute.SetFloat(KeepId, keep);
        _compute.SetFloat(FloorId, Quantum * 1.5f);
        _compute.SetTexture(_decayKernel, SourceId, _textures[src]);
        _compute.SetTexture(_decayKernel, TargetId, _textures[dst]);
        int groups = (_size + 7) >> 3;
        _compute.Dispatch(_decayKernel, groups, groups, 1);

        if (_stampCount > 0)
        {
            _stampBuffer.SetData(_stamps, 0, 0, _stampCount);
            _compute.SetBuffer(_stampKernel, StampsId, _stampBuffer);
            _compute.SetTexture(_stampKernel, TargetId, _textures[dst]);
            _compute.Dispatch(_stampKernel, _stampCount, 1, 1);
        }

        _stampCount = 0;
        _pendingShift = int2.zero;
        _clearPending = false;
        _current = dst;

        Shader.SetGlobalTexture(TextureId, _textures[_current]);
        Shader.SetGlobalVector(WindowId, new Vector4(_origin.x, _origin.y, _invWorld, _texel));
        Shader.SetGlobalFloat(EnabledId, 1f);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Shader.SetGlobalFloat(EnabledId, 0f);
        _stampBuffer?.Release();
        _stampBuffer = null;
        for (int i = 0; i < _textures.Length; i++)
        {
            if (_textures[i] == null) continue;
            _textures[i].Release();
            if (Application.isPlaying) UnityEngine.Object.Destroy(_textures[i]);
            else UnityEngine.Object.DestroyImmediate(_textures[i]);
            _textures[i] = null;
        }
        if (_stamps.IsCreated) _stamps.Dispose();
    }
}

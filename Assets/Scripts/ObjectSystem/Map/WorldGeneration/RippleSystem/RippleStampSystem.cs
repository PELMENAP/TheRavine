using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct SplashEvent
{
    public readonly Vector3 Position;
    public readonly float Radius;
    public readonly float Energy;

    public SplashEvent(Vector3 position, float radius, float energy)
    {
        Position = position;
        Radius = radius;
        Energy = energy;
    }
}

public class RippleStampSystem : MonoBehaviour
{
    public static RippleStampSystem Instance { get; private set; }
    public static event Action<SplashEvent> OnSplash;

    private const int MaxStamps = 128;
    private const float MinRadiusTexels = 3f;

    [SerializeField, Min(16f)] private float windowSize = 96f;

    private static readonly int RippleWindowId = Shader.PropertyToID("RippleWindow");

    private struct StampData
    {
        public Vector2 center;
        public float invRadius;
        public float strength;
    }

    private ComputeBuffer _stampBuffer;
    private readonly StampData[] _stampArray = new StampData[MaxStamps];
    private readonly List<StampData> _pending = new(32);

    private float _waterSizeInv;
    private float _minRadiusMeters;

    public float WindowSize => windowSize;
    public float WaterSizeInv => _waterSizeInv;

    private void Awake()
    {
        Instance = this;
        RecalculateMetrics(RippleEffect.TextureSize);
    }

    private void OnEnable()
    {
        _stampBuffer ??= new ComputeBuffer(MaxStamps, 16);
    }

    private void OnDisable()
    {
        _stampBuffer?.Release();
        _stampBuffer = null;
    }
    private Vector2 _windowCenter;
    private bool _hasWindow;
    private float _windowHalfLimit;

    public void RecalculateMetrics(int textureSize)
    {
        windowSize = Mathf.Max(16f, windowSize);
        _waterSizeInv = 1f / windowSize;
        float texelSize = windowSize / textureSize;
        _minRadiusMeters = texelSize * MinRadiusTexels;
        _windowHalfLimit = windowSize * 0.5f - 8f * texelSize;
    }

    public void SetSnappedWindowCenter(float snappedWorldX, float snappedWorldZ)
    {
        _windowCenter = new Vector2(snappedWorldX, snappedWorldZ);
        _hasWindow = true;
        Shader.SetGlobalVector(
            RippleWindowId,
            new Vector4(snappedWorldX * _waterSizeInv, snappedWorldZ * _waterSizeInv, _waterSizeInv, 0f));
    }

    [Obsolete("Window center is managed by RippleEffect via SetSnappedWindowCenter.")]
    public void SetWaterPosition(float waterX, float waterZ) { }

    public void Stamp(Vector3 worldPos, float radius, float strength)
    {
        if (Mathf.Abs(strength) < 1e-4f) return;

        OnSplash?.Invoke(new SplashEvent(worldPos, radius, Mathf.Abs(strength)));

        if (!_hasWindow) return;
        if (Mathf.Abs(worldPos.x - _windowCenter.x) > _windowHalfLimit ||
            Mathf.Abs(worldPos.z - _windowCenter.y) > _windowHalfLimit)
            return;

        float safeRadius = Mathf.Max(radius, _minRadiusMeters);
        float uvX = worldPos.x * _waterSizeInv;
        float uvZ = worldPos.z * _waterSizeInv;
        Vector2 uv = new(uvX - Mathf.Floor(uvX), uvZ - Mathf.Floor(uvZ));

        var data = new StampData
        {
            center = uv,
            invRadius = windowSize / safeRadius,
            strength = strength
        };

        if (_pending.Count < MaxStamps)
        {
            _pending.Add(data);
        }
        else
        {
            int weakestIdx = 0;
            float weakestAbs = Mathf.Abs(_pending[0].strength);
            for (int i = 1; i < _pending.Count; i++)
            {
                float a = Mathf.Abs(_pending[i].strength);
                if (a < weakestAbs)
                {
                    weakestAbs = a;
                    weakestIdx = i;
                }
            }

            if (Mathf.Abs(strength) > weakestAbs)
                _pending[weakestIdx] = data;
        }
    }

    public int UploadStamps(out ComputeBuffer buffer)
    {
        buffer = _stampBuffer;
        if (buffer == null) return 0;

        int count = _pending.Count;
        if (count > 0)
        {
            for (int i = 0; i < count; i++)
                _stampArray[i] = _pending[i];

            buffer.SetData(_stampArray, 0, 0, count);
            _pending.Clear();
        }

        return count;
    }
}
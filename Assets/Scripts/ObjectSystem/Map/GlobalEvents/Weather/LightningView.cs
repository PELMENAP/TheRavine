using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheRavine.Generator
{
    [Serializable]
    public sealed class LightningViewSettings
    {
        public Material boltMaterial;
        [Min(1)] public int maxBolts = 8;
        [Min(0.05f)] public float duration = 0.3f;
        [Min(0.01f)] public float boltWidth = 0.7f;
        [Range(0.02f, 0.5f)] public float displacement = 0.16f;
        [Min(0f)] public float lightIntensity = 60f;
        [Min(1f)] public float lightRange = 90f;
        [Min(1f)] public float flashRange = 700f;
        [Min(0f)] public float flashGain = 1f;
        [Min(0f)] public float flashDecay = 7f;
    }

    public sealed class LightningView : IDisposable
    {
        private const string BoltShader = "Weather/Lightning";
        private const int MainDepth = 5;
        private const int BranchDepth = 3;
        private const int MaxBranches = 3;
        private const int SegmentsPerBolt = (1 << MainDepth) + MaxBranches * (1 << BranchDepth);
        private const float Unused = -1e6f;
        private const float CloudFlashStrength = 0.6f;
        private const float LightLift = 3f;

        private static readonly int SegmentsId = Shader.PropertyToID("_BoltSegments");
        private static readonly int TimeId     = Shader.PropertyToID("_BoltTime");
        private static readonly int DurationId = Shader.PropertyToID("_BoltDuration");
        private static readonly int FlashId    = Shader.PropertyToID("_LightningFlash");

        private static readonly ProfilerMarker BuildMarker = new("LightningView.Build");

        [StructLayout(LayoutKind.Sequential)]
        private struct BoltSegment
        {
            public float4 A;
            public float4 B;
            public float4 C;
        }

        private readonly WeatherEvent _weather;
        private readonly LightningViewSettings _settings;
        private readonly Material _owned;
        private readonly MaterialPropertyBlock _props = new();
        private readonly float3[] _main = new float3[(1 << MainDepth) + 1];
        private readonly float3[] _branch = new float3[(1 << BranchDepth) + 1];
        private readonly float[] _boltBirth;
        private readonly float[] _boltSeed;
        private readonly float3[] _boltPoint;
        private readonly float3[] _boltMin;
        private readonly float3[] _boltMax;
        private readonly float3[] _flashPoint;
        private readonly float[] _flashBirth;
        private readonly float[] _flashSeed;
        private readonly float[] _flashStrength;
        private readonly GameObject _lightObject;
        private readonly Light _light;
        private RenderParams _params;

        private NativeArray<BoltSegment> _segments;
        private GraphicsBuffer _buffer;
        private double _epoch;
        private float _lastNow;
        private float _flash;
        private int _seen;
        private int _boltHead;
        private int _flashHead;
        private bool _disposed;

        public static LightningView TryCreate(WeatherEvent weather, LightningViewSettings settings)
        {
            if (weather == null || settings == null || !SystemInfo.supportsComputeShaders) return null;

            Material material = settings.boltMaterial;
            Material owned = null;
            if (material == null)
            {
                Shader shader = Resources.Load<Shader>(BoltShader);
                if (shader != null && shader.isSupported) owned = new Material(shader) { hideFlags = HideFlags.DontSave };
                material = owned;
            }
            if (material == null)
            {
                RavineLog.Warning("[LightningView] Lightning shader not found, bolts are not rendered");
                return null;
            }
            return new LightningView(weather, settings, material, owned);
        }

        private LightningView(WeatherEvent weather, LightningViewSettings settings, Material material, Material owned)
        {
            _weather = weather;
            _settings = settings;
            _owned = owned;
            _epoch = SimulationClock.TimeD;
            _seen = weather.StrikeCount;

            int bolts = math.max(settings.maxBolts, 1);
            _boltBirth = new float[bolts];
            _boltSeed = new float[bolts];
            _boltPoint = new float3[bolts];
            _boltMin = new float3[bolts];
            _boltMax = new float3[bolts];
            _flashPoint = new float3[bolts];
            _flashBirth = new float[bolts];
            _flashSeed = new float[bolts];
            _flashStrength = new float[bolts];
            for (int i = 0; i < bolts; i++) _boltBirth[i] = _flashBirth[i] = Unused;

            _segments = new NativeArray<BoltSegment>(bolts * SegmentsPerBolt, Allocator.Persistent);
            for (int i = 0; i < _segments.Length; i++) _segments[i] = new BoltSegment { C = new float4(Unused, 0f, 0f, 0f) };
            _buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _segments.Length, Marshal.SizeOf<BoltSegment>());
            _buffer.SetData(_segments);

            _props.SetBuffer(SegmentsId, _buffer);
            _props.SetFloat(DurationId, settings.duration);
            _params = new RenderParams(material)
            {
                matProps = _props,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = 0
            };

            _lightObject = new GameObject("LightningLight");
            _light = _lightObject.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.shadows = LightShadows.None;
            _light.range = settings.lightRange;
            _light.color = new Color(0.75f, 0.85f, 1f);
            _light.intensity = 0f;
            _light.enabled = false;
        }

        public void Render(Camera camera)
        {
            if (_disposed || _weather.IsDisposed || camera == null) return;

            float now = (float)(SimulationClock.TimeD - _epoch);
            if (now < _lastNow) Reset();
            _lastNow = now;

            Consume(now);
            UpdateFlash(now, camera.transform.position, SimulationClock.DeltaTime);
            UpdateLight(now);
            DrawBolts(now);
        }

        private void Reset()
        {
            _epoch = SimulationClock.TimeD;
            _seen = _weather.StrikeCount;
            for (int i = 0; i < _boltBirth.Length; i++) _boltBirth[i] = _flashBirth[i] = Unused;
        }

        private void Consume(float now)
        {
            int count = _weather.StrikeCount;
            if (count == _seen) return;

            using (BuildMarker.Auto())
            {
                for (int serial = math.max(_seen, count - _weather.StrikeCapacity); serial < count; serial++)
                {
                    WeatherStrike strike = _weather.StrikeAt(serial);
                    AddFlash(in strike, now);
                    if (strike.Ground != 0) AddBolt(in strike, now);
                }
                _seen = count;
            }
        }

        private void AddFlash(in WeatherStrike strike, float now)
        {
            int slot = _flashHead;
            _flashHead = (_flashHead + 1) % _flashBirth.Length;
            _flashPoint[slot] = strike.Position;
            _flashBirth[slot] = now;
            _flashSeed[slot] = RavineRandom.RangeFloat();
            _flashStrength[slot] = (strike.Ground != 0 ? 1f : CloudFlashStrength) * (0.6f + 0.4f * math.saturate(strike.Strength));
        }

        private void AddBolt(in WeatherStrike strike, float now)
        {
            int slot = _boltHead;
            _boltHead = (_boltHead + 1) % _boltBirth.Length;

            float seed = RavineRandom.RangeFloat();
            float height = _weather.Rules.cloudHeight;
            float3 bottom = strike.Position;
            float3 top = bottom + new float3(RavineRandom.RangeFloat(-0.3f, 0.3f) * height, height, RavineRandom.RangeFloat(-0.3f, 0.3f) * height);

            _main[0] = top;
            _main[1] = bottom;
            int n = Subdivide(_main, 2, MainDepth, _settings.displacement);

            float3 min = math.min(top, bottom);
            float3 max = math.max(top, bottom);
            int written = 0;
            int baseIndex = slot * SegmentsPerBolt;
            float4 c = new float4(now, seed, 0.7f + 0.3f * math.saturate(strike.Strength), 0f);

            for (int i = 0; i < n - 1; i++)
            {
                float t0 = i / (float)(n - 1);
                float t1 = (i + 1) / (float)(n - 1);
                _segments[baseIndex + written++] = new BoltSegment
                {
                    A = new float4(_main[i], _settings.boltWidth * math.lerp(0.55f, 1f, t0)),
                    B = new float4(_main[i + 1], _settings.boltWidth * math.lerp(0.55f, 1f, t1)),
                    C = c
                };
                min = math.min(min, _main[i]);
                max = math.max(max, _main[i]);
            }

            int branches = RavineRandom.RangeInt(2, MaxBranches + 1);
            for (int b = 0; b < branches; b++)
            {
                int from = RavineRandom.RangeInt(n / 5, n * 3 / 5);
                float3 origin = _main[from];
                float2 lateral = new float2(RavineRandom.RangeFloat(-1f, 1f), RavineRandom.RangeFloat(-1f, 1f));
                float3 direction = math.normalizesafe(new float3(lateral.x * 0.8f, -0.6f, lateral.y * 0.8f));
                float length = math.distance(top, bottom) * RavineRandom.RangeFloat(0.15f, 0.35f);
                _branch[0] = origin;
                _branch[1] = origin + direction * length;
                int m = Subdivide(_branch, 2, BranchDepth, _settings.displacement);

                for (int i = 0; i < m - 1; i++)
                {
                    float t0 = 1f - i / (float)(m - 1);
                    float t1 = 1f - (i + 1) / (float)(m - 1);
                    _segments[baseIndex + written++] = new BoltSegment
                    {
                        A = new float4(_branch[i], _settings.boltWidth * 0.4f * t0),
                        B = new float4(_branch[i + 1], _settings.boltWidth * 0.4f * t1),
                        C = c
                    };
                    min = math.min(min, _branch[i + 1]);
                    max = math.max(max, _branch[i + 1]);
                }
            }

            for (int i = written; i < SegmentsPerBolt; i++)
                _segments[baseIndex + i] = new BoltSegment { C = new float4(Unused, 0f, 0f, 0f) };

            _boltBirth[slot] = now;
            _boltSeed[slot] = seed;
            _boltPoint[slot] = bottom;
            _boltMin[slot] = min;
            _boltMax[slot] = max;
            _buffer.SetData(_segments, baseIndex, baseIndex, SegmentsPerBolt);
        }

        private static int Subdivide(float3[] points, int count, int depth, float displacement)
        {
            float scale = displacement;
            for (int level = 0; level < depth; level++)
            {
                for (int i = count - 1; i >= 0; i--) points[2 * i] = points[i];
                count = 2 * count - 1;

                for (int i = 1; i < count; i += 2)
                {
                    float3 a = points[i - 1];
                    float3 b = points[i + 1];
                    float3 d = b - a;
                    float length = math.length(d);
                    float3 dir = d / math.max(length, 1e-4f);
                    float3 u = math.normalizesafe(math.cross(dir, math.abs(dir.y) > 0.9f ? new float3(1f, 0f, 0f) : new float3(0f, 1f, 0f)));
                    float3 v = math.cross(dir, u);
                    points[i] = (a + b) * 0.5f + (u * RavineRandom.RangeFloat(-1f, 1f) + v * RavineRandom.RangeFloat(-1f, 1f)) * (length * scale);
                }
                scale *= 0.85f;
            }
            return count;
        }

        private void UpdateFlash(float now, float3 eye, float dt)
        {
            float target = 0f;
            float duration = _settings.duration;
            for (int i = 0; i < _flashBirth.Length; i++)
            {
                float intensity = WeatherMath.StrokeIntensity(now - _flashBirth[i], _flashSeed[i], duration) * _flashStrength[i];
                if (intensity <= 0f) continue;

                float atten = math.saturate(1f - math.distance(eye, _flashPoint[i]) / _settings.flashRange);
                target = math.max(target, intensity * atten * atten);
            }

            _flash = math.max(target, _flash * math.exp(-dt * _settings.flashDecay));
            if (_flash < 1e-3f) _flash = 0f;
            Shader.SetGlobalFloat(FlashId, _flash * _settings.flashGain);
        }

        private void UpdateLight(float now)
        {
            int best = -1;
            float bestLevel = 0f;
            for (int i = 0; i < _boltBirth.Length; i++)
            {
                float level = WeatherMath.StrokeIntensity(now - _boltBirth[i], _boltSeed[i], _settings.duration);
                if (level <= bestLevel) continue;
                bestLevel = level;
                best = i;
            }

            if (best < 0)
            {
                if (_light.enabled) _light.enabled = false;
                return;
            }

            _light.transform.position = _boltPoint[best] + new float3(0f, LightLift, 0f);
            _light.intensity = _settings.lightIntensity * bestLevel;
            if (!_light.enabled) _light.enabled = true;
        }

        private void DrawBolts(float now)
        {
            float3 min = new(float.MaxValue);
            float3 max = new(float.MinValue);
            bool any = false;
            for (int i = 0; i < _boltBirth.Length; i++)
            {
                float age = now - _boltBirth[i];
                if (age < 0f || age >= _settings.duration) continue;
                any = true;
                min = math.min(min, _boltMin[i]);
                max = math.max(max, _boltMax[i]);
            }
            if (!any) return;

            _props.SetFloat(TimeId, now);
            _params.worldBounds = new Bounds((min + max) * 0.5f, max - min + new float3(8f));
            Graphics.RenderPrimitives(in _params, MeshTopology.Triangles, 6, _segments.Length);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Shader.SetGlobalFloat(FlashId, 0f);
            _buffer?.Release();
            _buffer = null;
            if (_segments.IsCreated) _segments.Dispose();
            if (_lightObject != null) UnityEngine.Object.Destroy(_lightObject);
            if (_owned != null) UnityEngine.Object.Destroy(_owned);
        }
    }
}

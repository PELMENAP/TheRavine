using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

using TheRavine.Extensions;

namespace TheRavine.Generator
{
    [Serializable]
    public sealed class FireViewSettings
    {
        public bool enabled = true;
        public Material flameMaterial;
        public Material smokeMaterial;
        [Min(0)] public int lights = 4;
        [Min(0f)] public float lightIntensity = 6f;
        [Min(0.1f)] public float lightRange = 14f;
    }

    public sealed class FireView : IDisposable
    {
        private const int FlameVertices = 6 * 17;
        private const int SmokeVertices = 6 * 6;
        private const int ResidueCapacity = 1024;
        private const int MaxClusters = 16;
        private const float ClusterRadius = 10f;
        private const float ResidueSweepInterval = 1f;
        private const float HorizontalMargin = 16f;
        private const float VerticalMargin = 22f;
        private const float DetailBudget = 160f;
        private const float LightLift = 1.6f;
        private const float LightCameraFalloff = 900f;

        private const string FlameShader = "Fire/FireFlame";
        private const string SmokeShader = "Fire/FireSmoke";

        private static readonly int InstancesId     = Shader.PropertyToID("_FireInstances");
        private static readonly int TimeId          = Shader.PropertyToID("_FireTime");
        private static readonly int WindId          = Shader.PropertyToID("_FireWind");
        private static readonly int KeepId          = Shader.PropertyToID("_FireKeep");
        private static readonly int ScorchSecondsId = Shader.PropertyToID("_ScorchSeconds");

        private static readonly ProfilerMarker RebuildMarker = new("FireView.Rebuild");

        [StructLayout(LayoutKind.Sequential)]
        private struct FireInstance
        {
            public float4 PosSeed;
            public float4 Life;
            public float4 Normal;
            public float4 Shape;
        }

        private struct Tracked
        {
            public float3 Position;
            public float3 Normal;
            public float  Seed;
            public float  Size;
            public float  Total;
            public float  Ignited;
            public float  Left;
        }

        private readonly FireEvent _fire;
        private readonly MapGenerator _map;
        private readonly MaterialPropertyBlock _props = new();
        private readonly float _residueSeconds;
        private readonly Material _ownedFlame;
        private readonly Material _ownedSmoke;
        private double _epoch;
        private RenderParams _flameParams;
        private RenderParams _smokeParams;

        private NativeHashMap<long, Tracked> _tracked;
        private NativeList<long> _gone;
        private NativeArray<FireInstance> _residues;
        private NativeArray<FireInstance> _instances;
        private NativeArray<float2> _windFallback;
        private GraphicsBuffer _buffer;

        private int _residueCount;
        private int _burningCount;
        private int _instanceCount;
        private int _version = -1;
        private float _nextSweep;
        private float _lastNow;
        private float3 _boundsMin;
        private float3 _boundsMax;
        private float2 _wind;
        private WindField _windField;

        private readonly GameObject _lightRoot;
        private readonly Light[] _lights;
        private readonly float3[] _lightPos;
        private readonly float[] _lightLevel;
        private readonly float4[] _lightTarget;
        private readonly float4[] _clusters = new float4[MaxClusters];
        private readonly float[] _clusterWeight = new float[MaxClusters];
        private readonly int[] _picked;
        private readonly float _lightIntensity;
        private int _clusterCount;
        private bool _disposed;

        public static FireView TryCreate(FireEvent fire, MapGenerator map, FireViewSettings settings)
        {
            if (fire == null || map == null || settings == null || !SystemInfo.supportsComputeShaders) return null;

            Material flame = settings.flameMaterial;
            Material smoke = settings.smokeMaterial;
            Material ownedFlame = flame == null ? CreateMaterial(FlameShader) : null;
            Material ownedSmoke = smoke == null ? CreateMaterial(SmokeShader) : null;
            if (flame == null) flame = ownedFlame;
            if (smoke == null) smoke = ownedSmoke;
            if (flame == null || smoke == null)
            {
                RavineLog.Warning("[FireView] Fire shaders not found, fire is not rendered");
                DestroyMaterial(ownedFlame);
                DestroyMaterial(ownedSmoke);
                return null;
            }
            return new FireView(fire, map, flame, smoke, ownedFlame, ownedSmoke, settings);
        }

        private static Material CreateMaterial(string shaderPath)
        {
            Shader shader = Resources.Load<Shader>(shaderPath);
            return shader != null && shader.isSupported ? new Material(shader) { hideFlags = HideFlags.DontSave } : null;
        }

        private static void DestroyMaterial(Material material)
        {
            if (material != null) UnityEngine.Object.Destroy(material);
        }

        private FireView(FireEvent fire, MapGenerator map, Material flame, Material smoke,
            Material ownedFlame, Material ownedSmoke, FireViewSettings settings)
        {
            _fire  = fire;
            _map   = map;
            _ownedFlame = ownedFlame;
            _ownedSmoke = ownedSmoke;
            _epoch = SimulationClock.TimeD;
            _residueSeconds = (smoke.HasFloat(ScorchSecondsId) ? smoke.GetFloat(ScorchSecondsId) : 40f) + 2f;
            _lightIntensity = settings.lightIntensity;

            int capacity = math.max(fire.Rules.maxBurning, 16);
            _tracked      = new NativeHashMap<long, Tracked>(capacity * 2, Allocator.Persistent);
            _gone         = new NativeList<long>(capacity, Allocator.Persistent);
            _residues     = new NativeArray<FireInstance>(ResidueCapacity, Allocator.Persistent);
            _instances    = new NativeArray<FireInstance>(capacity + ResidueCapacity, Allocator.Persistent);
            _windFallback = new NativeArray<float2>(1, Allocator.Persistent);
            _buffer       = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _instances.Length, Marshal.SizeOf<FireInstance>());

            _props.SetBuffer(InstancesId, _buffer);
            _flameParams = CreateParams(flame, _props);
            _smokeParams = CreateParams(smoke, _props);

            int count = math.max(0, settings.lights);
            _lights      = new Light[count];
            _lightPos    = new float3[count];
            _lightLevel  = new float[count];
            _lightTarget = new float4[count];
            _picked      = new int[count];
            if (count == 0) return;

            _lightRoot = new GameObject("FireLights");
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("FireLight");
                go.transform.SetParent(_lightRoot.transform, false);
                var light = go.AddComponent<Light>();
                light.type      = LightType.Point;
                light.shadows   = LightShadows.None;
                light.range     = settings.lightRange;
                light.color     = new Color(1f, 0.55f, 0.24f);
                light.intensity = 0f;
                light.enabled   = false;
                _lights[i] = light;
            }
        }

        private static RenderParams CreateParams(Material material, MaterialPropertyBlock props) =>
            new(material)
            {
                matProps = props,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = 0
            };

        public void Render(Camera camera)
        {
            if (_disposed || _fire.IsDisposed) return;

            float now = (float)(SimulationClock.TimeD - _epoch);
            if (now < _lastNow)
            {
                _epoch = SimulationClock.TimeD;
                now = 0f;
                _tracked.Clear();
                _residueCount = 0;
                _version = -1;
            }
            _lastNow = now;

            if (_fire.Version != _version || (_residueCount > 0 && now >= _nextSweep))
            {
                _version = _fire.Version;
                using (RebuildMarker.Auto()) Rebuild(now, camera);
            }

            float dt = SimulationClock.DeltaTime;
            UpdateLights(now, dt);
            if (_instanceCount == 0) return;

            UpdateWind(dt);
            _props.SetFloat(TimeId, now);
            _props.SetVector(WindId, new Vector4(_wind.x, _wind.y, 0f, 0f));
            _props.SetFloat(KeepId, math.saturate(DetailBudget / math.max(1f, _burningCount)));

            float3 min = _boundsMin - new float3(HorizontalMargin, 2f, HorizontalMargin);
            float3 max = _boundsMax + new float3(HorizontalMargin, VerticalMargin, HorizontalMargin);
            var bounds = new Bounds((min + max) * 0.5f, max - min);
            _smokeParams.worldBounds = bounds;
            _flameParams.worldBounds = bounds;

            Graphics.RenderPrimitives(in _smokeParams, MeshTopology.Triangles, SmokeVertices, _instanceCount);
            Graphics.RenderPrimitives(in _flameParams, MeshTopology.Triangles, FlameVertices, _instanceCount);
        }

        private void Rebuild(float now, Camera camera)
        {
            var burning = _fire.Burning;
            float burnedLeft = _fire.Rules.stepInterval * 2f;
            float stepTime   = (float)(_fire.StepTime - _epoch);

            _gone.Clear();
            foreach (var kv in _tracked)
                if (!burning.ContainsKey(kv.Key)) _gone.Add(kv.Key);

            for (int i = 0; i < _gone.Length; i++)
            {
                long cell = _gone[i];
                Tracked t = _tracked[cell];
                _tracked.Remove(cell);
                AddResidue(in t, now, t.Left <= burnedLeft);
            }

            int capacity = _instances.Length - ResidueCapacity;
            int n = 0;
            float3 min = new(float.MaxValue);
            float3 max = new(float.MinValue);
            _clusterCount = 0;

            foreach (var kv in burning)
            {
                if (n >= capacity) break;
                FireCell cell = kv.Value;
                if (!_tracked.TryGetValue(kv.Key, out Tracked t)) t = Resolve(kv.Key, in cell, now);
                t.Left  = cell.Left;
                t.Total = cell.Total;
                t.Size  = cell.Size;
                _tracked[kv.Key] = t;

                float stamp = t.Left >= t.Total - 1e-4f ? t.Ignited : math.max(stepTime, t.Ignited);
                _instances[n++] = Pack(in t, new float4(t.Ignited, t.Left, stamp, -1f));
                min = math.min(min, t.Position);
                max = math.max(max, t.Position + new float3(0f, t.Size, 0f));
                Cluster(t.Position, t.Size);
            }
            _burningCount = n;

            for (int i = _residueCount - 1; i >= 0; i--)
            {
                if (now - _residues[i].Life.w <= _residueSeconds) continue;
                _residues[i] = _residues[--_residueCount];
            }

            for (int i = 0; i < _residueCount; i++)
            {
                FireInstance residue = _residues[i];
                _instances[n++] = residue;
                min = math.min(min, residue.PosSeed.xyz);
                max = math.max(max, residue.PosSeed.xyz + new float3(0f, residue.Shape.x, 0f));
            }

            _instanceCount = n;
            _boundsMin = min;
            _boundsMax = max;
            _nextSweep = now + ResidueSweepInterval;
            if (n > 0) _buffer.SetData(_instances, 0, 0, n);

            if (_windField == null) ServiceLocator.Services.TryGet(out _windField);
            PickLights(camera);
        }

        private static FireInstance Pack(in Tracked t, float4 life) => new()
        {
            PosSeed = new float4(t.Position, t.Seed),
            Life    = life,
            Normal  = new float4(t.Normal, 0f),
            Shape   = new float4(t.Size, t.Total, 0f, 0f)
        };

        private Tracked Resolve(long cell, in FireCell fire, float now)
        {
            int cx = Position2Int.GetX(cell);
            int cz = Position2Int.GetY(cell);
            float x = cx << MapGenerator.CellShift;
            float z = cz << MapGenerator.CellShift;

            float3 position = _map.TryGetObject(Position2Int.Pack(cx << MapGenerator.CellShift, cz << MapGenerator.CellShift), out ObjectInstInfo inst)
                ? (float3)inst.Position
                : new float3(x, _map.SampleHeightBilinear(x, z), z);

            return new Tracked
            {
                Position = position,
                Normal   = _map.SampleNormal(position.x, position.z),
                Seed     = (math.hash(new int2(cx, cz)) & 0xFFFFFF) * (1f / 16777216f),
                Size     = fire.Size,
                Total    = fire.Total,
                Ignited  = now,
                Left     = fire.Left
            };
        }

        private void AddResidue(in Tracked t, float now, bool burned)
        {
            FireInstance residue = Pack(in t, new float4(t.Ignited, burned ? 1f : 0f, now, now));

            if (_residueCount < ResidueCapacity)
            {
                _residues[_residueCount++] = residue;
                return;
            }

            int oldest = 0;
            float oldestTime = _residues[0].Life.w;
            for (int i = 1; i < _residueCount; i++)
            {
                float time = _residues[i].Life.w;
                if (time >= oldestTime) continue;
                oldestTime = time;
                oldest = i;
            }
            _residues[oldest] = residue;
        }

        private void Cluster(float3 p, float size)
        {
            const float radiusSq = ClusterRadius * ClusterRadius;
            for (int i = 0; i < _clusterCount; i++)
            {
                float4 c = _clusters[i];
                if (math.distancesq(c.xz / c.w, p.xz) > radiusSq) continue;
                _clusters[i] = c + new float4(p, 1f);
                _clusterWeight[i] += size;
                return;
            }
            if (_clusterCount == MaxClusters) return;
            _clusters[_clusterCount] = new float4(p, 1f);
            _clusterWeight[_clusterCount++] = size;
        }

        private void PickLights(Camera camera)
        {
            int count = _lights.Length;
            if (count == 0) return;

            float3 eye = camera != null ? (float3)camera.transform.position : float3.zero;
            int used = 0;
            int picks = 0;
            for (int k = 0; k < count; k++)
            {
                int best = -1;
                float bestScore = 0f;
                for (int i = 0; i < _clusterCount; i++)
                {
                    if ((used & (1 << i)) != 0) continue;
                    float4 c = _clusters[i];
                    float score = math.sqrt(_clusterWeight[i]) / (1f + math.distancesq(c.xyz / c.w, eye) / LightCameraFalloff);
                    if (score <= bestScore) continue;
                    bestScore = score;
                    best = i;
                }
                if (best < 0) break;
                used |= 1 << best;
                _picked[picks++] = best;
            }

            for (int k = 0; k < count; k++) _lightTarget[k].w = 0f;

            for (int k = 0; k < count && picks > 0; k++)
            {
                int nearest = 0;
                float nearestSq = float.MaxValue;
                for (int p = 0; p < picks; p++)
                {
                    float4 c = _clusters[_picked[p]];
                    float d = math.distancesq(c.xyz / c.w, _lightPos[k]);
                    if (d >= nearestSq) continue;
                    nearestSq = d;
                    nearest = p;
                }

                int index = _picked[nearest];
                float4 cluster = _clusters[index];
                float weight = _clusterWeight[index];
                float lift = LightLift + 0.5f * weight / cluster.w;
                _lightTarget[k] = new float4(cluster.xyz / cluster.w + new float3(0f, lift, 0f), math.min(1f, math.sqrt(weight) / 3f));
                _picked[nearest] = _picked[--picks];
            }
        }

        private void UpdateLights(float now, float dt)
        {
            float k = 1f - math.exp(-dt * 3f);
            for (int i = 0; i < _lights.Length; i++)
            {
                Light light = _lights[i];
                if (light == null) continue;

                float4 target = _lightTarget[i];
                if (_lightLevel[i] < 0.02f) _lightPos[i] = target.xyz;
                _lightLevel[i] = math.lerp(_lightLevel[i], target.w, k);
                _lightPos[i]   = math.lerp(_lightPos[i], target.xyz, k);

                if (_lightLevel[i] < 0.01f)
                {
                    if (light.enabled) light.enabled = false;
                    continue;
                }

                float flicker = 0.84f
                    + 0.12f * noise.snoise(new float2(now * 4.3f, i * 13.7f))
                    + 0.06f * noise.snoise(new float2(now * 11.1f, i * 5.3f + 3f));
                light.intensity = _lightIntensity * _lightLevel[i] * flicker;
                light.transform.position = _lightPos[i];
                if (!light.enabled) light.enabled = true;
            }
        }

        private void UpdateWind(float dt)
        {
            float2 target = float2.zero;
            if (_windField != null)
            {
                float2 centre = (_boundsMin.xz + _boundsMax.xz) * 0.5f;
                target = _windField.GetSampler(SimulationClock.Time, _windFallback).Sample(centre);
            }
            _wind = math.lerp(_wind, target, 1f - math.exp(-dt * 2f));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _buffer?.Release();
            _buffer = null;
            if (_tracked.IsCreated) _tracked.Dispose();
            if (_gone.IsCreated) _gone.Dispose();
            if (_residues.IsCreated) _residues.Dispose();
            if (_instances.IsCreated) _instances.Dispose();
            if (_windFallback.IsCreated) _windFallback.Dispose();
            if (_lightRoot != null) UnityEngine.Object.Destroy(_lightRoot);
            DestroyMaterial(_ownedFlame);
            DestroyMaterial(_ownedSmoke);
        }
    }
}

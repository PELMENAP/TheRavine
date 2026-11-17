using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheRavine.Generator
{
    [Serializable]
    public sealed class WildWindViewSettings
    {
        public bool enabled = true;
        public ComputeShader compute;
        public Material material;
        [Min(64)] public int boidsPerGust = 512;
        public Vector3 extents = new(30f, 15f, 30f);
        [Min(0.5f)] public float perception = 3f;
        [Min(0f)] public float viewDistance = 800f;
        [Min(0.001f)] public float maxDeltaTime = 0.033f;
        [Min(0f)] public float trail = 0.5f;
        public BoidFlockWeights weights = new()
        {
            separationRadius = 1.1f,
            separationMin = 0.25f,
            maxNeighbors = 24,
            separation = 3f,
            alignment = 1.2f,
            cohesion = 0.6f,
            bounds = 6f,
            target = 1.5f,
            noise = 1.5f,
            speedRange = new Vector2(4f, 9f),
            maxSteer = 14f
        };
    }

    public sealed class WildWindView : IDisposable
    {
        private static readonly int RenderBoidsId = Shader.PropertyToID("_Boids");
        private static readonly int CenterId = Shader.PropertyToID("_Center");
        private static readonly int RenderTimeId = Shader.PropertyToID("_Tm");

        private readonly WildWindEvent _wind;
        private readonly WildWindViewSettings _settings;
        private readonly BoidFlockGpu[] _flocks;
        private readonly MaterialPropertyBlock[] _props;
        private readonly GraphicsBuffer _args;
        private readonly Mesh _mesh;
        private double _lastTime = double.NaN;
        private int _frame;
        private bool _disposed;

        public static WildWindView TryCreate(WildWindEvent wind, WildWindViewSettings settings)
        {
            if (wind == null || settings == null || !settings.enabled) return null;
            if (settings.compute == null || settings.material == null)
            {
                RavineLog.Warning("[WildWindView] Boids compute shader or material is not assigned, wild winds are not drawn");
                return null;
            }
            if (!SystemInfo.supportsComputeShaders) return null;
            return new WildWindView(wind, settings);
        }

        private WildWindView(WildWindEvent wind, WildWindViewSettings settings)
        {
            _wind = wind;
            _settings = settings;

            int gusts = wind.GustCount;
            int boids = math.max(settings.boidsPerGust, 64);
            _flocks = new BoidFlockGpu[gusts];
            _props = new MaterialPropertyBlock[gusts];
            _mesh = BoidFlockGpu.BuildMesh();
            _args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            _args.SetData(new[]
            {
                new GraphicsBuffer.IndirectDrawIndexedArgs
                {
                    indexCountPerInstance = _mesh.GetIndexCount(0),
                    instanceCount = (uint)boids
                }
            });

            var data = new BoidGpu[boids];
            Vector3 extents = settings.extents;
            Vector2 speed = settings.weights.speedRange;
            for (int g = 0; g < gusts; g++)
            {
                for (int i = 0; i < boids; i++)
                {
                    float3 p = RandomInSphere() * 0.8f;
                    data[i] = new BoidGpu
                    {
                        position = new Vector3(p.x * extents.x, p.y * extents.y, p.z * extents.z),
                        phase = RavineRandom.RangeFloat() * math.PI * 2f,
                        velocity = (Vector3)(math.normalizesafe(RandomInSphere(), new float3(0f, 0f, 1f))
                                   * math.lerp(speed.x, speed.y, RavineRandom.RangeFloat())),
                        bank = 0f
                    };
                }

                _flocks[g] = new BoidFlockGpu(settings.compute, boids, extents, settings.perception);
                _flocks[g].SetData(data);

                var block = new MaterialPropertyBlock();
                block.SetBuffer(RenderBoidsId, _flocks[g].Output);
                _props[g] = block;
            }
        }

        private static float3 RandomInSphere()
        {
            for (int attempt = 0; attempt < 16; attempt++)
            {
                float3 p = new float3(RavineRandom.RangeFloat(), RavineRandom.RangeFloat(), RavineRandom.RangeFloat()) * 2f - 1f;
                if (math.lengthsq(p) <= 1f) return p;
            }
            return float3.zero;
        }

        public void Render(Camera camera)
        {
            if (_disposed || camera == null || !_wind.Active)
            {
                _lastTime = double.NaN;
                return;
            }

            double now = SimulationClock.TimeD;
            float dt = double.IsNaN(_lastTime) ? 0f : math.clamp((float)(now - _lastTime), 0f, _settings.maxDeltaTime);
            _lastTime = now;
            float time = (float)now;
            if (dt > 0f) _frame++;

            ComputeShader compute = _settings.compute;
            BoidFlockGpu.ApplyWeights(compute, in _settings.weights);
            BoidFlockGpu.ClearInteraction(compute);

            float3 eye = camera.transform.position;
            float range2 = _settings.viewDistance * _settings.viewDistance;
            Vector3 boundsSize = _settings.extents * 2.5f;

            for (int g = 0; g < _flocks.Length; g++)
            {
                float3 center = _wind.GustPosition(g);
                if (math.distancesq(center, eye) > range2) continue;

                BoidFlockGpu.ApplyFrame(compute, dt, time, _frame, Vector3.zero, (Vector3)(-_wind.GustVelocity(g) * _settings.trail));
                _flocks[g].Dispatch();

                MaterialPropertyBlock block = _props[g];
                block.SetVector(CenterId, (Vector3)center);
                block.SetFloat(RenderTimeId, time);

                var rp = new RenderParams(_settings.material)
                {
                    worldBounds = new Bounds(center, boundsSize),
                    matProps = block,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false
                };
                Graphics.RenderMeshIndirect(in rp, _mesh, _args);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            for (int g = 0; g < _flocks.Length; g++)
            {
                _flocks[g]?.Dispose();
                _flocks[g] = null;
            }
            _args?.Release();
            if (_mesh != null) UnityEngine.Object.Destroy(_mesh);
        }
    }
}

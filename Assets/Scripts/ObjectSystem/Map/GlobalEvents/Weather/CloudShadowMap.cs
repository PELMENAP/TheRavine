using System;
using Unity.Mathematics;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Rendering;

namespace TheRavine.Generator
{
    public sealed class CloudShadowMap : IDisposable
    {
        private const string BakeShader = "Weather/CloudShadowBake";

        private static readonly ProfilerMarker BakeMarker = new("CloudShadowMap.Bake");
        private static readonly int MapId       = Shader.PropertyToID("_CloudShadowMap");
        private static readonly int RectId      = Shader.PropertyToID("_CloudShadowRect");
        private static readonly int BakeRectId  = Shader.PropertyToID("_BakeRect");
        private static readonly int ParamsId    = Shader.PropertyToID("_BakeParams");
        private static readonly int ScaleId     = Shader.PropertyToID("_Scale");
        private static readonly int ScrollId    = Shader.PropertyToID("_Scroll");
        private static readonly int SoftnessId  = Shader.PropertyToID("_Softness");

        private readonly Material _material;
        private readonly RenderTexture _target;
        private readonly CommandBuffer _commands = new() { name = "CloudShadowBake" };
        private readonly int _resolution;
        private readonly float _size;
        private readonly float _interval;
        private float2 _origin;
        private double _next;
        private bool _baked;
        private bool _disposed;

        public static CloudShadowMap TryCreate(WeatherViewSettings settings)
        {
            if (settings == null || !(settings.cloudShadowSize > 0f) || settings.cloudShadowResolution < 8) return null;

            Shader shader = Resources.Load<Shader>(BakeShader);
            if (shader == null || !shader.isSupported) return null;
            return new CloudShadowMap(shader, settings);
        }

        private CloudShadowMap(Shader shader, WeatherViewSettings settings)
        {
            _material = new Material(shader) { hideFlags = HideFlags.DontSave };
            _resolution = settings.cloudShadowResolution;
            _size = settings.cloudShadowSize;
            _interval = math.max(settings.cloudShadowInterval, 0.05f);
            _target = new RenderTexture(_resolution, _resolution, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear)
            {
                name = "CloudShadowMap",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                hideFlags = HideFlags.DontSave
            };
            _target.Create();
        }

        public static float2 SnapOrigin(float2 center, float size, int resolution)
        {
            float texel = size / math.max(resolution, 1);
            return math.floor((center - size * 0.5f) / texel) * texel;
        }

        public static float2 ShadowCenter(float2 eye, float eyeY, float planeY, float3 toSun, bool hasSun)
        {
            if (!hasSun || toSun.y <= 0.05f) return eye;
            return eye + toSun.xz / math.max(toSun.y, 0.15f) * math.max(planeY - eyeY, 0f);
        }

        public void Update(Material cloud, WeatherRules rules, float2 eye, float eyeY, float3 toSun, bool hasSun)
        {
            if (_disposed) return;

            double now = SimulationClock.TimeD;
            float2 origin = SnapOrigin(ShadowCenter(eye, eyeY, rules.cloudPlaneY, toSun, hasSun), _size, _resolution);
            if (_baked && now < _next && math.all(origin == _origin)) return;

            _origin = origin;
            _next = now + _interval;
            _baked = true;

            if (cloud != null)
            {
                if (cloud.HasFloat(ScaleId)) _material.SetFloat(ScaleId, cloud.GetFloat(ScaleId));
                if (cloud.HasFloat(ScrollId)) _material.SetFloat(ScrollId, cloud.GetFloat(ScrollId));
                if (cloud.HasFloat(SoftnessId)) _material.SetFloat(SoftnessId, cloud.GetFloat(SoftnessId));
            }
            _material.SetVector(BakeRectId, new Vector4(origin.x, origin.y, _size, 0f));
            _material.SetVector(ParamsId, new Vector4((float)now, rules.fairWeatherCover, rules.cloudClumping, 0f));

            using (BakeMarker.Auto())
            {
                RenderTexture previous = RenderTexture.active;
                _commands.Clear();
                _commands.SetRenderTarget(_target);
                _commands.DrawProcedural(Matrix4x4.identity, _material, 0, MeshTopology.Triangles, 3);
                Graphics.ExecuteCommandBuffer(_commands);
                RenderTexture.active = previous;
            }

            Shader.SetGlobalTexture(MapId, _target);
            Shader.SetGlobalVector(RectId, new Vector4(origin.x, origin.y, _size, 0f));
        }

        public void Clear()
        {
            if (!_baked) return;
            _baked = false;
            Shader.SetGlobalVector(RectId, Vector4.zero);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Shader.SetGlobalVector(RectId, Vector4.zero);
            Shader.SetGlobalTexture(MapId, Texture2D.blackTexture);
            _commands.Release();
            if (_target != null)
            {
                _target.Release();
                UnityEngine.Object.Destroy(_target);
            }
            if (_material != null) UnityEngine.Object.Destroy(_material);
        }
    }
}

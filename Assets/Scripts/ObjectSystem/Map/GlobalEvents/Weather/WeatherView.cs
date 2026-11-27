using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

using TheRavine.Base;

namespace TheRavine.Generator
{
    [Serializable]
    public sealed class WeatherViewSettings
    {
        public bool enabled = true;
        [Tooltip("Material for nearby drops. Edit Drops and Light on this material.")]
        public Material rainMaterial;
        [Tooltip("Material for distant rain shafts. Edit Shafts and Light on this material. Falls back to Rain Material.")]
        public Material shaftMaterial;
        public Material cloudMaterial;
        [Min(0)] public int dropCount = 12000;
        public Vector3 rainBox = new(70f, 45f, 70f);
        [Min(0f)] public float rainThreshold = 0.02f;
        [Min(0f)] public float windSmoothing = 2f;
        [Min(100f)] public float cloudExtent = 5000f;
        [Range(0f, 1f)] public float cloudShadow = 0.55f;
        [Min(0f)] public float skySmoothing = 1.5f;
        [Min(1f)] public float coverRadius = 250f;
        public LightningViewSettings lightning = new();
    }

    public sealed class WeatherView : IDisposable
    {
        private const string RainShader = "Weather/Rain";
        private const string CloudShader = "Weather/CloudLayer";
        private const string ShaftKeyword = "RAIN_SHAFT";
        private const double TimeWrap = 4096d;
        private const float ShaftMinStrength = 0.02f;

        private static readonly int MapId       = Shader.PropertyToID("_WeatherMap");
        private static readonly int RectId      = Shader.PropertyToID("_WeatherRect");
        private static readonly int WindId      = Shader.PropertyToID("_WeatherWind");
        private static readonly int RainTimeId  = Shader.PropertyToID("_RainTime");
        private static readonly int RainBoxId   = Shader.PropertyToID("_RainBox");
        private static readonly int ShaftsId    = Shader.PropertyToID("_RainShafts");
        private static readonly int ShadowId    = Shader.PropertyToID("_WeatherShadow");
        private static readonly int CloudPlaneId = Shader.PropertyToID("_CloudPlane");
        private const float CloudPresence = 0.01f;

        private static readonly ProfilerMarker UploadMarker = new("WeatherView.Upload");
        private static readonly ProfilerMarker RenderMarker = new("WeatherView.Render");

        [StructLayout(LayoutKind.Sequential)]
        private struct ShaftInstance
        {
            public float4 A;
            public float4 B;
        }

        private readonly WeatherEvent _weather;
        private readonly MapGenerator _map;
        private readonly WeatherViewSettings _settings;
        private readonly Material _ownedDrops;
        private readonly Material _ownedShafts;
        private readonly Material _ownedCloud;
        private readonly MaterialPropertyBlock _cloudProps = new();
        private readonly MaterialPropertyBlock _dropProps = new();
        private readonly MaterialPropertyBlock _shaftProps = new();
        private readonly Texture2D _texture;
        private readonly Vector3 _box;
        private RenderParams _dropParams;
        private RenderParams _shaftParams;
        private RenderParams _cloudParams;
        private bool _hasCloud;
        private bool _cloudPresent;

        private NativeArray<half4> _texels;
        private NativeArray<ShaftInstance> _shafts;
        private NativeArray<float2> _windFallback;
        private GraphicsBuffer _shaftBuffer;
        private readonly LightningView _lightning;
        private WindField _windField;
        private int _shaftCount;
        private int _version = -1;
        private float2 _wind;
        private bool _disposed;
#if UNITY_EDITOR
        private int _rainMaterialDirty = -1;
        private int _shaftMaterialDirty = -1;
#endif

        public static WeatherView TryCreate(WeatherEvent weather, MapGenerator map, WeatherViewSettings settings)
        {
            if (weather == null || map == null || settings == null || !SystemInfo.supportsComputeShaders) return null;

            Material ownedDrops = CreateMaterial(settings.rainMaterial, false);
            Material ownedShafts = CreateMaterial(settings.shaftMaterial != null ? settings.shaftMaterial : settings.rainMaterial, true);
            Material drops = ownedDrops;
            Material shafts = ownedShafts;
            if (drops == null || shafts == null)
            {
                RavineLog.Warning("[WeatherView] Rain shader not found, weather is not rendered");
                DestroyMaterial(ownedDrops);
                DestroyMaterial(ownedShafts);
                return null;
            }
            Material cloud = settings.cloudMaterial;
            Material ownedCloud = null;
            if (cloud == null)
            {
                Shader shader = Resources.Load<Shader>(CloudShader);
                if (shader != null && shader.isSupported) ownedCloud = new Material(shader) { hideFlags = HideFlags.DontSave };
                cloud = ownedCloud;
            }
            return new WeatherView(weather, map, settings, drops, shafts, cloud, ownedDrops, ownedShafts, ownedCloud);
        }

        private static Material CreateMaterial(Material source, bool shaft)
        {
            Material material;
            if (source != null)
            {
                material = new Material(source) { hideFlags = HideFlags.DontSave };
            }
            else
            {
                Shader shader = Resources.Load<Shader>(RainShader);
                if (shader == null || !shader.isSupported) return null;
                material = new Material(shader) { hideFlags = HideFlags.DontSave };
            }

            SetRainVariant(material, shaft);
            return material;
        }

        private static void SetRainVariant(Material material, bool shaft)
        {
            if (shaft) material.EnableKeyword(ShaftKeyword);
            else material.DisableKeyword(ShaftKeyword);
        }

#if UNITY_EDITOR
        private static void RefreshMaterial(Material source, Material target, bool shaft, ref int dirtyCount)
        {
            if (source == null) return;
            int current = UnityEditor.EditorUtility.GetDirtyCount(source);
            if (current == dirtyCount) return;

            target.shader = source.shader;
            target.CopyPropertiesFromMaterial(source);
            SetRainVariant(target, shaft);
            dirtyCount = current;
        }
#endif

        private static void DestroyMaterial(Material material)
        {
            if (material != null) UnityEngine.Object.Destroy(material);
        }

        private WeatherView(WeatherEvent weather, MapGenerator map, WeatherViewSettings settings,
            Material drops, Material shafts, Material cloud, Material ownedDrops, Material ownedShafts, Material ownedCloud)
        {
            _weather = weather;
            _map = map;
            _settings = settings;
            _ownedDrops = ownedDrops;
            _ownedShafts = ownedShafts;
            _ownedCloud = ownedCloud;
            _box = new Vector3(math.max(settings.rainBox.x, 1f), math.max(settings.rainBox.y, 1f), math.max(settings.rainBox.z, 1f));

            int side = weather.Side;
            _texture = new Texture2D(side, side, TextureFormat.RGBAHalf, false, true)
            {
                name = "WeatherMap",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            _texels = new NativeArray<half4>(side * side, Allocator.Persistent);

            int cells = math.max(weather.Rules.maxCells, 1);
            _shafts = new NativeArray<ShaftInstance>(cells, Allocator.Persistent);
            _windFallback = new NativeArray<float2>(1, Allocator.Persistent);
            _shaftBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, cells, Marshal.SizeOf<ShaftInstance>());

            _shaftProps.SetBuffer(ShaftsId, _shaftBuffer);
            _dropParams = CreateParams(drops, _dropProps);
            _shaftParams = CreateParams(shafts, _shaftProps);
            if (cloud != null)
            {
                _cloudParams = CreateParams(cloud, _cloudProps);
                _hasCloud = true;
            }
            _lightning = LightningView.TryCreate(weather, settings.lightning);
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
            if (_disposed || _weather.IsDisposed || camera == null) return;

            using (RenderMarker.Auto())
            {
#if UNITY_EDITOR
                RefreshMaterial(_settings.rainMaterial, _ownedDrops, false, ref _rainMaterialDirty);
                RefreshMaterial(_settings.shaftMaterial != null ? _settings.shaftMaterial : _settings.rainMaterial,
                    _ownedShafts, true, ref _shaftMaterialDirty);
#endif
                float3 eye = camera.transform.position;
                Shader.SetGlobalVector(ShadowId, new Vector4(_settings.cloudShadow, _weather.Rules.cloudPlaneY, 0f, 0f));
                if (_weather.Version != _version)
                {
                    _version = _weather.Version;
                    using (UploadMarker.Auto())
                    {
                        UploadMap();
                        RebuildShafts();
                    }
                }

                float dt = SimulationClock.DeltaTime;
                UpdateWind(eye.xz, dt);
                UpdateSky(eye.xz, dt);
                float time = (float)(SimulationClock.TimeD % TimeWrap);
                _dropProps.SetFloat(RainTimeId, time);
                _shaftProps.SetFloat(RainTimeId, time);

                DrawClouds(camera, eye, (float)SimulationClock.TimeD);
                DrawDrops(eye);
                DrawShafts();
            }

            _lightning?.Render(camera);
        }

        private void UploadMap()
        {
            var map = _weather.Map;
            float cloud = 0f;
            for (int i = 0; i < _texels.Length; i++)
            {
                float4 value = map[i];
                _texels[i] = new half4(value);
                cloud = math.max(cloud, value.y);
            }
            _cloudPresent = cloud > CloudPresence;
            _texture.SetPixelData(_texels, 0);
            _texture.Apply(false);

            int2 origin = _weather.Origin;
            float texel = _weather.TexelSize;
            Shader.SetGlobalTexture(MapId, _texture);
            Shader.SetGlobalVector(RectId, new Vector4(origin.x * texel, origin.y * texel, texel, _weather.Side));
        }

        private void RebuildShafts()
        {
            WeatherRules rules = _weather.Rules;
            float3 min = new(float.MaxValue);
            float3 max = new(float.MinValue);
            int n = 0;

            for (int i = 0; i < _weather.CellCount && n < _shafts.Length; i++)
            {
                float envelope = _weather.CellEnvelope(i);
                float strength = _weather.CellMoisture(i) * envelope;
                if (strength < ShaftMinStrength) continue;

                float2 at = _weather.CellPosition(i);
                float radius = _weather.CellRadius(i);
                if (!_map.TrySampleHeightBilinear(at.x, at.y, out float ground)) continue;
                float top = rules.cloudPlaneY;
                if (ground >= top) continue;
                float evaporation = _weather.CellEvaporation(i);
                float bottom = ground + evaporation * (top - ground) * 0.92f;
                float seed = (math.hash(new int2((int)at.x, (int)at.y)) & 0xFFFF) * (1f / 65535f);

                _shafts[n++] = new ShaftInstance
                {
                    A = new float4(at.x, at.y, radius, strength),
                    B = new float4(bottom, top, seed, math.lerp(0.04f, 0.6f, evaporation))
                };
                min = math.min(min, new float3(at.x - radius, ground, at.y - radius));
                max = math.max(max, new float3(at.x + radius, top, at.y + radius));
            }

            _shaftCount = n;
            if (n == 0) return;

            _shaftBuffer.SetData(_shafts, 0, 0, n);
            _shaftParams.worldBounds = new Bounds((min + max) * 0.5f, max - min);
        }

        private void DrawClouds(Camera camera, float3 eye, float time)
        {
            var rules = _weather.Rules;
            if (!_hasCloud || (!_cloudPresent && rules.fairWeatherCover <= 0f)) return;

            float extent = math.min(_settings.cloudExtent, camera.farClipPlane * 0.9f);
            float y = rules.cloudPlaneY;
            if (math.abs(y - eye.y) >= camera.farClipPlane) return;
            _cloudProps.SetVector(CloudPlaneId, new Vector4(extent, y, time, rules.fairWeatherCover));
            _cloudParams.worldBounds = new Bounds(new Vector3(eye.x, y, eye.z), new Vector3(extent * 2f, 16f, extent * 2f));
            Graphics.RenderPrimitives(in _cloudParams, MeshTopology.Triangles, 6, 1);
        }

        private void UpdateSky(float2 at, float dt)
        {
            float r = _settings.coverRadius;
            float cover = _weather.CloudCoverAt(at)
                        + _weather.CloudCoverAt(at + new float2(r, 0f))
                        + _weather.CloudCoverAt(at + new float2(-r, 0f))
                        + _weather.CloudCoverAt(at + new float2(0f, r))
                        + _weather.CloudCoverAt(at + new float2(0f, -r));
            float rain = _weather.RainAt(at)
                       + _weather.RainAt(at + new float2(r, 0f))
                       + _weather.RainAt(at + new float2(-r, 0f))
                       + _weather.RainAt(at + new float2(0f, r))
                       + _weather.RainAt(at + new float2(0f, -r));

            float k = 1f - math.exp(-dt * math.max(_settings.skySmoothing, 0f));
            SkyCover.Value = math.lerp(SkyCover.Value, cover * 0.2f, k);
            SkyCover.Rain = math.lerp(SkyCover.Rain, rain * 0.2f, k);
        }

        private void DrawDrops(float3 eye)
        {
            int count = _settings.dropCount;
            if (count <= 0 || eye.y - _box.y * 0.5f >= _weather.Rules.cloudPlaneY || !RainNear(eye)) return;

            _dropProps.SetVector(RainBoxId, new Vector4(_box.x, _box.y, _box.z, 0f));
            _dropParams.worldBounds = new Bounds(eye, new Vector3(_box.x, _box.y, _box.z) * 2f);
            Graphics.RenderPrimitives(in _dropParams, MeshTopology.Triangles, 6, count);
        }

        private void DrawShafts()
        {
            if (_shaftCount == 0) return;
            Graphics.RenderPrimitives(in _shaftParams, MeshTopology.Triangles, 6, _shaftCount);
        }

        private bool RainNear(float3 eye)
        {
            float threshold = _settings.rainThreshold;
            float2 reach = new float2(_box.x, _box.z) * 0.5f;
            float2 p = eye.xz;
            return _weather.RainAt(p) > threshold
                || _weather.RainAt(p + new float2(reach.x, reach.y)) > threshold
                || _weather.RainAt(p + new float2(-reach.x, reach.y)) > threshold
                || _weather.RainAt(p + new float2(reach.x, -reach.y)) > threshold
                || _weather.RainAt(p - reach) > threshold;
        }

        private void UpdateWind(float2 at, float dt)
        {
            float2 target = _weather.WindAt(at);
            if (_windField == null) ServiceLocator.Services.TryGet(out _windField);
            if (_windField != null) target += _windField.GetSampler(SimulationClock.Time, _windFallback).Sample(at);

            _wind = math.lerp(_wind, target, 1f - math.exp(-dt * math.max(_settings.windSmoothing, 0f)));
            Shader.SetGlobalVector(WindId, new Vector4(_wind.x, _wind.y, 0f, 0f));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Shader.SetGlobalVector(RectId, Vector4.zero);
            Shader.SetGlobalVector(WindId, Vector4.zero);
            Shader.SetGlobalVector(ShadowId, Vector4.zero);
            SkyCover.Clear();
            Shader.SetGlobalTexture(MapId, Texture2D.blackTexture);

            _lightning?.Dispose();
            _shaftBuffer?.Release();
            _shaftBuffer = null;
            if (_texels.IsCreated) _texels.Dispose();
            if (_shafts.IsCreated) _shafts.Dispose();
            if (_windFallback.IsCreated) _windFallback.Dispose();
            if (_texture != null) UnityEngine.Object.Destroy(_texture);
            DestroyMaterial(_ownedDrops);
            DestroyMaterial(_ownedShafts);
            DestroyMaterial(_ownedCloud);
            _windField = null;
        }
    }
}

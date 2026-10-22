using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace TheRavine.Generator
{
    public struct WindParams
    {
        public float2 dir;
        public float strength, pulse, pulseMagnitude, turbulence;
        public float time;
    }

    public struct WindSampler
    {
        public WindParams Params;
        [ReadOnly] public NativeArray<float2> Map;
        public int2 MapSize;
        public float MapScale, PhaseScale;
        public float2 MapVelocity;

        public float2 Mean => Params.dir * (Params.strength * (1f + Params.pulseMagnitude * math.sin(Params.time * Params.pulse)));

        public float2 Sample(float2 xz)
        {
            float2 wind = Mean;
            if (MapSize.x <= 1 || Params.turbulence == 0f) return wind;

            float2 uv = xz * (MapScale + PhaseScale) + MapVelocity * Params.time;
            float2 g = uv * (float2)MapSize - 0.5f;
            float2 f = math.floor(g);
            float2 t = g - f;
            int2 i0 = (int2)f % MapSize;
            i0 += math.select(int2.zero, MapSize, i0 < 0);
            int2 i1 = math.select(i0 + 1, int2.zero, i0 + 1 >= MapSize);

            int r0 = i0.y * MapSize.x, r1 = i1.y * MapSize.x;
            float2 a = math.lerp(Map[r0 + i0.x], Map[r0 + i1.x], t.x);
            float2 b = math.lerp(Map[r1 + i0.x], Map[r1 + i1.x], t.x);
            return wind + math.lerp(a, b, t.y) * Params.turbulence;
        }
    }

    [RequireComponent(typeof(WindZone))]
    public sealed class WindField : MonoBehaviour, IDisposable
    {
        private static readonly int WindMapId = Shader.PropertyToID("_WindMap");
        private static readonly int WindMapScaleId = Shader.PropertyToID("_WindMapScale");
        private static readonly int WindVelocityId = Shader.PropertyToID("_WindVelocity");

        [SerializeField] private WindZone windZone;
        [SerializeField] private MtreeWind mtreeWind;
        [SerializeField] private Material grassMaterial;
        [SerializeField] private float phaseScale = 0.1f;

        private NativeArray<float2> _map;
        private int2 _mapSize;
        private float _mapScale;
        private float2 _mapVelocity;
        private WindParams _cached;
        private int _cachedFrame = -1;

        public bool HasMap => _map.IsCreated;

        private void Awake()
        {
            if (windZone == null) windZone = GetComponent<WindZone>();
            if (mtreeWind == null) mtreeWind = GetComponent<MtreeWind>();
            LoadMap();
            ServiceLocator.Register(this);
        }

        public WindParams GetParams(float time)
        {
            int frame = Time.frameCount;
            if (_cachedFrame != frame)
            {
                Refresh();
                _cachedFrame = frame;
            }
            WindParams p = _cached;
            p.time = time;
            return p;
        }

        public WindSampler GetSampler(float time, NativeArray<float2> fallbackMap)
        {
            bool has = _map.IsCreated;
            return new WindSampler
            {
                Params = GetParams(time),
                Map = has ? _map : fallbackMap,
                MapSize = has ? _mapSize : new int2(1, 1),
                MapScale = _mapScale,
                PhaseScale = phaseScale,
                MapVelocity = _mapVelocity
            };
        }

        public static WindSampler Constant(float2 dir, float strength, float time, NativeArray<float2> fallbackMap) => new()
        {
            Params = new WindParams { dir = math.normalizesafe(dir), strength = strength, time = time },
            Map = fallbackMap,
            MapSize = new int2(1, 1)
        };

        private void Refresh()
        {
            float strength = 0f, yaw = 0f, pulse = 0f, pulseMagnitude = 0f, turbulence = 0f;

            if (windZone != null)
            {
                strength = windZone.windMain;
                yaw = windZone.transform.localEulerAngles.y;
                pulse = windZone.windPulseFrequency;
                pulseMagnitude = windZone.windPulseMagnitude;
                turbulence = windZone.windTurbulence;
            }

            if (mtreeWind != null && mtreeWind.isActiveAndEnabled)
            {
                strength += mtreeWind.windStrength;
                yaw += mtreeWind.windDirection;
                pulse += mtreeWind.windPulse;
                turbulence += mtreeWind.windTurbulence;
            }

            float r = math.radians(yaw);
            _cached = new WindParams
            {
                dir = new float2(math.sin(r), math.cos(r)),
                strength = strength,
                pulse = pulse * (2f * math.PI),
                pulseMagnitude = pulseMagnitude,
                turbulence = turbulence
            };
        }

        private void LoadMap()
        {
            if (grassMaterial == null || !grassMaterial.HasTexture(WindMapId)) return;
            Texture tex = grassMaterial.GetTexture(WindMapId);
            if (tex == null) return;

            _mapScale = grassMaterial.GetFloat(WindMapScaleId);
            Vector4 v = grassMaterial.GetVector(WindVelocityId);
            _mapVelocity = new float2(v.x, v.y);

            int w = tex.width, h = tex.height;
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(tex, rt);

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var cpu = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            cpu.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            NativeArray<Color32> px = cpu.GetPixelData<Color32>(0);
            _map = new NativeArray<float2>(w * h, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            const float k = 2f / 255f;
            for (int i = 0; i < _map.Length; i++)
            {
                Color32 c = px[i];
                _map[i] = new float2(c.r * k - 1f, c.g * k - 1f);
            }
            _mapSize = new int2(w, h);
            Destroy(cpu);
        }

        private void OnDestroy() => Dispose();

        public void Dispose()
        {
            if (_map.IsCreated) _map.Dispose();
        }
    }
}

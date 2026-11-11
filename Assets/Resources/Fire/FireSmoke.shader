Shader "The Ravine/Effects/Fire Smoke"
{
    Properties
    {
        [Header(Smoke)]
        _SmokeColorYoung ("Young", Color) = (0.13, 0.12, 0.11, 1)
        _SmokeColorOld ("Old", Color) = (0.46, 0.45, 0.44, 1)
        _SmokeDensity ("Density", Range(0, 1)) = 0.55
        _SmokeStart ("Start Height", Float) = 1.6
        _SmokeRise ("Rise", Float) = 9
        _SmokeLife ("Life", Float) = 4.5
        _SmokeSizeMin ("Size Min", Float) = 1.0
        _SmokeSizeMax ("Size Max", Float) = 3.6
        _SmokeDrift ("Wind Drift", Float) = 1.2
        _SmokeLinger ("Linger Seconds", Float) = 6
        _UnderGlowColor ("Under Glow", Color) = (1.0, 0.45, 0.15, 1)
        _UnderGlowStrength ("Under Glow Strength", Float) = 0.8

        [Header(Scorch)]
        _ScorchColor ("Char", Color) = (0.05, 0.045, 0.04, 1)
        _AshColor ("Ash", Color) = (0.32, 0.3, 0.28, 1)
        _ScorchSize ("Size", Float) = 2.8
        _ScorchOpacity ("Opacity", Range(0, 1)) = 0.85
        _ScorchHold ("Hold Seconds", Float) = 20
        _ScorchSeconds ("Fade Out Seconds", Float) = 40
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "FireSmoke"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Assets/Shaders/Effects/FireCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _SmokeColorYoung;
                float4 _SmokeColorOld;
                float  _SmokeDensity;
                float  _SmokeStart;
                float  _SmokeRise;
                float  _SmokeLife;
                float  _SmokeSizeMin;
                float  _SmokeSizeMax;
                float  _SmokeDrift;
                float  _SmokeLinger;
                float4 _UnderGlowColor;
                float  _UnderGlowStrength;
                float4 _ScorchColor;
                float4 _AshColor;
                float  _ScorchSize;
                float  _ScorchOpacity;
                float  _ScorchHold;
                float  _ScorchSeconds;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                nointerpolation float4 data : TEXCOORD1;
                nointerpolation float glow : TEXCOORD2;
                half3 light : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            Varyings Culled()
            {
                Varyings o = (Varyings)0;
                o.positionCS = FireCulled();
                return o;
            }

            Varyings vert(uint vid : SV_VertexID, uint iid : SV_InstanceID)
            {
                uint quad = vid / 6;
                float2 corner = FireCorners[vid % 6];
                FireState s = FireLoad(iid);

                float3 positionWS;
                float4 data;
                float glow = 0.0;

                if (quad == 0)
                {
                    float opacity = s.ended < 0.0
                        ? saturate(s.age / max(_FireBurnSeconds, 0.01))
                        : lerp(0.4, 1.0, s.burned) * (1.0 - smoothstep(_ScorchHold, max(_ScorchSeconds, _ScorchHold + 0.01), s.ended));
                    opacity *= _ScorchOpacity;
                    if (opacity <= 0.002) return Culled();

                    float3 t, b;
                    FireGroundFrame(s.normal, t, b);
                    float size = _ScorchSize * (0.85 + 0.3 * FireRand(s.seed, 3));
                    float2 offset = FireRotate((corner - 0.5) * size, FireRand(s.seed, 5) * 6.2831853);
                    positionWS = s.base + t * offset.x + b * offset.y + s.normal * 0.08;
                    data = float4(0.0, opacity, s.seed * 13.0, 0.0);
                }
                else
                {
                    uint puff = quad - 1;
                    float density = s.ended < 0.0
                        ? saturate(s.heat) * smoothstep(0.0, 1.5, s.age)
                        : exp(-s.ended / max(_SmokeLinger, 0.01)) * lerp(0.35, 1.0, s.burned);
                    if (density <= 0.01 || FireRand(s.seed, 71 + puff) > _FireKeep) return Culled();

                    float life  = _SmokeLife * (0.75 + 0.5 * FireRand(s.seed, 73 + puff));
                    float cycle = _FireTime / life + FireRand(s.seed, 79 + puff);
                    float phase = frac(cycle);
                    uint  salt  = (uint)floor(cycle) * 8 + puff;

                    float angle = FireRand(s.seed, 83 + salt) * 6.2831853;
                    float3 start = s.base + float3(cos(angle), 0.0, sin(angle)) * 0.5 + float3(0.0, _SmokeStart, 0.0);
                    float  rise  = _SmokeRise * phase * (0.8 + 0.4 * FireRand(s.seed, 89 + salt));
                    float3 curl  = float3(sin(phase * 3.0 + angle), 0.0, cos(phase * 2.3 + angle)) * (0.6 * phase);
                    float3 drift = float3(_FireWind.x, 0.0, _FireWind.y) * (_SmokeDrift * phase * life);
                    float3 centre = start + float3(0.0, rise, 0.0) + curl + drift;

                    float thinning = lerp(1.0, rsqrt(max(_FireKeep, 0.05)), 0.6);
                    float size = lerp(_SmokeSizeMin, _SmokeSizeMax, sqrt(phase)) * thinning;
                    float spin = FireRand(s.seed, 97 + salt) * 6.2831853 + phase * (FireRand(s.seed, 101 + salt) - 0.5) * 1.5;
                    float2 offset = FireRotate((corner - 0.5) * size, spin);
                    positionWS = centre + FireCameraRight() * offset.x + FireCameraUp() * offset.y;

                    float envelope = smoothstep(0.0, 0.15, phase) * (1.0 - smoothstep(0.45, 1.0, phase));
                    glow = s.ended < 0.0 ? saturate(s.heat) * pow(1.0 - phase, 3.0) : 0.0;
                    data = float4(1.0, envelope * density * _SmokeDensity, frac(s.seed * 5.3 + salt * 0.137) * 61.0, phase);
                }

                Varyings o;
                positionWS = BendWorld(positionWS);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = corner;
                o.data = data;
                o.glow = glow;
                o.light = FireAmbient();
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 d = i.uv * 2.0 - 1.0;
                half3 rgb;
                half alpha;

                if (i.data.x < 0.5)
                {
                    float n = FireFbm(i.uv * 3.1 + i.data.z);
                    float edge = length(d) + (n - 0.5) * 0.55;
                    float mask = 1.0 - smoothstep(0.55, 0.95, edge);
                    float ash = (1.0 - smoothstep(0.0, 0.35, edge)) * n;
                    rgb = lerp(_ScorchColor.rgb, _AshColor.rgb, ash) * i.light;
                    alpha = mask * i.data.y;
                }
                else
                {
                    float seed = i.data.z;
                    float phase = i.data.w;

                    float n = FireFbm(i.uv * 2.4 + float2(seed, seed * 0.37 - phase * 0.6));
                    float m = saturate(1.0 - dot(d, d));
                    m *= m;
                    float shape = saturate(m * (0.55 + 0.9 * n) - 0.15);

                    rgb = lerp(_SmokeColorYoung.rgb, _SmokeColorOld.rgb, smoothstep(0.0, 0.8, phase)) * i.light
                        + _UnderGlowColor.rgb * (_UnderGlowStrength * i.glow * (1.0 - i.uv.y));
                    alpha = shape * i.data.y;
                }

                clip(alpha - 0.002);
                rgb = MixFog(rgb, i.fogFactor);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

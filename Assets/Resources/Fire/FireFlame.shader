Shader "The Ravine/Effects/Fire Flame"
{
    Properties
    {
        [Header(Flame)]
        _FlameHeight ("Height", Float) = 2.2
        _FlameWidth ("Width", Float) = 1.3
        _FlameSpread ("Tongue Spread", Float) = 0.55
        _FlameSpeed ("Scroll Speed", Float) = 2.4
        _WindLean ("Wind Lean", Float) = 0.45
        _MaxLean ("Max Lean", Float) = 1.2

        [Header(Palette)]
        _ColorDeep ("Deep", Color) = (0.62, 0.08, 0.02, 1)
        _ColorMid ("Mid", Color) = (1.0, 0.36, 0.05, 1)
        _ColorHot ("Hot", Color) = (1.0, 0.74, 0.22, 1)
        _ColorCore ("Core", Color) = (1.0, 0.95, 0.78, 1)
        _BandSoftness ("Band Softness", Range(0.005, 0.3)) = 0.05
        _Emission ("Emission", Float) = 2.2
        _Additive ("Additive", Range(0, 1)) = 0.6

        [Header(Ground Glow)]
        _GlowColor ("Color", Color) = (1.0, 0.42, 0.12, 1)
        _GlowSize ("Size", Float) = 3.4
        _GlowStrength ("Strength", Float) = 0.55
        _SmoulderSeconds ("Smoulder Seconds", Float) = 3

        [Header(Embers)]
        _EmberColor ("Color", Color) = (1.0, 0.6, 0.18, 1)
        _EmberSize ("Size", Float) = 0.11
        _EmberRise ("Rise", Float) = 4.5
        _EmberLife ("Life", Float) = 1.6
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "FireFlame"
            Tags { "LightMode" = "UniversalForward" }

            Blend One OneMinusSrcAlpha
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
                float  _FlameHeight;
                float  _FlameWidth;
                float  _FlameSpread;
                float  _FlameSpeed;
                float  _WindLean;
                float  _MaxLean;
                float4 _ColorDeep;
                float4 _ColorMid;
                float4 _ColorHot;
                float4 _ColorCore;
                float  _BandSoftness;
                float  _Emission;
                float  _Additive;
                float4 _GlowColor;
                float  _GlowSize;
                float  _GlowStrength;
                float  _SmoulderSeconds;
                float4 _EmberColor;
                float  _EmberSize;
                float  _EmberRise;
                float  _EmberLife;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                nointerpolation float4 data : TEXCOORD1;
                float fogFactor : TEXCOORD2;
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

                if (quad == 0)
                {
                    float glow = max(s.heat, s.burned * exp(-s.ended / max(_SmoulderSeconds, 0.01)) * 0.6);
                    if (glow <= 0.002) return Culled();

                    float3 t, b;
                    FireGroundFrame(s.normal, t, b);
                    float2 offset = (corner - 0.5) * (_GlowSize * (0.8 + 0.25 * saturate(glow)));
                    positionWS = s.base + t * offset.x + b * offset.y + s.normal * 0.15;
                    data = float4(0.0, glow * _GlowStrength, s.seed * 37.0, 0.0);
                }
                else if (quad <= 3)
                {
                    if (s.heat <= 0.002) return Culled();

                    uint tongue = quad - 1;
                    bool primary = tongue == 0;
                    float angle  = FireRand(s.seed, 11 + tongue) * 6.2831853;
                    float radius = primary ? 0.0 : _FlameSpread * (0.55 + 0.45 * FireRand(s.seed, 17 + tongue));
                    float3 centre = s.base + float3(cos(angle), 0.0, sin(angle)) * radius;

                    float scale = primary ? 1.0 : lerp(0.62, 0.85, FireRand(s.seed, 23 + tongue));
                    float flicker = 1.0
                        + 0.14 * sin(_FireTime * (7.0 + 3.0 * FireRand(s.seed, 29 + tongue)) + s.seed * 40.0)
                        + 0.08 * sin(_FireTime * 17.3 + tongue * 2.1);
                    float heat = saturate(s.heat);
                    float height = _FlameHeight * scale * flicker * lerp(0.25, 1.0, heat) * max(s.heat, 1.0);
                    float width  = _FlameWidth * scale * lerp(0.55, 1.0, heat);

                    float2 lean = _FireWind.xy * _WindLean;
                    float leanLength = length(lean);
                    lean *= min(1.0, _MaxLean / max(leanLength, 1e-4));

                    float3 right = FireCameraRightXZ();
                    positionWS = centre
                        + right * ((corner.x - 0.5) * width)
                        + float3(0.0, corner.y * height - 0.1, 0.0)
                        + float3(lean.x, 0.0, lean.y) * (height * corner.y * corner.y);
                    data = float4(1.0, heat, s.seed * 31.0 + tongue * 7.3, 0.0);
                }
                else
                {
                    uint ember = quad - 4;
                    float emit = s.ended < 0.0
                        ? saturate(s.heat)
                        : s.burned * exp(-s.ended / max(_SmoulderSeconds * 0.5, 0.01));
                    if (emit <= 0.01 || FireRand(s.seed, 41 + ember) > _FireKeep) return Culled();

                    float life  = _EmberLife * (0.7 + 0.6 * FireRand(s.seed, 47 + ember));
                    float cycle = _FireTime / life + FireRand(s.seed, 53 + ember);
                    float phase = frac(cycle);
                    uint  salt  = (uint)floor(cycle) * 16 + ember;

                    float angle  = FireRand(s.seed, 59 + salt) * 6.2831853;
                    float radius = 0.6 * sqrt(FireRand(s.seed, 61 + salt));
                    float3 start = s.base + float3(cos(angle) * radius, 0.4, sin(angle) * radius);
                    float  rise  = _EmberRise * phase * (0.6 + 0.4 * FireRand(s.seed, 67 + salt));
                    float3 swirl = float3(sin(phase * 9.0 + angle * 3.0), 0.0, cos(phase * 7.0 + angle * 2.0)) * (0.25 * phase);
                    float3 drift = float3(_FireWind.x, 0.0, _FireWind.y) * (phase * life * 0.8);
                    float3 centre = start + float3(0.0, rise, 0.0) + swirl + drift;

                    float size = _EmberSize * (1.0 - phase * 0.6);
                    positionWS = centre
                        + FireCameraRight() * ((corner.x - 0.5) * size)
                        + FireCameraUp() * ((corner.y - 0.5) * size);

                    float fade = (1.0 - phase) * (1.0 - phase);
                    float twinkle = 0.6 + 0.4 * sin(phase * 40.0 + ember * 1.7);
                    data = float4(2.0, emit * fade * twinkle, 0.0, 0.0);
                }

                Varyings o;
                positionWS = BendWorld(positionWS);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = corner;
                o.data = data;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half3 Ramp(float heat)
            {
                float s = _BandSoftness;
                half3 c = _ColorDeep.rgb;
                c = lerp(c, _ColorMid.rgb,  smoothstep(0.32 - s, 0.32 + s, heat));
                c = lerp(c, _ColorHot.rgb,  smoothstep(0.56 - s, 0.56 + s, heat));
                c = lerp(c, _ColorCore.rgb, smoothstep(0.80 - s, 0.80 + s, heat));
                return c;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float kind = i.data.x;
                float bright = i.data.y;
                half3 rgb;
                half alpha;

                if (kind < 0.5)
                {
                    float2 d = i.uv * 2.0 - 1.0;
                    float r2 = dot(d, d);
                    float g = exp(-r2 * 3.2) * saturate(1.0 - r2);
                    float flicker = 0.85 + 0.15 * sin(_FireTime * 9.0 + i.data.z);
                    rgb = _GlowColor.rgb * (g * bright * flicker);
                    alpha = 0.0;
                }
                else if (kind < 1.5)
                {
                    float2 uv = i.uv;
                    float t = frac(_FireTime * _FlameSpeed * (1.0 / 1024.0)) * 1024.0;
                    float n1 = FireFbm(float2(uv.x * 2.2 + i.data.z, uv.y * 2.6 - t));
                    float n2 = FireValueNoise(float2(uv.x * 5.0 - i.data.z, uv.y * 5.5 - t * 1.7));

                    float x = (uv.x - 0.5) * 2.0 + (n1 - 0.5) * 0.7 * uv.y;
                    float profile = sqrt(saturate(uv.y * 4.0)) * pow(saturate(1.0 - uv.y), 0.9);
                    float body = saturate(1.0 - abs(x) / max(profile, 1e-3));

                    float heat = body * (1.25 - uv.y * 0.85) - n2 * 0.35 * uv.y;
                    heat = saturate(heat * lerp(0.55, 1.0, bright));

                    alpha = smoothstep(0.06, 0.16, heat) * saturate(bright * 3.0);
                    clip(alpha - 0.003);
                    rgb = Ramp(heat) * (_Emission * alpha);
                    alpha *= 1.0 - _Additive;
                }
                else
                {
                    float2 d = i.uv * 2.0 - 1.0;
                    float g = saturate(1.0 - dot(d, d));
                    g *= g;
                    rgb = _EmberColor.rgb * (_Emission * 1.5 * g * bright);
                    alpha = g * bright * (1.0 - _Additive) * 0.5;
                }

                rgb = MixFogColor(rgb, half3(0.0, 0.0, 0.0), i.fogFactor);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

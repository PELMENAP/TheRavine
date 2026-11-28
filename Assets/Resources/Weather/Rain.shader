Shader "The Ravine/Weather/Rain"
{
    Properties
    {
        [Header(Drops)]
        _RainColor ("Color", Color) = (0.74, 0.8, 0.88, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.38
        _DropLength ("Length", Float) = 1.5
        _DropWidth ("Width", Float) = 0.04
        _FallSpeed ("Fall Speed", Float) = 24
        _WindLean ("Wind Lean", Float) = 10
        _Density ("Density", Float) = 1.4

        [Header(Shafts)]
        _ShaftWidth ("Width Per Radius", Float) = 1.7
        _ShaftAlpha ("Alpha", Range(0, 1)) = 0.5
        _ShaftGain ("Gain", Float) = 1.8
        _ShaftScroll ("Scroll Speed", Float) = 0.9
        _ShaftStreaks ("Streaks", Float) = 22

        [Header(Light)]
        _FlashGain ("Flash Gain", Float) = 4
        _AmbientGain ("Ambient Gain", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+1"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Rain"
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
            #pragma multi_compile_local _ RAIN_SHAFT

            #include "Assets/Shaders/Effects/WeatherCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _RainColor;
                float  _Alpha;
                float  _DropLength;
                float  _DropWidth;
                float  _FallSpeed;
                float  _WindLean;
                float  _Density;
                float  _ShaftWidth;
                float  _ShaftAlpha;
                float  _ShaftGain;
                float  _ShaftScroll;
                float  _ShaftStreaks;
                float  _FlashGain;
                float  _AmbientGain;
            CBUFFER_END

            float  _RainTime;
            float4 _RainBox;

#if defined(RAIN_SHAFT)
            struct ShaftCell
            {
                float4 a;
                float4 b;
            };

            StructuredBuffer<ShaftCell> _RainShafts;
#endif

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
                o.positionCS = WeatherCulled();
                return o;
            }

#if defined(RAIN_SHAFT)
            Varyings vert(uint vid : SV_VertexID, uint iid : SV_InstanceID)
            {
                ShaftCell c = _RainShafts[iid];
                float2 corner = WeatherCorners[vid];

                float3 center = float3(c.a.x, 0.0, c.a.y);
                float3 toCamera = _WorldSpaceCameraPos - center;
                toCamera.y = 0.0;
                float distanceXZ = length(toCamera);
                float3 right = normalize(float3(toCamera.z, 0.0, -toCamera.x) + float3(1e-5, 0.0, 0.0));

                float radius = max(c.a.z, 1.0);
                float facing = saturate((distanceXZ - radius * 0.35) / (radius * 0.5));
                float strength = saturate(c.a.w * _ShaftGain) * facing * _ShaftAlpha;
                if (strength <= 0.002) return Culled();

                float3 positionWS = center + right * ((corner.x - 0.5) * radius * _ShaftWidth);
                positionWS.y = lerp(c.b.x, c.b.y, corner.y);

                Varyings o;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = corner;
                o.data = float4(strength, c.b.z, c.b.w, 0.0);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float seed = i.data.y * 31.0;
                float streak = WeatherFbm(float2(i.uv.x * _ShaftStreaks + seed, i.uv.y * 2.2 + _RainTime * _ShaftScroll));
                float body = smoothstep(0.0, 0.3, i.uv.x) * smoothstep(1.0, 0.7, i.uv.x);
                float top = smoothstep(1.0, 0.75, i.uv.y);
                float bottom = smoothstep(0.0, max(i.data.z, 0.03), i.uv.y);
                float alpha = i.data.x * body * top * bottom * lerp(0.25, 1.0, streak);
                if (alpha <= 0.002) discard;

                half3 rgb = _RainColor.rgb * (WeatherAmbient() * _AmbientGain + _FlashGain * _LightningFlash);
                rgb = MixFog(rgb, i.fogFactor);
                return half4(rgb, alpha);
            }
#else
            Varyings vert(uint vid : SV_VertexID, uint iid : SV_InstanceID)
            {
                float2 corner = WeatherCorners[vid];

                float3 seed = float3(WeatherRand(iid, 1), WeatherRand(iid, 2), WeatherRand(iid, 3));
                float keep = WeatherRand(iid, 4);
                float speed = _FallSpeed * (0.85 + 0.3 * seed.z);
                float2 drift = _WeatherWind.xy * _WindLean;
                float3 velocity = float3(drift.x, -speed, drift.y);

                float3 box = _RainBox.xyz;
                float3 camera = _WorldSpaceCameraPos;
                float3 p = seed * box + velocity * _RainTime;
                float3 rel = (frac((p - camera) / box + 0.5) - 0.5) * box;
                float3 positionWS = camera + rel;

                if (positionWS.y >= _WeatherShadow.y) return Culled();
                float rain = SampleWeather(positionWS).rain;
                if (keep > rain * _Density) return Culled();

                float2 spread = rel.xz / (box.xz * 0.5);
                float3 edge = float3(spread, rel.y / (box.y * 0.5));
                float fade = (1.0 - smoothstep(0.55, 1.0, length(spread))) * (1.0 - smoothstep(0.7, 1.0, abs(edge.z)));
                if (fade <= 0.002) return Culled();

                float3 direction = normalize(velocity);
                float3 viewHead = TransformWorldToView(positionWS);
                float3 viewTail = TransformWorldToView(positionWS - direction * _DropLength);
                float3 axis = viewTail - viewHead;
                float2 side2 = float2(axis.y, -axis.x);
                float3 side = float3(side2 / max(length(side2), 1e-4), 0.0);

                float3 viewPos = viewHead + axis * corner.y + side * ((corner.x - 0.5) * _DropWidth);

                Varyings o;
                o.positionCS = TransformWViewToHClip(viewPos);
                o.uv = corner;
                o.data = float4(fade * _Alpha * saturate(rain * 1.5 + 0.35), 0.0, 0.0, 0.0);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float across = 1.0 - abs(i.uv.x * 2.0 - 1.0);
                float along = sin(i.uv.y * 3.14159265);
                float alpha = i.data.x * across * along;
                if (alpha <= 0.002) discard;

                half3 rgb = _RainColor.rgb * (WeatherAmbient() * _AmbientGain + _FlashGain * _LightningFlash);
                rgb = MixFog(rgb, i.fogFactor);
                return half4(rgb, alpha);
            }
#endif
            ENDHLSL
        }
    }
}

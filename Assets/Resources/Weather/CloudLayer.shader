Shader "The Ravine/Weather/Cloud Layer"
{
    Properties
    {
        [Header(Shape)]
        _Scale ("Fine Detail Scale", Float) = 0.0016
        _Scroll ("Drift Speed", Float) = 4
        _Softness ("Edge Softness", Range(0.02, 0.5)) = 0.14
        _Opacity ("Opacity", Range(0, 1)) = 0.92
        _LowCloudCoreOpacity ("Low Cloud Core Opacity", Range(0, 1)) = 1

        [Header(Light)]
        _CloudColor ("Color", Color) = (0.92, 0.94, 0.98, 1)
        _StormColor ("Storm Color", Color) = (0.2, 0.22, 0.28, 1)
        _StormDarken ("Storm Darkening", Range(0, 1)) = 0.9
        _Sun ("Sun Gain", Float) = 0.85
        _Ambient ("Ambient Gain", Float) = 0.9
        _StepLength ("Parallax Step", Float) = 90
        _Absorb ("Absorption", Float) = 0.55
        _FlashColor ("Flash Color", Color) = (0.62, 0.7, 1.0, 1)
        _FlashGain ("Flash Gain", Float) = 3.2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+5"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "CloudLayer"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Assets/Shaders/Effects/WeatherCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float  _Scale;
                float  _Scroll;
                float  _Softness;
                float  _Opacity;
                float  _LowCloudCoreOpacity;
                float4 _CloudColor;
                float4 _StormColor;
                float  _StormDarken;
                float  _Sun;
                float  _Ambient;
                float  _StepLength;
                float  _Absorb;
                float4 _FlashColor;
                float  _FlashGain;
            CBUFFER_END

            float4 _CloudPlane;
            float4 _CloudLayer;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(uint vid : SV_VertexID)
            {
                float2 corner = WeatherCorners[vid];
                float3 camera = _WorldSpaceCameraPos;

                Varyings o;
                o.positionWS = float3(camera.x + (corner.x * 2.0 - 1.0) * _CloudPlane.x, _CloudPlane.y, camera.z + (corner.y * 2.0 - 1.0) * _CloudPlane.x);
                o.positionCS = TransformWorldToHClip(lerp(camera, o.positionWS, _CloudLayer.x));
                return o;
            }

            float CloudNoise(float2 p, float detail)
            {
                float n = WeatherValueNoise(p) * 0.62 + WeatherValueNoise(p * 2.13 + 5.3) * 0.38;
                return lerp(n, n * 0.78 + WeatherValueNoise(p * 4.37 + 9.1) * 0.22, detail);
            }

            float CloudDensity(float2 xz, float2 drift, float detail, float cover, float layer)
            {
                if (cover <= 0.004) return 0.0;

                float2 p = xz + drift + layer * float2(3417.0, 7193.0);
                float fine = CloudNoise(p * _Scale, detail);
                float n;
                if (layer > 1.5)
                {
                    float2 feather = float2(dot(p, float2(0.8, 0.6)), dot(p, float2(-0.6, 0.8))) * 0.0011;
                    feather.y += 0.45 * WeatherValueNoise(feather * 0.6) + 0.3 * sin(feather.x * 1.4);
                    float strands = WeatherValueNoise(feather * float2(0.25, 5.0));
                    float veil = WeatherValueNoise(feather * float2(0.7, 1.5));
                    n = strands * 0.58 + veil * 0.32 + fine * 0.1;
                }
                else
                {
                    float2 shape = p * lerp(0.0016, 0.0008, layer);
                    float clustered = WeatherValueNoise(shape) * 0.68
                                    + WeatherValueNoise(shape * 2.07 + 5.3) * 0.32;
                    n = lerp(fine, clustered, _CloudLayer.z);
                }

                float threshold = 1.0 - cover;
                return smoothstep(threshold - _Softness, threshold + _Softness, n) * saturate(cover * 1.25);
            }

            half4 frag(Varyings i, out float depth : SV_Depth) : SV_Target
            {
                float2 xz = i.positionWS.xz;
                float distanceXZ = length(xz - _WorldSpaceCameraPos.xz);
                float fade = 1.0 - smoothstep(0.55, 1.0, distanceXZ / max(_CloudPlane.x, 1.0));
                if (fade <= 0.002) discard;

                float layer = _CloudLayer.y;
                WeatherSample w = (WeatherSample)0;
                if (layer < 0.5) w = SampleWeather(i.positionWS);
                float cover = saturate(_CloudPlane.w * (1.0 - 0.12 * layer) + w.cloud);
                float2 drift = float2(0.8, 0.6) * (_Scroll * _CloudPlane.z * (1.0 + 0.4 * layer));
                float density = CloudDensity(xz, drift, 1.0, cover, layer);
                if (density <= 0.004) discard;

                Light sun = GetMainLight();
                float2 toSun = sun.direction.xz / max(sun.direction.y, 0.2);
                float thickness = 0.0;
                if (layer < 1.5)
                {
                    float2 step = toSun * _StepLength;
                    thickness += CloudDensity(xz + step, drift, 0.0, cover, layer);
                    thickness += CloudDensity(xz + step * 2.0, drift, 0.0, cover, layer);
                    thickness += CloudDensity(xz + step * 4.0, drift, 0.0, cover, layer);
                }
                float transmit = exp(-thickness * _Absorb);

                half3 ambient = WeatherAmbient() * _Ambient;
                half3 lit = sun.color * (transmit * _Sun);
                half3 body = _CloudColor.rgb * (ambient + lit);
                float storm = saturate(w.charge * _StormDarken + w.rain * 0.4);
                body = lerp(body, _StormColor.rgb * (ambient + lit * 0.35), storm);
                body += _FlashColor.rgb * (_LightningFlash * _FlashGain * (0.35 + density));

                float4 worldClip = TransformWorldToHClip(i.positionWS);
                float clipDepth = worldClip.z / worldClip.w;
                #if UNITY_REVERSED_Z
                    depth = saturate(clipDepth);
                #else
                    depth = saturate((clipDepth - UNITY_NEAR_CLIP_VALUE) / (1.0 - UNITY_NEAR_CLIP_VALUE));
                #endif

                float layerOpacity = layer > 1.5 ? 0.58 : 1.0 - layer * 0.18;
                float opacity = density * _Opacity * layerOpacity;
                if (layer < 0.5)
                {
                    float core = smoothstep(0.38, 0.55, density);
                    opacity = max(opacity, core * _LowCloudCoreOpacity);
                }
                return half4(body, opacity * fade);
            }
            ENDHLSL
        }
    }
}

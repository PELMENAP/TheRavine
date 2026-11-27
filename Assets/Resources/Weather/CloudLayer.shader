Shader "The Ravine/Weather/Cloud Layer"
{
    Properties
    {
        [Header(Shape)]
        _Scale ("Noise Scale", Float) = 0.0016
        _Scroll ("Scroll", Float) = 40
        _Softness ("Edge Softness", Range(0.02, 0.5)) = 0.14
        _BaseCover ("Fair Weather Cover", Range(0, 1)) = 0
        _Opacity ("Opacity", Range(0, 1)) = 0.92

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
                float  _BaseCover;
                float  _Opacity;
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
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            float CloudNoise(float2 p, float detail)
            {
                float n = WeatherValueNoise(p) * 0.62 + WeatherValueNoise(p * 2.13 + 5.3) * 0.38;
                return lerp(n, n * 0.78 + WeatherValueNoise(p * 4.37 + 9.1) * 0.22, detail);
            }

            float CloudDensity(float2 xz, float2 drift, float detail)
            {
                float cover = saturate(SampleWeather(float3(xz.x, 0.0, xz.y)).cloud + _BaseCover);
                if (cover <= 0.004) return 0.0;

                float n = CloudNoise((xz + drift) * _Scale, detail);
                float threshold = 1.0 - cover;
                return smoothstep(threshold - _Softness, threshold + _Softness, n) * saturate(cover * 1.25);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 xz = i.positionWS.xz;
                float distanceXZ = length(xz - _WorldSpaceCameraPos.xz);
                float fade = 1.0 - smoothstep(0.55, 1.0, distanceXZ / max(_CloudPlane.x, 1.0));
                if (fade <= 0.002) discard;

                float2 drift = _WeatherWind.xy * (_Scroll * _CloudPlane.z);
                float density = CloudDensity(xz, drift, 1.0);
                if (density <= 0.004) discard;

                Light sun = GetMainLight();
                float2 toSun = sun.direction.xz / max(sun.direction.y, 0.2);
                float thickness = 0.0;
                thickness += CloudDensity(xz + toSun * (_StepLength * 1.0), drift, 0.0);
                thickness += CloudDensity(xz + toSun * (_StepLength * 2.0), drift, 0.0);
                thickness += CloudDensity(xz + toSun * (_StepLength * 3.0), drift, 0.0);
                thickness += CloudDensity(xz + toSun * (_StepLength * 4.0), drift, 0.0);
                thickness += CloudDensity(xz + toSun * (_StepLength * 5.0), drift, 0.0);
                float transmit = exp(-thickness * _Absorb);

                WeatherSample w = SampleWeather(i.positionWS);
                half3 ambient = WeatherAmbient() * _Ambient;
                half3 lit = sun.color * (transmit * _Sun);
                half3 body = _CloudColor.rgb * (ambient + lit);
                float storm = saturate(w.charge * _StormDarken + w.rain * 0.4);
                body = lerp(body, _StormColor.rgb * (ambient + lit * 0.35), storm);
                body += _FlashColor.rgb * (_LightningFlash * _FlashGain * (0.35 + density));

                return half4(body, density * _Opacity * fade);
            }
            ENDHLSL
        }
    }
}

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

        [Header(Volume)]
        _Powder ("Powder Edge Lightening", Range(0, 1)) = 0.6
        _Silver ("Silver Lining", Range(0, 2)) = 0.8
        _BaseDark ("Dark Thick Cores", Range(0, 1)) = 0.35
        _Parallax ("View Parallax", Float) = 70

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

            #include "Assets/Shaders/Effects/CloudDensity.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float  _Scale;
                float  _Scroll;
                float  _Softness;
                float  _Opacity;
                float  _LowCloudCoreOpacity;
                float  _Powder;
                float  _Silver;
                float  _BaseDark;
                float  _Parallax;
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
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.positionCS.z = o.positionCS.w * UNITY_RAW_FAR_CLIP_VALUE;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 xz = i.positionWS.xz;
                float3 toPoint = i.positionWS - _WorldSpaceCameraPos;
                float range = length(toPoint.xz) / max(_CloudPlane.x, 1.0);
                float fade = 1.0 - smoothstep(0.55, 1.0, range);
                if (fade <= 0.002) discard;

                float layer = _CloudLayer.x;
                float clump = _CloudLayer.y;
                float lod = 3.0 * smoothstep(0.15, 1.0, range);
                float detail = 1.0 - smoothstep(0.35, 0.7, range);

                WeatherSample w = (WeatherSample)0;
                if (layer < 0.5) w = SampleWeather(i.positionWS);
                float cover = CloudCover(_CloudPlane.w, w.cloud, layer);
                float2 drift = CloudDrift(_CloudPlane.z, _Scroll, layer);
                float density = CloudDensity(xz, drift, detail, cover, layer, lod, _Scale, _Softness, clump);
                if (density <= 0.004) discard;

                Light sun = GetMainLight();
                float taps = layer < 1.5 ? 1.0 - smoothstep(0.5, 0.7, range) : 0.0;
                float transmit = 1.0;
                float powder = 1.0;
                float silver = 0.0;
                float slab = density;
                if (taps > 0.01)
                {
                    float2 toSun = sun.direction.xz / max(sun.direction.y, 0.2);
                    float2 sunStep = toSun * _StepLength;
                    float tapNear = CloudDensity(xz + sunStep, drift, 0.0, cover, layer, lod, _Scale, _Softness, clump);
                    float tapMid = CloudDensity(xz + sunStep * 2.0, drift, 0.0, cover, layer, lod, _Scale, _Softness, clump);
                    float tapFar = CloudDensity(xz + sunStep * 4.0, drift, 0.0, cover, layer, lod, _Scale, _Softness, clump);
                    float thickness = lerp(density * 1.5, tapNear + tapMid + tapFar, taps);
                    transmit = exp(-thickness * _Absorb);
                    powder = lerp(1.0, 1.0 - exp(-density * 3.0), _Powder * taps);
                    float rim = saturate(density - tapNear);
                    silver = rim * rim * _Silver * taps;

                    float2 viewShift = toPoint.xz / max(abs(toPoint.y), 1.0) * _Parallax;
                    float deep = CloudDensity(xz + viewShift, drift, 0.0, cover, layer, lod, _Scale, _Softness, clump);
                    slab = lerp(density, 0.5 * (density + deep), taps);
                }

                half3 ambient = WeatherAmbient() * _Ambient;
                half3 lit = sun.color * (_Sun * (transmit * powder + silver));
                half3 body = _CloudColor.rgb * (ambient + lit);
                body *= 1.0 - _BaseDark * smoothstep(0.3, 1.0, slab);
                float storm = saturate(w.charge * _StormDarken + w.rain * 0.4);
                body = lerp(body, _StormColor.rgb * (ambient + lit * 0.35), storm);
                body += _FlashColor.rgb * (_LightningFlash * _FlashGain * (0.35 + density));

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

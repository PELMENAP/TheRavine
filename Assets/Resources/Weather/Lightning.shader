Shader "The Ravine/Weather/Lightning"
{
    Properties
    {
        [HDR] _BoltColor ("Color", Color) = (0.72, 0.82, 1.0, 1)
        _Emission ("Emission", Float) = 9
        _PixelWidth ("Minimum Width Per Distance", Float) = 0.0012
        _Core ("Core Sharpness", Range(0.5, 6)) = 2.2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+30"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Lightning"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Assets/Shaders/Effects/WeatherCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BoltColor;
                float  _Emission;
                float  _PixelWidth;
                float  _Core;
            CBUFFER_END

            struct BoltSegment
            {
                float4 a;
                float4 b;
                float4 c;
            };

            StructuredBuffer<BoltSegment> _BoltSegments;
            float _BoltTime;
            float _BoltDuration;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                nointerpolation float intensity : TEXCOORD1;
            };

            Varyings Culled()
            {
                Varyings o = (Varyings)0;
                o.positionCS = WeatherCulled();
                return o;
            }

            Varyings vert(uint vid : SV_VertexID, uint iid : SV_InstanceID)
            {
                BoltSegment s = _BoltSegments[iid];
                float intensity = WeatherStrokeIntensity(_BoltTime - s.c.x, s.c.y, _BoltDuration) * s.c.z;
                if (intensity <= 0.003) return Culled();

                float2 corner = WeatherCorners[vid];
                float3 p = lerp(s.a.xyz, s.b.xyz, corner.y);
                float3 axis = s.b.xyz - s.a.xyz;
                float3 toCamera = _WorldSpaceCameraPos - p;
                float3 side = normalize(cross(axis, toCamera) + float3(1e-5, 0.0, 0.0));
                float width = max(lerp(s.a.w, s.b.w, corner.y), length(toCamera) * _PixelWidth);
                p += side * ((corner.x - 0.5) * 2.0 * width);

                Varyings o;
                o.positionCS = TransformWorldToHClip(p);
                o.uv = corner;
                o.intensity = intensity;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float across = 1.0 - abs(i.uv.x * 2.0 - 1.0);
                float core = pow(saturate(across), _Core);
                float halo = across * across * 0.35;
                half3 rgb = _BoltColor.rgb * (_Emission * i.intensity * (core + halo));
                return half4(rgb, 1.0);
            }
            ENDHLSL
        }
    }
}

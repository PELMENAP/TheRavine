Shader "Hidden/The Ravine/Weather/Cloud Shadow Bake"
{
    Properties
    {
        _Scale ("Fine Detail Scale", Float) = 0.0016
        _Scroll ("Drift Speed", Float) = 4
        _Softness ("Edge Softness", Float) = 0.14
        _BakeRect ("Rect", Vector) = (0, 0, 1, 0)
        _BakeParams ("Params", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Bake"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Assets/Shaders/Effects/CloudDensity.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float  _Scale;
                float  _Scroll;
                float  _Softness;
                float4 _BakeRect;
                float4 _BakeParams;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(uint vid : SV_VertexID)
            {
                float2 uv = float2((vid << 1) & 2, vid & 2);
                Varyings o;
                o.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                #if UNITY_UV_STARTS_AT_TOP
                    o.positionCS.y = -o.positionCS.y;
                #endif
                o.uv = uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 xz = _BakeRect.xy + i.uv * _BakeRect.z;
                float weatherCloud = SampleWeather(float3(xz.x, 0.0, xz.y)).cloud;
                float cover = CloudCover(_BakeParams.y, weatherCloud, 0.0);
                float density = CloudDensity(xz, CloudDrift(_BakeParams.x, _Scroll, 0.0), 1.0, cover, 0.0, 0.0,
                    _Scale, _Softness, _BakeParams.z);
                return half4(density, 0.0, 0.0, 1.0);
            }
            ENDHLSL
        }
    }
}

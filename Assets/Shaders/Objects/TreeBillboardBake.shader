Shader "Hidden/TreeBillboardBake"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" }

        Pass
        {
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

            CBUFFER_START(UnityPerDraw)
                float4x4 unity_ObjectToWorld;
            CBUFFER_END

            float4x4 unity_MatrixV;
            float4x4 unity_MatrixVP;

            float4 _Color;
            float _Cutoff;
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalVS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float ao : TEXCOORD2;
            };

            struct BakeTargets
            {
                float4 albedo : SV_Target0;
                float4 normal : SV_Target1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = mul(unity_ObjectToWorld, float4(input.positionOS.xyz, 1.0)).xyz;
                float3 normalWS = normalize(mul((float3x3)unity_ObjectToWorld, input.normalOS));
                output.positionCS = mul(unity_MatrixVP, float4(positionWS, 1.0));
                output.normalVS = mul((float3x3)unity_MatrixV, normalWS);
                output.uv = input.uv;
                output.ao = input.color.r;
                return output;
            }

            BakeTargets frag(Varyings input, bool frontFace : SV_IsFrontFace)
            {
                float4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                clip(albedo.a - _Cutoff);

                float3 normalVS = normalize(input.normalVS) * (frontFace ? 1.0 : -1.0);

                BakeTargets output;
                output.albedo = float4(albedo.rgb * input.ao, 1.0);
                output.normal = float4(normalVS * 0.5 + 0.5, 1.0);
                return output;
            }
            ENDHLSL
        }
    }
}

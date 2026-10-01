Shader "The Ravine/Objects/FarInstanced"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0
        _VertexColorAO ("Vertex Color AO", Range(0, 1)) = 1
        _Translucency ("Translucency", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Toggle] _AlphaToMask ("Alpha To Mask", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull [_Cull]

        HLSLINCLUDE
        #pragma target 4.5
        #include "Assets/Shaders/Objects/FarObjectCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float _Cutoff;
            float _VertexColorAO;
            float _Translucency;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            float4 color : COLOR;
            uint instanceID : SV_InstanceID;
        };

        void FarVertex(Attributes input, out float3 positionWS, out float3 normalWS)
        {
            float4 instance = FarInstance(input.instanceID);
            positionWS = instance.xyz + input.positionOS.xyz * instance.w;
            normalWS = normalize(input.normalOS);
            BendWorld(positionWS, normalWS);
        }

        float SampleAlpha(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            AlphaToMask [_AlphaToMask]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float ao : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS, normalWS;
                FarVertex(input, positionWS, normalWS);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.ao = lerp(1.0, input.color.r, _VertexColorAO);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input, half facing : VFACE) : SV_Target
            {
                float4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                clip(albedo.a - _Cutoff);

                float3 normalWS = normalize(input.normalWS) * (facing > 0 ? 1.0 : -1.0);
                float3 color = FarLighting(albedo.rgb * input.ao, input.positionWS, normalWS, input.positionCS, _Translucency);
                color = MixFog(color, input.fogFactor);
                return half4(color, albedo.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            ShadowVaryings ShadowVert(Attributes input)
            {
                ShadowVaryings output;
                float3 positionWS, normalWS;
                FarVertex(input, positionWS, normalWS);
                output.positionCS = FarShadowPositionCS(positionWS, normalWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                clip(SampleAlpha(input.uv) - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            DepthVaryings DepthVert(Attributes input)
            {
                DepthVaryings output;
                float3 positionWS, normalWS;
                FarVertex(input, positionWS, normalWS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half DepthFrag(DepthVaryings input) : SV_Target
            {
                clip(SampleAlpha(input.uv) - _Cutoff);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}

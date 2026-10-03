Shader "The Ravine/Objects/TreeBillboard"
{
    Properties
    {
        [NoScaleOffset] _BillboardAlbedo ("Atlas Albedo", 2D) = "white" {}
        [NoScaleOffset] _BillboardNormal ("Atlas View Normal", 2D) = "gray" {}
        _AtlasGrid ("Atlas Grid (Columns, Rows)", Vector) = (2, 1, 0, 0)
        _BillboardPitch ("Bake Pitch", Range(0, 89)) = 30
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _Translucency ("Translucency", Range(0, 1)) = 0.3

        [MainColor] _AlbedoTint ("Albedo Tint", Color) = (1, 1, 1, 1)
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 1
        _AmbientFlatten ("Ambient Flatten", Range(0, 1)) = 0.5
        _AOStrength ("AO Strength", Range(0, 1)) = 1
        _AOBottom ("Bottom Occlusion", Range(0, 1)) = 0.75
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off

        HLSLINCLUDE
        #pragma target 4.5
        #include "Assets/Shaders/Objects/FarObjectCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _AtlasGrid;
            float _BillboardPitch;
            float _Cutoff;
            float _Translucency;
            float4 _AlbedoTint;
            float _AmbientStrength;
            float _AmbientFlatten;
            float _AOStrength;
            float _AOBottom;
        CBUFFER_END

        float4 _BillboardBox;
        float _BillboardCell;

        TEXTURE2D(_BillboardAlbedo); SAMPLER(sampler_BillboardAlbedo);
        TEXTURE2D(_BillboardNormal); SAMPLER(sampler_BillboardNormal);

        struct Attributes
        {
            float4 positionOS : POSITION;
            uint instanceID : SV_InstanceID;
        };

        struct BillboardFrame
        {
            float3 right;
            float3 up;
            float3 back;
            float column;
        };

        BillboardFrame GetFrame()
        {
            float f = _ViewFacing < 0.0 ? -1.0 : 1.0;
            float s, c;
            sincos(radians(_BillboardPitch), s, c);

            BillboardFrame frame;
            frame.right = float3(f, 0.0, 0.0);
            frame.up = float3(0.0, c, f * s);
            frame.back = -float3(0.0, -s, f * c);
            frame.column = f > 0.0 ? 0.0 : 1.0;
            return frame;
        }

        void BillboardVertex(Attributes input, out float3 positionWS, out float2 atlasUV,
                             out BillboardFrame frame, out float2 quad)
        {
            frame = GetFrame();
            float4 instance = FarInstance(input.instanceID);
            quad = input.positionOS.xy;

            float3 center = instance.xyz + float3(0.0, _BillboardBox.z, _BillboardBox.w) * instance.w;
            positionWS = center
                + frame.right * (quad.x - 0.5) * _BillboardBox.x * instance.w
                + frame.up * (quad.y - 0.5) * _BillboardBox.y * instance.w;
            frame.right = BendNormal(positionWS, frame.right);
            frame.up = BendNormal(positionWS, frame.up);
            frame.back = BendNormal(positionWS, frame.back);
            positionWS = BendWorld(positionWS);

            atlasUV = (float2(frame.column, _BillboardCell) + quad) / _AtlasGrid.xy;
        }

        float SampleAlpha(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_BillboardAlbedo, sampler_BillboardAlbedo, uv).a;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            AlphaToMask On

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
                float2 uv : TEXCOORD1;
                float3 right : TEXCOORD2;
                float3 up : TEXCOORD3;
                float3 back : TEXCOORD4;
                float fogFactor : TEXCOORD5;
                float aoGradient : TEXCOORD6; // 0 у основания, 1 у макушки
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS;
                float2 uv;
                BillboardFrame frame;
                float2 quad;
                BillboardVertex(input, positionWS, uv, frame, quad);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.uv = uv;
                output.right = frame.right;
                output.up = frame.up;
                output.back = frame.back;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.aoGradient = quad.y;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float4 albedo = SAMPLE_TEXTURE2D(_BillboardAlbedo, sampler_BillboardAlbedo, input.uv);
                clip(albedo.a - _Cutoff);

                float3 n = SAMPLE_TEXTURE2D(_BillboardNormal, sampler_BillboardNormal, input.uv).xyz * 2.0 - 1.0;
                float3 normalWS = normalize(input.right * n.x + input.up * n.y + input.back * n.z);

                float bakedAO = lerp(_AOBottom, 1.0, input.aoGradient);

                FarShading shading;
                shading.albedo = albedo.rgb * _AlbedoTint.rgb;
                shading.translucency = _Translucency;
                shading.bakedAO = bakedAO;
                shading.aoStrength = _AOStrength;
                shading.ambientScale = _AmbientStrength;
                shading.ambientFlatten = _AmbientFlatten;

                float3 color = FarLighting(shading, input.positionWS, normalWS, input.positionCS);
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
                float3 positionWS;
                float2 uv;
                BillboardFrame frame;
                float2 quad;
                BillboardVertex(input, positionWS, uv, frame, quad);
                output.positionCS = FarShadowPositionCS(positionWS, frame.back);
                output.uv = uv;
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
                float3 positionWS;
                float2 uv;
                BillboardFrame frame;
                float2 quad;
                BillboardVertex(input, positionWS, uv, frame, quad);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = uv;
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
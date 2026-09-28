Shader "The Ravine/Terrain/BiomeTerrain"
{
    Properties
    {
        [Header(Biomes)]
        _TexScale ("Biome Texture Scale", Float) = 0.1
        _HeightBlendDepth ("Height Blend Depth", Range(0.01, 1)) = 0.2
        _Smoothness ("Smoothness", Range(0, 1)) = 0.1

        [Header(Rock)]
        _RockTex ("Rock", 2D) = "gray" {}
        _RockColor ("Rock Tint", Color) = (1, 1, 1, 1)
        _RockScale ("Rock Scale", Float) = 0.08
        _RockSlopeMin ("Rock Slope Min", Range(0, 1)) = 0.25
        _RockSlopeMax ("Rock Slope Max", Range(0, 1)) = 0.45

        [Header(Snow)]
        _SnowTex ("Snow", 2D) = "white" {}
        _SnowColor ("Snow Tint", Color) = (1, 1, 1, 1)
        _SnowTempMin ("Snow Full Temperature", Range(0, 1)) = 0.05
        _SnowTempMax ("Snow Start Temperature", Range(0, 1)) = 0.2
        _SnowFlatness ("Snow Flatness", Range(0.01, 1)) = 0.5

        [Header(Sand)]
        _SandTex ("Sand", 2D) = "white" {}
        _SandColor ("Sand Tint", Color) = (0.86, 0.8, 0.62, 1)
        _WaterLevel ("Water Level", Float) = 4
        _SandHeight ("Sand Height", Float) = 1.5

        [Header(Wetness)]
        _WetStrength ("Wet Strength", Range(0, 4)) = 1.5
        _WetDarken ("Wet Darken", Range(0, 1)) = 0.6
        _WetSmoothness ("Wet Smoothness", Range(0, 1)) = 0.6

        [Header(Variation)]
        _MacroScale ("Macro Scale", Float) = 0.004
        _MacroStrength ("Macro Strength", Range(0, 0.5)) = 0.15
        _DetailFarDistance ("Detail Far Distance", Float) = 250
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE
        #pragma target 4.5
        #include "Assets/Shaders/Terrain/BiomeTerrainCommon.hlsl"
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

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
                float3 flatPositionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 climateRiver : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            Varyings vert(TerrainAttributes input)
            {
                Varyings output;
                float3 flatPositionWS, positionWS, normalWS;
                TerrainVertexWorld(input, flatPositionWS, positionWS, normalWS);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.flatPositionWS = flatPositionWS;
                output.normalWS = normalWS;
                output.climateRiver = float3(input.climate, input.river);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewVector = _WorldSpaceCameraPos - input.positionWS;
                float viewDistance = length(viewVector);
                float3 viewDirWS = viewVector / max(viewDistance, 1e-4);

                float3 albedo;
                float smoothness;
                TerrainSurface(input.flatPositionWS, normalWS, input.climateRiver.xy, input.climateRiver.z, viewDistance, albedo, smoothness);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDirWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;

                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
                float specPower = exp2(smoothness * 10.0) + 1.0;
                half4 shadowMask = half4(1, 1, 1, 1);

                Light mainLight = GetMainLight(inputData.shadowCoord);
                float mainAtten = mainLight.shadowAttenuation * mainLight.distanceAttenuation * ao.directAmbientOcclusion;
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float3 lighting = mainLight.color * mainAtten * saturate(dot(normalWS, mainLight.direction));
                float3 specular = mainLight.color * mainAtten * pow(saturate(dot(normalWS, halfDir)), specPower) * smoothness;

                #if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP
                #if USE_CLUSTER_LIGHT_LOOP
                UNITY_LOOP for (uint dirIndex = 0; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
                {
                    Light light = GetAdditionalLight(dirIndex, inputData.positionWS, shadowMask);
                    float atten = light.distanceAttenuation * light.shadowAttenuation;
                    lighting += light.color * atten * saturate(dot(normalWS, light.direction));
                }
                #endif

                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
                    float atten = light.distanceAttenuation * light.shadowAttenuation;
                    lighting += light.color * atten * saturate(dot(normalWS, light.direction));
                LIGHT_LOOP_END
                #endif

                float3 ambient = SampleSH(normalWS) * ao.indirectAmbientOcclusion;
                float3 color = albedo * (lighting + ambient) + specular;
                color = MixFog(color, inputData.fogCoord);
                return half4(color, 1.0);
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
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            float4 ShadowVert(TerrainAttributes input) : SV_POSITION
            {
                float3 flatPositionWS, positionWS, normalWS;
                TerrainVertexWorld(input, flatPositionWS, positionWS, normalWS);
                return TerrainShadowPositionCS(positionWS, normalWS);
            }

            half4 ShadowFrag() : SV_Target
            {
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
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            float4 DepthVert(TerrainAttributes input) : SV_POSITION
            {
                float3 flatPositionWS, positionWS, normalWS;
                TerrainVertexWorld(input, flatPositionWS, positionWS, normalWS);
                return TransformWorldToHClip(positionWS);
            }

            half DepthFrag(float4 positionCS : SV_POSITION) : SV_Target
            {
                return positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct NormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            NormalsVaryings DepthNormalsVert(TerrainAttributes input)
            {
                NormalsVaryings output;
                float3 flatPositionWS, positionWS, normalWS;
                TerrainVertexWorld(input, flatPositionWS, positionWS, normalWS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = normalWS;
                return output;
            }

            half4 DepthNormalsFrag(NormalsVaryings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                float2 remapped = saturate(octNormalWS * 0.5 + 0.5);
                return half4(PackFloat2To888(remapped), 0.0);
                #else
                return half4(NormalizeNormalPerPixel(normalWS), 0.0);
                #endif
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}

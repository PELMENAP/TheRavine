Shader "Custom/ChunkGrassShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

        [Header(Wind)]
        _WindMap ("Wind Map", 2D) = "gray" {}
        _WindStrength ("Wind Strength", Float) = 0.25
        _WindMapScale ("Wind Map Scale", Float) = 0.01
        _WindVelocity ("Wind Velocity", Vector) = (0.05, 0.02, 0, 0)

        [Header(Coloring)]
        _InstanceColorBlend ("Instance Color Blend", Range(0, 1)) = 1.0
        _AmbientOcclusion ("Ambient Occlusion", Range(0, 1)) = 0.2

        [Header(Player Interaction)]
        _PlayerRadius("Player Radius", Float) = 1.0
        _PlayerStrength("Player Strength", Float) = 0.5

        [Header(Lighting)]
        _NormalWindInfluence("Normal Wind Influence", Range(0, 2)) = 0.5
        _TranslucencyStrength("Translucency Strength", Range(0, 1)) = 0.3
        _AmbientStrength("Ambient Strength", Range(0, 2)) = 0.35
    }

    SubShader
    {
        
        Tags
        {
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest"
            "RenderPipeline"="UniversalPipeline"
        }

        LOD 300
        Cull Off

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        struct InstanceData
        {
            float4x4 trs;
            float4 color;
        };

        StructuredBuffer<InstanceData> instanceData;

        CBUFFER_START(UnityPerMaterial)

            float4 _MainTex_ST;

            float _Cutoff;

            float _WindStrength;
            float _WindMapScale;
            float2 _WindVelocity;

            float _InstanceColorBlend;
            float _AmbientOcclusion;

            float3 _PlayerPosition;
            float _PlayerRadius;
            float _PlayerStrength;

            float _NormalWindInfluence;
            float _TranslucencyStrength;
            float _AmbientStrength;

        CBUFFER_END

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        
        TEXTURE2D(_WindMap);
        SAMPLER(sampler_WindMap);   

        float3 TransformNormal(float3 normalOS, float4x4 trs)
        {
            return normalize(mul((float3x3)trs, normalOS));
        }

        float3 ApplyPlayerInteraction(float3 positionWS, float heightFactor)
        {
            float distanceToPlayer = distance(positionWS, _PlayerPosition);

            if (distanceToPlayer >= _PlayerRadius)
                return 0;

            float falloff = 1.0 - (distanceToPlayer / _PlayerRadius);
            falloff *= falloff;

            float3 direction = normalize(positionWS - _PlayerPosition);

            float3 offset =
                direction *
                falloff *
                _PlayerStrength *
                heightFactor;

            offset.y = -falloff * _PlayerStrength * 0.2;

            return offset;
        }

        float3 SampleWindOffset(float3 positionWS, float heightFactor)
        {
            float2 windUV = positionWS.xz * _WindMapScale + _WindVelocity.xy * _Time.y;
            float2 windSample = SAMPLE_TEXTURE2D_LOD(_WindMap, sampler_WindMap, windUV, 0).rg;
            windSample = windSample * 2.0 - 1.0;

            float windFactor = heightFactor * heightFactor;
            return float3(windSample.x, 0, windSample.y) * _WindStrength * windFactor;
        }

        float3 ApplyWind(
            float3 positionWS,
            float heightFactor,
            out float3 windOffset)
        {
            windOffset =
                SampleWindOffset(positionWS, heightFactor);

            float3 playerOffset =
                ApplyPlayerInteraction(positionWS, heightFactor);

            return positionWS + windOffset + playerOffset;
        }

        float3 TransformObjectToWorldWithWind(
            float4 positionOS,
            float4x4 instanceTRS,
            out float heightFactor,
            out float3 windOffset)
        {
            float3 positionWS =
                mul(positionOS, instanceTRS);

            heightFactor = positionOS.y;

            return ApplyWind(positionWS, heightFactor, windOffset);
        }

        ENDHLSL

        Pass
        {
            Name "ForwardLit"

            Tags
            {
                "LightMode"="UniversalForward"
            }

            ZWrite On
            ZTest LEqual

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;

                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;

                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;

                float4 color : COLOR;

                float heightFactor : TEXCOORD3;

                float4 shadowCoord : TEXCOORD4;

                float3 windOffset : TEXCOORD5;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                InstanceData instance =
                    instanceData[input.instanceID];

                float heightFactor;
                float3 windOffset;

                float3 positionWS =
                    TransformObjectToWorldWithWind(
                        input.positionOS,
                        instance.trs,
                        heightFactor,
                        windOffset);

                float3 normalWS =
                    TransformNormal(
                        input.normalOS,
                        instance.trs);

                normalWS =
                    normalize(
                        normalWS +
                        float3(windOffset.x, 0, windOffset.z)
                        * _NormalWindInfluence);

                output.positionCS =
                    TransformWorldToHClip(positionWS);

                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);

                output.color = instance.color;

                output.heightFactor = heightFactor;

                output.shadowCoord =
                    TransformWorldToShadowCoord(positionWS);

                output.windOffset = windOffset;

                return output;
            }

            float4 frag(
                Varyings input,
                half facing : VFACE) : SV_Target
            {
                if (input.color.a < 0.2)
                    discard;

                float4 texColor =
                    SAMPLE_TEXTURE2D(
                        _MainTex,
                        sampler_MainTex,
                        input.uv);

                clip(texColor.a - _Cutoff);

                float3 normalWS =
                    normalize(input.normalWS);

                normalWS *= facing > 0 ? 1 : -1;

                float4 finalColor =
                    lerp(
                        texColor,
                        texColor * input.color,
                        _InstanceColorBlend);

                float ao =
                    lerp(
                        1.0,
                        1.0 - _AmbientOcclusion,
                        1.0 - input.heightFactor);

                Light mainLight =
                    GetMainLight(input.shadowCoord);

                float NdotL =
                    saturate(
                        dot(normalWS, mainLight.direction));

                float3 directLighting =
                    mainLight.color *
                    NdotL *
                    mainLight.shadowAttenuation;

                float backLighting =
                    saturate(
                        dot(-mainLight.direction, normalWS));

                float3 translucency =
                    mainLight.color *
                    backLighting *
                    _TranslucencyStrength *
                    input.heightFactor;

                float3 ambientLighting =
                    SampleSH(normalWS) *
                    _AmbientStrength;

                float3 lighting =
                    directLighting +
                    translucency +
                    ambientLighting;

                uint additionalLightsCount =
                    GetAdditionalLightsCount();

                for (uint i = 0; i < additionalLightsCount; i++)
                {
                    Light light =
                        GetAdditionalLight(
                            i,
                            input.positionWS);

                    float attenuation =
                        light.distanceAttenuation *
                        light.shadowAttenuation;

                    float additionalNdotL =
                        saturate(
                            dot(normalWS, light.direction));

                    lighting +=
                        light.color *
                        additionalNdotL *
                        attenuation;
                }

                finalColor.rgb *= lighting * ao;

                return finalColor;
            }

            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"

            Tags
            {
                "LightMode"="ShadowCaster"
            }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM

            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;

                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 GetShadowPositionHClip(float3 positionWS)
            {
                float4 positionCS =
                    TransformWorldToHClip(positionWS);

                #if UNITY_REVERSED_Z
                    positionCS.z =
                        min(
                            positionCS.z,
                            positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z =
                        max(
                            positionCS.z,
                            positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                return positionCS;
            }

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;

                InstanceData instance =
                    instanceData[input.instanceID];

                float heightFactor;
                float3 windOffset;

                float3 positionWS =
                    TransformObjectToWorldWithWind(
                        input.positionOS,
                        instance.trs,
                        heightFactor,
                        windOffset);

                output.positionCS =
                    GetShadowPositionHClip(positionWS);

                output.uv = input.uv;

                return output;
            }

            float4 ShadowFrag(Varyings input) : SV_Target
            {
                float alpha =
                    SAMPLE_TEXTURE2D(
                        _MainTex,
                        sampler_MainTex,
                        input.uv).a;

                clip(alpha - _Cutoff);

                return 0;
            }

            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"

            Tags
            {
                "LightMode"="DepthOnly"
            }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM

            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;

                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;

                InstanceData instance =
                    instanceData[input.instanceID];

                float heightFactor;
                float3 windOffset;

                float3 positionWS =
                    TransformObjectToWorldWithWind(
                        input.positionOS,
                        instance.trs,
                        heightFactor,
                        windOffset);

                output.positionCS =
                    TransformWorldToHClip(positionWS);

                output.uv = input.uv;

                return output;
            }

            float4 DepthFrag(Varyings input) : SV_Target
            {
                float alpha =
                    SAMPLE_TEXTURE2D(
                        _MainTex,
                        sampler_MainTex,
                        input.uv).a;

                clip(alpha - _Cutoff);

                return 0;
            }

            ENDHLSL
        }
    }

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
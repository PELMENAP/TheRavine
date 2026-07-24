Shader "The Ravine/ChunkGrassShader"
{
    Properties
    {
        _MainTex ("Texture Array", 2DArray) = "" {}
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
        _PlayerHeight("Player Height", Float) = 0.5

        [Header(Lighting)]
        _NormalWindInfluence("Normal Wind Influence", Range(0, 2)) = 0.5
        _TranslucencyStrength("Translucency Strength", Range(0, 1)) = 0.3
        _AmbientStrength("Ambient Strength", Range(0, 2)) = 0.35

        [Header(Hue Variation)]
        _HueVariation ("Hue Variation", Range(0, 1)) = 0.15
        _SaturationVariation ("Saturation Variation", Range(0, 1)) = 0.1
        _ValueVariation ("Value Variation", Range(0, 1)) = 0.1
        _HueVariationScale ("Hue Variation Scale", Float) = 0.5
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
            float3 position;
            float rotation;

            float scaleXZ;
            float scaleY;

            uint packedColor;
        };

        StructuredBuffer<InstanceData> instanceData;

        CBUFFER_START(UnityPerMaterial)
            float _Cutoff;
            float _WindStrength;
            float _WindMapScale;
            float2 _WindVelocity;

            float _InstanceColorBlend;
            float _AmbientOcclusion;

            float3 _PlayerPosition;
            float _PlayerRadius;
            float _PlayerStrength;
            float _PlayerHeight;

            float _NormalWindInfluence;
            float _TranslucencyStrength;
            float _AmbientStrength;

            float _HueVariation;
            float _SaturationVariation;
            float _ValueVariation;
            float _HueVariationScale;
        CBUFFER_END

        TEXTURE2D_ARRAY(_MainTex); SAMPLER(sampler_MainTex);  
        TEXTURE2D(_WindMap); SAMPLER(sampler_WindMap);   

        float3 RotateY(float3 v, float sinRot, float cosRot)
        {
            return float3(
                v.x * cosRot - v.z * sinRot,
                v.y,
                v.x * sinRot + v.z * cosRot
            );
        }

        float hash13(float2 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * (p.x + p.y));
        }

        uint GetTextureIndex(float3 instancePos)
        {
            uint texWidth, texHeight, texCount;
            _MainTex.GetDimensions(texWidth, texHeight, texCount);
            
            if (texCount <= 1)
                return 0;

            float rnd = hash13(instancePos.xz);
            return (uint)(rnd * texCount) % texCount;
        }

        float3 ApplyPlayerInteraction(float3 positionWS, float heightFactor)
        {
            float2 posXZ = positionWS.xz;
            float2 playerXZ = _PlayerPosition.xz;
            float distanceXZ = length(posXZ - playerXZ);
            
            float heightDiff = positionWS.y - _PlayerPosition.y;
            float halfHeight = _PlayerHeight * 0.5;
            
            float radiusMask = saturate(1.0 - distanceXZ / _PlayerRadius);
            float heightMask = saturate(1.0 - abs(heightDiff) / halfHeight);
            
            float falloff = radiusMask * radiusMask * heightMask;
            
            float2 dirXZ = (distanceXZ > 0.0001) ? normalize(posXZ - playerXZ) : 0;
            
            float3 offset;
            offset.xz = dirXZ * falloff * _PlayerStrength * heightFactor;
            offset.y = -falloff * _PlayerStrength * 0.2;
            
            return offset * saturate(radiusMask) * saturate(heightMask);
        }

        float3 SampleWindOffset(float3 positionWS, float heightFactor, float3 instancePos)
        {
            float2 phaseOffset = instancePos.xz * 0.1;
            float2 windUV = positionWS.xz * _WindMapScale + _WindVelocity.xy * _Time.y + phaseOffset;
            
            float2 windSample = SAMPLE_TEXTURE2D_LOD(_WindMap, sampler_WindMap, windUV, 0).rg;
            windSample = windSample * 2.0 - 1.0;

            float windFactor = heightFactor * heightFactor;
            return float3(windSample.x, 0, windSample.y) * _WindStrength * windFactor;
        }

        float3 ApplyWind(
            float3 positionWS,
            float heightFactor,
            float3 instancePos,
            out float3 windOffset)
        {
            windOffset = SampleWindOffset(positionWS, heightFactor, instancePos);
            float3 playerOffset = ApplyPlayerInteraction(positionWS, heightFactor);

            return positionWS + windOffset + playerOffset;
        }

        float3 TransformInstanceVertex(
            float3 positionOS,
            InstanceData instance,
            out float sinRot,
            out float cosRot)
        {
            sincos(instance.rotation, sinRot, cosRot);

            float3 scaledPos;
            scaledPos.x = positionOS.x * instance.scaleXZ;
            scaledPos.y = positionOS.y * instance.scaleY;
            scaledPos.z = positionOS.z * instance.scaleXZ;

            float3 rotatedPos = RotateY(scaledPos, sinRot, cosRot);

            return rotatedPos + instance.position;
        }

        float4 UnpackColor(uint packed)
        {
            float r = (packed        & 0xFF) / 255.0;
            float g = ((packed >> 8)  & 0xFF) / 255.0;
            float b = ((packed >> 16) & 0xFF) / 255.0;
            float a = ((packed >> 24) & 0xFF) / 255.0;
            return float4(r, g, b, a);
        }


        float3 RGBtoHSV(float3 c)
        {
            float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
            float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
            float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));

            float d = q.x - min(q.w, q.y);
            float e = 1.0e-10;
            return float3(
                abs(q.z + (q.w - q.y) / (6.0 * d + e)),
                d / (q.x + e),
                q.x);
        }

        float3 HSVtoRGB(float3 c)
        {
            float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
            float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
            return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
        }

        float3 ApplyHueVariation(float3 color, float3 instancePos)
        {
            float rnd = hash13(instancePos.xz * _HueVariationScale);
            float3 hsv = RGBtoHSV(color);

            float hueShift = (rnd - 0.5) * _HueVariation;
            float satShift = (hash13(instancePos.xz * _HueVariationScale + 17.0) - 0.5) * _SaturationVariation;
            float valShift = (hash13(instancePos.xz * _HueVariationScale + 53.0) - 0.5) * _ValueVariation;

            hsv.x = frac(hsv.x + hueShift);
            hsv.y = saturate(hsv.y + satShift);
            hsv.z = saturate(hsv.z + valShift);

            return HSVtoRGB(hsv);
        }

        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            ZWrite On
            ZTest LEqual
            AlphaToMask On 

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
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
                float3 instancePos : TEXCOORD6;
                uint texIndex : TEXCOORD7;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                InstanceData instance = instanceData[input.instanceID];

                float heightFactor;
                float3 windOffset;
                float sinRot, cosRot;

                float3 positionWS = TransformInstanceVertex(input.positionOS.xyz, instance, sinRot, cosRot);
                heightFactor = input.positionOS.y;

                positionWS = ApplyWind(positionWS, heightFactor, instance.position, windOffset);

                float3 normalWS = RotateY(input.normalOS, sinRot, cosRot);
                normalWS = normalize(normalWS + float3(windOffset.x, 0, windOffset.z) * _NormalWindInfluence);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = normalWS;
                output.uv = input.uv;
                output.color = UnpackColor(instance.packedColor);
                output.heightFactor = heightFactor;
                output.shadowCoord = TransformWorldToShadowCoord(positionWS);
                output.windOffset = windOffset;
                output.instancePos = instance.position;
                
                output.texIndex = GetTextureIndex(instance.position);

                return output;
            }

            float4 frag(Varyings input, half facing : VFACE) : SV_Target
            {
                if (input.color.a < 0.2) discard;

                float4 texColor = SAMPLE_TEXTURE2D_ARRAY_LOD(_MainTex, sampler_MainTex, input.uv, input.texIndex, 0);
                clip(texColor.a - _Cutoff);

                float3 normalWS = normalize(input.normalWS);
                normalWS *= facing > 0 ? 1 : -1;

                float4 finalColor = lerp(texColor, texColor * input.color, _InstanceColorBlend);
                finalColor.rgb = ApplyHueVariation(finalColor.rgb, input.instancePos);

                float ao = lerp(1.0, 1.0 - _AmbientOcclusion, 1.0 - input.heightFactor);

                Light mainLight = GetMainLight(input.shadowCoord);
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float3 directLighting = mainLight.color * NdotL * mainLight.shadowAttenuation;

                float backLighting = saturate(dot(-mainLight.direction, normalWS));
                float3 translucency = mainLight.color * backLighting * _TranslucencyStrength * input.heightFactor;

                float3 ambientLighting = SampleSH(normalWS) * _AmbientStrength;
                float3 lighting = directLighting + translucency + ambientLighting;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;

                LIGHT_LOOP_BEGIN(lightIndex)
                {
                    Light light = GetAdditionalLight(lightIndex, inputData.positionWS);
                    float attenuation = light.distanceAttenuation * light.shadowAttenuation;
                    float additionalNdotL = saturate(dot(normalWS, light.direction));
                    lighting += light.color * additionalNdotL * attenuation;
                }
                LIGHT_LOOP_END

                finalColor.rgb *= lighting * ao;
                return finalColor;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

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
                uint texIndex : TEXCOORD1;
            };

            float4 GetShadowPositionHClip(float3 positionWS)
            {
                float4 positionCS = TransformWorldToHClip(positionWS);
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                InstanceData instance = instanceData[input.instanceID];

                float heightFactor;
                float3 windOffset;
                float sinRot, cosRot;

                float3 positionWS = TransformInstanceVertex(input.positionOS.xyz, instance, sinRot, cosRot);
                heightFactor = input.positionOS.y;
                positionWS = ApplyWind(positionWS, heightFactor, instance.position, windOffset);

                output.positionCS = GetShadowPositionHClip(positionWS);
                output.uv = input.uv;
                
                output.texIndex = GetTextureIndex(instance.position);

                return output;
            }

            float4 ShadowFrag(Varyings input) : SV_Target
            {
                float alpha = SAMPLE_TEXTURE2D_ARRAY_LOD(_MainTex, sampler_MainTex, input.uv, input.texIndex, 0).a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

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
                uint texIndex : TEXCOORD1;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                InstanceData instance = instanceData[input.instanceID];

                float heightFactor;
                float3 windOffset;
                float sinRot, cosRot;

                float3 positionWS = TransformInstanceVertex(input.positionOS.xyz, instance, sinRot, cosRot);
                heightFactor = input.positionOS.y;
                positionWS = ApplyWind(positionWS, heightFactor, instance.position, windOffset);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                
                output.texIndex = GetTextureIndex(instance.position);

                return output;
            }

            float4 DepthFrag(Varyings input) : SV_Target
            {
                float alpha = SAMPLE_TEXTURE2D_ARRAY_LOD(_MainTex, sampler_MainTex, input.uv, input.texIndex, 0).a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
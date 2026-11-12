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

        [Header(Soil)]
        _SoilHeightLoss ("Depletion Height Loss", Range(0, 1)) = 0.6
        _SoilCollapse ("Depletion Blade Collapse", Range(0, 1)) = 0.6
        _SoilStrawColor ("Depletion Straw Color", Color) = (0.8, 0.68, 0.35, 1)
        _SoilStrawStrength ("Depletion Straw Strength", Range(0, 1)) = 0.8
        _SoilEnergyRef ("Soil Energy Reference", Float) = 800
        _SoilEnergySaturation ("Soil Energy Saturation", Range(0, 1)) = 0.25
        _SoilEnergyDarken ("Soil Energy Darken", Range(0, 1)) = 0.25

        [Header(Season)]
        _WinterHue ("Winter Hue", Range(0, 1)) = 0.1
        _WinterHueStrength ("Winter Hue Strength", Range(0, 1)) = 0.8
        _WinterDesaturate ("Winter Desaturate", Range(0, 1)) = 0.25
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
        #pragma target 4.5

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/Shaders/Include/CurvedWorld.hlsl"

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

            float _SoilHeightLoss;
            float _SoilCollapse;
            float4 _SoilStrawColor;
            float _SoilStrawStrength;
            float _SoilEnergyRef;
            float _SoilEnergySaturation;
            float _SoilEnergyDarken;

            float _WinterHue;
            float _WinterHueStrength;
            float _WinterDesaturate;
        CBUFFER_END

        TEXTURE2D_ARRAY(_MainTex); SAMPLER(sampler_MainTex);  
        TEXTURE2D(_WindMap); SAMPLER(sampler_WindMap);   

        float3 _LightDirection;
        float3 _LightPosition;

        StructuredBuffer<float4> _SoilBuffer;
        float4 _SoilOrigin;
        float _SoilEnabled;
        float _WinterApproach;

        static const int SoilSide = 5;

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

        float4 SampleSoil(float2 xz)
        {
            UNITY_BRANCH
            if (_SoilEnabled < 0.5)
                return float4(0, 0, 0, 0);

            float2 u = (xz - _SoilOrigin.xy) * _SoilOrigin.w - 0.5;
            float2 base = floor(u);
            float2 f = u - base;
            int2 i0 = clamp((int2)base, 0, SoilSide - 1);
            int2 i1 = clamp((int2)base + 1, 0, SoilSide - 1);

            float4 c00 = _SoilBuffer[i0.y * SoilSide + i0.x];
            float4 c10 = _SoilBuffer[i0.y * SoilSide + i1.x];
            float4 c01 = _SoilBuffer[i1.y * SoilSide + i0.x];
            float4 c11 = _SoilBuffer[i1.y * SoilSide + i1.x];

            float4 w = float4((1 - f.x) * (1 - f.y), f.x * (1 - f.y), (1 - f.x) * f.y, f.x * f.y)
                     * float4(c00.w, c10.w, c01.w, c11.w);
            float total = w.x + w.y + w.z + w.w;
            float3 mean = (c00.xyz * w.x + c10.xyz * w.y + c01.xyz * w.z + c11.xyz * w.w) / max(total, 1e-4);

            float known = step(1e-4, total);
            return float4((1.0 - mean.x) * known, mean.y * known, mean.z * known, total);
        }

        InstanceData ApplySoilToInstance(InstanceData instance, out float4 soil)
        {
            soil = SampleSoil(instance.position.xz);
            float depletion = soil.x;

            float h = hash13(instance.position.xz * 1.73 + 11.1);
            float keep = 1.0 - saturate((depletion * _SoilCollapse - h) / 0.08);

            instance.scaleY *= lerp(1.0, 1.0 - _SoilHeightLoss, depletion) * keep;
            instance.scaleXZ *= keep;
            return instance;
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
            float3 center = _PlayerPosition;
            center.y += max(_PlayerHeight, 1e-4);

            float2 posXZ    = positionWS.xz;
            float2 delta    = posXZ - center.xz;
            float  distanceXZ = length(delta);

            float heightDiff = positionWS.y - center.y;
            float halfHeight = max(_PlayerHeight * 0.5, 1e-4);

            float radiusMask = saturate(1.0 - distanceXZ / max(_PlayerRadius, 1e-4));
            float heightMask = saturate(1.0 - abs(heightDiff) / halfHeight);

            float falloff = radiusMask * radiusMask * heightMask;

            float2 dirXZ = delta / max(distanceXZ, 1e-4);

            float3 offset;
            offset.xz = dirXZ * (falloff * _PlayerStrength * heightFactor);
            offset.y  = -falloff * _PlayerStrength * 0.2;

            return offset;
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

            return BendWorld(positionWS + windOffset + playerOffset);
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

        float3 ApplySoilColor(float3 color, float depletion, float energy)
        {
            float e = saturate(energy / max(_SoilEnergyRef, 1e-3));
            UNITY_BRANCH
            if (e > 1e-4)
            {
                float3 hsv = RGBtoHSV(color);
                hsv.y = saturate(hsv.y + e * _SoilEnergySaturation);
                hsv.z *= 1.0 - e * _SoilEnergyDarken;
                color = HSVtoRGB(hsv);
            }

            UNITY_BRANCH
            if (depletion > 1e-4)
            {
                float3 lumaWeights = float3(0.299, 0.587, 0.114);
                float3 straw = saturate(_SoilStrawColor.rgb * (dot(color, lumaWeights) / max(dot(_SoilStrawColor.rgb, lumaWeights), 1e-3)));
                color = lerp(color, straw, saturate(depletion) * _SoilStrawStrength);
            }
            return color;
        }

        float3 ApplyWinterColor(float3 color)
        {
            float w = saturate(_WinterApproach);
            UNITY_BRANCH
            if (w > 1e-4)
            {
                float3 hsv = RGBtoHSV(color);
                float d = _WinterHue - hsv.x;
                d -= round(d);
                hsv.x = frac(hsv.x + d * w * _WinterHueStrength);
                hsv.y *= lerp(1.0, 1.0 - _WinterDesaturate, w);
                color = HSVtoRGB(hsv);
            }
            return color;
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
            #pragma multi_compile_fog

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
                float fogFactor : TEXCOORD8;
                float2 soil : TEXCOORD9;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float4 soil;
                InstanceData instance = ApplySoilToInstance(instanceData[input.instanceID], soil);

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
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.soil = soil.xy;

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
                finalColor.rgb = ApplySoilColor(finalColor.rgb, input.soil.x, input.soil.y);
                finalColor.rgb = ApplyWinterColor(finalColor.rgb);

                float ao = lerp(1.0, 1.0 - _AmbientOcclusion, 1.0 - input.heightFactor);

                Light mainLight = GetMainLight(input.shadowCoord);
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float3 directLighting = mainLight.color * NdotL * mainLight.shadowAttenuation;

                float backLighting = saturate(dot(-mainLight.direction, normalWS));
                float3 translucency = mainLight.color * backLighting * _TranslucencyStrength * input.heightFactor * mainLight.shadowAttenuation;

                float3 ambientLighting = SampleSH(normalWS) * _AmbientStrength;
                float3 lighting = directLighting + translucency + ambientLighting;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                LIGHT_LOOP_BEGIN(lightIndex)
                {
                    Light light = GetAdditionalLight(lightIndex, inputData.positionWS);
                    float attenuation = light.distanceAttenuation * light.shadowAttenuation;
                    float additionalNdotL = saturate(dot(normalWS, light.direction));
                    lighting += light.color * additionalNdotL * attenuation;
                }
                LIGHT_LOOP_END

                finalColor.rgb *= lighting * ao;
                finalColor.rgb = MixFog(finalColor.rgb, input.fogFactor);
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
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

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
                float2 uv : TEXCOORD0;
                uint texIndex : TEXCOORD1;
            };

            float4 GetShadowPositionHClip(float3 positionWS, float3 normalWS)
            {
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
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
                float4 soil;
                InstanceData instance = ApplySoilToInstance(instanceData[input.instanceID], soil);

                float heightFactor;
                float3 windOffset;
                float sinRot, cosRot;

                float3 positionWS = TransformInstanceVertex(input.positionOS.xyz, instance, sinRot, cosRot);
                heightFactor = input.positionOS.y;
                positionWS = ApplyWind(positionWS, heightFactor, instance.position, windOffset);
                float3 normalWS = normalize(RotateY(input.normalOS, sinRot, cosRot));

                output.positionCS = GetShadowPositionHClip(positionWS, normalWS);
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
                float4 soil;
                InstanceData instance = ApplySoilToInstance(instanceData[input.instanceID], soil);

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
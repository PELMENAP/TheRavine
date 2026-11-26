Shader "The Ravine/VolumetricFog"
{
    Properties
    {
        _Color                        ("Fog Color",                 Color)            = (1, 1, 1, 1)
        _ShadowColor                  ("Shadow Color",              Color)            = (0.5, 0.5, 0.5, 1)
        _SelfShadowColor              ("Self Shadow Color",         Color)            = (0, 0, 0, 1)
        _Density                      ("Density",                   Range(0, 0.5))    = 0.05
        _MaxDistance                  ("Max Distance",              Float)            = 100.0
        _Anisotropy                   ("Phase Anisotropy (HG)",     Range(-0.85, 0.85)) = 0.45

        _HeightMaskBlend              ("Height Mask Blend",         Range(0, 1))      = 1.0
        _HeightMaskLength             ("Height Mask Length",        Float)            = 0.0
        _HeightMaskFalloff            ("Height Mask Falloff",       Range(0, 10))     = 1.0
        _HeightMaskRemapMin           ("Height Mask Remap Min",     Range(0, 1))      = 0.0
        _HeightMaskRemapMax           ("Height Mask Remap Max",     Range(0, 1))      = 1.0
        _HeightMaskTexture            ("Height Mask Texture",       2D)               = "white" {}
        _HeightMaskTextureAmplitude   ("HM Tex Amplitude",          Float)            = 1.0
        _HeightMaskTextureScale       ("HM Tex Scale",              Float)            = 1.0
        _HeightMaskTextureAnimation   ("HM Tex Animation",          Vector)           = (0, 0, 0, 0)

        _MainLightSelfShadowDistance  ("Main Light SS Distance",    Float)            = 10.0
        _SelfShadowPower              ("Self Shadow Power",         Float)            = 1.0
        _SelfShadowRemapMin           ("Self Shadow Remap Min",     Range(0, 1))      = 0.0
        _SelfShadowRemapMax           ("Self Shadow Remap Max",     Range(0, 1))      = 1.0

        [IntRange] _RayStepCount      ("Ray Steps",                 Range(1, 32))     = 16
        _TransmittanceCutoff          ("Transmittance Cutoff",      Range(0, 0.2))    = 0.001

        [Toggle(_FOG_ADDITIONAL_LIGHTS)] _AdditionalLightScatter ("Additional Light Scatter", Float) = 1
        _AdditionalLightMaxDistance   ("Add Light Max Distance",    Float)            = 50.0
        _LightMinContribution         ("Light Min Contribution",    Range(0, 1))      = 0.0

        [Toggle(_WATER_CLIP)] _ClipBelowWater ("Clip Below Water",  Float)            = 1
        _BilateralDepthSensitivity    ("Upsample Depth Sensitivity", Range(1, 200))   = 40.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        #define FOG_MAX_STEPS 32
        #define FOG_ADDITIONAL_LIGHT_STRIDE 2

        TEXTURE2D(_HeightMaskTexture);
        TEXTURE2D_X(_VolumetricFogTexture);

        float  _WaterLevel;
        float4 _VolumetricFogTexture_TexelSize;

        CBUFFER_START(UnityPerMaterial)
            float4 _HeightMaskTexture_ST;
            half4  _Color;
            half4  _ShadowColor;
            half4  _SelfShadowColor;
            half   _Density;
            float  _MaxDistance;
            half   _Anisotropy;
            half   _HeightMaskBlend;
            float  _HeightMaskLength;
            half   _HeightMaskFalloff;
            half   _HeightMaskRemapMin;
            half   _HeightMaskRemapMax;
            half   _HeightMaskTextureAmplitude;
            half   _HeightMaskTextureScale;
            half2  _HeightMaskTextureAnimation;
            float  _MainLightSelfShadowDistance;
            half   _SelfShadowPower;
            half   _SelfShadowRemapMin;
            half   _SelfShadowRemapMax;
            float  _RayStepCount;
            half   _TransmittanceCutoff;
            float  _AdditionalLightScatter;
            float  _AdditionalLightMaxDistance;
            half   _LightMinContribution;
            float  _ClipBelowWater;
            float  _BilateralDepthSensitivity;
        CBUFFER_END

        bool IsSkyDepth(float rawDepth)
        {
            #if UNITY_REVERSED_Z
                return rawDepth <= 1e-6;
            #else
                return rawDepth >= 1.0 - 1e-6;
            #endif
        }

        half ComputeWaterClipMask(float2 uvSS)
        {
            #if defined(_WATER_CLIP)
                #if UNITY_REVERSED_Z
                    const float nearDeviceDepth = 1.0;
                #else
                    const float nearDeviceDepth = 0.0;
                #endif
                float3 nearWS = ComputeWorldSpacePosition(uvSS, nearDeviceDepth, UNITY_MATRIX_I_VP);
                return (half)smoothstep(-0.02, 0.02, nearWS.y - _WaterLevel);
            #else
                return 1.0h;
            #endif
        }

        half EvaluateDensity(float3 positionWS, float2 uvScale, float2 uvOffset)
        {
            #if defined(_WATER_CLIP)
                if (positionWS.y <= _WaterLevel)
                    return 0.0h;
            #endif

            float2 uv = positionWS.xz * uvScale - uvOffset;
            half s = SAMPLE_TEXTURE2D_LOD(_HeightMaskTexture, sampler_LinearRepeat, uv, 0).r;
            s = (s * 2.0h - 1.0h) * _HeightMaskTextureAmplitude;

            float heightAbove = (positionWS.y - (float)s) - _HeightMaskLength;
            half expFalloff = (half)exp(-max(heightAbove, 0.0) * (float)_HeightMaskFalloff);
            half remapMax = max(_HeightMaskRemapMax, _HeightMaskRemapMin + 1e-4h);
            half heightMask = smoothstep(_HeightMaskRemapMin, remapMax, expFalloff);

            return lerp(1.0h, heightMask, _HeightMaskBlend);
        }

        half ComputeSelfShadow(float3 positionWS, float3 lightDir, float dist, float2 uvScale, float2 uvOffset)
        {
            float opticalDepth = 0.0;
            float stepSize = dist * 0.5;

            UNITY_UNROLL
            for (int j = 1; j <= 2; ++j)
            {
                float3 sp = positionWS + lightDir * ((float)j * stepSize);
                opticalDepth += (float)(EvaluateDensity(sp, uvScale, uvOffset) * _Density) * stepSize;
            }

            return exp(-(half)opticalDepth);
        }

        half HenyeyGreensteinNormalized(half cosTheta, half g)
        {
            half g2 = g * g;
            half denom = max(1.0h + g2 - 2.0h * g * cosTheta, 1e-3h);
            return (1.0h - g2) / (denom * sqrt(denom));
        }

        half3 AdditionalLightScatter(uint lightIndex, float3 pos)
        {
            Light al = GetAdditionalLight(lightIndex, pos);
            half alDist = (half)al.distanceAttenuation;
            half3 alColor = (half3)al.color;
            half maxChan = max(alColor.r, max(alColor.g, alColor.b));

            if (alDist <= 1e-5h || maxChan * alDist < _LightMinContribution)
                return 0.0h;

            #if defined(_ADDITIONAL_LIGHT_SHADOWS)
                half alShadow = (half)AdditionalLightShadow(lightIndex, pos, al.direction, half4(1, 1, 1, 1), half4(1, 1, 1, 1));
                alShadow = lerp(1.0h, alShadow, _ShadowColor.a);
                alColor *= alShadow;
            #endif

            return alColor * alDist;
        }

        bool TryGetFogCeiling(out float yTop)
        {
            yTop = 0.0;
            if ((float)_HeightMaskBlend < 0.9999 || (float)_HeightMaskFalloff < 1e-3)
                return false;

            float eMin = (float)_HeightMaskRemapMin;
            float eMax = max((float)_HeightMaskRemapMax, eMin + 1e-4);
            float eCut = clamp(eMin + 0.00577 * (eMax - eMin), 1e-4, 0.9999);
            float hCut = -log(eCut) / (float)_HeightMaskFalloff;

            yTop = _HeightMaskLength + abs((float)_HeightMaskTextureAmplitude) + hCut;
            return true;
        }

        half4 RaymarchVolumetricFog(float2 uvSS, float2 pixelCoord)
        {
            float rawDepth = SampleSceneDepth(uvSS);
            float3 rayOrigin = GetCameraPositionWS();
            float3 dirWS;
            float distToSurface;

            if (IsSkyDepth(rawDepth))
            {
                #if UNITY_REVERSED_Z
                    const float farDeviceDepth = 0.0;
                #else
                    const float farDeviceDepth = 1.0;
                #endif
                float3 farWS = ComputeWorldSpacePosition(uvSS, farDeviceDepth, UNITY_MATRIX_I_VP);
                dirWS = normalize(farWS - rayOrigin);
                distToSurface = _MaxDistance;
            }
            else
            {
                float3 surfaceWS = ComputeWorldSpacePosition(uvSS, rawDepth, UNITY_MATRIX_I_VP);
                float3 toSurface = surfaceWS - rayOrigin;
                distToSurface = length(toSurface);
                dirWS = toSurface / max(distToSurface, 1e-5);
            }

            float tStart = 0.0;
            float maxDist = min(distToSurface, _MaxDistance);

            #if defined(_WATER_CLIP)
                if (rayOrigin.y >= _WaterLevel)
                {
                    if (dirWS.y < -1e-5)
                    {
                        float tWater = (_WaterLevel - rayOrigin.y) / dirWS.y;
                        maxDist = min(maxDist, tWater);
                    }
                }
                else
                {
                    if (dirWS.y > 1e-5)
                    {
                        tStart = (_WaterLevel - rayOrigin.y) / dirWS.y;
                    }
                    else
                    {
                        return half4(0.0h, 0.0h, 0.0h, 1.0h);
                    }
                }
            #endif

            float yTop;
            if (TryGetFogCeiling(yTop))
            {
                float oy = rayOrigin.y;
                if (oy < yTop)
                {
                    if (dirWS.y > 1e-5)
                        maxDist = min(maxDist, (yTop - oy) / dirWS.y);
                }
                else
                {
                    if (dirWS.y >= -1e-5)
                        return half4(0.0h, 0.0h, 0.0h, 1.0h);

                    tStart = max(tStart, (yTop - oy) / dirWS.y);
                }
            }

            float segLen = maxDist - tStart;
            if (segLen <= 1e-3)
                return half4(0.0h, 0.0h, 0.0h, 1.0h);

            float2 uvScale = (float)_HeightMaskTextureScale * _HeightMaskTexture_ST.xy;
            float2 uvOffset = _HeightMaskTexture_ST.zw + (float2)_HeightMaskTextureAnimation * _Time.y;
            half noise = (half)InterleavedGradientNoise(pixelCoord, 0);

            Light mainLight = GetMainLight();
            bool mainLightOn = max(mainLight.color.r, max(mainLight.color.g, mainLight.color.b)) > 0.001h;
            half phase = mainLightOn ? HenyeyGreensteinNormalized((half)dot(dirWS, mainLight.direction), _Anisotropy) : 1.0h;
            half3 litSunColor = (half3)mainLight.color * phase;

            bool useSelfShadow = mainLightOn && (_SelfShadowColor.a > 0.001h) && (_MainLightSelfShadowDistance > 0.001);
            half ssRemapMax = max(_SelfShadowRemapMax, _SelfShadowRemapMin + 1e-4h);

            int stepCount = clamp((int)(_RayStepCount + 0.5), 1, FOG_MAX_STEPS);
            float invStepCount = rcp((float)stepCount);

            #if defined(_FOG_ADDITIONAL_LIGHTS) && USE_CLUSTER_LIGHT_LOOP
                InputData inputData = (InputData)0;
                inputData.normalizedScreenSpaceUV = uvSS;
                uint lightCount = GetAdditionalLightsCount();
                float addLightMaxT = _AdditionalLightMaxDistance;
                half3 cachedAddLight = 0.0h;
                int   lastAddLightStep = -FOG_ADDITIONAL_LIGHT_STRIDE;
            #endif

            half3 lighting = 0.0h;
            half  transmittance = 1.0h;
            half  cachedSelfShadow = 1.0h;
            int   lastSelfShadowStep = -2;
            float prevD = 0.0;

            [loop]
            for (int i = 0; i < stepCount; ++i)
            {
                float u1 = (float)(i + 1) * invStepCount;
                float nextD = u1 * u1 * segLen;
                float stepLength = nextD - prevD;
                float sampleT = tStart + lerp(prevD, nextD, (float)noise);
                prevD = nextD;

                float3 pos = rayOrigin + dirWS * sampleT;
                half density = EvaluateDensity(pos, uvScale, uvOffset);
                if (density <= 0.0001h)
                    continue;

                half opticalDepth = density * _Density * (half)stepLength;
                half stepTrans = exp(-opticalDepth);
                half scatter = 1.0h - stepTrans;

                half3 stepLight = 0.0h;

                if (mainLightOn)
                {
                    float4 shadowCoord = TransformWorldToShadowCoord(pos);
                    half shadow = (half)MainLightShadow(shadowCoord, pos, half4(1, 1, 1, 1), _MainLightOcclusionProbes);
                    shadow = lerp(1.0h, shadow, _ShadowColor.a);

                    half3 ml = lerp(_ShadowColor.rgb, litSunColor, shadow);

                    if (useSelfShadow && shadow > 0.001h)
                    {
                        if (i - lastSelfShadowStep >= 2)
                        {
                            half ss = ComputeSelfShadow(pos, mainLight.direction, _MainLightSelfShadowDistance, uvScale, uvOffset);
                            ss = lerp(1.0h, ss, shadow * _SelfShadowColor.a);
                            ss = smoothstep(_SelfShadowRemapMin, ssRemapMax, ss);
                            cachedSelfShadow = pow(max(ss, 1e-4h), _SelfShadowPower);
                            lastSelfShadowStep = i;
                        }
                        ml = lerp(_SelfShadowColor.rgb, ml, cachedSelfShadow);
                    }

                    stepLight = ml;
                }

                #if defined(_FOG_ADDITIONAL_LIGHTS) && USE_CLUSTER_LIGHT_LOOP
                    if (sampleT <= addLightMaxT)
                    {
                        if (i - lastAddLightStep >= FOG_ADDITIONAL_LIGHT_STRIDE)
                        {
                            inputData.positionWS = pos;
                            half3 addLight = 0.0h;

                            UNITY_LOOP for (uint dirIndex = 0; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
                                addLight += AdditionalLightScatter(dirIndex, pos);

                            LIGHT_LOOP_BEGIN(lightCount)
                                addLight += AdditionalLightScatter(lightIndex, pos);
                            LIGHT_LOOP_END

                            cachedAddLight = addLight;
                            lastAddLightStep = i;
                        }
                        stepLight += cachedAddLight;
                    }
                #endif

                lighting += (stepLight * scatter) * transmittance;
                transmittance *= stepTrans;

                if (transmittance < _TransmittanceCutoff)
                {
                    transmittance = 0.0h;
                    break;
                }
            }

            lighting *= _Color.rgb;
            return half4(lighting, transmittance);
        }
        ENDHLSL

        Pass
        {
            Name "VolumetricFogFullRes"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragFullRes

            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma shader_feature_local_fragment _ _FOG_ADDITIONAL_LIGHTS
            #pragma shader_feature_local_fragment _ _WATER_CLIP

            half4 FragFullRes(Varyings input) : SV_Target
            {
                float2 uvSS = input.texcoord;

                if ((float)_Density < 0.0002f)
                    return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uvSS, 0);

                half waterClipMask = ComputeWaterClipMask(uvSS);
                if (waterClipMask <= 1e-4h)
                    return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uvSS, 0);

                half4 fog = RaymarchVolumetricFog(uvSS, input.positionCS.xy);
                half4 scene = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uvSS, 0);

                return half4(lerp(scene.rgb, scene.rgb * fog.a + fog.rgb, _Color.a * waterClipMask), scene.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "VolumetricFogHalfRes"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragHalfRes

            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma shader_feature_local_fragment _ _FOG_ADDITIONAL_LIGHTS
            #pragma shader_feature_local_fragment _ _WATER_CLIP

            half4 FragHalfRes(Varyings input) : SV_Target
            {
                float2 uvSS = input.texcoord;

                if ((float)_Density < 0.0002f)
                    return half4(0.0h, 0.0h, 0.0h, 1.0h);

                if (ComputeWaterClipMask(uvSS) <= 1e-4h)
                    return half4(0.0h, 0.0h, 0.0h, 1.0h);

                return RaymarchVolumetricFog(uvSS, input.positionCS.xy);
            }
            ENDHLSL
        }

        Pass
        {
            Name "VolumetricFogUpsample"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragUpsample

            #pragma shader_feature_local_fragment _ _WATER_CLIP

            half4 FragUpsample(Varyings input) : SV_Target
            {
                float2 uvSS = input.texcoord;
                half4 scene = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uvSS, 0);

                if ((float)_Density < 0.0002f)
                    return scene;

                half waterClipMask = ComputeWaterClipMask(uvSS);
                if (waterClipMask <= 1e-4h)
                    return scene;

                float centerRawDepth = SampleSceneDepth(uvSS);
                float centerDepth = IsSkyDepth(centerRawDepth)
                    ? _ProjectionParams.z
                    : LinearEyeDepth(centerRawDepth, _ZBufferParams);

                float2 halfTexel = _VolumetricFogTexture_TexelSize.xy;
                float2 minUV = halfTexel * 0.5;
                float2 maxUV = 1.0 - minUV;

                float2 f = uvSS * _VolumetricFogTexture_TexelSize.zw - 0.5;
                float2 baseTexel = floor(f);
                float2 w = frac(f);

                float2 uvs[4] = {
                    clamp((baseTexel + float2(0.5, 0.5)) * halfTexel, minUV, maxUV),
                    clamp((baseTexel + float2(1.5, 0.5)) * halfTexel, minUV, maxUV),
                    clamp((baseTexel + float2(0.5, 1.5)) * halfTexel, minUV, maxUV),
                    clamp((baseTexel + float2(1.5, 1.5)) * halfTexel, minUV, maxUV)
                };

                float bilinWeights[4] = {
                    (1.0 - w.x) * (1.0 - w.y),
                    w.x         * (1.0 - w.y),
                    (1.0 - w.x) * w.y,
                    w.x         * w.y
                };

                float4 accumFog = 0.0;
                float totalWeight = 0.0;
                float bestDiff = 1e10;
                float4 bestFog = 0.0;

                UNITY_UNROLL
                for (int k = 0; k < 4; ++k)
                {
                    float tapRawDepth = SampleSceneDepth(uvs[k]);
                    float tapDepth = IsSkyDepth(tapRawDepth)
                        ? _ProjectionParams.z
                        : LinearEyeDepth(tapRawDepth, _ZBufferParams);

                    float relDiff = abs(centerDepth - tapDepth) / max(centerDepth, 1.0);
                    float depthWeight = exp(-relDiff * _BilateralDepthSensitivity);
                    float weight = bilinWeights[k] * depthWeight;

                    float4 fogTap = (float4)SAMPLE_TEXTURE2D_X_LOD(_VolumetricFogTexture, sampler_PointClamp, uvs[k], 0);
                    accumFog += fogTap * weight;
                    totalWeight += weight;

                    if (relDiff < bestDiff)
                    {
                        bestDiff = relDiff;
                        bestFog = fogTap;
                    }
                }

                const float kFallbackEps = 1e-4;
                half4 fog = (half4)((accumFog + bestFog * kFallbackEps) * rcp(totalWeight + kFallbackEps));

                return half4(lerp(scene.rgb, scene.rgb * fog.a + fog.rgb, _Color.a * waterClipMask), scene.a);
            }
            ENDHLSL
        }
    }
}
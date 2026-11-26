Shader "The Ravine/Water/UnderwaterEffects"
{
    Properties
    {
        [NoScaleOffset] _CausticTex("Caustic Texture", 2D) = "white" {}
        _CausticStrength("Caustic Strength", Range(0, 2)) = 0.6
        _CausticScale("Caustic Scale", Float) = 0.15
        _CausticSpeed("Caustic Speed", Float) = 0.03
        _CausticDepth("Caustic Max Depth", Float) = 8.0
        _CausticFadeStart("Caustic Fade Start", Float) = 40.0
        _CausticFadeRange("Caustic Fade Range", Float) = 60.0

        [HDR] _DeepColor("Underwater Fog Color", Color) = (0.04, 0.16, 0.28, 1)
        [HDR] _Absorption("Absorption Per Meter", Color) = (0.35, 0.09, 0.05, 1)
        _AbsorptionStrength("Absorption Strength", Range(0, 2)) = 0.65
        _FogDensity("Underwater Fog Density", Range(0, 0.5)) = 0.045
        _CameraDepthDarkening("Camera Depth Darkening", Range(0, 0.5)) = 0.08
        _NightFloor("Night Floor", Range(0, 0.15)) = 0.02

        _DistortionAmp("Underwater Distortion", Range(0, 0.01)) = 0.002
        _DistortionFreq("Distortion Frequency", Float) = 12.0
        _DistortionSpeed("Distortion Speed", Float) = 2.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_CausticTex);
        SAMPLER(sampler_CausticTex);

        float _WaterLevel;
        float _CameraUnderwater;

        CBUFFER_START(UnityPerMaterial)
            float _CausticStrength;
            float _CausticScale;
            float _CausticSpeed;
            float _CausticDepth;
            float _CausticFadeStart;
            float _CausticFadeRange;
            float4 _DeepColor;
            float4 _Absorption;
            float _AbsorptionStrength;
            float _FogDensity;
            float _CameraDepthDarkening;
            float _NightFloor;
            float _DistortionAmp;
            float _DistortionFreq;
            float _DistortionSpeed;
        CBUFFER_END

        bool IsSkyDepth(float rawDepth)
        {
            #if UNITY_REVERSED_Z
                return rawDepth <= 1e-6;
            #else
                return rawDepth >= 1.0 - 1e-6;
            #endif
        }
        ENDHLSL

        Pass
        {
            Name "UnderwaterCaustics"
            Blend One One

            HLSLPROGRAM
            #pragma vertex VertCaustics
            #pragma fragment FragCaustics
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            struct CausticsVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            CausticsVaryings VertCaustics(uint vertexID : SV_VertexID)
            {
                CausticsVaryings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(vertexID);
                return output;
            }

            half4 FragCaustics(CausticsVaryings input) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(uv);
                float3 worldPos = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);

                float3 dpdx = ddx(worldPos);
                float3 dpdy = ddy(worldPos);
                float2 cUV = worldPos.xz * _CausticScale;
                float2 dcUVdx = ddx(cUV);
                float2 dcUVdy = ddy(cUV);

                if (IsSkyDepth(rawDepth))
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);

                float subDepth = _WaterLevel - worldPos.y;
                if (subDepth <= 0.0)
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);

                float3 crossN = cross(dpdx, dpdy);
                float crossLenSq = dot(crossN, crossN);
                float3 worldNormal = crossLenSq > 1e-12 ? crossN * rsqrt(crossLenSq) : float3(0.0, 1.0, 0.0);
                worldNormal *= dot(worldNormal, _WorldSpaceCameraPos - worldPos) >= 0.0 ? 1.0 : -1.0;
                float slopeMask = saturate(worldNormal.y);

                float dist = length(worldPos - _WorldSpaceCameraPos);
                float distFade = 1.0 - saturate((dist - _CausticFadeStart) / max(_CausticFadeRange, 1e-3));
                float depthFade = saturate(1.0 - subDepth / max(_CausticDepth, 1e-3)) * saturate(subDepth * 2.0);
                float mask = slopeMask * depthFade * distFade;
                if (mask <= 1e-4)
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);

                float t = _Time.y * _CausticSpeed;
                float c1 = SAMPLE_TEXTURE2D_GRAD(_CausticTex, sampler_CausticTex, cUV + float2(t, t * 0.7), dcUVdx, dcUVdy).r;
                float c2 = SAMPLE_TEXTURE2D_GRAD(_CausticTex, sampler_CausticTex, cUV * 1.3 - float2(t * 0.8, -t), dcUVdx * 1.3, dcUVdy * 1.3).r;
                float caustic = min(c1, c2) * 2.0 * _CausticStrength * mask;

                float4 shadowCoord = TransformWorldToShadowCoord(worldPos);
                Light mainLight = GetMainLight(shadowCoord);

                return half4((half3)(caustic * mainLight.color * mainLight.shadowAttenuation), 0.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "UnderwaterPost"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragUnderwater

            half4 FragUnderwater(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                #if UNITY_REVERSED_Z
                    const float nearDeviceDepth = 1.0;
                    const float farDeviceDepth = 0.0;
                #else
                    const float nearDeviceDepth = 0.0;
                    const float farDeviceDepth = 1.0;
                #endif

                float3 nearWS = ComputeWorldSpacePosition(uv, nearDeviceDepth, UNITY_MATRIX_I_VP);
                float pixelSubmerged = 1.0 - smoothstep(-0.02, 0.02, nearWS.y - _WaterLevel);
                if (pixelSubmerged <= 1e-4)
                    return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);

                float2 wave = float2(
                    sin(uv.y * _DistortionFreq + _Time.y * _DistortionSpeed),
                    cos(uv.x * _DistortionFreq + _Time.y * _DistortionSpeed * 0.8));
                float2 sampleUV = saturate(uv + wave * (_DistortionAmp * pixelSubmerged));

                half4 sceneColor = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, sampleUV, 0);
                float rawDepth = SampleSceneDepth(sampleUV);

                float3 farWS = ComputeWorldSpacePosition(sampleUV, farDeviceDepth, UNITY_MATRIX_I_VP);
                float3 rayDir = normalize(farWS - _WorldSpaceCameraPos);

                float sceneDist;
                if (IsSkyDepth(rawDepth))
                {
                    sceneDist = _ProjectionParams.z;
                }
                else
                {
                    float3 worldPos = ComputeWorldSpacePosition(sampleUV, rawDepth, UNITY_MATRIX_I_VP);
                    sceneDist = length(worldPos - _WorldSpaceCameraPos);
                }

                float underwaterDist = sceneDist;
                if (rayDir.y > 1e-4 && _WorldSpaceCameraPos.y < _WaterLevel)
                {
                    float distToSurface = (_WaterLevel - _WorldSpaceCameraPos.y) / rayDir.y;
                    underwaterDist = min(sceneDist, distToSurface);
                }

                float3 absorb = exp(-_Absorption.rgb * (underwaterDist * _AbsorptionStrength));
                float fogFactor = 1.0 - exp(-underwaterDist * _FogDensity);

                float camDepth = max(_WaterLevel - _WorldSpaceCameraPos.y, 0.0);
                float depthDarken = exp(-camDepth * _CameraDepthDarkening);

                Light mainLight = GetMainLight();
                float lightScale = Luminance(mainLight.color) + Luminance(SampleSH(float3(0, 1, 0)));
                float3 fogColor = _DeepColor.rgb * max(saturate(lightScale) * depthDarken, _NightFloor);

                float3 underwaterRgb = lerp(sceneColor.rgb * absorb, fogColor, fogFactor);
                sceneColor.rgb = (half3)lerp(sceneColor.rgb, underwaterRgb, pixelSubmerged);
                return sceneColor;
            }
            ENDHLSL
        }
    }
}
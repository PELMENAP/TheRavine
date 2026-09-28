#ifndef RAVINE_FAR_OBJECT_COMMON_INCLUDED
#define RAVINE_FAR_OBJECT_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Assets/Shaders/Include/CurvedWorld.hlsl"

StructuredBuffer<float4> _FarInstances;
float _InstanceOffset;

float3 _LightDirection;
float3 _LightPosition;

float4 FarInstance(uint svInstanceID)
{
    return _FarInstances[(uint)_InstanceOffset + svInstanceID];
}

float4 FarShadowPositionCS(float3 positionWS, float3 normalWS)
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

float3 FarLighting(float3 albedo, float3 positionWS, float3 normalWS, float4 positionCS, float translucency)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalWS = normalWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);

    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
    half4 shadowMask = half4(1, 1, 1, 1);

    Light mainLight = GetMainLight(inputData.shadowCoord);
    float mainAtten = mainLight.shadowAttenuation * mainLight.distanceAttenuation * ao.directAmbientOcclusion;
    float ndl = dot(normalWS, mainLight.direction);
    float3 lighting = mainLight.color * mainAtten * (saturate(ndl) + saturate(-ndl) * translucency);

#if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP
#if USE_CLUSTER_LIGHT_LOOP
    UNITY_LOOP for (uint dirIndex = 0; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
    {
        Light light = GetAdditionalLight(dirIndex, inputData.positionWS, shadowMask);
        lighting += light.color * light.distanceAttenuation * light.shadowAttenuation * saturate(dot(normalWS, light.direction));
    }
#endif

    uint pixelLightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
        lighting += light.color * light.distanceAttenuation * light.shadowAttenuation * saturate(dot(normalWS, light.direction));
    LIGHT_LOOP_END
#endif

    return albedo * (lighting + SampleSH(normalWS) * ao.indirectAmbientOcclusion);
}

#endif

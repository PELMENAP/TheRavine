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

struct FarShading
{
    float3 albedo;
    float  translucency; 
    float  bakedAO;
    float  aoStrength;
    float  ambientScale;
    float  ambientFlatten;
};

float3 FarAmbient(float3 normalWS, float flatten)
{
    return lerp(SampleSH(normalWS), SampleSH(float3(0.0, 0.0, 0.0)), flatten);
}

float3 FarLighting(FarShading s, float3 positionWS, float3 normalWS, float4 positionCS)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalWS = normalWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);

    AmbientOcclusionFactor ssao = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
    half4 shadowMask = half4(1, 1, 1, 1);

    float directAO   = lerp(1.0, ssao.directAmbientOcclusion   * s.bakedAO, s.aoStrength);
    float indirectAO = lerp(1.0, ssao.indirectAmbientOcclusion * s.bakedAO, s.aoStrength);

    Light mainLight = GetMainLight(inputData.shadowCoord);
    float ndl = dot(normalWS, mainLight.direction);
    float mainAtten = mainLight.shadowAttenuation * mainLight.distanceAttenuation * directAO;
    float3 lighting = mainLight.color * mainAtten * (saturate(ndl) + saturate(-ndl) * s.translucency);

#if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP
#if USE_CLUSTER_LIGHT_LOOP
    UNITY_LOOP for (uint dirIndex = 0; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
    {
        Light light = GetAdditionalLight(dirIndex, inputData.positionWS, shadowMask);
        lighting += light.color * light.distanceAttenuation * light.shadowAttenuation * directAO
                 * saturate(dot(normalWS, light.direction));
    }
#endif

    uint pixelLightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
        lighting += light.color * light.distanceAttenuation * light.shadowAttenuation * directAO
                 * saturate(dot(normalWS, light.direction));
    LIGHT_LOOP_END
#endif

    float3 ambient = FarAmbient(normalWS, s.ambientFlatten) * s.ambientScale * indirectAO;
    return s.albedo * (lighting + ambient);
}

float3 FarLighting(float3 albedo, float3 positionWS, float3 normalWS, float4 positionCS, float translucency)
{
    FarShading s = (FarShading)0;
    s.albedo = albedo;
    s.translucency = translucency;
    s.bakedAO = 1.0;
    s.aoStrength = 1.0;
    s.ambientScale = 1.0;
    s.ambientFlatten = 0.0;
    return FarLighting(s, positionWS, normalWS, positionCS);
}

#endif
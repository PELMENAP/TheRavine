#ifndef RAVINE_BIOME_TERRAIN_COMMON_INCLUDED
#define RAVINE_BIOME_TERRAIN_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Assets/Shaders/Include/CurvedWorld.hlsl"
#include "Assets/Shaders/Include/BiomeCommon.hlsl"

CBUFFER_START(UnityPerMaterial)
    float _TexScale;
    float _HeightBlendDepth;
    float _Smoothness;

    float _RockScale;
    float _RockSlopeMin;
    float _RockSlopeMax;
    float4 _RockColor;

    float _SnowTempMin;
    float _SnowTempMax;
    float _SnowFlatness;
    float4 _SnowColor;

    float _WaterLevel;
    float _SandHeight;
    float4 _SandColor;

    float _WetStrength;
    float _WetDarken;
    float _WetSmoothness;

    float _MacroScale;
    float _MacroStrength;

    float _DetailFarDistance;
CBUFFER_END

TEXTURE2D_ARRAY(_BiomeAlbedo); SAMPLER(sampler_BiomeAlbedo);
TEXTURE2D(_RockTex); SAMPLER(sampler_RockTex);
TEXTURE2D(_SnowTex); SAMPLER(sampler_SnowTex);
TEXTURE2D(_SandTex); SAMPLER(sampler_SandTex);

float3 _LightDirection;
float3 _LightPosition;

struct TerrainAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 climate : TEXCOORD2;
    float river : TEXCOORD3;
};

void TerrainVertexWorld(TerrainAttributes input, out float3 flatPositionWS, out float3 positionWS, out float3 normalWS)
{
    flatPositionWS = TransformObjectToWorld(input.positionOS.xyz);
    normalWS = TransformObjectToWorldNormal(input.normalOS);
    positionWS = BendWorld(flatPositionWS);
}

float4 TerrainShadowPositionCS(float3 positionWS, float3 normalWS)
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

float TerrainHash(float2 p)
{
    p = frac(p * float2(0.1031, 0.1030));
    p += dot(p, p.yx + 33.33);
    return frac((p.x + p.y) * p.x);
}

float TerrainValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = TerrainHash(i);
    float b = TerrainHash(i + float2(1.0, 0.0));
    float c = TerrainHash(i + float2(0.0, 1.0));
    float d = TerrainHash(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float3 SampleTriplanar(TEXTURE2D_PARAM(tex, samplerTex), float3 positionWS, float3 normalWS, float scale)
{
    float3 w = pow(abs(normalWS), 4.0);
    w /= max(w.x + w.y + w.z, 1e-4);
    float3 x = SAMPLE_TEXTURE2D(tex, samplerTex, positionWS.zy * scale).rgb;
    float3 y = SAMPLE_TEXTURE2D(tex, samplerTex, positionWS.xz * scale).rgb;
    float3 z = SAMPLE_TEXTURE2D(tex, samplerTex, positionWS.xy * scale).rgb;
    return x * w.x + y * w.y + z * w.z;
}

void TerrainSurface(float3 flatPositionWS, float3 normalWS, float2 climate, float river, float viewDistance,
    out float3 albedo, out float smoothness)
{
    bool far = viewDistance > _DetailFarDistance;
    float2 uv = flatPositionWS.xz * _TexScale;

    BiomeBlend blend = BiomeTop2(climate);
    float4 a = SAMPLE_TEXTURE2D_ARRAY(_BiomeAlbedo, sampler_BiomeAlbedo, uv, blend.layerA);
    albedo = a.rgb;

    UNITY_BRANCH
    if (!far && blend.weightB > 0.01 && blend.layerB != blend.layerA)
    {
        float4 b = SAMPLE_TEXTURE2D_ARRAY(_BiomeAlbedo, sampler_BiomeAlbedo, uv, blend.layerB);
        albedo = lerp(a.rgb, b.rgb, HeightBlend(a.a, b.a, blend.weightB, _HeightBlendDepth));
    }

    albedo *= _BiomeTint[blend.indexA].rgb * blend.weightA + _BiomeTint[blend.indexB].rgb * blend.weightB;

    float macro = TerrainValueNoise(flatPositionWS.xz * _MacroScale) * 0.65 + TerrainValueNoise(flatPositionWS.xz * _MacroScale * 3.7) * 0.35;
    albedo *= lerp(1.0 - _MacroStrength, 1.0 + _MacroStrength, macro);

    float slope = 1.0 - saturate(normalWS.y);
    float rockMask = smoothstep(_RockSlopeMin, _RockSlopeMax, slope);
    UNITY_BRANCH
    if (rockMask > 0.001)
    {
        float3 rock = far
            ? SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, flatPositionWS.xz * _RockScale).rgb
            : SampleTriplanar(TEXTURE2D_ARGS(_RockTex, sampler_RockTex), flatPositionWS, normalWS, _RockScale);
        albedo = lerp(albedo, rock * _RockColor.rgb, rockMask);
    }

    float snowMask = smoothstep(_SnowTempMax, _SnowTempMin, climate.x) * smoothstep(1.0 - _SnowFlatness, 1.0, saturate(normalWS.y));
    UNITY_BRANCH
    if (snowMask > 0.001)
    {
        float3 snow = SAMPLE_TEXTURE2D(_SnowTex, sampler_SnowTex, uv).rgb * _SnowColor.rgb;
        albedo = lerp(albedo, snow, snowMask);
    }

    float sandMask = saturate(1.0 - (flatPositionWS.y - _WaterLevel) / max(_SandHeight, 1e-3)) * (1.0 - rockMask);
    UNITY_BRANCH
    if (sandMask > 0.001)
    {
        float3 sand = SAMPLE_TEXTURE2D(_SandTex, sampler_SandTex, uv).rgb * _SandColor.rgb;
        albedo = lerp(albedo, sand, sandMask);
    }

    float wet = saturate(river * _WetStrength) * (1.0 - snowMask);
    albedo *= lerp(1.0, _WetDarken, wet);
    smoothness = lerp(_Smoothness, _WetSmoothness, wet);
}

#endif

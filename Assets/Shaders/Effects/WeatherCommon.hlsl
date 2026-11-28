#ifndef RAVINE_WEATHER_COMMON_INCLUDED
#define RAVINE_WEATHER_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

TEXTURE2D(_WeatherMap);
SAMPLER(sampler_WeatherMap);
float4 _WeatherRect;
float4 _WeatherWind;
float  _LightningFlash;
float4 _WeatherShadow;
TEXTURE2D(_CloudShadowMap);
SAMPLER(sampler_CloudShadowMap);
float4 _CloudShadowRect;

static const float2 WeatherCorners[6] =
{
    float2(0.0, 0.0), float2(1.0, 0.0), float2(1.0, 1.0),
    float2(0.0, 0.0), float2(1.0, 1.0), float2(0.0, 1.0)
};

struct WeatherSample
{
    float rain;
    float cloud;
    float charge;
    float wetness;
};

float WeatherWindowWeight(float2 xz)
{
    float side = _WeatherRect.w;
    float2 local = (xz - _WeatherRect.xy) / max(_WeatherRect.z, 1e-3);
    float2 edge = saturate(min(local - 0.5, side - 0.5 - local));
    return side > 0.5 ? edge.x * edge.y : 0.0;
}

WeatherSample SampleWeather(float3 positionWS)
{
    WeatherSample w = (WeatherSample)0;
    float weight = WeatherWindowWeight(positionWS.xz);
    if (weight <= 0.0) return w;

    float2 uv = positionWS.xz / (_WeatherRect.z * _WeatherRect.w);
    float4 t = SAMPLE_TEXTURE2D_LOD(_WeatherMap, sampler_WeatherMap, uv, 0.0) * weight;
    w.rain    = t.x;
    w.cloud   = t.y;
    w.charge  = t.z;
    w.wetness = t.w;
    return w;
}

float WeatherCloudShade(float3 positionWS)
{
    float3 toSun = _MainLightPosition.xyz;
    float height = max(_WeatherShadow.y - positionWS.y, 0.0);
    float2 at = positionWS.xz + toSun.xz / max(toSun.y, 0.15) * height;
    if (_CloudShadowRect.z <= 0.0)
        return 1.0 - saturate(SampleWeather(float3(at.x, positionWS.y, at.y)).cloud * _WeatherShadow.x);

    float2 uv = (at - _CloudShadowRect.xy) / _CloudShadowRect.z;
    float2 edge = saturate(min(uv, 1.0 - uv) * 12.0);
    float density = SAMPLE_TEXTURE2D_LOD(_CloudShadowMap, sampler_CloudShadowMap, uv, 0.0).r * (edge.x * edge.y);
    return 1.0 - saturate(density * _WeatherShadow.x);
}

uint WeatherHashU(uint x)
{
    x ^= x >> 16;
    x *= 0x7feb352dU;
    x ^= x >> 15;
    x *= 0x846ca68bU;
    x ^= x >> 16;
    return x;
}

float WeatherRand(uint id, uint salt)
{
    return (WeatherHashU(id * 0x9E3779B9U + salt * 0x85EBCA6BU) & 0xFFFFFF) / 16777216.0;
}

float WeatherLattice(float2 cell)
{
    int2 i = (int2)cell;
    return (WeatherHashU((uint)i.x * 0x8da6b343U ^ (uint)i.y * 0xd8163841U) & 0xFFFF) / 65535.0;
}

float WeatherValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = WeatherLattice(i);
    float b = WeatherLattice(i + float2(1.0, 0.0));
    float c = WeatherLattice(i + float2(0.0, 1.0));
    float d = WeatherLattice(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float WeatherFbm(float2 p)
{
    return WeatherValueNoise(p) * 0.6 + WeatherValueNoise(p * 2.07 + 5.3) * 0.28 + WeatherValueNoise(p * 4.19 + 11.7) * 0.12;
}

float WeatherStrokeIntensity(float age, float seed, float duration)
{
    if (duration <= 0.0 || age < 0.0 || age >= duration) return 0.0;

    float u = age / duration;
    float a = u / 0.035;
    float b = (u - (0.22 + 0.1 * seed)) / 0.035;
    float c = (u - (0.52 + 0.12 * seed)) / 0.035;
    float strokes = exp(-a * a) + 0.7 * exp(-b * b) + 0.5 * exp(-c * c);
    float glow = 0.35 * exp(-u * 5.0);
    float fade = 1.0 - smoothstep(0.8, 1.0, u);
    return saturate(max(strokes, glow)) * fade;
}

float4 WeatherCulled()
{
    return float4(0.0, 0.0, -2.0, 1.0);
}

half3 WeatherAmbient()
{
    return SampleSH(half3(0.0, 1.0, 0.0));
}

#endif

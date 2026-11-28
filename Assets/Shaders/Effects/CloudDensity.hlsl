#ifndef RAVINE_CLOUD_DENSITY_INCLUDED
#define RAVINE_CLOUD_DENSITY_INCLUDED

#include "Assets/Shaders/Effects/WeatherCommon.hlsl"

TEXTURE2D(_CloudNoise);
SAMPLER(sampler_CloudNoise);

static const float CloudNoiseCells = 16.0;

float CloudValue(float2 q, float lod)
{
    return SAMPLE_TEXTURE2D_LOD(_CloudNoise, sampler_CloudNoise, q * (1.0 / CloudNoiseCells), lod).r;
}

float CloudFine(float2 q, float detail, float lod)
{
    float n = CloudValue(q, lod) * 0.62 + CloudValue(q * 2.13 + 5.3, lod) * 0.38;
    if (detail > 0.01) n = lerp(n, n * 0.78 + CloudValue(q * 4.37 + 9.1, lod) * 0.22, detail);
    return n;
}

float2 CloudDrift(float time, float scroll, float layer)
{
    return float2(0.8, 0.6) * (scroll * time * (1.0 + 0.4 * layer));
}

float CloudCover(float fair, float weatherCloud, float layer)
{
    return saturate(fair * (1.0 - 0.12 * layer) + weatherCloud);
}

float CloudDensity(float2 xz, float2 drift, float detail, float cover, float layer, float lod,
    float scale, float softness, float clump)
{
    if (cover <= 0.004) return 0.0;

    float2 p = xz + drift + layer * float2(3417.0, 7193.0);
    float fine = CloudFine(p * scale, detail, lod);
    float n;
    if (layer > 1.5)
    {
        float2 feather = float2(dot(p, float2(0.8, 0.6)), dot(p, float2(-0.6, 0.8))) * 0.0011;
        feather.y += 0.45 * CloudValue(feather * 0.6, lod) + 0.3 * sin(feather.x * 1.4);
        float strands = CloudValue(feather * float2(0.25, 5.0), lod);
        float veil = CloudValue(feather * float2(0.7, 1.5), lod);
        n = strands * 0.58 + veil * 0.32 + fine * 0.1;
    }
    else
    {
        float2 shape = p * lerp(0.0016, 0.0008, layer);
        float clustered = CloudValue(shape, lod) * 0.68 + CloudValue(shape * 2.07 + 5.3, lod) * 0.32;
        n = lerp(fine, clustered, clump);
    }

    float threshold = 1.0 - cover;
    return smoothstep(threshold - softness, threshold + softness, n) * saturate(cover * 1.25);
}

#endif

#ifndef RAVINE_BIOME_COMMON_INCLUDED
#define RAVINE_BIOME_COMMON_INCLUDED

#define RAVINE_MAX_BIOMES 16

float4 _BiomeCenters[RAVINE_MAX_BIOMES];
float4 _BiomeTint[RAVINE_MAX_BIOMES];
float4 _BiomeGrassTint[RAVINE_MAX_BIOMES];
float _BiomeCount;
float _BiomeBlendRcp2;

struct BiomeBlend
{
    uint indexA;
    uint indexB;
    float layerA;
    float layerB;
    float weightA;
    float weightB;
};

float BiomeWeight(float2 climate, uint i, out float dist2)
{
    float2 d = climate - _BiomeCenters[i].xy;
    dist2 = dot(d, d);
    float w = saturate(1.0 - dist2 * _BiomeBlendRcp2);
    return w * w * (3.0 - 2.0 * w);
}

BiomeBlend BiomeTop2(float2 climate)
{
    uint count = (uint)_BiomeCount;
    uint i1 = 0, i2 = 0, nearest = 0;
    float w1 = 0.0, w2 = 0.0, w3 = 0.0;
    float nearestDist = 1e9;

    [loop]
    for (uint i = 0; i < count; i++)
    {
        float dist2;
        float w = BiomeWeight(climate, i, dist2);

        if (dist2 < nearestDist)
        {
            nearestDist = dist2;
            nearest = i;
        }

        if (w > w1)
        {
            w3 = w2;
            w2 = w1; i2 = i1;
            w1 = w;  i1 = i;
        }
        else if (w > w2)
        {
            w3 = w2;
            w2 = w;  i2 = i;
        }
        else if (w > w3)
        {
            w3 = w;
        }
    }

    BiomeBlend blend;
    float a = w1 - w3;
    float b = w2 - w3;
    float sum = a + b;

    if (w1 <= 1e-4 || sum <= 1e-5)
    {
        i1 = nearest;
        i2 = nearest;
        a = 1.0;
        b = 0.0;
        sum = 1.0;
    }

    float rcpSum = 1.0 / sum;
    blend.indexA = i1;
    blend.indexB = i2;
    blend.layerA = _BiomeCenters[i1].z;
    blend.layerB = _BiomeCenters[i2].z;
    blend.weightA = a * rcpSum;
    blend.weightB = b * rcpSum;
    return blend;
}

float HeightBlend(float heightA, float heightB, float weightB, float depth)
{
    float ha = heightA + (1.0 - weightB);
    float hb = heightB + weightB;
    float top = max(ha, hb) - depth;
    float ba = max(ha - top, 0.0);
    float bb = max(hb - top, 0.0);
    return bb / max(ba + bb, 1e-5);
}

float3 BiomeTint(float2 climate)
{
    BiomeBlend blend = BiomeTop2(climate);
    return _BiomeTint[blend.indexA].rgb * blend.weightA + _BiomeTint[blend.indexB].rgb * blend.weightB;
}

float3 BiomeGrassTint(float2 climate)
{
    BiomeBlend blend = BiomeTop2(climate);
    return _BiomeGrassTint[blend.indexA].rgb * blend.weightA + _BiomeGrassTint[blend.indexB].rgb * blend.weightB;
}

#endif

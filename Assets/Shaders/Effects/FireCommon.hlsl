#ifndef RAVINE_FIRE_COMMON_INCLUDED
#define RAVINE_FIRE_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Assets/Shaders/Include/CurvedWorld.hlsl"

struct FireInstance
{
    float4 posSeed;
    float4 life;
    float4 normal;
};

StructuredBuffer<FireInstance> _FireInstances;
float  _FireTime;
float  _FireBurnSeconds;
float  _FireKeep;
float4 _FireWind;

static const float2 FireCorners[6] =
{
    float2(0.0, 0.0), float2(1.0, 0.0), float2(1.0, 1.0),
    float2(0.0, 0.0), float2(1.0, 1.0), float2(0.0, 1.0)
};

struct FireState
{
    float3 base;
    float3 normal;
    float  seed;
    float  heat;
    float  age;
    float  ended;
    float  burned;
};

uint FireHashU(uint x)
{
    x ^= x >> 16;
    x *= 0x7feb352dU;
    x ^= x >> 15;
    x *= 0x846ca68bU;
    x ^= x >> 16;
    return x;
}

float FireRand(float seed, uint salt)
{
    return (FireHashU(asuint(seed) ^ (salt * 0x9E3779B9U)) & 0xFFFFFF) / 16777216.0;
}

float FireLattice(float2 cell)
{
    int2 i = (int2)cell;
    return (FireHashU((uint)i.x * 0x8da6b343U ^ (uint)i.y * 0xd8163841U) & 0xFFFF) / 65535.0;
}

float FireValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = FireLattice(i);
    float b = FireLattice(i + float2(1.0, 0.0));
    float c = FireLattice(i + float2(0.0, 1.0));
    float d = FireLattice(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float FireFbm(float2 p)
{
    return FireValueNoise(p) * 0.65 + FireValueNoise(p * 2.13 + 7.1) * 0.35;
}

FireState FireLoad(uint id)
{
    FireInstance f = _FireInstances[id];
    FireState s;
    s.base   = f.posSeed.xyz;
    s.seed   = f.posSeed.w;
    s.normal = normalize(f.normal.xyz + float3(0.0, 1e-4, 0.0));
    s.age    = max(_FireTime - f.life.x, 0.0);

    if (f.life.w < 0.0)
    {
        float left  = max(f.life.y - max(_FireTime - f.life.z, 0.0), 0.0);
        float rise  = smoothstep(0.0, 0.6, s.age);
        float flare = 1.0 + 0.6 * exp(-s.age * 3.0) * rise;
        float fade  = smoothstep(0.0, max(_FireBurnSeconds * 0.3, 0.5), left);
        s.heat   = rise * flare * fade;
        s.ended  = -1.0;
        s.burned = 0.0;
    }
    else
    {
        s.ended  = max(_FireTime - f.life.w, 0.0);
        s.burned = f.life.y;
        s.heat   = (1.0 - s.burned) * saturate(1.0 - s.ended * 2.5);
    }
    return s;
}

float3 FireCameraRightXZ()
{
    float3 r = UNITY_MATRIX_V[0].xyz;
    r.y = 0.0;
    return normalize(r + float3(1e-5, 0.0, 0.0));
}

float3 FireCameraRight()
{
    return UNITY_MATRIX_V[0].xyz;
}

float3 FireCameraUp()
{
    return UNITY_MATRIX_V[1].xyz;
}

void FireGroundFrame(float3 normal, out float3 tangent, out float3 bitangent)
{
    tangent   = normalize(cross(float3(0.0, 0.0, 1.0), normal) + float3(1e-5, 0.0, 0.0));
    bitangent = cross(normal, tangent);
}

float2 FireRotate(float2 v, float angle)
{
    float s, c;
    sincos(angle, s, c);
    return float2(v.x * c - v.y * s, v.x * s + v.y * c);
}

float4 FireCulled()
{
    return float4(0.0, 0.0, -2.0, 1.0);
}

half3 FireAmbient()
{
    Light main = GetMainLight();
    return SampleSH(half3(0.0, 1.0, 0.0)) + main.color * (saturate(main.direction.y * 0.8 + 0.2) * 0.6);
}

#endif

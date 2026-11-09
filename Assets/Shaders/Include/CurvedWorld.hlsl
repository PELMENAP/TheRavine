#ifndef RAVINE_CURVED_WORLD_INCLUDED
#define RAVINE_CURVED_WORLD_INCLUDED

#include "Assets/Shaders/Include/ViewOrientation.hlsl"

float4 _CurveParams;

float CurveArc(float dist)
{
    return max(abs(dist) - _CurveParams.y, 0.0);
}

float CurveDistance(float3 positionWS)
{
    return dot(positionWS.xz, ViewForward()) - _CurveParams.z;
}

float3 BendWorld(float3 positionWS)
{
    float arc = CurveArc(CurveDistance(positionWS));
    positionWS.y -= _CurveParams.x * arc * arc;
    return positionWS;
}

float3 TiltNormal(float3 normalWS, float c, float s)
{
    float2 forward = ViewForward();
    float along = dot(normalWS.xz, forward);
    float tilted = along * c + normalWS.y * s;
    float y = normalWS.y * c - along * s;
    normalWS.xz += forward * (tilted - along);
    normalWS.y = y;
    return normalWS;
}

float3 BendNormal(float3 positionWS, float3 normalWS)
{
    float dist = CurveDistance(positionWS);
    float slope = 2.0 * _CurveParams.x * CurveArc(dist) * (dist >= 0.0 ? 1.0 : -1.0);
    float c = rsqrt(1.0 + slope * slope);
    float s = slope * c;
    return TiltNormal(normalWS, c, s);
}

void BendWorld(inout float3 positionWS, inout float3 normalWS)
{
    normalWS = BendNormal(positionWS, normalWS);
    positionWS = BendWorld(positionWS);
}

void BendWorldExact(inout float3 positionWS, inout float3 normalWS)
{
    float k = _CurveParams.x;
    if (k <= 0.0) return;

    float2 forward = ViewForward();
    float radius = 0.5 / k;
    float dist = CurveDistance(positionWS);
    float side = dist >= 0.0 ? 1.0 : -1.0;
    float angle = CurveArc(dist) / radius;

    float s, c;
    sincos(angle, s, c);

    float r = radius + positionWS.y - _CurveParams.w;
    float along = _CurveParams.z + side * (min(abs(dist), _CurveParams.y) + r * s);
    positionWS.xz += forward * (along - dot(positionWS.xz, forward));
    positionWS.y = _CurveParams.w - radius + r * c;

    normalWS = TiltNormal(normalWS, c, side * s);
}

#endif

#ifndef RAVINE_CURVED_WORLD_INCLUDED
#define RAVINE_CURVED_WORLD_INCLUDED

float4 _CurveParams;
float4 _CurveFocus;

float CurveArc(float dist)
{
    return max(dist - _CurveParams.y, 0.0);
}

float2 CurveRadial(float2 positionXZ, out float dist)
{
    float2 delta = positionXZ - _CurveFocus.xy;
    dist = length(delta);
    return dist > 1e-4 ? delta / dist : float2(0.0, 1.0);
}

float3 BendWorld(float3 positionWS)
{
    float arc = CurveArc(length(positionWS.xz - _CurveFocus.xy));
    positionWS.y -= _CurveParams.x * arc * arc;
    return positionWS;
}

float3 TiltNormal(float3 normalWS, float2 dir, float c, float s)
{
    float along = dot(normalWS.xz, dir);
    float tilted = along * c + normalWS.y * s;
    float y = normalWS.y * c - along * s;
    normalWS.xz += dir * (tilted - along);
    normalWS.y = y;
    return normalWS;
}

float3 BendNormal(float3 positionWS, float3 normalWS)
{
    float dist;
    float2 dir = CurveRadial(positionWS.xz, dist);
    float slope = 2.0 * _CurveParams.x * CurveArc(dist);
    float c = rsqrt(1.0 + slope * slope);
    float s = slope * c;
    return TiltNormal(normalWS, dir, c, s);
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

    float radius = 0.5 / k;
    float dist;
    float2 dir = CurveRadial(positionWS.xz, dist);
    float angle = CurveArc(dist) / radius;

    float s, c;
    sincos(angle, s, c);

    float r = radius + positionWS.y - _CurveFocus.z;
    float reach = min(dist, _CurveParams.y) + r * s;
    positionWS.xz = _CurveFocus.xy + dir * reach;
    positionWS.y = _CurveFocus.z - radius + r * c;

    normalWS = TiltNormal(normalWS, dir, c, s);
}

#endif

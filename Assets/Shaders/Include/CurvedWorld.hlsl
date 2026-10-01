#ifndef RAVINE_CURVED_WORLD_INCLUDED
#define RAVINE_CURVED_WORLD_INCLUDED

float4 _CurveParams;
float4 _CurveAxis;
float _ViewFacing;

float CurveDistance(float3 positionWS)
{
    return dot(positionWS.xz - _CurveParams.zw, _CurveAxis.xy);
}

float CurveArc(float distance)
{
    return max(abs(distance) - _CurveParams.y, 0.0);
}

float3 BendWorld(float3 positionWS)
{
    float arc = CurveArc(CurveDistance(positionWS));
    positionWS.y -= _CurveParams.x * arc * arc;
    return positionWS;
}

float3 BendNormal(float3 positionWS, float3 normalWS)
{
    float d = CurveDistance(positionWS);
    float slope = 2.0 * _CurveParams.x * CurveArc(d) * (d >= 0.0 ? 1.0 : -1.0);
    float c = rsqrt(1.0 + slope * slope);
    float s = slope * c;

    float3 axis = float3(_CurveAxis.x, 0.0, _CurveAxis.y);
    float along = dot(normalWS, axis);
    float3 side = normalWS - axis * along;

    float up = side.y;
    side.y = 0.0;
    return side + axis * (along * c + up * s) + float3(0.0, up * c - along * s, 0.0);
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
    float d = CurveDistance(positionWS);
    float side = d >= 0.0 ? 1.0 : -1.0;
    float angle = CurveArc(d) / radius;

    float s, c;
    sincos(angle, s, c);

    float3 axis = float3(_CurveAxis.x, 0.0, _CurveAxis.y);
    float r = radius + positionWS.y - _CurveAxis.z;
    float along = side * (min(abs(d), _CurveParams.y) + r * s);

    positionWS += axis * (along - d);
    positionWS.y = _CurveAxis.z - radius + r * c;

    float sa = side * s;
    float nAlong = dot(normalWS, axis);
    float3 nSide = normalWS - axis * nAlong - float3(0.0, normalWS.y, 0.0);
    normalWS = nSide + axis * (nAlong * c + normalWS.y * sa) + float3(0.0, normalWS.y * c - nAlong * sa, 0.0);
}

#endif

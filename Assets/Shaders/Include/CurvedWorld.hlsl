#ifndef RAVINE_CURVED_WORLD_INCLUDED
#define RAVINE_CURVED_WORLD_INCLUDED

float4 _CurveParams;
float _ViewFacing;

float3 BendWorld(float3 positionWS)
{
    float d = max(abs(positionWS.z - _CurveParams.z) - _CurveParams.y, 0.0);
    positionWS.y -= _CurveParams.x * d * d;
    return positionWS;
}

void BendWorldExact(inout float3 positionWS, inout float3 normalWS)
{
    float k = _CurveParams.x;
    if (k <= 0.0) return;

    float radius = 0.5 / k;
    float dz = positionWS.z - _CurveParams.z;
    float side = dz >= 0.0 ? 1.0 : -1.0;
    float arc = max(abs(dz) - _CurveParams.y, 0.0);
    float angle = arc / radius;

    float s, c;
    sincos(angle, s, c);

    float r = radius + positionWS.y - _CurveParams.w;
    positionWS.z = _CurveParams.z + side * (min(abs(dz), _CurveParams.y) + r * s);
    positionWS.y = _CurveParams.w - radius + r * c;

    float sa = side * s;
    normalWS = float3(normalWS.x, normalWS.y * c - normalWS.z * sa, normalWS.y * sa + normalWS.z * c);
}

#endif

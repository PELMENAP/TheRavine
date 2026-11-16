#ifndef RAVINE_PROFILE_INCLUDED
#define RAVINE_PROFILE_INCLUDED

float4 _RavineProfile;

float RavineBlend(float z)
{
    float t = saturate((abs(z) - _RavineProfile.x) * _RavineProfile.y);
    return _RavineProfile.z > 0.0 ? t : 0.0;
}

float RavineElevation(float z)
{
    return _RavineProfile.z * RavineBlend(z);
}

#endif

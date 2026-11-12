#ifndef RAVINE_GRASS_INSTANCE_INCLUDED
#define RAVINE_GRASS_INSTANCE_INCLUDED

struct InstanceData
{
    float3 position;
    float  rotation;
    float  scaleXZ;
    float  scaleY;
    uint   packedColor;
};

#endif

#ifndef RAVINE_VIEW_ORIENTATION_INCLUDED
#define RAVINE_VIEW_ORIENTATION_INCLUDED

float4 _ViewBasis;
float _ViewColumn;

float2 ViewForward()
{
    return _ViewBasis.xy;
}

float2 ViewRight()
{
    return _ViewBasis.zw;
}

#endif

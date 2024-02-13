using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

[BurstCompile]
public struct MoveMobsJob : IJobParallelForTransform
{
    public float deltaTime;
    [ReadOnly] public NativeArray<float> moveSpeeds;
    [ReadOnly] public NativeArray<float2> moveDirections;

    public void Execute(int index, TransformAccess transform)
    {
        float2 direction = moveDirections[index] * moveSpeeds[index] * deltaTime;
        transform.position += new Vector3(direction.x, direction.y, 0);
    }
}
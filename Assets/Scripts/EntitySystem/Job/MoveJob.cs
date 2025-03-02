using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace TheRavine.EntityControl
{
    [BurstCompile]
    public struct MoveJob : IJobParallelForTransform
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<float2> Velocities;
        [WriteOnly] public NativeArray<float2> Positions;
        [ReadOnly] public NativeArray<bool> IsMoving;
        [ReadOnly] public NativeArray<float2> Accelerations;
        [ReadOnly] public float DeltaTime, VelocityLimit;
        [ReadOnly] public Quaternion FlipRotation, IdentityRotation;
        public void Execute(int index, TransformAccess transform)
        {
            float4 velocityAcceleration = new float4(Velocities[index], Accelerations[index]);
            float4 deltaTimeVec = new float4(DeltaTime, DeltaTime, DeltaTime, DeltaTime);

            float4 newVelocity = velocityAcceleration + new float4(Accelerations[index], 0, 0) * deltaTimeVec;

            float velocityLength = math.length(newVelocity.xy);
            newVelocity.xy = math.select(float2.zero, newVelocity.xy / velocityLength * math.clamp(velocityLength, 1, VelocityLimit), velocityLength > 0);

            float3 position = transform.position;
            position.xy += newVelocity.xy * DeltaTime * math.select(0f, 1f, IsMoving[index]);
            transform.position = position;

            float flipFactor = 0.5f * (1f - math.step(0f, newVelocity.x));
            transform.rotation = math.slerp(IdentityRotation, FlipRotation, flipFactor * math.select(0f, 1f, IsMoving[index]));

            Positions[index] = position.xy;
            Velocities[index] = newVelocity.xy * math.select(0f, 1f, IsMoving[index]);
        }
    }

    [BurstCompile]
    public struct MoveMobsJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<float2> Velocities;
        [ReadOnly] public float DeltaTime;
        public void Execute(int index, TransformAccess transform)
        {
            float3 position = transform.position;
            position.xy += Velocities[index].xy * DeltaTime;
            transform.position = position;
        }
    }
}
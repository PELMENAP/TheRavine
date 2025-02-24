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
        public NativeArray<float2> Velocities;
        [WriteOnly] public NativeArray<float2> Positions;
        [ReadOnly] public NativeArray<bool> IsMoving;
        [ReadOnly] public NativeArray<float2> Accelerations;
        [ReadOnly] public float DeltaTime, VelocityLimit;
        [ReadOnly] public Quaternion FlipRotation, IdentityRotation;
        public void Execute(int index, TransformAccess transform)
        {
            float moveMultiplier = math.select(0f, 1f, IsMoving[index]);  

            float2 velocity = Velocities[index] + Accelerations[index] * DeltaTime;
            float velocityLength = math.length(velocity);
            velocity = math.select(float2.zero, velocity / velocityLength * math.clamp(velocityLength, 1, VelocityLimit), velocityLength > 0);

            float3 position = transform.position;
            position.xy += velocity * DeltaTime * moveMultiplier;  
            transform.position = position;

            float flipFactor = 0.5f * (1f - math.step(0f, velocity.x));  
            transform.rotation = math.slerp(IdentityRotation, FlipRotation, flipFactor * moveMultiplier);

            Positions[index] = position.xy;
            Velocities[index] = velocity * moveMultiplier;
        }
    }

    [BurstCompile]
    public struct MoveMobsJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<float2> Velocities;
        [ReadOnly] public float DeltaTime;
        public void Execute(int index, TransformAccess transform)
        {
            transform.position += new Vector3(Velocities[index].x * DeltaTime, Velocities[index].y * DeltaTime, 0);
        }
    }
}
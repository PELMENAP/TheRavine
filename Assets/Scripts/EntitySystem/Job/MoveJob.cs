using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace TheRavine.EntityControl
{
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct MoveJob : IJobParallelForTransform
    {
        [NativeDisableParallelForRestriction] public NativeArray<float4> PositionsAndVelocities;
        [ReadOnly] public NativeArray<bool> IsMoving;
        [ReadOnly] public NativeArray<float2> Accelerations;
        [ReadOnly] public float DeltaTime, VelocityLimitSq, VelocityLimit;
        [ReadOnly] public Quaternion FlipRotation, IdentityRotation;

        public void Execute(int index, TransformAccess transform)
        {
            if (!IsMoving[index]) return;

            float2 acc   = Accelerations[index];
            float2 vel   = PositionsAndVelocities[index].zw;
            float2 newVel = vel + acc * DeltaTime;

            float velLenSq = math.lengthsq(newVel);
            if (velLenSq > VelocityLimitSq)
                newVel = newVel * math.rsqrt(velLenSq) * VelocityLimit;
            else if (velLenSq < 1f)
                newVel = math.normalizesafe(newVel);


            float3 position = transform.position;
            position.x += newVel.x * DeltaTime;
            position.z += newVel.y * DeltaTime;

            transform.position = position;

            if (math.abs(newVel.x) > 0.1f)
                transform.rotation = newVel.x < 0 ? FlipRotation : IdentityRotation;

            PositionsAndVelocities[index] = new float4(position.x, position.z, newVel);
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
            position.x += Velocities[index].x * DeltaTime;
            position.z += Velocities[index].y * DeltaTime;
            transform.position = position;
        }
    }
}
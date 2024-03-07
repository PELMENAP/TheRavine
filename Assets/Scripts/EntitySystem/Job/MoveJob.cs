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
        [ReadOnly] public Vector3 Flip;
        public void Execute(int index, TransformAccess transform)
        {
            if (!IsMoving[index])
                return;
            float2 velocity = new float2(Velocities[index].x + Accelerations[index].x * DeltaTime, Velocities[index].y + Accelerations[index].y * DeltaTime);
            velocity = math.normalize(velocity) * math.clamp(math.length(velocity), 1, VelocityLimit);
            transform.position += new Vector3(Velocities[index].x * DeltaTime, Velocities[index].y * DeltaTime, 0);
            if (velocity.x < 0)
                transform.rotation = Quaternion.Euler(Flip);
            else
                transform.rotation = Quaternion.identity;
            Positions[index] = new float2(transform.position.x, transform.position.y);
            Velocities[index] = velocity;
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
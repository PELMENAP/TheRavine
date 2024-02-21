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
        [WriteOnly]
        public NativeArray<float2> Positions;
        [ReadOnly]
        public NativeArray<bool> IsMoving;
        [ReadOnly]
        public NativeArray<float2> Accelerations;
        [ReadOnly]
        public float DeltaTime, VelocityLimit;

        public void Execute(int index, TransformAccess transform)
        {
            if (!IsMoving[index])
                return;
            var velocity = Velocities[index] + Accelerations[index] * DeltaTime;
            var direction = math.normalize(velocity);
            velocity = direction * math.clamp(math.length(velocity), 1, VelocityLimit);
            transform.position += new Vector3(velocity.x, velocity.y, 0) * DeltaTime;
            // transform.rotation = Quaternion.LookRotation(direction);

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
            transform.position += new Vector3(Velocities[index].x, Velocities[index].y, 0) * DeltaTime;
        }
    }
}
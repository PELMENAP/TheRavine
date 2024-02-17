using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

[BurstCompile]
public struct MoveJob : IJobParallelForTransform
{
    public NativeArray<Vector3> Positions;
    public NativeArray<Vector3> Velocities;
    [ReadOnly]
    public NativeArray<bool> IsMoving;
    [ReadOnly]
    public NativeArray<Vector3> Accelerations;
    public float DeltaTime;
    public float VelocityLimit;

    public void Execute(int index, TransformAccess transform)
    {
        if (!IsMoving[index])
            return;
        var velocity = Velocities[index] + Accelerations[index] * DeltaTime;
        var direction = velocity.normalized;
        velocity = direction * math.clamp(velocity.magnitude, 1, VelocityLimit);
        transform.position += velocity * DeltaTime;
        // transform.rotation = Quaternion.LookRotation(direction);

        Positions[index] = transform.position;
        Velocities[index] = velocity;
    }
}
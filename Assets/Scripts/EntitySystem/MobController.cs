using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Mathematics;
using Unity.Jobs;
public class MobsController : MonoBehaviour
{
    public Transform[] mobTransforms;
    public float[] moveSpeed;
    public Vector2[] moveDirection;
    private TransformAccessArray transformAccessArray;
    private NativeArray<float> moveSpeeds;
    private NativeArray<float2> moveDirections;

    private void Start()
    {
        transformAccessArray = new TransformAccessArray(mobTransforms.Length);
        moveSpeeds = new NativeArray<float>(moveSpeed.Length, Allocator.Persistent);
        moveDirections = new NativeArray<float2>(moveDirection.Length, Allocator.Persistent);

        for (int i = 0; i < mobTransforms.Length; i++)
        {
            transformAccessArray.Add(mobTransforms[i]);
            moveSpeeds[i] = moveSpeed[i];
            moveDirections[i] = moveDirection[i];
        }
    }

    private void FixedUpdate()
    {
        MoveMobsJob moveMobsJob = new MoveMobsJob
        {
            deltaTime = Time.deltaTime,
            moveSpeeds = moveSpeeds,
            moveDirections = moveDirections
        };

        JobHandle jobHandle = moveMobsJob.Schedule(transformAccessArray);
        jobHandle.Complete();
    }

    private void OnDestroy()
    {
        transformAccessArray.Dispose();
        moveSpeeds.Dispose();
        moveDirections.Dispose();
    }
}
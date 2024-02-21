using Unity.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Mathematics;
using Unity.Jobs;

using TheRavine.Services;

namespace TheRavine.EntityControl
{
    public class MobController : MonoBehaviour, ISetAble
    {
        private List<AEntity> mobEntities;
        private TransformAccessArray transformAccessArray;
        private NativeArray<float2> velocities;
        private int EntityCount = 0;
        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            callback?.Invoke();
        }
        public void UpdateCurrentMobs(List<AEntity> _mobEntities)
        {
            mobEntities = _mobEntities;
            EntityCount = mobEntities.Count;
            transformAccessArray = new TransformAccessArray(EntityCount);
            velocities = new NativeArray<float2>(EntityCount, Allocator.Persistent);
            for (ushort i = 0; i < EntityCount; i++)
            {
                transformAccessArray.Add(mobEntities[i].transform);
            }
        }
        private void Update()
        {
            if (EntityCount < 1)
                return;
            for (int i = 0; i < EntityCount; i++)
            {
                mobEntities[i].UpdateEntityCycle();
                velocities[i] = mobEntities[i].GetEntityVelocity(); ;
            }

            MoveMobsJob moveMobsJob = new MoveMobsJob
            {
                Velocities = velocities,
                DeltaTime = Time.deltaTime
            };

            JobHandle jobHandle = moveMobsJob.Schedule(transformAccessArray);
            jobHandle.Complete();
        }
        public void BreakUp()
        {
            OnDestroy();
        }
        private void OnDestroy()
        {
            mobEntities.Clear();
            transformAccessArray.Dispose();
            velocities.Dispose();
        }
    }
}
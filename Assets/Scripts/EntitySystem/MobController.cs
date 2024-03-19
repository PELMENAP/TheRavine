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
        public ushort EntityCount = 0;
        private AEntity[] mobEntities;
        private TransformAccessArray transformAccessArray;
        private NativeArray<float2> velocities;
        public NativeArray<bool> isMoving;
        private ushort maxEntities = 1000;
        private MoveMobsJob moveMobsJob;
        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            mobEntities = new AEntity[maxEntities];
            transformAccessArray = new TransformAccessArray(maxEntities);
            velocities = new NativeArray<float2>(maxEntities, Allocator.Persistent);
            isMoving = new NativeArray<bool>(maxEntities, Allocator.Persistent);
            for (ushort i = 0; i < maxEntities; i++)
                transformAccessArray.Add(this.transform);
            moveMobsJob = new MoveMobsJob
            {
                Velocities = velocities,
                DeltaTime = Time.deltaTime
            };
            callback?.Invoke();
        }

        public void AddMobToUpdate(AEntity mobEntity)
        {
            int index = System.Array.IndexOf(mobEntities, mobEntity);
            if (index == -1)
                for (ushort i = 0; i < maxEntities; i++)
                {
                    if (mobEntities[i] == null)
                    {
                        mobEntities[i] = mobEntity;
                        transformAccessArray[i] = mobEntity.transform;
                        EntityCount++;
                        return;
                    }
                }
        }

        public void RemoveMobFromUpdate(AEntity mobEntity)
        {
            int index = System.Array.IndexOf(mobEntities, mobEntity);
            if (index != -1)
            {
                mobEntities[index] = null;
                EntityCount--;
            }
        }
        private void Update()
        {
            if (EntityCount < 1)
                return;
            for (ushort i = 0; i < maxEntities; i++)
                if (mobEntities[i] != null)
                {
                    mobEntities[i].UpdateEntityCycle();
                    velocities[i] = mobEntities[i].GetEntityVelocity();
                }
                else
                    velocities[i] = float2.zero;
            JobHandle jobHandle = moveMobsJob.Schedule(transformAccessArray);
            jobHandle.Complete();
        }
        public void BreakUp()
        {
            mobEntities = null;
            transformAccessArray.Dispose();
            velocities.Dispose();
            isMoving.Dispose();
        }
    }
}
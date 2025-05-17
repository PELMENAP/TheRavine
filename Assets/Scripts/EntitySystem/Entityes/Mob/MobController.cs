using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Mathematics;
using Unity.Jobs;

using TheRavine.Services;

namespace TheRavine.EntityControl
{
    public class MobController : MonoBehaviour, ISetAble
    {
        private ILogger logger;
        public int GetEntityCount() => mobEntities.Length;
        private NativeList<IntPtr> mobEntities;
        private List<GCHandle> gcHandles;
        private TransformAccessArray transformAccessArray;
        private NativeArray<float2> velocities;

        private MoveMobsJob moveMobsJob;
        private JobHandle moveJobHandle;

        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            logger = locator.GetLogger();
            logger.LogInfo("MobController service is available now");
            
            mobEntities = new NativeList<IntPtr>(Allocator.Persistent);
            gcHandles = new List<GCHandle>();
            transformAccessArray = new TransformAccessArray(0);
            velocities = new NativeArray<float2>(0, Allocator.Persistent);

            callback?.Invoke();
        }

        public void AddMobToUpdate(AEntity mobEntity)
        {
            if (mobEntity == null)
            {
                logger.LogError("Mob entity is null!");
                return;
            }

            try
            {
                GCHandle handle = GCHandle.Alloc(mobEntity, GCHandleType.Pinned);
                gcHandles.Add(handle);
                mobEntities.Add(GCHandle.ToIntPtr(handle));

                var transformComponent = mobEntity.GetEntityComponent<TransformComponent>();

                if (transformComponent == null)
                {
                    logger.LogError("TransformComponent is missing in mob entity!");
                    return;
                }

                transformAccessArray.Add(transformComponent.GetEntityTransform());
                ReallocateVelocities();
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to add mob to update: {ex.Message}");
            }
        }

        public void RemoveMobFromUpdate(AEntity mobEntity)
        {
            if (mobEntity == null)
            {
                logger.LogError("Mob entity is null!");
                return;
            }

            for (int i = 0; i < mobEntities.Length; i++)
            {
                GCHandle handle = GCHandle.FromIntPtr(mobEntities[i]);
                if ((AEntity)handle.Target == mobEntity)
                {
                    try
                    {
                        handle.Free();
                        gcHandles.Remove(handle);

                        mobEntities.RemoveAtSwapBack(i);
                        transformAccessArray.RemoveAtSwapBack(i);
                        ReallocateVelocities();
                        return;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"Failed to remove mob from update: {ex.Message}");
                    }
                }
            }

            logger.LogWarning($"Mob entity not found in update list: {mobEntity}");
        }

        private void Update()
        {
            if (!mobEntities.IsCreated || mobEntities.Length == 0) return;

            for (int i = 0; i < mobEntities.Length; i++)
            {
                GCHandle handle = GCHandle.FromIntPtr(mobEntities[i]);
                AEntity mob = (AEntity)handle.Target;
                velocities[i] = mob.GetEntityVelocity();
            }

            moveMobsJob = new MoveMobsJob
            {
                Velocities = velocities,
                DeltaTime = Time.deltaTime
            };

            moveJobHandle = moveMobsJob.Schedule(transformAccessArray, moveJobHandle);
        }

        private void LateUpdate()
        {
            if (moveJobHandle.Equals(default(JobHandle))) return;
            moveJobHandle.Complete();
        }

        public void BreakUp(ISetAble.Callback callback)
        {
            try
            {
                foreach (var handle in gcHandles)
                {
                    if (handle.IsAllocated) handle.Free();
                }
                gcHandles.Clear();
                mobEntities.Dispose();
                transformAccessArray.Dispose();
                velocities.Dispose();
                logger.LogInfo("MobController service is disabled");
                callback?.Invoke();
            }
            catch (Exception ex)
            {
                logger?.LogError($"Error during break up: {ex.Message}");
            }
        }

        private void OnDisable()
        {
            BreakUp(null);
        }

        private void ReallocateVelocities()
        {
            try
            {
                if (velocities.IsCreated)
                    velocities.Dispose();

                velocities = new NativeArray<float2>(mobEntities.Length, Allocator.Persistent);
            }
            catch (Exception ex)
            {
                logger.LogError($"Error reallocating velocities: {ex.Message}");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Mathematics;
using Unity.Jobs;


namespace TheRavine.EntityControl
{
    public class MobController : MonoBehaviour, ISetAble
    {
        private RavineLogger logger;
        public int GetEntityCount() => mobEntities.Length;
        private NativeList<IntPtr> mobEntities;
        private MovementComponent[] mobMovement = Array.Empty<MovementComponent>();
        private List<GCHandle> gcHandles;
        private TransformAccessArray transformAccessArray;
        private NativeArray<float2> velocities;
        private NativeList<int> mobIntervals;
        private NativeList<float> mobAccumulated;
        private Dictionary<AEntity, int> mobIndex;
        private int updateFrame;

        private MoveMobsJob moveMobsJob;
        private JobHandle moveJobHandle;

        public void SetUp(ISetAble.Callback callback)
        {
            ServiceLocator.Register(this);
            
            logger = ServiceLocator.GetService<RavineLogger>();
            logger.LogInfo("MobController service is available now");
            
            mobEntities = new NativeList<IntPtr>(Allocator.Persistent);
            gcHandles = new List<GCHandle>();
            transformAccessArray = new TransformAccessArray(0);
            velocities = new NativeArray<float2>(0, Allocator.Persistent);
            mobIntervals = new NativeList<int>(Allocator.Persistent);
            mobAccumulated = new NativeList<float>(Allocator.Persistent);
            mobIndex = new Dictionary<AEntity, int>();

            callback?.Invoke();
        }

        public void AddMobToUpdate(AEntity mobEntity)
        {
            if (mobEntity == null)
            {
                logger.LogError("Mob entity is null!");
                return;
            }
            if (!mobEntities.IsCreated) return;

            try
            {
                GCHandle handle = GCHandle.Alloc(mobEntity, GCHandleType.Pinned);
                gcHandles.Add(handle);
                mobIndex[mobEntity] = mobEntities.Length;
                int slot = mobEntities.Length;
                mobEntities.Add(GCHandle.ToIntPtr(handle));
                if (slot == mobMovement.Length)
                    Array.Resize(ref mobMovement, math.max(8, slot << 1));
                mobMovement[slot] = mobEntity.GetEntityComponent<MovementComponent>();
                mobIntervals.Add(1);
                mobAccumulated.Add(0f);

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
            if (!mobEntities.IsCreated) return;

            if (mobIndex.TryGetValue(mobEntity, out int i))
            {
                try
                {
                    GCHandle handle = GCHandle.FromIntPtr(mobEntities[i]);
                    handle.Free();
                    gcHandles.Remove(handle);

                    int last = mobEntities.Length - 1;
                    mobIndex.Remove(mobEntity);
                    if (i != last)
                        mobIndex[(AEntity)GCHandle.FromIntPtr(mobEntities[last]).Target] = i;

                    mobMovement[i] = mobMovement[last];
                    mobMovement[last] = null;
                    mobEntities.RemoveAtSwapBack(i);
                    mobIntervals.RemoveAtSwapBack(i);
                    mobAccumulated.RemoveAtSwapBack(i);
                    transformAccessArray.RemoveAtSwapBack(i);
                    ReallocateVelocities();
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError($"Failed to remove mob from update: {ex.Message}");
                    return;
                }
            }

            logger.LogWarning($"Mob entity not found in update list: {mobEntity}");
        }

        public void SetMobInterval(AEntity mobEntity, int interval)
        {
            if (mobEntity != null && mobIndex.TryGetValue(mobEntity, out int i))
                mobIntervals[i] = math.max(interval, 1);
        }

        private void Update()
        {
            if (!mobEntities.IsCreated || mobEntities.Length == 0) return;

            float deltaTime = Time.deltaTime;
            updateFrame++;

            for (int i = 0; i < mobEntities.Length; i++)
            {
                float accumulated = mobAccumulated[i] + deltaTime;
                if ((uint)(updateFrame + i) % (uint)mobIntervals[i] != 0u)
                {
                    mobAccumulated[i] = accumulated;
                    velocities[i] = float2.zero;
                    continue;
                }

                mobAccumulated[i] = 0f;
                var movement = mobMovement[i];
                velocities[i] = movement != null ? movement.GetEntityVelocity() * accumulated : float2.zero;
            }

            moveMobsJob = new MoveMobsJob
            {
                Velocities = velocities,
                DeltaTime = 1f
            };

            moveJobHandle = moveMobsJob.Schedule(transformAccessArray, moveJobHandle);
        }

        private void LateUpdate()
        {
            if (moveJobHandle.Equals(default)) return;
            moveJobHandle.Complete();
        }

        public void BreakUp(ISetAble.Callback callback)
        {
            try
            {
                moveJobHandle.Complete();
                moveJobHandle = default;
                if (gcHandles != null)
                {
                    foreach (var handle in gcHandles)
                    {
                        if (handle.IsAllocated) handle.Free();
                    }
                    gcHandles.Clear();
                }
                Array.Clear(mobMovement, 0, mobMovement.Length);
                if (mobEntities.IsCreated) mobEntities.Dispose();
                if (mobIntervals.IsCreated) mobIntervals.Dispose();
                if (mobAccumulated.IsCreated) mobAccumulated.Dispose();
                mobIndex?.Clear();
                if (transformAccessArray.isCreated) transformAccessArray.Dispose();
                if (velocities.IsCreated) velocities.Dispose();
                logger?.LogInfo("MobController service is disabled");
            }
            catch (Exception ex)
            {
                logger?.LogError($"Error during break up: {ex.Message}");
            }
            callback?.Invoke();
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

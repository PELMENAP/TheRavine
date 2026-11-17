using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Mathematics;
using Unity.Jobs;

using TheRavine.Generator;

namespace TheRavine.EntityControl
{
    public class MobController : MonoBehaviour, ISetAble
    {
        private const float AtlasMargin = (ObstacleQuery.MaxCellReach + 1) * MapGenerator.scale;

        private RavineLogger logger;
        public int GetEntityCount() => mobEntities.Length;
        private NativeList<IntPtr> mobEntities;
        private MovementComponent[] mobMovement = Array.Empty<MovementComponent>();
        private List<GCHandle> gcHandles;
        private TransformAccessArray transformAccessArray;
        private NativeArray<float2> velocities;
        private NativeList<int> mobIntervals;
        private NativeList<float> mobAccumulated;
        private NativeList<float3> mobBodies;
        private NativeList<float2> mobPositions;
        private float2[] atlasPoints = Array.Empty<float2>();
        private Dictionary<AEntity, int> mobIndex;
        private int updateFrame;
        private bool motionMissingReported;

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
            mobBodies = new NativeList<float3>(Allocator.Persistent);
            mobPositions = new NativeList<float2>(Allocator.Persistent);
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
                if (slot >= atlasPoints.Length)
                    Array.Resize(ref atlasPoints, math.max(8, slot << 1));
                mobMovement[slot] = mobEntity.GetEntityComponent<MovementComponent>();
                mobIntervals.Add(1);
                mobAccumulated.Add(0f);

                var transformComponent = mobEntity.GetEntityComponent<TransformComponent>();
                mobBodies.Add(BodyOf(mobEntity));
                mobPositions.Add(PositionOf(transformComponent));

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
                    mobBodies.RemoveAtSwapBack(i);
                    mobPositions.RemoveAtSwapBack(i);
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

            if (!ServiceLocator.Services.TryGet(out MotionSystem motion) || !motion.IsAlive)
            {
                if (!motionMissingReported)
                {
                    motionMissingReported = true;
                    logger?.LogWarning("MobController: MotionSystem is not available, mobs hold their position");
                }
                return;
            }
            motionMissingReported = false;

            int count = mobEntities.Length;
            for (int i = 0; i < count; i++) atlasPoints[i] = mobPositions[i];

            moveMobsJob = new MoveMobsJob
            {
                Velocities = velocities,
                Bodies = mobBodies.AsArray(),
                Positions = mobPositions.AsArray(),
                Atlas = motion.PrepareAtlas(atlasPoints, count, AtlasMargin),
                Obstacles = motion.Obstacles,
                DeltaTime = 1f,
                StepUp = SimulationRules.Frame.AgentStepUp
            };

            moveJobHandle = moveMobsJob.Schedule(transformAccessArray, moveJobHandle);
            moveJobHandle.Complete();
            moveJobHandle = default;
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
                if (mobBodies.IsCreated) mobBodies.Dispose();
                if (mobPositions.IsCreated) mobPositions.Dispose();
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

        private static float3 BodyOf(AEntity mobEntity)
        {
            var main = mobEntity.GetEntityComponent<MainComponent>();
            if (main != null
                && ServiceLocator.Services.TryGet(out EntitySystem entities)
                && entities.TryGetMobInfo(main.GetPrefabID(), out EntityInfo info))
                return new float3(info.BodyRadius, info.BodyHeight, info.GroundOffset);

            return new float3(SimulationRules.Frame.AgentBodyRadius, SimulationRules.Frame.AgentBodyHeight, 0f);
        }

        private static float2 PositionOf(TransformComponent transformComponent)
        {
            Transform tr = transformComponent?.GetEntityTransform();
            return tr != null ? new float2(tr.position.x, tr.position.z) : float2.zero;
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

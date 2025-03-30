using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Mathematics;
using NaughtyAttributes;
using Cysharp.Threading.Tasks;

using TheRavine.Base;
using Random = TheRavine.Extensions.RavineRandom;

namespace TheRavine.EntityControl
{
    public class BoidsBehaviour : MonoBehaviour
    {
        [SerializeField] private BoidsInfo boidsInfo;
        [SerializeField] private GameObject[] prefabs;
        private NativeArray<float4> _positionsAndVelocities;
        private NativeArray<float2> _accelerations, _otherTargets;
        private NativeArray<int> _flockIds;
        private NativeArray<bool> _isMoving;
        private NativeParallelMultiHashMap<int2, int> _spatialGrid;
        private TransformAccessArray _transformAccessArray;
        private Transform[] transforms;
        private Transform viewer; 
        private InitSpatialGridJob initSpatialGridJob;
        private AccelerationJob accelerationJob;
        private MoveJob moveJob;
        private bool isUpdate;
        private float2 GetTargetPositionCloseToViewer() 
        {
            if(viewer == null) return new(Random.RangeInt(-boidsInfo.distanceOfTargetFromPlayer, boidsInfo.distanceOfTargetFromPlayer), Random.RangeInt(-boidsInfo.distanceOfTargetFromPlayer, boidsInfo.distanceOfTargetFromPlayer));
            else return new(-viewer.position.x + Random.RangeInt(-boidsInfo.distanceOfTargetFromPlayer, boidsInfo.distanceOfTargetFromPlayer), -viewer.position.y + Random.RangeInt(-boidsInfo.distanceOfTargetFromPlayer, boidsInfo.distanceOfTargetFromPlayer));
        }
        public void StartBoids(Transform viewer)
        {       
            this.viewer = viewer; 
            isUpdate = false;
            
            _positionsAndVelocities = new NativeArray<float4>(boidsInfo.numberOfEntities, Allocator.Persistent);

            _accelerations = new NativeArray<float2>(boidsInfo.numberOfEntities, Allocator.Persistent);
            _otherTargets = new NativeArray<float2>(prefabs.Length, Allocator.Persistent);
            _isMoving = new NativeArray<bool>(boidsInfo.numberOfEntities, Allocator.Persistent);
            _flockIds = new NativeArray<int>(boidsInfo.numberOfEntities, Allocator.Persistent);
            _spatialGrid = new NativeParallelMultiHashMap<int2, int>(boidsInfo.numberOfEntities, Allocator.Persistent);

            transforms = new Transform[boidsInfo.numberOfEntities];

            for (int i = 0; i < prefabs.Length; i++)
            {
                _otherTargets[i] = GetTargetPositionCloseToViewer();
            }

            int flockSize = boidsInfo.numberOfEntities / prefabs.Length;
            int extraBoids = boidsInfo.numberOfEntities % prefabs.Length;
            
            int agentIndex = 0;

            for (int flock = 0; flock < prefabs.Length; flock++)
            {
                int currentFlockSize = flockSize + (flock < extraBoids ? 1 : 0);

                for (int i = 0; i < currentFlockSize; i++)
                {
                    transforms[agentIndex] = Instantiate(prefabs[flock]).transform;
                    transforms[agentIndex].position = new Vector2(
                        _otherTargets[flock].x + Random.RangeInt(-boidsInfo.nearTheTarget, boidsInfo.nearTheTarget),
                        _otherTargets[flock].y + Random.RangeInt(-boidsInfo.nearTheTarget, boidsInfo.nearTheTarget)
                    );
                    transforms[agentIndex].parent = transform;

                    float2 randomInCircle = Random.GetInsideCircle();

                    _positionsAndVelocities[agentIndex] = new float4(transforms[agentIndex].position.x, transforms[agentIndex].position.y, randomInCircle.x, randomInCircle.y);
                    _accelerations[agentIndex] = Random.GetInsideCircle();
                    _isMoving[agentIndex] = true;
                    _flockIds[agentIndex] = flock;

                    agentIndex++;
                }
            }

            _transformAccessArray = new TransformAccessArray(transforms);

            SetUpNewValues();

            TargetsUpdate().Forget();
            isUpdate = true;
        }



        private JobHandle moveHandle;
        private void Update()
        {
            if(!isUpdate) return;
            _spatialGrid.Clear();
            var initSpatialGridHandle = initSpatialGridJob.Schedule(boidsInfo.numberOfEntities, 64); 
             if(!isUpdate) return;
            var accelerationHandle = accelerationJob.Schedule(boidsInfo.numberOfEntities, 64, initSpatialGridHandle);
            if(!isUpdate) return;
            moveHandle = moveJob.Schedule(_transformAccessArray, accelerationHandle);
        }

        private void LateUpdate() 
        {
            if(!isUpdate) return;
            moveHandle.Complete();
        }

        public void DisableBoids()
        {
            isUpdate = false;
        }

        private void OnDisable() {
            if (_positionsAndVelocities.IsCreated)
                _positionsAndVelocities.Dispose();
            _accelerations.Dispose();
            _otherTargets.Dispose();
            _isMoving.Dispose();
            _transformAccessArray.Dispose();
        }

        [Button]
        private void SetUpNewValues()
        {
            isUpdate = false;

            initSpatialGridJob = new InitSpatialGridJob()
            {
                PositionsAndVelocities = _positionsAndVelocities,
                IsMoving = _isMoving,
                SpatialGrid = _spatialGrid.AsParallelWriter(),
                InvCellSize = 1.0f / boidsInfo.cellSize
            };
            
            accelerationJob = new AccelerationJob()
            {
                PositionsAndVelocities = _positionsAndVelocities,
                FlockIds = _flockIds,
                OtherTargets = _otherTargets,
                IsMoving = _isMoving,
                Accelerations = _accelerations,
                DestinationThreshold = boidsInfo.destinationThreshold,
                AvoidanceThreshold = boidsInfo.avoidanceThreshold,
                Weights = boidsInfo.accelerationWeights,
                SpatialGrid = _spatialGrid,
                InvCellSize = 1.0f / boidsInfo.cellSize
            };

            moveJob = new MoveJob()
            {
                PositionsAndVelocities = _positionsAndVelocities,
                IsMoving = _isMoving,
                Accelerations = _accelerations,
                DeltaTime = Time.deltaTime,
                VelocityLimit = boidsInfo.velocityLimit,
                VelocityLimitSq = boidsInfo.velocityLimit * boidsInfo.velocityLimit,
                FlipRotation = boidsInfo.flip,
                IdentityRotation = Quaternion.identity
            };
            isUpdate = true;
        }

        [Button]
        private void ChangeMoving()
        {
            for (int i = 0; i < Random.RangeInt(1, boidsInfo.numberOfEntities); i++)
            {
                int a = Random.RangeInt(0, boidsInfo.numberOfEntities);
                _isMoving[a] = !_isMoving[a];
            }
        }
        private async UniTaskVoid TargetsUpdate()
        {
            while(!DataStorage.sceneClose)
            {
                _otherTargets[Random.RangeInt(0, _otherTargets.Length)] = GetTargetPositionCloseToViewer();
                ChangeMoving();
                await UniTask.Delay(Random.RangeInt(1000 * boidsInfo.delayFactor, 10000 * boidsInfo.delayFactor));
            }
        }
    }
}
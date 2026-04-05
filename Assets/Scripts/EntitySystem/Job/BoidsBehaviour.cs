using System.Threading;

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

        private CancellationTokenSource _cts;
        private NativeArray<float4> _positionsAndVelocities;
        private NativeArray<float2> _accelerations, _otherTargets;
        private NativeArray<int> _flockIds;
        private NativeArray<bool> _isMoving;
        private NativeParallelMultiHashMap<int2, int> _spatialGrid;
        private TransformAccessArray _transformAccessArray;
        private Transform[] _transforms;
        private Transform _viewer;

        private InitSpatialGridJob _initSpatialGridJob;
        private AccelerationJob _accelerationJob;
        private MoveJob _moveJob;
        private JobHandle _moveHandle;
        private bool _isUpdate;
        private bool _moveScheduled;

        private float2 GetTargetPositionCloseToViewer()
        {
            float2 offset = new(
                Random.RangeInt(-boidsInfo.distanceOfTargetFromPlayer, boidsInfo.distanceOfTargetFromPlayer),
                Random.RangeInt(-boidsInfo.distanceOfTargetFromPlayer, boidsInfo.distanceOfTargetFromPlayer)
            );
            if (_viewer == null) return offset;
            return new float2(-_viewer.position.x, -_viewer.position.z) + offset;
        }

        public void StartBoids(Transform viewer)
        {
            _viewer = viewer;
            _isUpdate = false;

            _cts = new CancellationTokenSource();
            _positionsAndVelocities = new NativeArray<float4>(boidsInfo.numberOfEntities, Allocator.Persistent);
            _accelerations = new NativeArray<float2>(boidsInfo.numberOfEntities, Allocator.Persistent);
            _otherTargets = new NativeArray<float2>(prefabs.Length, Allocator.Persistent);
            _isMoving = new NativeArray<bool>(boidsInfo.numberOfEntities, Allocator.Persistent);
            _flockIds = new NativeArray<int>(boidsInfo.numberOfEntities, Allocator.Persistent);
            _spatialGrid = new NativeParallelMultiHashMap<int2, int>(boidsInfo.numberOfEntities, Allocator.Persistent);

            _transforms = new Transform[boidsInfo.numberOfEntities];

            for (int i = 0; i < prefabs.Length; i++)
                _otherTargets[i] = GetTargetPositionCloseToViewer();

            int flockSize = boidsInfo.numberOfEntities / prefabs.Length;
            int extraBoids = boidsInfo.numberOfEntities % prefabs.Length;
            int agentIndex = 0;

            for (int flock = 0; flock < prefabs.Length; flock++)
            {
                int currentFlockSize = flockSize + (flock < extraBoids ? 1 : 0);

                for (int i = 0; i < currentFlockSize; i++)
                {
                    float2 target = _otherTargets[flock];
                    float spawnX = target.x + Random.RangeInt(-boidsInfo.nearTheTarget, boidsInfo.nearTheTarget);
                    float spawnZ = target.y + Random.RangeInt(-boidsInfo.nearTheTarget, boidsInfo.nearTheTarget);
                    float spawnY = boidsInfo.yTarget + Random.GetInsideCircle().x * boidsInfo.ySpawnSpread;

                    _transforms[agentIndex] = Instantiate(prefabs[flock]).transform;
                    _transforms[agentIndex].position = new Vector3(spawnX, spawnY, spawnZ);
                    _transforms[agentIndex].parent = transform;

                    float2 randomVel = Random.GetInsideCircle();
                    _positionsAndVelocities[agentIndex] = new float4(spawnX, spawnZ, randomVel.x, randomVel.y);
                    _accelerations[agentIndex] = Random.GetInsideCircle();
                    _isMoving[agentIndex] = true;
                    _flockIds[agentIndex] = flock;

                    agentIndex++;
                }
            }

            _transformAccessArray = new TransformAccessArray(_transforms);

            SetUpNewValues();
            TargetsUpdate().Forget();
            _isUpdate = true;
        }

        private void Update()
        {
            if (!_isUpdate) return;

            _moveJob.DeltaTime = Time.deltaTime;

            _spatialGrid.Clear();
            var gridHandle = _initSpatialGridJob.Schedule(boidsInfo.numberOfEntities, 64);
            var accelHandle = _accelerationJob.Schedule(boidsInfo.numberOfEntities, 64, gridHandle);
            _moveHandle = _moveJob.Schedule(_transformAccessArray, accelHandle);
            _moveScheduled = true;
        }

        private void LateUpdate()
        {
            if (!_moveScheduled) return;
            _moveHandle.Complete();
            _moveScheduled = false;
        }

        public void DisableBoids()
        {
            if (_moveScheduled)
            {
                _moveHandle.Complete();
                _moveScheduled = false;
            }
            _isUpdate = false;
        }

        private void OnDisable()
        {
            _isUpdate = false;

            if (_moveScheduled)
            {
                _moveHandle.Complete();
                _moveScheduled = false;
            }

            if (_positionsAndVelocities.IsCreated) _positionsAndVelocities.Dispose();
            if (_accelerations.IsCreated) _accelerations.Dispose();
            if (_otherTargets.IsCreated) _otherTargets.Dispose();
            if (_isMoving.IsCreated) _isMoving.Dispose();
            if (_flockIds.IsCreated) _flockIds.Dispose();
            if (_spatialGrid.IsCreated) _spatialGrid.Dispose();
            if (_transformAccessArray.isCreated) _transformAccessArray.Dispose();

            _cts?.Cancel();
            _cts?.Dispose();
        }

        [Button]
        private void SetUpNewValues()
        {
            if (_moveScheduled)
            {
                _moveHandle.Complete();
                _moveScheduled = false;
            }
            _isUpdate = false;

            float invCell = 1f / boidsInfo.cellSize;

            _initSpatialGridJob = new InitSpatialGridJob
            {
                PositionsAndVelocities = _positionsAndVelocities,
                IsMoving = _isMoving,
                SpatialGrid = _spatialGrid.AsParallelWriter(),
                InvCellSize = invCell
            };

            _accelerationJob = new AccelerationJob
            {
                PositionsAndVelocities = _positionsAndVelocities,
                FlockIds = _flockIds,
                OtherTargets = _otherTargets,
                IsMoving = _isMoving,
                Accelerations = _accelerations,
                DestinationThreshold = boidsInfo.destinationThreshold,
                AvoidanceThreshold = boidsInfo.avoidanceThreshold,
                AlongThreshold = boidsInfo.alongThreshold,
                Weights = boidsInfo.accelerationWeights,
                SpatialGrid = _spatialGrid,
                InvCellSize = invCell
            };

            _moveJob = new MoveJob
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

            _isUpdate = true;
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
            while (!_cts.Token.IsCancellationRequested)
            {
                _otherTargets[Random.RangeInt(0, _otherTargets.Length)] = GetTargetPositionCloseToViewer();
                ChangeMoving();
                await UniTask.Delay(Random.RangeInt(1000 * boidsInfo.delayFactor, 10000 * boidsInfo.delayFactor), cancellationToken: _cts.Token);
            }
        }
    }
}
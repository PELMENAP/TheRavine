using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using Random = TheRavine.Extentions.RavineRandom;
using Unity.Mathematics;
using NaughtyAttributes;
using Cysharp.Threading.Tasks;

using TheRavine.Base;

namespace TheRavine.EntityControl
{
    public class BoidsBehaviour : MonoBehaviour
    {
        [SerializeField] private int _numberOfEntities, _numberOfTargets, delayFactor;
        [SerializeField] private GameObject _entityPrefab;
        [SerializeField] private float _destinationThreshold, _avoidanceThreshold, _targetThreshold;
        [SerializeField] private float _velocityLimit;
        [SerializeField] private float3 _accelerationWeights;
        [SerializeField] private Vector3 _flip;

        private NativeArray<float2> _positions;
        private NativeArray<float2> _velocities;
        private NativeArray<float2> _accelerations;
        private NativeArray<float2> _otherTargets;
        private NativeArray<bool> _isMoving;
        private TransformAccessArray _transformAccessArray;

        private Transform[] transforms;

        private AccelerationJob accelerationJob;
        private MoveJob moveJob;

        [SerializeField] private Transform viewer;

        private bool isUpdate;

        private float2 GetTargetPositionCloseToViewer() => new(-viewer.position.x + Random.RangeInt(-100, 100), -viewer.position.y + Random.RangeInt(-100, 100));
        public async UniTaskVoid StartBoids()
        {
            isUpdate = false;
            _positions = new NativeArray<float2>(_numberOfEntities, Allocator.Persistent);
            _velocities = new NativeArray<float2>(_numberOfEntities, Allocator.Persistent);
            _accelerations = new NativeArray<float2>(_numberOfEntities, Allocator.Persistent);
            _otherTargets = new NativeArray<float2>(_numberOfTargets, Allocator.Persistent);
            _isMoving = new NativeArray<bool>(_numberOfEntities, Allocator.Persistent);

            transforms = new Transform[_numberOfEntities];

            for (byte i = 0; i < _numberOfTargets; i++)
            {
                _otherTargets[i] = GetTargetPositionCloseToViewer();
            }

            for (ushort i = 0; i < _numberOfEntities; i++)
            {
                transforms[i] = Instantiate(_entityPrefab).transform;
                transforms[i].position = new Vector2(-_otherTargets[i % _numberOfTargets].x + Random.RangeInt(-20, 20), -_otherTargets[i % _numberOfTargets].y + Random.RangeInt(-20, 20));
                transforms[i].parent = transform;
                _velocities[i] = Random.GetInsideCircle();
                _accelerations[i] = Random.GetInsideCircle();
                _isMoving[i] = true;
                await UniTask.Delay(10);
                
            }

            _transformAccessArray = new TransformAccessArray(transforms);

            accelerationJob = new AccelerationJob()
            {
                Positions = _positions,
                OtherTargets = _otherTargets,
                Velocities = _velocities,
                IsMoving = _isMoving,
                Accelerations = _accelerations,
                DestinationThreshold = _destinationThreshold,
                AvoidanceThreshold = _avoidanceThreshold,
                Weights = _accelerationWeights
            };
            moveJob = new MoveJob()
            {
                Positions = _positions,
                Velocities = _velocities,
                IsMoving = _isMoving,
                Accelerations = _accelerations,
                DeltaTime = Time.deltaTime,
                VelocityLimit = _velocityLimit,
                Flip = _flip
            };

            TargetsUpdate().Forget();
            isUpdate = true;
        }

        private void FixedUpdate()
        {
            if(!isUpdate) return;
            var accelerationHandle = accelerationJob.Schedule(_numberOfEntities, 0);
            var moveHandle = moveJob.Schedule(_transformAccessArray, accelerationHandle);
            moveHandle.Complete();
        }

        public void DisableBoids()
        {
            isUpdate = false;
            _positions.Dispose();
            _velocities.Dispose();
            _accelerations.Dispose();
            _otherTargets.Dispose();
            _isMoving.Dispose();
            _transformAccessArray.Dispose();
        }

        [Button]
        private void ChangeMoving()
        {
            for (ushort i = 0; i < Random.RangeInt(1, _numberOfEntities); i++)
            {
                int a = Random.RangeInt(0, _numberOfEntities);
                _isMoving[a] = !_isMoving[a];
            }
        }

        [Button]
        private void ChangeOneBoidsMoving()
        {
            transforms[0].position += new Vector3(10, 0, 0);
        }

        [Button]
        private void SetUpNewValues()
        {
            accelerationJob = new AccelerationJob()
            {
                Positions = _positions,
                OtherTargets = _otherTargets,
                Velocities = _velocities,
                IsMoving = _isMoving,
                Accelerations = _accelerations,
                DestinationThreshold = _destinationThreshold,
                AvoidanceThreshold = _avoidanceThreshold,
                TargetThreshold = _targetThreshold,
                Weights = _accelerationWeights
            };
            moveJob = new MoveJob()
            {
                Positions = _positions,
                Velocities = _velocities,
                IsMoving = _isMoving,
                Accelerations = _accelerations,
                DeltaTime = Time.deltaTime,
                VelocityLimit = _velocityLimit,
                Flip = _flip
            };
        }


        private async UniTaskVoid TargetsUpdate()
        {
            while(!DataStorage.sceneClose){
                _otherTargets[Random.RangeInt(0, _otherTargets.Length)] = GetTargetPositionCloseToViewer();
                await UniTask.Delay(Random.RangeInt(1000 * delayFactor, 10000 * delayFactor));
            }
        }
    }
}
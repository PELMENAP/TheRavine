using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using Random = UnityEngine.Random;
using Unity.Mathematics;
using NaughtyAttributes;

namespace TheRavine.EntityControl
{
    public class BoidsBehaviour : MonoBehaviour
    {
        [SerializeField] private int _numberOfEntities, _randomSeed;
        [SerializeField] private GameObject _entityPrefab;
        [SerializeField] private float _destinationThreshold, _avoidanceThreshold, _randomnessRadius;
        [SerializeField] private float2 _areaSize;
        [SerializeField] private float _velocityLimit;
        [SerializeField] private float3 _accelerationWeights;
        [SerializeField] private float2[] targetArray;
        [SerializeField] private Vector3 _flip;

        private NativeArray<float2> _positions;
        private NativeArray<float2> _velocities;
        private NativeArray<float2> _accelerations;
        private NativeArray<float2> _otherTargets;
        private NativeArray<bool> _isMoving;
        private TransformAccessArray _transformAccessArray;

        private void Start()
        {
            _positions = new NativeArray<float2>(_numberOfEntities, Allocator.Persistent);
            _velocities = new NativeArray<float2>(_numberOfEntities, Allocator.Persistent);
            _accelerations = new NativeArray<float2>(_numberOfEntities, Allocator.Persistent);
            _otherTargets = new NativeArray<float2>(targetArray.Length, Allocator.Persistent);
            _isMoving = new NativeArray<bool>(_numberOfEntities, Allocator.Persistent);

            var transforms = new Transform[_numberOfEntities];
            for (ushort i = 0; i < _numberOfEntities; i++)
            {
                transforms[i] = Instantiate(_entityPrefab).transform;
                transforms[i].position = (Vector2)targetArray[i % targetArray.Length] + new Vector2(Random.Range(-20, 20), Random.Range(-20, 20));
                transforms[i].parent = this.transform;
                _velocities[i] = Random.insideUnitCircle;
                _accelerations[i] = Random.insideUnitCircle;
                _isMoving[i] = true;
            }

            for (byte i = 0; i < targetArray.Length; i++)
            {
                Instantiate(_entityPrefab, new Vector3(targetArray[i].x, targetArray[i].y, 0), Quaternion.identity);
                _otherTargets[i] = targetArray[i];
            }
            _transformAccessArray = new TransformAccessArray(transforms);
        }

        private void FixedUpdate()
        {
            var accelerationJob = new AccelerationJob()
            {
                Positions = _positions,
                OtherTargets = _otherTargets,
                Velocities = _velocities,
                IsMoving = _isMoving,
                Accelerations = _accelerations,
                DestinationThreshold = _destinationThreshold,
                AvoidanceThreshold = _avoidanceThreshold,
                RandomnessRadius = _randomnessRadius,
                RandomSeed = _randomSeed,
                Weights = _accelerationWeights
            };
            var moveJob = new MoveJob()
            {
                Positions = _positions,
                Velocities = _velocities,
                IsMoving = _isMoving,
                Accelerations = _accelerations,
                DeltaTime = Time.deltaTime,
                VelocityLimit = _velocityLimit,
                Flip = _flip
            };
            var accelerationHandle = accelerationJob.Schedule(_numberOfEntities,
            0);
            var moveHandle = moveJob.Schedule(_transformAccessArray, accelerationHandle);
            moveHandle.Complete();
        }

        private void OnDestroy()
        {
            _positions.Dispose();
            _velocities.Dispose();
            _accelerations.Dispose();
            _otherTargets.Dispose();
            _isMoving.Dispose();
            _transformAccessArray.Dispose();
        }

        [Button]
        private void ChangeTargets()
        {
            for (byte i = 0; i < Random.Range(1, _otherTargets.Length); i++)
            {
                int a = Random.Range(0, _otherTargets.Length);
                int b = Random.Range(0, _otherTargets.Length);
                float2 vec = _otherTargets[a];
                _otherTargets[a] = _otherTargets[b];
                _otherTargets[b] = vec;
            }
        }

        [Button]
        private void ChangeMoving()
        {
            for (ushort i = 0; i < Random.Range(1, _numberOfEntities); i++)
            {
                int a = Random.Range(0, _numberOfEntities);
                _isMoving[a] = !_isMoving[a];
            }
        }
    }
}
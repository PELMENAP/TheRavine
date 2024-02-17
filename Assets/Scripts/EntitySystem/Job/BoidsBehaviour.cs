using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using Random = UnityEngine.Random;
using NaughtyAttributes;
public class BoidsBehaviour : MonoBehaviour
{
    [SerializeField]
    private int _numberOfEntities, _randomSeed;

    [SerializeField]
    private GameObject _entityPrefab;

    [SerializeField]
    private float _destinationThreshold, _avoidanceThreshold, _randomnessRadius;

    [SerializeField]
    private Vector3 _areaSize;

    [SerializeField]
    private float _velocityLimit;

    [SerializeField]
    private Vector3 _accelerationWeights;
    [SerializeField]
    private Vector3[] targetArray;

    private NativeArray<Vector3> _positions;
    private NativeArray<Vector3> _velocities;
    private NativeArray<Vector3> _accelerations;
    private NativeArray<Vector3> _otherTargets;
    private NativeArray<bool> _isMoving;

    private TransformAccessArray _transformAccessArray;

    private void Start()
    {
        _positions = new NativeArray<Vector3>(_numberOfEntities, Allocator.Persistent);
        _velocities = new NativeArray<Vector3>(_numberOfEntities, Allocator.Persistent);
        _accelerations = new NativeArray<Vector3>(_numberOfEntities, Allocator.Persistent);
        _otherTargets = new NativeArray<Vector3>(targetArray.Length, Allocator.Persistent);
        _isMoving = new NativeArray<bool>(_numberOfEntities, Allocator.Persistent);

        var transforms = new Transform[_numberOfEntities];
        for (int i = 0; i < _numberOfEntities; i++)
        {
            transforms[i] = Instantiate(_entityPrefab).transform;
            transforms[i].position = targetArray[i % targetArray.Length] + new Vector3(Random.Range(-20, 20), Random.Range(-20, 20), 0);
            transforms[i].parent = this.transform;
            _velocities[i] = Random.insideUnitCircle;
            _accelerations[i] = Random.insideUnitCircle;
            _isMoving[i] = true;
        }

        for (int i = 0; i < targetArray.Length; i++)
        {
            _otherTargets[i] = targetArray[i];
        }
        _transformAccessArray = new TransformAccessArray(transforms);
    }

    private void FixedUpdate()
    {
        // var boundsJob = new BoundsJob()
        // {
        //     Positions = _positions,
        //     Accelerations = _accelerations,
        //     AreaSize = _areaSize
        // };
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
            VelocityLimit = _velocityLimit
        };
        // var boundsHandle = boundsJob.Schedule(_numberOfEntities, 0);
        // var accelerationHandle = accelerationJob.Schedule(_numberOfEntities,
        //     0, boundsHandle);
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

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(Vector3.zero, _areaSize);
    }

    [Button]
    private void ChangeTargets()
    {
        for (int i = 0; i < Random.Range(1, _otherTargets.Length); i++)
        {
            int a = Random.Range(0, _otherTargets.Length);
            int b = Random.Range(0, _otherTargets.Length);
            Vector3 vec = _otherTargets[a];
            _otherTargets[a] = _otherTargets[b];
            _otherTargets[b] = vec;
        }
    }

    [Button]
    private void ChangeMoving()
    {
        for (int i = 0; i < Random.Range(1, _numberOfEntities); i++)
        {
            int a = Random.Range(0, _numberOfEntities);
            _isMoving[a] = !_isMoving[a];
        }
    }
}
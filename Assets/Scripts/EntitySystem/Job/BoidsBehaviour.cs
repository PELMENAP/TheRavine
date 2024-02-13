using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using Random = UnityEngine.Random;

public class BoidsBehaviour : MonoBehaviour
{
    [SerializeField]
    private int _numberOfEntities;

    [SerializeField]
    private GameObject _entityPrefab;

    [SerializeField]
    private float _destinationThreshold;

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

    private TransformAccessArray _transformAccessArray;

    private void Start()
    {
        _positions = new NativeArray<Vector3>(_numberOfEntities, Allocator.Persistent);
        _velocities = new NativeArray<Vector3>(_numberOfEntities, Allocator.Persistent);
        _accelerations = new NativeArray<Vector3>(_numberOfEntities, Allocator.Persistent);
        _otherTargets = new NativeArray<Vector3>(targetArray.Length, Allocator.Persistent);

        var transforms = new Transform[_numberOfEntities];
        for (int i = 0; i < _numberOfEntities; i++)
        {
            transforms[i] = Instantiate(_entityPrefab).transform;
            _velocities[i] = Random.insideUnitSphere;
        }

        for (int i = 0; i < targetArray.Length; i++)
        {
            _otherTargets[i] = targetArray[i];
        }
        _transformAccessArray = new TransformAccessArray(transforms);
    }

    private void FixedUpdate()
    {
        var boundsJob = new BoundsJob()
        {
            Positions = _positions,
            Accelerations = _accelerations,
            AreaSize = _areaSize
        };
        var accelerationJob = new AccelerationJob()
        {
            Positions = _positions,
            OtherTargets = _otherTargets,
            Velocities = _velocities,
            Accelerations = _accelerations,
            DestinationThreshold = _destinationThreshold,
            Weights = _accelerationWeights
        };
        var moveJob = new MoveJob()
        {
            Positions = _positions,
            Velocities = _velocities,
            Accelerations = _accelerations,
            DeltaTime = Time.deltaTime,
            VelocityLimit = _velocityLimit
        };
        var boundsHandle = boundsJob.Schedule(_numberOfEntities, 0);
        var accelerationHandle = accelerationJob.Schedule(_numberOfEntities,
            0, boundsHandle);
        var moveHandle = moveJob.Schedule(_transformAccessArray, accelerationHandle);
        moveHandle.Complete();
    }

    private void OnDestroy()
    {
        _positions.Dispose();
        _velocities.Dispose();
        _accelerations.Dispose();
        _transformAccessArray.Dispose();
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(Vector3.zero, _areaSize);
    }
}
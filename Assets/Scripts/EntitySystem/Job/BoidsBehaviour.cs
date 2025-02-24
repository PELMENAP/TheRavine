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
        [SerializeField] private Transform viewer;
        [SerializeField] private GameObject[] prefabs;
        private NativeArray<float2> _positions, _velocities, _accelerations, _otherTargets;
        private NativeArray<int> _flockIds;
        private NativeArray<bool> _isMoving;
        private TransformAccessArray _transformAccessArray;
        private Transform[] transforms;
        private AccelerationJob accelerationJob;
        private MoveJob moveJob;
        private bool isUpdate;
        private float2 GetTargetPositionCloseToViewer() => new(-viewer.position.x + Random.RangeInt(-boidsInfo.distanceOfTargetFromPlayer, boidsInfo.distanceOfTargetFromPlayer), -viewer.position.y + Random.RangeInt(-boidsInfo.distanceOfTargetFromPlayer, boidsInfo.distanceOfTargetFromPlayer));
        public async UniTaskVoid StartBoids()
        {       
            isUpdate = false;
            
            _positions = new NativeArray<float2>(boidsInfo.numberOfEntities, Allocator.Persistent);
            _velocities = new NativeArray<float2>(boidsInfo.numberOfEntities, Allocator.Persistent);
            _accelerations = new NativeArray<float2>(boidsInfo.numberOfEntities, Allocator.Persistent);
            _otherTargets = new NativeArray<float2>(prefabs.Length, Allocator.Persistent);
            _isMoving = new NativeArray<bool>(boidsInfo.numberOfEntities, Allocator.Persistent);
            _flockIds = new NativeArray<int>(boidsInfo.numberOfEntities, Allocator.Persistent);

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

                    _positions[agentIndex] = new float2(transforms[agentIndex].position.x, transforms[agentIndex].position.y);
                    _velocities[agentIndex] = Random.GetInsideCircle();
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
            var accelerationHandle = accelerationJob.Schedule(boidsInfo.numberOfEntities, 0);
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
            _positions.Dispose();
            _velocities.Dispose();
            _accelerations.Dispose();
            _otherTargets.Dispose();
            _isMoving.Dispose();
            _transformAccessArray.Dispose();
        }

        [Button]
        private void SetUpNewValues()
        {
            isUpdate = false;
            accelerationJob = new AccelerationJob()
            {
                Positions = _positions,
                FlockIds = _flockIds,
                OtherTargets = _otherTargets,
                Velocities = _velocities,
                IsMoving = _isMoving,
                Accelerations = _accelerations,
                DestinationThreshold = boidsInfo.destinationThreshold,
                AvoidanceThreshold = boidsInfo.avoidanceThreshold,
                Weights = boidsInfo.accelerationWeights
            };

            moveJob = new MoveJob()
            {
                Positions = _positions,
                Velocities = _velocities,
                IsMoving = _isMoving,
                Accelerations = _accelerations,
                DeltaTime = Time.deltaTime,
                VelocityLimit = boidsInfo.velocityLimit,
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
                if(viewer != null) _otherTargets[Random.RangeInt(0, _otherTargets.Length)] = GetTargetPositionCloseToViewer();
                ChangeMoving();
                await UniTask.Delay(Random.RangeInt(1000 * boidsInfo.delayFactor, 10000 * boidsInfo.delayFactor));
            }
        }
    }
}
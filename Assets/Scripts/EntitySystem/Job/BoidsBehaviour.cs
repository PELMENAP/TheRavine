using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.SceneManagement;
using Unity.Mathematics;

namespace TheRavine.EntityControl
{
    public class BoidsBehaviour : MonoBehaviour
    {
        [SerializeField] private BoidsInfo boidsInfo;
        [SerializeField] private GameObject[] prefabs;

        [SerializeField] private bool isTest;

        private NativeArray<float4> _positionsAndVelocities;
        private NativeArray<float2> _accelerations, _otherTargets;
        private NativeArray<int> _flockIds;
        private NativeArray<bool> _isMoving;
        private NativeParallelMultiHashMap<int2, int> _spatialGrid;
        private TransformAccessArray _transformAccessArray;
        private Transform[] _transforms;
        private GameObject[] _birds;
        private float[] _flockTimers;
        private int[] _flockStart;
        private Transform _viewer;

        private InitSpatialGridJob _initSpatialGridJob;
        private AccelerationJob _accelerationJob;
        private MoveJob _moveJob;
        private JobHandle _moveHandle;
        private double _lastTime;
        private int _count;
        private bool _isUpdate;
        private bool _moveScheduled;
        private bool _birdsVisible;

        private float2 GetTargetPositionCloseToViewer()
        {
            float2 offset = new(
                RavineRandom.RangeInt(-boidsInfo.distanceOfTargetFromPlayer, boidsInfo.distanceOfTargetFromPlayer),
                RavineRandom.RangeInt(-boidsInfo.distanceOfTargetFromPlayer, boidsInfo.distanceOfTargetFromPlayer)
            );
            if (_viewer == null) return offset;
            Vector3 p = _viewer.position;
            return new float2(p.x, p.z) + offset;
        }

        private float NextFlockInterval()
            => RavineRandom.RangeFloat(boidsInfo.flockRetargetInterval.x, boidsInfo.flockRetargetInterval.y);

        private void Start()
        {
            if (isTest) StartBoids(transform);
        }

        public void StartBoids(Transform viewer)
        {
            if (boidsInfo == null || prefabs == null || prefabs.Length == 0) return;

            CompleteMove();
            _isUpdate = false;
            _viewer = viewer;

            if (!_positionsAndVelocities.IsCreated || _count != boidsInfo.numberOfEntities || _flockTimers.Length != prefabs.Length)
            {
                DisposeAll();
                Allocate();
                SetUpNewValues();
            }

            ResetState();
            SetBirdsVisible(true);

            _lastTime = SimulationClock.TimeD;
            _isUpdate = true;
        }

        private void Allocate()
        {
            _count = boidsInfo.numberOfEntities;
            int flocks = prefabs.Length;

            _positionsAndVelocities = new NativeArray<float4>(_count, Allocator.Persistent);
            _accelerations = new NativeArray<float2>(_count, Allocator.Persistent);
            _otherTargets = new NativeArray<float2>(flocks, Allocator.Persistent);
            _isMoving = new NativeArray<bool>(_count, Allocator.Persistent);
            _flockIds = new NativeArray<int>(_count, Allocator.Persistent);
            _spatialGrid = new NativeParallelMultiHashMap<int2, int>(_count, Allocator.Persistent);
            _flockTimers = new float[flocks];
            _flockStart = new int[flocks + 1];

            _transforms = new Transform[_count];
            _birds = new GameObject[_count];

            Scene scene = gameObject.scene;
            int flockSize = _count / flocks;
            int extraBoids = _count % flocks;
            int agentIndex = 0;

            for (int flock = 0; flock < flocks; flock++)
            {
                int currentFlockSize = flockSize + (flock < extraBoids ? 1 : 0);
                _flockStart[flock] = agentIndex;
                for (int i = 0; i < currentFlockSize; i++)
                {
                    GameObject bird = Instantiate(prefabs[flock]);
                    if (bird.scene != scene) SceneManager.MoveGameObjectToScene(bird, scene);
                    _birds[agentIndex] = bird;
                    _transforms[agentIndex] = bird.transform;
                    _flockIds[agentIndex] = flock;
                    agentIndex++;
                }
            }

            _flockStart[flocks] = agentIndex;
            _birdsVisible = true;
            _transformAccessArray = new TransformAccessArray(_transforms);
        }

        private void ResetState()
        {
            for (int i = 0; i < _otherTargets.Length; i++)
            {
                _otherTargets[i] = GetTargetPositionCloseToViewer();
                _flockTimers[i] = NextFlockInterval();
            }

            for (int i = 0; i < _count; i++)
            {
                float2 target = _otherTargets[_flockIds[i]];
                float spawnX = target.x + RavineRandom.RangeInt(-boidsInfo.nearTheTarget, boidsInfo.nearTheTarget);
                float spawnZ = target.y + RavineRandom.RangeInt(-boidsInfo.nearTheTarget, boidsInfo.nearTheTarget);
                float spawnY = boidsInfo.yTarget + RavineRandom.GetInsideCircle().x * boidsInfo.ySpawnSpread;

                _transforms[i].position = new Vector3(spawnX, spawnY, spawnZ);

                float2 randomVel = RavineRandom.GetInsideCircle();
                _positionsAndVelocities[i] = new float4(spawnX, spawnZ, randomVel.x, randomVel.y);
                _accelerations[i] = RavineRandom.GetInsideCircle();
                _isMoving[i] = true;
            }
        }

        private void Update()
        {
            if (!_isUpdate) return;

            double now = SimulationClock.TimeD;
            float dt = (float)(now - _lastTime);
            _lastTime = now;
            if (dt <= 0f) return;
            if (dt > boidsInfo.maxDeltaTime) dt = boidsInfo.maxDeltaTime;

            TickFlocks(dt);

            _moveJob.DeltaTime = dt;

            _spatialGrid.Clear();
            var gridHandle = _initSpatialGridJob.Schedule(_count, 64);
            var accelHandle = _accelerationJob.Schedule(_count, 64, gridHandle);
            _moveHandle = _moveJob.Schedule(_transformAccessArray, accelHandle);
            _moveScheduled = true;
        }

        private void TickFlocks(float dt)
        {
            for (int f = 0; f < _flockTimers.Length; f++)
            {
                float t = _flockTimers[f] - dt;
                if (t > 0f)
                {
                    _flockTimers[f] = t;
                    continue;
                }
                _flockTimers[f] = NextFlockInterval();
                _otherTargets[f] = GetTargetPositionCloseToViewer();
                ChangeMoving(f);
            }
        }

        private void LateUpdate() => CompleteMove();

        private void CompleteMove()
        {
            if (!_moveScheduled) return;
            _moveHandle.Complete();
            _moveScheduled = false;
        }

        public void DisableBoids()
        {
            CompleteMove();
            _isUpdate = false;
            SetBirdsVisible(false);
        }

        private void SetBirdsVisible(bool visible)
        {
            if (_birds == null || _birdsVisible == visible) return;
            for (int i = 0; i < _birds.Length; i++)
                if (_birds[i] != null) _birds[i].SetActive(visible);
            _birdsVisible = visible;
        }

        private void OnDisable() => DisableBoids();

        private void OnDestroy() => DisposeAll();

        private void DisposeAll()
        {
            CompleteMove();
            _isUpdate = false;

            if (_positionsAndVelocities.IsCreated) _positionsAndVelocities.Dispose();
            if (_accelerations.IsCreated) _accelerations.Dispose();
            if (_otherTargets.IsCreated) _otherTargets.Dispose();
            if (_isMoving.IsCreated) _isMoving.Dispose();
            if (_flockIds.IsCreated) _flockIds.Dispose();
            if (_spatialGrid.IsCreated) _spatialGrid.Dispose();
            if (_transformAccessArray.isCreated) _transformAccessArray.Dispose();

            if (_birds != null)
            {
                for (int i = 0; i < _birds.Length; i++)
                    if (_birds[i] != null) Destroy(_birds[i]);
                _birds = null;
            }
            _transforms = null;
            _flockTimers = null;
            _flockStart = null;
            _count = 0;
        }

        private void SetUpNewValues()
        {
            float invCell = 1f / boidsInfo.CellSize;

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
                InvSlowRadius = 1f / boidsInfo.slowRadius,
                MaxSpeed = boidsInfo.velocityLimit,
                TargetWeight = boidsInfo.targetWeight,
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
                VelocityLimit = boidsInfo.velocityLimit,
                VelocityLimitSq = boidsInfo.velocityLimit * boidsInfo.velocityLimit,
                FlipRotation = quaternion.RotateY(math.radians(boidsInfo.flipYaw)),
                IdentityRotation = quaternion.identity
            };
        }

        private void ChangeMoving(int flock)
        {
            int start = _flockStart[flock];
            int end = _flockStart[flock + 1];
            if (end <= start) return;
            int changes = RavineRandom.RangeInt(1, end - start + 1);
            for (int i = 0; i < changes; i++)
            {
                int a = RavineRandom.RangeInt(start, end);
                _isMoving[a] = !_isMoving[a];
            }
        }
    }
}

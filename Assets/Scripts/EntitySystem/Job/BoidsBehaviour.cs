using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Jobs;
using UnityEngine.SceneManagement;

using TheRavine.Generator;
using TheRavine.ObjectControl;

namespace TheRavine.EntityControl
{
    public class BoidsBehaviour : MonoBehaviour
    {
        private const int ClipStride = 4;

        [SerializeField] private BoidsInfo boidsInfo;
        [SerializeField] private GameObject[] prefabs;

        [SerializeField] private bool isTest;

        private NativeArray<float3> _pos, _vel, _acc, _perchPoint, _curve0, _curve1, _curve2;
        private NativeArray<byte> _state, _flock, _flip, _flockCommand;
        private NativeArray<float> _timer, _phase, _aux, _duration, _panic;
        private NativeArray<int> _perchRef, _flockStart;
        private NativeArray<float2> _flockTargets, _flockTargetGoal, _flockAnim, _windFallback;
        private NativeArray<int> _gridCellEnd, _gridItems, _gridBucketOf;
        private int _gridMask;
        private float _invCell;

        private BirdHeightField _height;
        private BirdPerchIndex _perches;

        private TransformAccessArray _transformAccessArray;
        private Transform[] _transforms;
        private GameObject[] _birds;
        private SimpleUnitAnimator[] _animators;
        private int[] _frameKey, _clipCounts;
        private byte[] _restPose;
        private float[] _flockTimers;

        private Transform _viewer;
        private Camera _camera;
        private MapGenerator _map;
        private ObjectSystem _objects;
        private WindField _wind;
        private UnityAction<long> _onMapUpdate;
        private bool _mapDirty;

        private BirdParams _params;
        private quaternion _flipRotation;
        private JobHandle _handle;
        private double _lastTime;
        private int _count;
        private bool _isUpdate;
        private bool _scheduled;
        private bool _birdsVisible;

        private void Awake() => _onMapUpdate = OnMapUpdate;

#if UNITY_EDITOR
        private void Start()
        {
            if (isTest) StartBoids(transform);
        }
#endif

        private void OnMapUpdate(long center) => _mapDirty = true;

        private float2 ViewerXZ()
        {
            Vector3 p = _viewer != null ? _viewer.position : transform.position;
            return new float2(p.x, p.z);
        }

        private float2 GetTargetPositionCloseToViewer()
        {
            int d = boidsInfo.distanceOfTargetFromPlayer;
            return ViewerXZ() + new float2(RavineRandom.RangeInt(-d, d), RavineRandom.RangeInt(-d, d));
        }

        private float NextFlockInterval()
            => RavineRandom.RangeFloat(boidsInfo.flockRetargetInterval.x, boidsInfo.flockRetargetInterval.y);

        public void StartBoids(Transform viewer)
        {
            if (boidsInfo == null || prefabs == null || prefabs.Length == 0 || prefabs.Length > byte.MaxValue) return;
            if (boidsInfo.numberOfEntities < 0 || !(boidsInfo.CellSize > 0f)) return;

            CompleteJobs();
            _isUpdate = false;
            _viewer = viewer;

            if (!_pos.IsCreated || _count != boidsInfo.numberOfEntities || _flockTimers.Length != prefabs.Length)
            {
                DisposeAll();
                Allocate();
            }

            _params = BirdParams.From(boidsInfo, SimulationRules.Active);
            _invCell = 1f / boidsInfo.CellSize;
            _flipRotation = quaternion.RotateY(math.radians(boidsInfo.flipYaw));

            ReleaseServices();
            AcquireServices();
            ClearPerchRefs();
            RebuildWorld(ViewerXZ());
            ResetState();
            SetBirdsVisible(true);

            _lastTime = SimulationClock.TimeD;
            _isUpdate = true;
        }

        private void Allocate()
        {
            _count = boidsInfo.numberOfEntities;
            int flocks = prefabs.Length;
            int n = _count;

            _pos = new NativeArray<float3>(n, Allocator.Persistent);
            _vel = new NativeArray<float3>(n, Allocator.Persistent);
            _acc = new NativeArray<float3>(n, Allocator.Persistent);
            _perchPoint = new NativeArray<float3>(n, Allocator.Persistent);
            _curve0 = new NativeArray<float3>(n, Allocator.Persistent);
            _curve1 = new NativeArray<float3>(n, Allocator.Persistent);
            _curve2 = new NativeArray<float3>(n, Allocator.Persistent);
            _state = new NativeArray<byte>(n, Allocator.Persistent);
            _flock = new NativeArray<byte>(n, Allocator.Persistent);
            _flip = new NativeArray<byte>(n, Allocator.Persistent);
            _timer = new NativeArray<float>(n, Allocator.Persistent);
            _phase = new NativeArray<float>(n, Allocator.Persistent);
            _aux = new NativeArray<float>(n, Allocator.Persistent);
            _duration = new NativeArray<float>(n, Allocator.Persistent);
            _panic = new NativeArray<float>(n, Allocator.Persistent);
            _perchRef = new NativeArray<int>(n, Allocator.Persistent);

            _flockStart = new NativeArray<int>(flocks + 1, Allocator.Persistent);
            _flockCommand = new NativeArray<byte>(flocks, Allocator.Persistent);
            _flockTargets = new NativeArray<float2>(flocks, Allocator.Persistent);
            _flockTargetGoal = new NativeArray<float2>(flocks, Allocator.Persistent);
            _flockAnim = new NativeArray<float2>(flocks, Allocator.Persistent);
            _windFallback = new NativeArray<float2>(1, Allocator.Persistent);

            _gridMask = math.ceilpow2(math.max(n, 1) * 2) - 1;
            _gridCellEnd = new NativeArray<int>(_gridMask + 2, Allocator.Persistent);
            _gridItems = new NativeArray<int>(n, Allocator.Persistent);
            _gridBucketOf = new NativeArray<int>(n, Allocator.Persistent);

            _height = new BirdHeightField(boidsInfo.heightFieldSize, boidsInfo.heightCellStep, boidsInfo.recenterFraction);
            _perches = new BirdPerchIndex(_height.ChunkSide, boidsInfo.perchCellSize, boidsInfo.treeMargin);

            _flockTimers = new float[flocks];
            _clipCounts = new int[flocks * ClipStride];
            _transforms = new Transform[n];
            _birds = new GameObject[n];
            _animators = new SimpleUnitAnimator[n];
            _frameKey = new int[n];
            _restPose = new byte[n];

            Scene scene = gameObject.scene;
            int flockSize = n / flocks;
            int extra = n % flocks;
            int index = 0;

            for (int flock = 0; flock < flocks; flock++)
            {
                _flockStart[flock] = index;
                int size = flockSize + (flock < extra ? 1 : 0);
                for (int i = 0; i < size; i++)
                {
                    GameObject bird = Instantiate(prefabs[flock], this.transform);
                    if (bird.scene != scene) SceneManager.MoveGameObjectToScene(bird, scene);
                    _birds[index] = bird;
                    _transforms[index] = bird.transform;
                    _animators[index] = bird.GetComponentInChildren<SimpleUnitAnimator>(true);
                    _flock[index] = (byte)flock;
                    index++;
                }

                SimpleUnitAnimator anim = size > 0 ? _animators[index - 1] : null;
                float fps = anim != null && anim.FrameDuration > 0f ? 1f / anim.FrameDuration : 0f;
                int flyCount = anim != null ? anim.FrameCount(SimpleUnitAnimator.ClipFly) : 0;
                for (int c = 0; c < ClipStride; c++)
                    _clipCounts[flock * ClipStride + c] = anim != null ? anim.FrameCount(c) : 0;
                _flockAnim[flock] = new float2(fps, math.max(flyCount, 1));
            }
            _flockStart[flocks] = index;

            _birdsVisible = true;
            _transformAccessArray = new TransformAccessArray(_transforms);

#if UNITY_EDITOR
            Debug.Assert(_flockStart[flocks] == _count && _transformAccessArray.length == _count, "Boids: SoA size mismatch");
            for (int f = 0; f < flocks; f++)
                for (int i = _flockStart[f]; i < _flockStart[f + 1]; i++)
                    Debug.Assert(_flock[i] == f, "Boids: flock range mismatch");
#endif
        }

        private void AcquireServices()
        {
            if (_map is null && ServiceLocator.Services.TryGet(out MapGenerator map) && map != null)
            {
                _map = map;
                _map.onUpdate += _onMapUpdate;
                _mapDirty = true;
            }
            if (_objects is null && ServiceLocator.Services.TryGet(out ObjectSystem objects) && objects != null)
            {
                _objects = objects;
                _mapDirty = true;
            }
            if (_wind is null && ServiceLocator.Services.TryGet(out WindField wind) && wind != null) _wind = wind;
            if (_camera == null) _camera = Camera.main;
        }

        private void ReleaseServices()
        {
            if (_map is not null) _map.onUpdate -= _onMapUpdate;
            _map = null;
            _objects = null;
            _wind = null;
            _camera = null;
        }

        private void RebuildWorld(float2 viewer)
        {
            MapGenerator map = _map != null ? _map : null;
            _height.Rebuild(map, viewer, boidsInfo.maxPerchSlope, boidsInfo.waterThreshold);
            _perches.Rebuild(map, _objects, _height.ChunkOrigin, _height.ChunkSide, _perchRef, _state, _timer);
            _height.StampCanopy(_perches.Trees);
            _mapDirty = false;
        }

        private void ClearPerchRefs()
        {
            for (int i = 0; i < _count; i++) _perchRef[i] = BirdState.PerchNone;
        }

        private void ResetState()
        {
            for (int f = 0; f < _flockTargets.Length; f++)
            {
                _flockTargets[f] = GetTargetPositionCloseToViewer();
                _flockTargetGoal[f] = _flockTargets[f];
                _flockTimers[f] = NextFlockInterval();
                _flockCommand[f] = 0;
            }

            BirdHeightSampler height = _height.Sampler;
            int near = boidsInfo.nearTheTarget;

            for (int i = 0; i < _count; i++)
            {
                float2 target = _flockTargets[_flock[i]];
                float2 xz = target + new float2(RavineRandom.RangeInt(-near, near), RavineRandom.RangeInt(-near, near));
                float y = height.SampleCanopy(xz) + boidsInfo.cruiseAGL + RavineRandom.GetInsideCircle().x * boidsInfo.ySpawnSpread;
                float3 p = new(xz.x, y, xz.y);
                Vector2 rv = RavineRandom.GetInsideCircle();

                _pos[i] = p;
                _vel[i] = new float3(rv.x, 0f, rv.y);
                _acc[i] = float3.zero;
                _state[i] = BirdState.Flying;
                _timer[i] = 0f;
                _phase[i] = RavineRandom.RangeFloat(0f, _flockAnim[_flock[i]].y);
                _aux[i] = 0f;
                _duration[i] = 1f;
                _panic[i] = BirdState.PanicNone;
                _flip[i] = 0;
                _frameKey[i] = -1;

                _transforms[i].SetPositionAndRotation(p, Quaternion.identity);
            }
        }

        private void Update()
        {
            if (!_isUpdate || _count == 0) return;

            double now = SimulationClock.TimeD;
            float dt = (float)(now - _lastTime);
            _lastTime = now;
            if (dt <= 0f) return;
            if (dt > boidsInfo.maxDeltaTime) dt = boidsInfo.maxDeltaTime;

            AcquireServices();

            float2 viewer = ViewerXZ();
            if (_mapDirty || _height.NeedsRecenter(viewer)) RebuildWorld(viewer);

            TickFlocks(dt);

            float3 camRight = new(1f, 0f, 0f);
            float2 camForward = new(0f, 1f);
            if (_camera != null)
            {
                Transform ct = _camera.transform;
                camRight = ct.right;
                Vector3 fwd = ct.forward;
                camForward = math.normalizesafe(new float2(fwd.x, fwd.z), camForward);
            }

            float time = SimulationClock.Time;
            WindSampler wind = _wind != null
                ? _wind.GetSampler(time, _windFallback)
                : WindField.Constant(new float2(0f, 1f), 0f, time, _windFallback);

            uint seed = (uint)RavineRandom.RangeInt(1, int.MaxValue);
            BirdHeightSampler height = _height.Sampler;
            BirdPerchGrid trees = _perches.Grid;

            JobHandle slot = new BirdSlotJob
            {
                Trees = _perches.Trees,
                TreeCellEnd = trees.CellEnd,
                TreeItems = trees.Items,
                TreeMask = trees.Mask,
                TreeInvCell = trees.InvCell,
                Height = height,
                Pos = _pos,
                State = _state,
                PerchRef = _perchRef,
                PerchPoint = _perchPoint,
                Timer = _timer,
                Aux = _aux,
                FlockStart = _flockStart,
                FlockCommand = _flockCommand,
                P = _params,
                CameraForward = camForward,
                Seed = seed,
                HeightValid = _map != null
            }.Schedule();

            JobHandle grid = new BirdGridJob
            {
                Pos = _pos,
                CellEnd = _gridCellEnd,
                Items = _gridItems,
                BucketOf = _gridBucketOf,
                Mask = _gridMask,
                InvCell = _invCell
            }.Schedule();

            JobHandle accel = new AccelerationJob
            {
                Pos = _pos,
                Vel = _vel,
                PerchPoint = _perchPoint,
                State = _state,
                Flock = _flock,
                PerchRef = _perchRef,
                FlockTargets = _flockTargets,
                Acc = _acc,
                Panic = _panic,
                Grid = new BirdGrid { CellEnd = _gridCellEnd, Items = _gridItems, Mask = _gridMask, InvCell = _invCell },
                Trees = trees,
                Height = height,
                Wind = wind,
                P = _params,
                Viewer = viewer,
                Seed = seed,
                Time = (float)SimulationClock.Time,
            }.Schedule(_count, 32, JobHandle.CombineDependencies(slot, grid));

            _handle = new MoveJob
            {
                Pos = _pos,
                Vel = _vel,
                PerchPoint = _perchPoint,
                Curve0 = _curve0,
                Curve1 = _curve1,
                Curve2 = _curve2,
                State = _state,
                Flip = _flip,
                Timer = _timer,
                Phase = _phase,
                Aux = _aux,
                Duration = _duration,
                Panic = _panic,
                PerchRef = _perchRef,
                Acc = _acc,
                Flock = _flock,
                FlockAnim = _flockAnim,
                Trees = _perches.Trees,
                Height = height,
                Wind = wind,
                P = _params,
                DeltaTime = dt,
                Viewer = viewer,
                CameraRight = camRight,
                FlipRotation = _flipRotation,
                IdentityRotation = quaternion.identity,
                Seed = seed * 747796405u + 2891336453u
            }.Schedule(_transformAccessArray, accel);

            _scheduled = true;
        }

        private void TickFlocks(float dt)
        {
            float smooth = math.saturate(dt / math.max(boidsInfo.targetSmoothTime, 0.01f));

            for (int f = 0; f < _flockTimers.Length; f++)
            {
                _flockTargets[f] = math.lerp(_flockTargets[f], _flockTargetGoal[f], smooth);

                float t = _flockTimers[f] - dt;
                if (t > 0f) { _flockTimers[f] = t; continue; }
                _flockTimers[f] = NextFlockInterval();

                if (RavineRandom.RangeFloat() < boidsInfo.flockPerchChance) _flockCommand[f] = 1;
                else _flockTargetGoal[f] = GetTargetPositionCloseToViewer();
            }
}

        private void LateUpdate()
        {
            if (!_scheduled) return;
            CompleteJobs();
#if UNITY_EDITOR
            ValidatePerches();
#endif
            UpdateAnimation();
        }

#if UNITY_EDITOR
        private void ValidatePerches()
        {
            NativeArray<TreePerch> trees = _perches.Trees;
            int held = 0;
            for (int i = 0; i < _count; i++)
            {
                int r = _perchRef[i];
                if (r < 0) continue;
                int t = r >> BirdState.SlotShift;
                Debug.Assert(t < trees.Length && (trees[t].Slots & (1ul << (r & BirdState.SlotMask))) != 0, "Boids: perch ref without slot");
                held++;
            }
            int used = 0;
            for (int t = 0; t < trees.Length; t++) used += math.countbits(trees[t].Slots);
            Debug.Assert(used == held, "Boids: perch slot count mismatch");
        }
#endif

        private void UpdateAnimation()
        {
            float takeOff = boidsInfo.takeOffDuration;
            for (int i = 0; i < _count; i++)
            {
                SimpleUnitAnimator anim = _animators[i];
                if (anim == null) continue;

                int baseClip = _flock[i] * ClipStride;
                int clip, index;

                switch (_state[i])
                {
                    case BirdState.Perched:
                        clip = SimpleUnitAnimator.ClipRest;
                        if (_frameKey[i] >> 16 != clip)
                            _restPose[i] = (byte)RavineRandom.RangeInt(0, math.max(_clipCounts[baseClip + clip], 1));
                        index = _restPose[i];
                        break;
                    case BirdState.Landing:
                        clip = SimpleUnitAnimator.ClipLanding;
                        index = Progress(_timer[i] / _duration[i], _clipCounts[baseClip + clip]);
                        break;
                    case BirdState.TakeOff:
                        clip = SimpleUnitAnimator.ClipTakeOff;
                        index = Progress(_timer[i] / takeOff, _clipCounts[baseClip + clip]);
                        break;
                    default:
                        clip = SimpleUnitAnimator.ClipFly;
                        index = math.min((int)_phase[i], math.max(_clipCounts[baseClip + clip] - 1, 0));
                        break;
                }

                int key = (clip << 16) | index;
                if (key == _frameKey[i]) continue;
                _frameKey[i] = key;
                anim.SetFrame(clip, index);
            }
        }

        private static int Progress(float t, int count) =>
            count <= 1 ? 0 : math.min((int)(math.saturate(t) * count), count - 1);

        private void CompleteJobs()
        {
            if (!_scheduled) return;
            _handle.Complete();
            _scheduled = false;
        }

        public void DisableBoids()
        {
            CompleteJobs();
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

        private void OnDestroy()
        {
            ReleaseServices();
            DisposeAll();
        }

        private void DisposeAll()
        {
            CompleteJobs();
            _isUpdate = false;

            if (_pos.IsCreated) _pos.Dispose();
            if (_vel.IsCreated) _vel.Dispose();
            if (_acc.IsCreated) _acc.Dispose();
            if (_perchPoint.IsCreated) _perchPoint.Dispose();
            if (_curve0.IsCreated) _curve0.Dispose();
            if (_curve1.IsCreated) _curve1.Dispose();
            if (_curve2.IsCreated) _curve2.Dispose();
            if (_state.IsCreated) _state.Dispose();
            if (_flock.IsCreated) _flock.Dispose();
            if (_flip.IsCreated) _flip.Dispose();
            if (_timer.IsCreated) _timer.Dispose();
            if (_phase.IsCreated) _phase.Dispose();
            if (_aux.IsCreated) _aux.Dispose();
            if (_duration.IsCreated) _duration.Dispose();
            if (_panic.IsCreated) _panic.Dispose();
            if (_perchRef.IsCreated) _perchRef.Dispose();
            if (_flockStart.IsCreated) _flockStart.Dispose();
            if (_flockCommand.IsCreated) _flockCommand.Dispose();
            if (_flockTargets.IsCreated) _flockTargets.Dispose();
            if (_flockTargetGoal.IsCreated) _flockTargetGoal.Dispose();
            if (_flockAnim.IsCreated) _flockAnim.Dispose();
            if (_windFallback.IsCreated) _windFallback.Dispose();
            if (_gridCellEnd.IsCreated) _gridCellEnd.Dispose();
            if (_gridItems.IsCreated) _gridItems.Dispose();
            if (_gridBucketOf.IsCreated) _gridBucketOf.Dispose();
            if (_transformAccessArray.isCreated) _transformAccessArray.Dispose();

            _height?.Dispose();
            _height = null;
            _perches?.Dispose();
            _perches = null;

            if (_birds != null)
            {
                for (int i = 0; i < _birds.Length; i++)
                    if (_birds[i] != null) Destroy(_birds[i]);
                _birds = null;
            }
            _transforms = null;
            _animators = null;
            _flockTimers = null;
            _count = 0;
        }
    }
}

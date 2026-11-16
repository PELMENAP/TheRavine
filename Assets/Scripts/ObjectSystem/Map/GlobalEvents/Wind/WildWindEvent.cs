using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using TheRavine.Base;
using TheRavine.Extensions;

namespace TheRavine.Generator
{
    public sealed class WildWindEvent : IMapEvent
    {
        private readonly MapGenerator _map;
        private readonly WildWindRules _rules;
        private readonly RavineProfile _ravine;
        private readonly int _count;

        private NativeArray<float3> _position;
        private NativeArray<float3> _velocity;
        private NativeArray<float3> _target;
        private NativeArray<float> _lost;
        private NativeArray<float3> _reports;
        private NativeArray<float2> _windFallback;
        private int _reportCount;
        private WindField _wind;
        private WildWindView _view;
        private float _accumulator;
        private bool _seeded;
        private bool _disposed;

        public WildWindRules Rules => _rules;
        public bool Active { get; private set; }
        public int StepIndex { get; private set; }
        public float LastStep { get; private set; }
        public int GustCount => _count;
        public bool IsDisposed => _disposed;

        public WildWindEvent(MapGenerator map, WildWindRules rules)
        {
            _map = map;
            _rules = rules;
            _ravine = map.Ravine;
            _count = math.max(rules.gustCount, 1);

            _position = new NativeArray<float3>(_count, Allocator.Persistent);
            _velocity = new NativeArray<float3>(_count, Allocator.Persistent);
            _target = new NativeArray<float3>(_count, Allocator.Persistent);
            _lost = new NativeArray<float>(_count, Allocator.Persistent);
            _reports = new NativeArray<float3>(math.max(rules.reportCapacity, 1), Allocator.Persistent);
            _windFallback = new NativeArray<float2>(1, Allocator.Persistent);
        }

        public void AttachView(WildWindView view) => _view = view;

        public float3 GustPosition(int index) => _position[index];
        public float3 GustVelocity(int index) => _velocity[index];

        public bool InBand(float y) => WildWindMath.InBand(y, _rules.bandBottom, _rules.bandTop);

        public void ReportCreature(float3 position)
        {
            if (_disposed || !Active || !InBand(position.y) || _reportCount >= _reports.Length) return;
            _reports[_reportCount++] = position;
        }

        public float DamageAt(float3 position)
        {
            if (_disposed || !Active) return 0f;
            float fade = WildWindMath.BandFade(position.y, _rules.bandBottom, _rules.bandTop, _rules.fadeWidth);
            if (fade <= 0f) return 0f;

            float sum = 0f;
            for (int g = 0; g < _count; g++)
                sum += WildWindMath.GustDamage(math.distance(position, _position[g]), _rules.gustRadius, _rules.damagePerSecond, fade);
            return sum;
        }

        private float Reach => (_map.NalReach + 0.5f) * MapGenerator.chunkSize;

        private float2 WindowCenter
        {
            get
            {
                long center = _map.WindowCenter;
                return new float2(Position2Int.GetX(center) + 0.5f, Position2Int.GetY(center) + 0.5f) * MapGenerator.chunkSize;
            }
        }

        public void Step(double now, float dt)
        {
            if (_disposed) return;

            float2 center = WindowCenter;
            float reach = Reach;
            Active = WildWindMath.Reaches(center.y, reach, _ravine.HalfWidth);
            if (!Active)
            {
                _seeded = false;
                _accumulator = 0f;
                _reportCount = 0;
                return;
            }

            if (!_seeded) Seed(center.x, reach);

            _accumulator += dt;
            if (_accumulator < _rules.stepInterval) return;
            float step = _accumulator;
            _accumulator = 0f;

            if (_wind == null) ServiceLocator.Services.TryGet(out _wind);
            WindSampler sampler = _wind != null
                ? _wind.GetSampler((float)now, _windFallback)
                : WindField.Constant(float2.zero, 0f, (float)now, _windFallback);

            new WildWindStepJob
            {
                Position = _position,
                Velocity = _velocity,
                Target = _target,
                Lost = _lost,
                Reports = _reports,
                ReportCount = _reportCount,
                Wind = sampler,
                Ravine = _ravine,
                Bottom = _rules.bandBottom,
                Top = math.max(_rules.bandTop, _rules.bandBottom),
                Speed = _rules.gustSpeed,
                Drift = _rules.driftScale,
                LostSeconds = _rules.lostTargetSeconds,
                Dt = step,
                CenterX = center.x,
                Reach = reach
            }.Run();

            _reportCount = 0;
            LastStep = step;
            StepIndex++;
        }

        private void Seed(float centerX, float reach)
        {
            float y = (_rules.bandBottom + math.max(_rules.bandTop, _rules.bandBottom)) * 0.5f;
            for (int g = 0; g < _count; g++)
            {
                float side = (g & 1) == 0 ? 1f : -1f;
                float x = centerX + reach * ((g + RavineRandom.RangeFloat()) * 2f / _count - 1f);
                _position[g] = new float3(x, y, WildWindMath.WallZ(in _ravine, y, side));
                _velocity[g] = float3.zero;
                _target[g] = _position[g];
                _lost[g] = -1f;
            }
            _seeded = true;
        }

        public void Render(Camera camera) => _view?.Render(camera);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Active = false;

            _view?.Dispose();
            _view = null;
            if (_position.IsCreated) _position.Dispose();
            if (_velocity.IsCreated) _velocity.Dispose();
            if (_target.IsCreated) _target.Dispose();
            if (_lost.IsCreated) _lost.Dispose();
            if (_reports.IsCreated) _reports.Dispose();
            if (_windFallback.IsCreated) _windFallback.Dispose();
            _wind = null;
        }
    }

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    public struct WildWindStepJob : IJob
    {
        public NativeArray<float3> Position;
        public NativeArray<float3> Velocity;
        public NativeArray<float3> Target;
        public NativeArray<float> Lost;
        [ReadOnly] public NativeArray<float3> Reports;
        public int ReportCount;
        public WindSampler Wind;
        public RavineProfile Ravine;
        public float Bottom;
        public float Top;
        public float Speed;
        public float Drift;
        public float LostSeconds;
        public float Dt;
        public float CenterX;
        public float Reach;

        public void Execute()
        {
            for (int g = 0; g < Position.Length; g++)
            {
                float3 p = Position[g];
                float side = p.z >= 0f ? 1f : -1f;

                int best = -1;
                float bestD2 = float.MaxValue;
                for (int r = 0; r < ReportCount; r++)
                {
                    float d2 = math.distancesq(Reports[r], p);
                    if (d2 < bestD2) { bestD2 = d2; best = r; }
                }

                float lost = Lost[g];
                if (best >= 0)
                {
                    Target[g] = Reports[best];
                    lost = 0f;
                }
                else if (lost >= 0f)
                {
                    lost += Dt;
                    if (lost > LostSeconds) lost = -1f;
                }
                Lost[g] = lost;

                float3 next;
                if (lost >= 0f)
                {
                    float3 goal = Target[g];
                    goal.y = math.clamp(goal.y, Bottom, Top);
                    next = WildWindMath.StepToward(p, goal, Speed, Dt);
                }
                else
                {
                    float2 wind = Wind.Sample(p.xz);
                    float3 home = new(p.x + wind.x * Drift * Dt, p.y, WildWindMath.WallZ(in Ravine, p.y, side));
                    next = WildWindMath.StepToward(new float3(home.x, p.y, p.z), home, Speed, Dt);
                }

                next.y = math.clamp(next.y, Bottom, Top);
                Velocity[g] = Dt > 0f ? (next - p) / Dt : float3.zero;
                next.x = WildWindMath.WrapX(next.x, CenterX, Reach);
                Position[g] = next;
            }
        }
    }
}

using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

using TheRavine.Base;
using TheRavine.Extensions;

namespace TheRavine.Generator
{
    public sealed partial class WeatherEvent : IMapEvent
    {
        private static readonly ProfilerMarker StepMarker = new("WeatherEvent.Step");

        private readonly GlobalMapEvents _events;
        private readonly MapGenerator _map;
        private readonly WeatherRules _rules;
        private readonly int _side;
        private readonly float _texel;
        private readonly float _invTexel;

        private NativeArray<float2> _position;
        private NativeArray<float2> _velocity;
        private NativeArray<float> _radius;
        private NativeArray<float> _moisture;
        private NativeArray<float> _charge;
        private NativeArray<float> _age;
        private NativeArray<float> _life;
        private int _count;

        private NativeArray<float4> _mapA;
        private NativeArray<float4> _mapB;
        private NativeArray<int2> _keys;
        private NativeArray<float2> _windFallback;
        private int2 _origin;

        private WindField _wind;
        private DayCycle _dayCycle;
        private WeatherView _view;
        private float _accumulator;
        private float _heat;
        private bool _disposed;

        public WeatherRules Rules => _rules;
        public int Version { get; private set; }
        public int StepIndex { get; private set; }
        public float LastStep { get; private set; }
        public double StepTime { get; private set; }
        public float Heat => _heat;
        public int CellCount => _count;
        public int Side => _side;
        public float TexelSize => _texel;
        public int2 Origin => _origin;
        public bool IsDisposed => _disposed;
        public NativeArray<float4>.ReadOnly Map => _mapA.AsReadOnly();
        public NativeArray<float4>.ReadOnly MapWind => _mapB.AsReadOnly();

        public WeatherEvent(GlobalMapEvents events, MapGenerator map, WeatherRules rules)
        {
            _events = events;
            _map = map;
            _rules = rules;
            _side = math.max(rules.mapSide, 8);
            _texel = math.max(rules.texelSize, 1f);
            _invTexel = 1f / _texel;

            int cells = math.max(rules.maxCells, 1);
            _position = new NativeArray<float2>(cells, Allocator.Persistent);
            _velocity = new NativeArray<float2>(cells, Allocator.Persistent);
            _radius = new NativeArray<float>(cells, Allocator.Persistent);
            _moisture = new NativeArray<float>(cells, Allocator.Persistent);
            _charge = new NativeArray<float>(cells, Allocator.Persistent);
            _age = new NativeArray<float>(cells, Allocator.Persistent);
            _life = new NativeArray<float>(cells, Allocator.Persistent);

            int texels = _side * _side;
            _mapA = new NativeArray<float4>(texels, Allocator.Persistent);
            _mapB = new NativeArray<float4>(texels, Allocator.Persistent);
            _keys = new NativeArray<int2>(texels, Allocator.Persistent);
            _windFallback = new NativeArray<float2>(1, Allocator.Persistent);
            _strikes = new NativeArray<WeatherStrike>(math.max(rules.strikeHistory, 1), Allocator.Persistent);
            _strikeBounds = EmptyBounds;
            for (int i = 0; i < texels; i++) _keys[i] = new int2(int.MinValue, int.MinValue);
        }

        private static float4 EmptyBounds => new(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);

        public void AttachView(WeatherView view) => _view = view;

        public float2 CellPosition(int i) => _position[i];
        public float2 CellVelocity(int i) => _velocity[i];
        public float CellRadius(int i) => _radius[i];
        public float CellMoisture(int i) => _moisture[i];
        public float CellCharge(int i) => _charge[i];
        public float CellEnvelope(int i) => WeatherMath.Envelope(_age[i], _life[i]);
        public float CellEvaporation(int i)
            => WeatherMath.Evaporation(_heat, 1f - WetnessAt(_position[i]), _rules.virgaStrength);

        public float RainAt(float2 p) => _disposed ? 0f : BilinearA(p).x;
        public float CloudCoverAt(float2 p) => _disposed ? 0f : BilinearA(p).y;
        public float ChargeAt(float2 p) => _disposed ? 0f : BilinearA(p).z;
        public float WetnessAt(float2 p) => _disposed ? 0f : BilinearA(p).w;
        public float2 WindAt(float2 p) => _disposed ? float2.zero : BilinearB(p).xy;
        public float PressureAt(float2 p) => _disposed ? 0f : BilinearB(p).z;

        public bool Spawn(float2 position, float moisture, float charge, float radius, float life)
        {
            if (_disposed || _count >= _position.Length) return false;

            int i = _count++;
            _position[i] = position;
            _velocity[i] = DriftTarget(CurrentWind());
            _radius[i] = math.max(radius, 1f);
            _moisture[i] = math.saturate(moisture);
            _charge[i] = math.saturate(charge);
            _age[i] = 0f;
            _life[i] = math.max(life, 1f);
            return true;
        }

        public void Step(double now, float dt)
        {
            if (_disposed) return;

            _accumulator += dt;
            if (_accumulator < _rules.stepInterval) return;
            float step = _accumulator;
            _accumulator = 0f;

            using (StepMarker.Auto())
            {
                StepTime = now;
                long chunk = _map.WindowCenter;
                float2 center = new float2(Position2Int.GetX(chunk) + 0.5f, Position2Int.GetY(chunk) + 0.5f) * MapGenerator.chunkSize;
                float half = _side * _texel * 0.5f;
                float area = math.min((_map.NalReach + 0.5f) * MapGenerator.chunkSize + _rules.spawnMargin, half);
                float limit = area + _rules.spawnMargin;

                float dayPhase = CurrentDayPhase();
                float season = _events.SeasonValue(now);
                bool winter = _events.IsWinter(now);
                _heat = WeatherMath.Heat(dayPhase, season, _rules);

                float2 wind = CurrentWind();
                MoveAndAge(step, wind, center, limit);
                SpawnStep(step, dayPhase, season, winter, wind, center, area, limit);
                StrikeStep(step);

                _origin = new int2((int)math.floor((center.x - half) * _invTexel), (int)math.floor((center.y - half) * _invTexel));
                new WeatherFieldJob
                {
                    Position = _position,
                    Radius = _radius,
                    Moisture = _moisture,
                    Charge = _charge,
                    Age = _age,
                    Life = _life,
                    Count = _count,
                    MapA = _mapA,
                    MapB = _mapB,
                    Keys = _keys,
                    Side = _side,
                    Origin = _origin,
                    Texel = _texel,
                    Wind = wind,
                    Heat = _heat,
                    Dt = step,
                    Rules = new WeatherFieldParams
                    {
                        Virga = _rules.virgaStrength,
                        WetnessGain = _rules.wetnessGain,
                        WetnessDry = _rules.wetnessDryPerSecond,
                        WetnessDryHeat = _rules.wetnessDryHeat,
                        WetnessDryWind = _rules.wetnessDryWind,
                        PressurePerMoisture = _rules.pressurePerMoisture,
                        PressurePerCharge = _rules.pressurePerCharge,
                        GustStrength = _rules.gustStrength,
                        CloudSpread = _rules.cloudSpread,
                        CloudBase = _rules.cloudBase
                    }
                }.Schedule(_side * _side, 64).Complete();

                LastStep = step;
                StepIndex++;
                Version++;
            }
        }

        private float CurrentDayPhase()
        {
            if (_dayCycle == null && !ServiceLocator.Services.TryGet(out _dayCycle)) return 0f;
            return _dayCycle.NormalizedTime.CurrentValue;
        }

        private float2 CurrentWind()
        {
            if (_wind == null) ServiceLocator.Services.TryGet(out _wind);
            double now = SimulationClock.TimeD;
            WindSampler sampler = _wind != null
                ? _wind.GetSampler((float)now, _windFallback)
                : WindField.Constant(float2.zero, 0f, (float)now, _windFallback);
            return sampler.Mean;
        }

        private float2 DriftTarget(float2 wind) => wind * _rules.driftFactor;

        private void MoveAndAge(float step, float2 wind, float2 center, float limit)
        {
            float2 target = DriftTarget(wind);
            float relax = 1f - math.exp(-step / math.max(_rules.inertiaSeconds, 0.1f));

            for (int i = _count - 1; i >= 0; i--)
            {
                float age = _age[i] + step;
                float2 velocity = math.lerp(_velocity[i], target, relax);
                float2 position = _position[i] + velocity * step;
                float2 away = math.abs(position - center);
                if (age >= _life[i] || math.max(away.x, away.y) > limit + _radius[i])
                {
                    RemoveSwapBack(i);
                    continue;
                }
                _age[i] = age;
                _velocity[i] = velocity;
                _position[i] = position;
            }
        }

        private void RemoveSwapBack(int index)
        {
            int last = --_count;
            if (index == last) return;
            _position[index] = _position[last];
            _velocity[index] = _velocity[last];
            _radius[index] = _radius[last];
            _moisture[index] = _moisture[last];
            _charge[index] = _charge[last];
            _age[index] = _age[last];
            _life[index] = _life[last];
        }

        private void SpawnStep(float step, float dayPhase, float season, bool winter, float2 wind,
            float2 center, float area, float limit)
        {
            float expected = WeatherMath.SpawnRate(_rules, dayPhase, season, winter) * step * (1f / WeatherMath.SecondsPerHour);
            int spawns = (int)expected;
            if (RavineRandom.RangeFloat() < expected - spawns) spawns++;

            float2 up = -math.normalizesafe(wind);
            for (int n = 0; n < spawns && _count < _position.Length; n++)
            {
                float2 point;
                if (math.lengthsq(up) > 0.5f && RavineRandom.RangeFloat() < _rules.upwindShare)
                {
                    float2 side = new float2(-up.y, up.x);
                    point = center + up * (area + RavineRandom.RangeFloat() * _rules.spawnMargin)
                          + side * RavineRandom.RangeFloat(-area, area);
                    point = center + math.clamp(point - center, -limit, limit);
                }
                else
                {
                    point = center + new float2(RavineRandom.RangeFloat(-area, area), RavineRandom.RangeFloat(-area, area));
                }

                float moisture = RavineRandom.RangeFloat(_rules.moistureMin, _rules.moistureMax);
                float charge = RavineRandom.RangeFloat(_rules.chargeMin, _rules.chargeMax) * WeatherMath.ChargeGrowth(_heat, winter, _rules);
                float share = math.unlerp(_rules.moistureMin, _rules.moistureMax, moisture);
                float life = WeatherMath.Life(share, RavineRandom.RangeFloat(), _rules);
                Spawn(point, moisture, charge, RavineRandom.RangeFloat(_rules.radiusMin, _rules.radiusMax), life);
            }
        }

        private float4 BilinearA(float2 p) => Bilinear(_mapA, p);
        private float4 BilinearB(float2 p) => Bilinear(_mapB, p);

        private float4 Bilinear(NativeArray<float4> map, float2 p)
        {
            float2 g = p * _invTexel - 0.5f;
            float2 f = math.floor(g);
            float2 t = g - f;
            int x0 = (int)f.x;
            int z0 = (int)f.y;
            int xMax = _origin.x + _side - 1;
            int zMax = _origin.y + _side - 1;

            int xa = Wrap(math.clamp(x0, _origin.x, xMax));
            int xb = Wrap(math.clamp(x0 + 1, _origin.x, xMax));
            int za = Wrap(math.clamp(z0, _origin.y, zMax)) * _side;
            int zb = Wrap(math.clamp(z0 + 1, _origin.y, zMax)) * _side;

            float4 a = math.lerp(map[za + xa], map[za + xb], t.x);
            float4 b = math.lerp(map[zb + xa], map[zb + xb], t.x);
            return math.lerp(a, b, t.y);
        }

        private int Wrap(int key)
        {
            int m = key % _side;
            return m < 0 ? m + _side : m;
        }

        public void Render(Camera camera) => _view?.Render(camera);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _view?.Dispose();
            _view = null;
            if (_position.IsCreated) _position.Dispose();
            if (_velocity.IsCreated) _velocity.Dispose();
            if (_radius.IsCreated) _radius.Dispose();
            if (_moisture.IsCreated) _moisture.Dispose();
            if (_charge.IsCreated) _charge.Dispose();
            if (_age.IsCreated) _age.Dispose();
            if (_life.IsCreated) _life.Dispose();
            if (_mapA.IsCreated) _mapA.Dispose();
            if (_mapB.IsCreated) _mapB.Dispose();
            if (_keys.IsCreated) _keys.Dispose();
            if (_windFallback.IsCreated) _windFallback.Dispose();
            if (_strikes.IsCreated) _strikes.Dispose();
            _wind = null;
            _dayCycle = null;
            _count = 0;
        }
    }
}

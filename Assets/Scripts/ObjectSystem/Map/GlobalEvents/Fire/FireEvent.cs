using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

using TheRavine.Extensions;
using TheRavine.ObjectControl;

namespace TheRavine.Generator
{
    public struct FireCell
    {
        public float Left;
        public float Total;
        public float Size;
    }

    public sealed class FireEvent : IMapEvent, IRandomTickHandler
    {
        private const float InvScale = 1f / MapGenerator.scale;
        private const float HalfCell = MapGenerator.scale * 0.5f;

        private readonly GlobalMapEvents _events;
        private readonly MapGenerator _map;
        private readonly FireRules _rules;
        private readonly float[] _sizeById;
        private readonly int _heatReach;

        private NativeHashMap<long, FireCell> _burning;
        private NativeList<long> _keys;
        private NativeArray<float2> _windFallback;
        private WindField _wind;
        private FireView _view;
        private float _accumulator;
        private float4 _heatBounds;
        private bool _disposed;

        public FireRules Rules => _rules;
        public int BurningCount => _burning.IsCreated ? _burning.Count : 0;
        public NativeHashMap<long, FireCell>.ReadOnly Burning => _burning.AsReadOnly();
        public int Version { get; private set; }
        public int StepIndex { get; private set; }
        public double StepTime { get; private set; }
        public float LastStep { get; private set; }
        public float4 HeatBounds => _heatBounds;
        public bool IsDisposed => _disposed;

        public FireEvent(GlobalMapEvents events, MapGenerator map, ObjectInfoRegistry registry, FireRules rules)
        {
            _events = events;
            _map    = map;
            _rules  = rules;

            int capacity = registry != null ? registry.IdCapacity : 0;
            _sizeById = new float[math.max(capacity, 1)];
            NativeArray<CollisionShape> shapes = map.Collision != null ? map.Collision.Set.Shapes : default;
            for (int id = 0; id < capacity; id++)
            {
                ObjectInfo info = registry.Get(id);
                if (info == null || !info.ObjectCanBurn) continue;
                CollisionShape shape = shapes.IsCreated && id < shapes.Length ? shapes[id] : default;
                _sizeById[id] = FireMath.Size(in shape, rules);
            }

            _heatReach = (int)math.ceil(FireMath.HeatRadius(math.max(rules.maxSize, rules.minSize), rules) * InvScale);
            _heatBounds = EmptyBounds;

            int burning = math.max(rules.maxBurning, 16);
            _burning      = new NativeHashMap<long, FireCell>(burning, Allocator.Persistent);
            _keys         = new NativeList<long>(burning, Allocator.Persistent);
            _windFallback = new NativeArray<float2>(1, Allocator.Persistent);
        }

        private static float4 EmptyBounds => new(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);

        public void AttachView(FireView view) => _view = view;

        public float SizeOf(int prefabId) => (uint)prefabId < (uint)_sizeById.Length ? _sizeById[prefabId] : 0f;

        public bool Accepts(int prefabId) => SizeOf(prefabId) > 0f;

        public void OnRandomTick(in RandomTickEvent e, double now)
        {
            if (_disposed || _burning.Count >= _rules.maxBurning) return;

            float season = FireMath.SeasonFactor(_events.SeasonValue(now), _events.IsWinter(now), _rules.seasonBias);
            if (season <= 0f) return;

            int worldX = ((Position2Int.GetX(e.Chunk) << MapGenerator.RowShift) + (e.Cell & MapGenerator.RowMask)) << MapGenerator.CellShift;
            int worldZ = ((Position2Int.GetY(e.Chunk) << MapGenerator.RowShift) + (e.Cell >> MapGenerator.RowShift)) << MapGenerator.CellShift;
            long cell = Position2Int.Pack(worldX >> MapGenerator.CellShift, worldZ >> MapGenerator.CellShift);
            if (_burning.ContainsKey(cell)) return;
            if (RavineRandom.RangeFloat() >= _rules.ignitionChance * season) return;

            long packed = Position2Int.Pack(worldX, worldZ);
            if (_map.Commands.HasPendingRemove(packed)
                || !_map.TryGetObject(packed, out ObjectInstInfo inst)
                || inst.PrefabID != e.PrefabId) return;

            float size = SizeOf(inst.PrefabID);
            if (size > 0f) Ignite(cell, size);
        }

        public bool TryIgnite(long cell)
        {
            if (_disposed || _burning.ContainsKey(cell)) return false;
            if (!TryResolve(cell, out long primary, out float size)) return false;
            return !_burning.ContainsKey(primary) && Ignite(primary, size);
        }

        private bool Ignite(long cell, float size)
        {
            if (_burning.Count >= _rules.maxBurning) return false;
            float total = FireMath.BurnSeconds(size, _rules);
            if (!_burning.TryAdd(cell, new FireCell { Left = total, Total = total, Size = size })) return false;
            Version++;
            return true;
        }

        private bool TryResolve(long cell, out long primary, out float size)
        {
            primary = cell;
            size = 0f;

            long packed = Position2Int.Pack(Position2Int.GetX(cell) << MapGenerator.CellShift, Position2Int.GetY(cell) << MapGenerator.CellShift);
            if (_map.Commands.HasPendingRemove(packed) || !_map.TryGetObject(packed, out ObjectInstInfo inst)) return false;

            size = SizeOf(inst.PrefabID);
            if (size <= 0f) return false;

            primary = CellOf(inst.Position.x, inst.Position.z);
            return true;
        }

        public void Step(double now, float dt)
        {
            if (_disposed) return;
            if (_burning.IsEmpty)
            {
                _accumulator = 0f;
                _heatBounds = EmptyBounds;
                return;
            }

            _accumulator += dt;
            if (_accumulator < _rules.stepInterval) return;
            float step = _accumulator;
            _accumulator = 0f;
            StepTime = now;
            Version++;

            if (_wind == null) ServiceLocator.Services.TryGet(out _wind);
            WindSampler sampler = _wind != null
                ? _wind.GetSampler((float)now, _windFallback)
                : WindField.Constant(float2.zero, 0f, (float)now, _windFallback);

            _keys.Clear();
            foreach (var kv in _burning) _keys.Add(kv.Key);

            WorldCommandBuffer commands = _map.Commands;
            float4 bounds = EmptyBounds;
            for (int k = 0; k < _keys.Length; k++)
            {
                long cell = _keys[k];
                if (!_burning.TryGetValue(cell, out FireCell fire)) continue;

                long packed = Position2Int.Pack(
                    Position2Int.GetX(cell) << MapGenerator.CellShift,
                    Position2Int.GetY(cell) << MapGenerator.CellShift);

                if (commands.HasPendingRemove(packed)
                    || !_map.TryGetObject(packed, out ObjectInstInfo inst)
                    || SizeOf(inst.PrefabID) <= 0f)
                {
                    _burning.Remove(cell);
                    continue;
                }

                fire.Left -= step;
                if (fire.Left <= 0f)
                {
                    commands.Push(new WorldCommand
                    {
                        Kind = WorldCommand.Remove,
                        Pos = packed,
                        PrefabId = inst.PrefabID,
                        ExpectedPrefabId = inst.PrefabID
                    });
                    _burning.Remove(cell);
                    continue;
                }

                _burning[cell] = fire;
                Spread(cell, fire.Size, in sampler);

                float2 center = CellCenter(cell);
                float radius = FireMath.HeatRadius(fire.Size, _rules);
                bounds.xy = math.min(bounds.xy, center - radius);
                bounds.zw = math.max(bounds.zw, center + radius);
            }

            _heatBounds = bounds;
            LastStep = step;
            StepIndex++;
        }

        private void Spread(long cell, float size, in WindSampler sampler)
        {
            float2 wind = math.normalizesafe(sampler.Sample(CellCenter(cell)));
            float reach = FireMath.SpreadReach(size, _rules);
            int attempts = FireMath.SpreadAttempts(size, _rules);
            int x = Position2Int.GetX(cell);
            int z = Position2Int.GetY(cell);

            for (int a = 0; a < attempts; a++)
            {
                if (_burning.Count >= _rules.maxBurning) return;

                math.sincos(RavineRandom.RangeFloat() * (2f * math.PI), out float s, out float c);
                float distance = math.max(1f, reach * math.sqrt(RavineRandom.RangeFloat()));
                int2 offset = (int2)math.round(new float2(c, s) * distance);
                if (offset.x == 0 && offset.y == 0) continue;

                long target = Position2Int.Pack(x + offset.x, z + offset.y);
                if (_burning.ContainsKey(target)) continue;
                if (!TryResolve(target, out long primary, out float targetSize)) continue;
                if (primary != target && _burning.ContainsKey(primary)) continue;

                float2 dir = offset;
                float length = math.length(dir);
                float chance = _rules.spreadChance * FireMath.SpreadFalloff(length, reach)
                             * math.max(0f, 1f + _rules.windBias * math.dot(wind, dir / length));
                if (RavineRandom.RangeFloat() < chance) Ignite(primary, targetSize);
            }
        }

        public float HeatDamageAt(float2 p)
        {
            if (_disposed || _burning.IsEmpty) return 0f;
            float4 b = _heatBounds;
            if (p.x < b.x || p.y < b.y || p.x > b.z || p.y > b.w) return 0f;

            int cx = (int)math.floor(p.x * InvScale);
            int cz = (int)math.floor(p.y * InvScale);
            float best = 0f;

            for (int dz = -_heatReach; dz <= _heatReach; dz++)
            for (int dx = -_heatReach; dx <= _heatReach; dx++)
            {
                long cell = Position2Int.Pack(cx + dx, cz + dz);
                if (!_burning.TryGetValue(cell, out FireCell fire)) continue;

                float cover = FireMath.HeatCover(math.distance(p, CellCenter(cell)), FireMath.HeatRadius(fire.Size, _rules));
                if (cover <= best) continue;
                best = cover;
                if (best >= 1f) return _rules.damagePerSecond;
            }

            return best * _rules.damagePerSecond;
        }

        public static long CellOf(float wx, float wz)
            => Position2Int.Pack((int)math.floor(wx * InvScale), (int)math.floor(wz * InvScale));

        public static float2 CellCenter(long cell)
            => new(Position2Int.GetX(cell) * MapGenerator.scale + HalfCell,
                   Position2Int.GetY(cell) * MapGenerator.scale + HalfCell);

        public void Render(Camera camera) => _view?.Render(camera);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _view?.Dispose();
            _view = null;
            if (_burning.IsCreated) _burning.Dispose();
            if (_keys.IsCreated) _keys.Dispose();
            if (_windFallback.IsCreated) _windFallback.Dispose();
            _wind = null;
        }
    }
}

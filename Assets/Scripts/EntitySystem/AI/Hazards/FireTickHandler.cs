using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

using TheRavine.Extensions;
using TheRavine.Generator;
using TheRavine.ObjectControl;

public sealed class FireTickHandler : IRandomTickHandler, IDisposable
{
    private static readonly int2[] NeighbourSteps =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
        new(1, 1), new(-1, 1), new(1, -1), new(-1, -1),
    };

    private static readonly float2[] NeighbourDirs =
    {
        new(1f, 0f), new(-1f, 0f), new(0f, 1f), new(0f, -1f),
        new(0.70710678f, 0.70710678f), new(-0.70710678f, 0.70710678f),
        new(0.70710678f, -0.70710678f), new(-0.70710678f, -0.70710678f),
    };

    private readonly MapGenerator _generator;
    private readonly ObjectInfoRegistry _registry;
    private readonly ChunkFoodIndex _food;

    private NativeHashMap<long, float> _burning;
    private NativeList<long> _keys;
    private NativeArray<float2> _windFallback;
    private WindField _wind;
    private float _accumulator;
    private bool _disposed;

    public int BurningCount => _burning.IsCreated ? _burning.Count : 0;

    public FireTickHandler(MapGenerator generator, ObjectInfoRegistry registry, ChunkFoodIndex food)
    {
        _generator = generator;
        _registry  = registry;
        _food      = food;

        int capacity = math.max(SimulationRules.Frame.FireMaxBurning, 16);
        _burning      = new NativeHashMap<long, float>(capacity, Allocator.Persistent);
        _keys         = new NativeList<long>(capacity, Allocator.Persistent);
        _windFallback = new NativeArray<float2>(1, Allocator.Persistent);
    }

    public bool Accepts(int prefabId)
    {
        ObjectInfo info = _registry.Get(prefabId);
        return info != null && info.IsFood && info.FoodKind == FoodKind.Plant;
    }

    public void OnRandomTick(in RandomTickEvent e, double now)
    {
        if (_disposed) return;

        ref readonly var r = ref SimulationRules.Frame;
        float season = SeasonFactor((float)now, in r);
        if (season <= 0f || _burning.Count >= r.FireMaxBurning) return;

        int worldX = ((Position2Int.GetX(e.Chunk) << MapGenerator.RowShift) + (e.Cell & MapGenerator.RowMask)) << MapGenerator.CellShift;
        int worldZ = ((Position2Int.GetY(e.Chunk) << MapGenerator.RowShift) + (e.Cell >> MapGenerator.RowShift)) << MapGenerator.CellShift;
        long cell = Position2Int.Pack(worldX >> MapGenerator.CellShift, worldZ >> MapGenerator.CellShift);
        if (_burning.ContainsKey(cell)) return;

        float chance = r.FireIgnitionChance * r.FoodFlammability(FoodKind.Plant) * season;
        if (RavineRandom.RangeFloat() >= chance) return;

        long packed = Position2Int.Pack(worldX, worldZ);
        if (_generator.Commands.HasPendingRemove(packed)
            || !_generator.TryGetObject(packed, out ObjectInstInfo inst)
            || inst.PrefabID != e.PrefabId) return;

        _burning.TryAdd(cell, r.FireBurnSeconds);
    }

    public void Step(double now, float dt, List<EntityModel> entities)
    {
        if (_disposed) return;
        if (_burning.IsEmpty)
        {
            _accumulator = 0f;
            return;
        }

        ref readonly var r = ref SimulationRules.Frame;
        _accumulator += dt;
        if (_accumulator < r.FireStepInterval) return;
        float step = _accumulator;
        _accumulator = 0f;

        if (_wind == null) ServiceLocator.Services.TryGet(out _wind);
        WindSampler sampler = _wind != null
            ? _wind.GetSampler((float)now, _windFallback)
            : WindField.Constant(float2.zero, 0f, (float)now, _windFallback);

        _keys.Clear();
        foreach (var kv in _burning) _keys.Add(kv.Key);

        WorldCommandBuffer commands = _generator.Commands;
        float margin = MapGenerator.scale * 1.5f;
        float4 bounds = new float4(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);
        for (int k = 0; k < _keys.Length; k++)
        {
            long cell = _keys[k];
            if (!_burning.TryGetValue(cell, out float left)) continue;

            long packed = Position2Int.Pack(
                Position2Int.GetX(cell) << MapGenerator.CellShift,
                Position2Int.GetY(cell) << MapGenerator.CellShift);

            if (commands.HasPendingRemove(packed)
                || !_generator.TryGetObject(packed, out ObjectInstInfo inst)
                || !IsFlammable(inst.PrefabID, in r))
            {
                _burning.Remove(cell);
                continue;
            }

            left -= step;
            if (left <= 0f)
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

            _burning[cell] = left;
            Spread(cell, in sampler, in r);

            float2 center = ChunkFoodIndex.CellCenter(cell);
            bounds.xy = math.min(bounds.xy, center - margin);
            bounds.zw = math.max(bounds.zw, center + margin);
        }

        if (bounds.x <= bounds.z) Scorch(entities, step, bounds, in r);
    }

    private void Spread(long cell, in WindSampler sampler, in SimulationRules.RulesFrame r)
    {
        float2 wind = math.normalizesafe(sampler.Sample(ChunkFoodIndex.CellCenter(cell)));
        int x = Position2Int.GetX(cell);
        int z = Position2Int.GetY(cell);

        for (int i = 0; i < NeighbourSteps.Length; i++)
        {
            if (_burning.Count >= r.FireMaxBurning) return;

            long neighbour = Position2Int.Pack(x + NeighbourSteps[i].x, z + NeighbourSteps[i].y);
            if (_burning.ContainsKey(neighbour)) continue;
            if (!_food.TryGetKind(neighbour, out FoodKind kind, out _)) continue;

            float flammability = r.FoodFlammability(kind);
            if (flammability <= 0f) continue;

            float chance = r.FireSpreadChance * flammability
                         * math.max(0f, 1f + r.FireWindBias * math.dot(wind, NeighbourDirs[i]));
            if (RavineRandom.RangeFloat() < chance) _burning.TryAdd(neighbour, r.FireBurnSeconds);
        }
    }

    private void Scorch(List<EntityModel> entities, float step, float4 bounds, in SimulationRules.RulesFrame r)
    {
        if (entities == null) return;

        float damage = r.FireDamagePerSecond * step;
        for (int i = 0; i < entities.Count; i++)
        {
            EntityModel e = entities[i];
            if (e.IsDisposed || e.IsDeathPending) continue;

            float2 p = e.Position2D;
            if (p.x < bounds.x || p.y < bounds.y || p.x > bounds.z || p.y > bounds.w) continue;
            if (!_burning.ContainsKey(ChunkFoodIndex.CellOf(p.x, p.y))) continue;
            e.TakeDamage(damage, null, DeathCause.Burn);
        }
    }

    private bool IsFlammable(int prefabId, in SimulationRules.RulesFrame r)
    {
        ObjectInfo info = _registry.Get(prefabId);
        return info != null && info.IsFood && r.FoodFlammability(info.FoodKind) > 0f;
    }

    private static float SeasonFactor(float time, in SimulationRules.RulesFrame r)
        => SeasonClock.IsWinter(time) ? 0f : math.max(0f, 1f + r.FireSeasonBias * SeasonClock.Value(time));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_burning.IsCreated) _burning.Dispose();
        if (_keys.IsCreated) _keys.Dispose();
        if (_windFallback.IsCreated) _windFallback.Dispose();
        _wind = null;
    }
}

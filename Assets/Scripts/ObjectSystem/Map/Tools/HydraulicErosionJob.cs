using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace TheRavine.Generator
{
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct HydraulicErosionJob : IJob
    {
        [ReadOnly] public int mapSize;
        [ReadOnly] public int seed;
        [ReadOnly] public ErosionSettings settings;
        public NativeArray<float> heightMap;

        private const float MinDirection = 0.0001f;

        public void Execute()
        {
            Random rng = new Random((uint)math.max(seed, 1));

            NativeArray<float> deltaMap = new NativeArray<float>(heightMap.Length, Allocator.Temp);

            for (int i = 0; i < settings.dropletCount; i++)
            {
                SimulateDroplet(ref rng, ref deltaMap);
            }

            for (int i = 0; i < heightMap.Length; i++)
            {
                heightMap[i] += deltaMap[i] * settings.amplify;
            }

            deltaMap.Dispose();
        }

        private void SimulateDroplet(ref Random rng, ref NativeArray<float> deltaMap)
        {
            float2 pos = new float2(
                rng.NextFloat(1, mapSize - 1),
                rng.NextFloat(1, mapSize - 1));

            float2 dir = 0;

            float speed = 1f;
            float water = 1f;
            float sediment = 0f;

            for (int i = 0; i < settings.lifetime; i++)
            {
                HeightSample current = Sample(pos, ref deltaMap);

                float2 grad = current.gradient;

                dir = dir * settings.inertia - grad * (1f - settings.inertia);

                float len = math.length(dir);
                if (len < MinDirection)
                    break;

                dir /= len;

                float2 nextPos = pos + dir;

                if (!IsInside(nextPos))
                    break;

                HeightSample next = Sample(nextPos, ref deltaMap);

                float deltaHeight = next.height - current.height;

                float capacity = math.max(-deltaHeight, settings.minSlope) * speed * water * settings.sedimentCapacity;

                if (sediment > capacity)
                {
                    float amount = (sediment - capacity) * settings.depositSpeed;
                    sediment -= amount;
                    Deposit(ref deltaMap, pos, amount);
                }
                else
                {
                    float erosion = math.min((capacity - sediment) * settings.erodeSpeed, -deltaHeight);

                    if (erosion > 0f)
                    {
                        ApplyErosion(ref deltaMap, pos, erosion);
                        sediment += erosion;
                    }
                }

                speed = math.sqrt(math.max(0.0001f, speed * speed - deltaHeight * settings.gravity));
                water *= 1f - settings.evaporation;

                pos = nextPos;

                if (water < 0.0001f)
                    break;
            }
        }

        private void ApplyErosion(ref NativeArray<float> deltaMap, float2 pos, float amount)
        {
            int r = settings.radius;
            float invR2 = math.rsqrt(r);
            
            int cx = (int)pos.x;
            int cy = (int)pos.y;

            float r2 = r * r;
            float total = 0f;

            for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
            {
                float d2 = x * x + y * y;
                if (d2 > r2) continue;
                total += 1f - d2 * invR2;
            }

            if (total <= 0f)
                return;

            for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
            {
                int px = cx + x;
                int py = cy + y;

                if (!IsInside(px, py))
                    continue;

                float d2 = x * x + y * y;
                if (d2 > r2)
                    continue;

                float w = 1f - d2 * invR2;

                int idx = Idx(px, py);
                float currentHeight = heightMap[idx] + deltaMap[idx];

                if (!settings.allowInfiniteErosionDepth)
                {
                    float maxRemove = math.max(0.05f, currentHeight - 100);
                    float remove = math.min(amount * w, maxRemove);
                    deltaMap[idx] -= remove;
                }
                else
                {
                    deltaMap[idx] -= amount * w;
                }
            }
        }

        private void Deposit(ref NativeArray<float> deltaMap, float2 pos, float amount)
        {
            int x = (int)pos.x;
            int y = (int)pos.y;

            float u = pos.x - x;
            float v = pos.y - y;

            Add(deltaMap, x, y, amount * (1 - u) * (1 - v));
            Add(deltaMap, x + 1, y, amount * u * (1 - v));
            Add(deltaMap, x, y + 1, amount * (1 - u) * v);
            Add(deltaMap, x + 1, y + 1, amount * u * v);
        }

        private HeightSample Sample(float2 pos, ref NativeArray<float> deltaMap)
        {
            int x = (int)pos.x;
            int y = (int)pos.y;

            float u = pos.x - x;
            float v = pos.y - y;

            float h00 = GetHeight(x, y, ref deltaMap);
            float h10 = GetHeight(x + 1, y, ref deltaMap);
            float h01 = GetHeight(x, y + 1, ref deltaMap);
            float h11 = GetHeight(x + 1, y + 1, ref deltaMap);

            float height = math.lerp(math.lerp(h00, h10, u), math.lerp(h01, h11, u), v);

            float gradX = math.lerp(h10 - h00, h11 - h01, v);
            float gradY = math.lerp(h01 - h00, h11 - h10, u);

            return new HeightSample { height = height, gradient = new float2(gradX, gradY) };
        }

        private float GetHeight(int x, int y, ref NativeArray<float> deltaMap)
        {
            return heightMap[Idx(x, y)] + deltaMap[Idx(x, y)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Add(NativeArray<float> map, int x, int y, float v)
        {
            if (!IsInside(x, y)) return;
            map[Idx(x, y)] += v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsInside(float2 p)
        {
            return p.x >= 0 && p.y >= 0 && p.x < mapSize - 1 && p.y < mapSize - 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsInside(int x, int y)
        {
            return x >= 0 && y >= 0 && x < mapSize && y < mapSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Idx(int x, int y)
        {
            return y * mapSize + x;
        }

        private struct HeightSample
        {
            public float height;
            public float2 gradient;
        }
    }
}
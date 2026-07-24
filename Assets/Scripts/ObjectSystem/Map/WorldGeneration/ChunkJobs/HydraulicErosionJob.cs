using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace TheRavine.Generator
{
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, DisableSafetyChecks = true)]
    public struct HydraulicErosionJob : IJob
    {
        [ReadOnly] public uint seed;
        [ReadOnly] public ErosionSettings settings;
        public NativeArray<float> heightMap, deltaMap;

        private const float MinDirection = 0.0001f;

        public void Execute()
        {
            Random rng = new(math.max(seed, 1));

            for (int i = 0; i < deltaMap.Length; i++)
            {
                deltaMap[i] = 0;
            }

            for (int i = 0; i < settings.dropletCount; i++)
            {
                SimulateDroplet(ref rng);
            }

            for (int i = 0; i < heightMap.Length; i++)
            {
                heightMap[i] += deltaMap[i] * settings.amplify;
            }
        }

        private void SimulateDroplet(ref Random rng)
        {
            float2 pos = new(
                rng.NextFloat(1, MapGenerator.mapChunkSize - 1),
                rng.NextFloat(1, MapGenerator.mapChunkSize - 1));

            float2 dir = 0;

            float speed = 1f;
            float water = 1f;
            float sediment = 0f;

            for (int i = 0; i < settings.lifetime; i++)
            {
                HeightSample current = Sample(pos);

                float2 grad = current.gradient;

                dir = dir * settings.inertia - grad * (1f - settings.inertia);

                float len = math.length(dir);
                if (len < MinDirection)
                    break;

                dir /= len;

                float2 nextPos = pos + dir;

                if (!IsInside(nextPos))
                    break;

                HeightSample next = Sample(nextPos);

                float deltaHeight = next.height - current.height;

                float capacity = math.max(-deltaHeight, settings.minSlope) * speed * water * settings.sedimentCapacity;

                if (sediment > capacity)
                {
                    float amount = (sediment - capacity) * settings.depositSpeed;
                    sediment -= amount;
                    Deposit(pos, amount);
                }
                else
                {
                    float erosion = math.min((capacity - sediment) * settings.erodeSpeed, -deltaHeight);

                    if (erosion > 0f)
                    {
                        ApplyErosion(pos, erosion);
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

        private void ApplyErosion(float2 pos, float amount)
        {
            float r = settings.radius;
            int cx = (int)pos.x;
            int cy = (int)pos.y;

            int xStart = math.max(cx - (int)r, 0);
            int xEnd = math.min(cx + (int)r, MapGenerator.mapChunkSize - 1);
            int yStart = math.max(cy - (int)r, 0);
            int yEnd = math.min(cy + (int)r, MapGenerator.mapChunkSize - 1);

            float r2 = r * r;
            float invR2 = 1f / r2;

            for (int y = yStart; y <= yEnd; y++)
            {
                for (int x = xStart; x <= xEnd; x++)
                {
                    int dx = x - cx;
                    int dy = y - cy;
                    float d2 = dx * dx + dy * dy;
                    
                    if (d2 > r2) continue;

                    float w = 1f - d2 * invR2;
                    int idx = y * MapGenerator.mapChunkSize + x;
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
        }

        private void Deposit(float2 pos, float amount)
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

        private HeightSample Sample(float2 pos)
        {
            int x = (int)pos.x;
            int y = (int)pos.y;

            float u = pos.x - x;
            float v = pos.y - y;

            float h00 = GetHeight(x, y);
            float h10 = GetHeight(x + 1, y);
            float h01 = GetHeight(x, y + 1);
            float h11 = GetHeight(x + 1, y + 1);

            float height = math.lerp(math.lerp(h00, h10, u), math.lerp(h01, h11, u), v);

            float gradX = math.lerp(h10 - h00, h11 - h01, v);
            float gradY = math.lerp(h01 - h00, h11 - h10, u);

            return new HeightSample { height = height, gradient = new float2(gradX, gradY) };
        }

        private float GetHeight(int x, int y)
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
            return p.x >= 0 && p.y >= 0 && p.x < MapGenerator.mapChunkSize - 1 && p.y < MapGenerator.mapChunkSize - 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsInside(int x, int y)
        {
            return x >= 0 && y >= 0 && x < MapGenerator.mapChunkSize && y < MapGenerator.mapChunkSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Idx(int x, int y)
        {
            return y * MapGenerator.mapChunkSize + x;
        }

        private struct HeightSample
        {
            public float height;
            public float2 gradient;
        }
    }
}


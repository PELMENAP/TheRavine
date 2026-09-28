using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace TheRavine.Generator
{
    public unsafe struct HeightWindow
    {
        public const int Radius = MapGenerator.chunkScale + 1;
        public const int Side = 2 * Radius + 1;
        public const int Cells = Side * MapGenerator.mapChunkSize;
        public const int MeshOrigin = (Radius - MapGenerator.chunkScale) * MapGenerator.mapChunkSize;

        private fixed long heights[Side * Side];
        private fixed long climates[Side * Side];
        private fixed long rivers[Side * Side];

        public void Set(int x, int z, ChunkData chunk)
        {
            int slot = z * Side + x;
            heights[slot] = (long)chunk.HeightRaw.GetUnsafeReadOnlyPtr();
            climates[slot] = (long)chunk.Climate.GetUnsafeReadOnlyPtr();
            rivers[slot] = (long)chunk.RiverBlend.GetUnsafeReadOnlyPtr();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Locate(ref int cellX, ref int cellZ, out int slot, out int local)
        {
            cellX = math.clamp(cellX, 0, Cells - 1);
            cellZ = math.clamp(cellZ, 0, Cells - 1);
            slot = (cellZ >> MapGenerator.RowShift) * Side + (cellX >> MapGenerator.RowShift);
            local = ((cellZ & MapGenerator.RowMask) << MapGenerator.RowShift) | (cellX & MapGenerator.RowMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Sample(int cellX, int cellZ)
        {
            Locate(ref cellX, ref cellZ, out int slot, out int local);
            return ((float*)heights[slot])[local];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float2 SampleClimate(int cellX, int cellZ)
        {
            Locate(ref cellX, ref cellZ, out int slot, out int local);
            return ((float2*)climates[slot])[local];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float SampleRiver(int cellX, int cellZ)
        {
            Locate(ref cellX, ref cellZ, out int slot, out int local);
            return ((float*)rivers[slot])[local];
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TerrainVertex
    {
        public float3 Position;
        public float3 Normal;
        public float2 Climate;
        public float River;
    }

    public static class TerrainVertexLayout
    {
        public static readonly VertexAttributeDescriptor[] Attributes =
        {
            new(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 2),
            new(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 1),
        };
    }

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    public struct TerrainVertexJob : IJobFor
    {
        public HeightWindow Heights;
        public int Step;
        public int Verts;
        public float CellSize;
        public float SkirtDepth;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<TerrainVertex> Vertices;

        public void Execute(int row)
        {
            if (row < Verts)
            {
                int rowStart = row * Verts;
                for (int x = 0; x < Verts; x++)
                    Vertices[rowStart + x] = Build(x, row);
                return;
            }

            int side = row - Verts;
            int last = Verts - 1;
            int skirtStart = Verts * Verts + side * Verts;

            for (int k = 0; k < Verts; k++)
            {
                int x = side < 2 ? k : (side == 2 ? 0 : last);
                int z = side < 2 ? (side == 0 ? 0 : last) : k;

                TerrainVertex v = Build(x, z);
                v.Position.y -= SkirtDepth;
                Vertices[skirtStart + k] = v;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TerrainVertex Build(int x, int z)
        {
            int cx = HeightWindow.MeshOrigin + x * Step;
            int cz = HeightWindow.MeshOrigin + z * Step;

            float h = Heights.Sample(cx, cz);
            float hl = Heights.Sample(cx - Step, cz);
            float hr = Heights.Sample(cx + Step, cz);
            float hd = Heights.Sample(cx, cz - Step);
            float hu = Heights.Sample(cx, cz + Step);

            float span = Step * CellSize;

            return new TerrainVertex
            {
                Position = new float3(x * span, h, z * span),
                Normal = math.normalize(new float3(hl - hr, 2f * span, hd - hu)),
                Climate = Heights.SampleClimate(cx, cz),
                River = Heights.SampleRiver(cx, cz)
            };
        }
    }

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    public struct GatherHeightsJob : IJobFor
    {
        public HeightWindow Heights;
        public int Resolution;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<float> Output;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<float2> ClimateOutput;

        public void Execute(int z)
        {
            int rowStart = z * Resolution;
            int cz = HeightWindow.MeshOrigin + z;
            for (int x = 0; x < Resolution; x++)
            {
                int cx = HeightWindow.MeshOrigin + x;
                Output[rowStart + x] = Heights.Sample(cx, cz);
                ClimateOutput[rowStart + x] = Heights.SampleClimate(cx, cz);
            }
        }
    }

    public static class TerrainIndexBuilder
    {
        public static int VertexCount(int verts) => verts * verts + 4 * verts;

        public static NativeArray<ushort> Build(int verts, Allocator allocator)
        {
            int quads = verts - 1;
            int count = quads * quads * 6 + 4 * quads * 6;
            var indices = new NativeArray<ushort>(count, allocator, NativeArrayOptions.UninitializedMemory);

            int w = 0;
            for (int z = 0; z < quads; z++)
            {
                for (int x = 0; x < quads; x++)
                {
                    int bl = z * verts + x;
                    int br = bl + 1;
                    int tl = bl + verts;
                    int tr = tl + 1;

                    indices[w++] = (ushort)bl;
                    indices[w++] = (ushort)tl;
                    indices[w++] = (ushort)tr;

                    indices[w++] = (ushort)bl;
                    indices[w++] = (ushort)tr;
                    indices[w++] = (ushort)br;
                }
            }

            int last = verts - 1;
            int skirtBase = verts * verts;

            for (int side = 0; side < 4; side++)
            {
                int skirtStart = skirtBase + side * verts;
                bool outwardForward = side == 0 || side == 3;

                for (int k = 0; k < quads; k++)
                {
                    int a = EdgeVertex(side, k, verts, last);
                    int b = EdgeVertex(side, k + 1, verts, last);
                    int sa = skirtStart + k;
                    int sb = sa + 1;

                    if (outwardForward)
                    {
                        indices[w++] = (ushort)a;
                        indices[w++] = (ushort)b;
                        indices[w++] = (ushort)sb;

                        indices[w++] = (ushort)a;
                        indices[w++] = (ushort)sb;
                        indices[w++] = (ushort)sa;
                    }
                    else
                    {
                        indices[w++] = (ushort)b;
                        indices[w++] = (ushort)a;
                        indices[w++] = (ushort)sa;

                        indices[w++] = (ushort)b;
                        indices[w++] = (ushort)sa;
                        indices[w++] = (ushort)sb;
                    }
                }
            }

            return indices;
        }

        private static int EdgeVertex(int side, int k, int verts, int last) => side switch
        {
            0 => k,
            1 => last * verts + k,
            2 => k * verts,
            _ => k * verts + last
        };
    }
}

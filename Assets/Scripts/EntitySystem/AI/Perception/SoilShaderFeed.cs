using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using TheRavine.Extensions;
using TheRavine.Generator;

public sealed class SoilShaderFeed : IDisposable
{
    public const int Side  = GrassSoilBuffer.Side;
    public const int Cells = GrassSoilBuffer.Cells;
    private const int Half = Side / 2;

    private NativeArray<float4> _cells;

    public SoilShaderFeed()
    {
        _cells = new NativeArray<float4>(Cells, Allocator.Persistent);
    }

    public void Update(ChunkFoodIndex index, MapGenerator map)
    {
        if (index != null && map != null && map.TryGetViewerChunk(out int cx, out int cz))
            Publish(index, cx, cz);
        else
            GrassSoilBuffer.Disable();
    }

    public void Publish(ChunkFoodIndex index, int centerX, int centerZ)
    {
        if (!_cells.IsCreated) return;

        for (int z = 0; z < Side; z++)
        {
            for (int x = 0; x < Side; x++)
            {
                long key = Position2Int.Pack(centerX + x - Half, centerZ + z - Half);
                _cells[z * Side + x] = index.TryGetSoil(key, out float fertility, out float depletion, out float energy)
                    ? new float4(1f - depletion, energy, fertility, 1f)
                    : new float4(1f, 0f, 0f, 0f);
            }
        }

        GrassSoilBuffer.Publish(_cells, new Vector4(
            (centerX - Half) * MapGenerator.chunkSize,
            (centerZ - Half) * MapGenerator.chunkSize,
            MapGenerator.chunkSize,
            MapGenerator.InvChunkSize));
    }

    public void Dispose()
    {
        GrassSoilBuffer.Disable();
        if (_cells.IsCreated) _cells.Dispose();
    }
}

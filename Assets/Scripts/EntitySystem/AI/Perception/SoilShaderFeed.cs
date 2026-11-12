using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using TheRavine.Extensions;
using TheRavine.Generator;

public sealed class SoilShaderFeed : IDisposable
{
    public const int Side  = 5;
    public const int Cells = Side * Side;
    private const int Half = Side / 2;

    private static readonly int BufferId  = Shader.PropertyToID("_SoilBuffer");
    private static readonly int OriginId  = Shader.PropertyToID("_SoilOrigin");
    private static readonly int EnabledId = Shader.PropertyToID("_SoilEnabled");

    private NativeArray<float4> _cells;
    private GraphicsBuffer _buffer;

    public SoilShaderFeed()
    {
        _cells  = new NativeArray<float4>(Cells, Allocator.Persistent);
        _buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Cells, sizeof(float) * 4);
    }

    public void Update(ChunkFoodIndex index, MapGenerator map)
    {
        if (index != null && map != null && map.TryGetViewerChunk(out int cx, out int cz))
            Publish(index, cx, cz);
        else
            Shader.SetGlobalFloat(EnabledId, 0f);
    }

    public void Publish(ChunkFoodIndex index, int centerX, int centerZ)
    {
        if (!_cells.IsCreated || _buffer == null) return;

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

        _buffer.SetData(_cells);
        Shader.SetGlobalBuffer(BufferId, _buffer);
        Shader.SetGlobalVector(OriginId, new Vector4(
            (centerX - Half) * MapGenerator.chunkSize,
            (centerZ - Half) * MapGenerator.chunkSize,
            MapGenerator.chunkSize,
            MapGenerator.InvChunkSize));
        Shader.SetGlobalFloat(EnabledId, 1f);
    }

    public void Dispose()
    {
        Shader.SetGlobalFloat(EnabledId, 0f);
        _buffer?.Release();
        _buffer = null;
        if (_cells.IsCreated) _cells.Dispose();
    }
}

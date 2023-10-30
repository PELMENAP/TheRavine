using System.Collections.Generic;
using UnityEngine;

public class EndlessObjects : IEndless
{
    private MapGenerator generator;
    private int chunkCount => MapGenerator.chunkCount;
    private int mapChunkSize => MapGenerator.mapChunkSize;
    public EndlessObjects(MapGenerator _generator)
    {
        generator = _generator;
    }
    public void UpdateChunk(Vector3 Vposition)
    {

    }

}

using System.Collections.Generic;
using UnityEngine;

public class EndlessObjects : IEndless
{
    private MapGenerator generator;
    private const int chunkCount = MapGenerator.chunkCount;
    private Vector2 vectorOffset = MapGenerator.vectorOffset;
    private List<Vector2> terrainChunksVisibleUpdate = new List<Vector2>(chunkCount * chunkCount);
    private List<Vector2> terrainChunksVisibleLastUpdate = new List<Vector2>(chunkCount * chunkCount);
    public EndlessObjects(MapGenerator _generator)
    {
        generator = _generator;
    }
    private int currentChunkCoordX, currentChunkCoordY;
    private Dictionary<Vector2, int> map;
    public void UpdateChunk(Vector3 Vposition)
    {
        currentChunkCoordX = Mathf.RoundToInt(Vposition.x);
        currentChunkCoordY = Mathf.RoundToInt(Vposition.y);
        int countOfIteration = 0;
        for (int yOffset = 0; yOffset < chunkCount; yOffset++)
        {
            for (int xOffset = 0; xOffset < chunkCount; xOffset++)
            {
                Vector2 chunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);
                terrainChunksVisibleUpdate.Add(chunkCoord);
                // if (terrainChunksVisibleLastUpdate.Contains(chunkCoord))
                //     continue;
                map = generator.GetMapData(new Vector2(-chunkCoord.x, chunkCoord.y)).objectsToInst;
                foreach (var item in map)
                {
                    if (ObjectSystem.inst.Changed(item.Key))
                        continue;
                    ObjectSystem.inst.Reuse(item.Value, item.Key);
                    // ObjectSystem.inst.PoolManagerBase.Reuse(item.Value, posobj);
                    // ObjectSystem.inst.InstantiatePoolByPosition(posobj, item.Value);
                }
                countOfIteration++;
            }
        }
        terrainChunksVisibleLastUpdate.Clear();
        terrainChunksVisibleLastUpdate.AddRange(terrainChunksVisibleUpdate.ToArray());
        terrainChunksVisibleUpdate.Clear();
        Debug.Log(countOfIteration);
    }

    private Texture2D texture;
    private Texture2D TextureFromColourMap(Color[] colourMap, int width, int height)
    {
        texture = new Texture2D(width, height);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(colourMap);
        texture.Apply();
        return texture;
    }
}
